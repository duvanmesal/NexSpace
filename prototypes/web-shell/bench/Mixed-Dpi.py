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
RECONNECT_TEST = "windows-monitors-100"
RECONNECT_PHASES = ("baseline", "moved", "disconnected", "reconnected")
CODES = {"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3}


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
        profiles = [dict(row) for row in connection.execute("SELECT id,name FROM profiles ORDER BY id")]
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
    return dict(sqlite=integrity, activeProfile=profile, profiles=profiles, groups=groups, references=references)


def checkpoint(identity, stage, note, report, monitors, *, test="windows-mixed-dpi", allow_inactive=False):
    folder = pilot.REPO / "artifacts/test-center/sessions" / identity
    root = pilot.session_path(identity)
    session = pilot.read_json(folder / "session.json")
    receipt = pilot.read_json(pilot.receipt_path(pilot.STORAGE, identity))
    ready = pilot.read_json(root / "evidence/ready.json")
    pid = session["hostPid"]
    pilot.require(session["testId"] == test and session["id"] == receipt["id"] == ready["session"] == identity
                  and session["buildId"] == receipt["build"] == ready["build"] and session["gitSha"] == receipt["commit"]
                  and receipt["process"][0] == pid == ready["hostPid"]
                  and pilot.same_path(session["database"], root / "floating.db"),
                  "El recibo no corresponde al ensayo de monitores.")
    live, owned = pilot.active_sessions(pilot.STORAGE, pilot.processes(pilot.powershell()))
    key = tuple(receipt["process"])
    host_active = key in live and key in owned and owned[key]["id"] == identity
    pilot.require(host_active or allow_inactive, "El host original de la sesión no está activo.")
    report.update(session=identity, buildId=receipt["build"], gitSha=receipt["commit"], hostPid=pid, stage=stage,
                  note=note, hostActive=host_active, windows=pilot_windows(pid, monitors) if host_active else [],
                  library=library(root))
    if test == "windows-mixed-dpi":
        evidence = folder / f"mixed-dpi-{stage}.json"
        with evidence.open("x", encoding="utf-8") as output:
            json.dump(report, output, ensure_ascii=False, indent=2)
    return report


def topology(monitors):
    return {monitor["device"]: monitor for monitor in monitors}


def two_at_100(monitors):
    if len(monitors) != 2 or len(topology(monitors)) != 2:
        return False
    if sum(monitor["primary"] for monitor in monitors) != 1 or any(monitor["dpi"] != 96 for monitor in monitors):
        return False
    (ax, ay, ar, ab), (bx, by, br, bb) = (monitor["bounds"] for monitor in monitors)
    return ar <= bx or br <= ax or ab <= by or bb <= ay


def verdict(status, reason, **details):
    return dict(status=status, reason=reason, **details)


def safe_windows(point):
    windows = point["windows"]
    if len(windows) != len(SURFACES) or {window["name"] for window in windows} != SURFACES:
        return False
    monitors = topology(point["monitors"])
    for window in windows:
        monitor = monitors.get(window["monitor"])
        if not monitor or window["minimized"] or window["dpi"] != monitor["dpi"]:
            return False
        x, y, right, bottom = window["bounds"]
        left, top, work_right, work_bottom = monitor["work"]
        if not (left <= x < right <= work_right and top <= y < bottom <= work_bottom):
            return False
    return True


def archivos(point):
    return next(window for window in point["windows"] if window["name"] == "Archivos")


def saved_archivos(library_state):
    return next((group for group in library_state["groups"] if group["name"] == "Archivos"), None)


def library_identity(state):
    return (state["activeProfile"], state["profiles"], state["references"],
            [(group["id"], group["name"]) for group in state["groups"]])


def verify_reconnect(session, preflight, points, closure=None, final_library=None):
    if session["testId"] != RECONNECT_TEST or not two_at_100(preflight["monitors"]):
        return verdict("BLOCKED", "Falta un preflight válido de dos pantallas extendidas a 96 DPI.")
    for stage, point in points.items():
        if (point["session"], point["buildId"], point["gitSha"], point["hostPid"], point["stage"]) != (
                session["id"], session["buildId"], session["gitSha"], session["hostPid"], stage):
            return verdict("BLOCKED", f"El punto {stage} pertenece a otro ensayo.")
        if point["outcome"] == "fail" and point["note"].strip():
            return verdict("FAIL", f"El operador registró un fallo en {stage}.", observation=point["note"])
    missing = [stage for stage in RECONNECT_PHASES if stage not in points
               or points[stage]["outcome"] != "pass" or not points[stage]["note"].strip()]
    if missing:
        return verdict("BLOCKED", "Faltan comprobaciones físicas satisfactorias.", pending=missing)
    baseline, moved, disconnected, reconnected = (points[stage] for stage in RECONNECT_PHASES)
    instants = [datetime.fromisoformat(points[stage]["observedUtc"]) for stage in RECONNECT_PHASES]
    if any(first >= second for first, second in zip(instants, instants[1:])):
        return verdict("BLOCKED", "Los puntos de control no están en el orden del recorrido.")
    displays_before = topology(preflight["monitors"])
    primary = next(monitor for monitor in displays_before.values() if monitor["primary"])
    secondary = next(monitor for monitor in displays_before.values() if not monitor["primary"])
    for point in (baseline, moved, reconnected):
        if topology(point["monitors"]) != displays_before:
            return verdict("BLOCKED", f"La topología de {point['stage']} difiere del preflight.")
    if disconnected["monitors"] != [primary]:
        return verdict("BLOCKED", "Falta demostrar la desaparición de la secundaria y la continuidad de la principal.")

    base_library = baseline["library"]
    references = base_library["references"]
    if (len(base_library["groups"]) != 3 or len(references) != 9
            or sum(bool(reference["sha256"]) for reference in references) != 4
            or not all(reference["exists"] for reference in references)):
        return verdict("BLOCKED", "La línea base no contiene la biblioteca aislada de demostración completa.")
    for point in points.values():
        if point["library"]["sqlite"] != "ok" or library_identity(point["library"]) != library_identity(base_library):
            return verdict("FAIL", f"Cambió la integridad de SQLite, los perfiles o las referencias en {point['stage']}.")
        if not point["hostActive"] or not safe_windows(point):
            return verdict("FAIL", f"Hay ventanas ausentes, minimizadas o fuera del área útil en {point['stage']}.")
    if archivos(baseline)["monitor"] != primary["device"]:
        return verdict("BLOCKED", "Archivos debe partir de la pantalla principal.")
    if archivos(moved)["monitor"] != secondary["device"]:
        return verdict("FAIL", "Archivos no quedó en el monitor secundario después del traslado confirmado.")
    saved = saved_archivos(moved["library"])
    if not saved or saved["monitor_key"] != secondary["device"] or [saved["x"], saved["y"]] != archivos(moved)["bounds"][:2]:
        return verdict("FAIL", "SQLite no guardó el dispositivo y las coordenadas del traslado.")
    if saved_archivos(disconnected["library"]) != saved or saved_archivos(reconnected["library"]) != saved:
        return verdict("FAIL", "La desconexión o reconexión modificó la ubicación persistida de Archivos.")
    if archivos(reconnected)["monitor"] != secondary["device"] or archivos(reconnected)["bounds"] != archivos(moved)["bounds"]:
        return verdict("FAIL", "Archivos no recuperó su ubicación al reconectar la pantalla secundaria.")
    if not closure:
        return verdict("BLOCKED", "Falta el recibo de cierre ordinario. Ejecuta -Stop y vuelve a verificar.")
    if ((closure["session"], closure["buildId"], closure["hostPid"], closure["sidecarPid"]) !=
            (session["id"], session["buildId"], session["hostPid"], session["sidecarPid"])
            or datetime.fromisoformat(closure["closedUtc"]) < instants[-1]):
        return verdict("BLOCKED", "El cierre no corresponde al piloto y al recorrido registrados.")
    if closure["status"] != "closed" or closure["remaining"] or closure["observedProcesses"] < 2:
        return verdict("FAIL", "El cierre ordinario dejó procesos o no acreditó el árbol del piloto.")
    if (final_library["sqlite"] != "ok" or library_identity(final_library) != library_identity(base_library)
            or saved_archivos(final_library) != saved):
        return verdict("FAIL", "La biblioteca al cierre difiere de las referencias, perfiles o posición verificados.")
    return verdict("PASS", "Traslado, recuperación, reconexión y cierre acreditados en dos pantallas a 96 DPI.",
                   stages=list(RECONNECT_PHASES), closure=closure)


def verify_record(identity, folder):
    session = pilot.read_json(folder / "session.json")
    points = {}
    for stage in RECONNECT_PHASES:
        path = folder / f"monitors-100-{stage}.json"
        if path.is_file():
            points[stage] = pilot.read_json(path)
    path = folder / "monitors-100-closure.json"
    closure = pilot.read_json(path) if path.is_file() else None
    final = library(pilot.session_path(identity)) if closure and closure.get("status") == "closed" else None
    return verify_reconnect(session, pilot.read_json(folder / "monitors-100-preflight.json"), points, closure, final)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="Save the monitor preflight.")
    parser.add_argument("--session")
    parser.add_argument("--stage", choices=("baseline", "moved", "disconnected", "reconnected", "recovered"))
    parser.add_argument("--note")
    parser.add_argument("--monitors-100", action="store_true")
    parser.add_argument("--operator", type=Path)
    parser.add_argument("--verify", type=Path, metavar="EVIDENCE")
    args = parser.parse_args()
    if args.verify:
        if not args.monitors_100 or not args.session or args.stage or args.operator or args.output:
            parser.error("--verify necesita --monitors-100 y --session.")
        try:
            result = verify_record(args.session, args.verify)
        except (OSError, KeyError, ValueError, RuntimeError, sqlite3.Error) as error:
            result = verdict("BLOCKED", f"No se puede leer la evidencia del ensayo: {error}")
        print(json.dumps(result, ensure_ascii=False))
        return CODES[result["status"]]
    if bool(args.session) != bool(args.stage) or (args.session and args.output):
        parser.error("Usa --session con --stage o ejecuta el preflight con --output.")
    if args.operator and not (args.monitors_100 and args.stage in RECONNECT_PHASES):
        parser.error("--operator solo se usa en un punto del recorrido de monitores al 100 %.")
    if args.monitors_100 and args.session and not args.operator:
        parser.error("Registra el punto de control con Test Center -Observe.")
    report = dict(observedUtc=datetime.now(UTC).isoformat())
    try:
        previous = set_dpi_context(w.HANDLE(-4))  # DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        if not previous:
            raise ct.WinError(ct.get_last_error())
        try:
            monitors = displays()
            varied = len({monitor["dpi"] for monitor in monitors.values()}) >= 2
            report.update(monitors=list(monitors.values()), readyForMixedDpi=varied)
            matches = varied
            if args.monitors_100:
                matches = two_at_100(report["monitors"])
                report["readyForMonitors100"] = matches
            if args.operator:
                observation = pilot.read_json(args.operator)
                checkpoint(args.session, args.stage, observation["note"], report, monitors, test=RECONNECT_TEST,
                           allow_inactive=observation["outcome"] != "pass")
                report["outcome"] = observation["outcome"]
                report = verdict("BLOCKED", "Punto de control registrado. Ejecuta -Verify después del cierre.",
                                 checkpoint=report)
            elif args.session:
                checkpoint(args.session, args.stage, args.note, report, monitors)
            else:
                scales = ", ".join(f"{monitor['device']} a {monitor['scalePercent']:g} %" for monitor in monitors.values())
                requirement = (
                    "Dos pantallas extendidas a 96 DPI verificadas." if matches
                    else "Se requieren dos pantallas extendidas al 100 %."
                ) if args.monitors_100 else (
                    "Se detectaron escalas diferentes." if varied else "Se requieren dos pantallas con escalas diferentes.")
                report["reason"] = f"Pantallas detectadas: {scales or 'ninguna'}. {requirement}"
        finally:
            set_dpi_context(previous)
        code = 2 if args.operator else 0 if args.session or matches else 2
    except (OSError, KeyError, ValueError, RuntimeError, sqlite3.Error) as error:
        report.update(reason=str(error), readyForMixedDpi=False)
        if args.monitors_100:
            report.update(status="BLOCKED", readyForMonitors100=False)
        code = 2
    if args.output:
        pilot.write_json(args.output, report)
    print(json.dumps(report, ensure_ascii=False))
    return code


if __name__ == "__main__":
    sys.exit(main())
