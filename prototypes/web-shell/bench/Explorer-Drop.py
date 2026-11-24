"""Prepare a physical Explorer drop, then verify SQLite and hashes with --verify DIR."""
import argparse
import ctypes
from ctypes import wintypes
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile

base = Path(__file__).resolve().parents[3] / "artifacts/tauri-nexcore/B"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--verify", type=Path)
parser.add_argument("--pilot", type=Path, default=base)
parser.add_argument("--demo", action="store_true",
                    help="Preparar una escena visual aislada mediante IPC. No verifica el gesto físico.")
args = parser.parse_args()
base = args.pilot.resolve(strict=True)


def snapshot(folder):
    return {
        str(p.relative_to(folder)): hashlib.sha256(p.read_bytes()).hexdigest()
        if p.is_file() else "directory"
        for p in folder.rglob("*")
    }


def integrity(pid):
    """Read the integrity RID for the probe, without changing either process token."""
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    security = ctypes.WinDLL("advapi32", use_last_error=True)
    kernel.OpenProcess.restype = wintypes.HANDLE
    security.GetSidSubAuthorityCount.restype = ctypes.POINTER(ctypes.c_ubyte)
    security.GetSidSubAuthority.restype = ctypes.POINTER(wintypes.DWORD)
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        return "unavailable"
    token = wintypes.HANDLE()
    try:
        if not security.OpenProcessToken(wintypes.HANDLE(handle), 8, ctypes.byref(token)):
            return "unavailable"
        needed = wintypes.DWORD()
        security.GetTokenInformation(token, 25, None, 0, ctypes.byref(needed))
        if not needed.value:
            return "unavailable"
        data = ctypes.create_string_buffer(needed.value)
        if not security.GetTokenInformation(token, 25, data, needed, ctypes.byref(needed)):
            return "unavailable"
        sid = ctypes.cast(data, ctypes.POINTER(ctypes.c_void_p)).contents
        count = security.GetSidSubAuthorityCount(sid).contents.value
        return hex(security.GetSidSubAuthority(sid, count - 1).contents.value)
    finally:
        if token:
            kernel.CloseHandle(token)
        kernel.CloseHandle(wintypes.HANDLE(handle))


if args.verify:
    root = args.verify.resolve(strict=True)
    if root.parent != Path(tempfile.gettempdir()) or not root.name.startswith("NexSpace-Phase4-drop-"):
        raise ValueError("Se requiere una biblioteca creada por este ensayo.")
    folder = root / "Arrastrar a Archivos"
    before = json.loads((root / "before.json").read_text(encoding="utf-8"))
    assert snapshot(folder) == before, "Cambió una ruta o hash del origen"
    with sqlite3.connect((root / "floating.db").as_uri() + "?mode=ro", uri=True) as db:
        targets = dict(db.execute("""SELECT i.target,i.kind FROM items i
            JOIN container_items ci ON ci.item_id=i.id JOIN containers c ON c.id=ci.container_id
            WHERE c.name='Archivos'"""))
    wrong = []
    for path in folder.iterdir():
        expected_kind = 1 if path.is_dir() else 2 if path.suffix == ".lnk" else 0
        if targets.get(str(path)) != expected_kind:
            wrong.append(path.name)
    assert not wrong, f"Faltan {len(wrong)} referencias correctas: {', '.join(wrong)}. Las rutas y hashes originales coinciden."
    print("PASS · cuatro destinos en Archivos y árbol original intacto")
else:
    root = Path(tempfile.mkdtemp(prefix="NexSpace-Phase4-drop-"))
    env = dict(os.environ, NEXSPACE_FLOATING_TEST_DIR=str(root), NEXSPACE_FLOATING_DROP_TRACE="1")
    subprocess.run([base / "core/NexSpace.CorePilot.exe", "--prepare-floating"], env=env, check=True)
    folder = root / "Arrastrar a Archivos"
    folder.mkdir()
    (folder / "Notas de prueba.txt").write_text("Documento aislado de NexSpace.", encoding="utf-8")
    (folder / "Muestra.pdf").write_bytes(b"%PDF-1.4\n% isolated drop target\n%%EOF\n")
    (folder / "Proyecto").mkdir()
    (folder / "Proyecto/testigo.txt").write_text("Original", encoding="utf-8")
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "$s=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $pwd 'Acceso.lnk'));"
                    "$s.TargetPath=(Join-Path $pwd 'Notas de prueba.txt');$s.Save()"], cwd=folder, check=True)
    (root / "before.json").write_text(json.dumps(snapshot(folder)), encoding="utf-8")
    if args.demo:
        import shutil

        application = folder / "Mapa de caracteres.exe"
        shutil.copyfile(Path(os.environ["WINDIR"]) / "System32/charmap.exe", application)
        with subprocess.Popen([base / "core/NexSpace.CorePilot.exe", "--floating"], env=env,
                              stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, encoding="utf-8") as core:
            serial = 0

            def request(op, **data):
                global serial
                serial += 1
                core.stdin.write(json.dumps(dict(v=2, id=serial, op=op, **data)) + "\n")
                core.stdin.flush()
                reply = json.loads(core.stdout.readline())
                assert reply["id"] == serial and not reply["error"], reply
                return reply["data"]

            collection = request("manifest")
            groups = {g["name"]: g["id"] for g in collection["groups"]}
            for group, name in [
                ("Archivos", "Muestra.pdf"), ("Archivos", "Proyecto"), ("Archivos", "Notas de prueba.txt"),
                ("Aplicaciones", application.name), ("Aplicaciones", "Acceso.lnk"),
            ]:
                request("add", profile=collection["profile"], target=groups[group], path=str(folder / name))
            missing = root / "Referencia de ensayo.txt"
            missing.touch()
            request("add", profile=collection["profile"], target=groups["Juegos"], path=str(missing))
            missing.unlink()
            core.stdin.close()
            assert core.wait(5) == 0
    p = subprocess.Popen([base / "nexspace-tauri-lab.exe"], env=env, cwd=base,
                         creationflags=subprocess.DETACHED_PROCESS, stdin=subprocess.DEVNULL,
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if not args.demo:
        os.startfile(folder)
    print(f"Biblioteca aislada: {root}\nPID del piloto: {p.pid}")
    user = ctypes.WinDLL("user32", use_last_error=True)
    user.GetShellWindow.restype = wintypes.HWND
    shell_pid = wintypes.DWORD()
    user.GetWindowThreadProcessId(wintypes.HWND(user.GetShellWindow()), ctypes.byref(shell_pid))
    print(f"Integridad de Explorer (Shell): {integrity(shell_pid.value)} · piloto: {integrity(p.pid)}")
