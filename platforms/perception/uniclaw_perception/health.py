"""Health + version endpoints for the UniClaw Perception Platform.

Owns: GET /health, GET /version, model identity computation.

Extracted from server.py. Preserves exact behavior.
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


# ── Model identity ──────────────────────────────────────────────

def _model_id() -> str:
    """Stable model identity: full SHA-256 of model artifact content.
    Content-addressed, path-independent, filename-independent.
    Frozen Phase 2 contract: exactly 64 lowercase hex characters."""
    cfg = get_config()
    path = Path(cfg.model_path)
    if not path.exists():
        return ""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _model_name() -> str:
    """Human-readable model label. Separate from canonical modelId."""
    cfg = get_config()
    path = Path(cfg.model_path)
    if not path.exists():
        return "unknown"
    return path.stem


# ── Endpoints ───────────────────────────────────────────────────

@router.get("/health")
async def health():
    return {"status": "ok", "warm": _WARM}


@router.get("/version")
async def version():
    """Return supported schema versions for Provider Host negotiation."""
    cfg = get_config()
    return {
        "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
        "serviceVersion": "1.0",
        "modelId": _model_id(),
        "modelName": _model_name(),
        "configHash": cfg.config_hash,
    }
