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


# ── Rule-set content axis (S1C governance binding) ──────────────
#: Stable marker bound into the config identity when the loaded config
#: carries NO rule set (the default root rule set semantics).  Including the
#: marker (rather than nothing) gives the default a *fixed* identity and
#: makes "absent" distinguishable from any serialized rule set — a serialized
#: rule-set document can never equal this marker (it is not valid rule-set
#: JSON), so hash collisions between absent and present are impossible.
DEFAULT_RULESET_MARKER = "perception.ruleset.default-root"


class RuleSetResolutionError(RuntimeError):
    """Active rule-set resolution failed; startup MUST abort (fail-closed).

    Raised by :func:`resolve_active_rule_set` when the loaded config carries
    a rule set that is structurally invalid or lints with diagnostics.  An
    unloadable rule set never enters runtime; the message names the
    diagnostic(s).
    """


def compute_config_hash(content: bytes, ruleset_content: str | None) -> str:
    """Deterministic config identity hash over (label-mapping bytes, ruleset).

    Same inputs ⇒ same hash; ANY ruleset byte change — or absence vs presence
    — ⇒ a different hash.  Absence binds the stable
    :data:`DEFAULT_RULESET_MARKER` so the default-root semantics carry a fixed
    identity and never collide with a serialized rule set (JSON cannot contain
    the NUL separator byte used below).
    """
    body = content
    if ruleset_content is not None:
        body += b"\x00ruleset:" + ruleset_content.encode("utf-8")
    else:
        body += b"\x00ruleset:" + DEFAULT_RULESET_MARKER.encode("utf-8")
    return hashlib.sha256(body).hexdigest()


def resolve_active_rule_set(cfg: PerceptionConfig) -> Any:
    """Resolve the ACTIVE rule set for a loaded config (S1C).

    Returns an ``operators.ruleset.RuleSetLoad``:
      * config carries no ruleset content → ``registry_defaults.DEFAULT_RULE_SET``
        (zero behavior difference — the S1 root defaults),
      * config carries a serialized rule set → strict deserialize + lint via
        ``operators.ruleset.load_rule_set``; a structural error (``ValueError``)
        or ANY lint diagnostic raises :class:`RuleSetResolutionError` naming
        the problem (fail-closed: an unloadable rule set never enters runtime).

    Only a rule set present in the loaded config's identity can resolve here;
    unpromoted candidate rule sets anywhere else never reach runtime
    resolution (spec: "Unpromoted candidate rules never run").
    """
    if cfg.ruleset_content is None:
        from .operators.registry_defaults import DEFAULT_RULE_SET
        from .operators.ruleset import RuleSetLoad
        return RuleSetLoad(rules=tuple(DEFAULT_RULE_SET), diagnostics=())
    from .operators import REGISTRY, load_rule_set
    try:
        loaded = load_rule_set(cfg.ruleset_content, REGISTRY)
    except ValueError as error:
        raise RuleSetResolutionError(
            f"active rule set failed to load: {error}"
        ) from None
    if not loaded.is_valid:
        diagnostics = "; ".join(
            f"[{diagnostic.kind}] {diagnostic.message}"
            for diagnostic in loaded.diagnostics
        )
        raise RuleSetResolutionError(
            f"active rule set rejected at load: {diagnostics}"
        )
    return loaded


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
        #: Serialized ACTIVE rule-set content (operators.ruleset text) or
        #: ``None`` = default root rule set semantics (zero behavior
        #: difference).  Populated by ``load()`` from the optional top-level
        #: ``"ruleset"`` field of the config JSON.
        self.ruleset_content: str | None = None


# Module-level singleton — loaded once at service startup.
_config: PerceptionConfig | None = None
#: Active rule set resolved at load time (fail-closed at startup); mirrors the
#: ``_config`` singleton pattern for the operator runtime (S1C).
_active_rule_set: Any | None = None


def load(config_path: str | None = None) -> PerceptionConfig:
    """Load configuration. Idempotent — returns cached config after first call."""
    global _config, _active_rule_set
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
    data = json.loads(content.decode("utf-8"))

    # Mandatory: configHash must incorporate the ACTIVE rule-set axis — same
    # config ⇒ same hash; ANY ruleset byte change ⇒ different hash (S1C).
    raw_ruleset = data.get("ruleset")
    if raw_ruleset is not None:
        if not isinstance(raw_ruleset, str) or not raw_ruleset.strip():
            raise ValueError(
                "config 'ruleset' must be the serialized rule-set JSON text "
                f"(got {type(raw_ruleset).__name__})"
            )
        cfg.ruleset_content = raw_ruleset
    cfg.config_hash = compute_config_hash(content, cfg.ruleset_content)

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

    # Resolve the ACTIVE rule set once at load: an unloadable rule set aborts
    # startup (fail-closed, naming the diagnostic) — it never enters runtime.
    _active_rule_set = resolve_active_rule_set(cfg)

    _config = cfg
    return cfg


def get_config() -> PerceptionConfig:
    """Return the cached config. Raises if load() hasn't been called."""
    if _config is None:
        raise RuntimeError("PerceptionConfig not loaded — call load() first.")
    return _config


def get_active_rule_set() -> Any:
    """Return the ACTIVE rule set resolved at load time (S1C).

    Raises if load() hasn't been called.  The result is an
    ``operators.ruleset.RuleSetLoad``: the config-carried rule set when
    present, else ``registry_defaults.DEFAULT_RULE_SET`` (default root
    semantics, zero behavior difference).
    """
    if _active_rule_set is None:
        raise RuntimeError("Active rule set not resolved — call load() first.")
    return _active_rule_set
