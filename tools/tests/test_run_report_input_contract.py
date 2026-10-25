"""Behavioral CLI contract for the report generator's owned input catalog."""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools" / "gen-run-report.py"


class RunReportInputContractTests(unittest.TestCase):
    def test_describe_inputs_returns_deterministic_catalog_without_run_dir(self):
        command = [sys.executable, str(SCRIPT), "--describe-inputs"]
        first = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
        second = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)

        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(first.stdout, second.stdout)
        catalog = json.loads(first.stdout)
        self.assertEqual(catalog["schemaVersion"], "uniclaw.run-report.inputs.v1")
        self.assertEqual(catalog["generatorName"], "gen-run-report")
        self.assertEqual(catalog["generatorVersion"], 1)
        self.assertEqual(
            catalog["files"],
            [
                {"name": "metadata", "fileName": "metadata.json", "strictRequired": True, "requiredForDelivery": False},
                {"name": "facts", "fileName": "facts.json", "strictRequired": True, "requiredForDelivery": True},
                {"name": "trace", "fileName": "trace.json", "strictRequired": True, "requiredForDelivery": False},
                {"name": "settingsTrace", "fileName": "settings-trace.json", "strictRequired": False, "requiredForDelivery": False},
                {"name": "journal", "fileName": "exec.journal", "strictRequired": True, "requiredForDelivery": False},
                {"name": "consultations", "fileName": "consultations.json", "strictRequired": False, "requiredForDelivery": False},
                {"name": "environmentPreflight", "fileName": "environment-preflight.json", "strictRequired": False, "requiredForDelivery": False},
                {"name": "failure", "fileName": "failure.json", "strictRequired": False, "requiredForDelivery": False},
                {"name": "runtimeRunEvents", "fileName": "runtime-run-events.json", "strictRequired": False, "requiredForDelivery": False},
                {"name": "consultationLog", "fileName": "consultations.ndjson", "strictRequired": False, "requiredForDelivery": False},
            ],
        )
        self.assertEqual(catalog["outputFiles"], ["report.json", "report.md"])

    def test_source_label_only_changes_run_directory_display_identity(self):
        with tempfile.TemporaryDirectory() as temporary:
            run_dir = Path(temporary) / "copied-inputs"
            out_dir = Path(temporary) / "out"
            run_dir.mkdir()
            (run_dir / "facts.json").write_text("{}", encoding="utf-8")
            completed = subprocess.run(
                [sys.executable, str(SCRIPT), "--run-dir", str(run_dir), "--out-dir", str(out_dir),
                 "--source-label", "original-artifact-set"],
                cwd=ROOT, capture_output=True, text=True,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            report = json.loads((out_dir / "report.json").read_text(encoding="utf-8"))
            self.assertEqual(report["identities"]["runDir"]["value"], "original-artifact-set")
            self.assertEqual(report["input"]["artifacts"][0]["availability"], "not-collected")


if __name__ == "__main__":
    unittest.main()
