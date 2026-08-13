"""Matcher tests (I19): deterministic greedy class+IoU matching."""
from __future__ import annotations

import unittest

from evaluation.matcher import MATCHER_REVISION, match


def _pred(t: str, bounds: tuple[float, float, float, float]) -> dict:
    return {"type": t, "bounds": bounds}


def _gt(cls: str, bounds: tuple[float, float, float, float]) -> dict:
    return {"gt_class": cls, "bounds": bounds}


class MatcherTests(unittest.TestCase):
    def test_one_gt_one_correct_prediction(self):
        m = match([_pred("text_block", (0.10, 0.10, 0.40, 0.20))],
                  [_gt("text_block", (0.10, 0.10, 0.40, 0.20))])
        self.assertEqual((m.tp, m.fp, m.fn), (1, 0, 0))
        self.assertEqual(m.matcher_revision, MATCHER_REVISION)

    def test_missed_gt(self):
        m = match([], [_gt("text_block", (0.10, 0.10, 0.40, 0.20))])
        self.assertEqual((m.tp, m.fp, m.fn), (0, 0, 1))

    def test_false_positive(self):
        m = match([_pred("text_block", (0.10, 0.10, 0.40, 0.20))], [])
        self.assertEqual((m.tp, m.fp, m.fn), (0, 1, 0))

    def test_overlapping_multiple_predictions_one_to_one(self):
        m = match([
            _pred("text_block", (0.10, 0.10, 0.40, 0.20)),
            _pred("text_block", (0.12, 0.11, 0.42, 0.21)),
        ], [_gt("text_block", (0.10, 0.10, 0.40, 0.20))])
        # one prediction matches; the other is a false positive
        self.assertEqual((m.tp, m.fp, m.fn), (1, 1, 0))

    def test_one_prediction_cannot_satisfy_two_gts(self):
        m = match([_pred("text_block", (0.10, 0.10, 0.40, 0.40))], [
            _gt("text_block", (0.10, 0.10, 0.40, 0.25)),
            _gt("text_block", (0.10, 0.25, 0.40, 0.40)),
        ])
        self.assertEqual((m.tp, m.fp, m.fn), (1, 0, 1))

    def test_class_mismatch_never_matches(self):
        m = match([_pred("icon", (0.10, 0.10, 0.40, 0.20))],
                  [_gt("text_block", (0.10, 0.10, 0.40, 0.20))])
        self.assertEqual((m.tp, m.fp, m.fn), (0, 1, 1))

    def test_insufficient_iou_never_matches(self):
        m = match([_pred("text_block", (0.10, 0.10, 0.40, 0.20))],
                  [_gt("text_block", (0.60, 0.60, 0.90, 0.80))])
        self.assertEqual((m.tp, m.fp, m.fn), (0, 1, 1))

    def test_no_gt(self):
        m = match([_pred("text_block", (0.10, 0.10, 0.40, 0.20))], [])
        self.assertEqual((m.tp, m.fp, m.fn), (0, 1, 0))

    def test_greedy_prefers_higher_iou(self):
        m = match([
            _pred("text_block", (0.11, 0.11, 0.41, 0.21)),   # better overlap
            _pred("text_block", (0.20, 0.20, 0.55, 0.35)),   # worse overlap
        ], [_gt("text_block", (0.10, 0.10, 0.40, 0.20))])
        self.assertEqual(m.tp, 1)
        self.assertEqual(m.matches[0].pred_index, 0)


if __name__ == "__main__":
    unittest.main()
