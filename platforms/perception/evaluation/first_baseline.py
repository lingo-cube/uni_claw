"""First Perception Evaluation Baseline orchestrator (P4-4B…P4-4E).

Executes the CURRENT ACTIVE deployment fresh against the seed suite and
persists an immutable FIRST_PERCEPTION_EVALUATION_BASELINE.

Process first, quality second: small corpus, honest gaps, no weights,
no thresholds, no promotion.
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from . import EVALUATION_SCHEMA_VERSION
from .asset import (
    AdmissionStance, CorpusRole, EvaluationAsset, PerceptionTask,
    load_asset_manifest,
)
from .baseline import BaselineReport, persist_baseline
from .deployment import DeploymentSnapshot
from .groundtruth import GroundTruth, TaskStance, load_groundtruth
from .metrics import EvaluationScoringContext, PredictionView
from .performance import PerformanceResult, capture_environment
from .runner_l2 import EvaluationInfrastructureError, run_fresh_inference
from .run import (
    AssetEvaluationOutcome, AssetOutcomeKind, EnvironmentProfile,
    EvaluationRunRequest, EvaluationRunResult, save_result,
)
from .provenance_scorecard import build_provenance_bound_scorecard
from .scorecard import build_coverage
from .suite import EvaluationSuite, SuiteMembership, save_suite
from .prediction import save_prediction

BASE = Path(__file__).resolve().parent
ASSETS_DIR = BASE / "assets"
MANIFESTS_DIR = ASSETS_DIR / "manifests"
GT_DIR = ASSETS_DIR / "groundtruth"
SUITES_DIR = BASE / "suites"
REPORTS_DIR = BASE / "reports"
RUNS_DIR = REPORTS_DIR / "runs"
PREDICTIONS_DIR = REPORTS_DIR / "predictions"
BASELINES_DIR = REPORTS_DIR / "baselines"

EVALUATOR_REVISION = "evaluator-v1"


def _load_gt(asset_id: str) -> GroundTruth | None:
    """GAP-004 FINAL: deterministic GroundTruth resolution for scoring.

    Multiple GT versions for one asset are AMBIGUOUS and fail closed —
    glob-order / first-match selection is forbidden.  The exact version
    that was scored is then recorded into the terminal outcome, and the
    baseline re-loads that exact identity (never re-globbing)."""
    if not GT_DIR.exists():
        return None
    matches = [gt for f in GT_DIR.glob("gt-*.json")
               if (gt := load_groundtruth(f)).asset_id == asset_id]
    if len(matches) > 1:
        raise ValueError(
            f"AMBIGUOUS_GROUND_TRUTH: multiple GT versions exist for "
            f"{asset_id} — exact version selection is required")
    return matches[0] if matches else None


def build_seed_suite(admitted_asset_ids: list[str],
                     needs_gt_asset_ids: list[str],
                     evaluator_revision: str = EVALUATOR_REVISION) -> EvaluationSuite:
    """Smallest truthful seed suite (I11).

    Includes ADMITTED assets (scored-capable) AND NEEDS_GROUND_TRUTH assets
    (unscored members prove the NOT_SCORABLE semantics; they also receive
    fresh inference + performance measurement).
    """
    members: list[SuiteMembership] = []
    for a in admitted_asset_ids:
        members.append(SuiteMembership(asset_id=a, roles=(CorpusRole.CALIBRATION,)))
    for a in needs_gt_asset_ids:
        members.append(SuiteMembership(asset_id=a, roles=(CorpusRole.CALIBRATION,)))
    return EvaluationSuite(
        suite_schema_version=EVALUATION_SCHEMA_VERSION,
        backend="L2_RECORDED_IMAGE_INFERENCE",
        evaluator_revision=evaluator_revision,
        required_tasks=(PerceptionTask.ELEMENT_DETECTION, PerceptionTask.OCR,
                        PerceptionTask.BOUNDS, PerceptionTask.SAFETY),
        members=tuple(members),
        description="seed suite: current trusted reality + synthetic mechanics fixtures",
    )


def execute_asset(asset: EvaluationAsset, run: EvaluationRunRequest,
                  deployment: DeploymentSnapshot) -> dict[str, Any]:
    """Fresh L2 inference + task metrics for one suite member."""
    out: dict[str, Any] = {"assetId": asset.asset_id, "admission": asset.admission.value}

    # fresh inference (B5): even NEEDS_GROUND_TRUTH assets get a real
    # prediction — proves asset identity / L2 execution / deployment linkage
    try:
        pred = run_fresh_inference(asset.source_path, run.run_id, asset.asset_id,
                                   deployment)
        save_prediction(pred, PREDICTIONS_DIR)
        out["prediction"] = {"yoloCount": pred.yolo_count, "ocrCount": pred.ocr_count,
                             "timingsMs": pred.timings_ms,
                             "schemaVersion": pred.schema_version}
    except EvaluationInfrastructureError as exc:
        out["prediction"] = None
        out["infrastructureError"] = str(exc)
        out["scored"] = False
        return out

    gt = _load_gt(asset.asset_id)
    if gt is None or asset.admission != AdmissionStance.ADMITTED:
        # no GT → no scoring (PF1); infrastructure remains OK
        out["groundTruth"] = None
        out["scored"] = False
        return out

    scoring = EvaluationScoringContext(
        request_id=run.request_id,
        prediction=pred,
        ground_truth=gt,
        deployment_hash=run.deployment.identity_hash,
        prediction_view=PredictionView.FUSED_EVIDENCE,
    ).score()
    results = scoring.task_results
    tasks: dict[str, dict[str, Any]] = {}
    any_scored = False
    for task, r in results.items():
        tasks[task.value] = {"stance": r.stance.value, "metrics": r.metrics,
                             "denominator": r.denominator, "note": r.note}
        if r.stance == TaskStance.SCORED:
            any_scored = True
    out["groundTruth"] = {"gtVersion": gt.gt_version, "source": gt.source}
    out["scoringProvenance"] = {
        "requestId": scoring.request_id,
        "predictionAssetId": scoring.prediction_asset_id,
        "predictionRequestId": scoring.prediction_request_id,
        "predictionDeploymentHash": scoring.prediction_deployment_hash,
        "groundTruthAssetId": scoring.ground_truth_asset_id,
        "groundTruthVersion": scoring.ground_truth_version,
        "groundTruthSource": scoring.ground_truth_source,
        "predictionView": scoring.prediction_view.value,
        "predictionStage": scoring.prediction_stage,
        "predictionLabelSpace": scoring.prediction_label_space,
        "compatibilityVerdict": scoring.compatibility_verdict,
    }
    out["tasks"] = tasks
    out["scored"] = any_scored
    out["scoringResult"] = scoring   # provenance-bound canonical evidence
    return out


def run_performance(deployment: DeploymentSnapshot, run: EvaluationRunRequest,
                    asset: EvaluationAsset, samples: int = 3) -> PerformanceResult | None:
    """Minimal warm-analyze latency sampling (n small → median/mean only)."""
    from .performance import capture_environment as _env
    times: list[float] = []
    # first call warms the model (not counted)
    try:
        run_fresh_inference(asset.source_path, run.run_id, asset.asset_id, deployment)
    except EvaluationInfrastructureError as exc:
        return None
    for _ in range(samples):
        pred = run_fresh_inference(asset.source_path, run.run_id, asset.asset_id,
                                   deployment)
        times.append(pred.timings_ms["totalMs"])
    return PerformanceResult.create(
        times, run_id=run.run_id, deployment=deployment, asset_id=asset.asset_id,
        environment=_env(model_id=deployment.model_id, warm=True),
        evaluator_revision=EVALUATOR_REVISION, warm=True,
        input_resolution="1440x3168" if "settings-home" in asset.source_path
        else "synthetic-400x800",
    )


def execute_baseline(suite: EvaluationSuite, deployment: DeploymentSnapshot,
                     description: str = "FIRST_PERCEPTION_EVALUATION_BASELINE",
                     performance_asset_id: str | None = None,
                     performance_samples: int = 3,
                     created_at: str = "") -> dict[str, Any]:
    """Execute one suite version → run → predictions → scorecard → baseline."""
    import platform as _platform
    env_profile = EnvironmentProfile(
        os_name=_platform.system(), cpu_arch=_platform.machine(),
        python_version=_platform.python_version(),
    )
    request = EvaluationRunRequest.create(
        suite_id=suite.suite_id, deployment=deployment,
        backend=suite.backend, evaluator_revision=suite.evaluator_revision,
        environment=env_profile, created_at=created_at,
    )
    run_id = request.request_id

    # load assets by id from manifests
    assets_by_id: dict[str, EvaluationAsset] = {}
    for m in suite.members:
        for f in MANIFESTS_DIR.glob("*.json"):
            a = load_asset_manifest(f)
            if a.asset_id == m.asset_id:
                assets_by_id[a.asset_id] = a
                break

    per_asset = []
    for m in suite.members:
        asset = assets_by_id[m.asset_id]
        # execute_asset only needs a stable request identifier for Prediction provenance.
        per_asset.append(execute_asset(asset, request, deployment))

    # The run request is persisted: baseline scope authority (GAP-004 FINAL)
    # derives the population from this canonical record.
    from .run import save_request
    save_request(request, RUNS_DIR)

    # performance on the designated real asset (non-authoritative display)
    perf: dict[str, Any] = {"status": "NOT_EXECUTABLE"}
    if performance_asset_id and performance_asset_id in assets_by_id:
        pr = run_performance(deployment, run, assets_by_id[performance_asset_id],
                             samples=performance_samples)
        if pr is not None:
            perf = pr.to_json()
            perf["status"] = "VALID"
        else:
            perf = {"status": "INSUFFICIENT",
                    "note": "inference infrastructure unavailable for sampling"}

    # terminal result FIRST: it is the canonical owner of per-member states
    # and of the exact GroundTruth identity used for each member
    # (GAP-004 FINAL).
    outcomes = tuple(
        AssetEvaluationOutcome(
            asset_id=item["assetId"],
            kind=(
                AssetOutcomeKind.INFRASTRUCTURE_FAILURE
                if item.get("infrastructureError")
                else AssetOutcomeKind.SCORABLE
                if item.get("scored")
                else AssetOutcomeKind.INSUFFICIENT_EVIDENCE
            ),
            evidence_ref=(
                item.get("scoringProvenance", {}).get("predictionRequestId", "")
            ),
            reason=(
                item.get("infrastructureError", "")
                or ("" if item.get("scored") else "NOT_SCORABLE")
            ),
            gt_version=(
                item.get("scoringProvenance", {}).get("groundTruthVersion", "")
                if item.get("scored") else ""),
            gt_source=(
                item.get("scoringProvenance", {}).get("groundTruthSource", "")
                if item.get("scored") else ""),
        )
        for item in per_asset
    )
    result = EvaluationRunResult.create(request.request_id, outcomes)
    save_result(result, RUNS_DIR)

    # canonical baseline: population, classifications, counts, coverage,
    # sufficiency, task slices, safety section, and GT identity are all
    # DERIVED from persisted records (GAP-004 FINAL).
    report = BaselineReport.create(
        request_id=request.request_id,
        run_dir=RUNS_DIR,
        suite_dir=SUITES_DIR,
        prediction_dir=PREDICTIONS_DIR,
        ground_truth_dir=GT_DIR,
        asset_manifest_dir=MANIFESTS_DIR,
        performance=perf,
        created_at=created_at,
    )
    path = persist_baseline(report, BASELINES_DIR)
    return {
        "description": description,
        "runId": request.request_id,
        "resultId": result.result_id,
        "suiteId": suite.suite_id,
        "baselineId": report.baseline_id,
        "baselinePath": str(path),
        "perAsset": per_asset,
        "coverage": report.coverage,
        "evidenceSufficiency": report.evidence_sufficiency,
        "performance": perf,
    }


def main() -> int:
    from .seed import onboarding as run_onboarding
    onboarding = run_onboarding()
    print(f"onboarding: {len(onboarding['created'])} manifests/GT records created")

    deployment = DeploymentSnapshot.current_active()
    print(f"deployment: {deployment.model_name} {deployment.model_id[:16]}…")

    admitted = onboarding["admitted"]
    needs_gt = onboarding["needsGroundTruth"]
    suite_v1 = build_seed_suite(admitted, needs_gt)
    path = save_suite(suite_v1, SUITES_DIR)
    print(f"suite v1: {suite_v1.suite_id} → {path.name}")

    result = execute_baseline(suite_v1, deployment,
                              performance_asset_id=admitted[0] if admitted else None,
                              performance_samples=3)
    print(f"baseline v1: {result['baselineId']}")
    print(f"  evidence sufficiency: {result['evidenceSufficiency']['stance']}")
    print(f"  coverage: {result['coverage']['assetCount']} assets, "
          f"{result['coverage']['scoredAssetCount']} scored")
    print(json.dumps(result, ensure_ascii=False, indent=2)[:2000])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
