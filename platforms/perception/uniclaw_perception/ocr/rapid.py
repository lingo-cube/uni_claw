"""RapidOCR inference backend (ONNX Runtime, D-198).

Active production OCR backend. Owns: singleton, warmup, full-image inference,
per-crop inference, result normalization.

Thread-safe. No memory leak (unlike PaddleOCR 2.10).
"""
from __future__ import annotations

import threading
from typing import Any

import numpy as np
from PIL import Image

from ..schema import OcrToken
from .common import (
    _get_ocr_executor,
    _roi_padding_px,
    crop_padded,
    offset_token,
)


# ── RapidOCR singleton (process-level, thread-safe) ─────────────
_rapid_ocr_singleton: Any = None
_rapid_ocr_lock = threading.Lock()


def _get_rapid_ocr() -> Any:
    """RapidOCR process-level singleton (D-198): instance is thread-safe."""
    global _rapid_ocr_singleton
    if _rapid_ocr_singleton is None:
        with _rapid_ocr_lock:
            if _rapid_ocr_singleton is None:
                try:
                    from rapidocr_onnxruntime import RapidOCR
                except ImportError as exc:
                    raise RuntimeError(
                        "rapidocr_onnxruntime is not installed. Install "
                        "requirements/runtime.txt.") from exc
                _rapid_ocr_singleton = RapidOCR()
    return _rapid_ocr_singleton


def warmup_rapid_ocr() -> None:
    """Warm up RapidOCR: construct + det/rec kernel init on synthetic images.

    Construction loads ONNX models (1-3s). ORT kernels initialize on first
    real execution — synthetic images move that cost to startup.
    """
    ocr = _get_rapid_ocr()
    try:
        # det kernel: 640×640 black image (no text → no boxes, just warm kernel)
        black = np.zeros((640, 640, 3), dtype=np.uint8)
        ocr.text_det(black)
        # rec kernel: white background + black bar simulating single text line
        line = np.full((48, 320, 3), 255, dtype=np.uint8)
        line[8:40, 16:64] = 0
        ocr.text_rec([line])
    except Exception:
        # Warmup failure does not block startup — first real request pays the
        # kernel init cost.
        return


# ── Full-image inference ────────────────────────────────────────

def run_rapid_ocr_on_image(
    image: Image.Image,
    *,
    text_score: float = 0.5,
) -> list[OcrToken]:
    """Run RapidOCR full pipeline (det→cls→rec) on an entire image.

    Single DBNet pass for detection + batched CRNN for recognition.
    Tokens are in full-image pixel coordinates.
    """
    rgb = image.convert("RGB")  # RGBA → RGB (text_rec asserts 3 channels)
    output = _get_rapid_ocr()(np.asarray(rgb)[:, :, ::-1])
    raw = output[0] if isinstance(output, tuple) else output
    return _normalize_rapid_result(raw, text_score)


# ── Per-crop inference ──────────────────────────────────────────

def run_rapid_ocr_on_crops(
    image: Image.Image,
    detections: list[Any],  # list[Detection]
    *,
    text_score: float = 0.5,
    padding: int | None = None,
    max_workers: int | None = None,
) -> list[list[OcrToken]]:
    """RapidOCR on each YOLO detection crop. Returns aligned token lists."""
    if not detections:
        return []

    if max_workers is None:
        executor = _get_ocr_executor()
        owns_executor = False
    else:
        from concurrent.futures import ThreadPoolExecutor
        executor = ThreadPoolExecutor(max_workers=max_workers)
        owns_executor = True

    try:
        # Step 1: parallel crop
        crops = list(executor.map(
            lambda d: crop_padded(
                image, d.box,
                _roi_padding_px(d.box.x2 - d.box.x1, d.box.y2 - d.box.y1)
                if padding is None else padding,
            ),
            detections,
        ))

        # Step 2: parallel OCR
        pairs = [
            (crop, det)
            for crop, det in zip(crops, detections)
            if crop is not None
        ]
        results = list(executor.map(
            lambda pair: _rapid_ocr_one_crop(pair[0], pair[1], text_score),
            pairs,
        ))
    finally:
        if owns_executor:
            executor.shutdown(wait=True)

    # Rebuild aligned result list
    aligned: list[list[OcrToken]] = []
    idx = 0
    for crop in crops:
        if crop is None:
            aligned.append([])
        else:
            aligned.append(results[idx])
            idx += 1
    return aligned


def _rapid_ocr_one_crop(
    crop: Image.Image,
    detection: Any,  # Detection
    text_score: float,
) -> list[OcrToken]:
    """RapidOCR on a single crop, tokens offset back to original image coords."""
    if crop.width < 4 or crop.height < 4:
        return []
    tokens = _run_rapid_ocr_on_pil(crop, text_score)
    return [offset_token(t, detection.box.x1, detection.box.y1) for t in tokens]


def _run_rapid_ocr_on_pil(crop: Image.Image, text_score: float) -> list[OcrToken]:
    """RapidOCR on a single PIL crop, tokens in crop-local coordinates."""
    if crop.width < 4 or crop.height < 4:
        return []
    rgb = crop.convert("RGB")
    output = _get_rapid_ocr()(np.asarray(rgb)[:, :, ::-1])
    raw = output[0] if isinstance(output, tuple) else output
    return _normalize_rapid_result(raw, text_score)


# ── Result normalization ────────────────────────────────────────

def _normalize_rapid_result(raw: Any, text_score: float) -> list[OcrToken]:
    """RapidOCR result [[box4points], text, score] → OcrToken, low-score filtered."""
    from ..schema import Box as BoxCls

    tokens: list[OcrToken] = []
    if not isinstance(raw, (list, tuple)):
        return tokens
    for item in raw:
        if not isinstance(item, (list, tuple)) or len(item) < 3:
            continue
        try:
            score = float(item[2])
        except (TypeError, ValueError):
            continue
        text = str(item[1]).strip()
        if score < text_score or not text:
            continue
        tokens.append(OcrToken(
            id=f"ocr_{len(tokens) + 1}",
            text=text,
            confidence=score,
            box=_box_from_rapid(item[0]),
        ))
    return tokens


def _box_from_rapid(raw_box: Any) -> BoxCls:
    """Convert RapidOCR box (4-point list) to axis-aligned Box."""
    from ..schema import Box as BoxCls
    if isinstance(raw_box, (list, tuple)) and len(raw_box) == 4 and all(
        isinstance(v, (int, float)) for v in raw_box
    ):
        x1, y1, x2, y2 = [float(v) for v in raw_box]
        return BoxCls(x1, y1, x2, y2)

    points = raw_box.tolist() if hasattr(raw_box, "tolist") else raw_box
    xs = [float(point[0]) for point in points]
    ys = [float(point[1]) for point in points]
    return BoxCls(min(xs), min(ys), max(xs), max(ys))
