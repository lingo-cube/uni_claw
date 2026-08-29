"""C4-scope + title-column exemption unit tests (FRAME_LOCAL_COMPOSITION_
VALIDITY_VETO_REPAIR_GATE; successor to WI-PFW-S2fix2 coverage).

FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE: the verifier's C4
column-spread check is a *uniform-list* shape assumption (all rows on one
column) and now executes ONLY on rows with uniform-list provenance
(``UNIFORM_LIST_ROW_REASONS``).  relation-head bands compose per-band columns
and are no longer wholesale-vetoed on the uniform-list single-column premise.

Within the C4 scope (uniform-list rows), the structural title-column
exemption (S2fix2) is preserved: a page-TITLE band on the far-left column with
the menu content rows on a single indented dominant column (e.g. x1
69 / 213 / 214 / 208) exempts the topmost band and re-computes the spread over
the dominant-column bands only.  The exemption is structural and fail-closed:

* fires ONLY when every non-topmost C4-scope band forms ONE dominant column
  cluster (>= 2 bands within the per-side column tolerance of that set's
  median x1) AND the topmost band lies LEFT of that cluster by more than the
  column tolerance;
* any other mixed-column shape keeps the fail-closed full-set spread veto;
* no threshold is relaxed.

Candidates use the verifier's input contract; the frozen failure-shape x1
values (69 / 213 / 214 / 208) come from the trace record one-to-one.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.operators.spacing_verifier import (
    VERIFIER_PARAM_DEFAULTS,
    verify,
)

#: Fused contract defaults the verifier resolves in the executed pipeline.
_PARAMS = dict(VERIFIER_PARAM_DEFAULTS)

_UNIFORM = "uniform_list_bracketed_row"
_RELATION = "row_relation_head"


def _menu(
    identifier: str,
    text: str,
    x1: float,
    y1: float,
    x2: float,
    y2: float,
    inferred: str = _UNIFORM,
) -> dict:
    """One generated menu row in the verifier's input contract (row geometry
    only; ``typeInferred`` provenance)."""
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
        "evidence": {"typeInferred": inferred, "ocrIds": [], "allIds": [identifier]},
        "riskFlags": [],
    }


def _frozen_title_column_shape(inferred: str = _UNIFORM) -> list[dict]:
    """The frozen C4 failure shape: topmost title band x1=69 on the far-left
    column + three content rows on the dominant column x1≈213/214/208
    (column spread over all four = 145px > bound max(24, 2*0.20*155)=62px)."""
    return [
        _menu("band_1", "Choose wallpaper", 69, 438, 828, 636, inferred),
        _menu("band_2", "Gallery", 213, 774, 373, 828, inferred),
        _menu("band_3", "Live Wallpapers", 214, 930, 580, 981, inferred),
        _menu("band_4", "Wallpaper & style", 208, 1083, 616, 1138, inferred),
    ]


class C4ScopeAndTitleColumnExemptionTests(unittest.TestCase):
    """C4 applies only to uniform-list provenance rows; within that scope the
    structural title-column exemption fires exactly on its one shape and every
    other mixed shape stays fail-closed."""

    def test_uniform_list_title_column_shape_is_exempt_and_verified(self):
        # Uniform-list rows with the frozen title-column shape: without the
        # exemption the spread (145px > 62px) vetoes; with it the title band is
        # a title column and the C4 spread over the dominant column = 6px.
        candidates = _frozen_title_column_shape(_UNIFORM)
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "verified", verdict["detail"])
        self.assertIn("title_column_exempted", verdict["detail"])
        record = verdict["titleColumnExempted"]
        self.assertEqual(record["band"], "band_1")
        self.assertEqual(record["x1"], 69.0)
        self.assertEqual(record["dominantColumnX1"], 213.0)
        self.assertEqual(record["columnTolerance"], 31.0)
        self.assertEqual(candidates, _frozen_title_column_shape(_UNIFORM))

    def test_relation_head_wide_spread_not_c4_vetoed(self):
        # Repair gate: relation-head bands are OUTSIDE the C4 scope — the
        # uniform-list single-column premise must not wholesale-veto them.
        # The same frozen shape with relation-head provenance verifies (no
        # C4 veto; no exemption record needed).
        candidates = _frozen_title_column_shape(_RELATION)
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "verified", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_uniform_off_column_band_that_is_not_topmost_still_rejects(self):
        # Within the C4 scope: the off-column band (x1=69) is NOT the topmost
        # band — a lower off-column row is a misaligned uniform-list menu row,
        # fail-closed veto (exemption requires the title column to be topmost).
        candidates = [
            _menu("band_1", "Gallery", 213, 438, 373, 492, _UNIFORM),
            _menu("band_x", "from", 69, 558, 262, 637, _UNIFORM),
            _menu("band_3", "Live Wallpapers", 214, 774, 580, 825, _UNIFORM),
            _menu("band_4", "Wallpaper & style", 208, 930, 616, 985, _UNIFORM),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("fail-closed", verdict["detail"])
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_uniform_no_dominant_column_single_content_row_not_exempt(self):
        # Only ONE content row below the title: no dominant column (cluster
        # needs >= 2 bands) -> no exemption -> full-set spread veto.
        candidates = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636, _UNIFORM),
            _menu("band_2", "Gallery", 213, 774, 373, 828, _UNIFORM),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_uniform_multiple_off_column_bands_still_reject(self):
        # Two bands left of the content column: not exactly one topmost title
        # band -> the strict one-title shape is violated, fail-closed veto.
        candidates = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636, _UNIFORM),
            _menu("band_x", "Section header", 72, 558, 420, 610, _UNIFORM),
            _menu("band_3", "Live Wallpapers", 214, 774, 580, 825, _UNIFORM),
            _menu("band_4", "Wallpaper & style", 208, 930, 616, 985, _UNIFORM),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_uniform_off_column_beyond_cluster_tolerance_still_rejects(self):
        # The non-topmost bands do NOT form ONE dominant column (x1 940 is
        # beyond the per-side tolerance of the 208..214 cluster): an
        # off-cluster band exists -> no exemption -> full-set veto.
        candidates = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636, _UNIFORM),
            _menu("band_2", "Gallery", 213, 774, 373, 828, _UNIFORM),
            _menu("band_3", "Live Wallpapers", 214, 930, 580, 981, _UNIFORM),
            _menu("band_4", "Wallpaper & style", 208, 1083, 616, 1138, _UNIFORM),
            _menu("band_5", "Separate section", 940, 1200, 1060, 1240, _UNIFORM),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("column spread", verdict["detail"])
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_uniform_topmost_band_inside_dominant_column_not_exempt(self):
        candidates = [
            _menu("band_1", "Gallery", 213, 438, 373, 492, _UNIFORM),
            _menu("band_2", "Live Wallpapers", 214, 558, 580, 605, _UNIFORM),
            _menu("band_3", "Wallpaper & style", 208, 678, 616, 725, _UNIFORM),
            _menu("band_4", "Network", 212, 798, 440, 845, _UNIFORM),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "verified")
        self.assertNotIn("title_column_exempted", verdict["detail"])

    def test_relation_head_mixed_shape_verified_by_c5_only(self):
        # Repair gate: a relation-head mixed-column shape that the old full-set
        # C4 would veto now verifies (C4 out of scope); C5 vertical cadence is
        # the remaining geometry gate for relation-head rows.
        candidates = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636, _RELATION),
            _menu("band_x", "Section header", 72, 558, 420, 610, _RELATION),
            _menu("band_3", "Live Wallpapers", 214, 774, 580, 825, _RELATION),
            _menu("band_4", "Wallpaper & style", 208, 930, 616, 985, _RELATION),
        ]
        verdict = verify(candidates, [], _PARAMS)
        self.assertEqual(verdict["status"], "verified", verdict["detail"])

    def test_tighten_only_parameters_unchanged(self):
        # The exemption must not weaken the tighten-only parameter surface.
        self.assertEqual(
            sorted(VERIFIER_PARAM_DEFAULTS),
            ["columnToleranceFloor", "columnToleranceRatio", "maxMenuItems", "minStepRatio"],
        )
        tightened = dict(_PARAMS)
        tightened["columnToleranceFloor"] = 100.0
        verdict = verify(_frozen_title_column_shape(_UNIFORM), [], tightened)
        self.assertEqual(verdict["status"], "verified")
        self.assertEqual(verdict["titleColumnExempted"]["columnTolerance"], 50.0)
        # Tightening NEVER converts a non-eligible shape into an exemption: a
        # mixed shape with TWO left-column bands still fails the structural
        # predicate, so the full-set spread check governs (structure, never a
        # loosen).
        mixed = [
            _menu("band_1", "Choose wallpaper", 69, 438, 828, 636, _UNIFORM),
            _menu("band_x", "Section header", 72, 558, 420, 610, _UNIFORM),
            _menu("band_3", "Live Wallpapers", 214, 774, 580, 825, _UNIFORM),
            _menu("band_4", "Wallpaper & style", 208, 930, 616, 985, _UNIFORM),
        ]
        verdict = verify(mixed, [], tightened)
        self.assertEqual(verdict["status"], "rejected")
        self.assertNotIn("title_column_exempted", verdict["detail"])


if __name__ == "__main__":
    unittest.main()