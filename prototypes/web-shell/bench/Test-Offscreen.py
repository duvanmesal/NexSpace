"""Verify a persisted offscreen group on real HWNDs using a fresh demo library."""
import argparse
import ctypes
from ctypes import wintypes
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("pilot", type=Path)
parser.add_argument("--capture", action="store_true")
display = parser.add_mutually_exclusive_group()
display.add_argument("--secondary", action="store_true")
display.add_argument("--missing-monitor", action="store_true")
args = parser.parse_args()
pilot = args.pilot.resolve(strict=True)
bench = Path(__file__).resolve().parent
root = Path(tempfile.mkdtemp(prefix="NexSpace-Phase4-offscreen-"))
env = dict(os.environ, NEXSPACE_FLOATING_TEST_DIR=str(root))
subprocess.run([pilot / "core/NexSpace.CorePilot.exe", "--prepare-floating"], env=env, check=True)
secondary = None
if args.secondary:
    import json

    result = subprocess.check_output(["pwsh", "-NoProfile", "-Command", """
        Add-Type -AssemblyName System.Windows.Forms
        $s=[Windows.Forms.Screen]::AllScreens | Where-Object {-not $_.Primary} | Select-Object -First 1
        if(!$s){exit 1}
        [ordered]@{key=$s.DeviceName;x=$s.WorkingArea.Left;y=$s.WorkingArea.Top;
            right=$s.WorkingArea.Right;bottom=$s.WorkingArea.Bottom}|ConvertTo-Json -Compress
    """], text=True, creationflags=subprocess.CREATE_NO_WINDOW)
    secondary = json.loads(result)
with sqlite3.connect(root / "floating.db") as db:
    if secondary:
        update = db.execute("UPDATE containers SET monitor_key=?,x=?,y=? WHERE name='Archivos'",
                            (secondary["key"], secondary["x"] + 50, secondary["y"] + 50))
    elif args.missing_monitor:
        update = db.execute("UPDATE containers SET monitor_key=?,x=-65000,y=-65000 WHERE name='Archivos'",
                            ("NexSpace-test-disconnected-device",))
    else:
        update = db.execute("UPDATE containers SET x=-65000,y=-65000 WHERE name='Archivos'")
    assert update.rowcount == 1

user = ctypes.WinDLL("user32", use_last_error=True)
user.GetDpiForWindow.argtypes = [wintypes.HWND]
user.GetDpiForWindow.restype = wintypes.UINT
user.GetWindowDpiAwarenessContext.argtypes = [wintypes.HWND]
user.GetWindowDpiAwarenessContext.restype = wintypes.HANDLE
user.GetAwarenessFromDpiAwarenessContext.argtypes = [wintypes.HANDLE]
user.GetAwarenessFromDpiAwarenessContext.restype = ctypes.c_int
visit_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
windows = {}

@visit_type
def visit(hwnd, _):
    owner = wintypes.DWORD()
    user.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
    if owner.value == app.pid and user.IsWindowVisible(hwnd):
        title = ctypes.create_unicode_buffer(256)
        user.GetWindowTextW(hwnd, title, len(title))
        windows[title.value] = hwnd
    return True

app = None
try:
    app = subprocess.Popen([pilot / "nexspace-tauri-lab.exe"], cwd=pilot, env=env)
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline and app.poll() is None:
        windows.clear()
        user.EnumWindows(visit, 0)
        if {"NexDock", "Archivos", "Juegos", "Aplicaciones"} <= windows.keys():
            break
        time.sleep(.1)
    rect = wintypes.RECT()
    assert user.GetWindowRect(windows["Archivos"], ctypes.byref(rect)), "Sin geometría"
    work = wintypes.RECT()
    if secondary:
        work = wintypes.RECT(secondary["x"], secondary["y"], secondary["right"], secondary["bottom"])
    else:
        assert user.SystemParametersInfoW(0x0030, 0, ctypes.byref(work), 0), "Sin área de trabajo"
    assert work.left <= rect.left < rect.right <= work.right
    assert work.top <= rect.top < rect.bottom <= work.bottom
    panel, dock = windows["Archivos"], windows["NexDock"]
    awareness = user.GetAwarenessFromDpiAwarenessContext(user.GetWindowDpiAwarenessContext(panel))
    print(f"PASS · Archivos restaurado dentro del área de trabajo: {rect.left},{rect.top}–{rect.right},{rect.bottom}. "
          f"DPI de Archivos: {user.GetDpiForWindow(panel)}. DPI del dock: {user.GetDpiForWindow(dock)}. "
          f"Conciencia DPI Win32: {awareness} (2 = por monitor).")
    if args.missing_monitor:
        with sqlite3.connect((root / "floating.db").as_uri() + "?mode=ro", uri=True) as db:
            saved = db.execute("SELECT monitor_key,x,y FROM containers WHERE name='Archivos'").fetchone()
        assert saved == ("NexSpace-test-disconnected-device", -65000, -65000), "El arranque reescribió la ubicación histórica."
    if args.capture:
        time.sleep(1.5)
        for title, surface in (("Archivos", "contenedor"), ("NexDock", "dock")):
            subprocess.run(["pwsh", "-NoProfile", "-File", bench / "Capture-Floating.ps1",
                            "-PilotId", str(app.pid), "-WindowId", str(windows[title]),
                            "-Surface", surface, "-WindowOnly"], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
finally:
    # Terminate only these test instances. Normal shutdown is measured in Compare-Floating.
    if app and app.poll() is None:
        subprocess.run(["taskkill", "/PID", str(app.pid), "/T", "/F"], capture_output=True)
        app.wait(timeout=10)
