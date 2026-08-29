"""Structural title-column exemption unit tests (WI-PFW-S2fix2, OpenSpec
change ``perception-operator-rule-framework``).

The verifier's C4 column-spread check is a uniform-list shape assumption (all
rows on one column).  Real relation-head child pages can carry ONE structural
shape the uniform-list model never produces: a page-TITLE band on the far-left
column with the menu content rows on a single indented dominant column
(e.g. x1 69 / 213 / 214 / 208).  For exactly that shape the topmost band is
exempted from the C4 spread computation (it is a title column, not a
misaligned menu row) and the spread is re-computed over the dominant-column
bands only.

The exemption is structural and fail-closed:

* fires ONLY when every non-topmost band forms ONE dominant column cluster
  (>= 2 bands within the verifier's per-side column tolerance of that set's
  median x1) AND the topmost band lies LEFT of that cluster by more than the
  column tolerance;
* any other mixed-column shape keeps the original full-set spread check
  (veto) — an off-cluster band that is NOT the topmost band, a missing
  dominant column (< 2 content rows), and multiple off-cluster bands all
  still reject;
* no threshold is relaxed: the same ``columnToleranceFloor`` /
  ``columnToleranceRatio`` / ``minStepRatio`` bounds and the full-band
  vertical-cadence check remain exactly as declared.

Candidates use the verifier's input contract (``boundsPx`` / ``centerPx`` /
``evidence.typeInferred`` with authorized GENERATOR provenance); the frozen
failure-shape x1 values (69 / 213 / 214 / 208, median step 155) come from the
relation-head trace record one-to-one.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.operators.spacing_verifier import (
    VERIFIER_PARAM_DEFAULTS,
    verify,
)

#: Fused contract defaults the verifier resolves in the executed pipeline.
_PARAMS = dict(VERIFIER_PARAM_DEFAULTS)


def _menu(
    identifier: str,
    text: str,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
) -> dict:
    """One generated menu row in the verifier's input contract (row geometry
    only; ``typeInferred`` = the authorized relation-head provenance)."""
    return {
        "id": identifier,
        "type": "menu_item",
        "text": text,
        "confidence": 0.9,
        "bounds": {
            "x1": round(x1 / 1080.0, 6),
            "y1": round(y1 / 2400.0, 6),
            "x2": round(x2 / 1080.0, 6),
            "y2": round(y2 / 2400.0, 6),
        },
        "boundsPx": [x1, y1, x2, y2],
        "center": {
            "x": round((x1 + x2) / 2.0 / 1080.0, 6),
            "y": round((y1 + y2) / 2.0 / 2400.0, 6),
        },
        "centerPx": [round((x1 + x2) / 2.0), round((y1 + y2) / 2.0)],
        "evidence": {"typeInferred": "row_relation_head", "ocrIds": [], "allIds": [identifier]},
        "riskFlags": [],
    }


def _frozen_child_page_shape() -> list[dict]:
    """The exact frozen failure shape (WI-PFW-S2fix2): title band x1=69 on the
    far-left column + three content rows on the dominant column x1≈213/214/208
    (column spread over all four = 145px > bound max(24, 2*0.20*155)=62px)."""
    return [
        _menu("relation_head_band_1", "Choose wallpaper", 69, 438, 828, 636),
        _menu("relation_head_band_2", "Gallery", 213, 774, 373, 828),
        _menu("relation_head_band_3", "Live Wallpapers", 214, 930, 580, 981),
        _menu("relation_head_band_4", "Wallpaper & style", 208, 1083, 616, 1138),
    ]


class TitleColumnExemptionTests(unittest.TestCase):
    """The exemption fires (and only fires) on the structural title-column
    shape; every other mixed-column shape keeps the fail-closed veto."""

    def test_frozen_child_page_shape_is_exempt_and_verified(self):
        # The E2E child-page shape (title 69 / content 213 / 214 / 208, median
        # step 155): spread over all four = 145px > 62px — WITHOUT the
        # exemption the veto fires; WITH it the title band is a title column,
        # the C4 spread over the dominant column = 214-208 = 6px, verified.
        candidates = _frozen_child_page_shape()
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(
            verdict["status"], "verified",
            f"the frozen child-page shape must verify; got {verdict['detail']}",
        )
        self.assertIn("title_column_exempted", verdict["detail"])
        record = verdict["titleColumnExempted"]
        self.assertEqual(record["band"], "relation_head_band_1")
        self.assertEqual(record["x1"], 69.0)
        self.assertEqual(record["dominantColumnX1"], 213.0)
        self.assertEqual(record["columnTolerance"], 31.0)
        # The candidates are untouched (the verifier is a pure VALIDATOR).
        self.assertEqual(candidates, _frozen_child_page_shape())

    def test_off_column_band_that_is_not_topmost_still_rejects(self):
        # The off-column band (x1=69) is NOT the page's topmost band: the
        # exemption requires the title column to BE the topmost band — a lower
        # off-column row is a misaligned menu row, fail-closed veto.
        candidates = [
            _menu("band_1", "Gallery", 213, 438, 373, 492),        # topmost, dominant col
            _menu("band_x", "from", 69, 558, 262, 637),            # off-column, NOT topmost
            _menu("band_3", "Live Wallpapers", 214, 774, 580, 825),
            _menu("band_4", "Wallpaper & style", 208, 930, 616, 985),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("fail-closed", verdict["detail"])
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_no_dominant_column_single_content_row_not_exempt(self):
        # Only ONE content row below the title: no dominant column (the cluster
        # needs >= 2 bands), so the exemption cannot fire — original full-set
        # spread check vetoes (144px > 62px).
        candidates = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636),
            _menu("band_2", "Gallery", 213, 774, 373, 828),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_multiple_off_column_bands_still_reject(self):
        # Two bands left of the content column (a second left-column row beyond
        # the topmost): not exactly one exempt topmost band — the strict
        # one-title shape is violated, fail-closed veto.
        candidates = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636),
            _menu("band_x", "Section header", 72, 558, 420, 610),
            _menu("band_3", "Live Wallpapers", 214, 774, 580, 825),
            _menu("band_4", "Wallpaper & style", 208, 930, 616, 985),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_off_column_band_beyond_cluster_tolerance_still_rejects(self):
        # The non-topmost bands do NOT form ONE dominant column (x1 940 is
        # beyond the per-side tolerance of the 208..214 cluster): an
        # off-cluster band exists → no exemption → full-set veto.
        candidates = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636),
            _menu("band_2", "Gallery", 213, 774, 373, 828),
            _menu("band_3", "Live Wallpapers", 214, 930, 580, 981),
            _menu("band_4", "Wallpaper & style", 208, 1083, 616, 1138),
            _menu("band_5", "Separate section", 940, 1200, 1060, 1240),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_topmost_band_inside_dominant_column_is_not_exempt(self):
        # The topmost band sits ON the dominant column (no title column): the
        # left-of-tolerance predicate fails → no exemption; the same-column
        # shape verifies through the ORIGINAL check (no record, byte-clean
        # verified detail).
        candidates = [
            _menu("band_1", "Gallery", 213, 438, 373, 492),
            _menu("band_2", "Live Wallpapers", 214, 558, 580, 605),
            _menu("band_3", "Wallpaper & style", 208, 678, 616, 725),
            _menu("band_4", "Network", 212, 798, 440, 845),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "verified")
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_tighten_only_parameters_unchanged(self):
        # The exemption must not weaken the tighten-only parameter surface:
        # the frozen set and safe directions are untouched (regression guard —
        # the parameter contract itself is asserted by the wiring tests).
        self.assertEqual(
            sorted(VERIFIER_PARAM_DEFAULTS),
            ["columnToleranceFloor", "columnToleranceRatio", "maxMenuItems", "minStepRatio"],
        )
        # A raised columnToleranceFloor widens the per-side exemption window
        # (floor/2) in the SAME declared tightening direction the framework
        # pins (values >= default): with floor 100 → per-side 50; the title is
        # 144px left of the dominant column — still beyond 50, so the
        # exemption still applies and records the resolved tolerance.
        tightened = dict(_PARAMS)
        tightened["columnToleranceFloor"] = 100.0
        verdict = verify(_frozen_child_page_shape(), [], tightened)
        self.assertEqual(verdict["status"], "verified")
        self.assertEqual(verdict["titleColumnExempted"]["columnTolerance"], 50.0)
        # Tightening NEVER converts a non-eligible shape into an exemption: a
        # mixed shape with TWO left-column bands (not exactly one topmost
        # title) still fails the structural predicate, so the full-set spread
        # check governs — and with floor 100 its bound (100px) still vetoes
        # the 145px spread.  The exemption predicate, not the threshold,
        # decides the path (structure, never a loosen).
        mixed = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636),
            _menu("band_x", "Section header", 72, 558, 420, 610),
            _menu("band_3", "Live Wallpapers", 214, 774, 580, 825),
            _menu("band_4", "Wallpaper & style", 208, 930, 616, 985),
        ]
        verdict = verify(mixed, [], tightened)
        self.assertEqual(verdict["status"], "rejected")
        self.assertNotIn("title_column_exempted", verdict["detail"])


if __name__ == "__main__":
    unittest.main()