"""Scorecard/coverage falsifiers: B9, B10, B11, B14-adjacent, I23, I24."""
from __future__ import annotations

import unittest

from evaluation.groundtruth import TaskStance
from evaluation.scorecard import (
    AssetScore, SliceStatus, build_coverage, build_scorecard, evidence_sufficiency,
)


def _scored_asset(aid: str, task: str, f1: float) -> AssetScore:
    return AssetScore(
        asset_id=aid, scored=True,
        tasks={task: {"stance": TaskStance.SCORED.value,
                      "metrics": {"f1": f1, "tp": 1, "fp": 0, "fn": 0},
                      "denominator": 1}},
        classification={"systemFamily": "UNKNOWN", "componentClass": "TEXT",
                        "criticality": "NORMAL"},
    )


def _unscored_asset(aid: str) -> AssetScore:
    return AssetScore(
        asset_id=aid, scored=False,
        tasks={"OCR": {"stance": TaskStance.NOT_SCORABLE.value, "metrics": {},
                       "denominator": 0}},
        classification={"systemFamily": "UNKNOWN", "componentClass": "UNKNOWN",
                        "criticality": "UNKNOWN"},
    )


class ScorecardTests(unittest.TestCase):
    def test_B9_zero_assets_reports_unassessed_not_perfect(self):
        classified = []
        cov = build_coverage([], classified)
        # system family slice with zero assets → UNASSESSED, never 100%
        for v, d in cov["systemFamilyCoverage"].items():
            self.assertEqual(d["total"], 0)
            self.assertEqual(d["status"], SliceStatus.UNASSESSED.value)
        self.assertEqual(cov["assetCount"], 0)

    def test_B10_overall_cannot_hide_category_results(self):
        # one perfect asset and one missing asset: the scorecard keeps both
        scores = [_scored_asset("sha256:a", "ELEMENT_DETECTION", 1.0),
                  _unscored_asset("sha256:b")]
        sc = build_scorecard(scores)
        self.assertEqual(sc["taskSlices"]["ELEMENT_DETECTION"]["scoredAssets"], 1)
        self.assertEqual(sc["taskSlices"]["ELEMENT_DETECTION"]["aggregate"]["n"], 1)
        # unscored asset is not merged into any quality slice
        self.assertNotIn("OCR", sc["taskSlices"])

    def test_B11_safety_separately_visible(self):
        scores = [_scored_asset("sha256:a", "SAFETY", 1.0)]
        sc = build_scorecard(scores)
        self.assertTrue(sc["sections"]["SAFETY"]["visible"])
        self.assertIn("sha256:a", sc["sections"]["SAFETY"]["perAsset"])

    def test_denominators_always_present(self):
        scores = [_scored_asset("sha256:a", "ELEMENT_DETECTION", 0.75)]
        sc = build_scorecard(scores)
        self.assertEqual(sc["taskSlices"]["ELEMENT_DETECTION"]["denominator"], 1)
        self.assertEqual(sc["taskSlices"]["ELEMENT_DETECTION"]["scoredAssets"], 1)

    def test_evidence_sufficiency_partial(self):
        scores = [_scored_asset("sha256:a", "ELEMENT_DETECTION", 1.0),
                  _unscored_asset("sha256:b")]
        suff = evidence_sufficiency(scores, ["ELEMENT_DETECTION", "OCR"])
        self.assertEqual(suff["stance"], "PARTIAL")
        self.assertEqual(suff["uncoveredDeclaredTasks"], ["OCR"])

    def test_evidence_sufficiency_insufficient(self):
        suff = evidence_sufficiency([_unscored_asset("sha256:b")],
                                    ["ELEMENT_DETECTION"])
        self.assertEqual(suff["stance"], "INSUFFICIENT")

    def test_evidence_sufficiency_sufficient(self):
        scores = [_scored_asset("sha256:a", "ELEMENT_DETECTION", 1.0),
                  _scored_asset("sha256:b", "OCR", 0.9)]
        suff = evidence_sufficiency(scores, ["ELEMENT_DETECTION", "OCR"])
        self.assertEqual(suff["stance"], "SUFFICIENT")

    def test_coverage_partial_status(self):
        classified = [{"assetId": "sha256:a", "systemFamily": "UNKNOWN",
                       "perceptionTask": "ELEMENT_DETECTION",
                       "componentClass": "TEXT", "corpusRole": "CALIBRATION",
                       "criticality": "NORMAL"}]
        cov = build_coverage([_scored_asset("sha256:a", "ELEMENT_DETECTION", 1.0)],
                             classified)
        self.assertEqual(cov["systemFamilyCoverage"]["UNKNOWN"]["total"], 1)
        self.assertEqual(cov["systemFamilyCoverage"]["UNKNOWN"]["status"],
                         SliceStatus.ASSESSED.value)
        self.assertEqual(cov["systemFamilyCoverage"]["ANDROID_AOSP"]["total"], 0)
        self.assertEqual(cov["systemFamilyCoverage"]["ANDROID_AOSP"]["status"],
                         SliceStatus.UNASSESSED.value)
        self.assertEqual(cov["holdoutStatus"], "NONE")


if __name__ == "__main__":
    unittest.main()
