"""YOLO inference module for the UniClaw Perception Platform.

Owns: YOLO model loading (singleton cache), inference invocation,
detection postprocessing, model warmup.

Extracted from backends.py run_yolo, run_yolo_on_image, _get_yolo_model, warmup_yolo.
Preserves exact behavior.
"""
from __future__ import annotations

from typing import Any

from PIL import Image

from ..schema import Box, Detection
from ..config import get_config
from .labels import normalize_yolo_label


# ── YOLO model cache (module-level singleton) ───────────────────
_yolo_model_cache: dict[str, Any] = {}


def _get_yolo_model(model_path: str) -> Any:
    """Module-level model cache: same model_path loads once (server warmup reuses)."""
    try:
        from ultralytics import YOLO
    except ImportError as exc:
        raise RuntimeError(
            "ultralytics is not installed. Install requirements/runtime.txt."
        ) from exc
    if model_path not in _yolo_model_cache:
        _yolo_model_cache[model_path] = YOLO(model_path)
    return _yolo_model_cache[model_path]


def run_yolo_on_image(
    image: Image.Image,
    *,
    model_path: str | None = None,
    image_size: int | None = None,
    confidence: float | None = None,
    device: str = "cpu",
) -> list[Detection]:
    """PIL Image in-memory inference (zero disk). Model cached at module level.

    ultralytics predict(source=...) natively accepts PIL Image, internally
    processes as RGB.
    """
    cfg = get_config()
    model_path = model_path or cfg.model_path
    image_size = image_size or cfg.image_size
    confidence = confidence or cfg.detection_confidence

    results = _get_yolo_model(model_path).predict(
        source=image, imgsz=image_size, conf=confidence,
        device=device, verbose=False)

    detections: list[Detection] = []
    for result in results:
        names = result.names
        boxes = result.boxes
        if boxes is None:
            continue
        for box in boxes:
            xyxy = [float(v) for v in box.xyxy[0].tolist()]
            cls = int(box.cls[0].item())
            conf = float(box.conf[0].item())
            raw_label = str(names.get(cls, cls))
            detections.append(
                Detection(
                    id=f"det_{len(detections) + 1}",
                    label=normalize_yolo_label(raw_label),
                    confidence=conf,
                    box=Box.from_list(xyxy),
                )
            )
    return detections


_WARMUP_IMAGE: Image.Image | None = None


def _get_warmup_image() -> Image.Image:
    global _WARMUP_IMAGE
    if _WARMUP_IMAGE is None:
        _WARMUP_IMAGE = Image.new("RGB", (640, 640), (0, 0, 0))
    return _WARMUP_IMAGE


def warmup_yolo() -> None:
    """Warm up YOLO: load model + run one inference on synthetic image.

    First model load: 3-5s. Subsequent calls: cached.
    """
    cfg = get_config()
    run_yolo_on_image(
        _get_warmup_image(),
        model_path=cfg.model_path,
        image_size=cfg.image_size,
        confidence=cfg.detection_confidence,
        device="cpu",
    )
