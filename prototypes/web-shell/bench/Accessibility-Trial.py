"""Bind native UI Automation snapshots and operator observations to the accessibility trial."""

import argparse
import json
from pathlib import Path
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import pilot_support as pilot

PHASES = ("keyboard", "narrator")
CODES = {"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3}


def context(identity):
    root = pilot.session_path(identity)
    receipt = pilot.read_json(pilot.receipt_path(pilot.STORAGE, identity))
    ready = pilot.read_json(root / "evidence/ready.json")
    host_pid, started = receipt["process"]
    pilot.require(receipt.get("trial") == "windows-accessibility"
                  and receipt["id"] == ready["session"] == identity
                  and receipt["build"] == ready["build"] and host_pid == ready["hostPid"]
                  and pilot.same_path(receipt["executable"], ready["executable"])
                  and pilot.same_path(receipt["evidence"], root / "evidence")
                  and pilot.same_path(receipt["database"], root / "floating.db")
                  and pilot.same_path(ready["database"], root / "floating.db"),
                  "El host y el recibo no corresponden al ensayo de accesibilidad.")
    return dict(session=identity, buildId=ready["build"], gitSha=receipt["commit"],
                hostPid=host_pid, started=started, sidecarPid=ready["corePid"])


def snapshot(binding, *, popover=False):
    args = [pilot.powershell(), "-NoProfile", "-NonInteractive", "-STA", "-ExecutionPolicy", "Bypass",
            "-File", Path(__file__).with_name("Accessibility-Snapshot.ps1"), "-PilotId", str(binding["hostPid"])]
    if popover:
        args.append("-Popover")
    completed = subprocess.run(args, check=True, capture_output=True, encoding="utf-8",
                               timeout=38, creationflags=subprocess.CREATE_NO_WINDOW)
    observed = json.loads(completed.stdout)
    # CIM rounds process creation times to microseconds; Get-Process keeps FILETIME precision.
    pilot.require(observed["hostPid"] == binding["hostPid"]
                  and int(observed["started"]) // 10 == int(binding["started"]) // 10,
                  "La captura de UI Automation pertenece a otro proceso.")
    return observed


def window_named(observed, name):
    return next((window for window in observed["windows"] if window["name"] == name), None)


def missing_controls(window, *names):
    if window is None:
        return ["La ventana no apareció en UI Automation."]
    missing = []
    if window["role"] != "ControlType.Window":
        missing.append(f"{window['name']} se expuso como {window['role']}.")
    for name in names:
        if not any(control["name"] == name and control["role"] == "ControlType.Button" and control["focusable"]
                   for control in window["controls"]):
            missing.append(f"{window['name']}: falta el botón enfocable «{name}» en UI Automation.")
    return missing


def popup(observed, baseline):
    island = window_named(baseline, "Juegos")
    if island is None:
        return None
    return next((window for window in observed["windows"]
                 if window["name"] == "Juegos" and window["hwnd"] != island["hwnd"]), None)


def observe(binding, folder, phase, operator):
    pilot.require(operator["outcome"] in ("pass", "fail", "blocked") and operator["note"].strip(),
                  "Indica un resultado observado y una nota.")
    checkpoint = dict(binding, phase=phase, operator=operator)
    if operator["outcome"] != "pass":
        status = operator["outcome"].upper()
        return dict(status=status, reason="Se registró la observación del operador.", checkpoint=checkpoint)
    data = snapshot(binding, popover=True)
    baseline = pilot.read_json(folder / "uia-initial.json")["native"]
    if popup(data, baseline) is None:
        return dict(status="BLOCKED", reason="Deja abierto el popover Juegos y repite la observación.", native=data)
    if phase == "narrator" and not data["narratorPids"]:
        return dict(status="BLOCKED", reason="Mantén Narrador activo al registrar sus anuncios.", native=data)
    checkpoint["native"] = data
    return dict(status="BLOCKED", reason="Observación guardada. Completa el recorrido y ejecuta -Verify.",
                checkpoint=checkpoint)


def verdict(binding, initial, observations):
    observations = {phase: observations.get(phase) for phase in PHASES}
    records = [initial, *(record for record in observations.values() if record is not None)]
    if any(any(record.get(key) != value for key, value in binding.items()) for record in records):
        return "ERROR", "La evidencia pertenece a otro build, host o sesión.", []
    for phase, record in observations.items():
        if record is None:
            continue
        operator = record.get("operator", {})
        if (record.get("phase") != phase or operator.get("outcome") not in ("pass", "fail", "blocked")
                or not operator.get("note", "").strip()):
            return "ERROR", "La observación está incompleta.", []
        if operator["outcome"] == "fail":
            return "FAIL", f"El operador registró un defecto en la fase {phase}.", []
    baseline = initial["native"]
    findings = missing_controls(window_named(baseline, "NexDock"), "Organizar espacio y perfiles", "Juegos")
    findings += missing_controls(window_named(baseline, "Juegos"), "Opciones", "Contraer", "Controles.txt")
    for phase, record in observations.items():
        if not record or record["operator"]["outcome"] != "pass":
            continue
        data = record.get("native")
        if not data or (phase == "narrator" and not data.get("narratorPids")):
            return "BLOCKED", f"Falta evidencia técnica de la fase {phase}.", findings
        findings += missing_controls(popup(data, baseline), "Opciones", "Cerrar categoría", "Controles.txt")
    if findings:
        return "FAIL", "La inspección de UI Automation encontró discrepancias.", list(dict.fromkeys(findings))
    if any(not record or record["operator"]["outcome"] != "pass" for record in observations.values()):
        return "BLOCKED", "Faltan comprobaciones físicas de teclado o Narrador.", []
    return "PASS", "Teclado y Narrador confirmados por el operador con nombres y roles comprobados en UI Automation.", []


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("baseline", "observe", "verify"))
    parser.add_argument("session")
    parser.add_argument("evidence", type=Path)
    parser.add_argument("--phase", choices=PHASES)
    parser.add_argument("--operator", type=Path)
    args = parser.parse_args()
    binding = context(args.session)
    if args.mode == "baseline":
        pilot.write_json(args.evidence / "uia-initial.json", dict(binding, native=snapshot(binding)))
        return 0
    if args.mode == "observe":
        if not args.phase or not args.operator:
            parser.error("observe requiere --phase y --operator")
        result = observe(binding, args.evidence, args.phase, pilot.read_json(args.operator))
    else:
        baseline = pilot.read_json(args.evidence / "uia-initial.json")
        observations = {}
        for phase in PHASES:
            path = args.evidence / f"accessibility-{phase}.json"
            observations[phase] = pilot.read_json(path) if path.is_file() else None
        status, reason, findings = verdict(binding, baseline, observations)
        result = dict(binding, status=status, reason=reason, findings=findings, observations=observations,
                      baselineFile=str(args.evidence / "uia-initial.json"))
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
