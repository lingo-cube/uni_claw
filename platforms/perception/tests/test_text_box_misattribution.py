"""R-VLM-1: text-to-box misattribution detection falsifiers.
Gate: R_VLM_1_IMPLEMENTATION_APPROVED_WITH_PROVENANCE_CONDITION.

Frozen corpus pattern (Display V1, occurrence-provenance.md):
  occ_21 'Appearance' text_block y=[0.653,0.669] x=[0.060,0.243]  ← ORIGINAL
  occ_22 'Appearance' menu_item  y=[0.653,0.669] x=[0.060,0.243]  ← normal dup
  occ_23 'Appearance' text_block y=[0.703,0.721] x=[0.061,0.307]  ← MISATTRIBUTED
  occ_24 'Dark theme' menu_item  y=[0.703,0.721] x=[0.061,0.307]  ← real row

Rule (purely structural — bounds + exact text equality, no semantics):
  text T at position A (original) and B (misattributed), where B has a
  different menu_item text T' (T ≠ T') and T@B overlaps T'@B → remove T@B.
  Only when there is exactly ONE distinct original position (unambiguous).
  Multiple possible originals → keep (don't guess). No provenance → keep.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.fusion.engine import _detect_text_box_misattribution


def cand(text: str, typ: str, y1: float, y2: float,
         x1: float = 0.06, x2: float = 0.50) -> dict:
    """Build a minimal candidate with normalized bounds."""
    return {"text": text, "type": typ, "bounds": {"x1": x1, "y1": y1, "x2": x2, "y2": y2}}


class TextBoxMisattributionFalsifiers(unittest.TestCase):
    """7 falsifiers for the R-VLM-1 structural misattribution rule."""

    # F1: Same text, two legitimate menu_item rows at different positions.
    #     No different-text menu_item overlaps either → KEEP BOTH.
    def test_f1_same_text_two_legitimate_menu_items_kept(self):
        candidates = [
            cand("Row A", "menu_item", 0.30, 0.32),
            cand("Row A", "menu_item", 0.50, 0.52),
        ]
        removed = _detect_text_box_misattribution(candidates)
        self.assertEqual(
            len(candidates), 2,
            "two legitimate same-text menu_items at different positions "
            "with no different-text overlap must both survive",
        )
        self.assertEqual(removed, [])

    # F2: Section header + same-name menu_item at the SAME position.
    #     Same text → the menu_item is NOT a "different text" row → not
    #     misattribution (normal duplicate) → KEEP BOTH.
    def test_f2_header_and_same_name_menu_same_position_kept(self):
        candidates = [
            cand("Header", "text_block", 0.20, 0.22),
            cand("Header", "menu_item", 0.20, 0.22),
        ]
        removed = _detect_text_box_misattribution(candidates)
        self.assertEqual(
            len(candidates), 2,
            "same-position same-text header+menu is a normal duplicate, "
            "not misattribution → both survive",
        )
        self.assertEqual(removed, [])

    # F3: Title/subtitle adjacent (different text, NonInteractive — not a
    #      menu_item) → KEEP.  Also: unique text with no other occurrence
    #      anywhere → KEEP (no provenance → don't guess).
    def test_f3_adjacent_different_text_and_unique_text_kept(self):
        # Adjacent title + subtitle (NonInteractive, not menu_item) → both survive
        candidates = [
            cand("Title", "text_block", 0.30, 0.35),
            cand("Subtitle", "NonInteractive", 0.33, 0.38),
        ]
        removed = _detect_text_box_misattribution(candidates)
        self.assertEqual(
            len(candidates), 2,
            "adjacent different-text title/subtitle must both survive",
        )
        self.assertEqual(removed, [])

        # Unique text (no other occurrence) → survives (no provenance)
        candidates2 = [cand("Solo", "text_block", 0.40, 0.42)]
        removed2 = _detect_text_box_misattribution(candidates2)
        self.assertEqual(
            len(candidates2), 1,
            "unique text with no other occurrence must survive (no provenance)",
        )

    # F4: 'Color' vs 'Colors' — similar but NOT exactly equal.  Exact text
    #     equality is required for grouping; fuzzy matching is forbidden.
    #     Both are single-occurrence groups → KEEP BOTH.
    def test_f4_similar_text_no_fuzzy_deletion(self):
        candidates = [
            cand("Color", "text_block", 0.80, 0.82),
            cand("Colors", "menu_item", 0.80, 0.82),
        ]
        removed = _detect_text_box_misattribution(candidates)
        self.assertEqual(
            len(candidates), 2,
            "'Color' vs 'Colors' are different texts (exact equality "
            "required) → both survive; no fuzzy deletion",
        )
        self.assertEqual(removed, [])

    # F5: The core misattribution pattern — same text at an original position
    #      AND a misattributed position that overlaps a different-text
    #      menu_item.  Delete ONLY the misattributed copy; keep original +
    #      the real menu_item row.
    def test_f5_misattributed_copy_deleted_original_kept(self):
        candidates = [
            cand("Appearance", "text_block", 0.653, 0.669, 0.060, 0.243),  # original
            cand("Appearance", "text_block", 0.703, 0.721, 0.061, 0.307),  # misattributed
            cand("Dark theme", "menu_item", 0.703, 0.721, 0.061, 0.307),   # real row
        ]
        removed = _detect_text_box_misattribution(candidates)
        texts = [c["text"] for c in candidates]
        self.assertIn("Dark theme", texts, "'Dark theme' menu_item must survive")
        appearances = [c for c in candidates if c["text"] == "Appearance"]
        self.assertEqual(
            len(appearances), 1,
            "exactly one 'Appearance' (the misattributed one) must be removed",
        )
        self.assertAlmostEqual(
            appearances[0]["bounds"]["y1"], 0.653, places=3,
            msg="surviving 'Appearance' must be the original at y≈0.653",
        )
        self.assertEqual(len(removed), 1)

    # F5 corpus variant: three 'Appearance' occurrences (original text_block +
    # normal menu_item duplicate at the same original position + misattributed
    # text_block at the row position).  The two originals share ONE distinct
    # position → still unambiguous → remove ONLY the misattributed copy.
    def test_f5_corpus_three_appearances_only_misattributed_removed(self):
        candidates = [
            cand("Appearance", "text_block", 0.653, 0.669, 0.060, 0.243),  # occ_21
            cand("Appearance", "menu_item", 0.653, 0.669, 0.060, 0.243),   # occ_22
            cand("Appearance", "text_block", 0.703, 0.721, 0.061, 0.307),  # occ_23
            cand("Dark theme", "menu_item", 0.703, 0.721, 0.061, 0.307),   # occ_24
        ]
        removed = _detect_text_box_misattribution(candidates)
        appearances = [c for c in candidates if c["text"] == "Appearance"]
        self.assertEqual(
            len(appearances), 2,
            "occ_21 + occ_22 survive (one distinct original position); "
            "only occ_23 removed",
        )
        for a in appearances:
            self.assertAlmostEqual(
                a["bounds"]["y1"], 0.653, places=3,
                msg="surviving 'Appearance' must be at the original y≈0.653",
            )
        self.assertIn("Dark theme", [c["text"] for c in candidates])
        self.assertEqual(len(removed), 1)

    # F6: Multiple possible owners — three instances of the same text at
    #      three different positions, one overlapping a different-text
    #      menu_item.  Two distinct original positions → ambiguous → DON'T
    #      delete any (don't guess).
    def test_f6_multiple_possible_owners_all_kept(self):
        candidates = [
            cand("Text", "text_block", 0.30, 0.32),
            cand("Text", "text_block", 0.50, 0.52),
            cand("Text", "text_block", 0.70, 0.72),
            cand("Other", "menu_item", 0.70, 0.72),
        ]
        removed = _detect_text_box_misattribution(candidates)
        self.assertEqual(
            len(candidates), 4,
            "ambiguous which original is real (two distinct original "
            "positions) → don't delete any",
        )
        self.assertEqual(removed, [])

    # F7: Diagnostics must record the rejected association with text,
    #      removed bounds, original bounds, overlapping menu text, and reason.
    def test_f7_diagnostics_record_rejected_association(self):
        candidates = [
            cand("Appearance", "text_block", 0.653, 0.669, 0.060, 0.243),
            cand("Appearance", "text_block", 0.703, 0.721, 0.061, 0.307),
            cand("Dark theme", "menu_item", 0.703, 0.721, 0.061, 0.307),
        ]
        removed = _detect_text_box_misattribution(candidates)
        self.assertEqual(len(removed), 1)
        entry = removed[0]
        self.assertEqual(entry["text"], "Appearance")
        self.assertEqual(entry["overlappingMenuText"], "Dark theme")
        self.assertIn("reason", entry)
        self.assertIn("RVLM", entry["reason"])
        self.assertIn("removedBounds", entry)
        self.assertIn("originalBounds", entry)
        # removedBounds = misattributed position (y≈0.703)
        self.assertAlmostEqual(entry["removedBounds"]["y1"], 0.703, places=3)
        # originalBounds = original position (y≈0.653)
        self.assertAlmostEqual(entry["originalBounds"]["y1"], 0.653, places=3)


if __name__ == "__main__":
    unittest.main()
