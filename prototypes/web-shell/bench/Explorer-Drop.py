"""Verify the isolated Explorer drop created by Run-Pilot.ps1 -DropTrial."""

import argparse
from collections import Counter
from datetime import datetime
import json
import os
from pathlib import Path
import sqlite3
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pilot_support import (STORAGE, drop_folder, launch, read_json, receipt_path, require,
                           same_path, session_path, snapshot, write_json)


def normalized(path):
    return os.path.normcase(str(Path(path).resolve()))


def ole_evidence(events, session, host_pid, window):
    """Return complete native batches and the time window of each commit."""
    entered = False
    seen = False
    drops, committed = {}, []
    for event in events:
        stage, detail = event["stage"], event["detail"]
        if (event["session"], event["hostPid"], detail.get("window")) != (session, host_pid, window):
            continue
        batch = detail.get("batch")
        if stage == "enter":
            entered = detail["accepted"] is True
        elif stage == "drop":
            seen = True
            if detail["accepted"] is True and entered and type(batch) is int and detail["count"] > 0:
                drops[batch] = (event["ms"], detail["count"], [])
            entered = False
        elif stage == "nexcore" and batch in drops:
            target = detail.get("target")
            accepted = (detail.get("source") == "ole-drop" and detail["ok"] is True
                        and detail["validated"] is True and target and Path(target).is_absolute())
            drops[batch][2].append(normalized(target) if accepted else None)
        elif stage == "persisted" and batch in drops:
            started, count, paths = drops.pop(batch)
            unique_paths = set(paths)
            if (detail["result"] == {"Ok": None} and started <= event["ms"]
                    and len(paths) == len(unique_paths) == count and None not in unique_paths):
                committed.append((started, event["ms"], unique_paths))
    return seen, committed


def persisted_ole_paths(batches, targets):
    """Match stored items to the OLE batch that created them, excluding later re-adds."""
    added = {path: int(datetime.fromisoformat(utc).timestamp() * 1000)
             for path, (_, utc, _) in targets.items()}
    return {path for started, finished, paths in batches for path in paths
            if path in added and started <= added[path] <= finished}


def core_additions(lines, session, profile, group):
    """Read successful adds for the trial group from NexCore's native audit."""
    sources = Counter()
    for line in lines:
        if not line.startswith("{"):
            continue
        entry = json.loads(line)
        if (entry["session"], entry["profile"], entry["target"]) == (session, profile, group) and entry["ok"] is True:
            sources[entry["source"]] += 1
    return sources


def verify(root, *, write_verdict=True):
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
    folder = drop_folder(root, trial)
    unchanged = snapshot(folder) == read_json(root / "before.json")
    expected = {normalized(path): 1 if path.is_dir() else 2 if path.suffix.lower() == ".lnk" else 0
                for path in folder.iterdir()}
    if not unchanged or len(expected) != 4:
        return {"status": "FAIL", "reason": "Alguna ruta o hash de origen cambió."}

    with sqlite3.connect(database.as_uri() + "?mode=ro", uri=True) as db:
        profile, = db.execute("SELECT active_id FROM profile_state WHERE id=1").fetchone()
        require(profile == trial["profile"] == ready["profile"], "Activa el perfil original del ensayo.")
        require(any(group["id"] == trial["group"] and group["name"] == "Archivos" for group in ready["groups"]),
                "El contenedor Archivos no coincide con el que anunció el host.")
        require(db.execute("SELECT id FROM containers WHERE id=? AND name='Archivos'",
                           (trial["group"],)).fetchone() is not None, "Falta el contenedor Archivos del ensayo.")
        targets = {normalized(path): (kind, added, path) for path, kind, added in db.execute("""
            SELECT i.target,i.kind,i.added_utc FROM items i JOIN container_items ci ON ci.item_id=i.id
            WHERE ci.container_id=?""", (trial["group"],))}
    matching = {path for path, kind in expected.items() if path in targets and targets[path][0] == kind}
    trace = evidence / "drop.jsonl"
    events = [json.loads(line) for line in trace.read_text(encoding="utf-8").splitlines()] if trace.is_file() else []
    label = f"group_{profile}_{trial['group']}"
    seen, batches = ole_evidence(events, identity, ready["hostPid"], label)
    confirmed = persisted_ole_paths(batches, targets)
    proven = matching & confirmed
    foreign = confirmed - expected.keys()
    audit = evidence / "core.log"
    lines = audit.read_text(encoding="utf-8").splitlines() if audit.is_file() else []
    additions = core_additions(lines, identity, profile, trial["group"])
    core_ole_adds = additions.pop("ole-drop", 0)
    if not seen:
        status, reason = "BLOCKED", "Sin drop OLE en Archivos de esta sesión. Las altas por selector o IPC no lo acreditan."
    elif additions:
        status, reason = "FAIL", "NexCore registró altas exitosas no OLE en el grupo aislado del ensayo."
    elif proven == expected.keys() and core_ole_adds >= len(proven):
        status, reason = "PASS", "Cuatro altas OLE del mismo destino, cuatro referencias correctas y originales intactos."
    elif foreign and not proven:
        status, reason = "FAIL", "El lote OLE se guardó en Archivos, pero las rutas provienen de otra carpeta."
    else:
        status, reason = "FAIL", "El drop OLE no acredita las cuatro altas y referencias requeridas."
    result = dict(status=status, reason=reason, references=len(matching), originalsIntact=unchanged,
                  oleReferences=len(proven), foreignOleReferences=len(foreign), expectedSource=str(folder),
                  foreignOleSources=sorted({str(Path(targets[path][2]).parent) for path in foreign}),
                  coreOleAdds=core_ole_adds, coreNonOleAdds=dict(additions),
                  session=identity, hostPid=ready["hostPid"], profile=profile, group=trial["group"])
    if write_verdict:
        write_json(evidence / "drop-verdict.json", result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", help="ID de sesión o directorio devuelto por el lanzador.")
    parser.add_argument("--read-only", action="store_true", help="Lee el ensayo sin sobrescribir su veredicto original.")
    parser.add_argument("--powershell", help="Ruta explícita a pwsh.exe o powershell.exe.")
    args = parser.parse_args()
    if args.verify:
        trial = Path(args.verify)
        root = trial.resolve() if trial.is_dir() else session_path(args.verify)
        result = verify(root, write_verdict=not args.read_only)
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
