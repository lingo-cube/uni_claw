"""PaddleOCR inference backend (Paddle Inference C++, LEGACY).

Kept for regression comparison with RapidOCR. Marked as LEGACY.
PaddleOCR 2.10 has a known per-request memory leak (D-4).
RapidOCR is the active production backend (D-198).

Moved as-is from backends.py. NOT refactored.
May be deprecated in Phase 4 if RapidOCR proves sufficient.
"""
from __future__ import annotations

import os
import tempfile
import threading
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image

from ..schema import Box, Detection, OcrToken
from .common import (
    _get_ocr_executor,
    _roi_padding_px,
    crop_padded,
    offset_token,
)


# ── PaddleOCR instance (thread-local, not thread-safe at instance level) ──
_ocr_local = threading.local()


def _get_ocr(language: str = "ch") -> Any:
    """Lazy thread-local PaddleOCR instance (thread-safe via threading.local)."""
    if not hasattr(_ocr_local, "instance"):
        try:
            from paddleocr import PaddleOCR
        except ImportError as exc:
            raise RuntimeError(
                "paddleocr is not installed. Install requirements/runtime.txt."
            ) from exc
        _ocr_local.instance = _create_paddle_ocr(PaddleOCR, language)
    return _ocr_local.instance


def _create_paddle_ocr(paddle_ocr_type: Any, language: str) -> Any:
    candidates = [
        {"use_angle_cls": True, "lang": language, "show_log": False},
        {"use_angle_cls": True, "lang": language},
        {"use_textline_orientation": True, "lang": language},
        {"lang": language},
    ]
    last_error: Exception | None = None
    for kwargs in candidates:
        try:
            return paddle_ocr_type(**kwargs)
        except (TypeError, ValueError) as exc:
            last_error = exc
    assert last_error is not None
    raise last_error


def _call_paddle_ocr(ocr: Any, source: Path | np.ndarray) -> Any:
    """Call PaddleOCR inference (Path | ndarray dual input)."""
    if isinstance(source, Path):
        source = str(source)
    calls = [
        lambda: ocr.ocr(source, cls=True),
        lambda: ocr.ocr(source),
        lambda: ocr.predict(source),
    ]
    last_error: Exception | None = None
    for call in calls:
        try:
            return call()
        except (TypeError, ValueError) as exc:
            last_error = exc
    assert last_error is not None
    raise last_error


def warmup_ocr(language: str = "ch") -> None:
    """Warm up PaddleOCR: main thread + executor worker threads.

    PaddleOCR constructor loads detection/recognition models (first call 2-5s).
    Submitting dummy tasks ensures each worker thread establishes its
    threading.local instance before the first real request.
    """
    _get_ocr(language)
    executor = _get_ocr_executor()
    import os as _os
    n = int(_os.environ.get("UNICLAW_OCR_PARALLEL", "4"))
    n = max(1, min(n, 8))
    list(executor.map(lambda _: _get_ocr(language), range(n)))


# ── ROI-crop OCR (legacy path) ──────────────────────────────────

def run_ocr_on_crops(
    image: Image.Image,
    detections: list[Detection],
    *,
    language: str = "ch",
    padding: int | None = None,
    max_workers: int | None = None,
) -> list[list[OcrToken]]:
    """PaddleOCR on each YOLO detection crop. Returns aligned token lists."""
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
            lambda pair: _ocr_one_crop(pair[0], pair[1], language),
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


def _ocr_one_crop(
    crop: Image.Image,
    detection: Detection,
    language: str,
) -> list[OcrToken]:
    """PaddleOCR on a single crop, tokens offset back to original image coords."""
    if crop.width < 4 or crop.height < 4:
        return []
    ocr = _get_ocr(language)
    tokens = _run_ocr_on_pil(ocr, crop)
    return [offset_token(t, detection.box.x1, detection.box.y1) for t in tokens]


def _run_ocr_on_pil(ocr: Any, crop: Image.Image) -> list[OcrToken]:
    """PaddleOCR on a PIL Image (ndarray path, zero disk; file fallback)."""
    try:
        raw = _call_paddle_ocr(ocr, np.asarray(crop)[:, :, ::-1])
    except (TypeError, ValueError):
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as f:
            crop.save(f, format="PNG")
            tmp_path = f.name
        try:
            raw = _call_paddle_ocr(ocr, Path(tmp_path))
        finally:
            os.unlink(tmp_path)
    return _normalize_paddle_result(raw)


# ── PaddleOCR result normalization ──────────────────────────────

def _normalize_paddle_result(raw: Any) -> list[OcrToken]:
    lines: list[Any] = []
    if isinstance(raw, list):
        for page in raw:
            if isinstance(page, list):
                lines.extend(page)
            elif isinstance(page, dict):
                lines.extend(_lines_from_paddle_dict(page))
            elif hasattr(page, "json"):
                lines.extend(_lines_from_paddle_dict(page.json))
            elif hasattr(page, "to_dict"):
                lines.extend(_lines_from_paddle_dict(page.to_dict()))

    tokens: list[OcrToken] = []
    for line in lines:
        parsed = _parse_paddle_line(line)
        if parsed is None:
            continue
        box, text, confidence = parsed
        tokens.append(
            OcrToken(
                id=f"ocr_{len(tokens) + 1}",
                text=text,
                confidence=confidence,
                box=box,
            )
        )
    return tokens


def _lines_from_paddle_dict(page: dict[str, Any]) -> list[Any]:
    texts = page.get("rec_texts") or []
    scores = page.get("rec_scores") or []
    boxes = page.get("rec_boxes") or page.get("rec_polys") or page.get("dt_polys") or []
    return [[box, (text, score if i < len(scores) else 1.0)] for i, (box, text) in enumerate(zip(boxes, texts))]


def _parse_paddle_line(line: Any) -> tuple[Box, str, float] | None:
    if not isinstance(line, (list, tuple)) or len(line) < 2:
        return None
    raw_box = line[0]
    raw_text = line[1]

    if isinstance(raw_text, (list, tuple)) and len(raw_text) >= 2:
        text = str(raw_text[0])
        confidence = float(raw_text[1])
    else:
        text = str(raw_text)
        confidence = 1.0

    return _box_from_paddle(raw_box), text, confidence


def _box_from_paddle(raw_box: Any) -> Box:
    if isinstance(raw_box, (list, tuple)) and len(raw_box) == 4 and all(
        isinstance(v, (int, float)) for v in raw_box
    ):
        x1, y1, x2, y2 = [float(v) for v in raw_box]
        return Box(x1, y1, x2, y2)

    points = raw_box.tolist() if hasattr(raw_box, "tolist") else raw_box
    xs = [float(point[0]) for point in points]
    ys = [float(point[1]) for point in points]
    return Box(min(xs), min(ys), max(xs), max(ys))
