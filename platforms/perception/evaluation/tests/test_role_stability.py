"""Role Flip Rate metric tests: zero-flip, full-flip, single-observation,
aggregation, invalid input."""
from __future__ import annotations

import unittest

from evaluation.role_stability import role_flip_rate


class RoleFlipRateTests(unittest.TestCase):
    def test_stable_track_zero_flip(self):
        result = role_flip_rate({"row_001": ("menu_item", "menu_item", "menu_item")})
        self.assertEqual(result.track_count, 1)
        self.assertEqual(result.transition_count, 2)
        self.assertEqual(result.flip_count, 0)
        self.assertEqual(result.role_flip_rate, 0.0)
        self.assertEqual(result.tracks[0].flip_rate, 0.0)
        self.assertEqual(result.tracks[0].flip_pairs, ())
        self.assertEqual(result.pair_counts, ())

    def test_alternating_track_full_flip(self):
        result = role_flip_rate({"row_002": ("text_block", "menu_item", "text_block")})
        self.assertEqual(result.flip_count, 2)
        self.assertEqual(result.transition_count, 2)
        self.assertEqual(result.role_flip_rate, 1.0)
        self.assertEqual(
            result.tracks[0].flip_pairs,
            (("text_block", "menu_item"), ("menu_item", "text_block")),
        )
        self.assertEqual(
            result.pair_counts,
            ((("menu_item", "text_block"), 1), (("text_block", "menu_item"), 1)),
        )

    def test_partial_flip_rate(self):
        result = role_flip_rate({"row_003": ("icon", "icon", "menu_item", "menu_item")})
        self.assertEqual(result.flip_count, 1)
        self.assertEqual(result.transition_count, 3)
        self.assertEqual(result.role_flip_rate, 1 / 3)
        self.assertEqual(result.tracks[0].flip_pairs, (("icon", "menu_item"),))

    def test_single_observation_track_contributes_no_denominator(self):
        result = role_flip_rate({"row_004": ("text_block",), "row_005": ("menu_item",)})
        self.assertEqual(result.track_count, 2)
        self.assertEqual(result.transition_count, 0)
        self.assertEqual(result.flip_count, 0)
        self.assertIsNone(result.role_flip_rate)
        self.assertIsNone(result.tracks[0].flip_rate)

    def test_mixed_track_aggregation(self):
        result = role_flip_rate(
            {
                "row_a": ("menu_item", "menu_item"),        # 0/1 flips
                "row_b": ("text_block", "menu_item", "menu_item", "icon"),  # 2/3 flips
            }
        )
        self.assertEqual(result.transition_count, 4)
        self.assertEqual(result.flip_count, 2)
        self.assertEqual(result.role_flip_rate, 0.5)
        self.assertEqual(result.tracks[0].track_id, "row_a")
        self.assertEqual(result.tracks[1].track_id, "row_b")
        # Equal counts are ordered deterministically by pair (lexicographic).
        self.assertEqual(
            result.pair_counts[0], (("menu_item", "icon"), 1)
        )
        self.assertEqual(
            result.pair_counts[1], (("text_block", "menu_item"), 1)
        )

    def test_empty_input(self):
        result = role_flip_rate({})
        self.assertEqual(result.track_count, 0)
        self.assertEqual(result.transition_count, 0)
        self.assertEqual(result.flip_count, 0)
        self.assertIsNone(result.role_flip_rate)
        self.assertEqual(result.tracks, ())
        self.assertEqual(result.pair_counts, ())

    def test_empty_sequence_is_invalid(self):
        with self.assertRaises(ValueError):
            role_flip_rate({"row_x": ()})

    def test_blank_role_is_invalid(self):
        with self.assertRaises(ValueError):
            role_flip_rate({"row_x": ("menu_item", "", "text_block")})


if __name__ == "__main__":
    unittest.main()