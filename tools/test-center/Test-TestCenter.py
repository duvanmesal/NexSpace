"""Exercise the orchestrator in scratch storage, without creating physical evidence."""

from importlib.util import module_from_spec, spec_from_file_location
import json
from datetime import UTC, datetime, timedelta
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


spec = spec_from_file_location("test_center", Path(__file__).resolve().parents[1] / "test-center.py")
app = module_from_spec(spec)
spec.loader.exec_module(app)
performance_spec = spec_from_file_location(
    "performance_hours", app.REPO / "prototypes/web-shell/bench/Performance-Hours.py")
performance = module_from_spec(performance_spec)
performance_spec.loader.exec_module(performance)


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

    def test_performance_hours_requires_opt_in(self):
        with patch.object(self.center, "command") as command:
            result = self.center.run("performance-hours")
            automatic = self.center.run("performance-hours", in_suite=True)
        self.assertEqual(result["status"], "BLOCKED")
        self.assertIn("-LongRun", result["reason"])
        self.assertIsNone(result["hostPid"])
        self.assertEqual(automatic["status"], "BLOCKED")
        command.assert_not_called()

    def test_failed_preflight_never_launches_pilot(self):
        def fail_preflight(command, values, folder, phase, deadline):
            self.assertEqual(phase, "prepare")
            app.pilot.write_json(values["{preflight}"], {"status": "BLOCKED", "reason": "Falló el preflight.",
                                                          "collectorError": "Add-Type: invalid source"})
            return 2, None, ""

        with (
            patch.object(app.pilot, "build_latest", return_value=(Path("build"), {
                "id": "build", "commit": "abc", "dirty": False})),
            patch.object(app.pilot, "processes", return_value=[]),
            patch.object(self.center, "command", side_effect=fail_preflight) as command,
        ):
            result = self.center.run("performance-hours", performance_mode="long")
        command.assert_called_once()
        self.assertIsNone(result["hostPid"])
        self.assertEqual(result["status"], "BLOCKED")
        self.assertIn("Add-Type", result["details"]["collectorError"])
        self.assertEqual(self.center.verify(result["id"])["reason"], result["reason"])

    def test_prepare_launches_host_without_starting_warmup(self):
        with (
            patch.object(app.pilot, "build_latest", return_value=(Path("build"), {
                "id": "build", "commit": "abc", "dirty": False})),
            patch.object(app.pilot, "processes", return_value=[]),
            patch.object(self.center, "command", return_value=(0, None, "")) as command,
            patch.object(self.center, "managed", return_value={"hostPid": 42, "sidecarPid": 43}),
        ):
            result = self.center.run("performance-hours", performance_mode="prepare")
        self.assertTrue(result["performancePrepared"])
        self.assertEqual(result["hostPid"], 42)
        self.assertEqual(result["status"], "BLOCKED")
        self.assertEqual([call.args[3] for call in command.call_args_list], ["prepare", "run-1"])

    def test_resume_prepared_host_keeps_identity(self):
        identity = "4" * 32
        folder, session = self.center.session(identity, test="performance-hours")
        bound = dict(buildId="test", gitSha="test", hostPid=42, sidecarPid=43, database="isolated.db")
        app.pilot.write_json(folder / "session.json", {**session, **bound, "performancePrepared": True})
        with (
            patch.object(self.center, "managed", return_value=bound),
            patch.object(self.center, "command", return_value=(0, None, "")) as command,
        ):
            result = self.center.start_prepared_performance(identity)
        self.assertFalse(result["performancePrepared"])
        self.assertEqual(result["hostPid"], 42)
        self.assertEqual([call.args[3] for call in command.call_args_list], ["prepare", "start"])
        self.assertEqual(command.call_args.args[1]["{session}"], identity)
        self.assertEqual(command.call_args.args[1]["{performanceMode}"], "long")

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

    def test_tray_manual_start_has_no_explorer_fixture(self):
        with (
            patch.object(app.pilot, "build_latest", return_value=(Path("build"), {
                "id": "build", "commit": "abc", "dirty": False})),
            patch.object(app.pilot, "processes", return_value=[]),
            patch.object(self.center, "command", return_value=(0, None, "")) as command,
            patch.object(self.center, "managed", return_value={"trayTrace": "tray.jsonl", "hostPid": 42}),
            patch.object(app.os, "startfile") as explorer,
        ):
            result = self.center.run("windows-tray")
        self.assertEqual(result["status"], "BLOCKED")
        self.assertEqual(result["trayTrace"], "tray.jsonl")
        self.assertEqual(command.call_args.args[1]["{session}"], result["id"])
        self.assertIn("--tray", command.call_args.args[0])
        explorer.assert_not_called()

    def test_explorer_observation_uses_recorded_session_and_keeps_probe_attempt(self):
        identity = "3" * 32
        folder, session = self.center.session(identity, test="windows-explorer", source=False)
        session["trayTrace"] = "test trace path"
        app.pilot.write_json(folder / "session.json", session)
        recorded = {"status": "BLOCKED", "checkpoint": {"phase": "during", "note": "probe fixture"}}
        reply = folder / "reply.json"
        app.pilot.write_json(reply, recorded)
        with patch.object(self.center, "command", return_value=(2, reply, "")) as command:
            result = self.center.observe(identity, "during", "pass", "test report")
        self.assertTrue(result["checkpointRecorded"])
        self.assertEqual(app.pilot.read_json(folder / "explorer-during.json"), recorded["checkpoint"])
        values = command.call_args.args[1]
        self.assertEqual(values["{session}"], identity)
        self.assertEqual(values["{evidence}"], folder)
        self.assertEqual(app.pilot.read_json(values["{operator}"])["note"], "test report")

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


class PerformanceSeriesTests(unittest.TestCase):
    def test_legacy_compiler_preflight_and_diagnostics(self):
        shell = app.pilot.powershell("powershell.exe")
        with tempfile.TemporaryDirectory() as path, patch.object(performance.pilot, "powershell", return_value=shell):
            folder = Path(path)
            report = performance.preflight("fixture", folder / "valid.json")
            self.assertEqual(report["status"], "PASS", report)
            self.assertTrue(report["version"].startswith("5.1."))
            source = folder / performance.SNAPSHOT.name
            source.write_bytes(performance.SNAPSHOT.read_bytes())
            (folder / "Counters.cs").write_text(
                "public static class Counters { static void Read(out uint value) { value = 0; } "
                "static void Invoke() { Read(out uint owner); } }", encoding="ascii")
            with patch.object(performance, "SNAPSHOT", source):
                report = performance.preflight("fixture", folder / "blocked.json")
            self.assertEqual(report["status"], "BLOCKED")
            self.assertIn("Add-Type", report["collectorError"])
            self.assertIn("Counters.cs", report["collectorError"])
            self.assertEqual(app.pilot.read_json(folder / "blocked.json"), report)

    def setUp(self):
        self.plan = dict(profile="long", binding={"hostPid": 42, "sidecarPid": 43},
                         timingSeconds=performance.PROFILES["long"])

    def samples(self, value, profile="long"):
        warmup, duration, interval = performance.PROFILES[profile]
        start = datetime(2026, 11, 25, tzinfo=UTC)
        return [
            dict(binding=self.plan["binding"], utc=(start + timedelta(seconds=elapsed)).isoformat(),
                 elapsedSeconds=elapsed, hostAlive=True, processCount=2, windows=[11, 12, 13, 14],
                 processes=[
                     dict(pid=pid, parent=parent, started="1", private=value(elapsed), ws=500,
                          handles=45, gdi=10, user=3, cpu=elapsed * 0.02)
                     for pid, parent in ((42, 30), (43, 42))
                 ])
            for elapsed in range(0, warmup + duration + interval, interval)
        ]

    def test_sustained_growth_differs_from_single_jump(self):
        growth = performance.series(self.plan, self.samples(lambda elapsed: 100 + elapsed))
        self.assertEqual(growth["status"], "FAIL")
        self.assertEqual(growth["metrics"], ["private"])
        plateau = performance.series(self.plan, self.samples(lambda elapsed: 500 if elapsed > 3000 else 100))
        self.assertEqual(plateau["status"], "PASS")
        memory = plateau["summary"]["metrics"]["private"]
        self.assertEqual(memory["finalDelta"], 800)

    def test_restart_gap_and_early_stop(self):
        samples = self.samples(lambda _: 500)
        samples[-1]["processes"][-1]["started"] = "2"
        self.assertEqual(performance.series(self.plan, samples)["status"], "FAIL")
        samples[-1]["processes"][-1]["started"] = "1"
        del samples[30:32]
        self.assertEqual(performance.series(self.plan, samples)["status"], "BLOCKED")
        samples = self.samples(lambda _: 500)[:10]
        samples[-1].update(hostAlive=False, stopRequested=True)
        self.assertEqual(performance.series(self.plan, samples)["status"], "BLOCKED")

    def test_partial_evidence_and_probe_cannot_pass(self):
        self.assertEqual(performance.series(self.plan, self.samples(lambda _: 500)[:-1])["status"], "BLOCKED")
        probe = dict(self.plan, profile="probe", timingSeconds=performance.PROFILES["probe"])
        result = performance.series(probe, self.samples(lambda _: 500, "probe"))
        self.assertEqual(result["status"], "BLOCKED")
        self.assertIn("summary", result)

    def test_verify_requires_physical_observation_after_full_run(self):
        with tempfile.TemporaryDirectory() as path:
            folder = Path(path)
            samples = self.samples(lambda _: 500)
            end = dict(binding=self.plan["binding"], status="PASS", finishedUtc="2026-11-25T04:00:00+00:00")
            app.pilot.write_json(folder / "performance-summary.json", end)
            with patch.object(performance, "read_samples", return_value=samples):
                self.assertEqual(performance.verify(folder, self.plan)["status"], "BLOCKED")
                attempt = folder / "observe-1"
                attempt.mkdir()
                app.pilot.write_json(attempt / "result.json", {"checkpoint": {
                    "phase": "incident", "binding": self.plan["binding"], "outcome": "fail", "note": "fixture"}})
                self.assertEqual(performance.verify(folder, self.plan)["status"], "FAIL")


if __name__ == "__main__":
    unittest.main()
