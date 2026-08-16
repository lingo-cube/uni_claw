# server.py — FastAPI orchestration for UniClaw Perception Platform
# ⚠️ OMP_NUM_THREADS must be set BEFORE any numpy/ultralytics import (D-18).
#    In the refactored package, these are lazy-imported inside yolo/ocr modules,
#    but we set the env var here at import time for safety.
import os

os.environ["OMP_NUM_THREADS"] = os.environ.get(
    "UNICLAW_OMP_THREADS", os.environ.get("OMP_NUM_THREADS", "4"))

import gc
import json
import logging
import time
import traceback
from contextlib import asynccontextmanager
from io import BytesIO
from pathlib import Path
from typing import Any

from fastapi import FastAPI, HTTPException, Request, Response
from fastapi.responses import JSONResponse
from PIL import Image

from . import __version__
from .config import PerceptionConfig, load as load_config
from .preprocessing import preprocess
from .remap import enforce_geometry, enforce_stage_views, remap_coords
from .health import router as health_router, set_warm
from .yolo.inference import run_yolo_on_image, warmup_yolo
from .ocr.rapid import (
    run_rapid_ocr_on_image,
    run_rapid_ocr_on_crops,
    warmup_rapid_ocr,
    _rapid_ocr_one_crop as rapid_ocr_one_crop,
)
from .ocr.paddle import run_ocr_on_crops, warmup_ocr
from .ocr.common import crop_padded, _roi_padding_px
from .fusion.engine import (
    DEFAULT_INTERACTIVE_LABELS,
    fuse_evidence,
    fuse_evidence_from_crops,
)
from .fusion.heuristics import merge_adjacent_boxes

# ── Config ──────────────────────────────────────────────────────
_config: PerceptionConfig | None = None
_logger = logging.getLogger("uniclaw.perception")


def _get_config() -> PerceptionConfig:
    if _config is None:
        raise RuntimeError("Config not loaded — lifespan must call load_config() first.")
    return _config


# ── FastAPI app ─────────────────────────────────────────────────

@asynccontextmanager
async def lifespan(app: FastAPI):
    global _config
    _config = load_config()
    warmup_yolo()
    cfg = _get_config()
    if cfg.ocr_backend == "rapidocr":
        warmup_rapid_ocr()
    else:
        warmup_ocr(language=cfg.ocr_lang)
    # ── Identity snapshot (G9/G10/G11): capture the identity of what was
    # actually LOADED once, after warmup. /version reports this snapshot —
    # post-start disk mutation can never leak into the reported identity.
    try:
        from governance.runtime_snapshot import capture_snapshot, set_snapshot
        from .health import _model_name
        set_snapshot(capture_snapshot(
            model_path=cfg.model_path, model_name=_model_name(),
            config=cfg, config_hash=cfg.config_hash,
            label_mapping_path=cfg.config_path))
    except Exception:
        # snapshot is additive — startup must not fail because of it
        pass
    set_warm(True)
    yield


app = FastAPI(lifespan=lifespan)
app.include_router(health_router)


@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception):
    """Unhandled exception → 500 with root cause summary in body.
    Full traceback logged to stderr.
    """
    if isinstance(exc, HTTPException):
        return JSONResponse(status_code=exc.status_code, content={"detail": exc.detail})
    _logger.error("unhandled exception in %s:\n%s", request.url.path, traceback.format_exc())
    return JSONResponse(
        status_code=500,
        content={"detail": f"{type(exc).__name__}: {exc}"})


# ── Pipeline ────────────────────────────────────────────────────

def _run_pipeline(
    image: Image.Image,
    orig_w: int,
    orig_h: int,
    *,
    capture_stage_views: bool = False,
) -> tuple[dict[str, Any], tuple[float, float, float, float]]:
    """Shared YOLO → OCR → fusion pipeline. Both analyze endpoints call this.

    Preprocessing (crop + resize) applied once at entry so YOLO and OCR
    share the same pixel space. Coordinates remapped back to original
    full-screen space before returning.

    capture_stage_views (default False, additive, behavior-preserving):
      when True, a third return element is added containing stage-scoped
      views for evaluation: raw model detections (DEKI_YOLO_RAW label
      space) and normalized detections (CANONICAL_DETECTION label space).
      The evidence schema is UNCHANGED — this only adds an optional
      return channel used by the evaluation L2 runner.
    """
    cfg = _get_config()
    t0 = time.perf_counter()

    # ── Preprocessing ──
    proc_img, scale, top_px, _ = preprocess(
        image,
        max_width=cfg.max_width,
        crop_top_ratio=cfg.crop_top,
        crop_bottom_ratio=cfg.crop_bottom,
    )
    proc_w, proc_h = proc_img.size

    # Step 1: YOLO
    detections = run_yolo_on_image(proc_img)
    t1 = time.perf_counter()

    # Step 2: OCR
    if cfg.ocr_backend == "rapidocr":
        if cfg.ocr_mode == "roi":
            # ROI-OCR: filter text labels → merge adjacent → per-crop OCR
            text_dets = [d for d in detections if d.label in cfg.text_likely_labels]
            merged = merge_adjacent_boxes(text_dets)
            padding = _roi_padding_px(100, 20)
            ocr_tokens = []
            for m in merged:
                crop = crop_padded(proc_img, m.box, padding)
                if crop is not None:
                    ocr_tokens.extend(rapid_ocr_one_crop(crop, m, cfg.ocr_text_score))
        else:
            # Full-image OCR (default)
            ocr_tokens = run_rapid_ocr_on_image(proc_img, text_score=cfg.ocr_text_score)
    else:
        # paddleocr fallback
        _NON_TEXT_LABELS = frozenset({"imageview", "line"})
        ocr_detections = [d for d in detections if d.label not in _NON_TEXT_LABELS]
        ocr_crops = run_ocr_on_crops(proc_img, ocr_detections, language=cfg.ocr_lang)
        ocr_idx = 0
        aligned_ocr = []
        for d in detections:
            if d.label in _NON_TEXT_LABELS:
                aligned_ocr.append([])
            else:
                aligned_ocr.append(ocr_crops[ocr_idx])
                ocr_idx += 1
    t2 = time.perf_counter()

    # Step 3: Fusion (in preprocessed pixel space)
    if cfg.ocr_backend == "rapidocr":
        evidence = fuse_evidence(
            detections, ocr_tokens,
            image=proc_img,
            image_width=proc_w, image_height=proc_h,
            interactive_labels=DEFAULT_INTERACTIVE_LABELS | {"text_block", "text"},
            promote_unmatched_ocr=True)
    else:
        evidence = fuse_evidence_from_crops(
            detections, aligned_ocr,
            image_width=proc_w, image_height=proc_h)
    t3 = time.perf_counter()

    # ── Remap coords back to original full-screen space ──
    remap_coords(evidence, scale, top_px, orig_w, orig_h)

    # ── GAP-002 complete response-boundary geometry enforcement ──
    # candidates / yolo / ocr are canonical production evidence: normalized
    # post-remap contract with original-frame pixel limits. Every serialized
    # collection is validated here — no alternate path skips this.
    enforce_geometry(evidence, orig_limits=(orig_w, orig_h))

    evidence["metadata"] = _metadata(orig_w, orig_h)
    evidence["scrollHints"] = _scroll_hints(evidence["candidates"])

    if capture_stage_views:
        # Stage-scoped views for evaluation (evidence schema unchanged).
        def _det_view(d: Any, with_raw: bool) -> dict[str, Any]:
            v = d.to_json(proc_w, proc_h)
            if with_raw:
                v["rawLabel"] = d.raw_label
            return v

        views = {
            "rawModelDetections": [_det_view(d, True) for d in detections],
            "normalizedDetections": [_det_view(d, False) for d in detections],
            "fusedEvidence": list(evidence.get("candidates", [])),
        }
        # stage views carry their OWNED coordinate contracts (pixel space
        # for raw/normalized detections, normalized for fused)
        enforce_stage_views(views, evidence,
                            proc_limits=(proc_w, proc_h),
                            orig_limits=(orig_w, orig_h))
        return evidence, (t0, t1, t2, t3), views
    return evidence, (t0, t1, t2, t3)


# ── Endpoints ───────────────────────────────────────────────────

@app.post("/v1/analyze")
async def analyze(request: Request):
    try:
        image_bytes = await request.body()
        image = Image.open(BytesIO(image_bytes))
        width, height = image.size

        evidence, (t0, t1, t2, t3) = _run_pipeline(image, width, height)
        t4 = time.perf_counter()

        headers = {
            "Server-Timing": _server_timing(
                yolo_ms=(t1 - t0) * 1000,
                ocr_ms=(t2 - t1) * 1000,
                fusion_ms=(t3 - t2) * 1000,
                scroll_ms=(t4 - t3) * 1000,
            ),
        }
        return Response(content=json.dumps(evidence, ensure_ascii=False),
                        media_type="application/json",
                        headers=headers)
    finally:
        gc.collect()


@app.post("/v1/analyze_raw")
async def analyze_raw(request: Request):
    try:
        width = int(request.headers["X-Image-Width"])
        height = int(request.headers["X-Image-Height"])
        pixel_format = int(request.headers.get("X-Image-Pixel-Format", "1"))
        if pixel_format != 1:
            raise HTTPException(400, f"Unsupported pixel format: {pixel_format}")

        body = await request.body()
        expected_len = width * height * 4
        if len(body) != expected_len:
            raise HTTPException(400,
                f"Body size mismatch: got {len(body)}, expected {expected_len} "
                f"({width}×{height}×4)")

        # PIL frombytes is pure memory wrap (0ms decode)
        image = Image.frombytes("RGBA", (width, height), body).convert("RGB")

        evidence, (t0, t1, t2, t3) = _run_pipeline(image, width, height)
        t4 = time.perf_counter()

        headers = {
            "Server-Timing": _server_timing(
                yolo_ms=(t1 - t0) * 1000,
                ocr_ms=(t2 - t1) * 1000,
                fusion_ms=(t3 - t2) * 1000,
                scroll_ms=(t4 - t3) * 1000,
            ),
        }
        return Response(content=json.dumps(evidence, ensure_ascii=False),
                        media_type="application/json",
                        headers=headers)
    finally:
        gc.collect()


# ── Evidence helpers ────────────────────────────────────────────

def _scroll_hints(candidates: list[dict[str, Any]]) -> dict[str, Any]:
    """Raw scroll observables. Decision is made on the C# side."""
    cfg = _get_config()
    threshold = cfg.spatial.get("edgeThreshold", 0.92)
    return {
        "totalCandidates": len(candidates),
        "candidatesNearBottom": sum(
            1 for c in candidates
            if (c.get("center") or {}).get("y", 0.0) > threshold),
        "scrollbarDetected": any(c.get("type") == "scrollbar" for c in candidates),
    }


def _metadata(width: int, height: int) -> dict[str, Any]:
    """Schema version + pipeline info + models + configHash."""
    cfg = _get_config()
    from .health import _model_id
    return {
        "schema": "uniclaw.localVisionEvidence.v1",
        "width": width,
        "height": height,
        "pipeline": {"name": "local-vision", "version": __version__},
        "models": {"yolo": cfg.model_path, "ocr": cfg.ocr_backend},
        "configHash": cfg.config_hash,
        # Phase 3 bridge to Phase 4 provenance (backward-compatible addition):
        "modelId": _model_id(),
    }


def _server_timing(yolo_ms: float, ocr_ms: float, fusion_ms: float, scroll_ms: float) -> str:
    return f"yolo;dur={yolo_ms:.1f}, ocr;dur={ocr_ms:.1f}, " \
           f"fusion;dur={fusion_ms:.1f}, scroll;dur={scroll_ms:.1f}"
