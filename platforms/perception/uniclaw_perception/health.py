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


# ── Endpoints ───────────────────────────────────────────────────

@router.get("/health")
async def health():
    return {"status": "ok", "warm": _WARM}


@router.get("/version")
async def version():
    """Return supported schema versions for Provider Host negotiation.

    P4-D6 + G9/G10/G11: reports the STARTUP IDENTITY SNAPSHOT — the
    identity of what was actually loaded into this process, captured once
    after warmup. Post-start disk mutation cannot alter the reported
    identity (RSI-01..03). Never echoes expected input (EXI-04).
    """
    cfg = get_config()
    response = {
        "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
        "serviceVersion": "1.0",
        "modelId": _model_id(),
        "modelName": _model_name(),
        "configHash": cfg.config_hash,   # legacy compatibility identity
    }
    try:
        from governance.runtime_snapshot import get_snapshot
        snap = get_snapshot()
        if snap is not None:
            # canonical path: report the frozen startup snapshot
            response["modelId"] = snap.model_id
            response["configId"] = snap.config_id
            response["configCompleteness"] = snap.config_completeness
            response["pipelineRevision"] = snap.pipeline_revision
            response["deploymentId"] = snap.deployment_id
        else:
            # no snapshot (dev tooling without lifespan): compute live —
            # this is NOT the canonical production path
            from governance.config_manifest import build_from_perception_config
            from governance.pipeline_revision import compute_pipeline_revision
            from governance.deployment import PerceptionDeploymentCandidate
            manifest = build_from_perception_config(
                cfg, cfg.config_path,
                label_mapping_content_hash=cfg.config_hash)
            rev = compute_pipeline_revision()
            candidate = PerceptionDeploymentCandidate(
                schema_version="uniclaw.localVisionEvidence.v1",
                model_id=response["modelId"],
                config_id=manifest.config_id,
                pipeline_revision=rev["pipelineRevision"],
                service_version=response["serviceVersion"],
            )
            response["configId"] = manifest.config_id
            response["configCompleteness"] = manifest.completeness.value
            response["pipelineRevision"] = rev["pipelineRevision"]
            response["deploymentId"] = candidate.deployment_id
    except Exception:
        # governance computation is additive — version endpoint must never
        # fail hard because of it; facts stay partially populated.
        pass
    return response
