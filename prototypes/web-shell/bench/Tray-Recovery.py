"""Verify the genuine native tray recovery recorded by a managed tray trial."""

import argparse
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pilot_support import STORAGE, read_json, receipt_path, require, same_path, session_path


def check_sequence(records, identity, host_pid, build):
    started = False
    hidden = pending = failure = None
    for record in records:
        if (record.get("session"), record.get("hostPid"), record.get("build")) != (identity, host_pid, build):
            continue
        stage, detail = record["stage"], record["detail"]
        if stage == "tray-ready":
            started = detail.get("dockIsVisible") is True
            hidden = pending = failure = None
        elif not started:
            continue
        elif stage == "dock-hidden":
            pending = None
            hidden = record if (detail.get("dockWasVisible") is True and detail.get("hideSucceeded") is True
                                and detail.get("dockIsVisible") is False) else None
            if detail.get("dockWasVisible") is True and hidden is None:
                failure = ("El cierre nativo de NexDock no confirmó que la ventana quedara oculta.", [record])
        elif stage == "tray-recover":
            attempt = detail.get("attempt")
            if detail.get("dockWasVisible") is not False:
                hidden = None
            pending = record if hidden and type(attempt) is int and attempt > 0 else None
        elif (stage == "tray-recovered" and pending and type(detail.get("attempt")) is int
              and detail["attempt"] == pending["detail"]["attempt"]):
            if detail.get("showSucceeded") is True and detail.get("dockIsVisible") is True:
                return ("PASS", "NexDock se ocultó y el menú nativo confirmó su recuperación.",
                        [hidden, pending, record])
            failure = ("El menú nativo recibió la acción con NexDock oculto y la recuperación no lo dejó visible.",
                       [hidden, pending, record])
            if detail.get("dockIsVisible") is not False:
                hidden = None
            pending = None
        elif stage == "tray-quit":
            break

    if failure:
        return "FAIL", *failure
    if not started:
        return "ERROR", "La traza no confirma que NexDock estuviera visible al iniciar el ensayo.", []
    return ("BLOCKED", "Falta el recorrido nativo completo: ocultar NexDock y recuperarlo desde su menú de bandeja.",
            [])


def verify(identity):
    root = session_path(identity)
    evidence = root / "evidence"
    receipt = read_json(receipt_path(STORAGE, identity))
    ready = read_json(evidence / "ready.json")
    require(receipt.get("trial") == "windows-tray" and ready.get("trayTracing") is True,
            "El host no fue iniciado como ensayo instrumentado de bandeja.")
    require(receipt["id"] == ready["session"] == identity and receipt["process"][0] == ready["hostPid"]
            and receipt["build"] == ready["build"] and same_path(receipt["executable"], ready["executable"])
            and same_path(receipt["evidence"], evidence)
            and same_path(receipt["database"], root / "floating.db")
            and same_path(ready["database"], root / "floating.db"),
            "La traza, el host y el recibo no corresponden a la misma sesión.")
    trace = evidence / "tray.jsonl"
    raw = trace.read_text(encoding="utf-8")
    # Ignore only an incomplete trailing line while the native host is appending it.
    records = (json.loads(line) for line in raw.split("\n")[:-1] if line)
    status, reason, sequence = check_sequence(records, identity, ready["hostPid"], ready["build"])
    return dict(status=status, reason=reason, session=identity, hostPid=ready["hostPid"],
                sidecarPid=ready["corePid"], buildId=ready["build"], source="native-tray-menu",
                trace=str(trace), sequence=sequence)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", required=True)
    args = parser.parse_args()
    try:
        result = verify(args.verify)
    except (OSError, RuntimeError, ValueError, KeyError) as error:
        result = dict(status="ERROR", reason=str(error), session=args.verify)
    print(json.dumps(result, indent=2, ensure_ascii=False))
    sys.exit({"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3}[result["status"]])
