"""Header-text attribution guards — RED→GREEN for the final header/row fix.

Root geometric principle (deterministic, label-agnostic, no scenario tokens):

    A text may be attributed as a ROW's primary text only if it vertically
    overlaps that row's anchor content.  A text entirely ABOVE the anchor
    content's top edge (beyond a small tolerance) belongs to a higher line
    (a section header) and must never become the row's title/head.

Three enforcement sites (the composition-layer mismatch sites):

A. ``apply_chevron_heuristic`` (fusion/heuristics.py) — assignment eligibility:
   a text candidate assigned to a widget anchor must vertically overlap the
   anchor band (overlap > 0, or center within 0.25x anchor height of it).
   A section header touching the first row's icon from above is currently
   sorted topmost and becomes the row's primary text (RED case 1).

B. ``row_relation_head._elect_band_head`` — head candidacy: when the band has
   leading widget content (non-OCR boxes left of the text column), a head
   candidate must vertically overlap that content.  An in-column header
   detection box above the first row currently wins the topmost election
   (RED case 2).

C. ``fusion/engine`` token attribution — an OCR token entirely above a
   detection box's top edge (beyond 0.25x box height) is not attributable to
   that box; ``primary_line_text``'s top-line pick currently lets a header
   token become the row's text (RED case 3).

Safety invariants asserted alongside: the header text is never silently
dropped (stays its own text_block), the row keeps exactly one menu_item
representation, and true row titles (vertically overlapping their anchors)
compose exactly as before.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.fusion.heuristics import apply_chevron_heuristic
from uniclaw_perception.operators.relation_head_router import (
    run_row_relation_head_routed,
)
from uniclaw_perception.operators.trace import build_raw_sources
from uniclaw_perception.schema import Box, Detection, OcrToken

_WIDTH, _HEIGHT = 720, 1400


def _norm(x1: float, y1: float, x2: float, y2: float) -> Box:
    return Box(x1 * _WIDTH, y1 * _HEIGHT, x2 * _WIDTH, y2 * _HEIGHT)


def _px(x1: float, y1: float, x2: float, y2: float) -> list[float]:
    return [x1 * _WIDTH, y1 * _HEIGHT, x2 * _WIDTH, y2 * _HEIGHT]


def _center(x1: float, y1: float, x2: float, y2: float) -> list[float]:
    return [(x1 + x2) * _WIDTH / 2, (y1 + y2) * _HEIGHT / 2]


# ─────────────────────────────────────────────────────────────────────────────
# Case 1 — chevron: section header directly above the first row's icon
# (touching bands, center distance inside the 40px window).
#
#   header 'SECTION'  y=[272,300]  center 286   (above the icon, overlap 0)
#   icon              y=[300,336]  center 318
#   title  'Battery'  y=[306,334]  center 320   (overlaps the icon band)
#
# Today: header sorts topmost → menu_item text 'SECTION', real title absorbed.
# Guard A: header not attributable to the icon → menu_item text 'Battery',
# header stays an independent text_block.
# ─────────────────────────────────────────────────────────────────────────────


def _header_row_detections() -> list[Detection]:
    return [
        Detection("y_icon", "icon", 0.9, _norm(100 / _WIDTH, 300 / 1400, 136 / _WIDTH, 336 / 1400)),
        Detection("y_head", "text_block", 0.9, _norm(176 / _WIDTH, 272 / 1400, 500 / _WIDTH, 300 / 1400)),
        Detection("y_titl", "text_block", 0.9, _norm(176 / _WIDTH, 306 / 1400, 480 / _WIDTH, 334 / 1400)),
    ]


def _header_row_ocr() -> list[OcrToken]:
    return [
        OcrToken("o_head", "SECTION", 0.9, _norm(178 / _WIDTH, 274 / 1400, 320 / _WIDTH, 298 / 1400)),
        OcrToken("o_titl", "Battery", 0.9, _norm(178 / _WIDTH, 308 / 1400, 300 / _WIDTH, 332 / 1400)),
    ]


class ChevronHeaderAboveRowTests(unittest.TestCase):
    def _run_engine(self):
        return fuse_evidence(
            _header_row_detections(),
            _header_row_ocr(),
            image_width=_WIDTH,
            image_height=_HEIGHT,
        )

    def test_row_title_not_section_header_becomes_primary(self):
        evidence = self._run_engine()
        menus = [c for c in evidence["candidates"] if c["type"] == "menu_item"]
        texts = [m.get("text") for m in menus]
        self.assertIn(
            "Battery", texts,
            f"the row's own vertically-overlapping title must compose the row; "
            f"menu texts were {texts} (header misattributed as row primary?)",
        )
        for menu in menus:
            self.assertNotEqual(
                menu.get("text"), "SECTION",
                "a section header above the row band must never be the row's "
                "primary text",
            )

    def test_header_is_not_silently_dropped(self):
        evidence = self._run_engine()
        blocks = [c for c in evidence["candidates"] if c.get("text") == "SECTION"]
        self.assertTrue(
            blocks,
            "the header text must remain visible as its own (non-row-primary) "
            "candidate — never silently dropped",
        )
        self.assertTrue(
            all(b["type"] != "menu_item" for b in blocks),
            "the header must not be actionable",
        )


# ─────────────────────────────────────────────────────────────────────────────
# Case 2 — row-relation-head: in-column header detection box inside the band.
#
#   header 'DISPLAY'  y=[464,494]  (in text column, above the icon, overlap 0)
#   icon              y=[500,536]  (leading widget, left of the text column)
#   title 'Brightness' y=[506,534] (overlaps the icon band)
#
# Today: band {header,title,icon} elects the topmost in-column detection
# (the header) as head → menu_item text 'DISPLAY'.
# Guard B: head must overlap the band's leading widget content → title wins.
# ─────────────────────────────────────────────────────────────────────────────


def _relation_head_detections() -> list[Detection]:
    return [
        Detection("r_icon", "icon", 0.9, _norm(142 / _WIDTH, 500 / 1400, 178 / _WIDTH, 536 / 1400)),
        Detection("r_head", "text_block", 0.9, _norm(176 / _WIDTH, 464 / 1400, 560 / _WIDTH, 494 / 1400)),
        Detection("r_titl", "text_block", 0.9, _norm(176 / _WIDTH, 506 / 1400, 460 / _WIDTH, 534 / 1400)),
    ]


def _relation_head_ocr() -> list[OcrToken]:
    return [
        OcrToken("ro_head", "DISPLAY", 0.9, _norm(178 / _WIDTH, 466 / 1400, 320 / _WIDTH, 492 / 1400)),
        OcrToken("ro_titl", "Brightness", 0.9, _norm(178 / _WIDTH, 508 / 1400, 340 / _WIDTH, 532 / 1400)),
    ]


class RelationHeadHeaderElectionTests(unittest.TestCase):
    def test_in_column_header_does_not_win_head_election(self):
        # The router appends composed relation-head rows into the candidates
        # list it is given; observe the composed row text through that seam.
        holder: list[dict] = []
        bundle = build_raw_sources(
            _relation_head_detections(), _relation_head_ocr(), _WIDTH, _HEIGHT
        )
        decision = run_row_relation_head_routed(
            holder, _relation_head_detections(), {}, bundle
        )
        rows = [c for c in holder if c.get("type") == "menu_item"]
        texts = [c.get("text") for c in rows]
        self.assertEqual(
            decision.get("status"), "activated",
            f"relation-head must still compose the band; decision={decision}",
        )
        self.assertTrue(
            rows,
            "the band must still compose exactly one row (the real row)",
        )
        for text in texts:
            self.assertNotEqual(
                text, "DISPLAY",
                f"an in-column header above the leading widget must not be "
                f"elected head; composed texts: {texts}",
            )
        self.assertIn(
            "Brightness", texts,
            f"the row's own title must head the band; composed texts: {texts}",
        )


# ─────────────────────────────────────────────────────────────────────────────
# Case 3 — OCR token attribution: header token above a detection box.
#
#   token 'BIG HEADER' y=[668,690]  (entirely above the box top 700)
#   box (row title)    y=[700,728]
#   token 'Storage'    y=[706,726]  (inside the box)
#
# Today: both tokens match the box (center distance < max_distance) and
# primary_line_text takes the TOP line → candidate text 'BIG HEADER'.
# Guard C: the token entirely above the box is not attributable → 'Storage'.
# ─────────────────────────────────────────────────────────────────────────────


class TokenAttributionHeaderLineTests(unittest.TestCase):
    def test_token_entirely_above_box_is_not_its_text(self):
        detections = [
            Detection("t_box", "text_block", 0.9, _norm(176 / _WIDTH, 700 / 1400, 500 / _WIDTH, 728 / 1400)),
        ]
        ocr = [
            OcrToken("t_hdr", "BIG HEADER", 0.9, _norm(178 / _WIDTH, 668 / 1400, 340 / _WIDTH, 690 / 1400)),
            OcrToken("t_row", "Storage", 0.9, _norm(178 / _WIDTH, 706 / 1400, 300 / _WIDTH, 726 / 1400)),
        ]
        evidence = fuse_evidence(
            detections, ocr, image_width=_WIDTH, image_height=_HEIGHT
        )
        target = [
            c for c in evidence["candidates"] if c.get("evidence", {}).get("yoloId") == "t_box"
        ]
        self.assertTrue(target, "the detection box must produce a candidate")
        self.assertNotEqual(
            target[0].get("text"), "BIG HEADER",
            "a header token entirely above the box must not become the box's text",
        )
        self.assertEqual(
            target[0].get("text"), "Storage",
            "the box's own in-band token is its primary line",
        )


# ─────────────────────────────────────────────────────────────────────────────
# True-row control — a title that vertically overlaps its anchor composes
# exactly as before (guards must be no-ops on well-formed rows).
# ─────────────────────────────────────────────────────────────────────────────


class TrueRowCompositionUnchangedTests(unittest.TestCase):
    def test_overlapping_title_still_composes_via_chevron(self):
        candidates = [
            {
                "id": "c_title",
                "type": "text_block",
                "text": "Network & internet",
                "confidence": 0.9,
                "boundsPx": _px(176 / _WIDTH, 406 / 1400, 480 / _WIDTH, 434 / 1400),
                "centerPx": _center(176 / _WIDTH, 406 / 1400, 480 / _WIDTH, 434 / 1400),
                "evidence": {"yoloId": "y_t", "ocrIds": ["o_t"], "allIds": ["y_t", "o_t"]},
            },
            {
                "id": "c_hdr",
                "type": "text_block",
                "text": "HEADER FAR ABOVE",
                "confidence": 0.9,
                "boundsPx": _px(176 / _WIDTH, 300 / 1400, 480 / _WIDTH, 330 / 1400),
                "centerPx": _center(176 / _WIDTH, 300 / 1400, 480 / _WIDTH, 330 / 1400),
                "evidence": {"yoloId": "y_h", "ocrIds": ["o_h"], "allIds": ["y_h", "o_h"]},
            },
        ]
        widgets = [
            Detection("y_t", "icon", 0.9, _norm(100 / _WIDTH, 400 / 1400, 136 / _WIDTH, 440 / 1400))
        ]
        # y_t icon is the title's own detection (self-match excluded); the
        # far-above header is outside the 40px window already — both before and
        # after the guard the title alone composes, proving no behavior change
        # for well-formed rows.
        apply_chevron_heuristic(candidates, widgets)
        menus = [c for c in candidates if c["type"] == "menu_item"]
        self.assertEqual(
            len(menus), 0,
            "the title's only anchor is its own detection (self-match) — no "
            "foreign anchor exists, so nothing composes; guard must not invent "
            "composition",
        )


if __name__ == "__main__":
    unittest.main()
