"""WI-P26-ROWFIX-A — row-band ownership merge + subtitle ownership +
cadence consensus (RED→GREEN against the real P26-V2 run1 geometry).

The three frozen fixes (``change_principles`` of the WorkItem), each with a
RED case reproducing the REAL trace geometry from
``openspec/changes/container-runtime-v2-core-semantics/evidence/p26-v2-run1/
p26-fusion-traces.json``:

1. **Row-band ownership merge** — when a ``row-relation-head`` band row enters
   the fused candidate list, absorb the same-band unresolved title
   ``text_block`` whose center-Y falls inside that band row's y-extent and x
   within the title-column tolerance: one row, one representation.  PURE
   geometry — no text-matching merge.  Same-text ambiguity across DIFFERENT
   bands stays fail-closed.
2. **Subtitle ownership** — a ``text_block`` in the title column directly
   below a visible parent row (gap <= 0.8*parent height, different text)
   attaches as that row's supporting element; a ``text_block`` in the clipped
   top/bottom viewport edge with no attachable parent is marked
   edge-clipped supporting; otherwise it stays unresolved (never guessed).
3. **Cadence consensus** — ``_infer_model`` keeps the pitch inference
   (median of the lower 60% of gaps) but replaces the all-gates rule
   (``direct >= 2 and valid == all``) with consensus
   (``direct >= 2 and valid >= max(3, ceil(0.6*len(gaps)))``).  Outlier gaps
   simply don't participate in cadence proposals (the existing ``continue``
   behaviour); if consensus fails the generator still NOOPs fail-closed.
   Validator semantics are untouched.

Good-frame invariants: frames 4 (gaps=[153,159,150,164,147,151]) and 13
(gaps=[157,169,294,151,326]) must produce byte-identical uniform-list output
before and after the fix (snapshot tests lock the CURRENT output as the golden
baseline captured from unmodified production).
"""
from __future__ import annotations

import math
import unittest

from uniclaw_perception.operators.uniform_list_row_grouping import (
    apply_uniform_list_grouping_params,
)


def _assign(cands: list[dict]) -> list[dict]:
    """Invoke the engine's row-band supporting-ownership assembly step.

    Imported lazily so the module still loads (and the cadence tests still
    run RED) before the fix #1/#2 function exists."""
    from uniclaw_perception.fusion.engine import _assign_row_band_supporting_ownership as fn

    return fn(cands)

# ─────────────────────────────────────────────────────────────────────────────
# Geometry/synthetic candidate helpers (boundsPx=[x1,y1,x2,y2],
# centerPx=[cx,cy]; normalized viewport 720x1400 kept only for realism).
# ─────────────────────────────────────────────────────────────────────────────


def _row(
    cid: str,
    text: str,
    x1: float,
    y1: float,
    y2: float,
    kind: str = "menu_item",
    inferred: str | None = None,
    width: float | None = None,
) -> dict:
    x2 = (x1 + width) if width is not None else (x1 + 260)
    cy = (y1 + y2) / 2.0
    evidence: dict = {"yoloId": f"y_{cid}", "ocrIds": [f"o_{cid}"], "allIds": [f"y_{cid}", f"o_{cid}"]}
    if inferred is not None:
        evidence["typeInferred"] = inferred
    return {
        "id": cid,
        "type": kind,
        "text": text,
        "confidence": 0.9,
        "bounds": {"x1": x1 / 720.0, "y1": y1 / 1400.0, "x2": x2 / 720.0, "y2": y2 / 1400.0},
        "boundsPx": [x1, y1, x2, y2],
        "centerPx": [(x1 + x2) / 2.0, cy],
        "evidence": evidence,
        "riskFlags": [],
    }


def _band_row(cid: str, text: str, x1: float, y1: float, y2: float, width: float = 260) -> dict:
    """A row-relation-head composed band row (fix #1 ownership source)."""
    return _row(cid, text, x1, y1, y2, kind="menu_item", inferred="row_relation_head", width=width)


# ── Frame-10-type anchor geometry (p26-fusion-traces.json seq 10) ──────────
_FRAME10_ANCHOR_YS = [258.0, 394.0, 855.0, 1008.0, 1181.0, 1315.0]
_FRAME10_ANCHOR_HS = [71.0, 34.0, 34.0, 30.0, 71.0, 29.0]
_FRAME10_ANCHOR_XS = [128.0, 127.0, 127.0, 127.0, 127.0, 128.0]
_FRAME10_GAPS = [136.0, 461.0, 153.0, 173.0, 134.0]

# Frame-10 unresolved title text_blocks (FusionOutput geometry).
# candidate_10 'Sound & vibration' and candidate_12 'Display' are the same
# physical row as relation-head band rows; candidate_13 'Dark theme, font
# size, brightness' is the subtitle directly below 'Display'.
_C10 = _row("candidate_10", "Sound & vibration", 128.0, 531.0, 559.0, kind="text_block")  # centerY 545
_C12 = _row("candidate_12", "Display", 127.0, 684.0, 721.0, kind="text_block")  # centerY 702
_C13 = _row(
    "candidate_13", "Dark theme, font size, brightness", 126.0, 728.0, 754.0,
    kind="text_block", width=400,
)  # centerY 741; gap below Display = 728-721 = 7px


def _frame10_anchors() -> list[dict]:
    texts = ["Mobile & networks", "Wi-Fi", "Network & internet",
             "Connected devices", "Apps", "Notifications"]
    return [
        _row(f"candidate_{i}", texts[i], xs, cy - h / 2, cy + h / 2)
        for i, (cy, h, xs) in enumerate(
            zip(_FRAME10_ANCHOR_YS, _FRAME10_ANCHOR_HS, _FRAME10_ANCHOR_XS)
        )
    ]


# ── Frame-7 top-clipped orphan (seq 7: 'Bluetooth, pairing' at y=[146,172]) ─
_FRAME7_EDGE_Y1, _FRAME7_EDGE_Y2 = 146.0, 172.0
_BLUETOOTH_PAIRING = _row(
    "candidate_6", "Bluetooth, pairing", 129.0, _FRAME7_EDGE_Y1, _FRAME7_EDGE_Y2,
    kind="text_block", width=190,
)


# ── Frame-4 / frame-13 good-frame anchor geometry ───────────────────────────
_FRAME4 = {
    "gaps": [153.0, 159.0, 150.0, 164.0, 147.0, 151.0],
    "ys": [241.0, 394.0, 553.0, 703.0, 867.0, 1014.0, 1165.0],
    "hs": [30.0, 30.0, 34.0, 28.0, 47.0, 34.0, 29.0],
    "xs": [128.0, 128.0, 128.0, 127.0, 128.0, 126.0, 127.0],
    "texts": ["Network & internet", "Connected devices", "Apps", "Notifications",
              "Battery", "Storage", "Sound & vibration"],
}
_FRAME13 = {
    "gaps": [157.0, 169.0, 294.0, 151.0, 326.0],
    "ys": [171.0, 328.0, 497.0, 791.0, 942.0, 1268.0],
    "hs": [30.0, 37.0, 68.0, 39.0, 27.0, 67.0],
    "xs": [127.0, 128.0, 126.0, 131.0, 128.0, 124.0],
    "texts": ["Sound & vibration", "Display", "Wallpaper", "Accessibility",
              "Security & privacy", "Location"],
}


def _goodframe_anchors(g: dict) -> list[dict]:
    return [
        _row(f"candidate_{i}", t, x, cy - h / 2, cy + h / 2)
        for i, (t, cy, h, x) in enumerate(zip(g["texts"], g["ys"], g["hs"], g["xs"]))
    ]


def _uniform_list_result(cands: list[dict]) -> tuple[str, list[dict]]:
    """Run uniform-list over a deep copy; return (status, surviving candidates
    rendered deterministically)."""
    import copy
    work = copy.deepcopy(cands)
    decision = apply_uniform_list_grouping_params(work, [])
    rendered = [
        {"id": c["id"], "type": c["type"], "text": c["text"],
         "cy": round(float(c["centerPx"][1]), 2)}
        for c in work
    ]
    return decision["status"], rendered


# ─────────────────────────────────────────────────────────────────────────────
# Fix #3 — cadence consensus
# ─────────────────────────────────────────────────────────────────────────────


class CadenceConsensusTests(unittest.TestCase):
    """Fix #3: replace the all-gates (``valid == all``) rule with consensus
    (``valid >= max(3, ceil(0.6*len(gaps)))``).  Frame-10's 173px outlier must
    no longer veto the model; genuinely irregular frames still NOOP."""

    def test_frame10_173px_outlier_cadence_survives(self):
        # Frame 10: gaps=[136,461,153,173,134].  The 173px (and 461px) gaps are
        # outliers vs pitch=136, but consensus holds (direct=3, valid=4 >= 3).
        # RED precondition: the current all-gates rule NOOPs this frame.
        status, _ = _uniform_list_result(_frame10_anchors())
        self.assertEqual(
            status, "activated",
            "cadence consensus must let frame-10-type geometry (173px outlier) "
            "activate uniform-list instead of failing closed",
        )

    def test_consensus_threshold_math(self):
        # The frozen threshold is max(3, ceil(0.6*len(gaps))).
        self.assertEqual(max(3, math.ceil(0.6 * 5)), 3)  # frame 10 / 13
        self.assertEqual(max(3, math.ceil(0.6 * 6)), 4)  # frame 4

    def test_good_frame4_still_activates(self):
        status, _ = _uniform_list_result(_goodframe_anchors(_FRAME4))
        self.assertEqual(status, "activated")

    def test_good_frame13_still_activates(self):
        status, _ = _uniform_list_result(_goodframe_anchors(_FRAME13))
        self.assertEqual(status, "activated")

    def test_genuinely_irregular_frame_fails_closed(self):
        # Consensus insufficient: gaps=[120,180,50,170] -> median(lower60%)
        # =120, direct=1 (<2) AND few valid -> fail-closed noop under BOTH the
        # current all-gates rule and the new consensus rule; no fabricated rows.
        ys = [100.0, 220.0, 400.0, 450.0, 620.0]
        anchors = [_row(f"c{i}", f"Row {i}", 127.0, y - 14, y + 14) for i, y in enumerate(ys)]
        status, rendered = _uniform_list_result(anchors)
        self.assertEqual(status, "noop")
        self.assertEqual(
            [c["text"] for c in rendered],
            [f"Row {i}" for i in range(5)],
            "fail-closed must leave the anchors untouched",
        )

    def test_fewer_than_two_direct_still_noop(self):
        # direct < 2 -> fail-closed (frozen guard unchanged).
        ys = [100.0, 125.0, 160.0, 200.0]
        anchors = [_row(f"c{i}", f"Row {i}", 127.0, y - 14, y + 14) for i, y in enumerate(ys)]
        status, _ = _uniform_list_result(anchors)
        self.assertEqual(status, "noop")


# ─────────────────────────────────────────────────────────────────────────────
# Fix #1 — row-band ownership merge
# ─────────────────────────────────────────────────────────────────────────────


class RowBandOwnershipMergeTests(unittest.TestCase):
    """Fix #1: a relation-head band row absorbs the same-band unresolved title
    text_block (pure geometry) -> one row, one representation.  RED: the
    current output keeps BOTH the text_block and the band row."""

    def _frame10_dual_emission_candidates(self) -> list[dict]:
        cands = _frame10_anchors()
        # relation-head composed two band rows at the same physical rows as
        # the unresolved title text_blocks (frame-10 evidence).
        cands.append(_band_row("relation_head_band_5", "Sound & vibration", 127.0, 531.0, 559.0))
        cands.append(_band_row("relation_head_band_6", "Display", 127.0, 684.0, 721.0))
        cands.append(_C10)  # text_block 'Sound & vibration' @ centerY 545
        cands.append(_C12)  # text_block 'Display' @ centerY 702
        cands.append(_C13)  # subtitle 'Dark theme…' below Display
        return cands

    def test_frame10_dual_emission_becomes_one_row_one_type(self):
        cands = self._frame10_dual_emission_candidates()
        supporting = _assign(cands)
        # Band rows remain menu_items, one per row; the same-band text_blocks
        # are absorbed as supporting and no longer emitted independently.
        rows = [c for c in cands if c.get("type") == "menu_item"]
        texts_by_row = [c["text"] for c in rows]
        # 'Sound & vibration' and 'Display' each appear exactly once as a row.
        self.assertEqual(texts_by_row.count("Sound & vibration"), 1)
        self.assertEqual(texts_by_row.count("Display"), 1)
        # No unresolved interaction text_block remains for the band rows.
        self.assertNotIn("Sound & vibration", [c.get("text") for c in cands if c.get("type") == "text_block"])
        self.assertNotIn("Display", [c.get("text") for c in cands if c.get("type") == "text_block"])
        # The absorbed text_blocks are annotated as supporting (not deleted).
        roles = [s.get("role") for s in supporting]
        self.assertIn("row_band_supporting", roles)
        self.assertGreaterEqual(
            sum(1 for s in supporting if s.get("role") == "row_band_supporting"),
            2,
            "both 'Sound & vibration' and 'Display' text_blocks must be absorbed",
        )

    def test_same_text_across_different_bands_stays_fail_closed(self):
        # A text_block whose TEXT matches a band but whose GEOMETRY is NOT in
        # that band's y-extent is a different band occurrence -> stays
        # unresolved (no text-matching merge; pure geometry).
        cands = _frame10_anchors()
        # Place the same-text text_block in an inter-anchor gap where it is NOT
        # directly below any visible parent (gap >> 0.8*parent height), so the
        # only thing it shares with the band is its TEXT — geometry keeps it
        # fail-closed (no text-matching merge).
        distant = _row("candidate_90", "Sound & vibration", 127.0, 600.0, 630.0, kind="text_block")
        cands.append(_band_row("relation_head_band_5", "Sound & vibration", 127.0, 531.0, 559.0))
        cands.append(distant)
        supporting = _assign(cands)
        self.assertNotIn(
            "candidate_90", [s.get("id") for s in supporting],
            "same text at a different band's geometry must NOT be absorbed",
        )
        self.assertTrue(
            any(c.get("id") == "candidate_90" and c.get("type") == "text_block" for c in cands),
            "the non-co-located text_block stays as an unresolved element",
        )


# ─────────────────────────────────────────────────────────────────────────────
# Fix #2 — subtitle ownership
# ─────────────────────────────────────────────────────────────────────────────


class SubtitleOwnershipTests(unittest.TestCase):
    """Fix #2: a title-column text_block directly below a visible parent row
    (gap <= 0.8*parent height, different text) attaches as supporting; a
    clipped top/bottom edge orphan with no attachable parent is
    edge-clipped supporting; otherwise it stays unresolved (never guessed)."""

    def test_dark_theme_subtitle_attaches_to_display_row(self):
        # Frame-10 'Dark theme, font size, brightness' is directly below the
        # Display row (gap=7px <= 0.8*~37px row height), different text.
        cands = _frame10_anchors()
        cands.append(_row("candidate_12", "Display", 127.0, 684.0, 721.0))
        cands.append(_C13)
        supporting = _assign(cands)
        dark = [s for s in supporting if s.get("id") == "candidate_13"]
        self.assertEqual(len(dark), 1)
        self.assertEqual(dark[0]["role"], "row_subtitle_supporting")
        self.assertEqual(dark[0]["parentText"], "Display")
        # The subtitle is no longer an independent unresolved element.
        self.assertFalse(
            any(c.get("id") == "candidate_13" for c in cands),
            "the attached subtitle must not be emitted as an unresolved element",
        )

    def test_frame7_top_clipped_orphan_becomes_edge_supporting(self):
        # Frame-7 'Bluetooth, pairing' at y=[146,172] (top of viewport) has no
        # attachable visible parent -> edge-clipped supporting, NOT unresolved.
        cands = [_BLUETOOTH_PAIRING]
        cands.append(_row("candidate_7", "Apps", 128.0, 408.0, 440.0))
        supporting = _assign(cands)
        edge = [s for s in supporting if s.get("id") == "candidate_6"]
        self.assertEqual(len(edge), 1)
        self.assertEqual(edge[0]["role"], "edge_clipped_supporting")
        self.assertFalse(
            any(c.get("id") == "candidate_6" for c in cands),
            "the edge-clipped orphan must not remain an unresolved element",
        )

    def test_non_edge_unattachable_orphan_stays_unresolved(self):
        # A text_block not directly below any visible parent and not at a
        # clipped edge stays unresolved (fail-closed — never guessed).
        cands = _frame10_anchors()
        outsider = _row(
            "candidate_50", "Standalone note", 127.0, 700.0, 730.0,
            kind="text_block", width=200,
        )
        cands.append(outsider)
        supporting = _assign(cands)
        self.assertTrue(
            all(s.get("id") != "candidate_50" for s in supporting),
            "an unattachable non-edge orphan must not be attached/edge-marked",
        )
        self.assertTrue(
            any(c.get("id") == "candidate_50" and c.get("type") == "text_block" for c in cands),
            "the non-edge unattachable orphan stays unresolved",
        )


# ─────────────────────────────────────────────────────────────────────────────
# Fix #3 — duplicate section-label representation dedup (P26-V2 run 6
# residual 2)
# ─────────────────────────────────────────────────────────────────────────────


def _section_label_satellite(cid: str, text: str, x1: float, y1: float, y2: float, width: float) -> dict:
    """A NonInteractive section_label satellite (the operator's label-height
    demotion output — the role-decided representation of the label line)."""
    sat = _row(cid, text, x1, y1, y2, kind="NonInteractive", width=width)
    sat["role"] = "section_label"
    evidence = dict(sat["evidence"])
    evidence["typeInferred"] = "label_height_rule"
    sat["evidence"] = evidence
    return sat


class DuplicateSectionLabelTests(unittest.TestCase):
    """Fix #3: a text_block that coincides with an EXISTING NonInteractive
    section_label satellite (same normalized text, same title column,
    vertical overlap) is a second representation of the SAME physical line
    (the raw twin from initial construction — run 6 seq 27+ 'Color' as both
    text_block and NonInteractive) and is absorbed.  RED: the current output
    keeps both representations.

    Deliberately NOT a general geometric label-above-row attachment — the
    frozen S1 corpus case ``uniform_list_ambiguous_midpoint_rejected``
    (two distinct short texts in one cadence slot, which must stay
    unresolved) is geometrically indistinguishable from a label above its
    row; only the operator's role-decided satellite separates a duplicate
    representation from a genuine unresolved element."""

    # Real run-2/6 Display-page geometry: 'Color' label y=[792,809] h=17
    # above the 'Colors' row y=[861,885] h=24.
    _TWIN = _row("candidate_1", "Color", 43.0, 792.0, 809.0, kind="text_block", width=58)
    _SATELLITE = _section_label_satellite(
        "relation_head_band_1_sat_label_0", "Color", 43.0, 792.0, 809.0, width=58)
    _COLORS_ROW = _row("relation_head_band_1", "Colors", 44.0, 861.0, 885.0)

    def test_color_twin_duplicate_of_satellite_is_absorbed(self):
        cands = [self._TWIN, self._SATELLITE, self._COLORS_ROW]
        supporting = _assign(cands)
        rec = [s for s in supporting if s.get("id") == "candidate_1"]
        self.assertEqual(len(rec), 1)
        self.assertEqual(rec[0]["role"], "duplicate_section_label_supporting")
        self.assertEqual(rec[0]["parentId"], self._SATELLITE["id"])
        self.assertFalse(
            any(c.get("id") == "candidate_1" for c in cands),
            "the raw twin must not remain an independent element",
        )
        # The role-decided representation and the row both remain.
        self.assertTrue(any(c.get("id") == self._SATELLITE["id"] for c in cands))
        self.assertTrue(any(c.get("id") == self._COLORS_ROW["id"] for c in cands))

    def test_ambiguous_pair_without_satellite_stays_unresolved(self):
        # The S1 corpus falsifier: two distinct short texts in one cadence
        # slot, NO section_label satellite — must stay unresolved (a general
        # geometric label rule would wrongly absorb them into the row below).
        pair_a = _row("candidate_a", "Candidate A", 120.0, 275.0, 295.0, kind="text_block", width=140)
        pair_b = _row("candidate_b", "Candidate B", 122.0, 305.0, 325.0, kind="text_block", width=143)
        row_below = _row("candidate_3", "Confirmed 3", 120.0, 385.0, 415.0)
        cands = [pair_a, pair_b, row_below]
        supporting = _assign(cands)
        self.assertEqual(supporting, [])
        self.assertTrue(
            any(c.get("id") == "candidate_a" and c.get("type") == "text_block" for c in cands)
            and any(c.get("id") == "candidate_b" and c.get("type") == "text_block" for c in cands),
            "the ambiguous pair stays unresolved (fail-closed — never guessed)",
        )

    def test_different_text_satellite_not_matched(self):
        satellite = _section_label_satellite(
            "sat_other", "Appearance", 43.0, 792.0, 809.0, width=90)
        cands = [self._TWIN, satellite, self._COLORS_ROW]
        supporting = _assign(cands)
        self.assertTrue(all(s.get("id") != "candidate_1" for s in supporting))

    def test_same_text_different_position_not_matched(self):
        # Same text but a DIFFERENT physical line (no vertical overlap):
        # text-matching alone never merges (mirror of fix #1's discipline).
        distant_satellite = _section_label_satellite(
            "sat_far", "Color", 43.0, 1200.0, 1217.0, width=58)
        cands = [self._TWIN, distant_satellite, self._COLORS_ROW]
        supporting = _assign(cands)
        self.assertTrue(all(s.get("id") != "candidate_1" for s in supporting))
        self.assertTrue(
            any(c.get("id") == "candidate_1" and c.get("type") == "text_block" for c in cands),
            "a non-co-located same-text twin stays unresolved",
        )

    def test_column_mismatch_not_matched(self):
        # Same text and vertical extent but a DIFFERENT column (icon-side
        # caption vs title-column label): not the same physical line.
        off_column_satellite = _section_label_satellite(
            "sat_offcol", "Color", 400.0, 792.0, 809.0, width=58)
        cands = [self._TWIN, off_column_satellite, self._COLORS_ROW]
        supporting = _assign(cands)
        self.assertTrue(all(s.get("id") != "candidate_1" for s in supporting))


# ─────────────────────────────────────────────────────────────────────────────
# Good-frame invariants
# ─────────────────────────────────────────────────────────────────────────────


class GoodFrameSnapshotTests(unittest.TestCase):
    """Frames 4/13 must produce byte-identical uniform-list output before and
    after the fix (golden baselines captured from UNMODIFIED production)."""

    def test_frame4_output_unchanged(self):
        status, rendered = _uniform_list_result(_goodframe_anchors(_FRAME4))
        self.assertEqual(status, "activated")
        self.assertEqual(
            rendered,
            [
                {"id": f"candidate_{i}", "type": "menu_item", "text": t,
                 "cy": round(float(cy), 2)}
                for i, (t, cy, h, x) in enumerate(
                    zip(_FRAME4["texts"], _FRAME4["ys"], _FRAME4["hs"], _FRAME4["xs"])
                )
            ],
            "frame-4 uniform-list output must be byte-identical after the fix",
        )

    def test_frame13_output_unchanged(self):
        status, rendered = _uniform_list_result(_goodframe_anchors(_FRAME13))
        self.assertEqual(status, "activated")
        self.assertEqual(
            rendered,
            [
                {"id": f"candidate_{i}", "type": "menu_item", "text": t,
                 "cy": round(float(cy), 2)}
                for i, (t, cy, h, x) in enumerate(
                    zip(_FRAME13["texts"], _FRAME13["ys"], _FRAME13["hs"], _FRAME13["xs"])
                )
            ],
            "frame-13 uniform-list output must be byte-identical after the fix",
        )

    def test_good_frame_assembly_is_pure_noop(self):
        # Fixes #1/#2 must not touch good frames (no text_blocks to absorb /
        # attach / edge-mark) -> the assembly function is a no-op.
        for g in (_FRAME4, _FRAME13):
            with self.subTest(gaps=g["gaps"]):
                cands = _goodframe_anchors(g)
                before = [dict(c) for c in cands]
                supporting = _assign(cands)
                self.assertEqual(supporting, [])
                self.assertEqual(cands, before)


if __name__ == "__main__":
    unittest.main()
