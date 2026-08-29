"""Column-aligned text_block -> menu_item type promotion (WI-S2fix6).

Locks the perception type-consistency repair for the E4 diagnosis: the same
Settings row (e.g. ``Sound & vibration``) is sensed as ``menu_item`` when
``row-relation-head`` composes it but as ``text_block`` when composition
fails, so the downstream ``Text|Type`` signature is unstable and the
normalizer overlap chain breaks.  The final assembly step
``_promote_column_aligned_text_blocks`` retypes a ``text_block`` to
``menu_item`` when it sits on the composed menu_items' modal x1 column, is
NOT a caption/satellite of a composed row, and is NOT bound to an existing
menu_item line — only the ``type`` field changes (text/bounds/confidence
untouched).

Acceptance (leader-locked):
  (1) text_block at menu_item column + wide box -> promoted to menu_item
  (2) text_block at a DIFFERENT column (x1 offset > tolerance) -> NOT promoted
  (3) a caption/satellite text_block (narrow, or a satellite text duplicate)
      -> NOT promoted
  (4) no menu_items in frame -> no promotion (no column reference)
  (5) multiple text_blocks at the column -> all promoted
  (6) determinism: double-run byte-identical

Plus the two guards that keep the S1 equivalence baseline byte-green:
  * promotion is scoped to the relation-head composition path (a
    ``row_relation_head`` menu_item must exist); when relation-head delegated
    to uniform-list (only ``row_composition`` menu_items), its fail-closed
    rejections are NOT overridden;
  * a text_block sharing a visual line with an existing menu_item is never
    retyped (no second navigation candidate on one physical line).
"""
from __future__ import annotations

import unittest

from uniclaw_perception.fusion.engine import (
    _promote_column_aligned_text_blocks,
    fuse_evidence,
)
from uniclaw_perception.schema import Box, Detection, OcrToken

_W, _H = 1000, 1000


def _candidate(
    identifier: str,
    type_: str,
    text: str,
    confidence: float,
    box: tuple[float, float, float, float],
    *,
    type_inferred: str = "",
) -> dict:
    """Build a minimal fusion-shaped candidate (normalized bounds + pixels).

    ``type_inferred`` populates ``evidence.typeInferred`` so a menu_item can
    be marked ``row_relation_head`` (relation-head composed) or
    ``row_composition`` (uniform-list composed) — the promotion discriminator.
    """
    x1, y1, x2, y2 = box
    return {
        "id": identifier,
        "type": type_,
        "text": text,
        "confidence": confidence,
        "bounds": {
            "x1": round(x1 / _W, 6),
            "y1": round(y1 / _H, 6),
            "x2": round(x2 / _W, 6),
            "y2": round(y2 / _H, 6),
        },
        "boundsPx": [x1, y1, x2, y2],
        "evidence": {"typeInferred": type_inferred},
    }


def _rh_menu(identifier: str, text: str, box: tuple[float, float, float, float]) -> dict:
    """A relation-head-composed menu_item (the E4 defect class anchor)."""
    return _candidate(identifier, "menu_item", text, 0.9, box,
                      type_inferred="row_relation_head")


def _ul_menu(identifier: str, text: str, box: tuple[float, float, float, float]) -> dict:
    """A uniform-list-composed menu_item (relation-head delegated)."""
    return _candidate(identifier, "menu_item", text, 0.9, box,
                      type_inferred="row_composition")


def _text_block(identifier: str, text: str, box: tuple[float, float, float, float],
                confidence: float = 0.8) -> dict:
    return _candidate(identifier, "text_block", text, confidence, box)


# Two relation-head menu_items on the modal column x1=170px (0.17), full row
# width 300px, on their own lines.
_RH_MENUS = [
    _rh_menu("m1", "Wallpaper", (170, 100, 470, 120)),
    _rh_menu("m2", "Accessibility", (170, 200, 470, 220)),
]


class TypePromotionUnitTests(unittest.TestCase):
    """Direct pass-level assertions over ``_promote_column_aligned_text_blocks``."""

    # (1) text_block at the menu_item column + wide box -> promoted.
    def test_column_aligned_wide_text_block_promoted(self):
        candidates = list(_RH_MENUS) + [_text_block("t1", "Sound & vibration",
                                                    (170, 300, 470, 320))]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(
            [c["type"] for c in candidates if c["id"] == "t1"], ["menu_item"],
            "the column-aligned wide text_block must be retyped to menu_item",
        )
        self.assertEqual(promoted, [{"id": "t1", "text": "Sound & vibration"}])
        # Only the type field changes: text/bounds/confidence untouched.
        t1 = next(c for c in candidates if c["id"] == "t1")
        self.assertEqual(t1["text"], "Sound & vibration")
        self.assertEqual(t1["boundsPx"], [170, 300, 470, 320])
        self.assertEqual(t1["confidence"], 0.8)

    # (2) text_block at a DIFFERENT column (offset > tolerance) -> NOT promoted.
    def test_different_column_text_block_not_promoted(self):
        # x1=300px (0.30): offset from modal 0.17 is 0.13 >> tolerance 0.03.
        candidates = list(_RH_MENUS) + [_text_block("t1", "Far column",
                                                    (300, 300, 600, 320))]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(promoted, [])
        self.assertEqual(
            [c["type"] for c in candidates if c["id"] == "t1"], ["text_block"],
            "a text_block off the menu_item column must stay text_block",
        )

    # (3a) a NARROW text_block at the column (a caption/subtitle shape) is NOT
    # promoted — titles are as wide as their row; a narrow box is a caption.
    def test_narrow_caption_text_block_not_promoted(self):
        # avg menu width = 300px; 60% = 180px. A 100px box is a caption.
        candidates = list(_RH_MENUS) + [_text_block("cap", "subtitle line",
                                                    (170, 300, 270, 320))]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(promoted, [])
        self.assertEqual(
            [c["type"] for c in candidates if c["id"] == "cap"], ["text_block"],
            "a narrow caption-width text_block must not be promoted to menu_item",
        )

    # (3b) a text_block whose text exactly matches an emitted relation-head
    # satellite (a caption duplicate) is NOT promoted.
    def test_satellite_text_duplicate_not_promoted(self):
        satellite = {
            "id": "m1_sat_0", "type": "NonInteractive",
            "text": "Set wallpaper, style", "confidence": 0.5,
            "bounds": {"x1": 0.17, "y1": 0.30, "x2": 0.47, "y2": 0.32},
            "boundsPx": [170, 300, 470, 320],
            "evidence": {"typeInferred": "row_relation_head_satellite",
                         "headId": "m1"},
        }
        candidates = list(_RH_MENUS) + [
            _text_block("dup", "Set wallpaper, style", (170, 300, 470, 320)),
            satellite,
        ]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(promoted, [])
        self.assertEqual(
            [c["type"] for c in candidates if c["id"] == "dup"], ["text_block"],
            "a text_block duplicating an emitted satellite's text must not be "
            "promoted (it is a caption duplicate, not a row title)",
        )

    # (4) no menu_items in frame -> no promotion (no column reference).
    def test_no_menu_items_no_promotion(self):
        candidates = [
            _text_block("t1", "Alpha", (170, 100, 470, 120)),
            _text_block("t2", "Beta", (170, 200, 470, 220)),
        ]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(promoted, [])
        self.assertTrue(all(c["type"] == "text_block" for c in candidates))

    # (5) multiple text_blocks at the column -> all promoted.
    def test_multiple_column_text_blocks_all_promoted(self):
        candidates = list(_RH_MENUS) + [
            _text_block("t1", "Sound & vibration", (170, 300, 470, 320)),
            _text_block("t2", "Display", (170, 400, 470, 420)),
            _text_block("t3", "Battery", (170, 500, 470, 520)),
        ]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(
            {p["id"] for p in promoted}, {"t1", "t2", "t3"},
            "all column-aligned wide text_blocks must be promoted",
        )
        self.assertTrue(
            all(c["type"] == "menu_item" for c in candidates if c["id"] in {"t1", "t2", "t3"}),
        )

    # (6) determinism: two independent passes produce identical results.
    def test_double_run_unit_identical(self):
        def run():
            candidates = list(_RH_MENUS) + [
                _text_block("t1", "Sound & vibration", (170, 300, 470, 320)),
                _text_block("t2", "Display", (170, 400, 470, 420)),
            ]
            promoted = _promote_column_aligned_text_blocks(candidates)
            return promoted, [c["type"] for c in candidates]
        self.assertEqual(run(), run())

    # Guard: a text_block sharing a visual line with an existing menu_item is
    # never retyped (no second navigation candidate on one physical line).
    def test_text_block_on_menu_item_line_not_promoted(self):
        # t1 overlaps m2's line (y=[300,320] vs m2 y=[200,220] -> adjacent gap
        # 80px > shorter 20px: NOT same line). Use a genuine overlap instead.
        candidates = list(_RH_MENUS) + [
            _text_block("t1", "Wallpaper", (175, 102, 475, 122)),  # same line as m1
        ]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(promoted, [])
        self.assertEqual(
            [c["type"] for c in candidates if c["id"] == "t1"], ["text_block"],
            "a text_block bound to an existing menu_item line must not become a "
            "second menu_item on that line",
        )

    # Guard: promotion is scoped to the relation-head path. When relation-head
    # delegated to uniform-list (only row_composition menu_items), its
    # fail-closed rejections are NOT overridden — this is what keeps the S1
    # equivalence baseline byte-green.
    def test_uniform_list_only_path_no_promotion(self):
        candidates = [
            _ul_menu("u1", "Confirmed 1", (170, 100, 470, 120)),
            _ul_menu("u2", "Confirmed 2", (170, 200, 470, 220)),
            _text_block("t1", "Rejected row", (170, 300, 470, 320)),
        ]
        promoted = _promote_column_aligned_text_blocks(candidates)
        self.assertEqual(promoted, [])
        self.assertEqual(
            [c["type"] for c in candidates if c["id"] == "t1"], ["text_block"],
            "a column-aligned text_block left by uniform-list (relation-head "
            "delegated) must NOT be promoted — uniform-list owns its fail-closed "
            "rejections and the S1 baseline must not drift",
        )


class TypePromotionEngineTests(unittest.TestCase):
    """End-to-end: the promotion fires through the full ``fuse_evidence`` path.

    The frozen child-page shapes compose relation-head rows (Gallery / Live
    Wallpapers / Wallpaper & style at the menu column ~0.197).  An extra
    text_block at that column is placed in a band that relation-head rejects
    fail-closed (two same-line same-width detections -> ``_REASON_TIE`` -> no
    confident head), so it stays ``text_block`` after the operator pipeline —
    the E4 partial-composition-failure shape.  The final assembly step must
    retype it to ``menu_item`` and surface it in
    ``_diagnostics["typePromotions"]``.
    """

    _WIDTH, _HEIGHT = 1080, 2400

    @staticmethod
    def _norm(x1: float, y1: float, x2: float, y2: float) -> Box:
        return Box(x1 * 1080, y1 * 2400, x2 * 1080, y2 * 2400)

    @classmethod
    def _detections(cls) -> list[Detection]:
        return [
            Detection("y1", "icon", 0.9, cls._norm(0.03, 0.079, 0.09, 0.125)),
            Detection("y2", "text_block", 0.9, cls._norm(0.0638, 0.1825, 0.7666, 0.265)),
            Detection("y3", "text_block", 0.9, cls._norm(0.0611, 0.2325, 0.2430, 0.2656)),
            Detection("y4", "text_block", 0.9, cls._norm(0.1972, 0.3225, 0.3458, 0.345)),
            Detection("y5", "text_block", 0.9, cls._norm(0.1986, 0.3875, 0.5375, 0.4087)),
            Detection("y6", "text_block", 0.9, cls._norm(0.1930, 0.4512, 0.5708, 0.4743)),
            # TIE band at the menu column: two same-line same-width detections
            # -> relation-head fail-closed (_REASON_TIE) -> both stay text_block.
            Detection("y7", "text_block", 0.9, cls._norm(0.197, 0.55, 0.45, 0.572)),
            Detection("y8", "text_block", 0.9, cls._norm(0.197, 0.55, 0.45, 0.572)),
        ]

    @classmethod
    def _ocr(cls) -> list[OcrToken]:
        return [
            OcrToken("o1", "Choose wallpaper", 0.9, cls._norm(0.064, 0.183, 0.767, 0.2225)),
            OcrToken("o2", "from", 0.9, cls._norm(0.061, 0.233, 0.243, 0.266)),
            OcrToken("o3", "Gallery", 0.9, cls._norm(0.197, 0.323, 0.346, 0.345)),
            OcrToken("o4", "Live Wallpapers", 0.9, cls._norm(0.199, 0.388, 0.537, 0.409)),
            OcrToken("o5", "Wallpaper & style", 0.9, cls._norm(0.193, 0.451, 0.571, 0.474)),
            OcrToken("o6", "Display", 0.9, cls._norm(0.205, 0.552, 0.45, 0.57)),
        ]

    def test_uncomposed_column_text_block_promoted_end_to_end(self):
        evidence = fuse_evidence(
            self._detections(), self._ocr(),
            image_width=self._WIDTH, image_height=self._HEIGHT,
        )
        promotions = evidence.get("_diagnostics", {}).get("typePromotions", [])
        self.assertEqual(
            len(promotions), 1,
            f"exactly one text_block must be promoted; got {promotions}",
        )
        self.assertEqual(promotions[0]["text"], "Display")
        # The promoted candidate is now a menu_item with NO row_relation_head
        # provenance (it is a retyped fusion candidate, not a relation-head
        # composition) — its text/bounds are unchanged, only the type moved.
        promoted = next(
            c for c in evidence["candidates"]
            if c.get("text") == "Display" and c["id"] == promotions[0]["id"]
        )
        self.assertEqual(promoted["type"], "menu_item")
        self.assertNotEqual(
            (promoted.get("evidence") or {}).get("typeInferred"), "row_relation_head",
            "the promoted candidate is a retyped fusion text_block, not a "
            "relation-head composition",
        )
        # Relation-head still composed the real rows (the fix does not suppress
        # successful compositions).
        rh_menus = [
            c for c in evidence["candidates"]
            if c["type"] == "menu_item"
            and (c.get("evidence") or {}).get("typeInferred") == "row_relation_head"
        ]
        texts = {c.get("text") for c in rh_menus}
        for expected in ("Gallery", "Live Wallpapers", "Wallpaper & style"):
            self.assertIn(expected, texts)

    # (6) engine determinism: two independent runs are byte-identical.
    def test_engine_double_run_byte_identical(self):
        def run():
            return fuse_evidence(
                self._detections(), self._ocr(),
                image_width=self._WIDTH, image_height=self._HEIGHT,
            )
        self.assertEqual(run(), run(), "double-run must be byte-identical")


if __name__ == "__main__":
    unittest.main()
