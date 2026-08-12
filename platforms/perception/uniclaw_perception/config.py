"""Configuration loading for the UniClaw Perception Platform.

Owns: config loading, environment variable resolution, model/config path
resolution, configHash calculation.

Does NOT own: canonical configId (Phase 4), EffectiveConfigManifest (Phase 4).
"""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
from typing import Any


# ── Path resolution (cwd-independent) ──────────────────────────
_PACKAGE_ROOT = Path(__file__).resolve().parent.parent  # platforms/perception/


def _resolve_path(env_var: str, relative_default: str) -> Path:
    """Resolve a resource path. Priority: env var > package-relative default."""
    env_val = os.environ.get(env_var)
    if env_val:
        path = Path(env_val)
        if not path.is_absolute():
            path = _PACKAGE_ROOT / path
        return path.resolve()
    return (_PACKAGE_ROOT / relative_default).resolve()


# ── Defaults ────────────────────────────────────────────────────
_MODEL_PATH_ENV = "UNICLAW_YOLO_MODEL"
_DEFAULT_MODEL_PATH = "models/yolo/android_ui_detection_yolov8/best.pt"

_CONFIG_PATH_ENV = "UNICLAW_LABEL_MAPPING"
_DEFAULT_CONFIG_PATH = "config/label-mapping.json"

_OCR_LANG = os.environ.get("UNICLAW_OCR_LANG", "en")
_OCR_BACKEND = os.environ.get("UNICLAW_OCR_BACKEND", "rapidocr").lower()
_OCR_TEXT_SCORE = float(os.environ.get("UNICLAW_OCR_TEXT_SCORE", "0.5"))
_IMAGE_SIZE = 640
_OCR_MODE = os.environ.get("UNICLAW_OCR_MODE", "full")

# Preprocessing defaults (env > label-mapping.json spatial.preprocessing)
_PREPROCESS_MAX_WIDTH = int(os.environ.get("UNICLAW_IMAGE_MAX_WIDTH", "720"))
_PREPROCESS_CROP_TOP = float(os.environ.get("UNICLAW_IMAGE_CROP_TOP", "0.0625"))
_PREPROCESS_CROP_BOTTOM = float(os.environ.get("UNICLAW_IMAGE_CROP_BOTTOM", "0.0625"))

# ROI-OCR text labels for per-crop mode
_TEXT_LIKELY_LABELS = frozenset({"text_block", "input", "button", "list_item", "toolbar", "tab"})


# ── Runtime state (populated by load()) ─────────────────────────
class PerceptionConfig:
    """Immutable perception configuration snapshot."""

    def __init__(self) -> None:
        self.spatial: dict[str, Any] = {}
        self.detection_confidence: float = 0.35
        self.config_hash: str = ""
        self.model_path: str = ""
        self.config_path: str = ""
        self.max_width: int = _PREPROCESS_MAX_WIDTH
        self.crop_top: float = _PREPROCESS_CROP_TOP
        self.crop_bottom: float = _PREPROCESS_CROP_BOTTOM
        self.ocr_backend: str = _OCR_BACKEND
        self.ocr_lang: str = _OCR_LANG
        self.ocr_text_score: float = _OCR_TEXT_SCORE
        self.ocr_mode: str = _OCR_MODE
        self.image_size: int = _IMAGE_SIZE
        self.text_likely_labels: frozenset[str] = _TEXT_LIKELY_LABELS


# Module-level singleton — loaded once at service startup.
_config: PerceptionConfig | None = None


def load(config_path: str | None = None) -> PerceptionConfig:
    """Load configuration. Idempotent — returns cached config after first call."""
    global _config
    if _config is not None:
        return _config

    cfg = PerceptionConfig()

    # Resolve config path
    if config_path:
        cfg.config_path = str(Path(config_path).resolve())
    else:
        cfg.config_path = str(_resolve_path(_CONFIG_PATH_ENV, _DEFAULT_CONFIG_PATH))

    # Resolve model path
    cfg.model_path = str(_resolve_path(_MODEL_PATH_ENV, _DEFAULT_MODEL_PATH))

    # Load label-mapping.json
    path = Path(cfg.config_path)
    import json
    content = path.read_bytes()
    cfg.config_hash = hashlib.sha256(content).hexdigest()
    data = json.loads(content.decode("utf-8"))

    # Populate from label-mapping.json
    cfg.spatial = data.get("spatial", {})
    cfg.detection_confidence = data.get("detection", {}).get("confidence", cfg.detection_confidence)

    # Preprocessing params: env > config > default
    preproc = cfg.spatial.get("preprocessing", {})
    cfg.max_width = int(os.environ.get("UNICLAW_IMAGE_MAX_WIDTH",
        preproc.get("maxWidth", _PREPROCESS_MAX_WIDTH)))
    cfg.crop_top = float(os.environ.get("UNICLAW_IMAGE_CROP_TOP",
        preproc.get("cropTopRatio", _PREPROCESS_CROP_TOP)))
    cfg.crop_bottom = float(os.environ.get("UNICLAW_IMAGE_CROP_BOTTOM",
        preproc.get("cropBottomRatio", _PREPROCESS_CROP_BOTTOM)))

    # Configure ROI padding in backends
    from .ocr.common import configure_roi_padding
    configure_roi_padding(cfg.spatial.get("roiPadding", {}))

    _config = cfg
    return cfg


def get_config() -> PerceptionConfig:
    """Return the cached config. Raises if load() hasn't been called."""
    if _config is None:
        raise RuntimeError("PerceptionConfig not loaded — call load() first.")
    return _config
