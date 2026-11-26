"""Collect and verify resource samples from one managed floating pilot."""

import argparse
from datetime import UTC, datetime
import json
from pathlib import Path
import statistics
import subprocess
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import pilot_support as pilot

PROFILES = {"long": (900, 10800, 60), "probe": (12, 36, 3)}
METRICS = ("private", "ws", "handles", "gdi", "user", "cpuOneCorePct")
CODES = {"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3}
WAITING = "Todavía faltan muestras del periodo de reposo."
SNAPSHOT = Path(__file__).with_name("Resource-Snapshot.ps1")


def now():
    return datetime.now(UTC).isoformat(timespec="milliseconds")


def status(outcome, reason, **details):
    return dict(status=outcome, reason=reason, **details)


def binding(identity, folder):
    root = pilot.session_path(identity)
    session = pilot.read_json(folder / "session.json")
    receipt = pilot.read_json(pilot.receipt_path(pilot.STORAGE, identity))
    ready = pilot.read_json(root / "evidence/ready.json")
    bound = dict(session=identity, buildId=receipt["build"], gitSha=receipt["commit"],
                 hostPid=receipt["process"][0], hostStarted=receipt["process"][1],
                 sidecarPid=ready["corePid"], database=str(root / "floating.db"))
    pilot.require(session["id"] == receipt["id"] == ready["session"] == identity
                  and session["testId"] == "performance-hours"
                  and all(session[key] == bound[key] for key in
                          ("buildId", "gitSha", "hostPid", "sidecarPid", "database"))
                  and ready["hostPid"] == bound["hostPid"] and ready["build"] == bound["buildId"]
                  and pilot.same_path(receipt["database"], bound["database"])
                  and pilot.same_path(ready["database"], bound["database"])
                  and Path(bound["database"]).is_file(), "La biblioteca y los procesos no corresponden a la sesión.")
    return bound


def load_plan(identity, folder):
    plan = pilot.read_json(folder / "performance-plan.json")
    pilot.require(plan["binding"] == binding(identity, folder)
                  and PROFILES.get(plan["profile"]) == tuple(plan["timingSeconds"]),
                  "El plan no coincide con la sesión o con el perfil del banco.")
    return plan


def read_snapshot(shell, *arguments):
    command = [shell, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
               "-File", str(SNAPSHOT), *arguments]
    result = subprocess.run(command, capture_output=True, encoding="utf-8", errors="replace", check=True,
                            creationflags=subprocess.CREATE_NO_WINDOW, timeout=20)
    return json.loads(result.stdout)


def snapshot(plan):
    bound = plan["binding"]
    engine = plan["collector"]
    sample = read_snapshot(engine["powerShell"], "-HostId", str(bound["hostPid"]), "-HostStarted", bound["hostStarted"])
    pilot.require(sample["powerShellVersion"] == engine["version"], "Cambió la versión de PowerShell del colector.")
    return sample


def collector_error(error):
    output = error.stderr if isinstance(error, (subprocess.CalledProcessError, subprocess.TimeoutExpired)) else None
    if isinstance(output, bytes):
        output = output.decode("utf-8", errors="replace")
    return output.strip() if output else str(error)


def preflight(identity, path):
    engine = dict(session=identity, checkedUtc=now())
    try:
        engine["powerShell"] = pilot.powershell()
        sample = read_snapshot(engine["powerShell"], "-Preflight")
        pilot.require(sample["hostAlive"] and sample["processes"]
                      and sample["processCount"] == len(sample["processes"]), "El colector no pudo leer su propio árbol.")
        totals(sample)
        pilot.require(isinstance(sample["windows"], list), "El colector no devolvió la enumeración de ventanas.")
        engine["version"] = sample["powerShellVersion"]
        verdict = status("PASS", "El colector compiló y leyó sus contadores.")
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as error:
        verdict = status("BLOCKED", "Falló el preflight del colector.", collectorError=collector_error(error))
    record = {**engine, **verdict}
    pilot.write_json(path, record)
    return record


def topology(sample):
    return dict(processes=[(row["pid"], row["started"], row["parent"]) for row in sample["processes"]],
                windows=sample["windows"])


def totals(sample):
    return {metric: sum(row[metric] for row in sample["processes"]) for metric in (*METRICS[:-1], "cpu")}


def sample_fault(sample, plan, stop_requested, previous=None, baseline=None):
    if not sample["hostAlive"]:
        if stop_requested:
            return status("BLOCKED", "El operador cerró el piloto durante el muestreo.")
        return status("FAIL", "El host original terminó durante el muestreo.")
    if stop_requested:
        return status("BLOCKED", "Se solicitó el cierre del piloto durante el muestreo.")
    bound = plan["binding"]
    core_present = any(row["pid"] == bound["sidecarPid"] and row["parent"] == bound["hostPid"]
                       for row in sample["processes"])
    if len(sample["processes"]) != sample["processCount"]:
        return status("BLOCKED", "Cambió el árbol durante la lectura de contadores.")
    if not core_present:
        return status("FAIL", "El sidecar original desapareció del árbol.")
    if baseline is not None and topology(sample) != topology(baseline):
        return status("FAIL", "Cambió la identidad de un proceso o de una ventana durante el reposo.")
    values = totals(sample)
    if values["private"] > 2 * 1024 ** 3 or values["handles"] > 24000:
        return status("FAIL", "Se superó el presupuesto de seguridad del banco de rendimiento.")
    if previous is not None:
        gap = sample["elapsedSeconds"] - previous["elapsedSeconds"]
        wall = (datetime.fromisoformat(sample["utc"]) - datetime.fromisoformat(previous["utc"])).total_seconds()
        if min(gap, wall) <= 0 or max(gap, wall) > 2 * plan["timingSeconds"][2]:
            return status("BLOCKED", "Se interrumpió la cadencia del muestreo.", gapSeconds=gap, wallGapSeconds=wall)
    return None


def summarize(steady, duration):
    """Compare six equal blocks against the first block and report full-run values."""
    origin = steady[0]["elapsedSeconds"]
    blocks = [[] for _ in range(6)]
    timeline = []
    for before, sample in zip(steady, steady[1:]):
        values = totals(sample)
        gap = sample["elapsedSeconds"] - before["elapsedSeconds"]
        values["cpuOneCorePct"] = 100 * (values.pop("cpu") - totals(before)["cpu"]) / gap
        timeline.append(values)
        block = int((sample["elapsedSeconds"] - origin) * 6 / duration)
        if block < len(blocks):
            blocks[block].append(values)
    if not all(blocks):
        return None
    metrics = {}
    for key in METRICS:
        values = [row[key] for row in timeline]
        b = [row[key] for row in blocks[0]]
        p05, p95 = (statistics.quantiles(b, n=20, method="inclusive")[0::18]
                    if len(b) > 1 else (b[0], b[0]))
        medians = [statistics.median(row[key] for row in block) for block in blocks]
        persistent = (all(a < z for a, z in zip(medians[-4:], medians[-3:]))
                      and medians[-1] > p95 + (p95 - p05))
        block_hours = duration / (6 * 3600)
        slope = statistics.median((medians[j] - medians[i]) / ((j - i) * block_hours)
                                  for j in range(6) for i in range(j))
        metrics[key] = dict(baselineP05=p05, baselineMedian=medians[0], baselineP95=p95,
                            blockMedians=medians, finalDelta=medians[-1] - medians[0],
                            min=min(values), max=max(values), slopePerHour=slope, persistent=persistent)
    return dict(steadySeconds=steady[-1]["elapsedSeconds"] - origin, samples=len(steady),
                blockSamples=[len(block) for block in blocks], lastUtc=steady[-1]["utc"],
                processes=len(steady[0]["processes"]), windows=len(steady[0]["windows"]),
                metrics=metrics)


def series(plan, samples):
    """Recompute technical status from the recorded samples for this exact plan."""
    warmup, duration, _ = plan["timingSeconds"]
    steady = []
    previous = None
    for sample in samples:
        pilot.require(sample["binding"] == plan["binding"], "Una muestra pertenece a otra sesión.")
        if fault := sample_fault(sample, plan, sample.get("stopRequested", False), previous,
                                 steady[0] if steady else None):
            return fault
        if sample["elapsedSeconds"] >= warmup:
            if len(sample["windows"]) != 4:
                return status("FAIL", "Se esperaban NexDock y tres islas visibles al cerrar el calentamiento.")
            steady.append(sample)
        previous = sample
    if not steady or steady[-1]["elapsedSeconds"] - steady[0]["elapsedSeconds"] < duration:
        return status("BLOCKED", WAITING)
    summary = summarize(steady, duration)
    if summary is None:
        return status("BLOCKED", "Faltan bloques de observación comparables.")
    if plan["profile"] == "probe":
        return status("BLOCKED", "La prueba corta valida el muestreo. Falta la sesión prolongada.", summary=summary)
    suspects = [metric for metric in ("private", "handles", "gdi", "user", "cpuOneCorePct")
                if summary["metrics"][metric]["persistent"]]
    if suspects:
        return status("FAIL", "Hay crecimiento sostenido que requiere investigar.", metrics=suspects, summary=summary)
    return status("PASS", "El periodo técnico terminó con la topología prevista.", summary=summary)


def read_samples(folder):
    path = folder / "performance-samples.jsonl"
    if not path.is_file():
        return []
    with path.open(encoding="utf-8", newline="") as file:
        return [json.loads(line) for line in file if line.endswith("\n")]


def start(identity, folder, profile, preflight_path):
    engine = pilot.read_json(preflight_path)
    pilot.require(engine["session"] == identity and engine["status"] == "PASS",
                  "Hace falta un preflight aprobado para esta sesión.")
    plan = dict(profile=profile, timingSeconds=PROFILES[profile], binding=binding(identity, folder),
                collector={key: engine[key] for key in ("powerShell", "version")}, preflight=str(preflight_path),
                createdUtc=now())
    try:
        first = snapshot(plan)
        if fault := sample_fault(first, plan, False):
            raise RuntimeError(fault["reason"])
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as error:
        verdict = status("BLOCKED", "El colector no pudo muestrear el piloto antes del calentamiento.",
                         collectorError=collector_error(error), collector=plan["collector"])
        print(json.dumps(verdict, ensure_ascii=False))
        return CODES["BLOCKED"]
    with (folder / "performance-plan.json").open("x", encoding="utf-8") as file:
        json.dump(plan, file, ensure_ascii=False, indent=2)
    with (folder / "performance-worker.log").open("ab") as log:
        child = subprocess.Popen([sys.executable, "-X", "utf8", __file__, "worker", identity, str(folder)],
                                 stdin=subprocess.DEVNULL, stdout=log, stderr=log,
                                 creationflags=subprocess.DETACHED_PROCESS)
    print(json.dumps({"session": identity, "collectorPid": child.pid, "profile": profile}))
    return 0


def worker(identity, folder):
    plan = load_plan(identity, folder)
    warmup, duration, interval = plan["timingSeconds"]
    origin = time.monotonic()
    samples = []
    steady_from = None
    stop_file = pilot.session_path(identity) / "evidence/stop.json"
    try:
        with (folder / "performance-samples.jsonl").open("x", encoding="utf-8", buffering=1) as stream:
            while True:
                sample = snapshot(plan)
                sample.update(elapsedSeconds=round(time.monotonic() - origin, 3), binding=plan["binding"],
                              stopRequested=stop_file.is_file())
                samples.append(sample)
                stream.write(json.dumps(sample, ensure_ascii=False) + "\n")
                elapsed = sample["elapsedSeconds"]
                if steady_from is None and elapsed >= warmup:
                    steady_from = elapsed
                measured = 0 if steady_from is None else elapsed - steady_from
                phase = "warmup" if steady_from is None else "steady"
                current = dict(phase=phase, profile=plan["profile"], lastUtc=sample["utc"],
                               samples=len(samples), elapsedSeconds=elapsed, measuredSeconds=round(measured, 3),
                               targetSeconds=duration, intervalSeconds=interval)
                pilot.write_json(folder / "performance-progress.json", current)
                verdict = series(plan, samples)
                if verdict["reason"] != WAITING:
                    break
                time.sleep(max(0, origin + len(samples) * interval - time.monotonic()))
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as error:
        verdict = status("BLOCKED", "El colector se interrumpió.", collectorError=collector_error(error))
    pilot.write_json(folder / "performance-summary.json",
                     {**verdict, "binding": plan["binding"], "finishedUtc": now()})
    return 0


def observe(folder, plan, phase, operator):
    note = pilot.read_json(operator)
    if phase == "incident":
        pilot.require(note["outcome"] in ("fail", "blocked"), "Las incidencias admiten Fail o Blocked.")
    else:
        end = folder / "performance-summary.json"
        final = pilot.read_json(end) if end.is_file() else {}
        if plan["profile"] != "long" or final.get("status") != "PASS":
            return status("BLOCKED", "Espera a que termine correctamente el periodo técnico de la sesión larga.")
        if note["outcome"] == "pass":
            sample = snapshot(plan)
            samples = read_samples(folder)
            if fault := sample_fault(sample, plan, False, baseline=samples[-1]):
                return {**fault, "reason": "Punto de control posterior: " + fault["reason"]}
            note["topology"] = topology(sample)
    checkpoint = dict(binding=plan["binding"], phase=phase, utc=now(), **note)
    return status("BLOCKED", "Observación registrada. Ejecuta -Verify para evaluar la sesión.", checkpoint=checkpoint)


def verify(folder, plan):
    samples = read_samples(folder)
    technical = series(plan, samples)
    incidents = []
    for path in sorted(folder.glob("observe-*/result.json")):
        checkpoint = pilot.read_json(path).get("checkpoint")
        if checkpoint and checkpoint["phase"] == "incident":
            pilot.require(checkpoint["binding"] == plan["binding"], "La incidencia pertenece a otra sesión.")
            incidents.append(checkpoint)
    summary_file = folder / "performance-summary.json"
    final = pilot.read_json(summary_file) if summary_file.is_file() else None
    if final:
        pilot.require(final["binding"] == plan["binding"], "El cierre técnico pertenece a otra sesión.")
        if technical["status"] != "FAIL" and final["status"] != "PASS":
            technical = final
    response = folder / "performance-responsiveness.json"
    operator = pilot.read_json(response) if response.is_file() else None
    if operator:
        pilot.require(operator["binding"] == plan["binding"] and final
                      and operator["utc"] >= final["finishedUtc"], "La comprobación física no corresponde al cierre.")
    details = dict(technical=technical, collector=final, incidents=incidents, responsiveness=operator,
                   samples=len(samples))
    if technical["status"] == "FAIL" or any(note["outcome"] == "fail" for note in incidents) or (
            operator and operator["outcome"] == "fail"):
        return status("FAIL", "La sesión registró un fallo técnico u observado.", **details)
    if incidents or not final or technical["status"] != "PASS" or not operator or operator["outcome"] != "pass":
        reason = "Quedan incidencias, muestras o comprobaciones físicas pendientes."
        if final and technical["status"] == "BLOCKED":
            reason = final["reason"]
        return status("BLOCKED", reason, **details)
    expected = json.loads(json.dumps(topology(samples[-1])))
    pilot.require(operator.get("topology") == expected, "La topología del punto físico cambió.")
    return status("PASS", "Reposo completo con evidencia técnica y capacidad de respuesta confirmada.", **details)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("preflight", "start", "worker", "observe", "verify"))
    parser.add_argument("session")
    parser.add_argument("evidence", type=Path)
    parser.add_argument("--profile", choices=PROFILES)
    parser.add_argument("--phase", choices=("responsiveness", "incident"))
    parser.add_argument("--operator", type=Path)
    parser.add_argument("--preflight", type=Path)
    args = parser.parse_args()
    try:
        if args.mode == "start":
            return start(args.session, args.evidence, args.profile, args.preflight)
        if args.mode == "worker":
            return worker(args.session, args.evidence)
        if args.mode == "preflight":
            verdict = preflight(args.session, args.preflight)
        else:
            plan = load_plan(args.session, args.evidence)
            verdict = (verify(args.evidence, plan) if args.mode == "verify" else
                       observe(args.evidence, plan, args.phase, args.operator))
        print(json.dumps(verdict, ensure_ascii=False))
        return CODES[verdict["status"]]
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as error:
        print(json.dumps(status("ERROR", str(error)), ensure_ascii=False))
        return CODES["ERROR"]


if __name__ == "__main__":
    sys.exit(main())
