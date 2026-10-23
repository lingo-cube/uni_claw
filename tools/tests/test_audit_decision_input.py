import importlib.util
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools" / "audit-decision-input.py"
SPEC = importlib.util.spec_from_file_location("audit_decision_input", SCRIPT)
AUDIT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(AUDIT)


def qualified(material_id="m1", source_id="s1"):
    digest = "a" * 64
    item = {
        "materialId": material_id,
        "sourceId": source_id,
        "sourceDigest": digest,
        "finalProviderInput": {
            "captureBoundary": "provider-request-envelope",
            "captureEvidence": {"ref": "capture.log#42", "sha256": "b" * 64},
            "messages": [{"role": "user", "content": "fixed input"}],
            "tools": [],
            "truncationRule": "truncate-before-provider-call:v2",
            "configurationVersion": "provider-config:7",
            "sha256": "c" * 64,
        },
    }

    final = item["finalProviderInput"]
    final["requestEnvelope"] = {"messages": final["messages"], "tools": final["tools"],
                                "model": "controlled-model", "temperature": 0}
    final["sha256"] = hashlib.sha256(json.dumps(final["requestEnvelope"], ensure_ascii=False,
        sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()
    return item


class DecisionInputAuditTests(unittest.TestCase):
    def test_complete_fixed_provider_envelope_is_eligible_material(self):
        result = AUDIT.audit({"materials": [qualified()]})
        row = result["materials"][0]
        self.assertEqual(row["comparisonEligibility"], "same-input-eligible")
        self.assertTrue(row["eligibleForSameInputStatistics"])
        self.assertRegex(row["sameInputKey"], r"^[0-9a-f]{64}$")

    def test_legacy_context_export_is_diagnostic_and_lists_missing_proof(self):
        report = AUDIT.audit([{
            "requestId": "req-1", "generation": 2, "prompt": "context and messages",
            "sha256": "d" * 64, "comparisonEligibility": "diagnostic-only-provider-envelope-unproven",
        }], "channel-final-prompts.json")
        row = report["materials"][0]
        self.assertEqual(row["comparisonEligibility"], "diagnostic-only")
        self.assertFalse(row["eligibleForSameInputStatistics"])
        for reason in ("final-provider-input-envelope-unproven", "tools-not-recorded",
                       "truncation-rule-missing", "configuration-version-missing"):
            self.assertIn(reason, row["reasons"])
        self.assertIsNone(row["sameInputKey"])

    def test_tools_truncation_and_configuration_are_independent_requirements(self):
        for field, expected in (("tools", "tools-not-recorded"),
                                ("truncationRule", "truncation-rule-missing"),
                                ("configurationVersion", "configuration-version-missing")):
            with self.subTest(field=field):
                item = qualified()
                del item["finalProviderInput"][field]
                row = AUDIT.audit({"materials": [item]})["materials"][0]
                self.assertIn(expected, row["reasons"])
                self.assertFalse(row["eligibleForSameInputStatistics"])

    def test_same_source_copy_cannot_upgrade_incomplete_material(self):
        incomplete = {"materialId": "old", "sourceId": "same-source", "sourceDigest": "a" * 64}
        copied_and_annotated = qualified("copy", "same-source")
        rows = AUDIT.audit({"materials": [incomplete, copied_and_annotated]})["materials"]
        self.assertTrue(all(row["comparisonEligibility"] == "diagnostic-only" for row in rows))
        self.assertIn("same-source-copy-cannot-upgrade", rows[1]["reasons"])

    def test_changed_payload_digest_or_configuration_cannot_share_an_input_key(self):
        original = qualified()
        original_key = AUDIT.audit({"materials": [original]})["materials"][0]["sameInputKey"]
        altered = qualified()
        altered["finalProviderInput"]["requestEnvelope"]["temperature"] = 1
        result = AUDIT.audit({"materials": [altered]})["materials"][0]
        self.assertIn("provider-input-sha256-mismatch", result["reasons"])
        self.assertFalse(result["eligibleForSameInputStatistics"])
        versioned = qualified()
        versioned["finalProviderInput"]["configurationVersion"] = "provider-config:8"
        new_key = AUDIT.audit({"materials": [versioned]})["materials"][0]["sameInputKey"]
        self.assertNotEqual(original_key, new_key)

    def test_cli_writes_filterable_report_without_copying_prompt_text(self):
        with tempfile.TemporaryDirectory() as directory:
            manifest = Path(directory) / "manifest.json"
            report = Path(directory) / "report.json"
            manifest.write_text(json.dumps({"materials": [qualified()]}), encoding="utf-8")
            completed = subprocess.run([sys.executable, str(SCRIPT), str(manifest), "--output", str(report)],
                                       capture_output=True, text=True, check=False)
            self.assertEqual(completed.returncode, 0, completed.stderr)
            output = json.loads(report.read_text(encoding="utf-8"))
            self.assertEqual(output["sameInputEligibleCount"], 1)
            self.assertNotIn("fixed input", report.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
