"""Metric tests: NOT_SCORABLE semantics, count mode, OCR presence, safety."""
from __future__ import annotations

import unittest

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import PerceptionTask
from evaluation.groundtruth import GroundTruth, GroundTruthElement, TaskStance
from evaluation.metrics import compute_task_metrics, normalize_text


def _cand(t: str, bounds=None, text="", extra=None) -> dict:
    d: dict = {"type": t, "text": text}
    if bounds is not None:
        d["bounds"] = bounds
    if extra:
        d.update(extra)
    return d


class MetricsTests(unittest.TestCase):
    def test_element_metrics_full_recall(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="synthetic-fixture",
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
            elements=(GroundTruthElement(gt_class="text_block",
                                         bounds=(0.1, 0.1, 0.4, 0.2)),),
        )
        results = compute_task_metrics([_cand("text_block", (0.1, 0.1, 0.4, 0.2))], gt)
        r = results[PerceptionTask.ELEMENT_DETECTION]
        self.assertEqual(r.stance, TaskStance.SCORED)
        self.assertEqual(r.metrics["tp"], 1)
        self.assertEqual(r.metrics["precision"], 1.0)
        self.assertEqual(r.metrics["recall"], 1.0)
        self.assertEqual(r.denominator, 1)

    def test_count_conformance_mode(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="harness-manifest-v1",
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
            expected_class_counts={"text_block": 2, "icon": 1},
        )
        preds = [_cand("text_block"), _cand("text_block"), _cand("list_item")]
        r = compute_task_metrics(preds, gt)[PerceptionTask.ELEMENT_DETECTION]
        self.assertEqual(r.stance, TaskStance.SCORED)
        self.assertEqual(r.metrics["mode"], "count_conformance")
        self.assertEqual(r.metrics["perClass"]["text_block"]["actual"], 2)
        self.assertEqual(r.metrics["perClass"]["text_block"]["match"], True)
        self.assertEqual(r.metrics["perClass"]["icon"]["actual"], 0)
        self.assertEqual(r.metrics["perClass"]["icon"]["match"], False)
        self.assertEqual(r.denominator, 2)

    def test_ocr_presence(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="harness-manifest-v1",
            declared_tasks=(PerceptionTask.OCR,),
            expected_texts=("Search settings", "System"),
        )
        preds = [_cand("text_block", text="Search settings"),
                 _cand("text_block", text="About emulated device")]
        r = compute_task_metrics(preds, gt)[PerceptionTask.OCR]
        self.assertEqual(r.stance, TaskStance.SCORED)
        self.assertEqual(r.metrics["found"], 1)
        self.assertEqual(r.metrics["missing"], 1)
        self.assertEqual(r.metrics["missingTexts"], ["System"])
        self.assertEqual(r.denominator, 2)

    def test_ocr_not_scorable_without_gt(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="synthetic-fixture",
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
        )
        r = compute_task_metrics([_cand("text_block", text="x")], gt)[PerceptionTask.OCR]
        self.assertEqual(r.stance, TaskStance.NOT_SCORABLE)
        self.assertNotEqual(r.stance, TaskStance.SCORED)

    def test_bounds_not_scorable_without_element_bounds(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="harness-manifest-v1",
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,
                            PerceptionTask.BOUNDS),
            expected_class_counts={"text_block": 1},
        )
        r = compute_task_metrics([], gt)[PerceptionTask.BOUNDS]
        self.assertEqual(r.stance, TaskStance.NOT_SCORABLE)

    def test_safety_coordinate_validity_always_scorable(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="synthetic-fixture",
            declared_tasks=(),
        )
        preds = [_cand("text_block", (0.1, 0.1, 0.4, 0.2)),
                 _cand("icon", (0.5, 0.5, 1.3, 0.6))]   # x2 out of range
        r = compute_task_metrics(preds, gt)[PerceptionTask.SAFETY]
        self.assertEqual(r.stance, TaskStance.SCORED)
        self.assertEqual(r.metrics["invalidCoordinateBounds"], 1)
        self.assertEqual(r.metrics["coordinateValidityRate"], 0.5)

    def test_safety_fabrication_requires_element_gt(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="synthetic-fixture",
            declared_tasks=(),
        )
        r = compute_task_metrics([_cand("text_block", (0.1, 0.1, 0.4, 0.2))], gt)
        s = r[PerceptionTask.SAFETY]
        self.assertIsNone(s.metrics["fabricationRate"])
        self.assertIn("note", s.metrics)

    def test_normalize_text_matches_legacy_rules(self):
        self.assertEqual(normalize_text("Bluetooth, pairing"),
                         normalize_text("Bluetooth,pairing"))
        self.assertEqual(normalize_text("  X   Y  "), "x y")
        self.assertEqual(normalize_text(""), "")


if __name__ == "__main__":
    unittest.main()
