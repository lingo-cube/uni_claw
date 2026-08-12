"""Coordinate remapping: preprocessed pixel space → original full-screenshot space.

Preserves exact behavior from server.py _remap_coords().
"""
from __future__ import annotations

from typing import Any


def remap_coords(
    evidence: dict[str, Any],
    scale: float,
    top_px: float,
    orig_w: int,
    orig_h: int,
) -> None:
    """Remap all coordinates in evidence from preprocessed to original space.

    Mapping:  x_orig = x_preproc * scale
              y_orig = y_preproc * scale + top_px
    Normalized: norm_x = x_orig / orig_w, norm_y = y_orig / orig_h

    Idempotent: scale==1.0 and top_px==0.0 → no-op.
    """
    if scale == 1.0 and top_px == 0.0:
        return

    def _remap_item(obj: dict[str, Any]) -> None:
        # ── pixel coords: boundsPx ──
        if "boundsPx" in obj and isinstance(obj["boundsPx"], list) and len(obj["boundsPx"]) == 4:
            x1 = obj["boundsPx"][0] * scale
            y1 = obj["boundsPx"][1] * scale + top_px
            x2 = obj["boundsPx"][2] * scale
            y2 = obj["boundsPx"][3] * scale + top_px
            obj["boundsPx"] = [round(x1), round(y1), round(x2), round(y2)]

            obj["bounds"] = {
                "x1": round(x1 / orig_w, 6),
                "y1": round(y1 / orig_h, 6),
                "x2": round(x2 / orig_w, 6),
                "y2": round(y2 / orig_h, 6),
            }

        # ── pixel coords: centerPx ──
        if "centerPx" in obj and isinstance(obj["centerPx"], list) and len(obj["centerPx"]) == 2:
            cx = obj["centerPx"][0] * scale
            cy = obj["centerPx"][1] * scale + top_px
            obj["centerPx"] = [round(cx), round(cy)]

            obj["center"] = {
                "x": round(cx / orig_w, 6) if orig_w else 0.0,
                "y": round(cy / orig_h, 6) if orig_h else 0.0,
            }

        # coordinate (same as center in candidate schema)
        if "coordinate" in obj and isinstance(obj["coordinate"], dict):
            if "centerPx" in obj:
                cx = obj["centerPx"][0]
                cy = obj["centerPx"][1]
            else:
                cx = obj["coordinate"].get("x", 0.0) * orig_w * scale
                cy = obj["coordinate"].get("y", 0.0) * orig_h * scale + top_px
            obj["coordinate"] = {
                "x": round(cx / orig_w, 6) if orig_w else 0.0,
                "y": round(cy / orig_h, 6) if orig_h else 0.0,
            }

    for c in evidence.get("candidates", []):
        _remap_item(c)
    for d in evidence.get("yolo", []):
        _remap_item(d)
    for t in evidence.get("ocr", []):
        _remap_item(t)

    if "image" in evidence:
        evidence["image"]["width"] = orig_w
        evidence["image"]["height"] = orig_h
    if "metadata" in evidence:
        evidence["metadata"]["width"] = orig_w
        evidence["metadata"]["height"] = orig_h
