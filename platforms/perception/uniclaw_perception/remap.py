"""Coordinate remapping: preprocessed pixel space → original full-screenshot space.

Preserves exact behavior from server.py _remap_coords().
"""
from __future__ import annotations

from typing import Any
import math


def _valid_bounds(bounds: Any, width: int, height: int) -> bool:
    if not isinstance(bounds, list) or len(bounds) != 4:
        return False
    try:
        x1, y1, x2, y2 = (float(v) for v in bounds)
    except (TypeError, ValueError):
        return False
    return (all(math.isfinite(v) for v in (x1, y1, x2, y2))
            and 0 <= x1 < x2 <= width and 0 <= y1 < y2 <= height)


def _valid_normalized(bounds: Any) -> bool:
    if not isinstance(bounds, dict):
        return False
    try:
        x1, y1, x2, y2 = (float(bounds[k]) for k in ("x1", "y1", "x2", "y2"))
    except (KeyError, TypeError, ValueError):
        return False
    return (all(math.isfinite(v) for v in (x1, y1, x2, y2))
            and 0 <= x1 < x2 <= 1 and 0 <= y1 < y2 <= 1)


def validate_geometry(
    items: list[dict[str, Any]],
    *,
    space_label: str,
    pixel_limits: tuple[int, int] | None = None,
) -> tuple[list[dict[str, Any]], int]:
    """Validate one serialized collection's geometry (CORR-GEO).

    Contract per item (explicit space_label records which coordinate space
    the contract belongs to — CORR-GEO-08):
      • bounds  (normalized dict) — finite, 0<=x1<x2<=1, 0<=y1<y2<=1
      • boundsPx (pixel list)     — finite, ordered, within pixel_limits
        when pixel_limits is provided; else finite + ordered.
    Invalid items are DROPPED; valid siblings PRESERVED. Never clamps.
    Returns (valid_items, rejected_count).
    """
    valid: list[dict[str, Any]] = []
    rejected = 0
    for item in items:
        bounds_ok = _valid_normalized(item.get("bounds"))
        bounds_px_ok = True
        px = item.get("boundsPx")
        if px is not None:
            if not isinstance(px, list) or len(px) != 4:
                bounds_px_ok = False
            else:
                try:
                    x1, y1, x2, y2 = (float(v) for v in px)
                except (TypeError, ValueError):
                    bounds_px_ok = False
                else:
                    finite = all(math.isfinite(v) for v in (x1, y1, x2, y2))
                    ordered = x1 < x2 and y1 < y2
                    if pixel_limits is not None:
                        w, h = pixel_limits
                        within = (0 <= x1 < x2 <= w and 0 <= y1 < y2 <= h)
                    else:
                        within = x1 >= 0 and y1 >= 0
                    bounds_px_ok = finite and ordered and within
        if bounds_ok and bounds_px_ok:
            valid.append(item)
        else:
            rejected += 1
    return valid, rejected


def enforce_geometry(
    evidence: dict[str, Any],
    *,
    orig_limits: tuple[int, int] | None = None,
    proc_limits: tuple[int, int] | None = None,
) -> int:
    """Complete response-boundary geometry enforcement (GAP-002).

    Validates EVERY collection that can carry geometry across the
    production/evaluation evidence boundary — no collection left
    unchecked, no alternate serialization path:

      candidates / yolo / ocr  — canonical production evidence;
                                 normalized post-remap contract with
                                 original-frame pixel limits (orig_limits)
      stage views              — evaluation observability with their
                                 owned coordinate contracts:
        rawModelDetections / normalizedDetections → pre-remap pixel space
                                 (proc_limits)
        fusedEvidence                            → post-remap normalized
                                 (orig_limits)

    All-invalid → semantic empty + status INVALID_GEOMETRY (never
    OK_EMPTY). Mixed → drop invalid, preserve valid siblings.
    Returns total rejected count.
    """
    total_rejected = 0
    for key in ("candidates", "yolo", "ocr"):
        if key not in evidence:
            continue
        items = evidence.get(key, [])
        if not isinstance(items, list):
            items = []
        valid, rejected = validate_geometry(
            items, space_label="NORMALIZED_PRODUCTION",
            pixel_limits=orig_limits)
        if rejected:
            total_rejected += rejected
            evidence[key] = valid
            diagnostics = evidence.setdefault("diagnostics", [])
            if not any(isinstance(d, dict) and d.get("code") == "INVALID_GEOMETRY"
                       for d in diagnostics):
                diagnostics.append({"code": "INVALID_GEOMETRY"})
            if not valid:
                evidence["status"] = "INVALID_GEOMETRY"
    return total_rejected


def enforce_stage_views(
    views: dict[str, Any],
    evidence: dict[str, Any],
    *,
    proc_limits: tuple[int, int] | None = None,
    orig_limits: tuple[int, int] | None = None,
) -> int:
    """Validate evaluation stage views against their OWNED coordinate
    contracts (CORR-GEO-08): raw/normalized detections are pre-remap pixel
    space (proc_limits); fusedEvidence is post-remap normalized
    (orig_limits)."""
    total_rejected = 0
    for key, limits, label in (
        ("rawModelDetections", proc_limits, "PROC_PIXEL"),
        ("normalizedDetections", proc_limits, "PROC_PIXEL"),
        ("fusedEvidence", orig_limits, "NORMALIZED_PRODUCTION"),
    ):
        if key not in views:
            continue
        items = views.get(key, [])
        if not isinstance(items, list):
            items = []
        valid, rejected = validate_geometry(
            items, space_label=label, pixel_limits=limits)
        if rejected:
            total_rejected += rejected
            views[key] = valid
            diagnostics = evidence.setdefault("diagnostics", [])
            if not any(isinstance(d, dict) and d.get("code") == "INVALID_GEOMETRY"
                       for d in diagnostics):
                diagnostics.append({"code": "INVALID_GEOMETRY"})
    return total_rejected


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
    # Preserve the historical no-op contract for identity transforms while
    # still rejecting invalid normalized production candidates.  The
    # pipeline's identity case already carries original-space pixels.
    identity_transform = scale == 1.0 and top_px == 0.0

    def _remap_item(obj: dict[str, Any]) -> None:
        # ── pixel coords: boundsPx ──
        if (not identity_transform and "boundsPx" in obj
                and isinstance(obj["boundsPx"], list) and len(obj["boundsPx"]) == 4):
            if not all(isinstance(v, (int, float)) and math.isfinite(float(v)) for v in obj["boundsPx"]):
                return
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
        if (not identity_transform and "centerPx" in obj
                and isinstance(obj["centerPx"], list) and len(obj["centerPx"]) == 2):
            cx = obj["centerPx"][0] * scale
            cy = obj["centerPx"][1] * scale + top_px
            obj["centerPx"] = [round(cx), round(cy)]

            obj["center"] = {
                "x": round(cx / orig_w, 6) if orig_w else 0.0,
                "y": round(cy / orig_h, 6) if orig_h else 0.0,
            }

        # coordinate (same as center in candidate schema)
        if (not identity_transform and "coordinate" in obj
                and isinstance(obj["coordinate"], dict)):
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

    if not identity_transform:
        for c in evidence.get("candidates", []):
            _remap_item(c)
        for d in evidence.get("yolo", []):
            _remap_item(d)
        for t in evidence.get("ocr", []):
            _remap_item(t)

    # Reject malformed post-remap candidates without clamping; retain valid
    # siblings and expose a stable diagnostic for callers.
    candidates = evidence.get("candidates", [])
    valid = []
    rejected = 0
    for candidate in candidates:
        if orig_w > 0 and orig_h > 0 and _valid_normalized(
            candidate.get("bounds")
        ):
            valid.append(candidate)
        else:
            rejected += 1
    if rejected:
        evidence["candidates"] = valid
        diagnostics = evidence.setdefault("diagnostics", [])
        if not any(
            isinstance(item, dict) and item.get("code") == "INVALID_GEOMETRY"
            for item in diagnostics
        ):
            diagnostics.append({"code": "INVALID_GEOMETRY"})
        if not valid:
            evidence["status"] = "INVALID_GEOMETRY"

    if not identity_transform and "image" in evidence:
        evidence["image"]["width"] = orig_w
        evidence["image"]["height"] = orig_h
    if not identity_transform and "metadata" in evidence:
        evidence["metadata"]["width"] = orig_w
        evidence["metadata"]["height"] = orig_h
