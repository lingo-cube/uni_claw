"""Health + version endpoints for the UniClaw Perception Platform.

Owns: GET /health, GET /version, model identity computation.

Extracted from server.py. PER-007 R2: startup identity snapshot moved
in-package (frozen once after warmup — G9/G10/G11 semantics preserved);
legacy governance.runtime_snapshot / config_manifest / pipeline_revision /
deployment machinery dropped with the migration (their additive /version
fields — configId / configCompleteness / pipelineRevision / deploymentId —
had no consumer in the Target tree; provenance carries metadata.configHash).
"""
from __future__ import annotations

import hashlib
from pathlib import Path

from fastapi import APIRouter

from .config import get_config

router = APIRouter()


# ── Warm flag (module-level, set by lifespan) ───────────────────
_WARM = False


def set_warm(value: bool = True) -> None:
    global _WARM
    _WARM = value


def is_warm() -> bool:
    return _WARM


# ── Startup identity snapshot (PER-007 R2, in-package) ──────────
#: Frozen once by lifespan after warmup (capture_identity); /version and
#: response metadata report this snapshot — post-start disk mutation cannot
#: leak into the reported identity of what was actually loaded.
_IDENTITY: dict[str, str] | None = None


def capture_identity() -> None:
    """Freeze loaded identity once at startup (lifespan, after warmup)."""
    global _IDENTITY
    _IDENTITY = {
        "model_id": _model_id_live(),
        "model_name": _model_name_live(),
        "config_hash": get_config().config_hash,
    }


# ── Model identity ──────────────────────────────────────────────

def _model_id_live() -> str:
    """Stable model identity: full SHA-256 of model artifact content.
    Content-addressed, path-independent, filename-independent.
    Frozen Phase 2 contract: exactly 64 lowercase hex characters."""
    cfg = get_config()
    path = Path(cfg.model_path)
    if not path.exists():
        return ""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _model_name_live() -> str:
    """Stable human-readable model family identity.
    Derived from directory name (e.g. android_ui_detection_yolov8),
    NOT from checkpoint filename (e.g. best.pt).
    Separate from canonical modelId (full SHA-256)."""
    cfg = get_config()
    path = Path(cfg.model_path)
    if not path.exists():
        return "unknown"
    # Model family = parent directory name (stable), not file stem (checkpoint role)
    return path.parent.name


def _model_id() -> str:
    """Frozen startup identity when captured; live compute otherwise
    (dev tooling without lifespan)."""
    if _IDENTITY is not None:
        return _IDENTITY["model_id"]
    return _model_id_live()


def _model_name() -> str:
    if _IDENTITY is not None:
        return _IDENTITY["model_name"]
    return _model_name_live()


# ── Endpoints ───────────────────────────────────────────────────

@router.get("/health")
async def health():
    return {"status": "ok", "warm": _WARM}


@router.get("/version")
async def version():
    """Return supported schema versions for Provider Host negotiation.

    Reports the frozen startup identity snapshot (what was actually
    loaded into this process). Never echoes expected input (EXI-04).
    """
    cfg = get_config()
    return {
        "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
        "serviceVersion": "1.0",
        "modelId": _model_id(),
        "modelName": _model_name(),
        "configHash": cfg.config_hash,   # legacy compatibility identity
    }
