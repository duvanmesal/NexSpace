"""Exercise preflight and evidence matching with synthetic inputs, without creating an OLE event."""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import uuid


spec = importlib.util.spec_from_file_location("drop_test_driver", Path(__file__).with_name("Explorer-Drop.py"))
drop = importlib.util.module_from_spec(spec)
spec.loader.exec_module(drop)
tray_spec = importlib.util.spec_from_file_location("tray_test_driver", Path(__file__).with_name("Tray-Recovery.py"))
tray = importlib.util.module_from_spec(tray_spec)
tray_spec.loader.exec_module(tray)
import pilot_support as pilot


class ToolsTests(unittest.TestCase):
    def test_prefers_7_and_falls_back_to_windows_powershell(self):
        with patch.object(pilot.shutil, "which", side_effect=lambda name: name), self.subTest("PowerShell 7"):
            self.assertEqual(pilot.powershell(), "pwsh.exe")
        with patch.object(pilot.shutil, "which", side_effect=lambda name: name if name == "powershell.exe" else None):
            self.assertEqual(pilot.powershell(), "powershell.exe")
        with tempfile.TemporaryDirectory() as empty, patch.dict(pilot.os.environ, WINDIR=empty):
            with patch.object(pilot.shutil, "which", return_value=None):
                with self.assertRaisesRegex(RuntimeError, "Se requiere PowerShell"):
                    pilot.powershell()

    def test_failed_build_cannot_fall_back_to_old_output(self):
        with tempfile.TemporaryDirectory() as directory:
            storage = Path(directory)
            pilot.write_json(storage / "build-status.json", {"status": "failed"})
            with self.assertRaisesRegex(RuntimeError, "no es válida"):
                pilot.build_latest(storage)

    def test_stale_source_is_rejected(self):
        with patch.object(pilot, "source_identity", return_value={"commit": "a", "sha256": "fresh"}):
            with self.assertRaisesRegex(RuntimeError, "otros archivos"):
                pilot.verify_source({"commit": "a", "sourceSha256": "old"})

    def test_foreign_session_id_cannot_escape_temp_root(self):
        with self.assertRaisesRegex(RuntimeError, "inválido"):
            pilot.session_path("../outside")

    def test_drop_folder_is_session_specific_and_keeps_legacy_receipts_readable(self):
        identity = "040b56a0" + "0" * 24
        root = pilot.session_path(identity)
        legacy = {"session": identity}
        self.assertEqual(pilot.drop_folder(root, legacy), root / "Arrastrar a Archivos")
        source = pilot.drop_folder_name(identity)
        self.assertEqual(pilot.drop_folder(root, {**legacy, "sourceFolder": source}), root / source)
        with self.assertRaisesRegex(RuntimeError, "no corresponde"):
            pilot.drop_folder(root, {**legacy, "sourceFolder": pilot.drop_folder_name("abcd1234")})


class DropEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.session = uuid.uuid4().hex
        self.pid = 42
        self.window = "group_expected"
        self.paths = [Path(tempfile.gettempdir()) / "fixture" / f"item-{index}.txt" for index in range(4)]
        self.expected = {drop.normalized(path) for path in self.paths}

    def batch(self, paths, batch=1, started=100):
        def event(stage, ms, **details):
            return dict(session=self.session, hostPid=self.pid, stage=stage, ms=ms,
                        detail=dict(window=self.window, batch=batch, **details))

        return [
            event("enter", started - 1, accepted=True),
            event("drop", started, accepted=True, count=len(paths)),
            *(event("nexcore", started + 1, source="ole-drop", target=str(path), ok=True, validated=True)
              for path in paths),
            event("persisted", started + 2, result={"Ok": None}),
        ]

    def evidence(self, events):
        return drop.ole_evidence(events, self.session, self.pid, self.window)

    def stored(self, paths, milliseconds=101):
        utc = f"1970-01-01T00:00:00.{milliseconds:03d}+00:00"
        return {drop.normalized(path): (0, utc, str(path)) for path in paths}

    def test_requires_native_enter_drop_and_completed_batch_on_correct_host(self):
        events = self.batch(self.paths)
        seen, batches = self.evidence(events)
        self.assertTrue(seen)
        self.assertEqual(drop.persisted_ole_paths(batches, self.stored(self.paths)), self.expected)
        for rejected in (events[1:], events[:-1], events[:-2] + [events[-1], events[-2]]):
            self.assertEqual(self.evidence(rejected), (True, []))
        for wrong in ({"session": "different"}, {"hostPid": 43}):
            self.assertEqual(self.evidence([{**event, **wrong} for event in events]), (False, []))
        self.assertEqual(self.evidence([{**event, "detail": {**event["detail"], "window": "group_other"}}
                                        for event in events]), (False, []))

    def test_picker_ipc_duplicate_and_rejected_ack_cannot_prove_a_batch(self):
        for change in ({"source": "picker"}, {"source": "test-ipc"},
                       {"target": str(self.paths[1])}, {"ok": False}, {"validated": False}):
            with self.subTest(change=change):
                events = self.batch(self.paths)
                events[2]["detail"].update(change)
                self.assertEqual(self.evidence(events), (True, []))

    def test_foreign_batch_is_diagnosed_and_does_not_poison_an_exact_retry(self):
        foreign = [path.parent.parent / "different-fixture" / path.name for path in self.paths]
        first = self.batch(foreign)
        _, batches = self.evidence(first)
        actual = self.stored(foreign)
        self.assertEqual(len(drop.persisted_ole_paths(batches, actual) - self.expected), 4)
        self.assertFalse(drop.persisted_ole_paths(batches, actual) & self.expected)

        _, batches = self.evidence(first + self.batch(self.paths, batch=2, started=200))
        actual.update(self.stored(self.paths, milliseconds=201))
        self.assertEqual(drop.persisted_ole_paths(batches, actual) & self.expected, self.expected)

    def test_ole_log_without_matching_sqlite_row_or_after_non_ole_reinsert_is_insufficient(self):
        _, batches = self.evidence(self.batch(self.paths))
        actual = self.stored(self.paths[:2])
        actual.update(self.stored(self.paths[2:3], milliseconds=110))
        self.assertEqual(drop.persisted_ole_paths(batches, actual),
                         {drop.normalized(path) for path in self.paths[:2]})

    def test_core_audit_is_scoped_to_session_profile_and_group(self):
        entry = dict(session=self.session, profile="expected", target="files", source="ole-drop", ok=True)
        picker = {**entry, "source": "picker"}
        records = [entry, picker, {**picker, "target": "games"}, {**picker, "profile": "other"},
                   {**picker, "session": "previous"}, {**entry, "source": "test-ipc"},
                   {**picker, "ok": False}]
        lines = ["información de diagnóstico", *(json.dumps(record) for record in records)]
        self.assertEqual(drop.core_additions(lines, self.session, "expected", "files"),
                         {"ole-drop": 1, "picker": 1, "test-ipc": 1})


class TraySequenceTests(unittest.TestCase):
    """Check the sequence logic in memory, without producing physical evidence."""

    def event(self, stage, **detail):
        return dict(session="current", hostPid=42, build="build", stage=stage, detail=detail)

    def setUp(self):
        self.ready = self.event("tray-ready", dockIsVisible=True)
        self.hidden = self.event("dock-hidden", dockWasVisible=True, hideSucceeded=True, dockIsVisible=False)
        self.clicked = self.event("tray-recover", attempt=1, dockWasVisible=False)
        self.visible = self.event("tray-recovered", attempt=1, showSucceeded=True, dockIsVisible=True)

    def verify(self, *records):
        return tray.check_sequence(records, "current", 42, "build")

    def test_complete_sequence_returns_the_verified_records(self):
        result = self.verify(self.ready, self.hidden, self.clicked, self.visible)
        self.assertEqual(result[0], "PASS")
        self.assertEqual(result[2], [self.hidden, self.clicked, self.visible])

    def test_requires_hide_click_and_completion_of_the_same_attempt(self):
        for records in ((self.clicked, self.visible), (self.hidden, self.visible), (self.hidden, self.clicked),
                        (self.hidden, self.visible, self.clicked),
                        (self.hidden, self.clicked, self.event("tray-recovered", attempt=2,
                                                               showSucceeded=True, dockIsVisible=True)),
                        (self.hidden, self.event("tray-recover", attempt=1, dockWasVisible=True), self.visible)):
            with self.subTest(records=records):
                self.assertEqual(self.verify(self.ready, *records)[0], "BLOCKED")

    def test_rejects_other_processes_sessions_builds_and_legacy_quit(self):
        for mismatched in ({"hostPid": 2968}, {"session": "older"}, {"build": "unrelated"}):
            with self.subTest(mismatched=mismatched):
                self.assertEqual(self.verify(self.ready, self.hidden, {**self.clicked, **mismatched},
                                             self.visible)[0], "BLOCKED")
        old_quit = {**self.event("tray-quit"), "hostPid": 2968, "session": "older"}
        self.assertEqual(self.verify(old_quit, self.ready)[0], "BLOCKED")

    def test_distinguishes_native_failure_from_missing_instrumentation(self):
        false_hide = self.event("dock-hidden", dockWasVisible=True, hideSucceeded=False, dockIsVisible=True)
        false_restore = self.event("tray-recovered", attempt=1, showSucceeded=True, dockIsVisible=False)
        self.assertEqual(self.verify(self.ready, false_hide)[0], "FAIL")
        self.assertEqual(self.verify(self.ready, self.hidden, self.clicked, false_restore)[0], "FAIL")
        self.assertEqual(self.verify(self.hidden, self.clicked, self.visible)[0], "ERROR")


if __name__ == "__main__":
    unittest.main()
