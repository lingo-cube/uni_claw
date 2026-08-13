"""RM-MET-01..10 — canonical quality record-minting closure (GAP-004)."""
from __future__ import annotations

import unittest
import tempfile
from pathlib import Path

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import PerceptionTask
from evaluation.baseline import BaselineReport
from evaluation.groundtruth import (
    GroundTruth, GroundTruthElement, TaskStance, save_groundtruth,
)
from evaluation.metrics import (
    EvaluationScoringContext, EvaluationScoringResult, PredictionView,
    TaskMetricResult,
)
from evaluation.provenance_scorecard import (
    CanonicalVerificationError, ProvenanceBoundScorecard,
    build_provenance_bound_scorecard,
)
from evaluation.stage import EvaluationTargetStage, LabelSpace
from evaluation.prediction import Prediction, save_prediction


def _gt(asset_id: str = "sha256:gt") -> GroundTruth:
    return GroundTruth(
        schema_version=EVALUATION_SCHEMA_VERSION, asset_id=asset_id,
        gt_version="1", source="synthetic-fixture",
        evaluation_target_stage=EvaluationTargetStage.FUSED_EVIDENCE,
        label_space=LabelSpace.FUSED_OUTPUT_V1,
        declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
    )


def _pred(asset_id: str = "sha256:gt", run_id: str = "run:r",
          deployment_hash: str = "deploy:d") -> Prediction:
    return Prediction(
        run_id=run_id, asset_id=asset_id, deployment_hash=deployment_hash,
        schema_version="test", candidates=(), yolo_count=0, ocr_count=0,
        stage_views={"fusedEvidence": []})


class Store:
    """Canonical persisted-record store for verification tests."""

    def __init__(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.prediction_dir = self.root / "predictions"
        self.ground_truth_dir = self.root / "groundtruth"
        self.prediction_dir.mkdir()
        self.ground_truth_dir.mkdir()

    def persist(self, prediction, gt: GroundTruth) -> None:
        save_prediction(prediction, self.prediction_dir)
        save_groundtruth(gt, self.ground_truth_dir)


def _scoring_result(store: Store, *, asset_id="sha256:gt",
                    run_id="run:r", deployment_hash="deploy:d") -> EvaluationScoringResult:
    pred = _pred(asset_id, run_id, deployment_hash)
    gt = _gt(asset_id)
    store.persist(pred, gt)
    return EvaluationScoringContext(
        request_id=run_id, prediction=pred, ground_truth=gt,
        deployment_hash=deployment_hash,
        prediction_view=PredictionView.FUSED_EVIDENCE).score()


def _create(**over) -> BaselineReport:
    kwargs = dict(
        deployment={}, suite_id="s:x", evaluator_revision="ev",
        environment={}, asset_count=0, scored_count=0, unscored_count=0,
        asset_classifications=[],
        request_id="run:r", deployment_hash="deploy:d",
        scoring_results=[], prediction_dir=Path("missing-predictions"),
        ground_truth_dir=Path("missing-groundtruth"), classified=[],
        declared_tasks=[], safety_scorecard={}, performance={},
        coverage_gaps=[], ground_truth_gaps=[], unassessed_categories=[],
    )
    kwargs.update(over)
    return BaselineReport.create(**kwargs)


class RmMetTests(unittest.TestCase):
    def test_RM_MET01_direct_scorecard_construction_cannot_mint(self):
        """A caller-created ProvenanceBoundScorecard is NOT an accepted
        input to canonical quality persistence."""
        minted = ProvenanceBoundScorecard(
            request_id="run:FORGED", deployment_hash="deploy:FORGED",
            scoring_results=(),
            task_slices={"ELEMENT_DETECTION": {
                "aggregate": {"mean": 1.0, "n": 1000}}},
            safety_section={}, coverage={}, evidence_sufficiency={})
        with self.assertRaises(TypeError):
            BaselineReport.create(
                deployment={}, suite_id="s:x", evaluator_revision="ev",
                environment={}, asset_count=0, scored_count=0,
                unscored_count=0, asset_classifications=[],
                quality_scorecard=minted,        # no such parameter anymore
                safety_scorecard={}, performance={}, coverage={},
                evidence_sufficiency={}, coverage_gaps=[],
                ground_truth_gaps=[], unassessed_categories=[])

    def test_RM_MET02_invented_task_slices_ignored(self):
        """There is no parameter through which invented taskSlices can
        enter BaselineReport — quality is derived only."""
        import inspect
        sig = inspect.signature(BaselineReport.create)
        for banned in ("taskSlices", "task_slices", "quality_scorecard",
                       "aggregate"):
            self.assertNotIn(banned, sig.parameters)

    def test_RM_MET03_zero_results_cannot_yield_invented_aggregates(self):
        report = _create()
        j = report.to_json()
        self.assertEqual(j["qualityScorecard"]["scoringResultCount"], 0)
        self.assertEqual(j["qualityScorecard"]["taskSlices"], {})
        self.assertNotIn("mean", j["qualityScorecard"].get("taskSlices", {})
                         .get("ELEMENT_DETECTION", {}))

    def test_RM_MET04_wrong_request_rejected(self):
        store = Store()
        result = _scoring_result(store)
        with self.assertRaises(CanonicalVerificationError):
            _create(scoring_results=[result],
                    prediction_dir=store.prediction_dir,
                    ground_truth_dir=store.ground_truth_dir,
                    request_id="run:WRONG")   # claimed request != persisted

    def test_RM_MET05_wrong_prediction_rejected(self):
        store = Store()
        result = _scoring_result(store)
        with self.assertRaises(CanonicalVerificationError):
            _create(scoring_results=[result],
                    prediction_dir=Path("missing-predictions"),
                    ground_truth_dir=store.ground_truth_dir)

    def test_RM_MET06_wrong_ground_truth_rejected(self):
        store = Store()
        result = _scoring_result(store)
        with self.assertRaises(CanonicalVerificationError):
            _create(scoring_results=[result],
                    prediction_dir=store.prediction_dir,
                    ground_truth_dir=Path("missing-groundtruth"))

    def test_RM_MET07_wrong_deployment_rejected(self):
        store = Store()
        result = _scoring_result(store)
        with self.assertRaises(CanonicalVerificationError):
            _create(scoring_results=[result],
                    prediction_dir=store.prediction_dir,
                    ground_truth_dir=store.ground_truth_dir,
                    deployment_hash="deploy:WRONG")

    def test_RM_MET08_wrong_stage_or_label_space_rejected(self):
        store = Store()
        result = _scoring_result(store)
        # tamper the CLAIMED stage/labelspace by forging a result copy
        forged = EvaluationScoringResult(
            request_id=result.request_id,
            prediction_asset_id=result.prediction_asset_id,
            prediction_request_id=result.prediction_request_id,
            prediction_deployment_hash=result.prediction_deployment_hash,
            ground_truth_asset_id=result.ground_truth_asset_id,
            ground_truth_version=result.ground_truth_version,
            ground_truth_source=result.ground_truth_source,
            prediction_view=result.prediction_view,
            prediction_stage="RAW_DETECTION",          # forged claim
            prediction_label_space="DEKI_YOLO_RAW_V1",
            compatibility_verdict="SCORABLE",
            task_results=result.task_results,
        )
        with self.assertRaises(CanonicalVerificationError):
            _create(scoring_results=[forged],
                    prediction_dir=store.prediction_dir,
                    ground_truth_dir=store.ground_truth_dir)

    def test_RM_MET09_canonical_summary_derived_from_verified_results(self):
        store = Store()
        result = _scoring_result(store)
        report = _create(scoring_results=[result],
                         prediction_dir=store.prediction_dir,
                         ground_truth_dir=store.ground_truth_dir)
        j = report.to_json()
        self.assertEqual(j["qualityScorecard"]["scoringResultCount"], 1)
        self.assertIn("requestId", j["qualityScorecard"])
        # summary derived from the VERIFIED (re-scored) results
        self.assertEqual(j["qualityScorecard"]["requestId"], "run:r")

    def test_RM_MET09a_forged_metric_claim_cannot_change_verified_quality(self):
        """A forged 1.0 metric claim has no aggregation power.

        The persisted prediction has no detections against count GT, so its
        verified ELEMENT_DETECTION F1 is 0.0.  Supplying a claimed 1.0 task
        result must leave the canonical task slice at the re-derived value.
        """
        store = Store()
        pred = _pred()
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id="sha256:gt",
            gt_version="1", source="synthetic-fixture",
            evaluation_target_stage=EvaluationTargetStage.FUSED_EVIDENCE,
            label_space=LabelSpace.FUSED_OUTPUT_V1,
            declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
            elements=(GroundTruthElement("Switch", (0.1, 0.1, 0.2, 0.2)),),
        )
        store.persist(pred, gt)
        result = EvaluationScoringContext(
            request_id="run:r", prediction=pred, ground_truth=gt,
            deployment_hash="deploy:d",
            prediction_view=PredictionView.FUSED_EVIDENCE).score()
        forged = EvaluationScoringResult(
            request_id=result.request_id,
            prediction_asset_id=result.prediction_asset_id,
            prediction_request_id=result.prediction_request_id,
            prediction_deployment_hash=result.prediction_deployment_hash,
            ground_truth_asset_id=result.ground_truth_asset_id,
            ground_truth_version=result.ground_truth_version,
            ground_truth_source=result.ground_truth_source,
            prediction_view=result.prediction_view,
            prediction_stage=result.prediction_stage,
            prediction_label_space=result.prediction_label_space,
            compatibility_verdict=result.compatibility_verdict,
            task_results={
                PerceptionTask.ELEMENT_DETECTION: TaskMetricResult(
                    PerceptionTask.ELEMENT_DETECTION, TaskStance.SCORED,
                    metrics={"f1": 1.0}, denominator=999),
            },
        )
        report = _create(
            scoring_results=[forged], prediction_dir=store.prediction_dir,
            ground_truth_dir=store.ground_truth_dir,
        )
        actual = report.to_json()["qualityScorecard"]["taskSlices"]
        self.assertEqual(actual["ELEMENT_DETECTION"]["aggregate"]["mean"], 0.0)
        self.assertEqual(actual["ELEMENT_DETECTION"]["denominator"], 1)

    def test_RM_MET09b_lying_loader_api_is_absent(self):
        """Canonical record loading cannot be overridden by a caller lambda."""
        import inspect
        signature = inspect.signature(BaselineReport.create)
        for banned in ("prediction_loader", "gt_loader", "asset_scores"):
            self.assertNotIn(banned, signature.parameters)

    def test_RM_MET10_no_alternate_public_quality_save_path(self):
        import pkgutil
        import evaluation
        writers = []
        for mod in pkgutil.walk_packages(evaluation.__path__,
                                         prefix="evaluation."):
            if "tests" in mod.name:
                continue
            # Source inspection intentionally avoids importing optional
            # provider dependencies (for example PIL) into this governance
            # check.
            relative = mod.name.removeprefix("evaluation.").replace(".", "/")
            module_path = Path(evaluation.__path__[0]) / f"{relative}.py"
            if not module_path.is_file():
                continue
            src = module_path.read_text(encoding="utf-8")
            if "qualityScorecard" in src and mod.name not in (
                    "evaluation.baseline", "evaluation.provenance_scorecard"):
                writers.append(mod.name)
        self.assertEqual(writers, [])


if __name__ == "__main__":
    unittest.main()
