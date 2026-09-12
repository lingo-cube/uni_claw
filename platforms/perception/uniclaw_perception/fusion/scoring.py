"""Fusion scoring: spatial matching, confidence combination, risk flags.

Extracted from fusion.py. Deterministic mechanisms — no heuristics.
"""
from __future__ import annotations

import math
from typing import Any


def match_score(detection: Any, token: Any, max_distance: float) -> float:
    """Spatial YOLO↔OCR matching score. 1.0 = containment, >0 = proximity."""
    if detection.box.contains_center(token.box):
        return 1.0

    overlap = detection.box.intersection_area(token.box)
    if overlap > 0:
        denom = max(1.0, min(detection.box.area(), token.box.area()))
        return min(0.95, 0.55 + (overlap / denom) * 0.4)

    dcx, dcy = detection.box.center()
    tcx, tcy = token.box.center()
    distance = math.hypot(dcx - tcx, dcy - tcy)
    if distance <= max_distance:
        return max(0.15, 0.5 * (1.0 - distance / max_distance))

    return 0.0


def combined_confidence(detection: Any, tokens: list[Any]) -> float:
    """Weighted YOLO+OCR confidence: 0.72×yolo + 0.28×ocr_mean."""
    if not tokens:
        return detection.confidence * 0.85
    ocr_conf = sum(t.confidence for t in tokens) / len(tokens)
    return detection.confidence * 0.72 + ocr_conf * 0.28


def candidate_risks(detection: Any, tokens: list[Any]) -> list[str]:
    """Assign risk flags to a candidate based on evidence quality."""
    risks: list[str] = []
    if detection.confidence < 0.55:
        risks.append("low_yolo_confidence")
    if not tokens and detection.label not in {"icon", "back", "toolbar", "popup"}:
        risks.append("no_text_evidence")
    if tokens and min(t.confidence for t in tokens) < 0.6:
        risks.append("low_ocr_confidence")
    return risks


def normalized_center(item: Any, width: int, height: int) -> dict[str, float]:
    """Normalized center coordinates [0,1]×[0,1]."""
    cx, cy = item.box.center()
    return {"x": round(cx / width, 6), "y": round(cy / height, 6)}
