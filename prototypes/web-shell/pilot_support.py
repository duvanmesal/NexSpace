"""Build and session tooling for the experimental Windows pilot."""

import argparse
from contextlib import contextmanager
import hashlib
import json
import msvcrt
import os
from pathlib import Path
import platform
import re
import shutil
import sqlite3
import struct
import subprocess
import sys
import tempfile
import time
import uuid

REPO = Path(__file__).resolve().parents[2]
STORAGE = REPO / "artifacts/tauri-nexcore"
HOST = "nexspace-tauri-lab.exe"
CORE = "NexSpace.CorePilot.exe"
SOURCE = (
    "prototypes/web-shell/src", "prototypes/web-shell/core", "prototypes/web-shell/public",
    "prototypes/web-shell/src-tauri", "prototypes/web-shell/package.json",
    "prototypes/web-shell/package-lock.json", "prototypes/web-shell/tsconfig.json",
    "prototypes/web-shell/vite.config.ts", "prototypes/web-shell/index.html",
    "prototypes/web-shell/Build-Pilot.ps1", "prototypes/web-shell/Pilot-Tools.ps1",
    "prototypes/web-shell/pilot_support.py",
    "src/NexSpace.Application", "src/NexSpace.Domain", "src/NexSpace.Infrastructure",
    "src/NexSpace.Windows", "Directory.Build.props", "Directory.Build.targets",
    "Directory.Packages.props", "global.json", "NuGet.config",
)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, data):
    pending = path.with_name(f"{path.name}.{uuid.uuid4().hex}.tmp")
    pending.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(pending, path)


def git(*args):
    return subprocess.check_output(["git", "-C", REPO, *args])


def digest(path):
    with path.open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()


def source_identity():
    names = git("ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", *SOURCE)
    parts = sorted(set(names.decode("utf-8").split("\0")) - {""})
    material = hashlib.sha256()
    for name in parts:
        path = REPO / name
        material.update(name.encode("utf-8") + b"\0")
        material.update(bytes.fromhex(digest(path)) if path.is_file() else b"missing")
    return {
        "commit": git("rev-parse", "HEAD").decode().strip(),
        "sha256": material.hexdigest(),
        "dirty": bool(git("status", "--porcelain", "--untracked-files=no")
                      or git("ls-files", "--others", "--exclude-standard", "--", *SOURCE)),
    }


def x64(path):
    with path.open("rb") as file:
        require(file.read(2) == b"MZ", f"No es un ejecutable PE: {path.name}")
        file.seek(0x3C)
        file.seek(struct.unpack("<I", file.read(4))[0])
        require(file.read(6) == b"PE\0\0\x64\x86", f"Se requiere PE x64: {path.name}")


def preflight(node, dotnet):
    require(platform.machine().upper() in {"AMD64", "X86_64"} and struct.calcsize("P") == 8,
            "Usa Python x64 en Windows x64.")
    require(sys.version_info >= (3, 11), "Se requiere Python 3.11 o posterior.")
    x64(Path(node))
    x64(Path(dotnet))
    require(subprocess.check_output([node, "-p", "process.arch"]).strip() == b"x64", "Node debe ser x64.")
    engines = subprocess.check_output([node, "-p", "process.versions.node"], text=True).strip()
    major, minor, *_ = (int(n) for n in engines.split("."))
    require((major == 20 and minor >= 19) or (major == 22 and minor >= 12) or major > 22,
            "Vite 7 requiere Node 20.19+, 22.12+ o una versión posterior compatible.")
    locations = [Path(os.environ[var]) / "Microsoft/EdgeWebView/Application"
                 for var in ("ProgramFiles(x86)", "ProgramFiles", "LOCALAPPDATA") if os.environ.get(var)]
    versions = {p.parent.name for root in locations for p in root.glob("*/msedgewebview2.exe")}
    require(versions, "No se encontró WebView2 Evergreen. Comprueba la instalación de WebView2 Runtime.")
    print("WebView2 Evergreen:", ", ".join(sorted(versions)))


def core_read(build, env, operation):
    request = json.dumps({"v": 2, "id": 1, "op": operation}) + "\n"
    completed = subprocess.run([build / "core" / CORE, "--floating"], input=request,
                               capture_output=True, text=True, encoding="utf-8", env=env,
                               creationflags=subprocess.CREATE_NO_WINDOW, check=True, timeout=15)
    reply = json.loads(completed.stdout)
    require((reply["v"], reply["id"], reply["error"]) == (2, 1, None),
            f"NexCore no confirmó el IPC v2: {reply['error']}")
    return reply["data"]


def verify_source(manifest):
    source = source_identity()
    require((manifest["commit"], manifest["sourceSha256"]) == (source["commit"], source["sha256"]),
            "La compilación corresponde a otros archivos o revisión. Ejecuta Build-Pilot.ps1.")


def publish(build, storage):
    data = read_json(build / "build-input.json")
    verify_source(data)
    for name in (HOST, "WebView2Loader.dll", "core/" + CORE):
        x64(build / name)
    require(data["id"].encode("ascii") in (build / HOST).read_bytes(), "El EXE carece del identificador de este build.")
    require((build / "core/NexSpace.CorePilot.dll").is_file(), "Falta el ensamblado de NexCore.")
    core_read(build, os.environ, "diagnostics")
    payload = [build / HOST, build / "WebView2Loader.dll", *(build / "core").rglob("*")]
    data["files"] = {p.relative_to(build).as_posix(): digest(p) for p in payload if p.is_file()}
    data["validations"] = dict(toolchain="PASS", typescript="PASS", vite="PASS",
                               cargoLocked="PASS", dotnetPublish="PASS", peX64="PASS",
                               embeddedBuildId="PASS", sqliteCoreIpc="PASS")
    write_json(build / "build.json", data)
    write_json(storage / "build-status.json", {"status": "verified", "id": data["id"]})


def build_latest(storage):
    state = storage / "build-status.json"
    require(state.is_file(), "Primero ejecuta Build-Pilot.ps1.")
    pointer = read_json(state)
    require(pointer.get("status") == "verified", "La última compilación no es válida o sigue en curso.")
    identity = pointer["id"]
    require(re.fullmatch(r"[0-9a-f]{8}-[0-9a-f]{32}", identity), "Identificador de build inválido.")
    build = storage / "builds" / identity
    manifest = read_json(build / "build.json")
    require(manifest["id"] == identity, "El manifiesto y el puntero de compilación difieren.")
    verify_source(manifest)
    require({HOST, "WebView2Loader.dll", f"core/{CORE}", "core/NexSpace.CorePilot.dll"} <= manifest["files"].keys(),
            "El manifiesto no contiene los artefactos mínimos del piloto.")
    for name, expected in manifest["files"].items():
        item = build / name
        require(item.resolve().is_relative_to(build.resolve()) and item.is_file() and digest(item) == expected,
                f"Artefacto modificado o ausente: {name}. Vuelve a compilar.")
    return build, manifest


def powershell(explicit=None):
    if explicit:
        path = shutil.which(explicit) or explicit
        require(Path(path).is_file(), "La ruta explícita de PowerShell no existe.")
        return str(Path(path).resolve())
    for name in ("pwsh.exe", "powershell.exe"):
        if path := shutil.which(name):
            return path
    legacy = Path(os.environ.get("WINDIR", r"C:\Windows")) / "System32/WindowsPowerShell/v1.0/powershell.exe"
    require(legacy.is_file(), "Se requiere PowerShell 7 (pwsh.exe) o Windows PowerShell (powershell.exe).")
    return str(legacy)


def snapshot(folder):
    return {p.relative_to(folder).as_posix(): digest(p) if p.is_file() else "directory"
            for p in folder.rglob("*")}


def drop_folder_name(identity):
    return f"Arrastrar a Archivos [{identity[:8]}]"


def drop_folder(root, trial):
    name = trial.get("sourceFolder")
    if name is None:
        return root / "Arrastrar a Archivos"
    require(name == drop_folder_name(trial["session"]), "La carpeta de arrastre no corresponde al ensayo.")
    return root / name


def fixtures(root, shell, identity):
    folder = root / drop_folder_name(identity)
    folder.mkdir()
    (folder / "Notas de prueba.txt").write_text("Documento aislado.", encoding="utf-8")
    (folder / "Muestra.pdf").write_bytes(b"%PDF-1.4\n% NexSpace fixture\n%%EOF\n")
    (folder / "Proyecto").mkdir()
    (folder / "Proyecto/testigo.txt").write_text("Original", encoding="utf-8")
    subprocess.run([shell, "-NoProfile", "-NonInteractive", "-Command",
                    "$s=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $pwd 'Acceso.lnk'));"
                    "$s.TargetPath=(Join-Path $pwd 'Notas de prueba.txt');$s.Save()"], cwd=folder,
                   check=True, timeout=15, creationflags=subprocess.CREATE_NO_WINDOW)
    require((folder / "Acceso.lnk").is_file(), "PowerShell no creó el acceso directo de ensayo.")
    write_json(root / "before.json", snapshot(folder))
    return folder


def processes(shell):
    command = ("[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);"
               "$rows=@(Get-CimInstance Win32_Process | "
               "Select-Object Name,ProcessId,ParentProcessId,ExecutablePath,"
               "@{Name='Started';Expression={$_.CreationDate.ToFileTimeUtc().ToString()}});"
               "ConvertTo-Json -InputObject $rows -Compress")
    return json.loads(subprocess.check_output([shell, "-NoProfile", "-NonInteractive", "-Command", command],
                                              encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW, timeout=15))


def pid_key(row):
    return row["ProcessId"], row["Started"]


def session_path(identity):
    require(re.fullmatch(r"[a-f0-9]{32}", identity), "Identificador de sesión inválido.")
    return Path(tempfile.gettempdir()) / f"NexSpace-Phase4-pilot-{identity}"


def receipt_path(storage, identity):
    session_path(identity)
    return storage / "sessions" / f"{identity}.json"


def active_sessions(storage, rows):
    live = {pid_key(row): row for row in rows if row["Name"].lower() == HOST}
    result = {}
    for index in (storage / "sessions").glob("*.json"):
        receipt = read_json(index)
        key = tuple(receipt["process"])
        if key in live and same_path(live[key]["ExecutablePath"], receipt["executable"]):
            result[key] = receipt
    return live, result


def same_path(left, right):
    return bool(left and right) and os.path.normcase(str(Path(left).resolve())) == os.path.normcase(str(Path(right).resolve()))


@contextmanager
def launch_gate(storage):
    storage.mkdir(parents=True, exist_ok=True)
    with (storage / "launch.lock").open("a+b") as file:
        file.seek(0)
        try:
            msvcrt.locking(file.fileno(), msvcrt.LK_NBLCK, 1)
        except OSError as error:
            raise RuntimeError("Otro lanzador está preparando una sesión de NexSpace.") from error
        try:
            yield
        finally:
            file.seek(0)
            msvcrt.locking(file.fileno(), msvcrt.LK_UNLCK, 1)


def launch(storage, demo=None, drop=False, allow_parallel=False, shell=None, open_explorer=True, *, identity=None):
    shell = powershell(shell)
    with launch_gate(storage):
        build, manifest = build_latest(storage)
        rows = processes(shell)
        live, known = active_sessions(storage, rows)
        for key, row in live.items():
            session = known.get(key)
            origin = "mismo ejecutable" if same_path(row["ExecutablePath"], build / HOST) else "otro ejecutable"
            library = f"sesión {session['id']}, base {session['database']}" if session else "biblioteca desconocida de un arranque anterior"
            print(f"Instancia existente {key[0]} ({origin}): {library}", file=sys.stderr)
        require(not live or allow_parallel, "Ya hay un piloto abierto. Ciérralo desde su bandeja o usa -AllowParallel para otra copia aislada.")
        identity = identity if identity is not None else uuid.uuid4().hex
        root = session_path(identity)
        evidence = root / "evidence"
        require(not root.exists(), "El directorio de la sesión ya existe.")
        evidence.mkdir(parents=True)
        env = dict(os.environ, NEXSPACE_FLOATING_TEST_DIR=str(root), NEXSPACE_PILOT_SESSION_ID=identity)
        env.pop("NEXSPACE_FLOATING_DROP_TRACE", None)
        if drop:
            env["NEXSPACE_FLOATING_DROP_TRACE"] = "1"
        prepare = [build / "core" / CORE, "--prepare-floating"]
        if demo:
            require(not drop, "El ensayo físico usa su propia biblioteca de demostración.")
            prepare.append(demo.resolve(strict=True))
        subprocess.run(prepare, env=env, check=True, timeout=20, creationflags=subprocess.CREATE_NO_WINDOW)
        folder = fixtures(root, shell, identity) if drop else None
        target = None
        if drop:
            data = core_read(build, env, "manifest")
            target = next(group["id"] for group in data["groups"] if group["name"] == "Archivos")
            write_json(root / "drop-trial.json", {"session": identity, "profile": data["profile"], "group": target,
                                                  "sourceFolder": folder.name})
        with (evidence / "host.log").open("ab") as log:
            child = subprocess.Popen([build / HOST], cwd=build, env=env, stdin=subprocess.DEVNULL,
                                     stdout=log, stderr=log, creationflags=subprocess.DETACHED_PROCESS)
        rows = processes(shell)
        owner = next((row for row in rows if row["ProcessId"] == child.pid), None)
        require(owner is not None and same_path(owner["ExecutablePath"], build / HOST),
                f"No se pudo identificar el host iniciado. Consulta {evidence}.")
        receipt = dict(id=identity, executable=str(build / HOST), build=manifest["id"],
                       commit=manifest["commit"], builtUtc=manifest["builtUtc"],
                       database=str(root / "floating.db"), evidence=str(evidence), process=pid_key(owner))
        index = receipt_path(storage, identity)
        index.parent.mkdir(parents=True, exist_ok=True)
        write_json(index, receipt)
        ready = evidence / "ready.json"
        end = time.monotonic() + 20
        while not ready.is_file() and child.poll() is None and time.monotonic() < end:
            time.sleep(.1)
        require(ready.is_file() and child.poll() is None, f"NexCore no confirmó la sesión {identity}. Consulta {evidence}.")
        report = read_json(ready)
        rows = processes(shell)
        require(report["session"] == identity and report["build"] == manifest["id"]
                and report["hostPid"] == child.pid and same_path(report["executable"], receipt["executable"])
                and same_path(report["database"], receipt["database"])
                and any(r["ProcessId"] == report["corePid"] and r["ParentProcessId"] == child.pid
                        and same_path(r["ExecutablePath"], build / "core" / CORE) for r in rows),
                "La respuesta de NexCore no coincide con el host, la biblioteca y la compilación iniciados.")
        if folder and open_explorer:
            os.startfile(folder)
        return {**receipt, **report, "dropTarget": target, "dropSource": str(folder) if folder else None}


def stop_session(storage, identity, shell):
    receipt = read_json(receipt_path(storage, identity))
    root = session_path(identity)
    rows = processes(shell)
    live, known = active_sessions(storage, rows)
    owner = tuple(receipt["process"])
    require(owner in known and known[owner]["id"] == identity, "El PID original de esta sesión ya no está activo.")
    tree = {owner[0]}
    for _ in range(len(rows)):
        extended = tree | {r["ProcessId"] for r in rows if r["ParentProcessId"] in tree and r["Started"] >= owner[1]}
        if extended == tree:
            break
        tree = extended
    owned = {pid_key(row) for row in rows if row["ProcessId"] in tree}
    write_json(root / "evidence/stop.json", {"session": identity})
    deadline = time.monotonic() + 12
    while time.monotonic() < deadline:
        alive = {pid_key(row) for row in processes(shell)} & owned
        if not alive:
            return {"session": identity, "status": "closed", "observedProcesses": len(owned), "remaining": []}
        time.sleep(.25)
    raise RuntimeError(f"La salida normal dejó procesos observados: {sorted(alive)}. Consulta {root / 'evidence'}.")


def show_status(storage, identity, shell):
    rows = processes(shell)
    live, known = active_sessions(storage, rows)
    if not identity:
        return [known.get(key, {"pid": key[0], "executable": row["ExecutablePath"], "database": None})
                for key, row in live.items()]
    receipt = read_json(receipt_path(storage, identity))
    ready = session_path(identity) / "evidence/ready.json"
    return {**receipt, **(read_json(ready) if ready.is_file() else {}),
            "running": tuple(receipt["process"]) in known}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("source")
    commands.add_parser("latest", help="Emitir la carpeta de la última compilación verificada.")
    pe = commands.add_parser("x64")
    pe.add_argument("binary", type=Path)
    check = commands.add_parser("preflight")
    check.add_argument("node")
    check.add_argument("dotnet")
    release = commands.add_parser("publish")
    release.add_argument("build", type=Path)
    release.add_argument("storage", type=Path)
    run = commands.add_parser("run")
    run.add_argument("--demo", type=Path)
    run.add_argument("--drop", action="store_true")
    run.add_argument("--allow-parallel", action="store_true")
    run.add_argument("--no-explorer", action="store_true")
    run.add_argument("--powershell")
    run.add_argument("--id", help="Identificador reservado por Test Center.")
    for name in ("status", "stop"):
        command = commands.add_parser(name)
        command.add_argument("session", nargs="?" if name == "status" else None)
        command.add_argument("--powershell")
    args = parser.parse_args()
    if args.command == "source":
        print(json.dumps(source_identity()))
    elif args.command == "latest":
        print(build_latest(STORAGE)[0])
    elif args.command == "x64":
        x64(args.binary)
    elif args.command == "preflight":
        preflight(args.node, args.dotnet)
    elif args.command == "publish":
        publish(args.build, args.storage)
    else:
        if args.command == "run":
            result = launch(STORAGE, args.demo, args.drop, args.allow_parallel, args.powershell, not args.no_explorer,
                            identity=args.id)
        else:
            action = show_status if args.command == "status" else stop_session
            result = action(STORAGE, args.session, powershell(args.powershell))
        print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"NexSpace: {error}", file=sys.stderr)
        sys.exit(1)
