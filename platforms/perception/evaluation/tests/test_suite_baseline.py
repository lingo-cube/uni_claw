"""Suite versioning + baseline immutability falsifiers: PF2, B16."""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import CorpusRole, PerceptionTask
from evaluation.provenance_scorecard import ProvenanceBoundScorecard
from evaluation.baseline import (
    BaselineImmutabilityError, BaselineReport, persist_baseline,
)
from evaluation.suite import EvaluationSuite, SuiteMembership, save_suite


def _suite(asset_ids: list[str]) -> EvaluationSuite:
    return EvaluationSuite(
        suite_schema_version=EVALUATION_SCHEMA_VERSION,
        backend="L2_RECORDED_IMAGE_INFERENCE",
        evaluator_revision="evaluator-v1",
        required_tasks=(PerceptionTask.ELEMENT_DETECTION,),
        members=tuple(SuiteMembership(asset_id=a,
                                      roles=(CorpusRole.CALIBRATION,))
                      for a in asset_ids),
        description="test suite",
    )


class SuiteTests(unittest.TestCase):
    def test_PF2_new_membership_creates_new_suite_version_not_mutation(self):
        v1 = _suite(["sha256:a"])
        v2 = v1.with_members(
            tuple(SuiteMembership(asset_id=a,
                                  roles=(CorpusRole.CALIBRATION,))
                  for a in ["sha256:a", "sha256:b"]))
        self.assertNotEqual(v1.suite_id, v2.suite_id)
        # v1 remains intact — no mutation
        self.assertEqual(len(v1.members), 1)
        self.assertEqual(v1.suite_id, _suite(["sha256:a"]).suite_id)

    def test_suite_persist_then_load_roundtrip(self):
        with tempfile.TemporaryDirectory() as tmp:
            v1 = _suite(["sha256:a"])
            p = save_suite(v1, Path(tmp))
            from evaluation.suite import load_suite
            loaded = load_suite(p)
            self.assertEqual(loaded.suite_id, v1.suite_id)
            self.assertEqual(len(loaded.members), 1)
            self.assertEqual(loaded.members[0].asset_id, "sha256:a")


class BaselineTests(unittest.TestCase):
    def _report(self, suite_id: str = "suite:x",
                scored: int = 1, total: int = 1) -> BaselineReport:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            prediction_dir = root / "predictions"
            ground_truth_dir = root / "groundtruth"
            prediction_dir.mkdir()
            ground_truth_dir.mkdir()
            return BaselineReport.create(
            deployment={"serviceVersion": "1.0", "modelId": "m" * 64,
                        "schemaVersion": "uniclaw.localVisionEvidence.v1",
                        "modelName": "android_ui_detection_yolov8",
                        "ocrBackend": "rapidocr", "pipelineRevision": "1.0.0",
                        "configIdentity": "LEGACY_PARTIAL_CONFIG_IDENTITY",
                        "configHash": "a85d7e78a27cde2321c64a8d62fab46179242f056f1addb6bf6698839aafddc3"},
            suite_id=suite_id, evaluator_revision="evaluator-v1",
            environment={"os": "Darwin", "cpuArch": "x86_64",
                         "pythonVersion": "3.11"},
            asset_count=total, scored_count=scored, unscored_count=total - scored,
            asset_classifications=[{"assetId": "sha256:a",
                                    "systemFamily": "UNKNOWN"}],
            request_id="run:test",
            deployment_hash="deploy:test",
            scoring_results=[],
            prediction_dir=prediction_dir,
            ground_truth_dir=ground_truth_dir,
            classified=[],
            declared_tasks=[],
            safety_scorecard={"visible": True, "perAsset": {}},
            performance={"status": "VALID"},
            coverage_gaps=["no holdout"], ground_truth_gaps=[],
            unassessed_categories=[],
        )

    def test_B16_baseline_immutable_after_creation(self):
        with tempfile.TemporaryDirectory() as tmp:
            r = self._report()
            p1 = persist_baseline(r, Path(tmp))
            self.assertTrue(p1.exists())
            content = p1.read_text(encoding="utf-8")
            # identical re-persist is a no-op (same content)
            p2 = persist_baseline(r, Path(tmp))
            self.assertEqual(p1, p2)
            self.assertEqual(p1.read_text(encoding="utf-8"), content)
            # Direct filesystem tampering is never repairable by a write:
            # same identity with different bytes must be refused.
            with self.assertRaises(BaselineImmutabilityError):
                target = Path(tmp) / f"{r.baseline_id.replace('baseline:', '')}.json"
                target.write_text(json.dumps({"tampered": True}))
                persist_baseline(r, Path(tmp))

    def test_IMM_05_baseline_overwrite_escape_hatch_is_not_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            report = self._report()
            persist_baseline(report, Path(tmp))
            with self.assertRaises(TypeError):
                persist_baseline(report, Path(tmp), overwrite=True)

    def test_new_inputs_new_baseline_id(self):
        r1 = self._report(suite_id="suite:1")
        r2 = self._report(suite_id="suite:2")
        self.assertNotEqual(r1.baseline_id, r2.baseline_id)

    def test_baseline_fields_truthful_defaults(self):
        r = self._report()
        self.assertEqual(r.holdout_status, "NONE")
        self.assertEqual(r.numeric_thresholds, "NOT_FROZEN")


if __name__ == "__main__":
    unittest.main()
