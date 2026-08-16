"""Immutable baseline report (R21/R22/I32).

Frozen by Phase 4 gate (B16):
  • A persisted baseline is NEVER mutated.
  • New assets / GT / suite membership / evaluator revision produce a NEW
    baseline (new content-addressed id, new file).
  • Historical baselines remain reproducible.

GAP-004 FINAL (scope authority):
  • BaselineReport.create derives its ENTIRE authoritative population from
    persisted canonical records:
      EvaluationRunRequest → EvaluationSuite membership → terminal
      EvaluationRunResult → per-member Prediction / failure / insufficiency
      → exact GroundTruth identity (version + source) → re-scoring →
      derived classified / declared tasks / counts / coverage / evidence
      sufficiency / task slices / safety section.
  • No caller input selects or shrinks the population.  The only
    non-authoritative caller inputs are display metadata: performance,
    numeric_thresholds, created_at.
  • persist_baseline verifies the report identity derives from its content
    and that the report is internally consistent before writing.

GAP-004 FINAL-PERSIST (persistence authority):
  • BaselineReport.create stamps an internal canonical derivation proof
    (derivation_receipt_id + derivation_context) bound to the PERSISTED
    canonical evidence it actually loaded (EvaluationRunRequest →
    EvaluationSuite → terminal EvaluationRunResult → scorecard) and the
    exact derivation inputs.  Public dataclass construction can never
    obtain this proof.
  • persist_baseline refuses ANY report that does not carry the proof and
    is not byte-identical to a FRESH re-derivation from the persisted
    canonical evidence referenced by the proof.  Fabricated counts,
    fabricated safety scorecards, fabricated sufficiency, copied proofs on
    mutated bodies, fake derivation receipts, and deleted source evidence
    all fail closed.  Type validity never equals semantic authority.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .identity import canonical_hash
from .provenance_scorecard import (
    CanonicalVerificationError, ProvenanceBoundScorecard,
    build_provenance_bound_scorecard,
)
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
    # ── GAP-004 FINAL-PERSIST: canonical derivation proof (internal) ──
    # Stamped ONLY by BaselineReport.create.  The receipt binds the
    # persisted canonical evidence content + the exact derivation inputs;
    # the context records the canonical storage locations + inputs needed
    # for persist-time re-derivation.  Public construction yields the
    # default empty proof → persist_baseline refuses (NON_AUTHORITATIVE).
    derivation_receipt_id: str = ""
    derivation_context: dict[str, Any] = field(default_factory=dict)

    @classmethod
    def create(cls, *, request_id: str,
               run_dir: str | Path,
               suite_dir: str | Path,
               prediction_dir: str | Path,
               ground_truth_dir: str | Path,
               asset_manifest_dir: str | Path,
               performance: dict[str, Any] | None = None,
               numeric_thresholds: str = "NOT_FROZEN",
               created_at: str = "") -> "BaselineReport":
        """GAP-004 FINAL: canonical scope authority.

        The evaluation population, classifications, declared tasks, counts,
        coverage, evidence sufficiency, task slices, safety section, and
        the exact GroundTruth identity per member are all DERIVED from
        persisted canonical records.  Caller-supplied scoring claims,
        classified lists, declared task lists, counts, coverage dicts, and
        safety scorecards are NOT accepted — the only caller inputs are
        canonical storage locations and non-authoritative display metadata
        (performance / numeric_thresholds / created_at).
        """
        from .asset import load_asset_manifest
        from .groundtruth import load_groundtruth_exact
        from .metrics import EvaluationScoringContext, PredictionView
        from .prediction import Prediction
        from .run import (
            AssetOutcomeKind, EvaluationRunRequest, load_request,
            load_terminal_result,
        )
        from .suite import load_suite_by_id

        # ── canonical records: request → suite → terminal result ──
        request = load_request(request_id, run_dir)
        if request is None:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH: no persisted canonical "
                f"EvaluationRunRequest for request={request_id}")
        suite = load_suite_by_id(request.suite_id, suite_dir)
        if suite is None:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH: no persisted canonical EvaluationSuite "
                f"for suite={request.suite_id}")
        result = load_terminal_result(request.request_id, run_dir)
        if result is None:
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH: no persisted terminal "
                f"EvaluationRunResult for request={request_id}")

        # ── authoritative population (caller cannot shrink it) ──
        population = (
            tuple(request.asset_scope) if request.asset_scope
            else tuple(m.asset_id for m in suite.members))
        if len(set(population)) != len(population):
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:AMBIGUOUS_POPULATION — duplicate "
                "asset ids in requested membership")
        outcomes = {o.asset_id: o for o in result.asset_outcomes}
        if len(outcomes) != len(result.asset_outcomes):
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:AMBIGUOUS_RESULT — duplicate asset "
                "outcomes in terminal result")
        out_of_scope = [o for o in result.asset_outcomes
                        if o.asset_id not in set(population)]
        if out_of_scope:
            # GAP-004 FINAL: out-of-suite / out-of-scope canonical outcomes
            # are REJECTED — nothing outside the requested population can
            # enter baseline accounting.
            raise CanonicalVerificationError(
                "PROVENANCE_MISMATCH:OUT_OF_SCOPE_OUTCOME — terminal result "
                "contains outcomes outside the canonical requested "
                "population: " + ", ".join(o.asset_id for o in out_of_scope))

        prediction_root = Path(prediction_dir)
        gt_root = Path(ground_truth_dir)
        manifest_root = Path(asset_manifest_dir)

        def load_prediction(asset: str) -> Prediction | None:
            expected = prediction_root / (
                f"{request_id.replace('run:', '')}"
                f"-{asset.replace('sha256:', '')}.json")
            if not expected.is_file():
                return None
            try:
                record = json.loads(expected.read_text(encoding="utf-8"))
                pred = Prediction.from_json(record)
            except (OSError, ValueError, TypeError):
                return None
            if pred.run_id != request_id or pred.asset_id != asset:
                return None
            return pred

        # ── per-member derivation: SCORED / UNSCORABLE / FAILED / MISSING ──
        derived_results = []
        member_states: dict[str, str] = {}
        for asset in population:
            outcome = outcomes.get(asset)
            if outcome is None:
                member_states[asset] = "MISSING"
                continue
            if outcome.kind == AssetOutcomeKind.INFRASTRUCTURE_FAILURE:
                member_states[asset] = "INFRASTRUCTURE_FAILURE"
                continue
            pred = load_prediction(asset)
            if pred is None:
                # Terminal result vs persisted Prediction population
                # disagreement: represent truthfully, never normalize.
                member_states[asset] = (
                    "INSUFFICIENT_EVIDENCE"
                    if outcome.kind == AssetOutcomeKind.INSUFFICIENT_EVIDENCE
                    else "MISSING")
                continue
            if outcome.kind == AssetOutcomeKind.INSUFFICIENT_EVIDENCE:
                member_states[asset] = "INSUFFICIENT_EVIDENCE"
                continue
            if not outcome.gt_version:
                member_states[asset] = "UNSCORABLE"
                continue
            gt = load_groundtruth_exact(asset, outcome.gt_version, gt_root)
            if gt is None:
                member_states[asset] = "UNSCORABLE"
                continue
            if outcome.gt_source and gt.source != outcome.gt_source:
                raise CanonicalVerificationError(
                    "PROVENANCE_MISMATCH:GROUND_TRUTH_SOURCE — terminal "
                    f"result recorded source {outcome.gt_source} but exact "
                    f"GT record for {asset} declares {gt.source}")
            try:
                derived = EvaluationScoringContext(
                    request_id=request.request_id,
                    prediction=pred,
                    ground_truth=gt,
                    deployment_hash=request.deployment.identity_hash,
                    prediction_view=PredictionView.FUSED_EVIDENCE,
                ).score()
            except (ValueError, CanonicalVerificationError) as exc:
                member_states[asset] = "UNSCORABLE"
                continue
            derived_results.append(derived)
            member_states[asset] = (
                "SCORED"
                if any(m.stance.value == "SCORED"
                       for m in derived.task_results.values())
                else "UNSCORABLE")

        # ── classifications from canonical asset manifests ──
        classified: list[dict[str, Any]] = []
        for asset in population:
            manifest_path = manifest_root / (
                f"{asset.replace('sha256:', '')}.json")
            try:
                manifest = load_asset_manifest(manifest_path)
                if manifest.asset_id != asset:
                    manifest = None
            except (OSError, ValueError, TypeError):
                manifest = None
            if manifest is None:
                classified.append({
                    "assetId": asset, "systemFamily": "UNKNOWN",
                    "perceptionTask": "", "componentClass": "UNKNOWN",
                    "corpusRole": "", "criticality": "UNKNOWN"})
            else:
                classified.append({
                    "assetId": asset,
                    "systemFamily": manifest.system_family.value,
                    "perceptionTask": ",".join(
                        t.value for t in manifest.perception_tasks),
                    "componentClass": manifest.component_class.value,
                    "corpusRole": ",".join(
                        r.value for r in manifest.corpus_roles),
                    "criticality": manifest.criticality.value})

        declared_tasks = [t.value for t in suite.required_tasks]

        quality_scorecard = build_provenance_bound_scorecard(
            request_id=request.request_id,
            deployment_hash=request.deployment.identity_hash,
            scoring_results=derived_results,
            classified=classified,
            declared_tasks=declared_tasks,
        )
        coverage = quality_scorecard.coverage
        evidence_sufficiency = quality_scorecard.evidence_sufficiency

        scored_count = sum(1 for state in member_states.values()
                           if state == "SCORED")
        unscored_count = len(population) - scored_count
        coverage_gaps = [
            f"member {asset}: {state}"
            for asset, state in member_states.items() if state != "SCORED"]
        ground_truth_gaps = [
            f"member {asset}: no canonical GroundTruth identity"
            for asset, state in member_states.items()
            if state in ("UNSCORABLE", "MISSING")]
        unassessed_categories = [
            f"{dim}:{value}"
            for dim in ("systemFamily", "componentClass")
            for value, data in coverage[
                f"{dim}Coverage"].items()
            if data["total"] == 0]

        # ── GAP-004 FINAL-PERSIST: canonical derivation proof ──
        # Bound to the PERSISTED canonical evidence actually loaded for
        # this derivation + the exact derivation inputs.  persist_baseline
        # re-derives the report from this evidence and requires byte
        # equality — a proof can be obtained for the true canonical
        # derivation only, never for a fabricated report.
        derivation_receipt_id = "baseline-derivation:" + canonical_hash({
            "request": request.to_json(),
            "suite": suite.to_json(),
            "result": result.to_json(),
            "scorecard": quality_scorecard.to_json(),
            "inputs": {
                "performance": performance,
                "numericThresholds": numeric_thresholds,
            },
        })
        derivation_context = {
            "requestId": request.request_id,
            "runDir": str(run_dir),
            "suiteDir": str(suite_dir),
            "predictionDir": str(prediction_dir),
            "groundTruthDir": str(ground_truth_dir),
            "assetManifestDir": str(asset_manifest_dir),
            "performance": performance,
            "numericThresholds": numeric_thresholds,
            "createdAt": created_at,
        }

        body = {
            "baselineSchemaVersion": "1.0",
            "deployment": request.deployment.to_json(),
            "suiteId": request.suite_id,
            "evaluatorRevision": request.evaluator_revision,
            "environment": request.environment.to_json(),
            "assetCount": len(population),
            "scoredCount": scored_count,
            "unscoredCount": unscored_count,
            "assetClassifications": classified,
            "qualityScorecard": quality_scorecard.to_json(),
            "safetyScorecard": quality_scorecard.safety_section,
            "performance": performance
                if performance is not None
                else {"status": "NOT_EXECUTABLE"},
            "coverage": coverage,
            "evidenceSufficiency": evidence_sufficiency,
            "coverageGaps": sorted(coverage_gaps),
            "groundTruthGaps": sorted(ground_truth_gaps),
            "unassessedCategories": sorted(unassessed_categories),
            "holdoutStatus": "NONE",
            "numericThresholds": numeric_thresholds,
            "derivationReceiptId": derivation_receipt_id,
        }
        return cls(
            baseline_id=f"baseline:{canonical_hash(body)}",
            baseline_schema_version="1.0",
            deployment=request.deployment.to_json(),
            suite_id=request.suite_id,
            evaluator_revision=request.evaluator_revision,
            environment=request.environment.to_json(),
            asset_count=len(population),
            scored_count=scored_count,
            unscored_count=unscored_count,
            asset_classifications=tuple(classified),
            quality_scorecard=quality_scorecard,
            safety_scorecard=quality_scorecard.safety_section,
            performance=body["performance"],
            coverage=coverage,
            evidence_sufficiency=evidence_sufficiency,
            coverage_gaps=tuple(sorted(coverage_gaps)),
            ground_truth_gaps=tuple(sorted(ground_truth_gaps)),
            unassessed_categories=tuple(sorted(unassessed_categories)),
            holdout_status="NONE",
            numeric_thresholds=numeric_thresholds,
            created_at=created_at,
            derivation_receipt_id=derivation_receipt_id,
            derivation_context=derivation_context,
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
            "derivationReceiptId": self.derivation_receipt_id,
            "createdAt": self.created_at,
        }


class BaselineImmutabilityError(WriteOnceIntegrityError):
    """Raised when persisting would overwrite an existing baseline (B16)."""


def persist_baseline(report: BaselineReport, out_dir: str | Path) -> Path:
    """Write-once persistence with identity + internal-consistency
    verification (GAP-004 FINAL-MET-12): the persisted record's identity
    must derive from its own content and its declared counts must match
    its derived scorecard — a caller-chosen id or a caller-minted
    inconsistent report is refused.

    GAP-004 FINAL-PERSIST (persistence authority): a BaselineReport is
    persisted ONLY when it carries a canonical derivation proof stamped by
    BaselineReport.create AND is byte-identical to a FRESH re-derivation
    from the persisted canonical evidence it references (request → suite →
    terminal result).  Public dataclass construction, missing / fake
    derivation receipts, copied proofs on mutated bodies, fabricated
    counts / safety scorecard / sufficiency / coverage, and deleted source
    evidence all fail closed — type validity never equals semantic
    authority."""
    # ── GAP-004 FINAL-PERSIST: canonical derivation authority gate ──
    ctx = report.derivation_context or {}
    if not report.derivation_receipt_id or not ctx:
        raise BaselineImmutabilityError(
            "NON_AUTHORITATIVE_BASELINE: report carries no canonical "
            "derivation proof — public BaselineReport construction is not "
            "authoritative; only BaselineReport.create may mint baselines")
    request_id = str(ctx.get("requestId") or "")
    if not request_id or report.quality_scorecard.request_id != request_id:
        raise BaselineImmutabilityError(
            "DERIVATION_RECEIPT_MISMATCH: report scorecard request id does "
            "not match its derivation context")
    required = ("runDir", "suiteDir", "predictionDir", "groundTruthDir",
                "assetManifestDir")
    if any(not ctx.get(k) for k in required):
        raise BaselineImmutabilityError(
            "DERIVATION_RECEIPT_MISMATCH: incomplete canonical derivation "
            "context")
    from .run import load_request, load_terminal_result
    from .suite import load_suite_by_id
    request = load_request(request_id, ctx["runDir"])
    if request is None:
        raise BaselineImmutabilityError(
            "CANONICAL_EVIDENCE_UNAVAILABLE: no persisted canonical "
            f"EvaluationRunRequest for {request_id} at derivation time")
    if load_suite_by_id(request.suite_id, ctx["suiteDir"]) is None:
        raise BaselineImmutabilityError(
            "CANONICAL_EVIDENCE_UNAVAILABLE: no persisted canonical "
            f"EvaluationSuite for {request.suite_id} at derivation time")
    if load_terminal_result(request_id, ctx["runDir"]) is None:
        raise BaselineImmutabilityError(
            "CANONICAL_EVIDENCE_UNAVAILABLE: no persisted terminal "
            f"EvaluationRunResult for {request_id} at derivation time")
    try:
        derived = BaselineReport.create(
            request_id=request_id,
            run_dir=ctx["runDir"], suite_dir=ctx["suiteDir"],
            prediction_dir=ctx["predictionDir"],
            ground_truth_dir=ctx["groundTruthDir"],
            asset_manifest_dir=ctx["assetManifestDir"],
            performance=ctx.get("performance"),
            numeric_thresholds=ctx.get("numericThresholds", "NOT_FROZEN"),
            created_at=ctx.get("createdAt", ""))
    except CanonicalVerificationError as error:
        raise BaselineImmutabilityError(
            "CANONICAL_EVIDENCE_UNAVAILABLE: canonical re-derivation "
            f"failed: {error}") from error
    if derived.derivation_receipt_id != report.derivation_receipt_id:
        raise BaselineImmutabilityError(
            "FAKE_DERIVATION_RECEIPT: report proof does not match the "
            "recomputed canonical derivation receipt")
    if derived.to_json() != report.to_json():
        raise BaselineImmutabilityError(
            "DERIVED_REPORT_MISMATCH: report body diverges from the fresh "
            "canonical re-derivation (mutated counts / safety scorecard / "
            "sufficiency / coverage / copied proof on changed fields)")

    # ── content identity + internal consistency (defense in depth) ──
    body = report.to_json()
    # createdAt is history metadata only (not identity, per the dataclass
    # contract) — the canonical identity covers the authoritative content.
    identity_body = {k: v for k, v in body.items()
                     if k not in ("baselineId", "createdAt")}
    expected_id = f"baseline:{canonical_hash(identity_body)}"
    if (body.get("baselineId") != report.baseline_id
            or report.baseline_id != expected_id):
        raise BaselineImmutabilityError(
            "baseline identity does not derive from its content")
    if (report.asset_count != report.coverage.get("assetCount")
            or report.scored_count != report.coverage.get("scoredAssetCount")
            or report.unscored_count != report.coverage.get("unscoredAssetCount")):
        raise BaselineImmutabilityError(
            "baseline counts are inconsistent with derived coverage")
    if (body["qualityScorecard"] != report.quality_scorecard.to_json()
            or body["coverage"] != report.coverage
            or body["evidenceSufficiency"] != report.evidence_sufficiency
            or body["safetyScorecard"] != report.safety_scorecard):
        raise BaselineImmutabilityError(
            "baseline body disagrees with its derived scorecard")
    out = Path(out_dir)
    path = out / f"{report.baseline_id.replace('baseline:', '')}.json"
    try:
        return write_once_json(path, body)
    except WriteOnceIntegrityError as error:
        raise BaselineImmutabilityError(str(error)) from error
