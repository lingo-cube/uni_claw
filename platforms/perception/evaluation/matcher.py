"""Versioned deterministic Prediction ↔ GroundTruth matcher.

Purchased policy (R14): class compatibility + IoU + one-to-one greedy.
Matching semantics live HERE, versioned by MATCHER_REVISION — never buried
in score formulas.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any

MATCHER_REVISION = "matcher-greedy-v1"
IOU_THRESHOLD = 0.5


def _iou(a: tuple[float, float, float, float],
         b: tuple[float, float, float, float]) -> float:
    x1 = max(a[0], b[0]); y1 = max(a[1], b[1])
    x2 = min(a[2], b[2]); y2 = min(a[3], b[3])
    inter = max(0.0, x2 - x1) * max(0.0, y2 - y1)
    if inter <= 0:
        return 0.0
    area_a = max(0.0, a[2] - a[0]) * max(0.0, a[3] - a[1])
    area_b = max(0.0, b[2] - b[0]) * max(0.0, b[3] - b[1])
    union = area_a + area_b - inter
    return inter / union if union > 0 else 0.0


@dataclass(frozen=True)
class MatchPair:
    pred_index: int
    gt_index: int
    iou: float


@dataclass(frozen=True)
class MatchResult:
    matcher_revision: str
    matches: tuple[MatchPair, ...]
    unmatched_predictions: tuple[int, ...]   # false positives
    unmatched_ground_truth: tuple[int, ...]  # false negatives

    @property
    def tp(self) -> int:
        return len(self.matches)

    @property
    def fp(self) -> int:
        return len(self.unmatched_predictions)

    @property
    def fn(self) -> int:
        return len(self.unmatched_ground_truth)

    def to_json(self) -> dict[str, Any]:
        return {
            "matcherRevision": self.matcher_revision,
            "matches": [
                {"predIndex": m.pred_index, "gtIndex": m.gt_index, "iou": round(m.iou, 6)}
                for m in self.matches
            ],
            "unmatchedPredictions": list(self.unmatched_predictions),
            "unmatchedGroundTruth": list(self.unmatched_ground_truth),
        }


def match(predictions: list[dict[str, Any]], gt_elements: list[dict[str, Any]],
          iou_threshold: float = IOU_THRESHOLD) -> MatchResult:
    """Greedy one-to-one matching.

    predictions: [{type, bounds: (x1,y1,x2,y2)?, text?}, ...]
    gt_elements: [{gt_class, bounds?, text?}, ...]
    Rules:
      • class compatibility: prediction type == gt class
      • IoU ≥ threshold (elements without bounds on either side are
        excluded from IoU matching — they become unmatched)
      • one prediction satisfies at most one GT, and vice versa
      • greedy: highest-IoU compatible pairs assigned first
    """
    candidates: list[tuple[float, int, int]] = []
    for pi, p in enumerate(predictions):
        pb = p.get("bounds")
        if pb is None:
            continue
        for gi, g in enumerate(gt_elements):
            gb = g.get("bounds")
            if gb is None:
                continue
            if p.get("type") != g.get("gt_class"):
                continue  # class mismatch → never matched
            score = _iou(tuple(pb), tuple(gb))
            if score >= iou_threshold:
                candidates.append((score, pi, gi))

    candidates.sort(key=lambda t: (-t[0], t[1], t[2]))
    used_p: set[int] = set()
    used_g: set[int] = set()
    pairs: list[MatchPair] = []
    for score, pi, gi in candidates:
        if pi in used_p or gi in used_g:
            continue
        used_p.add(pi)
        used_g.add(gi)
        pairs.append(MatchPair(pred_index=pi, gt_index=gi, iou=score))

    unmatched_p = tuple(sorted(
        i for i in range(len(predictions))
        if i not in used_p and predictions[i].get("bounds") is not None
    ))
    unmatched_g = tuple(sorted(
        i for i in range(len(gt_elements))
        if i not in used_g and gt_elements[i].get("bounds") is not None
    ))
    return MatchResult(
        matcher_revision=MATCHER_REVISION,
        matches=tuple(pairs),
        unmatched_predictions=unmatched_p,
        unmatched_ground_truth=unmatched_g,
    )
