"""S2fix4 topmost head-election regression (WI-PFW-S2fix4, OpenSpec change
``perception-operator-rule-framework``).

Locks the frozen REAL mid-viewport geometry (run-5 frame 9, 1080x2400,
normalized bounds reproduced below): the Settings root list where every row is
title-above-caption at the SAME column and every caption is WIDER than its
title.  The former widest-first head rule elected those wider captions as menu
heads ('Volume, vibration, Do Not Disturb', 'Dark theme, font size,
brightness', 'Home, lock screen', 'On / 1 app has access to location',
'38%used-9.97GBfree' all became menu items) — the v1n-class misclassification
recurring on a real frame.

The S2fix4 rule (topmost primary, width as same-line tiebreak) must elect the
8 TITLES as menu heads, absorb every caption + duplicate-detector box as a
NonInteractive satellite, and never promote any caption text.  The raw arrays
are built from the frozen bounds exactly as the operator consumes them
(uncombined detector ``text_block`` boxes + OCR tokens at matching boxes);
rows with two boxes (Sound & vibration OCR dup at the caption offset, Wallpaper
dup box, Accessibility second box, Security & privacy wider dup) carry BOTH
detections with their own OCR tokens to exercise the duplicate handling
honestly.  The 'AD' icon glyph (left of the text column) is OCR-only — an
unanchored band that must fail closed, never a head.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.operators.row_relation_head import record_trace_bytes, run

_W, _H = 1080, 2400


def _px(x1: float, y1: float, x2: float, y2: float) -> list[int]:
    """Normalized frozen bounds → pixel boundsPx (rounded, as real OCR/YOLO)."""
    return [round(x1 * _W), round(y1 * _H), round(x2 * _W), round(y2 * _H)]


def _det(identifier: str, text: str, x1: float, y1: float, x2: float, y2: float) -> dict:
    return {
        "id": identifier, "label": "text_block", "confidence": 0.9,
        "boundsPx": _px(x1, y1, x2, y2),
    }


def _ocr(identifier: str, text: str, x1: float, y1: float, x2: float, y2: float) -> dict:
    return {
        "id": identifier, "text": text, "confidence": 0.99,
        "boundsPx": _px(x1, y1, x2, y2),
    }


#: Frozen run-5 frame 9 rows (title above caption, same column; caption WIDER
#: than title — the trap the widest rule fell into).  Each listed box becomes
#: a detection + its own OCR token; two-box rows carry both detections.
_FROZEN_ROWS: list[dict] = [
    {"title": "Battery", "title_box": (0.182, 0.210, 0.331, 0.232)},          # no caption
    {"title": "Storage", "title_box": (0.172, 0.304, 0.349, 0.329),
     "caption": "38%used-9.97GBfree", "caption_box": (0.175, 0.334, 0.540, 0.347)},
    {"title": "Sound & vibration", "title_box": (0.175, 0.401, 0.558, 0.422),
     "caption": "Volume, vibration, Do Not Disturb",
     "caption_box": (0.172, 0.430, 0.678, 0.446),
     "dup_box_at_caption": (0.175, 0.430, 0.558, 0.446)},                     # OCR dup w/ det
    {"title": "Display", "title_box": (0.171, 0.495, 0.340, 0.523),
     "caption": "Dark theme, font size, brightness",
     "caption_box": (0.179, 0.526, 0.668, 0.541)},
    {"title": "Wallpaper", "title_box": (0.171, 0.592, 0.401, 0.619),
     "caption": "Home, lock screen", "caption_box": (0.175, 0.622, 0.458, 0.637),
     "dup_box_at_caption": (0.171, 0.622, 0.401, 0.637)},                     # dup box w/ det
    {"title": "Accessibility", "title_box": (0.176, 0.699, 0.560, 0.736),
     "dup_box_at_caption": (0.176, 0.718, 0.560, 0.755)},                     # 2nd box, same text
    {"title": "Security & privacy", "title_box": (0.179, 0.787, 0.562, 0.810),
     "dup_box_at_caption": (0.175, 0.797, 0.749, 0.832)},                     # wider dup w/ det
    {"title": "Location", "title_box": (0.171, 0.881, 0.371, 0.905),
     "caption": "On / 1 app has access to location",
     "caption_box": (0.172, 0.911, 0.675, 0.929)},
]

#: The 5 caption texts that the widest rule promoted to menu heads on the real
#: frame — forbidden as heads, must be NonInteractive caption satellites.
_CAPTION_PHRASES = [
    "Volume, vibration, Do Not Disturb",
    "Dark theme, font size, brightness",
    "Home, lock screen",
    "On / 1 app has access to location",
    "38%used-9.97GBfree",
]

_TITLES = [row["title"] for row in _FROZEN_ROWS]


def _frozen_frame() -> tuple[list[dict], list[dict]]:
    detections: list[dict] = []
    ocr_tokens: list[dict] = []
    for i, row in enumerate(_FROZEN_ROWS):
        x1, y1, x2, y2 = row["title_box"]
        detections.append(_det(f"d{i}_title", row["title"], x1, y1, x2, y2))
        ocr_tokens.append(_ocr(f"o{i}_title", row["title"], x1, y1, x2, y2))
        if "caption" in row:
            cx1, cy1, cx2, cy2 = row["caption_box"]
            detections.append(_det(f"d{i}_cap", row["caption"], cx1, cy1, cx2, cy2))
            ocr_tokens.append(_ocr(f"o{i}_cap", row["caption"], cx1, cy1, cx2, cy2))
        if "dup_box_at_caption" in row:
            dx1, dy1, dx2, dy2 = row["dup_box_at_caption"]
            detections.append(_det(f"d{i}_dup", row["title"], dx1, dy1, dx2, dy2))
            ocr_tokens.append(_ocr(f"o{i}_dup", row["title"], dx1, dy1, dx2, dy2))
    # 'AD' icon glyph: OCR-only, left of the text column (icon column) — an
    # unanchored band that must fail closed, never a head.
    ocr_tokens.append(_ocr("ad_ocr", "AD", 0.089, 0.414, 0.125, 0.427))
    return detections, ocr_tokens


def _menu_texts(record: dict) -> list[str]:
    return [candidate["text"] for candidate in record["candidates"]]


def _satellites(record: dict) -> list[dict]:
    return [s for s in record["satellites"] if s["type"] == "NonInteractive"]


class TopmostHeadElectionRegressionTests(unittest.TestCase):
    """S2fix4: the frozen real mid-viewport geometry elects titles, never
    captions, as menu heads."""

    def test_fixture_is_a_true_widest_trap(self):
        # Sanity: in every title+caption row the caption detection is WIDER
        # than the title detection — this fixture discriminates the old
        # widest-first rule (caption head) from topmost (title head).
        for row in _FROZEN_ROWS:
            if "caption" not in row:
                continue
            tx1, ty1, tx2, ty2 = row["title_box"]
            cx1, cy1, cx2, cy2 = row["caption_box"]
            self.assertGreater(
                cx2 - cx1, tx2 - tx1,
                f"{row['title']}: caption must be wider than the title "
                "(the widest-first trap)",
            )

    def test_titles_are_the_eight_menu_heads(self):
        detections, ocr_tokens = _frozen_frame()
        record = run(detections, ocr_tokens, _W, _H)

        self.assertEqual(record["status"], "activated")
        self.assertEqual(record["emitted"], 8)
        self.assertEqual(
            _menu_texts(record), _TITLES,
            "menu heads must be the topmost text-bearing boxes — the 8 titles",
        )
        for candidate in record["candidates"]:
            self.assertEqual(candidate["type"], "menu_item")
            self.assertEqual(
                candidate["evidence"]["typeInferred"], "row_relation_head",
            )

    def test_captions_never_menu_heads_always_satellites(self):
        detections, ocr_tokens = _frozen_frame()
        record = run(detections, ocr_tokens, _W, _H)

        menus = _menu_texts(record)
        for phrase in _CAPTION_PHRASES:
            self.assertNotIn(
                phrase, menus,
                f"{phrase!r} must NEVER be a menu head (v1n-class regression)",
            )
        self.assertNotIn("AD", menus, "the unanchored icon glyph must not be promoted")

        satellites = _satellites(record)
        satellite_texts = [s["text"] for s in satellites]
        for phrase in _CAPTION_PHRASES:
            self.assertIn(
                phrase, satellite_texts,
                f"{phrase!r} must be absorbed as a NonInteractive satellite",
            )
            caption = next(s for s in satellites if s["text"] == phrase)
            self.assertEqual(caption["role"], "caption")
            self.assertEqual(caption["evidence"]["typeInferred"],
                             "row_relation_head_satellite")

    def test_caption_satellite_links_to_its_title_head(self):
        # Provenance: the Storage caption satellite is bound to the Storage
        # band head (band 1), not to any other row.
        detections, ocr_tokens = _frozen_frame()
        record = run(detections, ocr_tokens, _W, _H)
        caption = next(
            s for s in record["satellites"]
            if s["text"] == "38%used-9.97GBfree"
        )
        self.assertEqual(caption["evidence"]["headId"], "relation_head_band_1")
        self.assertEqual(record["candidates"][1]["text"], "Storage")

    def test_duplicate_boxes_never_heads(self):
        # The two-box rows (Sound & vibration / Wallpaper / Accessibility /
        # Security & privacy dup detections at the caption offsets) are
        # absorbed as satellites — only the single topmost title is the head.
        detections, ocr_tokens = _frozen_frame()
        record = run(detections, ocr_tokens, _W, _H)
        self.assertEqual(_menu_texts(record), _TITLES)
        self.assertTrue(
            all(s["type"] == "NonInteractive" for s in record["satellites"]),
            "every satellite (caption + dup box) must be NonInteractive",
        )

    def test_unanchored_ad_band_fails_closed(self):
        # 8 composed bands + 1 rejected: the OCR-only 'AD' band has no
        # detector at its column and is rejected fail-closed.
        detections, ocr_tokens = _frozen_frame()
        record = run(detections, ocr_tokens, _W, _H)
        self.assertEqual(len(record["bands"]), 9)
        rejected = [b for b in record["bands"] if b["status"] == "rejected"]
        self.assertEqual(len(rejected), 1)
        self.assertIn("fail-closed", rejected[0]["reason"])
        self.assertEqual([b["status"] for b in record["bands"]].count("composed"), 8)

    def test_deterministic_double_run(self):
        detections, ocr_tokens = _frozen_frame()
        first = run(detections, ocr_tokens, _W, _H)
        second = run(detections, ocr_tokens, _W, _H)
        self.assertEqual(first, second)
        self.assertEqual(record_trace_bytes(first), record_trace_bytes(second))


if __name__ == "__main__":
    unittest.main()