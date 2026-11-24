"""Prepare a physical Explorer drop, then verify SQLite and hashes with --verify DIR."""
import argparse
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
args = parser.parse_args()


def snapshot(folder):
    return {
        str(p.relative_to(folder)): hashlib.sha256(p.read_bytes()).hexdigest()
        if p.is_file() else "directory"
        for p in folder.rglob("*")
    }


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
    for path in folder.iterdir():
        expected_kind = 1 if path.is_dir() else 2 if path.suffix == ".lnk" else 0
        assert targets.get(str(path)) == expected_kind, f"Falta la referencia correcta a {path.name}"
    print("PASS · cuatro destinos en Archivos y árbol original intacto")
else:
    root = Path(tempfile.mkdtemp(prefix="NexSpace-Phase4-drop-"))
    env = dict(os.environ, NEXSPACE_FLOATING_TEST_DIR=str(root))
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
    p = subprocess.Popen([base / "nexspace-tauri-lab.exe"], env=env, cwd=base,
                         creationflags=subprocess.DETACHED_PROCESS, stdin=subprocess.DEVNULL,
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    os.startfile(folder)
    print(f"Biblioteca aislada: {root}\nPID del piloto: {p.pid}")
