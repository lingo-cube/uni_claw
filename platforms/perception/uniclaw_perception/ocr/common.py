"""Shared OCR utilities: thread pool, ROI padding, image crop, token offset.

Extracted from backends.py. Shared between RapidOCR and PaddleOCR backends.
"""
from __future__ import annotations

import threading
from concurrent.futures import ThreadPoolExecutor
from typing import Any

from PIL import Image

from ..schema import Box, OcrToken


# ── ROI padding ─────────────────────────────────────────────────
_ROI_PADDING_SPEC: dict[str, float] = {}


def configure_roi_padding(spec: dict[str, float]) -> None:
    """Set ROI padding spec (called at config load time from label-mapping.json)."""
    global _ROI_PADDING_SPEC
    _ROI_PADDING_SPEC = dict(spec)


def _roi_padding_px(box_width: float, box_height: float) -> int:
    """ROI padding proportional to box dimensions, clamped."""
    spec = _ROI_PADDING_SPEC
    px = max(
        spec.get("x", 0.15) * box_width,
        spec.get("y", 0.1) * box_height,
        float(spec.get("minPx", 8)),
    )
    return int(min(px, spec.get("maxPx", 64)))


# ── Thread pool ─────────────────────────────────────────────────
_ocr_executor: ThreadPoolExecutor | None = None
_ocr_parallelism_cache: int | None = None


def _ocr_parallelism() -> int:
    """OCR worker count from env, default 4, clamped to 1-8."""
    import os
    env = os.environ.get("UNICLAW_OCR_PARALLEL", "4")
    try:
        n = int(env)
        return max(1, min(n, 8))
    except ValueError:
        return 2


def _get_ocr_executor() -> ThreadPoolExecutor:
    """Module-level long-lived OCR thread pool: reused across requests."""
    global _ocr_executor
    if _ocr_executor is None:
        _ocr_executor = ThreadPoolExecutor(max_workers=_ocr_parallelism())
    return _ocr_executor


# ── Image crop ──────────────────────────────────────────────────

def crop_padded(
    image: Image.Image,
    box: Box,
    padding: int,
) -> Image.Image | None:
    """Crop a padded region from the image. Returns None if crop is empty."""
    x1 = max(0, int(box.x1) - padding)
    y1 = max(0, int(box.y1) - padding)
    x2 = min(image.width, int(box.x2) + padding)
    y2 = min(image.height, int(box.y2) + padding)
    if x2 <= x1 or y2 <= y1:
        return None
    return image.crop((x1, y1, x2, y2))


# ── Token coordinate offset ─────────────────────────────────────

def offset_token(token: OcrToken, dx: float, dy: float) -> OcrToken:
    """Offset token from crop-local coords back to original image coords."""
    return OcrToken(
        id=token.id,
        text=token.text,
        confidence=token.confidence,
        box=Box(token.box.x1 + dx, token.box.y1 + dy,
                token.box.x2 + dx, token.box.y2 + dy),
    )
