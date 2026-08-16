"""Fusion heuristics: chevron alignment, search-box pre-labeling, text extraction.

Extracted from fusion.py. Heuristic policy — encodes domain assumptions
about Android Settings UI patterns. NOT semantic authority.
"""
from __future__ import annotations

from typing import Any


# YOLO labels that signal "interactive list row widget"
_ROW_WIDGET_LABELS = {"icon", "switch", "toggle", "checkbox"}

# Generic Android switch track morphology used by the raw-pixel toggle
# detector. Reference values in the pipeline's canonical preprocessed
# input space (max_width=720); the detector scales them by img_w/720 so
# it also works on full-resolution captures. Real tracks are 34-110px
# wide and 16-60px tall in 720-space (DPI-dependent: ~35x21px on a
# 1080px capture, ~90x55px on higher-density captures); the old
# screen-fraction thresholds (8% width / 2% height) rejected real
# 52x31px tracks on a 1080px viewport.
_TOGGLE_MIN_W = 34
_TOGGLE_MAX_W = 110
_TOGGLE_MIN_H = 16
_TOGGLE_MAX_H = 60
_TOGGLE_MIN_ASPECT = 1.2
_TOGGLE_MAX_ASPECT = 3.0
# Generic right-side control placement for Settings-style rows.
_TOGGLE_RIGHT_ZONE = 0.55
# Background-relative contrast: catches light-gray OFF tracks (|229-240|=11).
_TOGGLE_CONTRAST = 8.0
# Thumb must differ strongly from its track (real ON knob |255-104|=151,
# real OFF knob |121-229|=108).
_TOGGLE_KNOB_CONTRAST = 25.0


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



# ── Toggle inference heuristic ────────────────────────────────────

def apply_toggle_inference_heuristic(candidates: list[dict[str, Any]], *, image: Any | None = None) -> None:
    """Infer toggle type from structural/geometric evidence.

    When YOLO does not provide switch/toggle/checkbox labels, this heuristic
    attempts to infer toggle controls from candidate geometry and (if available)
    raw pixel data.

    Evidence dimensions:
    - Compact horizontal shape (narrower than row, wider than tall)
    - Right-side placement relative to text row
    - Vertical overlap with text row
    - Bounded distance from row text end
    - Repeated settings-row structure

    Does NOT infer toggle from text content alone.
    Does NOT use target-name, semantic knowledge, or scenario-specific data.
    """
    # Collect row-like candidates (text blocks, menu items, etc.)
    rows = [c for c in candidates if c.get("type") in
            {"text_block", "menu_item", "button"} and c.get("text", "").strip()]
    if not rows:
        return

    # Collect potential control candidates (icon, empty text, or untagged).
    # Exclude text rows from being their own control candidate.
    # Exclude already-typed switch/toggle candidates: they are already
    # represented as candidates and must not be re-inferred (which would
    # duplicate the control or inherit a loose detector box).
    row_ids = {row.get("id") for row in rows if row.get("id")}
    controls = [c for c in candidates
                if c.get("id") not in row_ids
                and c.get("type") not in {"switch", "toggle"}
                and (c.get("type") in {"icon", "text_block"}
                     or not c.get("text", "").strip())]

    # If image is available, perform raw-pixel toggle detection
    if image is not None:
        raw_toggle_controls = _detect_toggle_regions_from_image(image, rows)
        if raw_toggle_controls:
            # A raw-pixel region that already overlaps an existing
            # switch/toggle candidate represents the same control — skip it
            # so we never double-emit the same switch.
            existing_switch_bounds = [
                c.get("bounds", {}) for c in candidates
                if c.get("type") in {"switch", "toggle"} and c.get("bounds")
            ]
            for raw in raw_toggle_controls:
                rb = raw.get("bounds", {})
                if any(_iou(rb, eb) >= 0.3 for eb in existing_switch_bounds):
                    continue
                controls.append(raw)

    next_index = len(candidates) + 1
    inferred_toggles = []
    # A control claimed by one row must not be re-claimed by another
    # (OCR can emit duplicate/overlapping rows for the same visual row).
    used_control_ids: set[str] = set()

    for row in rows:
        r_bounds = row.get("bounds", {})
        if not r_bounds:
            continue
        r_y1 = r_bounds.get("y1", 0)
        r_y2 = r_bounds.get("y2", 0)
        r_x2 = r_bounds.get("x2", 0)

        # Look for a compatible control on the right side of this row
        best_control = None
        best_dist = float("inf")

        for ctrl in controls:
            c_id = ctrl.get("id")
            if c_id in used_control_ids:
                continue
            c_bounds = ctrl.get("bounds", {})
            if not c_bounds:
                continue
            c_y1 = c_bounds.get("y1", 0)
            c_y2 = c_bounds.get("y2", 0)
            c_x1 = c_bounds.get("x1", 0)
            c_x2 = c_bounds.get("x2", 0)

            # Must have vertical overlap with the row
            if not _vertical_overlap(r_y1, r_y2, c_y1, c_y2):
                continue

            # Must be to the right of the row text (or near the right edge)
            if c_x1 < r_x2 - 0.05:  # Control is too far left of the row's right edge
                continue

            # Check aspect ratio: wider than tall (toggle-like)
            width = c_x2 - c_x1
            height = c_y2 - c_y1
            if height <= 0 or width <= 0:
                continue
            aspect = width / height
            if aspect < 1.0 or aspect > 5.0:  # Not toggle-like shape
                continue

            # Measure distance from row right edge (for ranking only)
            dist = c_x1 - r_x2
            if dist < 0:
                dist = 0  # overlapping is okay
            if dist < best_dist:
                best_dist = dist
                best_control = ctrl

        # Accept if the control lies in the generic right-side control zone.
        # Real Android Settings rows place toggles near the viewport right edge,
        # which can be far from the text. A single global distance threshold
        # (e.g., 0.5) would reject legitimate far-right toggles.
        # The right-side zone is a generic structural property of Settings-style
        # rows, not a page-specific coordinate.
        if best_control is not None and best_dist < 1.0:
            c_bounds = best_control.get("bounds", {})
            c_x1 = c_bounds.get("x1", 0)
            # Must be in the right portion of the viewport where toggles live
            if c_x1 >= 0.55:
                # Found a potential toggle. Create inferred toggle candidate.
                c_bounds = best_control.get("bounds", {})
                width = c_bounds.get("x2", 0) - c_bounds.get("x1", 0)
                height = c_bounds.get("y2", 0) - c_bounds.get("y1", 0)

                # Determine switch state from visual/positional evidence
                # (in a separate function for clarity)
                switch_state = _infer_switch_state_from_bounds(c_bounds)

                # Carry raw-pixel provenance through to the final candidate so
                # consumers can distinguish detector-derived vs raw-pixel-derived
                # toggle evidence.
                ctrl_evidence = best_control.get("evidence", {}) or {}
                ctrl_risk = best_control.get("riskFlags") or []
                risk_flags = ["inferred_toggle"]
                for rf in ctrl_risk:
                    if rf not in risk_flags:
                        risk_flags.append(rf)

                inferred_toggle = {
                    "id": f"candidate_{next_index}",
                    "type": "switch",  # Will be mapped to "toggle" by label mapping
                    "text": "",
                    "confidence": round(0.5, 6),  # Inferred, not detected
                    "bounds": {
                        "x1": c_bounds.get("x1", 0),
                        "y1": c_bounds.get("y1", 0),
                        "x2": c_bounds.get("x2", 0),
                        "y2": c_bounds.get("y2", 0),
                    },
                    "boundsPx": best_control.get("boundsPx", []),
                    "center": {
                        "x": (c_bounds.get("x1", 0) + c_bounds.get("x2", 0)) / 2,
                        "y": (c_bounds.get("y1", 0) + c_bounds.get("y2", 0)) / 2,
                    },
                    "centerPx": best_control.get("centerPx", [0, 0]),
                    "evidence": {
                        "yoloId": ctrl_evidence.get("yoloId"),
                        "ocrIds": [],
                        "allIds": [],
                        "typeInferred": ctrl_evidence.get("typeInferred", "toggle_geometry"),
                        "associatedRowId": row.get("id", ""),
                    },
                    "riskFlags": risk_flags,
                    "switch_state": switch_state,
                }
                inferred_toggles.append(inferred_toggle)
                next_index += 1
                used_control_ids.add(best_control.get("id"))

    # Add inferred toggles to candidates
    candidates.extend(inferred_toggles)

    # Post-inference de-duplication: the same physical switch can be
    # reached through multiple evidence paths in one pass (YOLO's own
    # switch class with a loose box, a YOLO icon box, and the raw-pixel
    # detector). Overlapping switch/toggle candidates (IoU >= 0.6)
    # represent the same control; keep the tightest raw-pixel one so the
    # C# ImageSwitchStateProvider samples the actual track pixels.
    switch_cands = [c for c in candidates
                    if c.get("type") in {"switch", "toggle"} and c.get("bounds")]
    if len(switch_cands) > 1:
        kept: list[dict[str, Any]] = []
        for c in switch_cands:
            cb = c.get("bounds", {})
            dup = next((k for k in kept if _iou(cb, k.get("bounds", {})) >= 0.6), None)
            if dup is None:
                kept.append(c)
                continue
            c_ev = (c.get("evidence") or {}).get("typeInferred")
            k_ev = (dup.get("evidence") or {}).get("typeInferred")
            c_area = (cb.get("x2", 0) - cb.get("x1", 0)) * (cb.get("y2", 0) - cb.get("y1", 0))
            k_bounds = dup.get("bounds", {})
            k_area = (k_bounds.get("x2", 0) - k_bounds.get("x1", 0)) * (k_bounds.get("y2", 0) - k_bounds.get("y1", 0))
            if (c_ev == "raw_pixel_toggle" and k_ev != "raw_pixel_toggle") or \
               (c_ev == k_ev and c_area < k_area):
                kept.remove(dup)
                kept.append(c)
        if len(kept) != len(switch_cands):
            non_switches = [c for c in candidates
                            if c.get("type") not in {"switch", "toggle"}]
            candidates[:] = non_switches + kept


def _detect_toggle_regions_from_image(
    image: Any,
    rows: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Detect toggle-like regions from raw image pixels.

    For each text row, scan the right-side region for a compact,
    horizontally-elongated structure whose interior shows the
    characteristic "track + thumb" luminance structure of an Android
    switch: a rounded track with a contrasting circular thumb inset
    inside it.

    Fail-closed: a structure is only emitted as a switch candidate when
    BOTH the track geometry (width/height/aspect/right-zone) AND the
    interior thumb evidence (compact, inset, contrasting cluster) are
    present. Chevrons, badges, pills, icons, and uniform controls are
    rejected because they lack one of the two signals.

    Thresholds are generic Android switch morphology (36-100px wide
    tracks, 18-46px tall) rather than screen-fraction heuristics, so
    real 52x31px tracks on a 1080px-wide viewport are no longer rejected
    by the old 8% screen-width minimum. The contrast threshold is
    background-relative (local median), which also catches light-gray
    OFF tracks.

    Returns a list of synthetic control candidates with type="switch"
    and tight track bounds (normalized to the full image).
    """
    try:
        import numpy as np
        from PIL import Image as PILImage
    except ImportError:
        return []

    # Convert image to numpy array if it's a PIL Image
    if hasattr(image, "convert"):
        img_array = np.array(image.convert("RGB"))
    else:
        return []

    img_h, img_w = img_array.shape[:2]
    if img_h < 40 or img_w < 40:
        return []
    gray = img_array.astype(np.float32).mean(axis=2)

    # Track geometry is expressed relative to the pipeline's canonical
    # preprocessed width (max_width=720): real Android switch tracks are
    # 34-100px wide and 16-46px tall in that space. Scaling by img_w/720
    # keeps the detector correct when it is also run directly on
    # full-resolution captures (e.g. 1080px-wide reality assets).
    ref = img_w / 720.0
    min_w = max(30, int(round(_TOGGLE_MIN_W * ref)))
    max_w = max(min_w + 1, int(round(_TOGGLE_MAX_W * ref)))
    min_h = max(14, int(round(_TOGGLE_MIN_H * ref)))
    max_h = max(min_h + 1, int(round(_TOGGLE_MAX_H * ref)))

    results: list[dict[str, Any]] = []

    for row in rows:
        bounds = row.get("bounds", {})
        if not bounds:
            continue

        r_y1 = bounds.get("y1", 0)
        r_y2 = bounds.get("y2", 0)
        r_x2 = bounds.get("x2", 0)

        # Search region: from row text end to the screen right edge,
        # vertically padded around the row (the switch is vertically
        # centered on the row touch target, which is taller than the
        # OCR text box).
        px_x1 = max(0, int(r_x2 * img_w))
        px_x2 = img_w - 1
        row_h_px = max((r_y2 - r_y1) * img_h, 14.0)
        pad = max(12.0, row_h_px * 0.5)
        px_y1 = max(0, int(r_y1 * img_h - pad))
        px_y2 = min(img_h - 1, int(r_y2 * img_h + pad))

        if px_x2 - px_x1 < 44 or px_y2 - px_y1 < 26:
            continue

        band = gray[px_y1:px_y2 + 1, px_x1:px_x2 + 1]
        band_h, band_w = band.shape

        # Background-relative contrast: the background dominates the band,
        # so its median is a robust local background estimate. A small
        # threshold catches light-gray OFF tracks (|229-240| = 11) that
        # the old global diff > 30 missed.
        bg = float(np.median(band))
        mask = np.abs(band - bg) > _TOGGLE_CONTRAST

        labels, n = _components_2d(mask)
        if n == 0:
            continue

        for comp in range(1, n + 1):
            ys, xs = np.nonzero(labels == comp)
            if ys.size == 0:
                continue
            cx1, cx2 = xs.min(), xs.max()
            cy1, cy2 = ys.min(), ys.max()
            cw = cx2 - cx1 + 1
            ch = cy2 - cy1 + 1

            # Generic track geometry (reference 720px-wide input space).
            if cw < min_w or cw > max_w:
                continue
            if ch < min_h or ch > max_h:
                continue
            aspect = cw / ch
            if aspect < _TOGGLE_MIN_ASPECT or aspect > _TOGGLE_MAX_ASPECT:
                continue

            # Generic right-side control placement (Settings-style rows).
            abs_x1 = px_x1 + cx1
            if abs_x1 < _TOGGLE_RIGHT_ZONE * img_w:
                continue

            # A track clipped against the search-band edges is incomplete
            # (e.g. cut off by the text-end boundary) -> fail closed.
            if cy1 <= 1 or cy2 >= band_h - 2 or cx2 >= band_w - 2:
                continue

            # Interior thumb (knob) validation — the decisive fail-closed
            # check against chevrons/icons/badges/pills.
            if not _has_knob(gray, abs_x1, px_y1 + cy1, px_x1 + cx2, px_y1 + cy2):
                continue

            norm_x1 = abs_x1 / img_w
            norm_x2 = (px_x1 + cx2 + 1) / img_w
            norm_y1 = (px_y1 + cy1) / img_h
            norm_y2 = (px_y1 + cy2 + 1) / img_h

            results.append({
                "id": f"raw_toggle_{len(results) + 1}",
                "type": "switch",
                "text": "",
                "bounds": {
                    "x1": norm_x1, "y1": norm_y1,
                    "x2": norm_x2, "y2": norm_y2,
                },
                "boundsPx": [
                    int(norm_x1 * img_w), int(norm_y1 * img_h),
                    int(norm_x2 * img_w), int(norm_y2 * img_h),
                ],
                "center": {
                    "x": (norm_x1 + norm_x2) / 2,
                    "y": (norm_y1 + norm_y2) / 2,
                },
                "centerPx": [
                    int((norm_x1 + norm_x2) / 2 * img_w),
                    int((norm_y1 + norm_y2) / 2 * img_h),
                ],
                "evidence": {
                    "yoloId": None,
                    "ocrIds": [],
                    "allIds": [],
                    "typeInferred": "raw_pixel_toggle",
                },
                "riskFlags": ["raw_pixel_toggle"],
            })

    # De-duplicate regions that overlap (the same switch reached from
    # overlapping OCR rows for the same visual row).
    deduped: list[dict[str, Any]] = []
    for r in results:
        rb = r.get("bounds", {})
        if any(_iou(rb, d.get("bounds", {})) >= 0.6 for d in deduped):
            continue
        deduped.append(r)
    return deduped


def _components_2d(mask):
    """Label 4-connected components of a boolean mask (numpy-only BFS)."""
    import numpy as np

    h, w = mask.shape
    labels = np.zeros((h, w), dtype=np.int32)
    current = 0
    for y in range(h):
        for x in range(w):
            if mask[y, x] and labels[y, x] == 0:
                current += 1
                stack = [(y, x)]
                labels[y, x] = current
                while stack:
                    cy, cx = stack.pop()
                    if cy > 0 and mask[cy - 1, cx] and labels[cy - 1, cx] == 0:
                        labels[cy - 1, cx] = current
                        stack.append((cy - 1, cx))
                    if cy < h - 1 and mask[cy + 1, cx] and labels[cy + 1, cx] == 0:
                        labels[cy + 1, cx] = current
                        stack.append((cy + 1, cx))
                    if cx > 0 and mask[cy, cx - 1] and labels[cy, cx - 1] == 0:
                        labels[cy, cx - 1] = current
                        stack.append((cy, cx - 1))
                    if cx < w - 1 and mask[cy, cx + 1] and labels[cy, cx + 1] == 0:
                        labels[cy, cx + 1] = current
                        stack.append((cy, cx + 1))
    return labels, current


def _has_knob(gray, x1, y1, x2, y2):
    """True when a track-like region contains an inset contrasting thumb.

    Operates on absolute pixel coordinates. The thumb must be a compact
    interior cluster whose luminance differs strongly from the track
    median and whose horizontal run is strictly inside the track width
    (neither absent nor filling the whole track). This fail-closed rule
    rejects chevrons (glyph fills bbox), badges/pills (no interior
    contrast or text fills width) and plain uniform controls (no thumb).

    The check runs on the track interior: the outer 2px ring of the
    component bbox can contain anti-aliasing halo pixels around the
    track edge, while a real thumb is always fully interior.
    """
    import numpy as np

    track = gray[y1:y2 + 1, x1:x2 + 1]
    th, tw = track.shape
    if th < 14 or tw < 30:
        return False
    inner = track[2:th - 2, 2:tw - 2]
    ih, iw = inner.shape
    if ih < 8 or iw < 24:
        return False
    med = float(np.median(inner))
    out = np.abs(inner - med) > _TOGGLE_KNOB_CONTRAST

    # Middle vertical band (the thumb is vertically centered in the track).
    mid = out[ih // 4: 3 * ih // 4, :]
    if mid.size == 0:
        return False

    col_any = mid.any(axis=0)

    # Longest contiguous outlier run — the thumb itself. The track's own
    # 1-2px outline ring can create isolated outlier columns at the very
    # edges of the interior; those must not count as thumb evidence.
    runs: list[tuple[int, int, int]] = []
    run = 0
    run_start = 0
    for i, v in enumerate(col_any):
        if v:
            if run == 0:
                run_start = i
            run += 1
        else:
            if run:
                runs.append((run_start, run_start + run - 1, run))
            run = 0
    if run:
        runs.append((run_start, run_start + run - 1, run))
    if not runs:
        return False
    s, e, longest_run = max(runs, key=lambda r: r[2])

    knob_area = int(mid.sum())
    track_area = ih * iw

    # Thumb must occupy a meaningful but sub-dominant share of the track.
    if knob_area < 0.03 * track_area:
        return False
    if longest_run < 0.22 * iw:
        return False
    if longest_run > 0.78 * iw:
        return False

    # Thumb must be horizontally inset: a real knob never fills the
    # track width (a glyph that fills the box does). One-edge contact
    # with the interior is tolerated — at downscaled pipeline
    # resolutions the thumb's anti-aliased edge can blend into the
    # track cap and reach the interior boundary — but the thumb must
    # not then dominate the width, and a run filling the whole
    # interior is always rejected.
    if s <= 0 and e >= iw - 1:
        return False
    run_w = e - s + 1
    if s <= 0 and run_w > 0.6 * iw:
        return False
    if e >= iw - 1 and run_w > 0.6 * iw:
        return False

    # Thumb must be vertically concentrated in the middle band.
    knob_mid = mid[:, s:e + 1]
    rows_with_knob = knob_mid.any(axis=1).nonzero()[0]
    if rows_with_knob.size == 0:
        return False
    frac = (rows_with_knob[-1] - rows_with_knob[0] + 1) / ih
    if frac > 0.62:
        return False

    return True


def _iou(a: dict[str, float], b: dict[str, float]) -> float:
    """Intersection-over-union of two normalized bound dicts."""
    ax1, ay1, ax2, ay2 = a.get("x1", 0), a.get("y1", 0), a.get("x2", 0), a.get("y2", 0)
    bx1, by1, bx2, by2 = b.get("x1", 0), b.get("y1", 0), b.get("x2", 0), b.get("y2", 0)
    ix1 = max(ax1, bx1)
    iy1 = max(ay1, by1)
    ix2 = min(ax2, bx2)
    iy2 = min(ay2, by2)
    iw = max(0.0, ix2 - ix1)
    ih = max(0.0, iy2 - iy1)
    inter = iw * ih
    if inter <= 0.0:
        return 0.0
    union = (ax2 - ax1) * (ay2 - ay1) + (bx2 - bx1) * (by2 - by1) - inter
    if union <= 0.0:
        return 0.0
    return inter / union


def _vertical_overlap(y1_a: float, y2_a: float, y1_b: float, y2_b: float) -> bool:
    """Check if two vertical ranges overlap."""
    return y1_a < y2_b and y1_b < y2_a


def _infer_switch_state_from_bounds(bounds: dict[str, float]) -> bool | None:
    """Infer switch state from visual/positional evidence.

    This is a placeholder that returns None (UNKNOWN) when visual evidence
    is insufficient. Actual implementation would use pixel-level analysis
    to determine knob position relative to track.

    Returns:
        True for ON, False for OFF, None for UNKNOWN/ambiguous.
    """
    # Without pixel-level analysis, we cannot determine state reliably.
    # Return None to indicate UNKNOWN.
    return None


def _dedupe_preserve_order(values: list[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        if value and value not in seen:
            seen.add(value)
            result.append(value)
    return result
