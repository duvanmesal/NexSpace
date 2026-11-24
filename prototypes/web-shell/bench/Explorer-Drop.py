"""Verify the isolated Explorer drop created by Run-Pilot.ps1 -DropTrial."""

import argparse
from collections import Counter
import json
import os
from pathlib import Path
import sqlite3
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pilot_support import (STORAGE, launch, read_json, receipt_path, require,
                           same_path, session_path, snapshot, write_json)


def normalized(path):
    return os.path.normcase(str(Path(path).resolve()))


def ole_evidence(events, expected, session, host_pid, window):
    """Tie each accepted absolute path and committed reply to one native drop batch."""
    bound = [event for event in events if (event["session"], event["hostPid"], event["detail"].get("window"))
             == (session, host_pid, window)]
    entered = False
    drops, committed, accepted = {}, set(), {}
    for event in bound:
        stage, detail = event["stage"], event["detail"]
        batch = detail.get("batch")
        if stage == "enter" and detail["accepted"]:
            entered = True
        elif stage == "drop" and detail["accepted"] and entered and type(batch) is int:
            drops[batch] = detail["count"]
            entered = False
        elif stage == "nexcore" and detail.get("source") == "ole-drop" and batch in drops:
            if detail["ok"] and detail["validated"]:
                accepted.setdefault(batch, []).append(normalized(detail["target"]))
        elif stage == "persisted" and batch in drops and detail["result"] == {"Ok": None}:
            committed.add(batch)
    result = Counter(path for batch in committed if len(accepted.get(batch, [])) == drops[batch]
                     for path in accepted[batch])
    return any(event["stage"] == "drop" for event in bound), result == Counter(expected.keys())


def verify(root):
    identity = root.name.removeprefix("NexSpace-Phase4-pilot-")
    if not (root / "drop-trial.json").is_file():
        return {"status": "BLOCKED", "reason": "Ensayo sin identidad verificable. Abre uno con Run-Pilot.ps1 -DropTrial."}
    require(same_path(root, session_path(identity)), "La ruta no corresponde a la sesión.")
    trial = read_json(root / "drop-trial.json")
    evidence = root / "evidence"
    ready = read_json(evidence / "ready.json")
    receipt = read_json(receipt_path(STORAGE, identity))
    database = root / "floating.db"
    require(trial["session"] == ready["session"] == receipt["id"] == identity
            and ready["hostPid"] == receipt["process"][0]
            and ready["build"] == receipt["build"]
            and same_path(ready["executable"], receipt["executable"])
            and same_path(ready["database"], database) and same_path(receipt["database"], database),
            "Sesión, compilación o biblioteca incoherente.")
    if not ready["tracing"]:
        return {"status": "BLOCKED", "reason": "La instrumentación no está activa en el host."}
    folder = root / "Arrastrar a Archivos"
    unchanged = snapshot(folder) == read_json(root / "before.json")
    expected = {normalized(path): 1 if path.is_dir() else 2 if path.suffix.lower() == ".lnk" else 0
                for path in folder.iterdir()}
    if not unchanged or len(expected) != 4:
        return {"status": "FAIL", "reason": "Alguna ruta o hash de origen cambió."}

    with sqlite3.connect(database.as_uri() + "?mode=ro", uri=True) as db:
        profile, = db.execute("SELECT active_id FROM profile_state WHERE id=1").fetchone()
        require(profile == trial["profile"] == ready["profile"], "Activa el perfil original del ensayo.")
        require(db.execute("SELECT id FROM containers WHERE id=? AND name='Archivos'",
                           (trial["group"],)).fetchone() is not None, "Falta el contenedor Archivos del ensayo.")
        targets = {normalized(path): kind for path, kind in db.execute("""
            SELECT i.target,i.kind FROM items i JOIN container_items ci ON ci.item_id=i.id
            WHERE ci.container_id=?""", (trial["group"],))}
    count = sum(targets.get(path) == kind for path, kind in expected.items())
    trace = evidence / "drop.jsonl"
    events = [json.loads(line) for line in trace.read_text(encoding="utf-8").splitlines()] if trace.is_file() else []
    label = f"group_{profile}_{trial['group']}"
    seen, proven = ole_evidence(events, expected, identity, ready["hostPid"], label)
    if count == 4 and proven:
        status, reason = "PASS", "Cuatro altas OLE del mismo destino, cuatro referencias correctas y originales intactos."
    elif not seen:
        status, reason = "BLOCKED", "Sin drop OLE en Archivos de esta sesión. Las altas por selector o IPC no lo acreditan."
    else:
        status, reason = "FAIL", "El drop OLE no acredita las cuatro altas y referencias requeridas."
    result = dict(status=status, reason=reason, references=count, originalsIntact=unchanged,
                  session=identity, hostPid=ready["hostPid"], profile=profile, group=trial["group"])
    write_json(evidence / "drop-verdict.json", result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", help="ID de sesión o directorio devuelto por el lanzador.")
    parser.add_argument("--powershell", help="Ruta explícita a pwsh.exe o powershell.exe.")
    args = parser.parse_args()
    if args.verify:
        trial = Path(args.verify)
        root = trial.resolve() if trial.is_dir() else session_path(args.verify)
        result = verify(root)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return {"PASS": 0, "FAIL": 1, "BLOCKED": 2}[result["status"]]
    print(json.dumps(launch(STORAGE, drop=True, shell=args.powershell), ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, OSError, ValueError, sqlite3.Error) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
