"""P4-T8 — candidate enters the EXISTING frozen L2 evaluation workflow.

No training_evaluator, no candidate scorer — the same EvaluationRun /
Prediction / Matcher / Metrics / Scorecard machinery evaluates the
candidate model exactly like the ACTIVE baseline.
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from evaluation.deployment import DeploymentSnapshot
from evaluation.first_baseline import (
    EVALUATOR_REVISION, PREDICTIONS_DIR, RUNS_DIR, execute_baseline,
    build_seed_suite, load_asset_manifest, _load_gt,
)
from evaluation.runner_l2 import run_fresh_inference
from evaluation.metrics import EvaluationScoringContext, PredictionView
from evaluation.groundtruth import TaskStance
from evaluation.asset import AdmissionStance
from evaluation.suite import EvaluationSuite, SuiteMembership
from evaluation.run import (
    AssetEvaluationOutcome, AssetOutcomeKind, EnvironmentProfile,
    EvaluationRunRequest, EvaluationRunResult, save_result,
)
from evaluation.identity import sha256_file

BASE = Path(__file__).resolve().parent
ARTIFACTS = BASE / "artifacts"
EVAL_REPORTS = Path(__file__).resolve().parent.parent / "evaluation" / "reports"


def _candidate_snapshot(model_id: str, model_name: str) -> DeploymentSnapshot:
    base = DeploymentSnapshot.current_active()
    return DeploymentSnapshot(
        service_version=base.service_version,
        schema_version=base.schema_version,
        model_name=model_name,
        model_id=model_id,
        ocr_backend=base.ocr_backend,
        pipeline_revision=base.pipeline_revision,
        config_identity=base.config_identity,
        config_hash=base.config_hash,
        profile="CANDIDATE_TEST_ONLY_PROFILE",
    )


def evaluate_candidate(model_id: str, model_name: str,
                       artifact_path: str) -> dict[str, Any]:
    """Evaluate a candidate through the frozen workflow."""
    import platform as _platform

    # suite: existing fused-stage synthetic fixture (mechanism proof)
    eval_assets_dir = Path(__file__).resolve().parent.parent / "evaluation" / "assets"
    manifests = eval_assets_dir / "manifests"
    assets = [load_asset_manifest(f) for f in manifests.glob("*.json")]
    scored_assets = [a for a in assets
                     if a.admission == AdmissionStance.ADMITTED
                     and _load_gt(a.asset_id) is not None]
    if not scored_assets:
        return {"status": "NOT_EXECUTABLE", "reason": "no scored evaluation assets"}

    suite = EvaluationSuite(
        suite_schema_version="1.0",
        backend="L2_RECORDED_IMAGE_INFERENCE",
        evaluator_revision=EVALUATOR_REVISION,
        required_tasks=(),
        members=tuple(SuiteMembership(asset_id=a.asset_id, roles=())
                      for a in scored_assets),
        description="candidate evaluation suite (frozen workflow reuse)",
    )

    deployment = _candidate_snapshot(model_id, model_name)
    env = EnvironmentProfile(
        os_name=_platform.system(), cpu_arch=_platform.machine(),
        python_version=_platform.python_version(),
    )
    request = EvaluationRunRequest.create(
        suite_id=suite.suite_id, deployment=deployment,
        backend="L2_RECORDED_IMAGE_INFERENCE",
        evaluator_revision=EVALUATOR_REVISION, environment=env,
    )

    per_asset = []
    for a in scored_assets:
        try:
            pred = run_fresh_inference(
                a.source_path, request.request_id, a.asset_id, deployment,
                model_path_override=artifact_path)
        except Exception as exc:  # infra failure — recorded truthfully
            per_asset.append({"assetId": a.asset_id, "scored": False,
                              "infrastructureError": str(exc)})
            continue
        gt = _load_gt(a.asset_id)
        scoring = EvaluationScoringContext(
            request_id=request.request_id,
            prediction=pred,
            ground_truth=gt,
            deployment_hash=deployment.identity_hash,
            prediction_view=PredictionView.FUSED_EVIDENCE,
        ).score()
        results = scoring.task_results
        tasks = {}
        any_scored = False
        for task, r in results.items():
            tasks[task.value] = {"stance": r.stance.value,
                                 "metrics": r.metrics, "denominator": r.denominator}
            if r.stance == TaskStance.SCORED:
                any_scored = True
        per_asset.append({"assetId": a.asset_id, "scored": any_scored,
                          "tasks": tasks,
                          "scoringProvenance": {
                              "requestId": scoring.request_id,
                              "predictionAssetId": scoring.prediction_asset_id,
                              "predictionRequestId": scoring.prediction_request_id,
                              "deploymentHash": scoring.prediction_deployment_hash,
                              "predictionView": scoring.prediction_view.value,
                              "predictionStage": scoring.prediction_stage,
                              "predictionLabelSpace": scoring.prediction_label_space,
                              "compatibilityVerdict": scoring.compatibility_verdict,
                          },
                          "prediction": {"yoloCount": pred.yolo_count,
                                         "ocrCount": pred.ocr_count,
                                         "timingsMs": pred.timings_ms}})
    outcomes = tuple(
        AssetEvaluationOutcome(
            item["assetId"],
            AssetOutcomeKind.INFRASTRUCTURE_FAILURE
            if item.get("infrastructureError")
            else AssetOutcomeKind.SCORABLE
            if item.get("scored")
            else AssetOutcomeKind.INSUFFICIENT_EVIDENCE,
            item.get("scoringProvenance", {}).get("predictionRequestId", ""),
            item.get("infrastructureError", ""),
        )
        for item in per_asset
    )
    result = EvaluationRunResult.create(request.request_id, outcomes)
    save_result(result, RUNS_DIR)
    return {
        "status": "EVALUATED",
        "runId": request.request_id,
        "resultId": result.result_id,
        "terminalStatus": result.terminal_status.value,
        "suiteId": suite.suite_id,
        "modelId": model_id,
        "perAsset": per_asset,
    }


def main() -> int:
    artifact_files = sorted(
        (ARTIFACTS / "manifests" / "model-artifacts").glob("*.json"))
    if not artifact_files:
        print("no model artifact — run training.mini first")
        return 1
    latest = json.loads(artifact_files[-1].read_text(encoding="utf-8"))
    result = evaluate_candidate(
        model_id=latest["modelId"], model_name=latest["modelName"],
        artifact_path=latest["artifactPath"])
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["status"] == "EVALUATED" else 1


if __name__ == "__main__":
    raise SystemExit(main())
