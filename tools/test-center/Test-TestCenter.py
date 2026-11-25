"""Exercise the orchestrator in scratch storage, without creating physical evidence."""

from importlib.util import module_from_spec, spec_from_file_location
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


spec = spec_from_file_location("test_center", Path(__file__).resolve().parents[1] / "test-center.py")
app = module_from_spec(spec)
spec.loader.exec_module(app)


class TestCenterTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.center = app.Center(root / "center", root / "pilot")
        source = patch.object(app.pilot, "source_identity", return_value={"commit": "test", "dirty": False})
        source.start()
        self.addCleanup(source.stop)

    def test_catalog_and_suite_references(self):
        required = {"name", "description", "type", "category", "component", "platform", "requirements",
                    "preparation", "execution", "verification", "acceptanceCriteria", "timeoutSeconds",
                    "humanRequired", "evidence", "unavailableReason"}
        for entry in self.center.tests.values():
            self.assertTrue(required <= entry.keys())
            self.assertGreater(entry["timeoutSeconds"], 0)
        for suite in self.center.suites.values():
            self.assertTrue(suite["tests"])
            self.assertTrue(set(suite["tests"]) <= self.center.tests.keys())

    def commands(self, code):
        spec = self.center.tests["pilot-tools"]
        spec["execution"] = [["python", "-c", code]]
        return self.center.run("pilot-tools")

    def test_records_a_real_child_failure_as_fail(self):
        result = self.commands("raise SystemExit('criterion failed')")
        self.assertEqual(result["status"], "FAIL")
        self.assertEqual(result["failedCriteria"], ["criterion failed"])
        folder = self.center.folder(result["id"])
        self.assertEqual(app.pilot.read_json(folder / "result.json")["status"], "FAIL")
        self.assertTrue((folder / "run-1.stderr.log").is_file())

    def test_records_missing_executable_as_error(self):
        self.center.tests["pilot-tools"]["execution"] = [["missing-test-center-program.exe"]]
        result = self.center.run("pilot-tools")
        self.assertEqual(result["status"], "ERROR")
        self.assertEqual(result["failedCriteria"], [])

    def test_records_unavailable_git_as_error(self):
        with patch.object(app.pilot, "source_identity", side_effect=OSError("Git inaccesible")):
            result = self.center.run("pilot-tools")
        self.assertEqual(result["status"], "ERROR")
        self.assertEqual(self.center.reports(identity=result["id"])[0]["reason"], "Git inaccesible")

    def test_blocks_human_case_and_continues_suite(self):
        self.center.suites["trial"] = {"tests": ["explorer-drop", "pilot-tools"]}
        self.center.tests["pilot-tools"]["execution"] = [["python", "-c", "print('ok')"]]
        result = self.center.suite("trial")
        self.assertEqual(result["status"], "BLOCKED")
        self.assertEqual(result["blockers"], ["explorer-drop"])
        self.assertEqual(result["approvedCriteria"], ["pilot-tools"])
        self.assertEqual(self.center.reports(suite="trial")[0]["status"], "BLOCKED")
        history = self.center.folder(result["id"]) / "history"
        self.assertEqual(len(list(history.glob("*.json"))), 1)

    def test_does_not_launch_a_second_pilot(self):
        with (
            patch.object(app.pilot, "build_latest", return_value=(Path("build"), {
                "id": "build", "commit": "abc", "dirty": False})),
            patch.object(app.pilot, "processes", return_value=[]),
            patch.object(app.pilot, "active_sessions", return_value=({(4200, "stamp"): {}}, {})),
            patch.object(self.center, "command") as command,
        ):
            result = self.center.run("explorer-drop")
        self.assertEqual(result["status"], "BLOCKED")
        self.assertIn("piloto abierto", result["reason"])
        command.assert_not_called()

    def test_manual_start_keeps_one_id_and_opens_the_confirmed_source(self):
        with (
            patch.object(app.pilot, "build_latest", return_value=(Path("build"), {
                "id": "build", "commit": "abc", "dirty": False})),
            patch.object(app.pilot, "processes", return_value=[]),
            patch.object(self.center, "command", return_value=(0, None, "")) as command,
            patch.object(self.center, "managed", return_value={"sourceFolder": "confirmed", "hostPid": 42}) as managed,
            patch.object(app.os, "startfile") as explorer,
        ):
            result = self.center.run("explorer-drop")
        self.assertEqual(result["status"], "BLOCKED")
        self.assertEqual(result["hostPid"], 42)
        self.assertEqual(command.call_args.args[1]["{session}"], result["id"])
        managed.assert_called_once_with(result["id"])
        explorer.assert_called_once_with("confirmed")

    def test_historical_report_and_filters(self):
        session = "bcbf8d92f0094c6cb2dbc4c4be989051"
        report, = self.center.reports(identity=session)
        self.assertEqual(report["status"], "PASS")
        self.assertEqual(report["details"]["oleReferences"], 4)
        self.assertEqual(self.center.reports(start="2026-11-25", end="2026-11-25",
                                             test="explorer-drop", build=report["buildId"]), [report])
        self.assertEqual(self.center.reports(end="2026-11-24"), [])
        self.assertIn(session, app.markdown([report]))
        self.assertNotIn("AppData", json.dumps(report))

    def test_expired_evidence_preserves_public_acceptance(self):
        session = "bcbf8d92f0094c6cb2dbc4c4be989051"
        with patch.object(app.pilot, "session_path", return_value=Path(self.temp.name) / "expired"):
            result = self.center.verify(session)
        self.assertEqual(result["status"], "BLOCKED")
        self.assertEqual(result["historicalStatus"], "PASS")
        self.assertEqual(self.center.reports(identity=session)[0]["status"], "PASS")

    def test_sessions_cannot_escape_artifact_storage_or_swap_library(self):
        with self.assertRaisesRegex(RuntimeError, "inválido"):
            self.center.folder("../outside")
        identity = "1" * 32
        root = Path(self.temp.name) / "pilot-session"
        evidence = root / "evidence"
        evidence.mkdir(parents=True)
        (root / "floating.db").touch()
        app.pilot.write_json(root / "drop-trial.json", {})
        app.pilot.write_json(root / "before.json", {})
        receipt = app.pilot.receipt_path(self.center.pilot_storage, identity)
        receipt.parent.mkdir(parents=True)
        app.pilot.write_json(receipt, {"id": identity, "build": "same", "process": [42, "1"],
                                      "executable": str(root / "host.exe"), "evidence": str(evidence),
                                      "database": str(root / "different" / "floating.db")})
        app.pilot.write_json(evidence / "ready.json",
                             {"session": identity, "build": "same", "hostPid": 42,
                              "executable": str(root / "host.exe"), "database": str(root / "floating.db")})
        with patch.object(app.pilot, "session_path", return_value=root):
            self.assertEqual(self.center.verify(identity)["status"], "ERROR")


if __name__ == "__main__":
    unittest.main()
