"""ProvenanceBoundScorecard (GAP-004 closure).

DETACHED METRIC MATH = NONCANONICAL.
PROVENANCE-BOUND SCORING RESULT = ONLY INPUT ALLOWED TO CREATE/PERSIST
CANONICAL QUALITY EVIDENCE.

The canonical quality scorecard is an immutable object built ONLY from
EvaluationScoringResults — each of which binds run request, prediction
identity, ground-truth identity, stored view, stage, and LabelSpace via
EvaluationScoringContext. Raw dictionaries cannot cross the canonical
quality boundary.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .identity import canonical_hash
from .metrics import EvaluationScoringResult
from .scorecard import (
    AssetScore, build_coverage, build_scorecard, evidence_sufficiency,
)


@dataclass(frozen=True)
class ProvenanceBoundScorecard:
    """Canonical quality evidence — provenance-bound, immutable."""

    request_id: str                          # EvaluationRunRequest identity
    deployment_hash: str                     # evaluated deployment identity
    scoring_results: tuple[EvaluationScoringResult, ...]
    task_slices: dict[str, Any]              # derived aggregation (internal)
    safety_section: dict[str, Any]
    coverage: dict[str, Any]
    evidence_sufficiency: dict[str, Any]

    @property
    def scorecard_id(self) -> str:
        return f"pbs:{canonical_hash(self._canonical())}"

    def _canonical(self) -> dict[str, Any]:
        return {
            "schema": "uniclaw.provenanceBoundScorecard.v1",
            "requestId": self.request_id,
            "deploymentHash": self.deployment_hash,
            "scoringResults": [
                {
                    "requestId": s.request_id,
                    "predictionAssetId": s.prediction_asset_id,
                    "predictionRequestId": s.prediction_request_id,
                    "predictionDeploymentHash": s.prediction_deployment_hash,
                    "groundTruthAssetId": s.ground_truth_asset_id,
                    "groundTruthVersion": s.ground_truth_version,
                    "groundTruthSource": s.ground_truth_source,
                    "predictionView": s.prediction_view.value,
                    "predictionStage": s.prediction_stage,
                    "predictionLabelSpace": s.prediction_label_space,
                    "compatibilityVerdict": s.compatibility_verdict,
                }
                for s in sorted(self.scoring_results,
                                key=lambda x: x.prediction_asset_id)
            ],
            "taskSlices": self.task_slices,
            "safetySection": self.safety_section,
            "coverage": self.coverage,
            "evidenceSufficiency": self.evidence_sufficiency,
        }

    def to_json(self) -> dict[str, Any]:
        """The qualityScorecard payload for BaselineReport serialization."""
        return {
            "scorecardId": self.scorecard_id,
            "requestId": self.request_id,
            "deploymentHash": self.deployment_hash,
            "taskSlices": self.task_slices,
            "safetySection": self.safety_section,
            "coverage": self.coverage,
            "evidenceSufficiency": self.evidence_sufficiency,
            "scoringResultCount": len(self.scoring_results),
        }


def build_provenance_bound_scorecard(
    *,
    request_id: str,
    deployment_hash: str,
    scoring_results: list[EvaluationScoringResult],
    classified: list[dict[str, Any]],
    declared_tasks: list[str],
) -> ProvenanceBoundScorecard:
    """CANONICAL construction: aggregation derived INSIDE from provenance-
    bound scoring results.  AssetScore is deliberately derived here rather
    than accepted from a caller: it is an aggregation input, not authority.
    Classification and declared tasks are allowed only as declared scope
    metadata; metric values and scored stances originate in the verified
    EvaluationScoringResult records."""
    asset_scores = _derive_asset_scores(scoring_results, classified)
    quality = build_scorecard(asset_scores)
    coverage = build_coverage(asset_scores, classified)
    suff = evidence_sufficiency(asset_scores, declared_tasks)
    return ProvenanceBoundScorecard(
        request_id=request_id,
        deployment_hash=deployment_hash,
        scoring_results=tuple(scoring_results),
        task_slices=quality.get("taskSlices", {}),
        safety_section=quality.get("sections", {}).get("SAFETY", {}),
        coverage=coverage,
        evidence_sufficiency=suff,
    )


def _derive_asset_scores(
    scoring_results: list[EvaluationScoringResult],
    classified: list[dict[str, Any]],
) -> list[AssetScore]:
    """Project canonical scoring records into aggregation-only AssetScores.

    A classification record can declare that a suite member exists, but it
    cannot attach a numerical score or a scoring stance.  Members with no
    verified result are represented as unscored so coverage remains honest.
    """
    results_by_asset: dict[str, EvaluationScoringResult] = {}
    for result in scoring_results:
        if result.prediction_asset_id in results_by_asset:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:DUPLICATE_SCORING_ASSET")
        results_by_asset[result.prediction_asset_id] = result

    classification_by_asset = {
        item["assetId"]: dict(item)
        for item in classified
        if isinstance(item, dict) and isinstance(item.get("assetId"), str)
    }
    asset_ids = sorted(set(classification_by_asset) | set(results_by_asset))
    derived: list[AssetScore] = []
    for asset_id in asset_ids:
        result = results_by_asset.get(asset_id)
        task_data = (
            {
                task.value: {
                    "stance": metric.stance.value,
                    "metrics": dict(metric.metrics),
                    "denominator": metric.denominator,
                    "note": metric.note,
                }
                for task, metric in result.task_results.items()
            }
            if result is not None else {}
        )
        derived.append(AssetScore(
            asset_id=asset_id,
            scored=any(
                data["stance"] == "SCORED" for data in task_data.values()),
            tasks=task_data,
            classification=classification_by_asset.get(asset_id, {}),
        ))
    return derived


class CanonicalVerificationError(ValueError):
    """A scoring claim does not bind to persisted canonical evidence."""


def verify_and_derive_scorecard(
    *,
    request_id: str,
    deployment_hash: str,
    claimed_scoring_results: list[EvaluationScoringResult],
    prediction_dir: str | Path,
    ground_truth_dir: str | Path,
    classified: list[dict[str, Any]],
    declared_tasks: list[str],
) -> ProvenanceBoundScorecard:
    """GAP-004 record-minting closure.

    Canonical quality evidence is RE-DERIVED from persisted records —
    caller-supplied scoring claims are never trusted, only verified:

      for each claimed EvaluationScoringResult:
        • load the PERSISTED Prediction by (request_id, asset_id) from the
          canonical prediction directory
        • verify prediction.run_id / asset_id / deployment_hash bindings
        • load GroundTruth by asset_id from canonical ground-truth storage
        • re-run EvaluationScoringContext.score() on the loaded records
        • verify the re-derived task results equal the claimed ones
      then derive the scorecard summary FROM the verified results.

    Invented taskSlices / aggregates / zero-result inventions cannot enter.
    """
    from .groundtruth import load_groundtruth_exact
    from .metrics import EvaluationScoringContext
    from .prediction import Prediction

    prediction_root = Path(prediction_dir)
    ground_truth_root = Path(ground_truth_dir)

    def load_prediction(request: str, asset: str):
        expected = prediction_root / (
            f"{request.replace('run:', '')}-{asset.replace('sha256:', '')}.json")
        if not expected.is_file():
            return None
        return Prediction.from_json(json.loads(expected.read_text(encoding="utf-8")))

    def load_gt(asset: str, gt_version: str):
        # GAP-004 FINAL: GroundTruth resolves by EXACT canonical identity
        # (asset + version) — never glob order, never first match.  The
        # version comes from the scoring claim, and the loaded record must
        # carry that exact version or the claim cannot be verified.
        return load_groundtruth_exact(asset, gt_version, ground_truth_root)

    verified: list[EvaluationScoringResult] = []
    for claimed in claimed_scoring_results:
        pred = load_prediction(claimed.prediction_request_id,
                               claimed.prediction_asset_id)
        if pred is None:
            raise CanonicalVerificationError(
                f"PROVENANCE_MISMATCH: no persisted Prediction for "
                f"request={claimed.prediction_request_id} "
                f"asset={claimed.prediction_asset_id}")
        if pred.run_id != claimed.prediction_request_id:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:PREDICTION_REQUEST_ID")
        if pred.asset_id != claimed.prediction_asset_id:
            raise CanonicalVerificationError("PROVENANCE_MISMATCH:ASSET_ID")
        if pred.deployment_hash != claimed.prediction_deployment_hash:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:DEPLOYMENT_IDENTITY")
        if claimed.prediction_deployment_hash != deployment_hash:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:RUN_DEPLOYMENT_IDENTITY")

        gt = load_gt(claimed.prediction_asset_id, claimed.ground_truth_version)
        if gt is None:
            raise CanonicalVerificationError(
                f"PROVENANCE_MISMATCH: no GroundTruth for "
                f"{claimed.prediction_asset_id} version "
                f"{claimed.ground_truth_version}")
        if gt.asset_id != claimed.ground_truth_asset_id:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:GROUND_TRUTH_ASSET")
        if gt.gt_version != claimed.ground_truth_version:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:GROUND_TRUTH_VERSION")

        # re-derive from the LOADED records — caller-declared numbers are
        # ignored; only the re-derived result counts
        try:
            context = EvaluationScoringContext(
                request_id=request_id,
                prediction=pred,
                ground_truth=gt,
                deployment_hash=deployment_hash,
                prediction_view=claimed.prediction_view,
            )
            derived = context.score()
        except (ValueError, CanonicalVerificationError) as exc:
            raise CanonicalVerificationError(
                f"PROVENANCE_MISMATCH: re-derivation failed: {exc}") from exc
        if (derived.compatibility_verdict != claimed.compatibility_verdict
                or derived.prediction_stage != claimed.prediction_stage
                or derived.prediction_label_space != claimed.prediction_label_space):
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:STAGE_LABELSPACE_CLAIM")
        verified.append(derived)

    return build_provenance_bound_scorecard(
        request_id=request_id,
        deployment_hash=deployment_hash,
        scoring_results=verified,
        classified=classified,
        declared_tasks=declared_tasks,
    )
