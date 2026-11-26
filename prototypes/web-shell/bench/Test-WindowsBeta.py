"""Verify the installer and exercise its payload without installing software."""

import argparse
from contextlib import closing
import json
import os
from pathlib import Path
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import pilot_support as pilot


def latest():
    storage = pilot.REPO / "artifacts/beta/windows"
    status = pilot.read_json(storage / "build-status.json")
    pilot.require(status["status"] == "ready", "El último empaquetado no terminó.")
    folder = storage / "builds" / status["id"]
    metadata = pilot.read_json(folder / "package.json")
    source = pilot.source_identity()
    pilot.require(metadata["id"] == status["id"] and metadata["commit"] == source["commit"]
                  and metadata["sourceSha256"] == source["sha256"], "El paquete no corresponde al código actual.")
    return folder


def verify_package(folder, metadata):
    installer = metadata["installer"]
    package = folder / installer["file"]
    pilot.require(package.stat().st_size == installer["bytes"] and pilot.digest(package) == installer["sha256"].lower(),
                  "El hash o el tamaño del instalador cambió.")
    payload = folder / "payload"
    files = {path.relative_to(folder).as_posix(): pilot.digest(path) for path in payload.rglob("*") if path.is_file()}
    pilot.require(files == {key: value.lower() for key, value in metadata["files"].items()}, "El inventario del payload no coincide.")
    required = ("NexSpace.exe", "WebView2Loader.dll", "core/NexSpace.CorePilot.exe", "core/coreclr.dll",
                "core/e_sqlite3.dll", "core/hostfxr.dll")
    for name in required:
        pilot.x64(payload / name)
    runtime = pilot.read_json(payload / "core/NexSpace.CorePilot.runtimeconfig.json")["runtimeOptions"]
    pilot.require("includedFrameworks" in runtime and "framework" not in runtime and "frameworks" not in runtime,
                  "El sidecar depende de un runtime .NET instalado.")
    pilot.require(not any(Path(name).suffix in {".db", ".bak"} or "pilot-data" in name for name in files),
                  "La distribución contiene una base ajena.")
    resources = pilot.read_json(folder / "bundle-resources.json")["bundle"]["resources"]
    script = (folder / "installer.nsi").read_text(encoding="utf-8-sig")
    bundled = {name.removeprefix("payload/") for name in files} - {"NexSpace.exe", "WebView2Loader.dll"}
    pilot.require(set(resources.values()) - {"WebView2Loader.dll"} == bundled,
                  "El instalador omite recursos del payload.")
    source_paths = script.replace("\\", "/").lower()
    for source in resources:
        pilot.require(Path(source).as_posix().lower() in source_paths, f"NSIS no incluye {Path(source).name}.")
    for key, value in {"INSTALLMODE": "currentUser", "INSTALLWEBVIEW2MODE": "downloadBootstrapper",
                       "BUNDLEID": metadata["identifier"], "VERSION": metadata["version"],
                       "ARCH": "x64", "ALLOWDOWNGRADES": "false"}.items():
        pilot.require(f'!define {key} "{value}"' in script, f"Metadato de NSIS incorrecto: {key}.")
    for snippet in ('RequestExecutionLevel user', '/oname=WebView2Loader.dll', 'WriteUninstaller',
                    'WriteRegStr SHCTX "${UNINSTKEY}" "DisplayIcon"', 'CreateShortcut',
                    '${If} $DeleteAppDataCheckboxState = 1'):
        pilot.require(snippet in script, f"Falta una directiva de NSIS: {snippet}.")
    pilot.require(not re.search(r'BM_SETCHECK.*DeleteAppDataCheckbox|DeleteAppDataCheckbox.*BM_SETCHECK', script),
                  "NSIS establece una selección automática para borrar datos.")
    subprocess.run([sys.executable, str(Path(__file__).with_name("Validate-AppIcon.py")), "--exe", str(payload / "NexSpace.exe")], check=True)
    return {"installer": installer, "payloadFiles": len(files), "mappedResources": len(resources)}


def wait_for(predicate, message, timeout=25):
    limit = time.monotonic() + timeout
    while time.monotonic() < limit:
        result = predicate()
        if result:
            return result
        time.sleep(0.2)
    raise RuntimeError(message)


def start_and_close(exe, cwd, data, env, shell, metadata, duplicate=False):
    ready, stop = data / "smoke-ready.json", data / "smoke-stop"
    ready.unlink(missing_ok=True)
    stop.unlink(missing_ok=True)
    args = [str(exe), "--beta-smoke", str(data)]
    with (data / "host-stderr.log").open("ab") as stderr:
        child = subprocess.Popen(args, cwd=cwd, env=env, creationflags=subprocess.CREATE_NO_WINDOW, stderr=stderr)
        try:
            def report():
                pilot.require(child.poll() is None, f"El host terminó durante el arranque: {child.returncode}.")
                return pilot.read_json(ready) if ready.is_file() else None

            runtime = wait_for(report, "El host no creó el recibo de inicio.")
            pilot.require(runtime["build"] == metadata["id"] and runtime["hostPid"] == child.pid
                          and Path(runtime["database"]).samefile(data / "floating.db"),
                          "La app usó un build, proceso o biblioteca distinto.")
            labels = runtime["windows"]
            pilot.require("dock" in labels and sum(label.startswith("group_") for label in labels) == 3,
                          f"Faltan ventanas de la beta: {labels}.")
            rows = pilot.processes(shell)
            owner = next(pilot.pid_key(row) for row in rows if row["ProcessId"] == child.pid)
            tree = pilot.process_tree(rows, owner)
            pilot.require(any(pid == runtime["corePid"] for pid, _ in tree), "NexCore no pertenece al árbol del host.")
            if duplicate:
                contender = subprocess.run(args, cwd=cwd, env=env, timeout=12, creationflags=subprocess.CREATE_NO_WINDOW)
                pilot.require(contender.returncode == 0 and child.poll() is None and pilot.read_json(ready) == runtime,
                              "El bloqueo por biblioteca no preservó la instancia original.")
            stop.touch()
            pilot.require(child.wait(15) == 0, "La beta no cerró normalmente.")
            wait_for(lambda: not tree.intersection(map(pilot.pid_key, pilot.processes(shell))),
                     "Quedaron procesos de la beta.", timeout=15)
            return {**runtime, "observedProcesses": len(tree), "remaining": [], "duplicateChecked": duplicate}
        finally:
            if child.poll() is None:
                stop.touch()
                try:
                    child.wait(5)
                except subprocess.TimeoutExpired:
                    subprocess.run(["taskkill", "/PID", str(child.pid), "/T", "/F"], timeout=10, capture_output=True)


def ipc(core, data, env, requests):
    lines = [json.dumps({"v": 2, "id": index, **request}, ensure_ascii=False)
             for index, request in enumerate(requests, 1)]
    completion = subprocess.run([core, "--floating-beta", data], cwd=data, env=env, encoding="utf-8",
                                input="\n".join(lines) + "\n", capture_output=True, timeout=15,
                                creationflags=subprocess.CREATE_NO_WINDOW)
    pilot.require(completion.returncode == 0, f"El sidecar no inició: {completion.stderr}.")
    responses = [json.loads(line) for line in completion.stdout.splitlines()]
    pilot.require(len(responses) == len(requests) and all(row["id"] == index and row["error"] is None
                  for index, row in enumerate(responses, 1)), f"Falló una escritura IPC: {responses}.")
    return [row["data"] for row in responses]


def query(database, sql):
    with closing(sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)) as connection:
        return connection.execute(sql).fetchall()


def verify_runtime(folder, metadata, persistence, evidence):
    stage = Path(tempfile.mkdtemp(prefix="NexSpace-Phase4-beta-"))
    data = Path(tempfile.mkdtemp(prefix="NexSpace-Phase4-beta-"))
    evidence.update(stage=str(stage), data=str(data))
    payload = stage / "Instalaci\u00f3n de prueba"
    shutil.copytree(folder / "payload", payload)
    env = os.environ.copy()
    for name in tuple(env):
        if name.upper().startswith(("NEXSPACE_", "DOTNET_", "CARGO_", "RUSTUP_")):
            del env[name]
    env["PATH"] = os.pathsep.join(str(Path(os.environ["SystemRoot"]) / folder) for folder in ("System32", "."))
    host, core = payload / "NexSpace.exe", payload / "core/NexSpace.CorePilot.exe"
    shell = pilot.powershell()
    initial = start_and_close(host, stage, data, env, shell, metadata, duplicate=True)
    evidence["firstRun"] = initial
    db = data / "floating.db"
    pilot.require(query(db, "PRAGMA quick_check") == [("ok",)] and query(db, "PRAGMA user_version") == [(3,)],
                  "La base inicial no es íntegra o tiene otro esquema.")
    pilot.require(query(db, "SELECT count(*) FROM items") == [(0,)] and not (data / "recursos").exists(),
                  "El primer inicio creó referencias de laboratorio.")
    pilot.require(all((data / "logs" / name).is_file() for name in ("core.log", "events.jsonl"))
                  and not (payload / "pilot-data").exists() and not (stage / "logs").exists(),
                  "La beta escribió fuera de su biblioteca aislada o faltan sus logs.")
    if not persistence:
        return
    original = stage / "Referencia.txt"
    original.write_bytes(b"Contenido de prueba NexSpace.\n")
    digest = pilot.digest(original)
    profile = ipc(core, data, env, [{"op": "createProfile", "name": "Beta IPC", "flag": True, "profile": initial["profile"]}])[0]
    manifest = ipc(core, data, env, [{"op": "switchProfile", "target": profile, "profile": initial["profile"]}, {"op": "manifest"}])[1]
    group = next(row["id"] for row in manifest["groups"] if row["name"] == "Archivos")
    ipc(core, data, env, [{"op": "add", "target": group, "profile": profile, "path": str(original)},
                          {"op": "collapse", "target": group, "profile": profile, "flag": True}])
    restarted = start_and_close(host, stage, data, env, shell, metadata)
    evidence["restart"] = restarted
    pilot.require(restarted["profile"] == profile and any(row["id"] == group and row["isCollapsed"] for row in restarted["groups"]),
                  "El perfil activo o la contracción no persistieron.")
    reference = "SELECT i.target,c.name FROM items i JOIN container_items ci ON i.id=ci.item_id JOIN containers c ON c.id=ci.container_id"
    pilot.require(query(db, reference) == [(str(original), "Archivos")] and pilot.digest(original) == digest,
                  "La referencia no persistió en Archivos o se alteró el original.")

    # Exercise the actual migration on a copy of the populated database.
    migration = Path(tempfile.mkdtemp(prefix="NexSpace-Phase4-beta-"))
    migrated = migration / "floating.db"
    with closing(sqlite3.connect(db)) as source, closing(sqlite3.connect(migrated)) as target:
        source.backup(target)
        target.execute("PRAGMA user_version=2")
    ipc(core, migration, env, [{"op": "manifest"}])
    backups = list((migration / "backups").glob("floating.db.pre-v3-*.bak"))
    pilot.require(len(backups) == 1 and query(backups[0], "PRAGMA user_version") == [(2,)]
                  and query(backups[0], reference) == query(migrated, reference) == [(str(original), "Archivos")]
                  and query(migrated, "PRAGMA user_version") == [(3,)] and query(migrated, "PRAGMA quick_check") == [("ok",)],
                  "La migración o el respaldo perdió datos o metadatos.")
    with closing(sqlite3.connect(migrated)) as connection:
        connection.execute("PRAGMA user_version=99")
    before = pilot.digest(migrated)
    refused = subprocess.run([core, "--floating-beta", migration], input=b"", capture_output=True, timeout=15,
                             env=env, cwd=stage, creationflags=subprocess.CREATE_NO_WINDOW)
    pilot.require(refused.returncode != 0 and pilot.digest(migrated) == before, "Se alteró un esquema futuro.")
    evidence["persistence"] = {"referenceSha256": digest, "migrationBackup": str(backups[0]), "newerSchemaRefused": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("package", "first-run", "persistence"))
    parser.add_argument("--package", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--adapter", type=Path)
    args = parser.parse_args()
    report = {"status": "FAIL", "mode": args.mode}
    try:
        try:
            package = args.package.resolve() if args.package else latest()
        except (OSError, ValueError, RuntimeError) as error:
            report.update(status="BLOCKED", reason=str(error))
            return 2
        metadata = pilot.read_json(package / "package.json")
        identity = {"buildId": metadata["id"], "gitSha": metadata["commit"], "dirty": metadata["dirty"],
                    "nativeEvidence": str((args.output / "windows-beta.json").resolve())}
        report.update(package=str(package), **identity)
        if args.adapter:
            pilot.write_json(args.adapter, identity)
        report["packageCheck"] = verify_package(package, metadata)
        if args.mode != "package":
            verify_runtime(package, metadata, args.mode == "persistence", report)
        report["status"] = "PASS"
        return 0
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        report["reason"] = str(error)
        return 1
    finally:
        args.output.mkdir(parents=True, exist_ok=True)
        pilot.write_json(args.output / "windows-beta.json", report)
        print(json.dumps(report, ensure_ascii=True))


if __name__ == "__main__":
    sys.exit(main())
