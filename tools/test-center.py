"""Run, verify and report NexSpace development tests from one catalog."""

import argparse
from contextlib import nullcontext
from datetime import UTC, datetime, timedelta
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import uuid

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS.parent / "prototypes/web-shell"))
import pilot_support as pilot

REPO = pilot.REPO
PILOT = pilot.STORAGE
STORAGE = REPO / "artifacts/test-center"
CATALOG = TOOLS / "test-center/catalog.json"
ACCEPTED = TOOLS / "test-center/accepted-scenarios.json"
CODES = {"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3, "NOT_TESTED": 4}
PRIORITY = ("ERROR", "FAIL", "BLOCKED", "NOT_TESTED", "PASS")
METADATA = ("buildId", "gitSha", "dirty", "hostPid", "sidecarPid", "profile", "group", "database", "sourceFolder")


def now():
    return datetime.now(UTC).isoformat(timespec="milliseconds")


def period(value):
    date = datetime.fromisoformat(value)
    return date.replace(tzinfo=UTC) if date.tzinfo is None else date.astimezone(UTC)


def print_json(data):
    print(json.dumps(data, ensure_ascii=False, indent=2))


def last_message(path):
    lines = path.read_text(encoding="utf-8-sig", errors="replace").strip().splitlines()
    return lines[-1].strip() if lines else ""


def run_command(argv, folder, phase, timeout):
    """Capture one script's output. A timeout cleans up only its own process tree."""
    stdout, stderr = folder / f"{phase}.stdout.log", folder / f"{phase}.stderr.log"
    env = os.environ.copy()
    for name in ("NEXSPACE_PILOT_SESSION_ID", "NEXSPACE_FLOATING_TEST_DIR", "NEXSPACE_FLOATING_DROP_TRACE",
                 "NEXSPACE_FLOATING_TRAY_TRACE"):
        env.pop(name, None)
    with stdout.open("wb") as out, stderr.open("wb") as err:
        with subprocess.Popen(argv, cwd=REPO, env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err) as child:
            try:
                code = child.wait(timeout=timeout)
            except subprocess.TimeoutExpired as error:
                if child.poll() is None:
                    subprocess.run(["taskkill.exe", "/PID", str(child.pid), "/T", "/F"],
                                   stdout=out, stderr=err, check=False, timeout=15)
                    if child.poll() is None:
                        child.kill()
                raise TimeoutError(f"{phase} agotó el plazo de {timeout:g} s.") from error
    return code, stdout, last_message(stderr) or last_message(stdout)


class Center:
    def __init__(self, storage=STORAGE, pilot_storage=PILOT):
        self.storage = storage
        self.pilot_storage = pilot_storage
        catalog = pilot.read_json(CATALOG)
        self.tests = catalog["tests"]
        self.suites = catalog["suites"]

    def folder(self, identity):
        pilot.session_path(identity)  # Reuse the managed pilot's ID validation.
        return self.storage / "sessions" / identity

    def session(self, identity, *, test=None, suite=None, source=True):
        folder = self.folder(identity)
        folder.mkdir(parents=True, exist_ok=False)
        data = {key: None for key in METADATA}
        data.update(id=identity, testId=test, suiteId=suite, startedUtc=now(), finishedUtc=None,
                    evidenceDirectory=str(folder), result="result.json", status="NOT_TESTED")
        if source:
            try:
                revision = pilot.source_identity()
                data.update(gitSha=revision["commit"], dirty=revision["dirty"])
            except (OSError, subprocess.SubprocessError) as error:
                data["sourceError"] = str(error)
        pilot.write_json(folder / "session.json", data)
        return folder, data

    def save(self, folder, session, status, reason, criteria=(), *, action=None, details=None, outcomes=None):
        finished = now()
        result = dict(session=session["id"], status=status, reason=reason, finishedUtc=finished,
                      durationSeconds=round((period(finished) - period(session["startedUtc"])).total_seconds(), 3),
                      approvedCriteria=list(criteria) if status == "PASS" else [],
                      failedCriteria=[reason] if status == "FAIL" else [],
                      blockers=[reason] if status == "BLOCKED" else [],
                      nextAction=action, details=details)
        if outcomes is not None:
            result.update(outcomes)
        session.update(status=status, finishedUtc=finished)
        attempts = folder / "history"
        attempts.mkdir(exist_ok=True)
        pilot.write_json(attempts / f"{uuid.uuid4().hex}.json", result)
        pilot.write_json(folder / "session.json", session)
        pilot.write_json(folder / "result.json", result)
        return {**session, **result}

    def build_data(self, session, manifest):
        session.update(buildId=manifest["id"], gitSha=manifest["commit"], dirty=manifest["dirty"])

    def managed(self, identity):
        """Bind imported and new sessions to the existing launcher receipt and ready marker."""
        root = pilot.session_path(identity)
        receipt_file = pilot.receipt_path(self.pilot_storage, identity)
        receipt = pilot.read_json(receipt_file)
        ready = pilot.read_json(root / "evidence/ready.json")
        database = root / "floating.db"
        pilot.require(receipt["id"] == ready["session"] == identity and receipt["build"] == ready["build"]
                      and receipt["process"][0] == ready["hostPid"]
                      and pilot.same_path(receipt["executable"], ready["executable"])
                      and pilot.same_path(receipt["evidence"], root / "evidence")
                      and pilot.same_path(receipt["database"], database)
                      and pilot.same_path(ready["database"], database), "Recibo y host de ensayo incoherentes.")
        data = dict(buildId=receipt["build"], gitSha=receipt["commit"], hostPid=ready["hostPid"],
                    sidecarPid=ready["corePid"], profile=ready["profile"], database=str(database),
                    startedUtc=(datetime(1601, 1, 1, tzinfo=UTC) +
                                timedelta(microseconds=int(receipt["process"][1]) // 10)).isoformat(),
                    pilotEvidence=str(root / "evidence"), pilotReceipt=str(receipt_file))
        if receipt.get("trial") in ("windows-tray", "windows-explorer"):
            data["trayTrace"] = str(root / "evidence/tray.jsonl")
        if receipt.get("trial") == "windows-explorer":
            data["sourceFolder"] = str(pilot.explorer_folder(root, identity))
        if receipt.get("trial") == "windows-accessibility":
            data["uiaEvidence"] = str(self.folder(identity) / "uia-initial.json")
        manifest = Path(receipt["executable"]).parent / "build.json"
        if manifest.is_file():
            build = pilot.read_json(manifest)
            pilot.require(build["id"] == receipt["build"] and build["commit"] == receipt["commit"],
                          "El manifiesto no corresponde al recibo.")
            data.update(dirty=build["dirty"], buildManifest=str(manifest))
        trial_file = root / "drop-trial.json"
        if trial_file.is_file():
            trial = pilot.read_json(trial_file)
            pilot.require(trial["session"] == identity and trial["profile"] == ready["profile"]
                          and any(group["id"] == trial["group"] for group in ready["groups"]),
                          "La identidad de la prueba OLE no coincide con el host.")
            data.update(group=trial["group"], sourceFolder=str(pilot.drop_folder(root, trial)))
        return data

    def command(self, tokens, values, folder, phase, deadline):
        variables = {key: str(value) for key, value in values.items()}
        argv = [variables.get(token, token) for token in tokens]
        if argv[0] == "python":
            argv[:1] = [sys.executable, "-X", "utf8"]
        elif argv[0] == "powershell":
            argv[:1] = [pilot.powershell(), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File"]
        elif argv[0] == "pwsh":
            argv[:1] = [shutil.which("pwsh.exe"), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File"]
        elif argv[0] == "dotnet":
            argv[0] = str(values["{dotnet}"])
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise TimeoutError("Se agotó el plazo de la prueba.")
        return run_command(argv, folder, phase, remaining)

    def run(self, test_id, timeout=None, *, in_suite=False, performance_mode=None):
        if test_id not in self.tests:
            raise ValueError(f"Prueba desconocida: {test_id}.")
        if performance_mode and test_id != "performance-hours":
            raise ValueError("-LongRun, -Probe y -Prepare se aplican a performance-hours.")
        test = self.tests[test_id]
        identity = uuid.uuid4().hex
        folder, session = self.session(identity, test=test_id)
        result = lambda status, reason, **kwargs: self.save(folder, session, status, reason, test["acceptanceCriteria"],
                                                           action=test["nextAction"] if status != "PASS" else None,
                                                           **kwargs)
        if session.get("sourceError"):
            return result("ERROR", session["sourceError"])
        if test["humanRequired"] and in_suite:
            return result("BLOCKED", "La suite requiere una comprobación humana explícita.")
        if test_id == "performance-hours" and not performance_mode:
            return result("BLOCKED", "Usa -LongRun para iniciar, -Prepare para abrir un piloto o -Probe para comprobar el banco.")
        if not test["execution"]:
            return result("BLOCKED" if test["humanRequired"] else "NOT_TESTED", test["unavailableReason"])
        if test.get("requiresPwsh") and not shutil.which("pwsh.exe"):
            return result("BLOCKED", "Este banco requiere PowerShell 7.")
        values = {"{session}": identity, "{evidence}": folder, "{metadata}": folder / "adapter.json",
                  "{python}": sys.executable, "{performanceMode}": performance_mode,
                  "{preflight}": folder / "performance-preflight.json",
                  "{mixedDpiPreflight}": folder / "mixed-dpi-preflight.json",
                  "{monitors100Preflight}": folder / "monitors-100-preflight.json"}
        deadline = time.monotonic() + (timeout or test["timeoutSeconds"])
        try:
            if test.get("requiresBuild"):
                try:
                    path, manifest = pilot.build_latest(self.pilot_storage)
                except (RuntimeError, OSError, ValueError) as error:
                    return result("BLOCKED", str(error))
                self.build_data(session, manifest)
                session["buildManifest"] = str(path / "build.json")
                values["{build}"] = path
            if test.get("requiresDotnet"):
                code, output, reason = self.command(
                    ["powershell", "tools/test-center/Resolve-DotNet.ps1"], values, folder, "toolchain", deadline)
                if code:
                    return result("BLOCKED", reason)
                values["{dotnet}"] = last_message(output)
            guard = pilot.launch_gate(self.storage / "desktop") if test.get("requiresIdleDesktop") else nullcontext()
            with guard:
                if test.get("requiresIdleDesktop"):
                    shell = pilot.powershell()
                    live, _ = pilot.active_sessions(self.pilot_storage, pilot.processes(shell))
                    if live:
                        return result("BLOCKED", "Ya hay un piloto abierto. Ciérralo de forma ordinaria antes de repetir.")
                if test["preparation"]:
                    code, _, reason = self.command(test["preparation"], values, folder, "prepare", deadline)
                    if code:
                        if test_id == "windows-mixed-dpi" and values["{mixedDpiPreflight}"].is_file():
                            detail = pilot.read_json(values["{mixedDpiPreflight}"])
                            return result("BLOCKED", detail["reason"], details=detail)
                        if test_id == "windows-monitors-100" and values["{monitors100Preflight}"].is_file():
                            detail = pilot.read_json(values["{monitors100Preflight}"])
                            return result("BLOCKED", detail["reason"], details=detail)
                        preflight = values["{preflight}"]
                        if test_id == "performance-hours" and preflight.is_file():
                            detail = pilot.read_json(preflight)
                            return result("BLOCKED", detail["reason"], details=detail)
                        return result("BLOCKED", reason or "Falló la preparación.")
                commands = test["execution"][:1] if performance_mode == "prepare" else test["execution"]
                for index, tokens in enumerate(commands, start=1):
                    code, output, reason = self.command(tokens, values, folder, f"run-{index}", deadline)
                    adapter = values["{metadata}"]
                    if adapter.is_file():
                        metadata = pilot.read_json(adapter)
                        session.update({key: value for key, value in metadata.items()
                                        if key in (*METADATA, "hostPids", "sidecarPids", "nativeEvidence")})
                    if code:
                        status = "BLOCKED" if code in test.get("blockedExitCodes", []) else test.get("nonzeroStatus", "FAIL")
                        if test_id == "performance-hours" and index == 2 and status == "BLOCKED":
                            detail = pilot.read_json(output)
                            return result(status, detail["reason"], details=detail)
                        return result(status, reason or f"El comando devolvió {code}.",
                                      details={"exitCode": code, "step": index})
                    if test.get("managed") and index == 1:
                        session.update(self.managed(identity))
                        pilot.write_json(folder / "session.json", session)
                if test["humanRequired"]:
                    if test_id == "performance-hours":
                        if performance_mode == "prepare":
                            session["performancePrepared"] = True
                            return result("BLOCKED", "Piloto preparado. Usa -LongRun -Session con este ID para iniciar el calentamiento.")
                        return result("BLOCKED", "El muestreo se inició. Consulta -Report para ver el avance.",
                                      details={"instructions": test["humanInstructions"], "profile": performance_mode})
                    if session["sourceFolder"]:
                        os.startfile(session["sourceFolder"])
                    return result("BLOCKED", "El ensayo está abierto y espera la comprobación física.",
                                  details={"instructions": test["humanInstructions"]})
            if test_id == "build-release":
                path, manifest = pilot.build_latest(self.pilot_storage)
                self.build_data(session, manifest)
                session["buildManifest"] = str(path / "build.json")
            return result("PASS", "Los comandos del ensayo y sus comprobaciones terminaron correctamente.")
        except (RuntimeError, OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
            return result("ERROR", str(error))

    def start_prepared_performance(self, identity, timeout=None):
        folder = self.folder(identity)
        test = self.tests["performance-hours"]
        with pilot.launch_gate(folder):
            session = pilot.read_json(folder / "session.json")
            pilot.require(session.get("performancePrepared") and not (folder / "performance-plan.json").exists(),
                          "Indica una sesión preparada sin muestreo. Consulta -Report con su ID.")
            data = self.managed(identity)
            for key in ("buildId", "gitSha", "hostPid", "sidecarPid", "database"):
                pilot.require(session[key] == data[key], f"La identidad de sesión cambió: {key}.")
            attempt = folder / f"start-{uuid.uuid4().hex}"
            attempt.mkdir()
            preflight = attempt / "performance-preflight.json"
            tokens = {"{session}": identity, "{evidence}": folder, "{performanceMode}": "long", "{preflight}": preflight}
            deadline = time.monotonic() + (timeout or test["timeoutSeconds"])
            code, _, reason = self.command(test["preparation"], tokens, attempt, "prepare", deadline)
            if code:
                detail = pilot.read_json(preflight) if preflight.is_file() else {"reason": reason}
                return self.save(folder, session, "BLOCKED", detail["reason"], details=detail, action=test["nextAction"])
            code, output, reason = self.command(test["execution"][1], tokens, attempt, "start", deadline)
            if code:
                detail = pilot.read_json(output)
                return self.save(folder, session, "BLOCKED" if code == 2 else "ERROR", detail["reason"],
                                 details=detail, action=test["nextAction"])
            session["performancePrepared"] = False
            return self.save(folder, session, "BLOCKED", "El muestreo se inició. Consulta -Report para ver el avance.",
                             details={"instructions": test["humanInstructions"], "profile": "long"},
                             action=test["nextAction"])

    def archive_evidence(self, folder, session):
        """Preserve small original receipts and trace files outside the temporary library."""
        evidence = Path(session["pilotEvidence"])
        root = evidence.parent
        archived = folder / "original"
        archived.mkdir(exist_ok=True)
        receipt = Path(session["pilotReceipt"])
        if session["testId"] == "explorer-drop":
            files = (root / "before.json", root / "drop-trial.json", evidence / "core.log",
                     evidence / "drop.jsonl", evidence / "drop-verdict.json")
        elif session["testId"] == "windows-accessibility":
            checkpoints = Path(session["evidenceDirectory"])
            files = (checkpoints / "uia-initial.json", checkpoints / "accessibility-keyboard.json",
                     checkpoints / "accessibility-narrator.json")
        elif session["testId"] == "performance-hours":
            checkpoints = Path(session["evidenceDirectory"])
            files = tuple(checkpoints / name for name in ("performance-plan.json", "performance-summary.json",
                                                          "performance-responsiveness.json", "performance-closure.json"))
        elif session["testId"] == "windows-monitors-100":
            files = tuple(Path(session["evidenceDirectory"]).glob("monitors-100-*.json"))
        else:
            files = (evidence / "tray.jsonl",)
            if session["testId"] == "windows-explorer":
                checkpoint_dir = Path(session["evidenceDirectory"])
                files += (root / "explorer-before.json", checkpoint_dir / "explorer-baseline.json",
                          checkpoint_dir / "explorer-during.json", checkpoint_dir / "explorer-after.json")
        for path in (receipt, evidence / "ready.json", *files):
            target = archived / ("pilot-receipt.json" if path == receipt else path.name)
            if path.is_file() and not target.exists():
                shutil.copy2(path, target)

    def verify(self, identity, timeout=None):
        folder = self.folder(identity)
        descriptor = folder / "session.json"
        receipt = pilot.receipt_path(self.pilot_storage, identity)
        root = pilot.session_path(identity)
        known = self.accepted(identity)
        existing = pilot.read_json(descriptor) if descriptor.is_file() else None
        test_id = existing["testId"] if existing else "explorer-drop"
        if not existing and receipt.is_file():
            test_id = pilot.read_json(receipt).get("trial") or test_id
        if test_id not in ("explorer-drop", "windows-tray", "windows-explorer", "windows-accessibility",
                           "performance-hours", "windows-monitors-100"):
            raise ValueError("La sesión no admite verificación diferida.")
        if not existing:
            if not receipt.is_file() or (known and not (root / "floating.db").is_file()):
                return self.historical(identity, requested=True)
        if not existing:
            folder, existing = self.session(identity, test=test_id, source=False)
        test = self.tests[test_id]
        with pilot.launch_gate(folder):
            previous = self.record(folder)
            required = (receipt, root / "floating.db", root / "evidence/ready.json")
            missing_source = False
            if test_id == "explorer-drop":
                required += (root / "drop-trial.json", root / "before.json")
                if previous.get("status") == "PASS" or known:
                    required += (root / "evidence/drop.jsonl", root / "evidence/core.log")
                    missing_source = bool(existing.get("sourceFolder")) and not Path(existing["sourceFolder"]).is_dir()
            elif test_id == "windows-accessibility":
                required += (folder / "uia-initial.json",)
            elif test_id == "performance-hours":
                if not (folder / "performance-plan.json").is_file() and previous.get("status") == "BLOCKED":
                    return previous
                required += (folder / "performance-plan.json",)
            elif test_id == "windows-monitors-100":
                required += (folder / "monitors-100-preflight.json",)
            else:
                required += (root / "evidence/tray.jsonl",)
                if test_id == "windows-explorer":
                    required += (folder / "explorer-baseline.json", root / "explorer-before.json")
            if missing_source or any(not file.is_file() for file in required):
                if previous.get("status") == "PASS":
                    return {**previous, "status": "BLOCKED", "historicalStatus": "PASS",
                            "verificationNote": "Faltan archivos originales para revalidar."}
                if known:
                    return self.historical(identity, requested=True)
                if (test_id in ("windows-explorer", "windows-accessibility", "performance-hours", "windows-monitors-100")
                        and not existing["hostPid"]
                        and previous.get("status") == "BLOCKED"):
                    return previous  # Earlier catalog placeholders never created an instrumented host.
                return self.save(folder, existing, "BLOCKED", "Faltan archivos originales del ensayo.",
                                 action=test["nextAction"])
            try:
                data = self.managed(identity)
                for key in ("buildId", "hostPid", "profile", "group", "database", "sourceFolder"):
                    if existing[key] is not None:
                        pilot.require(existing[key] == data.get(key), f"La identidad de sesión cambió: {key}.")
                existing.update(data)
                evidence_key = {"explorer-drop": "sourceFolder", "windows-accessibility": "uiaEvidence"}.get(
                    test_id, "trayTrace")
                if test_id not in ("performance-hours", "windows-monitors-100"):
                    pilot.require(existing.get(evidence_key),
                                  "El recibo no corresponde al tipo de ensayo registrado.")
                attempt = folder / f"verify-{uuid.uuid4().hex}"
                attempt.mkdir()
                deadline = time.monotonic() + (timeout or test["timeoutSeconds"])
                code, output, reason = self.command(test["verification"], {"{session}": identity, "{evidence}": folder},
                                                    attempt, "verify", deadline)
                try:
                    verdict = pilot.read_json(output)
                except ValueError as error:
                    raise RuntimeError(reason or "El verificador no devolvió un resultado válido.") from error
                status = verdict["status"]
                pilot.require(CODES.get(status) == code, "El código y el veredicto de la prueba difieren.")
                if status != "BLOCKED":
                    self.archive_evidence(folder, existing)
                if status == "PASS" or test_id != "explorer-drop":
                    self.archive_evidence(attempt, existing)
                if status == "BLOCKED" and test_id != "explorer-drop":
                    verdict["instructions"] = test["humanInstructions"]
                pilot.write_json(attempt / "result.json", verdict)
                return self.save(folder, existing, status, verdict["reason"], test["acceptanceCriteria"],
                                 action=test["nextAction"] if status != "PASS" else None, details=verdict)
            except (RuntimeError, OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
                return self.save(folder, existing, "ERROR", str(error), action=test["nextAction"])

    def observe(self, identity, phase, outcome, note):
        folder = self.folder(identity)
        with pilot.launch_gate(folder):
            session = self.record(folder)
            test_id = session.get("testId")
            if test_id == "windows-explorer" and session.get("trayTrace"):
                prefix, phases, outcomes = "explorer", ("during", "after"), ("pass", "fail")
            elif test_id == "windows-accessibility" and session.get("uiaEvidence"):
                prefix, phases, outcomes = "accessibility", ("keyboard", "narrator"), ("pass", "fail", "blocked")
            elif test_id == "performance-hours" and (folder / "performance-plan.json").is_file():
                prefix, phases, outcomes = "performance", ("incident", "responsiveness"), ("pass", "fail", "blocked")
            elif test_id == "windows-monitors-100" and session.get("pilotReceipt"):
                prefix, phases, outcomes = "monitors-100", ("baseline", "moved", "disconnected", "reconnected"), (
                    "pass", "fail", "blocked")
            else:
                raise ValueError("Indica una sesión guiada que admita observaciones.")
            pilot.require(phase in phases and outcome in outcomes and note and note.strip(),
                          "Indica la fase, el resultado observado y una nota.")
            attempt = folder / f"observe-{uuid.uuid4().hex}"
            attempt.mkdir()
            operator_file = attempt / "operator.json"
            pilot.write_json(operator_file, {"outcome": outcome, "note": note.strip()})
            command = self.tests[test_id]["observation"]
            tokens = {"{session}": identity, "{evidence}": folder, "{phase}": phase, "{operator}": operator_file}
            code, output, reason = self.command(command, tokens, attempt, "observe", time.monotonic() + 50)
            if code == CODES["ERROR"]:
                raise RuntimeError(reason or "No se pudo tomar el punto de control.")
            result = pilot.read_json(output)
            pilot.require(CODES.get(result["status"]) == code, "El resultado del punto de control es incoherente.")
            if "checkpoint" in result:
                pilot.write_json(folder / f"{prefix}-{phase}.json", result["checkpoint"])
            result.update(session=identity, phase=phase, checkpointRecorded="checkpoint" in result)
            pilot.write_json(attempt / "result.json", result)
            return result

    def stop(self, identity):
        folder = self.folder(identity)
        session = self.record(folder)
        pilot.require(session.get("pilotReceipt"), "La sesión no es un host gestionado.")
        closure = pilot.stop_session(self.pilot_storage, identity, pilot.powershell())
        if session["testId"] == "performance-hours":
            pilot.write_json(folder / "performance-closure.json", {**closure, "closedUtc": now(),
                           "buildId": session["buildId"], "hostPid": session["hostPid"], "sidecarPid": session["sidecarPid"]})
        elif session["testId"] == "windows-mixed-dpi":
            pilot.write_json(folder / "mixed-dpi-closure.json", {**closure, "closedUtc": now(), "buildId": session["buildId"]})
        elif session["testId"] == "windows-monitors-100":
            pilot.write_json(folder / "monitors-100-closure.json", {**closure, "closedUtc": now(),
                           "buildId": session["buildId"], "hostPid": session["hostPid"], "sidecarPid": session["sidecarPid"]})
        return closure

    def suite(self, suite_id, timeout=None):
        if suite_id not in self.suites:
            raise ValueError(f"Suite desconocida: {suite_id}.")
        folder, session = self.session(uuid.uuid4().hex, suite=suite_id)
        if session.get("sourceError"):
            return self.save(folder, session, "ERROR", session["sourceError"])
        tests = self.suites[suite_id]["tests"]
        members = []
        for test_id in tests:
            result = self.run(test_id, timeout, in_suite=True)
            members.append({key: result[key] for key in ("id", "testId", "status", "buildId", "reason")})
        builds = {member["buildId"] for member in members if member["buildId"]}
        if len(builds) == 1:
            session["buildId"] = builds.pop()
        status = min((member["status"] for member in members), key=PRIORITY.index)
        counts = {name: sum(member["status"] == name for member in members) for name in CODES}
        reason = ", ".join(f"{name}: {count}" for name, count in counts.items() if count)
        outcomes = dict(approvedCriteria=[m["testId"] for m in members if m["status"] == "PASS"],
                       failedCriteria=[m["testId"] for m in members if m["status"] == "FAIL"],
                       blockers=[m["testId"] for m in members if m["status"] == "BLOCKED"])
        return self.save(folder, session, status, reason, details={"members": members}, outcomes=outcomes,
                         action="Consultar las sesiones de las pruebas que siguen pendientes." if status != "PASS" else None)

    def record(self, folder):
        descriptor, verdict = folder / "session.json", folder / "result.json"
        if not descriptor.is_file():
            return {}
        session = pilot.read_json(descriptor)
        row = {**session, **(pilot.read_json(verdict) if verdict.is_file() else {})}
        if session["testId"] == "performance-hours":
            for key, name in (("preflight", "preflight"), ("progress", "progress"),
                              ("technicalEnd", "summary"), ("closure", "closure")):
                path = folder / f"performance-{name}.json"
                if path.is_file():
                    row[key] = pilot.read_json(path)
        elif session["testId"] == "windows-mixed-dpi":
            row["checkpoints"] = sorted(path.name for path in folder.glob("mixed-dpi-*.json"))
        elif session["testId"] == "windows-monitors-100":
            row["checkpoints"] = sorted(path.name for path in folder.glob("monitors-100-*.json"))
            closure = folder / "monitors-100-closure.json"
            if closure.is_file():
                row["closure"] = pilot.read_json(closure)
        return row

    def accepted(self, identity):
        return next((row for row in pilot.read_json(ACCEPTED) if row["id"] == identity), None)

    def historical(self, identity, *, requested=False):
        record = self.accepted(identity)
        if not record:
            raise ValueError(f"Sesión desconocida: {identity}.")
        if requested:
            record.update(historicalStatus=record["status"], status="BLOCKED",
                          verificationNote="Evidencia histórica. La biblioteca original no está disponible en este entorno.")
        return record

    def reports(self, *, identity=None, test=None, suite=None, build=None, start=None, end=None):
        if identity:
            row = self.record(self.folder(identity))
            return [row if row.get("finishedUtc") else self.accepted(identity) or row or self.historical(identity)]
        rows = [row for folder in (self.storage / "sessions").glob("*") if (row := self.record(folder))]
        accepted = {row["id"]: row for row in pilot.read_json(ACCEPTED)}
        for row in rows:
            if row["finishedUtc"]:
                accepted.pop(row["id"], None)
        rows = [row for row in rows if row["id"] not in accepted] + list(accepted.values())
        lower = period(start) if start else None
        upper = period(end) if end else None
        if end and len(end) == 10:
            upper += timedelta(days=1)
        return sorted((row for row in rows if (not test or row["testId"] == test)
                       and (not suite or row["suiteId"] == suite) and (not build or row["buildId"] == build)
                       and (not lower or period(row["startedUtc"]) >= lower)
                       and (not upper or period(row["startedUtc"]) < upper)),
                      key=lambda row: row["startedUtc"], reverse=True)

    def listing(self):
        latest = {}
        for session in self.reports():
            latest.setdefault(session["testId"], session)
        return [dict(id=identity, **entry, lastStatus=latest.get(identity, {}).get("status", "NOT_TESTED"),
                     lastRunUtc=latest.get(identity, {}).get("startedUtc"))
                for identity, entry in self.tests.items()]


def markdown(rows):
    lines = ["# NexSpace Test Center"]
    for row in rows:
        lines.extend(["", f"## {row['testId'] or row['suiteId']} · {row['status']}", "",
                      f"- Sesión `{row['id']}`", f"- Inicio `{row['startedUtc']}`",
                      f"- Build `{row.get('buildId') or 'sin build asociado'}`",
                      f"- SHA `{row.get('gitSha') or 'sin SHA asociado'}`",
                      f"- Duración {row['durationSeconds']} s" if row.get("durationSeconds") is not None
                      else "- Duración sin registrar", "", row.get("reason", "Ejecución sin resultado."), ""])
        for key, label in (("approvedCriteria", "Aprobado"), ("failedCriteria", "Fallido"), ("blockers", "Bloqueo")):
            lines.extend(f"- {label} · {value}" for value in row.get(key, []))
        for key, label in (("evidenceDirectory", "Evidencia local"), ("pilotEvidence", "Evidencia del piloto"),
                           ("publicSummary", "Resumen público"), ("sourceFolder", "Carpeta de origen"),
                           ("nextAction", "Siguiente acción"), ("verificationNote", "Revalidación")):
            if row.get(key):
                lines.append(f"- {label} · {row[key]}")
        for member in (row.get("details") or {}).get("members", []):
            lines.append(f"- {member['testId']} · {member['status']} · `{member['id']}` · {member['reason']}")
        if progress := row.get("progress"):
            lines.append(f"- Muestreo · {progress['phase']} · {progress['samples']} muestras · "
                         f"{progress['measuredSeconds']:g}/{progress['targetSeconds']:g} s de reposo · "
                         f"última muestra {progress['lastUtc']}")
        if preflight := row.get("preflight"):
            lines.append(f"- Preflight del colector · {preflight['status']} · "
                         f"{preflight.get('powerShell', 'sin intérprete')} {preflight.get('version', '')}")
        if end := row.get("technicalEnd"):
            lines.append(f"- Cierre técnico · {end['status']} · {end['reason']}")
            if summary := end.get("summary"):
                lines.extend(f"- {metric} · mediana inicial {values['baselineMedian']:.4g} · "
                             f"variación final {values['finalDelta']:+.4g} · pendiente/h {values['slopePerHour']:+.4g}"
                             for metric, values in summary["metrics"].items())
        if closure := row.get("closure"):
            lines.append(f"- Salida del piloto · {closure['status']} · {closure['observedProcesses']} procesos observados")
        for checkpoint in row.get("checkpoints", []):
            lines.append(f"- Evidencia de monitores · {checkpoint}")
    if not rows:
        lines.extend(["", "No hay sesiones para este filtro."])
    return "\n".join(lines) + "\n"


def menu(center):
    while True:
        print("\nNexSpace Test Center\n")
        listing = center.listing()
        for number, test in enumerate(listing, start=1):
            print(f"{number:2}  {test['name']} [{test['id']}] · {test['type']} · "
                  f"{test['lastStatus']} · {test['lastRunUtc'] or 'sin ejecución'}")
        try:
            selection = input("\nNúmero para ejecutar, H para historial, V para verificar, S para salir: ").strip().lower()
            if selection == "s":
                return 0
            if selection == "h":
                test = input("ID de prueba (vacío para todas): ").strip()
                print(markdown(center.reports(test=test or None)))
            elif selection == "v":
                print_json(center.verify(input("Sesión: ").strip()))
            elif selection.isdecimal() and 1 <= int(selection) <= len(listing):
                print_json(center.run(listing[int(selection) - 1]["id"]))
        except (EOFError, KeyboardInterrupt):
            return 0
        except (RuntimeError, ValueError, OSError) as error:
            print(f"ERROR: {error}", file=sys.stderr)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("menu", "list", "run", "verify", "report", "stop", "observe"))
    for flag in ("test", "suite", "session", "build", "from", "to"):
        parser.add_argument(f"--{flag}", dest="start" if flag == "from" else "end" if flag == "to" else flag)
    parser.add_argument("--timeout", type=int)
    parser.add_argument("--format", choices=("json", "markdown"), default="markdown")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--phase", choices=("during", "after", "keyboard", "narrator", "incident", "responsiveness",
                                            "baseline", "moved", "disconnected", "reconnected"))
    parser.add_argument("--outcome", choices=("pass", "fail", "blocked"))
    parser.add_argument("--note")
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--long-run", dest="performance_mode", action="store_const", const="long")
    modes.add_argument("--probe", dest="performance_mode", action="store_const", const="probe")
    modes.add_argument("--prepare", dest="performance_mode", action="store_const", const="prepare")
    args = parser.parse_args()
    if args.performance_mode and (args.mode != "run" or args.test != "performance-hours" or args.suite):
        parser.error("--long-run, --probe y --prepare requieren run --test performance-hours")
    if args.mode == "run" and args.session and args.performance_mode != "long":
        parser.error("--session al iniciar requiere --test performance-hours --long-run")
    center = Center()
    if args.mode == "menu":
        return menu(center)
    if args.mode == "list":
        print_json(center.listing())
        return 0
    if args.mode == "report":
        rows = center.reports(identity=args.session, test=args.test, suite=args.suite, build=args.build,
                              start=args.start, end=args.end)
        content = markdown(rows) if args.format == "markdown" else json.dumps(rows, ensure_ascii=False, indent=2) + "\n"
        if args.output:
            with args.output.open("x", encoding="utf-8") as file:
                file.write(content)
        else:
            print(content, end="")
        return 0
    if args.mode == "stop":
        print_json(center.stop(args.session))
        return 0
    if args.mode == "observe":
        result = center.observe(args.session, args.phase, args.outcome, args.note)
        print_json(result)
        return 0 if result["checkpointRecorded"] and result["status"] == "BLOCKED" else CODES[result["status"]]
    if args.timeout is not None and not 1 <= args.timeout <= 14400:
        raise ValueError("El plazo debe estar entre 1 y 14400 segundos.")
    if args.mode == "verify":
        result = center.verify(args.session, args.timeout)
    elif args.session:
        result = center.start_prepared_performance(args.session, args.timeout)
    elif args.test:
        result = center.run(args.test, args.timeout, performance_mode=args.performance_mode)
    else:
        result = center.suite(args.suite, args.timeout)
    print_json(result)
    return CODES[result["status"]]


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(CODES["ERROR"])
