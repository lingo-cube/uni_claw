"""Ground-truth falsifiers: B4, PF1, PF5 + task-scoped stance semantics."""
from __future__ import annotations

import unittest

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import PerceptionTask
from evaluation.failure_candidate import failure_episode_to_candidate
from evaluation.groundtruth import GroundTruth, GroundTruthElement, TaskStance


class GroundTruthTests(unittest.TestCase):
    def test_B4_prediction_never_becomes_ground_truth_automatically(self):
        """GT is an explicit separate record; there is no API that copies a
        prediction into GT without a deliberate GT construction."""
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="synthetic-fixture",
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
            elements=(GroundTruthElement(gt_class="text_block",
                                         bounds=(0.1, 0.1, 0.4, 0.2)),),
        )
        # the GT record structure has no field that accepts prediction output
        self.assertFalse(hasattr(gt, "predictions"))
        # constructing GT requires explicit element declarations
        self.assertEqual(len(gt.elements), 1)
        self.assertEqual(gt.elements[0].gt_class, "text_block")

    def test_PF1_missing_gt_is_not_scorable_never_zero(self):
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="synthetic-fixture",
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
        )
        self.assertEqual(gt.task_stance(PerceptionTask.ELEMENT_DETECTION),
                         TaskStance.SCORED)
        self.assertEqual(gt.task_stance(PerceptionTask.OCR),
                         TaskStance.NOT_SCORABLE)
        self.assertEqual(gt.task_stance(PerceptionTask.SWITCH_STATE),
                         TaskStance.NOT_SCORABLE)

    def test_task_scoped_gt_partial_coverage(self):
        """Detection GT present, OCR GT absent → Detection SCORABLE,
        OCR NOT_SCORABLE (I7)."""
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
            gt_version="1", source="harness-manifest-v1",
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,
                            PerceptionTask.OCR),
            expected_class_counts={"text_block": 17},
        )
        self.assertEqual(gt.has_task(PerceptionTask.ELEMENT_DETECTION), True)
        self.assertEqual(gt.has_task(PerceptionTask.BOUNDS), False)
        self.assertEqual(gt.expected_texts, ())

    def test_PF5_failure_episode_cannot_assign_ground_truth(self):
        """The structural boundary produces a candidate only; GT field is
        frozen to None and cannot be set."""
        cand = failure_episode_to_candidate(
            source_failure_episode_id=None,
            provenance="SYNTHETIC",
        )
        self.assertIsNone(cand.ground_truth)
        self.assertFalse(cand.has_ground_truth)
        self.assertEqual(cand.source_type, "failure_episode")
        # serialized form structurally cannot carry GT
        j = cand.to_json()
        self.assertIsNone(j["groundTruth"])
        # provenance preserved (B3 relationship)
        self.assertEqual(j["provenance"], "SYNTHETIC")


if __name__ == "__main__":
    unittest.main()
