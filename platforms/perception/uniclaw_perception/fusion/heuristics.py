"""Fusion heuristics: chevron alignment, search-box pre-labeling, text extraction.

Extracted from fusion.py. Heuristic policy — encodes domain assumptions
about Android Settings UI patterns. NOT semantic authority.
"""
from __future__ import annotations

from typing import Any


# YOLO labels that signal "interactive list row widget"
_ROW_WIDGET_LABELS = {"icon", "switch", "toggle", "checkbox"}


def apply_chevron_heuristic(
    candidates: list[dict[str, Any]],
    yolo_detections: list[Any],
    *,
    max_y_delta_px: float = 40.0,
) -> None:
    """Y-alignment heuristic (system-agnostic).

    OCR text on the same row as ANY YOLO interactive widget
    (icon, switch, toggle, checkbox) is a navigable menu item.
    No assumptions about widget position (left or right side).

    Reclassifies text_block → menu_item for aligned text.
    """
    widgets = [
        d for d in yolo_detections
        if d.label in _ROW_WIDGET_LABELS
    ]
    if not widgets:
        return

    for c in candidates:
        if not c["text"].strip():
            continue
        # Preserve already-correct top-level types
        if c["type"] in {"menu_item", "input", "button"}:
            continue
        ccy = c["centerPx"][1]

        best_id: str | None = None
        best_dist = float("inf")
        for w in widgets:
            if c["evidence"]["yoloId"] == w.id:
                continue  # self-match
            dist = abs(ccy - w.box.center()[1])
            if dist < best_dist:
                best_dist = dist
                best_id = w.id

        if best_id is not None and best_dist <= max_y_delta_px:
            c["type"] = "menu_item"
            if c["evidence"]["yoloId"] is None:
                c["evidence"]["yoloId"] = best_id
            c["evidence"]["allIds"].append(best_id)
            c["evidence"]["typeInferred"] = "row_alignment"
            risks: list[str] = c.get("riskFlags", [])
            for clearable in ("ocr_only", "low_ocr_confidence"):
                if clearable in risks:
                    risks.remove(clearable)


def apply_search_box_labeling(candidates: list[dict[str, Any]]) -> None:
    """Search-box pre-labeling: OCR text containing 'search' → type='input'.

    Prevents search boxes from being treated as navigable menu items.
    Runs BEFORE chevron heuristic.
    """
    for c in candidates:
        if c.get("type") == "input":
            continue  # already correctly typed
        text = c.get("text", "")
        if text and "search" in text.lower():
            c["type"] = "input"
            c["evidence"]["typeInferred"] = "search_text"


def primary_line_text(tokens: list[Any]) -> str:
    """Extract primary line text from OCR tokens within a YOLO box.

    Handles: multi-line clustering (takes top row), overlapping tokens
    (keeps longer text), single-character noise filtering.
    """
    meaningful = [t for t in tokens if len(t.text.strip()) >= 2]
    if not meaningful:
        return ""

    ordered = sorted(meaningful, key=lambda t: (t.box.y1, t.box.x1))
    heights = sorted(t.box.y2 - t.box.y1 for t in ordered)
    median_h = heights[len(heights) // 2]
    row_threshold = max(10.0, median_h * 0.6)

    line_y1 = ordered[0].box.y1
    primary = [t for t in ordered if t.box.y1 - line_y1 <= row_threshold]
    primary.sort(key=lambda t: t.box.x1)

    parts: list[str] = []
    last: Any = None
    for token in primary:
        text = token.text.strip()
        if last is not None and _tokens_overlap(last, token):
            if len(text) > len(parts[-1]):
                parts[-1] = text
                last = token
            continue
        parts.append(text)
        last = token
    return " ".join(_dedupe_preserve_order(parts))


def merge_adjacent_boxes(dets: list[Any]) -> list[Any]:
    """Merge vertically adjacent, horizontally overlapping YOLO text boxes.

    Reduces DBNet calls for ROI-OCR mode.
    """
    if not dets:
        return []
    from ..schema import Detection, Box
    sorted_dets = sorted(dets, key=lambda d: (d.box.y1, d.box.x1))
    merged = [sorted_dets[0]]
    for d in sorted_dets[1:]:
        prev = merged[-1]
        avg_h = ((prev.box.y2 - prev.box.y1) + (d.box.y2 - d.box.y1)) / 2
        y_gap = d.box.y1 - prev.box.y2
        x_overlap = max(0, min(prev.box.x2, d.box.x2) - max(prev.box.x1, d.box.x1))
        x_ratio = x_overlap / max(
            prev.box.x2 - prev.box.x1, d.box.x2 - d.box.x1, 1)
        if y_gap < avg_h * 1.5 and x_ratio > 0.3:
            merged[-1] = Detection(
                id="merged", label="merged", confidence=0.5,
                box=Box(min(prev.box.x1, d.box.x1), min(prev.box.y1, d.box.y1),
                        max(prev.box.x2, d.box.x2), max(prev.box.y2, d.box.y2)))
        else:
            merged.append(d)
    return merged


# ── Internal helpers ────────────────────────────────────────────

def _tokens_overlap(a: Any, b: Any) -> bool:
    """Horizontal + vertical overlap detection (split token rejoin)."""
    return (
        a.box.x2 > b.box.x1 and b.box.x2 > a.box.x1
        and min(a.box.y2, b.box.y2) > max(a.box.y1, b.box.y1)
    )


def _dedupe_preserve_order(values: list[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        if value and value not in seen:
            seen.add(value)
            result.append(value)
    return result
