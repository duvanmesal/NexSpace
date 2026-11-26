"""Read effective monitor DPI and collect physical-test checkpoints for a managed pilot."""

import argparse
import ctypes as ct
from ctypes import wintypes as w
from datetime import UTC, datetime
import json
from pathlib import Path
import sqlite3
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import pilot_support as pilot

USER = ct.WinDLL("user32", use_last_error=True)
MONITOR_VISITOR = ct.WINFUNCTYPE(w.BOOL, w.HMONITOR, w.HDC, ct.POINTER(w.RECT), w.LPARAM)
WINDOW_VISITOR = ct.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
SURFACES = {"NexDock", "Archivos", "Juegos", "Aplicaciones"}


class MonitorInfo(ct.Structure):
    _fields_ = [("size", w.DWORD), ("bounds", w.RECT), ("work", w.RECT),
                ("flags", w.DWORD), ("device", w.WCHAR * 32)]


def api(name, result, *arguments):
    function = getattr(USER, name)
    function.restype, function.argtypes = result, arguments
    return function


enum_displays = api("EnumDisplayMonitors", w.BOOL, w.HDC, ct.POINTER(w.RECT), MONITOR_VISITOR, w.LPARAM)
monitor_info = api("GetMonitorInfoW", w.BOOL, w.HMONITOR, ct.POINTER(MonitorInfo))
set_dpi_context = api("SetThreadDpiAwarenessContext", w.HANDLE, w.HANDLE)
create_window = api("CreateWindowExW", w.HWND, w.DWORD, w.LPCWSTR, w.LPCWSTR, w.DWORD, ct.c_int,
                    ct.c_int, ct.c_int, ct.c_int, w.HWND, w.HMENU, w.HINSTANCE, w.LPVOID)
destroy_window = api("DestroyWindow", w.BOOL, w.HWND)
window_dpi = api("GetDpiForWindow", w.UINT, w.HWND)
window_monitor = api("MonitorFromWindow", w.HMONITOR, w.HWND, w.DWORD)
enum_windows = api("EnumWindows", w.BOOL, WINDOW_VISITOR, w.LPARAM)
visible = api("IsWindowVisible", w.BOOL, w.HWND)
minimized = api("IsIconic", w.BOOL, w.HWND)
owner = api("GetWindowThreadProcessId", w.DWORD, w.HWND, ct.POINTER(w.DWORD))
title = api("GetWindowTextW", ct.c_int, w.HWND, w.LPWSTR, ct.c_int)
get_rect = api("GetWindowRect", w.BOOL, w.HWND, ct.POINTER(w.RECT))


def bounds(rect):
    return [rect.left, rect.top, rect.right, rect.bottom]


def displays():
    handles = []

    @MONITOR_VISITOR
    def collect(monitor, _dc, _rect, _data):
        handles.append(monitor)
        return True

    if not enum_displays(None, None, collect, 0):
        raise ct.WinError(ct.get_last_error())
    result = {}
    for handle in handles:
        info = MonitorInfo()
        info.size = ct.sizeof(info)
        if not monitor_info(handle, ct.byref(info)):
            raise ct.WinError(ct.get_last_error())
        x = (info.bounds.left + info.bounds.right) // 2
        y = (info.bounds.top + info.bounds.bottom) // 2
        # A hidden per-monitor-aware HWND gives effective DPI without changing display settings.
        window = create_window(0, "STATIC", None, 0x80000000, x, y, 1, 1, None, None, None, None)
        if not window:
            raise ct.WinError(ct.get_last_error())
        try:
            dpi = window_dpi(window)
            pilot.require(dpi and window_monitor(window, 0) == handle, "No se pudo comprobar el DPI efectivo de la pantalla.")
        finally:
            destroy_window(window)
        result[handle] = dict(device=info.device, primary=bool(info.flags & 1), dpi=dpi, scalePercent=dpi / 96 * 100,
                              bounds=bounds(info.bounds), work=bounds(info.work))
    return result


def pilot_windows(pid, monitors):
    result = []

    @WINDOW_VISITOR
    def collect(window, _data):
        process = w.DWORD()
        owner(window, ct.byref(process))
        if process.value == pid and visible(window):
            name = ct.create_unicode_buffer(256)
            title(window, name, len(name))
            rect = w.RECT()
            if name.value in SURFACES and get_rect(window, ct.byref(rect)):
                monitor = monitors.get(window_monitor(window, 0), {})
                result.append(dict(name=name.value, hwnd=window, bounds=bounds(rect), dpi=window_dpi(window),
                                   monitor=monitor.get("device"), minimized=bool(minimized(window))))
        return True

    if not enum_windows(collect, 0):
        raise ct.WinError(ct.get_last_error())
    return sorted(result, key=lambda row: row["name"])


def library(root):
    connection = sqlite3.connect((root / "floating.db").as_uri() + "?mode=ro", uri=True)
    try:
        connection.row_factory = sqlite3.Row
        connection.execute("BEGIN")
        integrity = connection.execute("PRAGMA quick_check").fetchone()[0]
        profile = connection.execute("SELECT active_id FROM profile_state WHERE id=1").fetchone()[0]
        groups = [dict(row) for row in connection.execute(
            "SELECT id,name,monitor_key,x,y,width,height FROM containers ORDER BY id")]
        references = [dict(row) for row in connection.execute(
            "SELECT container_id,item_id,display_name,target,kind FROM container_items "
            "JOIN items ON item_id=items.id ORDER BY container_id,item_id")]
    finally:
        connection.close()
    resources = (root / "recursos").resolve()
    for row in references:
        target = Path(row.pop("target")).resolve()
        pilot.require(target.is_relative_to(resources), "Se encontró una referencia fuera de la biblioteca aislada.")
        row.update(target=target.relative_to(resources).as_posix(), exists=target.exists(),
                   sha256=pilot.digest(target) if target.is_file() else None)
    return dict(sqlite=integrity, activeProfile=profile, groups=groups, references=references)


def checkpoint(identity, stage, note, report, monitors):
    folder = pilot.REPO / "artifacts/test-center/sessions" / identity
    root = pilot.session_path(identity)
    session = pilot.read_json(folder / "session.json")
    receipt = pilot.read_json(pilot.receipt_path(pilot.STORAGE, identity))
    ready = pilot.read_json(root / "evidence/ready.json")
    pid = session["hostPid"]
    pilot.require(session["testId"] == "windows-mixed-dpi" and session["id"] == receipt["id"] == ready["session"] == identity
                  and session["buildId"] == receipt["build"] == ready["build"] and session["gitSha"] == receipt["commit"]
                  and receipt["process"][0] == pid == ready["hostPid"]
                  and pilot.same_path(session["database"], root / "floating.db"),
                  "El recibo no corresponde al ensayo de DPI mixto.")
    live, owned = pilot.active_sessions(pilot.STORAGE, pilot.processes(pilot.powershell()))
    key = tuple(receipt["process"])
    pilot.require(key in live and owned[key]["id"] == identity, "El host original de la sesión no está activo.")
    report.update(session=identity, buildId=receipt["build"], gitSha=receipt["commit"], hostPid=pid, stage=stage,
                  note=note, windows=pilot_windows(pid, monitors), library=library(root))
    evidence = folder / f"mixed-dpi-{stage}.json"
    with evidence.open("x", encoding="utf-8") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="Save the monitor preflight.")
    parser.add_argument("--session")
    parser.add_argument("--stage", choices=("baseline", "moved", "disconnected", "reconnected", "recovered"))
    parser.add_argument("--note")
    args = parser.parse_args()
    if bool(args.session) != bool(args.stage) or (args.session and args.output):
        parser.error("Usa --session con --stage o ejecuta el preflight con --output.")
    report = dict(observedUtc=datetime.now(UTC).isoformat())
    try:
        previous = set_dpi_context(w.HANDLE(-4))  # DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        if not previous:
            raise ct.WinError(ct.get_last_error())
        try:
            monitors = displays()
            varied = len({monitor["dpi"] for monitor in monitors.values()}) >= 2
            report.update(monitors=list(monitors.values()), readyForMixedDpi=varied)
            if args.session:
                checkpoint(args.session, args.stage, args.note, report, monitors)
            else:
                scales = ", ".join(f"{monitor['device']} a {monitor['scalePercent']:g} %" for monitor in monitors.values())
                report["reason"] = f"Pantallas detectadas: {scales or 'ninguna'}. " + (
                    "Se detectaron escalas diferentes." if varied else "Se requieren dos pantallas con escalas diferentes.")
        finally:
            set_dpi_context(previous)
        code = 0 if args.session or varied else 2
    except (OSError, KeyError, ValueError, RuntimeError, sqlite3.Error) as error:
        report.update(reason=str(error), readyForMixedDpi=False)
        code = 2
    if args.output:
        pilot.write_json(args.output, report)
    print(json.dumps(report, ensure_ascii=False))
    return code


if __name__ == "__main__":
    sys.exit(main())
