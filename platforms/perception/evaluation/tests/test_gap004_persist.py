"""GAP004-FINAL-PERSIST-01..08 — persistence-boundary authority falsifiers
(GAP-004 FINAL-PERSIST).

persist_baseline must refuse every caller-minted BaselineReport (public
dataclass construction, fabricated counts / safety scorecard / sufficiency,
fake derivation receipts, copied proofs on mutated bodies, deleted source
evidence) and accept only reports that are byte-identical to a fresh
canonical re-derivation from persisted evidence.
"""
from __future__ import annotations

import dataclasses
import json
import tempfile
import unittest
from pathlib import Path

from evaluation.baseline import (
    BaselineImmutabilityError, BaselineReport, persist_baseline,
)
from evaluation.identity import canonical_hash
from evaluation.provenance_scorecard import ProvenanceBoundScorecard
from evaluation.tests.chain_helpers import (
    Chain, asset_id, persist_scored_member,
)


class Gap004PersistTests(unittest.TestCase):
    """Fresh falsifiers at the persistence boundary (GAP-004 FINAL-PERSIST)."""

    # ── canonical fixtures ────────────────────────────────────────────────
    def _chain(self, *, members: tuple[int, ...] = (1, 2),
               gt_version: str = "1"):
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite(tuple(asset_id(i) for i in members))
        request = chain.build_request(suite)
        for i in members:
            persist_scored_member(chain, asset_id(i), request, gt_version)
        outcomes = tuple(chain.scorable(asset_id(i), gt_version)
                         for i in members)
        chain.build_result(request, outcomes)
        return chain, request

    def _canonical(self, chain: Chain, request) -> BaselineReport:
        return BaselineReport.create(
            request_id=request.request_id,
            run_dir=chain.run_dir, suite_dir=chain.suite_dir,
            prediction_dir=chain.prediction_dir,
            ground_truth_dir=chain.gt_dir,
            asset_manifest_dir=chain.manifest_dir)

    def _public_forged(self, chain: Chain, request,
                       **overrides) -> BaselineReport:
        """PUBLIC BaselineReport(...) construction — no derivation proof.
        Self-consistent by construction (identity recomputed over the
        forged content), exactly like the auditor's forged report."""
        canonical = self._canonical(chain, request)
        base = {f.name: getattr(canonical, f.name)
                for f in dataclasses.fields(BaselineReport)}
        base.update({"derivation_receipt_id": "", "derivation_context": {}})
        base.update(overrides)
        forged = BaselineReport(**base)
        # keep the forged record self-consistent: identity from its content
        body = forged.to_json()
        identity_body = {k: v for k, v in body.items()
                         if k not in ("baselineId", "createdAt")}
        return dataclasses.replace(
            forged, baseline_id=f"baseline:{canonical_hash(identity_body)}")

    def _persist_rejects(self, report: BaselineReport, prefix: str,
                         tmp: Path) -> None:
        with self.assertRaises(BaselineImmutabilityError) as cm:
            persist_baseline(report, tmp)
        self.assertIn(prefix, str(cm.exception))

    # ── GAP004-FINAL-PERSIST-01: public constructor → FAIL ───────────────
    def test_GAP004_FINAL_PERSIST_01_public_constructor_rejected(self):
        chain, request = self._chain()
        with tempfile.TemporaryDirectory() as tmp:
            # (a) copied canonical fields via public construction, NO proof
            forged = self._public_forged(chain, request)
            self._persist_rejects(forged, "NON_AUTHORITATIVE_BASELINE",
                                  Path(tmp))
            # (b) the auditor's exact attack: fabricated counts + forged
            # safety scorecard + SUFFICIENT sufficiency + zero scoring
            # results, all self-consistent — still refused (no proof).
            forged_safety = {"forged": True, "visible": True}
            forged_sufficiency = {
                "stance": "SUFFICIENT", "scoredAssets": 7, "population": 10}
            forged_coverage = {
                "assetCount": 7, "scoredAssetCount": 7,
                "unscoredAssetCount": 3}
            forged_scorecard = ProvenanceBoundScorecard(
                request_id="run:FORGED", deployment_hash="deploy:FORGED",
                scoring_results=(),
                task_slices={"ELEMENT_DETECTION": {"status": "ASSESSED"}},
                safety_section=forged_safety,
                coverage=forged_coverage,
                evidence_sufficiency=forged_sufficiency)
            audit_forged = self._public_forged(
                chain, request,
                asset_count=7, scored_count=7, unscored_count=3,
                safety_scorecard=forged_safety,
                evidence_sufficiency=forged_sufficiency,
                coverage=forged_coverage,
                quality_scorecard=forged_scorecard)
            self._persist_rejects(audit_forged, "NON_AUTHORITATIVE_BASELINE",
                                  Path(tmp))

    # ── GAP004-FINAL-PERSIST-02: modified assetCount → FAIL ──────────────
    def test_GAP004_FINAL_PERSIST_02_modified_asset_count_rejected(self):
        chain, request = self._chain()
        canonical = self._canonical(chain, request)
        mutated = dataclasses.replace(canonical, asset_count=99)
        with tempfile.TemporaryDirectory() as tmp:
            self._persist_rejects(mutated, "DERIVED_REPORT_MISMATCH",
                                  Path(tmp))

    # ── GAP004-FINAL-PERSIST-03: modified safetyScorecard → FAIL ─────────
    def test_GAP004_FINAL_PERSIST_03_modified_safety_scorecard_rejected(self):
        chain, request = self._chain()
        canonical = self._canonical(chain, request)
        mutated = dataclasses.replace(
            canonical, safety_scorecard={"forged": True, "visible": True})
        with tempfile.TemporaryDirectory() as tmp:
            self._persist_rejects(mutated, "DERIVED_REPORT_MISMATCH",
                                  Path(tmp))

    # ── GAP004-FINAL-PERSIST-04: valid proof, different body → FAIL ──────
    def test_GAP004_FINAL_PERSIST_04_copied_proof_changed_fields_rejected(self):
        chain, request = self._chain()
        canonical = self._canonical(chain, request)
        # copied proof (receipt + context kept) but sufficiency rewritten
        mutated = dataclasses.replace(
            canonical,
            evidence_sufficiency={
                "stance": "SUFFICIENT", "scoredAssets": 2, "population": 2})
        with tempfile.TemporaryDirectory() as tmp:
            self._persist_rejects(mutated, "DERIVED_REPORT_MISMATCH",
                                  Path(tmp))
        # copied proof but a different coverage body
        mutated2 = dataclasses.replace(
            canonical, coverage={**canonical.coverage, "assetCount": 9})
        with tempfile.TemporaryDirectory() as tmp:
            self._persist_rejects(mutated2, "DERIVED_REPORT_MISMATCH",
                                  Path(tmp))

    # ── GAP004-FINAL-PERSIST-05: fake derivation id → FAIL ───────────────
    def test_GAP004_FINAL_PERSIST_05_fake_derivation_receipt_rejected(self):
        chain, request = self._chain()
        canonical = self._canonical(chain, request)
        fake = dataclasses.replace(
            canonical, derivation_receipt_id="baseline-derivation:deadbeef")
        with tempfile.TemporaryDirectory() as tmp:
            self._persist_rejects(fake, "FAKE_DERIVATION_RECEIPT", Path(tmp))
        # fake context request id
        fake2 = dataclasses.replace(
            canonical, derivation_context={
                **canonical.derivation_context, "requestId": "request:fake"})
        with tempfile.TemporaryDirectory() as tmp:
            self._persist_rejects(fake2, "DERIVATION_RECEIPT_MISMATCH",
                                  Path(tmp))

    # ── GAP004-FINAL-PERSIST-06: deleted source EvaluationRun → FAIL ─────
    def test_GAP004_FINAL_PERSIST_06_deleted_source_evidence_rejected(self):
        chain, request = self._chain()
        canonical = self._canonical(chain, request)
        result = chain.build_result(  # noqa: F841 — reloaded from disk below
            request, (chain.scorable(asset_id(1)), chain.scorable(asset_id(2))))
        result_file = chain.run_dir / (
            f"{result.result_id.removeprefix('result:')}.json")
        self.assertTrue(result_file.exists())
        result_file.unlink()
        with tempfile.TemporaryDirectory() as tmp:
            self._persist_rejects(canonical, "CANONICAL_EVIDENCE_UNAVAILABLE",
                                  Path(tmp))

    # ── GAP004-FINAL-PERSIST-07: canonical create path → PASS ────────────
    def test_GAP004_FINAL_PERSIST_07_canonical_create_path_passes(self):
        chain, request = self._chain()
        canonical = self._canonical(chain, request)
        self.assertTrue(canonical.derivation_receipt_id.startswith(
            "baseline-derivation:"))
        self.assertEqual(canonical.derivation_context["requestId"],
                         request.request_id)
        with tempfile.TemporaryDirectory() as tmp:
            tmpd = Path(tmp)
            p1 = persist_baseline(canonical, tmpd)
            self.assertTrue(p1.exists())
            content = p1.read_text(encoding="utf-8")
            persisted = json.loads(content)
            self.assertEqual(persisted["baselineId"], canonical.baseline_id)
            self.assertEqual(persisted["derivationReceiptId"],
                             canonical.derivation_receipt_id)
            self.assertEqual(persisted, canonical.to_json())
            # identical re-persist is a write-once no-op (same bytes)
            p2 = persist_baseline(canonical, tmpd)
            self.assertEqual(p1, p2)
            self.assertEqual(p1.read_text(encoding="utf-8"), content)

    # ── GAP004-FINAL-PERSIST-08: incremental consumes canonical only ─────
    def test_GAP004_FINAL_PERSIST_08_incremental_consumes_canonical_only(self):
        chain, request = self._chain()
        canonical = self._canonical(chain, request)
        with tempfile.TemporaryDirectory() as tmp:
            tmpd = Path(tmp)
            p = persist_baseline(canonical, tmpd)
            # incremental.py reads the persisted file exactly like this
            # (json.loads → baselineId); the consumed record carries the
            # canonical derivation receipt (proof present).
            consumed = json.loads(p.read_text(encoding="utf-8"))
            self.assertEqual(consumed["baselineId"], canonical.baseline_id)
            self.assertTrue(consumed["derivationReceiptId"].startswith(
                "baseline-derivation:"))
            # a forged report can never reach the baselines directory:
            # every public-mint attempt fails the persistence gate
            # (PERSIST-01..06) — no alternate writer exists.
            forged = self._public_forged(chain, request)
            with self.assertRaises(BaselineImmutabilityError):
                persist_baseline(forged, tmpd)
            remaining = sorted(tmpd.glob("*.json"))
            self.assertEqual(len(remaining), 1)
            self.assertEqual(remaining[0].name,
                             f"{canonical.baseline_id.replace('baseline:', '')}.json")


if __name__ == "__main__":
    unittest.main()
