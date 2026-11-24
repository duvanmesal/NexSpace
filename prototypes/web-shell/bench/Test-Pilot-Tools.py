"""Exercise preflight and evidence matching with synthetic inputs, without creating an OLE event."""

import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import uuid


spec = importlib.util.spec_from_file_location("drop_test_driver", Path(__file__).with_name("Explorer-Drop.py"))
drop = importlib.util.module_from_spec(spec)
spec.loader.exec_module(drop)
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

    def test_ole_requires_the_exact_paths_and_session(self):
        session, pid, window = uuid.uuid4().hex, 42, "group_expected"
        paths = [Path(tempfile.gettempdir()) / f"synthetic-{index}.txt" for index in range(4)]
        expected = {drop.normalized(path): 0 for path in paths}

        def event(stage, **details):
            return dict(session=session, hostPid=pid, stage=stage,
                        detail=dict(window=window, batch=1, **details))

        entries = [event("enter", accepted=True), event("drop", accepted=True, count=4)]
        additions = [event("nexcore", source="ole-drop", target=str(path), ok=True, validated=True)
                     for path in paths]
        committed = event("persisted", result={"Ok": None})

        def proven(events):
            return drop.ole_evidence(events, expected, session, pid, window)

        self.assertEqual(proven(entries + additions + [committed]), (True, True))
        substituted = [*additions[:-1], event("nexcore", source="picker", target=str(paths[-1]),
                                            ok=True, validated=True)]
        self.assertEqual(proven(entries + substituted + [committed]), (True, False))
        self.assertEqual(proven(entries + additions[:-1] + [additions[0], committed]), (True, False))
        self.assertEqual(proven(entries + additions), (True, False))
        self.assertEqual(proven([{**e, "session": "different"} for e in entries + additions + [committed]]),
                         (False, False))


if __name__ == "__main__":
    unittest.main()
