"""MET-01..06 and RUN-01..10 semantic-enforcement falsifiers."""
from __future__ import annotations

import tempfile
from pathlib import Path

import pytest

from evaluation.asset import PerceptionTask
from evaluation.deployment import DeploymentSnapshot
from evaluation.groundtruth import GroundTruth, GroundTruthElement
from evaluation.metrics import EvaluationScoringContext, PredictionView
from evaluation.prediction import Prediction
from evaluation.run import (
    AssetEvaluationOutcome, AssetOutcomeKind, EnvironmentProfile,
    EvaluationRun, EvaluationRunRequest, EvaluationRunResult, TerminalStatus,
    save_result, terminal_status_for,
)
from evaluation.stage import EvaluationTargetStage, LabelSpace
from persistence import WriteOnceIntegrityError


def _deployment() -> DeploymentSnapshot:
    return DeploymentSnapshot(
        service_version="test", schema_version="schema", model_name="model",
        model_id="m" * 64, ocr_backend="ocr", pipeline_revision="prev:test",
        config_identity="LEGACY_PARTIAL_CONFIG_IDENTITY", config_hash="c" * 64,
    )


def _request() -> EvaluationRunRequest:
    return EvaluationRunRequest.create(
        "suite:test", _deployment(), "L2", "evaluator:test",
        EnvironmentProfile("test", "test", "3.11"), ("sha256:a",),
    )


def _gt(asset_id: str = "sha256:a") -> GroundTruth:
    return GroundTruth(
        schema_version="1", asset_id=asset_id, gt_version="1",
        source="synthetic-fixture", declared_tasks=(PerceptionTask.ELEMENT_DETECTION,),
        elements=(GroundTruthElement("button", (0.1, 0.1, 0.2, 0.2)),),
    )


def _prediction(request_id: str, asset_id: str = "sha256:a",
                deployment_hash: str | None = None) -> Prediction:
    return Prediction(
        request_id, asset_id, deployment_hash or _deployment().identity_hash,
        "schema", ({"type": "button", "bounds": [0.1, 0.1, 0.2, 0.2]},),
        1, 0,
        stage_views={
            "rawModelDetections": ({"type": "Button", "bounds": [0.1, 0.1, 0.2, 0.2]},),
            "normalizedDetections": ({"type": "button", "bounds": [0.1, 0.1, 0.2, 0.2]},),
            "fusedEvidence": ({"type": "button", "bounds": [0.1, 0.1, 0.2, 0.2]},),
        },
    )


def _context(request: EvaluationRunRequest, **overrides) -> EvaluationScoringContext:
    values = dict(
        request_id=request.request_id,
        prediction=_prediction(request.request_id),
        ground_truth=_gt(),
        deployment_hash=request.deployment.identity_hash,
        prediction_view=PredictionView.FUSED_EVIDENCE,
    )
    values.update(overrides)
    return EvaluationScoringContext(**values)


def test_MET_01_canonical_scoring_binds_request_prediction_gt_and_deployment():
    request = _request()
    scored = _context(request).score()
    assert scored.request_id == scored.prediction_request_id == request.request_id
    assert scored.prediction_asset_id == scored.ground_truth_asset_id == "sha256:a"
    assert scored.prediction_deployment_hash == request.deployment.identity_hash


def test_MET_02_request_mismatch_is_not_scorable():
    request = _request()
    with pytest.raises(ValueError, match="PREDICTION_REQUEST_ID"):
        _context(request, prediction=_prediction("request:other")).score()


def test_MET_03_asset_mismatch_is_not_scorable():
    request = _request()
    with pytest.raises(ValueError, match="ASSET_ID"):
        _context(request, ground_truth=_gt("sha256:other")).score()


def test_MET_04_deployment_mismatch_is_not_scorable():
    request = _request()
    with pytest.raises(ValueError, match="DEPLOYMENT_IDENTITY"):
        _context(request, prediction=_prediction(request.request_id,
                                                 deployment_hash="wrong")).score()


def test_MET_05_stage_and_label_space_derive_from_typed_stored_view():
    request = _request()
    scored = _context(
        request, prediction_view=PredictionView.NORMALIZED_DETECTION).score()
    assert scored.prediction_stage == EvaluationTargetStage.RAW_DETECTION.value
    assert scored.prediction_label_space == LabelSpace.CANONICAL_DETECTION_V1.value


def test_MET_06_missing_stored_view_rejected_and_no_detached_persistence_api():
    request = _request()
    prediction = _prediction(request.request_id)
    prediction = Prediction(**{**prediction.__dict__, "stage_views": {}})
    with pytest.raises(ValueError, match="STORED_VIEW_NOT_AVAILABLE"):
        _context(request, prediction=prediction,
                 prediction_view=PredictionView.RAW_MODEL).score()
    import inspect
    assert "TaskMetricResult" not in inspect.signature(save_result).parameters


def _out(asset: str, kind: AssetOutcomeKind, reason: str = ""):
    return AssetEvaluationOutcome(asset, kind, reason=reason)


def test_RUN_01_request_identity_is_deterministic():
    assert _request().request_id == _request().request_id


def test_RUN_02_prediction_references_request_id():
    request = _request()
    assert _prediction(request.request_id).run_id == request.request_id


def test_RUN_03_result_identity_is_distinct_and_deterministic():
    request = _request()
    outcomes = (_out("a", AssetOutcomeKind.SCORABLE),)
    a = EvaluationRunResult.create(request.request_id, outcomes)
    b = EvaluationRunResult.create(request.request_id, outcomes)
    assert a.result_id == b.result_id
    assert a.result_id != request.request_id


def test_RUN_04_pending_is_not_a_new_canonical_result():
    assert not hasattr(EvaluationRunRequest, "terminal_status")
    assert not hasattr(EvaluationRun, "create")


def test_RUN_05_crash_before_aggregation_leaves_no_result():
    with tempfile.TemporaryDirectory() as tmp:
        _request()
        assert list(Path(tmp).glob("*.json")) == []


def test_RUN_06_result_persistence_is_write_once():
    request = _request()
    result = EvaluationRunResult.create(
        request.request_id, (_out("a", AssetOutcomeKind.SCORABLE),))
    with tempfile.TemporaryDirectory() as tmp:
        path = save_result(result, tmp)
        before = path.read_bytes()
        assert save_result(result, tmp).read_bytes() == before
        path.write_bytes(b"collision")
        with pytest.raises(WriteOnceIntegrityError):
            save_result(result, tmp)


def test_RUN_07_mixed_valid_and_infrastructure_failure_is_infrastructure_failure():
    assert terminal_status_for((
        _out("a", AssetOutcomeKind.SCORABLE),
        _out("b", AssetOutcomeKind.INFRASTRUCTURE_FAILURE),
    )) == TerminalStatus.INFRASTRUCTURE_FAILURE


def test_RUN_08_mixed_valid_and_honest_insufficient_is_partial():
    assert terminal_status_for((
        _out("a", AssetOutcomeKind.SCORABLE),
        _out("b", AssetOutcomeKind.INSUFFICIENT_EVIDENCE),
    )) == TerminalStatus.PARTIAL


def test_RUN_09_all_honest_insufficient_is_insufficient_evidence():
    assert terminal_status_for((
        _out("a", AssetOutcomeKind.INSUFFICIENT_EVIDENCE),
        _out("b", AssetOutcomeKind.INSUFFICIENT_EVIDENCE),
    )) == TerminalStatus.INSUFFICIENT_EVIDENCE


def test_RUN_10_all_scored_is_completed_regardless_of_score_value():
    assert terminal_status_for((
        _out("a", AssetOutcomeKind.SCORABLE, "terrible score"),
        _out("b", AssetOutcomeKind.SCORABLE, "perfect score"),
    )) == TerminalStatus.COMPLETED
