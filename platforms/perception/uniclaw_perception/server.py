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
from .pipeline import (
    PipelineConfig,
    PipelineValidationError,
    load_default,
    load_variants,
    lint_against_config,
    identity_content as pipeline_identity_content,
)
from . import identity
from .preprocessing import preprocess
from .remap import enforce_geometry, enforce_stage_views, remap_coords
from .health import router as health_router, set_warm
from .yolo.inference import run_yolo_on_image, warmup_yolo
from .ocr.rapid import (
    configure_ocr_models,
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

# ── Pipeline registry（PER-008 D1：default + 预声明变体；启动全量 lint）──
#: key = "default" 或 variantId；value = (PipelineConfig, identity dict)。
#: 请求经 X-Pipeline-Variant 选择（只可选预声明变体，不携带配置内容）。
_pipelines: dict[str, tuple[PipelineConfig, dict[str, str]]] = {}

DEFAULT_PIPELINE_KEY = "default"


def _pipeline_for(variant_header: str | None) -> tuple[str, PipelineConfig]:
    if not variant_header:
        return DEFAULT_PIPELINE_KEY, _pipelines[DEFAULT_PIPELINE_KEY][0]
    if variant_header not in _pipelines:
        raise HTTPException(
            400, f"unknown pipeline variant: {variant_header!r} "
                 f"(declared: {sorted(k for k in _pipelines if k != DEFAULT_PIPELINE_KEY)})")
    return variant_header, _pipelines[variant_header][0]


def _get_config() -> PerceptionConfig:
    if _config is None:
        raise RuntimeError("Config not loaded — lifespan must call load_config() first.")
    return _config


# ── FastAPI app ─────────────────────────────────────────────────

@asynccontextmanager
async def lifespan(app: FastAPI):
    global _config, _pipelines
    _config = load_config()
    # PER-008：管道配置 + 变体注册（启动全量 lint；任一失败 → fail-closed 中止）
    default_pipeline = load_default()
    try:
        lint_against_config(default_pipeline, _config)
        _pipelines[DEFAULT_PIPELINE_KEY] = (default_pipeline, {})
        for variant_id, variant in load_variants().items():
            lint_against_config(variant.config, _config)
            _pipelines[variant_id] = (variant.config, {})
    except PipelineValidationError:
        raise
    warmup_yolo()
    cfg = _get_config()
    if cfg.ocr_backend == "rapidocr":
        # P-OCR: resolve the rec model from the declared language BEFORE
        # building the singleton — unregistered/unavailable language fails
        # closed at startup (spec perception/ocr-backend-selection).
        try:
            from .ocr.rapid import _rapid_ocr_kwargs
            _rapid_ocr_kwargs.update(configure_ocr_models(language=cfg.ocr_lang))
        except RuntimeError:
            raise
        warmup_rapid_ocr()
    else:
        warmup_ocr(language=cfg.ocr_lang)
    # ── Identity snapshot (G9/G10/G11, PER-007 R2 in-package): capture the
    # identity of what was actually LOADED once, after warmup. /version and
    # response metadata report this snapshot — post-start disk mutation can
    # never leak into the reported identity.
    from .health import capture_identity, _model_id
    capture_identity()
    # PER-008 D2：四层身份（每管道一份 configId/deploymentId；变体感知）
    revision = identity.compute_pipeline_revision()
    model_id = _model_id()
    for key, (pipeline_config, _) in _pipelines.items():
        variant_id = None if key == DEFAULT_PIPELINE_KEY else key
        config_id = identity.build_config_id(
            _config, pipeline_identity_content(pipeline_config, variant_id))
        _pipelines[key] = (pipeline_config, {
            "configId": config_id,
            "pipelineRevision": revision["pipelineRevision"],
            "deploymentId": identity.compute_deployment_id(
                "uniclaw.localVisionEvidence.v1", model_id, config_id,
                revision["pipelineRevision"]),
        })
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
    pipeline: PipelineConfig | None = None,
    pipeline_key: str = DEFAULT_PIPELINE_KEY,
    capture_stage_views: bool = False,
    stabilize_context: list[dict[str, Any]] | None = None,
    trace_sink: Any | None = None,
) -> tuple[dict[str, Any], tuple[float, float, float, float]]:
    """Shared staged pipeline（PER-008：STAGES DAG 见 pipeline.py；串行实现，
    detect/recognize 的并行化是 OPT-001 的增量）。Both analyze endpoints call
    this. Preprocessing (crop + resize) applied once at entry so YOLO and OCR
    share the same pixel space. Coordinates remapped back to original
    full-screen space before returning. fusion 参数来自管道配置（默认 = 历史
    硬编码值，行为冻结）。
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
    operator_traces: list[dict[str, Any]] = []
    fusion_stages: list[dict[str, Any]] = []
    want_trace = capture_stage_views or trace_sink is not None
    trace_sink_engine = operator_traces.append if want_trace else None
    stage_sink = fusion_stages.append if capture_stage_views else None
    if cfg.ocr_backend == "rapidocr":
        fuse_params = (pipeline.fuse if pipeline is not None
                       else _pipelines[DEFAULT_PIPELINE_KEY][0].fuse)
        evidence = fuse_evidence(
            detections, ocr_tokens,
            image=proc_img,
            image_width=proc_w, image_height=proc_h,
            interactive_labels=DEFAULT_INTERACTIVE_LABELS | set(fuse_params.interactive_extra_labels),
            promote_unmatched_ocr=fuse_params.promote_unmatched_ocr,
            stabilize=fuse_params.stabilize,  # cross-frame row stabilizer (WI-CTX, stateless)
            max_ocr_distance_ratio=fuse_params.max_ocr_distance_ratio,
            stabilize_context=stabilize_context,  # known_rows from X-Known-Rows
            trace_sink=trace_sink_engine,
            stage_sink=stage_sink)
    else:
        stabilize = (pipeline.fuse.stabilize if pipeline is not None
                     else _pipelines[DEFAULT_PIPELINE_KEY][0].fuse.stabilize)
        evidence = fuse_evidence_from_crops(
            detections, aligned_ocr,
            image_width=proc_w, image_height=proc_h,
            stabilize=stabilize,  # cross-frame row stabilizer (WI-CTX, stateless)
            stabilize_context=stabilize_context,  # known_rows from X-Known-Rows
            trace_sink=trace_sink_engine,
            stage_sink=stage_sink)
    # Compact fusion causal trace (gate-approved trace coverage): the caller
    # receives the STRIPPED trace (refs + decisions only; no heavy stage
    # views) — TRACE != CONTROL, TRACE != EVIDENCE AUTHORITY.  Stage data
    # stays in the stage/artifact channels.
    if trace_sink is not None and operator_traces:
        from .fusion.causal_trace import strip_stage_views
        trace_sink(strip_stage_views(operator_traces[0]))
    t3 = time.perf_counter()

    # ── Remap coords back to original full-screen space ──
    remap_coords(evidence, scale, top_px, orig_w, orig_h)

    # ── GAP-002 complete response-boundary geometry enforcement ──
    # candidates / yolo / ocr are canonical production evidence: normalized
    # post-remap contract with original-frame pixel limits. Every serialized
    # collection is validated here — no alternate path skips this.
    enforce_geometry(evidence, orig_limits=(orig_w, orig_h))

    evidence["metadata"] = _metadata(orig_w, orig_h, pipeline_key)
    evidence["scrollHints"] = _scroll_hints(evidence["candidates"])

    if capture_stage_views:
        # Stage-scoped views for evaluation (evidence schema unchanged).
        def _det_view(d: Any, with_raw: bool) -> dict[str, Any]:
            v = d.to_json(proc_w, proc_h)
            if with_raw:
                v["rawLabel"] = d.raw_label
                v["rawClassId"] = d.raw_class_id
            return v

        views = {
            "rawModelDetections": [_det_view(d, True) for d in detections],
            "normalizedDetections": [_det_view(d, False) for d in detections],
            "operatorTrace": operator_traces[0] if operator_traces else None,
            "fusionStages": fusion_stages,
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

def _capture_stage_views_requested(request: Request) -> bool:
    """Validation-only opt-in; absent/false preserves the production response."""
    return request.headers.get("x-capture-stage-views", "").strip().lower() in {
        "1", "true", "yes"
    }


def _compact_trace_requested(request: Request) -> bool:
    """Fusion causal trace opt-in (gate-approved trace coverage): when set, the
    response carries the compact stripped trace under ``trace``.  Absent/false
    preserves the production response byte-for-byte."""
    return request.headers.get("x-perception-trace", "").strip().lower() in {
        "1", "true", "yes"
    }

@app.post("/v1/analyze")
async def analyze(request: Request):
    try:
        image_bytes = await request.body()
        image = Image.open(BytesIO(image_bytes))
        width, height = image.size

        # WI-CTX: known-row context supplied by the C# Runtime via the
        # X-Known-Rows header (D4): ``[{"id":"row_001","text":"..."}, ...]``.
        # Absent/empty header -> no context -> every candidate is a new row.
        known_rows_header = request.headers.get("x-known-rows")
        stabilize_context = (
            json.loads(known_rows_header) if known_rows_header else None
        )

        capture_stage_views = _capture_stage_views_requested(request)
        compact_traces: list[dict[str, Any]] = []
        want_compact_trace = _compact_trace_requested(request)
        pipeline_key, pipeline_config = _pipeline_for(
            request.headers.get("x-pipeline-variant"))
        pipeline = _run_pipeline(
            image, width, height,
            pipeline=pipeline_config,
            pipeline_key=pipeline_key,
            capture_stage_views=capture_stage_views,
            stabilize_context=stabilize_context,
            trace_sink=compact_traces.append if want_compact_trace else None)
        if capture_stage_views:
            evidence, (t0, t1, t2, t3), stage_views = pipeline
            response_body = dict(evidence, stageViews=stage_views)
        else:
            evidence, (t0, t1, t2, t3) = pipeline
            response_body = evidence
        if want_compact_trace:
            response_body = dict(
                response_body,
                trace=compact_traces[0] if compact_traces else None,
            )
        t4 = time.perf_counter()

        # OPT-001 S1：计时补全——序列化与 GC 各自成段（原先序列化/GC 藏在
        # 分段之外不可见；研究指出不能拿旧计时判瓶颈）。
        serialize_start = time.perf_counter()
        body = json.dumps(response_body, ensure_ascii=False)
        t5 = time.perf_counter()
        gc_start = time.perf_counter()
        gc.collect()
        t6 = time.perf_counter()

        headers = {
            "Server-Timing": _server_timing(
                yolo_ms=(t1 - t0) * 1000,
                ocr_ms=(t2 - t1) * 1000,
                fusion_ms=(t3 - t2) * 1000,
                scroll_ms=(t4 - t3) * 1000,
                serialize_ms=(t5 - serialize_start) * 1000,
                gc_ms=(t6 - gc_start) * 1000,
            ),
        }
        return Response(content=body,
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

        # WI-CTX: known-row context supplied by the C# Runtime via the
        # X-Known-Rows header (D4).  Absent/empty header -> no context.
        known_rows_header = request.headers.get("x-known-rows")
        stabilize_context = (
            json.loads(known_rows_header) if known_rows_header else None
        )

        capture_stage_views = _capture_stage_views_requested(request)
        compact_traces: list[dict[str, Any]] = []
        want_compact_trace = _compact_trace_requested(request)
        pipeline_key, pipeline_config = _pipeline_for(
            request.headers.get("x-pipeline-variant"))
        pipeline = _run_pipeline(
            image, width, height,
            pipeline=pipeline_config,
            pipeline_key=pipeline_key,
            capture_stage_views=capture_stage_views,
            stabilize_context=stabilize_context,
            trace_sink=compact_traces.append if want_compact_trace else None)
        if capture_stage_views:
            evidence, (t0, t1, t2, t3), stage_views = pipeline
            response_body = dict(evidence, stageViews=stage_views)
        else:
            evidence, (t0, t1, t2, t3) = pipeline
            response_body = evidence
        if want_compact_trace:
            response_body = dict(
                response_body,
                trace=compact_traces[0] if compact_traces else None,
            )
        t4 = time.perf_counter()

        # OPT-001 S1：计时补全——序列化与 GC 各自成段（原先序列化/GC 藏在
        # 分段之外不可见；研究指出不能拿旧计时判瓶颈）。
        serialize_start = time.perf_counter()
        body = json.dumps(response_body, ensure_ascii=False)
        t5 = time.perf_counter()
        gc_start = time.perf_counter()
        gc.collect()
        t6 = time.perf_counter()

        headers = {
            "Server-Timing": _server_timing(
                yolo_ms=(t1 - t0) * 1000,
                ocr_ms=(t2 - t1) * 1000,
                fusion_ms=(t3 - t2) * 1000,
                scroll_ms=(t4 - t3) * 1000,
                serialize_ms=(t5 - serialize_start) * 1000,
                gc_ms=(t6 - gc_start) * 1000,
            ),
        }
        return Response(content=body,
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


def _metadata(width: int, height: int,
              pipeline_key: str = DEFAULT_PIPELINE_KEY) -> dict[str, Any]:
    """Schema version + pipeline info + models + configHash + 四层身份（additive）。"""
    cfg = _get_config()
    from .health import _model_id
    meta = {
        "schema": "uniclaw.localVisionEvidence.v1",
        "width": width,
        "height": height,
        "pipeline": {"name": "local-vision", "version": __version__},
        "models": {"yolo": cfg.model_path, "ocr": cfg.ocr_backend},
        "configHash": cfg.config_hash,
        # Phase 3 bridge to Phase 4 provenance (backward-compatible addition):
        "modelId": _model_id(),
    }
    # PER-008 D2：身份 additive 字段（默认管道也携带；变体带各自的 configId）
    if pipeline_key in _pipelines:
        _, pipeline_identity = _pipelines[pipeline_key]
        if pipeline_identity:
            meta["configId"] = pipeline_identity["configId"]
            meta["pipelineRevision"] = pipeline_identity["pipelineRevision"]
            meta["deploymentId"] = pipeline_identity["deploymentId"]
            meta["pipelineVariant"] = pipeline_key
    return meta


def _server_timing(yolo_ms: float, ocr_ms: float, fusion_ms: float, scroll_ms: float,
                   serialize_ms: float = 0.0, gc_ms: float = 0.0) -> str:
    return (f"yolo;dur={yolo_ms:.1f}, ocr;dur={ocr_ms:.1f}, "
            f"fusion;dur={fusion_ms:.1f}, scroll;dur={scroll_ms:.1f}, "
            f"serialize;dur={serialize_ms:.1f}, gc;dur={gc_ms:.1f}")
