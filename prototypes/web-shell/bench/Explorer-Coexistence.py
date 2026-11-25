"""Prepare and verify a physical FLOATING/Explorer trial without sending input."""

import argparse
import json
from pathlib import Path
import subprocess
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import pilot_support as pilot

PHASE_FOLDERS = {"during": "01-Durante", "after": "02-Despues"}
CODES = {"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3}


def probe(shell, folder=None, host_pid=None):
    args = [shell, "-NoProfile", "-NonInteractive", "-STA", "-ExecutionPolicy", "Bypass", "-File",
            Path(__file__).with_name("Explorer-Checkpoint.ps1")]
    if folder is not None:
        args.extend(("-Folder", folder, "-PilotId", str(host_pid)))
    result = subprocess.run(args, check=True, capture_output=True, encoding="utf-8",
                            timeout=32, creationflags=subprocess.CREATE_NO_WINDOW)
    return json.loads(result.stdout)


def baseline(identity, evidence):
    pilot.session_path(identity)
    observed = probe(pilot.powershell())
    pilot.require(observed["shell"], "No se detectó el escritorio interactivo de Explorer.")
    pilot.write_json(evidence / "explorer-baseline.json", {"session": identity, **observed})


def context(identity):
    root = pilot.session_path(identity)
    receipt = pilot.read_json(pilot.receipt_path(pilot.STORAGE, identity))
    ready = pilot.read_json(root / "evidence/ready.json")
    pilot.require(receipt.get("trial") == "windows-explorer" and ready.get("trayTracing") is True
                  and ready["session"] == receipt["id"] == identity
                  and ready["build"] == receipt["build"] and ready["hostPid"] == receipt["process"][0]
                  and pilot.same_path(receipt["executable"], ready["executable"])
                  and pilot.same_path(receipt["database"], root / "floating.db")
                  and pilot.same_path(ready["database"], root / "floating.db")
                  and pilot.same_path(receipt["evidence"], root / "evidence"),
                  "El recibo, la biblioteca y el host no coinciden con el ensayo Explorer.")
    binding = dict(session=identity, buildId=receipt["build"], gitSha=receipt["commit"],
                   hostPid=ready["hostPid"], sidecarPid=ready["corePid"])
    return root, receipt, binding


def live_tree(rows, receipt, binding):
    owner = tuple(receipt["process"])
    host = next((r for r in rows if pilot.pid_key(r) == owner), None)
    core = next((r for r in rows if r["ProcessId"] == binding["sidecarPid"]
                 and r["Name"].lower() == pilot.CORE.lower() and r["ParentProcessId"] == owner[0]
                 and r["Started"] >= owner[1]), None)
    if (host and core and pilot.same_path(host["ExecutablePath"], receipt["executable"])
            and pilot.same_path(core["ExecutablePath"], Path(receipt["executable"]).parent / "core" / pilot.CORE)):
        return sorted(pilot.process_tree(rows, owner))
    return []


def read_checkpoint(evidence, phase):
    path = evidence / f"explorer-{phase}.json"
    return pilot.read_json(path) if path.is_file() else None


def observe(identity, evidence, phase, operator_file):
    root, receipt, binding = context(identity)
    operator = pilot.read_json(operator_file)
    pilot.require(operator["outcome"] in ("pass", "fail") and operator["note"].strip(),
                  "Indica el resultado de la interacción y una observación concreta.")
    checkpoint = dict(binding, phase=phase, operator=operator, recordedMs=time.time_ns() // 1_000_000)
    if operator["outcome"] == "fail":
        return dict(status="FAIL", reason="El operador informó un fallo observado.", checkpoint=checkpoint)

    during = read_checkpoint(evidence, "during")
    if phase == "after" and not (during and during.get("native")):
        return dict(status="BLOCKED", reason="Registra primero el punto de control Durante con el piloto abierto.")
    shell = pilot.powershell()
    native = probe(shell, pilot.explorer_folder(root, identity) / PHASE_FOLDERS[phase], binding["hostPid"])
    if not native["explorer"]:
        return dict(status="BLOCKED", reason=f"Pon Explorer en primer plano en {PHASE_FOLDERS[phase]} y deja el puntero dentro de la carpeta.")
    rows = pilot.processes(shell)
    checkpoint["native"] = native
    if phase == "during":
        tree = live_tree(rows, receipt, binding)
        if not tree or not {"NexDock", "Archivos"} <= set(native["pilotWindows"]):
            return dict(status="BLOCKED", reason="Usa la instancia del ensayo con NexDock y Archivos visibles.")
        checkpoint["owned"] = tree
    else:
        active = {pilot.pid_key(row) for row in rows}
        checkpoint["remaining"] = sorted(active & {tuple(key) for key in during["owned"]})
        if tuple(receipt["process"]) in active:
            return dict(status="BLOCKED", reason="El piloto sigue activo. Ciérralo desde su menú de bandeja.")
    return dict(status="BLOCKED", reason="Observación registrada. Completa los pasos pendientes y ejecuta -Verify.",
                checkpoint=checkpoint)


def native_quit(records, binding, during_ms, after_ms):
    return next((event for event in records if event["stage"] == "tray-quit"
                 and (event.get("session"), event.get("hostPid"), event.get("build"))
                 == (binding["session"], binding["hostPid"], binding["buildId"])
                 and during_ms < event["ms"] < after_ms), None)


def verdict(binding, baseline_record, during, after, records, originals_intact):
    if baseline_record["session"] != binding["session"] or not baseline_record["shell"]:
        return "ERROR", "Falta la identidad inicial de Explorer vinculada al ensayo.", None
    for phase, checkpoint in (("during", during), ("after", after)):
        if checkpoint is not None:
            if checkpoint.get("phase") != phase or any(checkpoint.get(key) != value for key, value in binding.items()):
                return "ERROR", "La observación pertenece a otra sesión, build o proceso.", None
            operator = checkpoint["operator"]
            if not operator["note"].strip() or operator["outcome"] not in ("pass", "fail"):
                return "ERROR", "La observación del operador está incompleta.", None
            if operator["outcome"] == "fail":
                return "FAIL", "El operador registró un problema durante la prueba física.", None
    if not originals_intact:
        return "FAIL", "Cambió el contenido de los archivos aislados de Explorer.", None
    if not (during and after):
        return "BLOCKED", "Faltan observaciones físicas vinculadas durante y después del cierre.", None
    live, stopped = during["native"], after["native"]
    if not baseline_record["observedMs"] < live["observedMs"] < stopped["observedMs"]:
        return "ERROR", "Los puntos de control no están en orden temporal.", None
    if not baseline_record["shell"] == live["shell"] == stopped["shell"]:
        return "FAIL", "Cambió la identidad del escritorio de Explorer entre puntos de control.", None
    if not live["explorer"] or live["explorer"] != stopped["explorer"]:
        return "FAIL", "La ventana del ensayo de Explorer no conservó su identidad.", None
    quit_event = native_quit(records, binding, live["observedMs"], stopped["observedMs"])
    if not quit_event:
        return "BLOCKED", "Falta un cierre de bandeja posterior al punto de control Durante.", None
    if not {"NexDock", "Archivos"} <= set(live["pilotWindows"]) or not during["owned"]:
        return "BLOCKED", "Falta constancia de las ventanas o procesos activos del piloto.", None
    if stopped["pilotWindows"] or after["remaining"]:
        return "FAIL", "Quedaron ventanas o procesos observados del piloto tras el cierre.", None
    return "PASS", "Explorer respondió durante la convivencia y después del cierre nativo, con los originales intactos.", quit_event


def verify(identity, evidence):
    root, _, binding = context(identity)
    initial = pilot.read_json(evidence / "explorer-baseline.json")
    during, after = (read_checkpoint(evidence, phase) for phase in PHASE_FOLDERS)
    folder = pilot.explorer_folder(root, identity)
    for phase, checkpoint in (("during", during), ("after", after)):
        if checkpoint and checkpoint.get("native"):
            pilot.require(pilot.same_path(checkpoint["native"].get("explorerFolder"), folder / PHASE_FOLDERS[phase]),
                          "Explorer fue observado en una carpeta distinta a la del ensayo.")
    trace = (root / "evidence/tray.jsonl").read_text(encoding="utf-8")
    records = [json.loads(line) for line in trace.split("\n")[:-1] if line]
    intact = pilot.snapshot(folder) == pilot.read_json(root / "explorer-before.json")
    status, reason, quit_event = verdict(binding, initial, during, after, records, intact)
    return dict(binding, status=status, reason=reason, originalsIntact=intact, baseline=initial,
                during=during, after=after, trayQuit=quit_event)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("baseline", "observe", "verify"))
    parser.add_argument("session")
    parser.add_argument("evidence", type=Path)
    parser.add_argument("--phase", choices=PHASE_FOLDERS)
    parser.add_argument("--operator", type=Path)
    args = parser.parse_args()
    if args.mode == "baseline":
        baseline(args.session, args.evidence)
        return 0
    if args.mode == "observe" and not (args.phase and args.operator):
        parser.error("observe requiere --phase y --operator")
    result = (observe(args.session, args.evidence, args.phase, args.operator) if args.mode == "observe"
              else verify(args.session, args.evidence))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return CODES[result["status"]]


if __name__ == "__main__":
    try:
        sys.exit(main())
    except subprocess.CalledProcessError as error:
        print(f"ERROR: {error.stderr or error}", file=sys.stderr)
        sys.exit(3)
    except (OSError, RuntimeError, ValueError, KeyError, subprocess.SubprocessError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(3)
