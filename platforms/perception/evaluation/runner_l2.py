"""L2_RECORDED_IMAGE_INFERENCE runner — fresh current-model inference.

Frozen by Phase 4 gate (B5 / PF3):
  • The runner executes the CURRENT production pipeline fresh:
    Decode → Preprocess → YOLO → OCR → Fusion → CoordinateRemap → Serialize.
  • Stored historical perception JSON is NEVER used as a current prediction.
  • Replay/simulation records are NOT L2 evidence (PF3/PF4).
  • Inference dependency failures raise EvaluationInfrastructureError — a run
    then terminates INSUFFICIENT_EVIDENCE / INFRASTRUCTURE_FAILURE, never PASS.
"""
from __future__ import annotations

import time
import io
from pathlib import Path
from typing import Any

from .deployment import DeploymentSnapshot
from .prediction import Prediction
from .identity import content_id
from .asset import ASSET_CONTENT_IDENTITY_MISMATCH


class EvaluationInfrastructureError(Exception):
    """Infrastructure failure (B12) — distinct from model quality failure."""


# Lazily bound pipeline accessors (patchable in tests without importing
# the heavy uniclaw_perception package).
_load_config_fn = None
_pipeline_fn = None


def _load_config():
    global _load_config_fn
    if _load_config_fn is None:
        from uniclaw_perception.config import load as _fn
        _load_config_fn = _fn
    cfg = _load_config_fn()
    # The production server wires its pipeline config in the FastAPI lifespan.
    # Evaluation runs the pipeline in-process without a server, so it wires
    # the identical config snapshot the same way. No production change.
    try:
        import uniclaw_perception.server as _server
        _server._config = cfg
    except ImportError:
        pass
    return cfg


def _load_pipeline():
    global _pipeline_fn
    if _pipeline_fn is None:
        from uniclaw_perception.server import _run_pipeline as _fn
        _pipeline_fn = _fn
    return _pipeline_fn


def run_fresh_inference(
    asset_path: str | Path,
    run_id: str,
    asset_id: str,
    deployment: DeploymentSnapshot,
    *,
    model_path_override: str | None = None,
) -> Prediction:
    """Execute the fresh production perception pipeline on a stored screenshot.

    Uses uniclaw_perception's production pipeline directly — the exact same
    code path the vision service executes per request.

    model_path_override: evaluation-side override for CANDIDATE model
    evaluation (sets UNICLAW_YOLO_MODEL + resets the config cache before
    load). Production behavior unchanged.

    P4-D8 execution truth guard: when the deployment snapshot claims a
    canonical identity, the EXECUTED model bytes / config / pipeline
    revision must match the claim. Mismatch raises
    EvaluationInfrastructureError with
    EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH — never persisted as a valid
    quality EvaluationRun.
    """
    path = Path(asset_path)
    if not path.exists():
        raise EvaluationInfrastructureError(f"asset bytes missing: {path}")
    # Read bytes exactly once; bind execution to the claimed content identity.
    try:
        asset_bytes = path.read_bytes()
    except OSError as exc:
        raise EvaluationInfrastructureError(f"asset bytes unreadable: {path}: {exc}") from exc
    actual_asset_id = content_id(asset_bytes)
    if actual_asset_id != asset_id:
        raise EvaluationInfrastructureError(
            f"{ASSET_CONTENT_IDENTITY_MISMATCH}: bytes {actual_asset_id} "
            f"!= claimed {asset_id}")

    from PIL import Image

    if model_path_override is not None:
        import os as _os
        _os.environ["UNICLAW_YOLO_MODEL"] = model_path_override
        import uniclaw_perception.config as _cfg_mod
        _cfg_mod._config = None   # reset cache so override takes effect

    try:
        _load_config()
        run_pipeline = _load_pipeline()
    except ImportError as exc:
        raise EvaluationInfrastructureError(
            f"perception pipeline unavailable: {exc}") from exc

    # ── EXI-01: executed model bytes must match claimed ModelId ──
    if model_path_override is not None and Path(model_path_override).exists():
        from evaluation.identity import sha256_file
        actual_model_id = sha256_file(model_path_override)
        if actual_model_id != deployment.model_id:
            raise EvaluationInfrastructureError(
                "EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH: "
                f"executed model bytes {actual_model_id[:16]}… != claimed "
                f"modelId {deployment.model_id[:16]}…")

    # ── EXI-02: effective config must match claimed canonical ConfigId ──
    if deployment.is_canonical:
        import uniclaw_perception.config as _cfg_mod
        cfg = _cfg_mod.get_config()
        try:
            from governance.config_manifest import build_from_perception_config
            manifest = build_from_perception_config(cfg, cfg.config_path)
            if manifest.config_id != deployment.config_id:
                raise EvaluationInfrastructureError(
                    "EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH: "
                    f"effective configId {manifest.config_id} != claimed "
                    f"{deployment.config_id}")
            # ── EXI-03: pipeline revision must match claim ──
            from governance.pipeline_revision import compute_pipeline_revision
            rev = compute_pipeline_revision()
            if rev["pipelineRevision"] != deployment.pipeline_revision:
                raise EvaluationInfrastructureError(
                    "EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH: "
                    f"actual pipelineRevision {rev['pipelineRevision']} != "
                    f"claimed {deployment.pipeline_revision}")
        except EvaluationInfrastructureError:
            raise
        except Exception as exc:
            raise EvaluationInfrastructureError(
                "EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH: "
                f"identity verification failed: {exc}") from exc

    try:
        with Image.open(io.BytesIO(asset_bytes)) as image:
            image.load()  # eager decode — pipeline crops after the file closes
            width, height = image.size
        t0 = time.perf_counter()
        result = run_pipeline(image, width, height, capture_stage_views=True)
        if len(result) == 3:
            evidence, timings, stage_views = result
        else:
            # production pipeline without the additive capture channel
            evidence, timings = result
            stage_views = {}
        elapsed_ms = (time.perf_counter() - t0) * 1000
    except Exception as exc:  # noqa: BLE001 — any inference failure is infra
        raise EvaluationInfrastructureError(
            f"fresh inference failed for {path}: {type(exc).__name__}: {exc}") from exc

    candidates = tuple(evidence.get("candidates", []))
    timings_ms = {
        "totalMs": round(elapsed_ms, 2),
        "yoloMs": round((timings[1] - timings[0]) * 1000, 2),
        "ocrMs": round((timings[2] - timings[1]) * 1000, 2),
        "fusionMs": round((timings[3] - timings[2]) * 1000, 2),
    }
    return Prediction(
        run_id=run_id,
        asset_id=asset_id,
        deployment_hash=deployment.identity_hash,
        schema_version=evidence.get("metadata", {}).get(
            "schema", "unknown"),
        candidates=candidates,
        yolo_count=len(evidence.get("yolo", [])),
        ocr_count=len(evidence.get("ocr", [])),
        timings_ms=timings_ms,
        note="fresh L2 inference via uniclaw_perception production pipeline",
        stage_views=dict(stage_views),
        source_content_hash=actual_asset_id,
    )
