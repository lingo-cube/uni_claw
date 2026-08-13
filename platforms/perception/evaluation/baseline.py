"""Immutable baseline report (R21/R22/I32).

Frozen by Phase 4 gate (B16):
  • A persisted baseline is NEVER mutated.
  • New assets / GT / suite membership / evaluator revision produce a NEW
    baseline (new content-addressed id, new file).
  • Historical baselines remain reproducible.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .identity import canonical_hash, canonical_json
from .provenance_scorecard import ProvenanceBoundScorecard
from persistence import WriteOnceIntegrityError, write_once_json


@dataclass(frozen=True)
class BaselineReport:
    baseline_id: str
    baseline_schema_version: str
    deployment: dict[str, Any]
    suite_id: str
    evaluator_revision: str
    environment: dict[str, Any]
    asset_count: int
    scored_count: int
    unscored_count: int
    asset_classifications: tuple[dict[str, Any], ...]
    quality_scorecard: ProvenanceBoundScorecard
    safety_scorecard: dict[str, Any]
    performance: dict[str, Any]
    coverage: dict[str, Any]
    evidence_sufficiency: dict[str, Any]
    coverage_gaps: tuple[str, ...]
    ground_truth_gaps: tuple[str, ...]
    unassessed_categories: tuple[str, ...]
    holdout_status: str = "NONE"
    numeric_thresholds: str = "NOT_FROZEN"
    created_at: str = ""          # history metadata, not identity

    @classmethod
    def create(cls, *, deployment: dict[str, Any], suite_id: str,
               evaluator_revision: str, environment: dict[str, Any],
               asset_count: int, scored_count: int, unscored_count: int,
               asset_classifications: list[dict[str, Any]],
               request_id: str,
               deployment_hash: str,
               scoring_results: list[Any],
               prediction_dir: str | Path,
               ground_truth_dir: str | Path,
               classified: list[dict[str, Any]],
               declared_tasks: list[str],
               safety_scorecard: dict[str, Any],
               performance: dict[str, Any],
               coverage_gaps: list[str],
               ground_truth_gaps: list[str],
               unassessed_categories: list[str],
               holdout_status: str = "NONE",
               numeric_thresholds: str = "NOT_FROZEN",
               created_at: str = "") -> "BaselineReport":
        """GAP-004 record-minting closure: canonical quality evidence is
        DERIVED here from persisted records via verify_and_derive_scorecard.
        No scorecard object, no raw dict, no caller-declared quality
        numbers are accepted — a caller-created ProvenanceBoundScorecard
        is NOT an accepted input (RM-MET-01/02/03).  The only accepted record
        source is the canonical prediction and ground-truth directories;
        injected loaders and caller-supplied aggregation inputs are absent."""
        from .provenance_scorecard import verify_and_derive_scorecard

        quality_scorecard = verify_and_derive_scorecard(
            request_id=request_id,
            deployment_hash=deployment_hash,
            claimed_scoring_results=list(scoring_results),
            prediction_dir=prediction_dir,
            ground_truth_dir=ground_truth_dir,
            classified=classified,
            declared_tasks=declared_tasks,
        )
        coverage = quality_scorecard.coverage
        evidence_sufficiency = quality_scorecard.evidence_sufficiency
        body = {
            "baselineSchemaVersion": "1.0",
            "deployment": deployment,
            "suiteId": suite_id,
            "evaluatorRevision": evaluator_revision,
            "environment": environment,
            "assetCount": asset_count,
            "scoredCount": scored_count,
            "unscoredCount": unscored_count,
            "assetClassifications": asset_classifications,
            "qualityScorecard": quality_scorecard.to_json(),
            "safetyScorecard": safety_scorecard,
            "performance": performance,
            "coverage": coverage,
            "evidenceSufficiency": evidence_sufficiency,
            "coverageGaps": sorted(coverage_gaps),
            "groundTruthGaps": sorted(ground_truth_gaps),
            "unassessedCategories": sorted(unassessed_categories),
            "holdoutStatus": holdout_status,
            "numericThresholds": numeric_thresholds,
        }
        return cls(
            baseline_id=f"baseline:{canonical_hash(body)}",
            baseline_schema_version="1.0",
            deployment=deployment, suite_id=suite_id,
            evaluator_revision=evaluator_revision, environment=environment,
            asset_count=asset_count, scored_count=scored_count,
            unscored_count=unscored_count,
            asset_classifications=tuple(asset_classifications),
            quality_scorecard=quality_scorecard,
            safety_scorecard=safety_scorecard, performance=performance,
            coverage=coverage, evidence_sufficiency=evidence_sufficiency,
            coverage_gaps=tuple(coverage_gaps),
            ground_truth_gaps=tuple(ground_truth_gaps),
            unassessed_categories=tuple(unassessed_categories),
            holdout_status=holdout_status, numeric_thresholds=numeric_thresholds,
            created_at=created_at,
        )

    def to_json(self) -> dict[str, Any]:
        return {
            "baselineId": self.baseline_id,
            "baselineSchemaVersion": self.baseline_schema_version,
            "deployment": self.deployment,
            "suiteId": self.suite_id,
            "evaluatorRevision": self.evaluator_revision,
            "environment": self.environment,
            "assetCount": self.asset_count,
            "scoredCount": self.scored_count,
            "unscoredCount": self.unscored_count,
            "assetClassifications": list(self.asset_classifications),
            "qualityScorecard": self.quality_scorecard.to_json(),
            "safetyScorecard": self.safety_scorecard,
            "performance": self.performance,
            "coverage": self.coverage,
            "evidenceSufficiency": self.evidence_sufficiency,
            "coverageGaps": list(self.coverage_gaps),
            "groundTruthGaps": list(self.ground_truth_gaps),
            "unassessedCategories": list(self.unassessed_categories),
            "holdoutStatus": self.holdout_status,
            "numericThresholds": self.numeric_thresholds,
            "createdAt": self.created_at,
        }


class BaselineImmutabilityError(WriteOnceIntegrityError):
    """Raised when persisting would overwrite an existing baseline (B16)."""


def persist_baseline(report: BaselineReport, out_dir: str | Path) -> Path:
    """Write-once persistence. Existing baseline file → immutability error."""
    out = Path(out_dir)
    path = out / f"{report.baseline_id.replace('baseline:', '')}.json"
    try:
        return write_once_json(path, report.to_json())
    except WriteOnceIntegrityError as error:
        raise BaselineImmutabilityError(str(error)) from error
