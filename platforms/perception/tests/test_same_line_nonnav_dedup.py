"""Same-line same-text non-navigation dedup pass (WI-PFW-S2fix3).

Locks the frozen real-frame defect repair (run 4, seq 17): one visual search
line emits 2-4 ``input`` candidates with the identical text ``Q Search
settings`` (OCR returns horizontally-offset boxes — magnifier glyph + text —
around the same search bar).  The existing IoU-based dedup (IoU >= 0.6) cannot
merge horizontally-offset same-line boxes, so without this pass every
duplicate becomes an evidence-less Unknown element in the frozen downstream
stack.

Acceptance (leader-locked):
  (a) 4× same-text same-line ``input`` -> exactly 1 (deterministic survivor)
  (b) same text on DIFFERENT lines -> all preserved
  (c) same line, different text -> all preserved
  (d) ``menu_item`` duplicates are NOT deduped by this pass
  (e) determinism: double-run byte-identical
"""
from __future__ import annotations

import unittest

from uniclaw_perception.fusion.engine import (
    dedupe_same_line_nonnav_candidates,
    fuse_evidence,
    fuse_evidence_from_crops,
)
from uniclaw_perception.schema import Box, Detection, OcrToken

_W, _H = 1080, 2400


def _det(identifier: str, label: str, confidence: float, box: tuple[float, float, float, float]) -> Detection:
    return Detection(identifier, label, confidence, Box(*box))


def _ocr(identifier: str, text: str, confidence: float, box: tuple[float, float, float, float]) -> OcrToken:
    return OcrToken(identifier, text, confidence, Box(*box))


class SameLineNonnavDedupEngineTests(unittest.TestCase):
    """Engine-path acceptance: the frozen 4×input shape and preservation cases."""

    # (a) The frozen real-frame shape: four same-line input detections all
    # matching the single 'Q Search settings' OCR token (horizontally offset;
    # identical vertical span, so IoU-based dedup cannot merge them).
    def test_four_same_text_same_line_inputs_collapse_to_one(self):
        token = _ocr("o1", "Q Search settings", 0.9, (100.0, 520.0, 580.0, 545.0))
        detections = [
            _det("d1", "input", 0.75, (60.0, 510.0, 200.0, 550.0)),
            _det("d2", "input", 0.90, (180.0, 510.0, 320.0, 550.0)),
            _det("d3", "input", 0.80, (300.0, 510.0, 440.0, 550.0)),
            _det("d4", "input", 0.70, (420.0, 510.0, 560.0, 550.0)),
        ]
        evidence = fuse_evidence(
            detections, [token],
            image_width=_W, image_height=_H,
        )
        inputs = [c for c in evidence["candidates"] if c["type"] == "input"]
        self.assertEqual(
            len(inputs), 1,
            "4 same-text same-line input candidates must collapse to exactly 1; "
            f"got {len(inputs)} (ids: {[c['id'] for c in inputs]})",
        )
        self.assertEqual(
            [c["text"] for c in inputs], ["Q Search settings"],
        )
        # Deterministic survivor: highest confidence (d2 = 0.90) wins.
        self.assertEqual(
            inputs[0]["evidence"]["yoloId"], "d2",
            "survivor must be the highest-confidence input (tie broken "
            "deterministically elsewhere)",
        )
        # Suppression detail surfaces on the non-candidate diagnostics key.
        diagnostics = evidence.get("_diagnostics", {})
        suppressed = diagnostics.get("lineDupSuppressed", [])
        self.assertEqual(len(suppressed), 3)
        self.assertEqual(
            {entry["keptId"] for entry in suppressed}, {inputs[0]["id"]},
        )
        self.assertEqual(
            {entry["text"] for entry in suppressed}, {"Q Search settings"},
        )
        # No candidate key is altered by the pass: candidates serialize plainly.
        self.assertNotIn("_diagnostics", evidence["candidates"][0])

    # (b) Same text on distinct visual lines must be preserved (reuse of the
    # repeated_labels corpus semantics: two 'Accounts' rows stay two rows).
    def test_same_text_different_lines_both_survive(self):
        token_1 = _ocr("o1", "Search", 0.9, (100.0, 518.0, 300.0, 542.0))
        token_2 = _ocr("o2", "Search", 0.9, (100.0, 748.0, 300.0, 772.0))
        detections = [
            _det("d1", "input", 0.9, (60.0, 510.0, 560.0, 550.0)),
            _det("d2", "input", 0.9, (60.0, 740.0, 560.0, 780.0)),
        ]
        evidence = fuse_evidence(
            detections, [token_1, token_2],
            image_width=_W, image_height=_H,
        )
        inputs = [c for c in evidence["candidates"] if c["type"] == "input"]
        self.assertEqual(
            len(inputs), 2,
            "same text on DIFFERENT lines must be preserved; "
            f"got {len(inputs)} (ids: {[c['id'] for c in inputs]})",
        )
        self.assertTrue(all(c["text"] == "Search" for c in inputs))

    # (c) Same line, different text: both candidates describe distinct content.
    def test_same_line_different_text_both_survive(self):
        token_1 = _ocr("o1", "Alpha", 0.9, (80.0, 308.0, 200.0, 332.0))
        token_2 = _ocr("o2", "Beta", 0.9, (580.0, 308.0, 700.0, 332.0))
        detections = [
            _det("d1", "input", 0.9, (60.0, 300.0, 520.0, 340.0)),
            _det("d2", "input", 0.9, (560.0, 300.0, 1020.0, 340.0)),
        ]
        evidence = fuse_evidence(
            detections, [token_1, token_2],
            image_width=_W, image_height=_H,
        )
        inputs = [c for c in evidence["candidates"] if c["type"] == "input"]
        self.assertEqual(
            len(inputs), 2,
            "same line with DIFFERENT text must be preserved; "
            f"got {len(inputs)} (ids: {[c['id'] for c in inputs]})",
        )
        self.assertEqual({c["text"] for c in inputs}, {"Alpha", "Beta"})

    # (e) Determinism: two independent engine runs are byte-identical.
    def test_double_run_byte_identical(self):
        def run():
            token = _ocr("o1", "Q Search settings", 0.9, (100.0, 520.0, 580.0, 545.0))
            detections = [
                _det("d1", "input", 0.75, (60.0, 510.0, 200.0, 550.0)),
                _det("d2", "input", 0.90, (180.0, 510.0, 320.0, 550.0)),
                _det("d3", "input", 0.80, (300.0, 510.0, 440.0, 550.0)),
                _det("d4", "input", 0.70, (420.0, 510.0, 560.0, 550.0)),
            ]
            return fuse_evidence(
                detections, [token],
                image_width=_W, image_height=_H,
            )

        first, second = run(), run()
        self.assertEqual(first, second, "double-run must be byte-identical")
        self.assertEqual(len(first["candidates"]), len(second["candidates"]))

    # The crops path shares the same final assembly defect class: per-crop OCR
    # can hand the same text to two same-line input detections.
    def test_crops_path_same_line_same_text_collapses(self):
        token_1 = _ocr("o1", "Q Search settings", 0.9, (100.0, 520.0, 300.0, 545.0))
        token_2 = _ocr("o2", "Q Search settings", 0.9, (320.0, 520.0, 580.0, 545.0))
        detections = [
            _det("d1", "input", 0.9, (60.0, 510.0, 320.0, 550.0)),
            _det("d2", "input", 0.9, (300.0, 510.0, 620.0, 550.0)),
        ]
        evidence = fuse_evidence_from_crops(
            detections, [[token_1], [token_2]],
            image_width=_W, image_height=_H,
        )
        inputs = [c for c in evidence["candidates"] if c["type"] == "input"]
        self.assertEqual(len(inputs), 1)


class SameLineNonnavDedupUnitTests(unittest.TestCase):
    """Direct pass-level assertions (menu_item immunity + deterministic ties)."""

    @staticmethod
    def _candidate(identifier: str, type_: str, text: str, confidence: float,
                   box: tuple[float, float, float, float]) -> dict:
        x1, y1, x2, y2 = box
        return {
            "id": identifier,
            "type": type_,
            "text": text,
            "confidence": confidence,
            "boundsPx": [x1, y1, x2, y2],
        }

    # (d) menu_item duplicates are exempt from this pass (rows own their own
    # absorption); inputs on the same line still collapse.
    def test_menu_item_duplicates_untouched(self):
        candidates = [
            self._candidate("m1", "menu_item", "Accounts", 0.9, (100, 200, 300, 230)),
            self._candidate("m2", "menu_item", "Accounts", 0.9, (150, 201, 350, 231)),
            self._candidate("i1", "input", "Search", 0.9, (100, 300, 500, 330)),
            self._candidate("i2", "input", "Search", 0.8, (200, 300, 600, 330)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        self.assertEqual(len(suppressed), 1)
        ids = {c["id"] for c in candidates}
        self.assertEqual(ids, {"m1", "m2", "i1"},
                         "menu_item duplicates must be untouched; only the "
                         "duplicate input collapses")
        self.assertEqual(suppressed[0]["id"], "i2")
        self.assertEqual(suppressed[0]["keptId"], "i1")

    # Operator-composed row_relation_head satellites (NonInteractive) are
    # band-bound outputs, not fusion candidates: a same-text same-line fusion
    # text_block must NOT absorb the band's text satellite (G-1 gate shape:
    # f3_subtitle_low_anchor_never_promoted keeps the satellite emission).
    def test_relation_head_satellites_not_absorbed(self):
        candidates = [
            self._candidate("c1", "text_block", "Wi-Fi", 0.9252, (120, 100, 480, 130)),
            self._candidate("c2", "text_block", "Wi-Fi, connections, networks", 0.8676, (120, 134, 480, 158)),
            self._candidate("h0", "menu_item", "Wi-Fi", 0.9, (120, 100, 480, 130)),
            {
                "id": "sat_0",
                "type": "NonInteractive",
                "text": "",
                "boundsPx": [120, 134, 480, 158],
            },
            {
                "id": "sat_1",
                "type": "NonInteractive",
                "text": "Wi-Fi, connections, networks",
                "boundsPx": [122, 136, 478, 156],
            },
            self._candidate("c3", "text_block", "Bluetooth", 0.9252, (120, 400, 480, 430)),
            self._candidate("h1", "menu_item", "Bluetooth", 0.9, (120, 400, 480, 430)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        self.assertEqual(suppressed, [])
        ids = {c["id"] for c in candidates}
        self.assertIn("sat_1", ids,
                      "the band's text satellite must survive the dedup pass")
        self.assertIn("c2", ids,
                      "the same-text same-line fusion text_block also survives: "
                      "the pass never merges an operator satellite with a fused "
                      "candidate")

    def test_tie_break_confidence_then_area_then_id(self):
        # Equal confidence -> larger bounds area wins.
        bigger = self._candidate("b", "input", "Text", 0.8, (100, 100, 400, 130))
        smaller = self._candidate("s", "input", "Text", 0.8, (120, 100, 300, 130))
        kept = [bigger, smaller]
        dedupe_same_line_nonnav_candidates(kept)
        self.assertEqual([c["id"] for c in kept], ["b"])

        # Equal confidence AND equal area -> lexicographically smallest id.
        left = self._candidate("a_x", "input", "Text", 0.8, (100, 100, 300, 130))
        right = self._candidate("b_x", "input", "Text", 0.8, (300, 100, 500, 130))
        kept = [right, left]
        dedupe_same_line_nonnav_candidates(kept)
        self.assertEqual([c["id"] for c in kept], ["a_x"])

    def test_empty_text_and_icons_untouched(self):
        candidates = [
            self._candidate("i1", "icon", "", 0.9, (100, 100, 140, 140)),
            self._candidate("i2", "icon", "", 0.9, (150, 100, 190, 140)),
            self._candidate("t1", "text_block", "  ", 0.9, (100, 300, 300, 330)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        self.assertEqual(suppressed, [])
        self.assertEqual(len(candidates), 3,
                         "empty text / no-text candidates are not dedup targets")


class SameLineNonnavDedupAdjacentLineTests(unittest.TestCase):
    """Adjacent-line extension (WI-S2fix5).

    S2fix3's same-line predicate (vertical overlap >= half the shorter
    candidate's height) does NOT cover the frozen real-frame adjacent-row
    defect: a Settings title ``Sound & vibration`` at y=[0.401,0.421]
    (height 0.020) followed immediately by a duplicate shadow text_block at
    y=[0.430,0.446] (height 0.016) — gap=0.009, no overlap, so the old rule
    left both in-frame and the downstream quiescence gate correctly fail-closed
    on the same-signature duplicate.

    The fix widens the visual-row predicate to
    ``overlap >= shorter/2 OR gap <= shorter_height`` (gap = distance between
    the two boxes when they do not overlap). Same-text truly-different-line
    pairs (gap >> height) stay preserved; exemptions (menu_item /
    NonInteractive) and exact-strip text equality are unchanged.
    """

    @staticmethod
    def _candidate(identifier: str, type_: str, text: str, confidence: float,
                   box: tuple[float, float, float, float]) -> dict:
        x1, y1, x2, y2 = box
        return {
            "id": identifier,
            "type": type_,
            "text": text,
            "confidence": confidence,
            "boundsPx": [x1, y1, x2, y2],
        }

    # Frozen real geometry: title + immediately-following shadow, same text,
    # no vertical overlap but gap (0.009) <= shorter height (0.016) -> deduped
    # to exactly one survivor.
    def test_adjacent_line_same_text_collapses_to_one(self):
        candidates = [
            self._candidate("title", "text_block", "Sound & vibration",
                            0.93, (0.250, 0.401, 0.750, 0.421)),
            self._candidate("shadow", "text_block", "Sound & vibration",
                            0.80, (0.255, 0.430, 0.745, 0.446)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        self.assertEqual(
            len(candidates), 1,
            "adjacent-line same-text title+shadow must collapse to 1 survivor; "
            f"got {len(candidates)} (ids: {[c['id'] for c in candidates]})",
        )
        # Deterministic survivor: highest confidence (title = 0.93).
        self.assertEqual(candidates[0]["id"], "title")
        self.assertEqual(len(suppressed), 1)
        self.assertEqual(suppressed[0]["id"], "shadow")
        self.assertEqual(suppressed[0]["keptId"], "title")

    # Gap just over the shorter height must NOT dedup: both survive. Here
    # shorter height = 0.020, gap = 0.025 (> shorter) -> two distinct rows.
    def test_gap_just_over_shorter_height_both_survive(self):
        candidates = [
            self._candidate("a", "text_block", "Label", 0.9,
                            (0.10, 0.400, 0.60, 0.420)),
            self._candidate("b", "text_block", "Label", 0.9,
                            (0.10, 0.445, 0.60, 0.465)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        # shorter height = min(0.020, 0.020) = 0.020; gap = 0.445 - 0.420 = 0.025
        # 0.025 > 0.020 -> NOT same visual row.
        self.assertEqual(
            len(candidates), 2,
            "gap just over shorter height must preserve both; "
            f"got {len(candidates)}",
        )
        self.assertEqual(suppressed, [])

    # Same text on truly different lines (gap >= 0.06, typical Settings row
    # spacing, vs height ~0.020) must both survive.
    def test_same_text_truly_different_lines_both_survive(self):
        candidates = [
            self._candidate("r1", "text_block", "Accessibility", 0.9,
                            (0.10, 0.300, 0.60, 0.320)),
            self._candidate("r2", "text_block", "Accessibility", 0.9,
                            (0.10, 0.380, 0.60, 0.400)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        # gap = 0.380 - 0.320 = 0.060; shorter = 0.020; 0.060 > 0.020 -> preserved.
        self.assertEqual(
            len(candidates), 2,
            "same text on truly different lines (gap >> height) must survive; "
            f"got {len(candidates)}",
        )
        self.assertEqual(suppressed, [])

    # Adjacent rows but DIFFERENT text: both survive (text equality still gates).
    def test_adjacent_line_different_text_both_survive(self):
        candidates = [
            self._candidate("a", "text_block", "Alpha", 0.9,
                            (0.10, 0.400, 0.60, 0.420)),
            self._candidate("b", "text_block", "Beta", 0.9,
                            (0.10, 0.424, 0.60, 0.444)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        self.assertEqual(
            len(candidates), 2,
            "adjacent rows with DIFFERENT text must both survive; "
            f"got {len(candidates)}",
        )
        self.assertEqual(suppressed, [])

    # menu_item adjacent duplicates are exempt (S2fix3 rule unchanged).
    def test_menu_item_adjacent_duplicates_not_deduped(self):
        candidates = [
            self._candidate("m1", "menu_item", "Sound & vibration", 0.9,
                            (0.10, 0.401, 0.60, 0.421)),
            self._candidate("m2", "menu_item", "Sound & vibration", 0.9,
                            (0.10, 0.430, 0.60, 0.446)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        self.assertEqual(
            len(candidates), 2,
            "menu_item adjacent duplicates are exempt and must both survive; "
            f"got {len(candidates)}",
        )
        self.assertEqual(suppressed, [])

    # NonInteractive adjacent duplicates are exempt (S2fix3 rule unchanged).
    def test_noninteractive_adjacent_duplicates_not_deduped(self):
        candidates = [
            self._candidate("n1", "NonInteractive", "Sound & vibration", 0.9,
                            (0.10, 0.401, 0.60, 0.421)),
            self._candidate("n2", "NonInteractive", "Sound & vibration", 0.9,
                            (0.10, 0.430, 0.60, 0.446)),
        ]
        suppressed = dedupe_same_line_nonnav_candidates(candidates)
        self.assertEqual(
            len(candidates), 2,
            "NonInteractive adjacent duplicates are exempt and must both "
            f"survive; got {len(candidates)}",
        )
        self.assertEqual(suppressed, [])

    # End-to-end engine path: the frozen adjacent title+shadow shape must
    # collapse through fuse_evidence (mirrors the 4×input engine test but for
    # the adjacent-line defect class).
    def test_engine_path_adjacent_title_shadow_collapses(self):
        token_title = _ocr("o1", "Sound & vibration", 0.9,
                           (270.0, 960.0, 810.0, 1010.0))
        token_shadow = _ocr("o2", "Sound & vibration", 0.85,
                            (275.0, 1030.0, 805.0, 1070.0))
        detections = [
            _det("d1", "text_block", 0.93, (270.0, 962.0, 810.0, 1010.0)),
            _det("d2", "text_block", 0.80, (276.0, 1032.0, 804.0, 1068.0)),
        ]
        evidence = fuse_evidence(
            detections, [token_title, token_shadow],
            image_width=_W, image_height=_H,
        )
        blocks = [c for c in evidence["candidates"]
                  if c["type"] == "text_block"
                  and c["text"] == "Sound & vibration"]
        self.assertEqual(
            len(blocks), 1,
            "adjacent title+shadow text_blocks must collapse to 1 survivor "
            f"through the engine path; got {len(blocks)} "
            f"(ids: {[c['id'] for c in blocks]})",
        )


if __name__ == "__main__":
    unittest.main()