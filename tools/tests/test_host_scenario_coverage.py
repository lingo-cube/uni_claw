"""Host + Simulation buyer scenario truth-chain regressions."""
import contextlib
import importlib.machinery
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def module(name, path):
    loader = importlib.machinery.SourceFileLoader(name, str(path))
    spec = importlib.util.spec_from_loader(name, loader)
    result = importlib.util.module_from_spec(spec)
    loader.exec_module(result)
    return result


coverage = module("host_coverage", ROOT / "tools/scenario-coverage.py")
verify = module("host_verify", ROOT / "tools/verify-change")


class HostScenarioCoverageTests(unittest.TestCase):
    def test_binary_trait_discovery_merges_both_roots_with_exact_fqns(self):
        sim = "UniClaw.Simulation.Tests.Cases.Scenario(text: \"with spaces\")"
        host = "UniClaw.Host.Tests.Cases.Scenario(text: \"with spaces\")"
        def run(args, **kwargs):
            root = args[2]
            name = sim if root == str(coverage.SIM_TESTS) else host
            return subprocess.CompletedProcess(args, 0, "Tests available:\n    " + name + "\n", "")
        with patch.object(coverage.subprocess, "run", side_effect=run):
            mapped = coverage.scenario_test_map(["SCN-X"], no_build=True)
        self.assertEqual(mapped, {"SCN-X": [sim, host]})

    def test_duplicate_binary_trait_name_across_roots_is_rejected(self):
        def run(args, **kwargs):
            return subprocess.CompletedProcess(args, 0, "Tests available:\n    UniClaw.Shared.Case\n", "")
        with patch.object(coverage.subprocess, "run", side_effect=run), contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                coverage.scenario_test_map(["SCN-X"], no_build=True)

    def test_missing_host_outcome_does_not_pass_scene(self):
        sim = "UniClaw.Simulation.Tests.Case"
        host = "UniClaw.Host.Tests.Case"
        data = json.loads((ROOT / "scenarios/SCN-WIFI-001.json").read_text())
        data["id"] = "SCN-X"
        with tempfile.TemporaryDirectory() as tmp:
            scenarios = Path(tmp)
            (scenarios / "SCN-X.json").write_text(json.dumps(data))
            with patch.object(coverage, "SCENARIOS", scenarios), patch.object(coverage, "runtime_source_hash", return_value="fixture"), patch.object(coverage, "verify_entry", return_value=[]), patch.object(coverage, "load_schema_violations", return_value=(None, [])):
                _, violations = coverage.load({sim: "Passed"}, {"SCN-X": [sim, host]})
        self.assertTrue(any(host in violation and "无结果" in violation for violation in violations))

    def test_cross_root_duplicate_fqn_is_rejected_even_if_outcome_matches(self):
        with tempfile.TemporaryDirectory() as tmp:
            paths = [Path(tmp) / "sim.trx", Path(tmp) / "host.trx"]
            for path in paths:
                path.write_text('<TestRun><UnitTestResult testName="same" outcome="Passed"/></TestRun>')
            with self.assertRaisesRegex(ValueError, "映射歧义"):
                coverage.merge_trx(paths)

    def test_cli_reports_both_trx_files_and_exits_success_after_valid_join(self):
        entry = json.loads((ROOT / "scenarios/SCN-WIFI-001.json").read_text())
        entry["_derived"] = "passing"
        paths = [Path("sim.trx"), Path("host.trx")]
        output = io.StringIO()
        with patch.object(sys, "argv", ["coverage", "--trx", "sim.trx", "--trx", "host.trx"]), \
             patch.object(coverage, "discover_trx", return_value=paths), \
             patch.object(coverage, "merge_trx", return_value={}), \
             patch.object(coverage, "scenario_test_map", return_value={}), \
             patch.object(coverage, "load", return_value=([entry], [])), \
             patch.object(coverage, "freshness_violation", return_value=None), \
             contextlib.redirect_stdout(output):
            self.assertEqual(coverage.main(), 0)
        self.assertIn("truth chain verified against sim.trx, host.trx", output.getvalue())

    def test_explicit_repeated_trx_and_default_require_both_roots(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            roots = [base / "sim", base / "host"]
            for root in roots:
                (root / "TestResults").mkdir(parents=True)
                (root / "TestResults/one.trx").write_text("<TestRun/>")
            with patch.object(coverage, "TEST_ROOTS", tuple(roots)):
                self.assertEqual(coverage.discover_trx(None), [root / "TestResults/one.trx" for root in roots])
                self.assertEqual(coverage.discover_trx([str(roots[0] / "TestResults/one.trx"), str(roots[1] / "TestResults/one.trx")]), [root / "TestResults/one.trx" for root in roots])
                (roots[1] / "TestResults/one.trx").unlink()
                with self.assertRaises(SystemExit):
                    coverage.discover_trx(None)

    def test_host_source_and_test_files_participate_in_freshness(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            roots = [base / "sim", base / "host"]
            host_src = base / "src" / "UniClaw.Host"
            kernel = base / "src" / "UniClaw.Kernel"
            agent = base / "src" / "UniClaw.Agent"
            scenarios = base / "scenarios"
            for directory in [*roots, host_src, kernel, agent, scenarios]:
                directory.mkdir(parents=True)
            host_file = host_src / "Host.cs"
            trx = base / "host.trx"
            host_file.write_text("new")
            trx.write_text("trx")
            import os
            os.utime(trx, (1, 1))
            with patch.object(coverage, "TEST_ROOTS", tuple(roots)), patch.object(coverage, "ROOT", base), patch.object(coverage, "SCENARIOS", scenarios):
                with patch.object(coverage, "ROOT", base):
                    # freshness_violation resolves canonical product roots from ROOT.
                    (base / "src/UniClaw.Host").mkdir(exist_ok=True)
                    self.assertIsNotNone(coverage.freshness_violation(trx))
                    os.utime(host_file, (1, 1))
                    os.utime(trx, (2, 2))
                    generated = host_src / "obj/Debug/Generated.cs"
                    generated.parent.mkdir(parents=True)
                    generated.write_text("generated after Simulation TRX")
                    self.assertIsNone(coverage.freshness_violation(trx))

    def test_verify_full_wires_host_trx_from_single_host_execution(self):
        calls = []
        def run(label, args):
            calls.append((label, args))
            return 0, ""
        with patch.object(sys, "argv", ["verify-change", "SIM-016", "--scope", "full", "--no-snapshot"]), patch.object(verify, "run", side_effect=run), contextlib.redirect_stdout(io.StringIO()):
            verify.main()
        host_runs = [(label, args) for label, args in calls if label == "FULL_TEST_PROJECTS[UniClaw.Host.Tests]"]
        self.assertEqual(len(host_runs), 1)
        self.assertIn("trx;LogFileName=sim006-full-host.trx", host_runs[0][1])
        coverage_call = next(args for label, args in calls if label == "COVERAGE")
        self.assertEqual(coverage_call.count("--trx"), 2)
        self.assertIn("sim006-full-host.trx", " ".join(coverage_call))


if __name__ == "__main__":
    unittest.main(verbosity=2)
