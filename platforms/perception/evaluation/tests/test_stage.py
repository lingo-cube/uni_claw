"""Stage + label-space contract falsifiers (ST-01..ST-08)."""
from __future__ import annotations

import unittest

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import PerceptionTask
from evaluation.groundtruth import GroundTruth, GroundTruthElement, TaskStance
from evaluation.metrics import compute_task_metrics
from evaluation.stage import (
    CompatibilityVerdict, EvaluationTargetStage, LabelSpace, check_compatibility,
)


def _gt(*, stage: EvaluationTargetStage, space: LabelSpace,
        tasks=(PerceptionTask.ELEMENT_DETECTION,),
        counts: dict | None = None,
        elements: tuple = ()) -> GroundTruth:
    return GroundTruth(
        schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:x",
        gt_version="1", source="test",
        evaluation_target_stage=stage, label_space=space,
        declared_tasks=tasks,
        expected_class_counts=counts,
        elements=elements,
    )


class CompatibilityTests(unittest.TestCase):
    def test_guard_matrix(self):
        self.assertEqual(
            check_compatibility(EvaluationTargetStage.RAW_DETECTION,
                                LabelSpace.DEKI_YOLO_RAW_V1,
                                EvaluationTargetStage.RAW_DETECTION,
                                LabelSpace.DEKI_YOLO_RAW_V1),
            CompatibilityVerdict.SCORABLE)
        self.assertEqual(
            check_compatibility(EvaluationTargetStage.RAW_DETECTION,
                                LabelSpace.DEKI_YOLO_RAW_V1,
                                EvaluationTargetStage.FUSED_EVIDENCE,
                                LabelSpace.DEKI_YOLO_RAW_V1),
            CompatibilityVerdict.STAGE_MISMATCH)
        self.assertEqual(
            check_compatibility(EvaluationTargetStage.RAW_DETECTION,
                                LabelSpace.DEKI_YOLO_RAW_V1,
                                EvaluationTargetStage.RAW_DETECTION,
                                LabelSpace.CANONICAL_DETECTION_V1),
            CompatibilityVerdict.LABEL_SPACE_MISMATCH)
        self.assertEqual(
            check_compatibility(EvaluationTargetStage.RAW_DETECTION,
                                LabelSpace.UNRESOLVED,
                                EvaluationTargetStage.RAW_DETECTION,
                                LabelSpace.DEKI_YOLO_RAW_V1),
            CompatibilityVerdict.UNRESOLVED_DIAGNOSTIC_ONLY)

    def test_ST01_raw_deki_cannot_score_raw_canonical(self):
        gt = _gt(stage=EvaluationTargetStage.RAW_DETECTION,
                 space=LabelSpace.DEKI_YOLO_RAW_V1,
                 counts={"text": 3})
        r = compute_task_metrics(
            [{"type": "text_block", "bounds": None}], gt,
            pred_stage=EvaluationTargetStage.RAW_DETECTION,
            pred_label_space=LabelSpace.CANONICAL_DETECTION_V1)
        det = r[PerceptionTask.ELEMENT_DETECTION]
        self.assertEqual(det.stance, TaskStance.NOT_SCORABLE)
        self.assertIn("LABEL_SPACE_MISMATCH", det.note)

    def test_ST02_raw_detection_cannot_score_fused_evidence(self):
        gt = _gt(stage=EvaluationTargetStage.RAW_DETECTION,
                 space=LabelSpace.DEKI_YOLO_RAW_V1,
                 counts={"text": 3})
        r = compute_task_metrics(
            [{"type": "text_block", "bounds": None}], gt,
            pred_stage=EvaluationTargetStage.FUSED_EVIDENCE,
            pred_label_space=LabelSpace.FUSED_OUTPUT_V1)
        det = r[PerceptionTask.ELEMENT_DETECTION]
        self.assertEqual(det.stance, TaskStance.NOT_SCORABLE)
        self.assertIn("STAGE_MISMATCH", det.note)

    def test_ST03_historical_unresolved_counts_diagnostic_only(self):
        gt = _gt(stage=EvaluationTargetStage.RAW_DETECTION,
                 space=LabelSpace.UNRESOLVED,
                 counts={"text_block": 17, "icon": 13})
        r = compute_task_metrics(
            [{"type": "text_block", "bounds": None}], gt,
            pred_stage=EvaluationTargetStage.RAW_DETECTION,
            pred_label_space=LabelSpace.CANONICAL_DETECTION_V1)
        det = r[PerceptionTask.ELEMENT_DETECTION]
        self.assertEqual(det.stance, TaskStance.DIAGNOSTIC_ONLY)
        self.assertIn("NOT_RELEASE_ELIGIBLE", det.note)

    def test_ST04_raw_training_annotation_scores_raw_model_view(self):
        gt = _gt(stage=EvaluationTargetStage.RAW_DETECTION,
                 space=LabelSpace.DEKI_YOLO_RAW_V1,
                 counts={"text": 2})
        r = compute_task_metrics(
            [{"type": "text", "bounds": None}, {"type": "text", "bounds": None}],
            gt,
            pred_stage=EvaluationTargetStage.RAW_DETECTION,
            pred_label_space=LabelSpace.DEKI_YOLO_RAW_V1)
        det = r[PerceptionTask.ELEMENT_DETECTION]
        self.assertEqual(det.stance, TaskStance.SCORED)
        self.assertEqual(det.metrics["mode"], "count_conformance")
        self.assertEqual(det.metrics["perClass"]["text"]["actual"], 2)
        self.assertEqual(det.metrics["perClass"]["text"]["match"], True)

    def test_ST05_normalized_gt_scores_normalized_view(self):
        gt = _gt(stage=EvaluationTargetStage.RAW_DETECTION,
                 space=LabelSpace.CANONICAL_DETECTION_V1,
                 counts={"text_block": 1})
        r = compute_task_metrics(
            [{"type": "text_block", "bounds": None}], gt,
            pred_stage=EvaluationTargetStage.RAW_DETECTION,
            pred_label_space=LabelSpace.CANONICAL_DETECTION_V1)
        self.assertEqual(r[PerceptionTask.ELEMENT_DETECTION].stance,
                         TaskStance.SCORED)

    def test_ST06_fusion_gt_cannot_consume_normalized_detections(self):
        gt = _gt(stage=EvaluationTargetStage.FUSED_EVIDENCE,
                 space=LabelSpace.FUSED_OUTPUT_V1,
                 elements=(GroundTruthElement(gt_class="text_block",
                                              bounds=(0.1, 0.1, 0.4, 0.2)),))
        r = compute_task_metrics(
            [{"type": "text_block", "bounds": (0.1, 0.1, 0.4, 0.2)}], gt,
            pred_stage=EvaluationTargetStage.RAW_DETECTION,
            pred_label_space=LabelSpace.CANONICAL_DETECTION_V1)
        det = r[PerceptionTask.ELEMENT_DETECTION]
        self.assertEqual(det.stance, TaskStance.NOT_SCORABLE)
        self.assertIn("STAGE_MISMATCH", det.note)

    def test_ST07_no_silent_alias_invocation(self):
        """The metric layer must not import or invoke YOLO_LABEL_ALIASES."""
        import evaluation.metrics as m
        import inspect
        src = inspect.getsource(m)
        self.assertNotIn("YOLO_LABEL_ALIASES", src)
        self.assertNotIn("normalize_yolo_label", src)
        import evaluation.matcher as mt
        self.assertNotIn("YOLO_LABEL_ALIASES", inspect.getsource(mt))

    def test_ST08_no_lossy_reverse_mapping(self):
        """No code path maps canonical labels back to raw YOLO classes."""
        import pkgutil, importlib, inspect
        import evaluation
        for mod in pkgutil.walk_packages(evaluation.__path__,
                                         prefix="evaluation."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            self.assertNotIn("canonical_to_raw", src)
            self.assertNotIn("reverse_alias", src)


if __name__ == "__main__":
    unittest.main()
