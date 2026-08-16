"""RM-MET-01..10 + FINAL-MET-01..12 — canonical quality record-minting
closure (GAP-004 FINAL).

BaselineReport.create is a pure DERIVATION over persisted canonical
records (request → suite → terminal result → prediction/GT/manifest).
Callers persist records; they cannot declare population, counts, coverage,
sufficiency, safety, or GroundTruth-version authority.
"""
from __future__ import annotations

import inspect
import unittest
from pathlib import Path

from evaluation.baseline import BaselineReport, persist_baseline
from evaluation.provenance_scorecard import (
    CanonicalVerificationError, ProvenanceBoundScorecard,
)
from evaluation.asset import (
    ComponentClass, Criticality, PerceptionTask, SystemFamily,
)
from evaluation.tests.chain_helpers import (
    Chain, asset_id, persist_scored_member,
)


class RmMetTests(unittest.TestCase):
    def _chain_report(self, *, members=(asset_id(1),)):
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite(members)
        request = chain.build_request(suite)
        return chain, request

    def _report(self, chain, request):
        return BaselineReport.create(
            request_id=request.request_id,
            run_dir=chain.run_dir, suite_dir=chain.suite_dir,
            prediction_dir=chain.prediction_dir,
            ground_truth_dir=chain.gt_dir,
            asset_manifest_dir=chain.manifest_dir)

    # ── preserved signature / absence falsifiers ──────────────────────────
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
                request_id="request:forged", run_dir="x", suite_dir="x",
                prediction_dir="x", ground_truth_dir="x",
                asset_manifest_dir="x", quality_scorecard=minted)

    def test_RM_MET02_invented_task_slices_ignored(self):
        """There is no parameter through which invented taskSlices can
        enter BaselineReport — quality is derived only."""
        sig = inspect.signature(BaselineReport.create)
        for banned in ("taskSlices", "task_slices", "quality_scorecard",
                       "aggregate", "scoring_results"):
            self.assertNotIn(banned, sig.parameters)

    def test_RM_MET03_zero_results_cannot_yield_invented_aggregates(self):
        """SCORABLE terminal outcome but NO persisted GroundTruth identity →
        nothing scorable → empty task slices, never invented aggregates."""
        chain, request = self._chain_report()
        chain.add_prediction(asset_id(1), request.request_id)
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        j = self._report(chain, request).to_json()
        self.assertEqual(j["qualityScorecard"]["scoringResultCount"], 0)
        self.assertEqual(j["qualityScorecard"]["taskSlices"], {})
        self.assertNotIn("mean", j["qualityScorecard"].get("taskSlices", {})
                         .get("ELEMENT_DETECTION", {}))
        self.assertEqual(j["scoredCount"], 0)

    def test_RM_MET04_wrong_request_rejected(self):
        chain, request = self._chain_report()
        persist_scored_member(chain, asset_id(1), request)
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        with self.assertRaises(CanonicalVerificationError):
            BaselineReport.create(
                request_id="request:WRONG",
                run_dir=chain.run_dir, suite_dir=chain.suite_dir,
                prediction_dir=chain.prediction_dir,
                ground_truth_dir=chain.gt_dir,
                asset_manifest_dir=chain.manifest_dir)

    def test_RM_MET05_wrong_or_missing_prediction_cannot_score(self):
        """Missing persisted Prediction → member MISSING (truthful), never
        silently scored or normalized away."""
        chain, request = self._chain_report()
        chain.add_gt(asset_id(1))
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        j = self._report(chain, request).to_json()
        self.assertEqual(j["scoredCount"], 0)
        self.assertTrue(any("MISSING" in g for g in j["coverageGaps"]))

    def test_RM_MET06_wrong_ground_truth_version_rejected(self):
        """Terminal outcome records gt_version=9 but only GT v1 persisted →
        exact identity load fails → UNSCORABLE (never falls back to v1)."""
        chain, request = self._chain_report()
        chain.add_gt(asset_id(1), gt_version="1")
        chain.add_prediction(asset_id(1), request.request_id)
        chain.build_result(request, (chain.scorable(asset_id(1), gt_version="9"),))
        j = self._report(chain, request).to_json()
        self.assertEqual(j["scoredCount"], 0)
        self.assertTrue(
            any("GroundTruth identity" in g for g in j["groundTruthGaps"]))

    def test_RM_MET07_wrong_deployment_rejected(self):
        chain, request = self._chain_report()
        chain.add_gt(asset_id(1))
        chain.add_prediction(asset_id(1), request.request_id,
                             deployment_hash="deploy:WRONG")
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        j = self._report(chain, request).to_json()
        self.assertEqual(j["scoredCount"], 0)

    def test_RM_MET08_wrong_stage_or_label_space_rejected(self):
        """Stage / label-space are not caller inputs at all: the canonical
        consumer re-scores every member at the frozen FUSED_EVIDENCE
        boundary; there is no parameter to forge."""
        sig = inspect.signature(BaselineReport.create)
        for banned in ("prediction_stage", "prediction_label_space",
                       "prediction_view", "stage", "label_space",
                       "compatibility_verdict"):
            self.assertNotIn(banned, sig.parameters)

    def test_RM_MET09_canonical_summary_derived_from_verified_results(self):
        chain, request = self._chain_report()
        persist_scored_member(chain, asset_id(1), request)
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        j = self._report(chain, request).to_json()
        q = j["qualityScorecard"]
        self.assertEqual(q["scoringResultCount"], 1)
        self.assertEqual(q["requestId"], request.request_id)
        self.assertEqual(q["taskSlices"]["ELEMENT_DETECTION"]["denominator"], 1)

    def test_RM_MET09a_forged_metric_claim_cannot_change_verified_quality(self):
        """The persisted prediction has no detections against one GT
        element, so verified ELEMENT_DETECTION F1 is 0.0 — no caller
        channel exists to claim 1.0."""
        sig = inspect.signature(BaselineReport.create)
        self.assertNotIn("scoring_results", sig.parameters)
        chain, request = self._chain_report()
        persist_scored_member(chain, asset_id(1), request)
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        actual = self._report(chain, request).to_json()["qualityScorecard"]["taskSlices"]
        self.assertEqual(actual["ELEMENT_DETECTION"]["aggregate"]["mean"], 0.0)
        self.assertEqual(actual["ELEMENT_DETECTION"]["denominator"], 1)

    def test_RM_MET09b_lying_loader_api_is_absent(self):
        """Canonical record loading cannot be overridden by a caller lambda."""
        signature = inspect.signature(BaselineReport.create)
        for banned in ("prediction_loader", "gt_loader", "asset_scores",
                       "outcome_loader", "request_loader"):
            self.assertNotIn(banned, signature.parameters)

    def test_RM_MET10_no_alternate_public_quality_save_path(self):
        import pkgutil
        import evaluation
        writers = []
        for mod in pkgutil.walk_packages(evaluation.__path__,
                                         prefix="evaluation."):
            if "tests" in mod.name:
                continue
            relative = mod.name.removeprefix("evaluation.").replace(".", "/")
            module_path = Path(evaluation.__path__[0]) / f"{relative}.py"
            if not module_path.is_file():
                continue
            src = module_path.read_text(encoding="utf-8")
            if "qualityScorecard" in src and mod.name not in (
                    "evaluation.baseline", "evaluation.provenance_scorecard"):
                writers.append(mod.name)
        self.assertEqual(writers, [])


class FinalMetTests(unittest.TestCase):
    """FINAL-MET-01..12 — GAP-004 FINAL consumer authority falsifiers."""

    def _report(self, chain, request):
        return BaselineReport.create(
            request_id=request.request_id,
            run_dir=chain.run_dir, suite_dir=chain.suite_dir,
            prediction_dir=chain.prediction_dir,
            ground_truth_dir=chain.gt_dir,
            asset_manifest_dir=chain.manifest_dir)

    def _ten_requested_seven_scored(self):
        """10 requested members, only 7 favorable records supplied."""
        members = tuple(asset_id(i) for i in range(1, 11))
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite(members)
        request = chain.build_request(suite, asset_scope=members)
        outcomes = []
        for i in range(1, 8):
            persist_scored_member(chain, asset_id(i), request)
            outcomes.append(chain.scorable(asset_id(i)))
        for i in range(8, 11):
            outcomes.append(chain.insufficient(asset_id(i)))
        chain.build_result(request, tuple(outcomes))
        return chain, request

    def test_FINAL_MET_01_requested_population_cannot_be_shrunk_to_favorable(self):
        """10 requested / 7 favorable supplied cannot become 7/7 SUFFICIENT:
        population = canonical request/suite membership, denominator stays 10."""
        chain, request = self._ten_requested_seven_scored()
        j = self._report(chain, request).to_json()
        cov = j["coverage"]
        self.assertEqual(cov["assetCount"], 10)
        self.assertEqual(cov["scoredAssetCount"], 7)
        self.assertEqual(cov["unscoredAssetCount"], 3)
        self.assertEqual(j["assetCount"], 10)
        self.assertEqual(j["evidenceSufficiency"]["stance"], "PARTIAL")
        self.assertNotEqual(j["evidenceSufficiency"]["stance"], "SUFFICIENT")

    def test_FINAL_MET_02_omitted_assets_remain_in_denominator(self):
        chain, request = self._ten_requested_seven_scored()
        j = self._report(chain, request).to_json()
        self.assertEqual(j["unscoredCount"], 3)
        self.assertEqual(j["qualityScorecard"]["scoringResultCount"], 7)
        self.assertEqual(len(j["coverageGaps"]), 3)
        for i in (8, 9, 10):
            self.assertTrue(
                any(f"member {asset_id(i)}" in g for g in j["coverageGaps"]),
                f"omitted member {i} must stay visible in coverage gaps")

    def test_FINAL_MET_03_out_of_suite_prediction_gt_cannot_enter(self):
        """(a) an out-of-scope outcome in the terminal result is REJECTED;
        (b) stray out-of-suite Prediction/GT records (no outcome) cannot
        enter population, counts, classifications, or coverage."""
        members = (asset_id(1),)
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite(members)
        request = chain.build_request(suite)
        persist_scored_member(chain, asset_id(1), request)
        chain.add_gt(asset_id(99))
        chain.add_prediction(asset_id(99), request.request_id)
        chain.build_result(request, (
            chain.scorable(asset_id(1)),
            chain.scorable(asset_id(99)),
        ))
        with self.assertRaises(CanonicalVerificationError):
            self._report(chain, request)

        chain2 = Chain()
        self.addCleanup(chain2.close)
        suite2 = chain2.build_suite(members)
        request2 = chain2.build_request(suite2)
        persist_scored_member(chain2, asset_id(1), request2)
        chain2.add_gt(asset_id(99))
        chain2.add_prediction(asset_id(99), request2.request_id)
        chain2.build_result(request2, (chain2.scorable(asset_id(1)),))
        j = self._report(chain2, request2).to_json()
        self.assertEqual(j["assetCount"], 1)
        self.assertEqual(j["qualityScorecard"]["scoringResultCount"], 1)
        self.assertNotIn(
            asset_id(99),
            {c["assetId"] for c in j["assetClassifications"]})

    def test_FINAL_MET_04_classifications_only_from_canonical_manifests(self):
        """Fabricated classified dimensions cannot enter canonical coverage:
        classification comes from persisted manifests; absent manifest →
        UNKNOWN, never caller-chosen."""
        sig = inspect.signature(BaselineReport.create)
        self.assertNotIn("classified", sig.parameters)
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite((asset_id(1), asset_id(2)))
        request = chain.build_request(suite)
        persist_scored_member(chain, asset_id(1), request)
        persist_scored_member(chain, asset_id(2), request)
        chain.add_manifest(asset_id(1), system_family=SystemFamily.ANDROID_AOSP,
                           component_class=ComponentClass.BUTTON,
                           criticality=Criticality.CRITICAL)
        chain.build_result(request, (
            chain.scorable(asset_id(1)), chain.scorable(asset_id(2))))
        j = self._report(chain, request).to_json()
        by_id = {c["assetId"]: c for c in j["assetClassifications"]}
        self.assertEqual(by_id[asset_id(1)]["systemFamily"], "ANDROID_AOSP")
        self.assertEqual(by_id[asset_id(2)]["systemFamily"], "UNKNOWN")
        cov = j["coverage"]["systemFamilyCoverage"]
        self.assertEqual(cov["ANDROID_AOSP"]["total"], 1)
        self.assertEqual(cov["UNKNOWN"]["total"], 1)

    def test_FINAL_MET_05_declared_tasks_cannot_be_shrunk_to_upgrade_sufficiency(self):
        """declared_tasks come from the canonical suite — the caller has no
        parameter; a suite requiring ELEMENT_DETECTION with 7/10 scored can
        never report SUFFICIENT."""
        self.assertNotIn("declared_tasks",
                         inspect.signature(BaselineReport.create).parameters)
        chain, request = self._ten_requested_seven_scored()
        j = self._report(chain, request).to_json()
        self.assertEqual(j["evidenceSufficiency"]["stance"], "PARTIAL")
        self.assertEqual(
            j["evidenceSufficiency"]["declaredTasks"],
            ["ELEMENT_DETECTION"])

    def test_FINAL_MET_06_caller_counts_cannot_mint_baseline(self):
        """asset/scored/unscored counts are DERIVED — no caller parameter."""
        sig = inspect.signature(BaselineReport.create)
        for banned in ("asset_count", "scored_count", "unscored_count",
                       "assetCount", "scoredCount"):
            self.assertNotIn(banned, sig.parameters)
        chain, request = self._ten_requested_seven_scored()
        j = self._report(chain, request).to_json()
        self.assertEqual(j["assetCount"], 10)
        self.assertEqual(j["scoredCount"], 7)

    def test_FINAL_MET_07_safety_scorecard_derived_not_declared(self):
        """A caller-created safety scorecard has ZERO authority: safety is
        derived from the verified scoring records."""
        self.assertNotIn("safety_scorecard",
                         inspect.signature(BaselineReport.create).parameters)
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite((asset_id(1),))
        request = chain.build_request(suite)
        persist_scored_member(chain, asset_id(1), request)
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        j = self._report(chain, request).to_json()
        self.assertEqual(
            j["safetyScorecard"],
            j["qualityScorecard"]["safetySection"])
        self.assertTrue(j["safetyScorecard"].get("visible") is True)

    def test_FINAL_MET_08_wrong_gt_version_rejected(self):
        """Terminal outcome owns the exact GT version; if that exact record
        is absent the member is UNSCORABLE — v1 can never substitute."""
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite((asset_id(1),))
        request = chain.build_request(suite)
        chain.add_gt(asset_id(1), gt_version="1")
        chain.add_prediction(asset_id(1), request.request_id)
        chain.build_result(request, (chain.scorable(asset_id(1), gt_version="2"),))
        j = self._report(chain, request).to_json()
        self.assertEqual(j["scoredCount"], 0)
        self.assertEqual(j["qualityScorecard"]["scoringResultCount"], 0)
        self.assertTrue(
            any("GroundTruth identity" in g for g in j["groundTruthGaps"]))

    def test_FINAL_MET_09_gt_version_resolved_by_identity_not_glob_order(self):
        """v1 and v10 exist for the same asset; v1 sorts first on disk and
        declares NO tasks.  Outcome records gt_version=10 → exact canonical
        identity must resolve v10 (ELEMENT_DETECTION scored), never
        glob-order v1.  (SAFETY/coordinate-validity is always scorable per
        B11, so the discriminator is the ELEMENT_DETECTION slice, not the
        raw scored flag.)"""
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite((asset_id(1),))
        request = chain.build_request(suite)
        chain.add_gt(asset_id(1), gt_version="1", declared_tasks=())
        chain.add_gt(asset_id(1), gt_version="10")
        chain.add_prediction(asset_id(1), request.request_id)
        chain.build_result(request, (chain.scorable(asset_id(1), gt_version="10"),))
        j = self._report(chain, request).to_json()
        self.assertEqual(j["scoredCount"], 1)
        self.assertEqual(j["qualityScorecard"]["scoringResultCount"], 1)
        self.assertIn("ELEMENT_DETECTION",
                      j["qualityScorecard"]["taskSlices"])
        # the other direction: identity "1" resolves the task-less GT —
        # ELEMENT_DETECTION must NOT be scored (glob-order would pick v10)
        chain2 = Chain()
        self.addCleanup(chain2.close)
        suite2 = chain2.build_suite((asset_id(1),))
        request2 = chain2.build_request(suite2)
        chain2.add_gt(asset_id(1), gt_version="1", declared_tasks=())
        chain2.add_gt(asset_id(1), gt_version="10")
        chain2.add_prediction(asset_id(1), request2.request_id)
        chain2.build_result(request2, (chain2.scorable(asset_id(1), gt_version="1"),))
        j2 = self._report(chain2, request2).to_json()
        self.assertNotIn("ELEMENT_DETECTION",
                         j2["qualityScorecard"]["taskSlices"])
        self.assertEqual(
            j2["qualityScorecard"]["taskSlices"].get("ELEMENT_DETECTION"),
            None)

    def test_FINAL_MET_10_wrong_request_deployment_stage_rejected(self):
        """(a) prediction persisted under a different request id → MISSING;
        (b) prediction with a different deployment identity → UNSCORABLE;
        (c) stage/label-space are not caller inputs at all."""
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite((asset_id(1),))
        request = chain.build_request(suite)
        chain.add_gt(asset_id(1))
        chain.add_prediction(asset_id(1), "request:OTHER")
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        j = self._report(chain, request).to_json()
        self.assertEqual(j["scoredCount"], 0)

        chain2 = Chain()
        self.addCleanup(chain2.close)
        suite2 = chain2.build_suite((asset_id(2),))
        request2 = chain2.build_request(suite2)
        chain2.add_gt(asset_id(2))
        chain2.add_prediction(asset_id(2), request2.request_id,
                              deployment_hash="deploy:wrong")
        chain2.build_result(request2, (chain2.scorable(asset_id(2)),))
        j2 = self._report(chain2, request2).to_json()
        self.assertEqual(j2["scoredCount"], 0)

        sig = inspect.signature(BaselineReport.create)
        for banned in ("stage", "label_space", "prediction_view"):
            self.assertNotIn(banned, sig.parameters)

    def test_FINAL_MET_11_terminal_partial_run_truthful_partial_baseline(self):
        """Canonical PARTIAL terminal run → truthful PARTIAL baseline:
        3 scored, 1 unscorable (no GT identity), 1 insufficient."""
        members = tuple(asset_id(i) for i in range(1, 6))
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite(members)
        request = chain.build_request(suite)
        outcomes = []
        for i in (1, 2, 3):
            persist_scored_member(chain, asset_id(i), request)
            outcomes.append(chain.scorable(asset_id(i)))
        chain.add_prediction(asset_id(4), request.request_id)
        outcomes.append(chain.scorable(asset_id(4)))
        outcomes.append(chain.insufficient(asset_id(5)))
        result = chain.build_result(request, tuple(outcomes))
        self.assertEqual(result.terminal_status.value, "PARTIAL")
        j = self._report(chain, request).to_json()
        self.assertEqual(j["assetCount"], 5)
        self.assertEqual(j["scoredCount"], 3)
        self.assertEqual(j["unscoredCount"], 2)
        self.assertEqual(j["evidenceSufficiency"]["stance"], "PARTIAL")
        gaps = "\n".join(j["coverageGaps"])
        self.assertIn(f"member {asset_id(4)}", gaps)
        self.assertIn(f"member {asset_id(5)}", gaps)

    def test_FINAL_MET_12_no_alternate_persistence_path_accepts_caller_scope(self):
        """persist_baseline accepts only the derived BaselineReport + dir;
        BaselineReport is frozen — scope cannot be set post-creation."""
        sig = inspect.signature(persist_baseline)
        self.assertEqual(list(sig.parameters), ["report", "out_dir"])
        chain, request = self._ten_requested_seven_scored()
        report = self._report(chain, request)
        with self.assertRaises(AttributeError):
            report.asset_count = 7
        with self.assertRaises(AttributeError):
            report.scored_count = 7


if __name__ == "__main__":
    unittest.main()
