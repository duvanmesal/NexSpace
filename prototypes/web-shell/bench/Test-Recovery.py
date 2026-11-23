"""Ejercita ventanas, reinicio WebView, EOF, timeout y cancelación en el binario publicado."""
import pathlib
import sqlite3
import subprocess
import tempfile
import time

base = pathlib.Path(__file__).resolve().parents[3] / "artifacts/web-lab/B"
exe = base / "nexspace-tauri-lab.exe"
proc = subprocess.Popen([exe, "--validate"], cwd=base)
def wait_for(find, seconds=25):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        value = find()
        if value:
            return value
        if proc.poll() is not None:
            raise RuntimeError(f"Salida inesperada: {proc.returncode}")
        time.sleep(.08)
    raise TimeoutError("No llegó la señal del piloto")

try:
    session = wait_for(lambda: next(pathlib.Path(tempfile.gettempdir()).glob(f"NexSpace-WebLab-B-{proc.pid}-*"), None))
    wait_for(lambda: (session/"geometry.ok").exists())
    db = sqlite3.connect((base/"pilot-data/library.db").as_uri()+"?mode=rw", uri=True)
    try:
        db.execute("BEGIN EXCLUSIVE")
        (session/"db.locked").write_text("locked")
        wait_for(lambda: (session/"ipc.ok").exists())
    finally:
        db.rollback()
        db.close()
        (session/"db.released").write_text("released")
    assert proc.wait(timeout=15) == 0, (session/"validation.result").read_text()
    print("OK: mover, redimensionar, contraer/recrear WebView, expandir, recuperar ventanas ocultas, timeout, cancelación y reconexión.")
finally:
    if proc.poll() is None:
        proc.kill()
        proc.wait()
