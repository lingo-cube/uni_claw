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
    SCREENPARSER_DETECT_IMPLS,
    PipelineConfig,
    PipelineValidationError,
    detect_device,
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
    from .pipeline import detect_device as _detect_device
    warmup_yolo(device=_detect_device(default_pipeline))
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
) -> tuple[dict[str, Any], tuple[float, float, float, float, float | None]]:
    """Shared staged pipeline（PER-008：STAGES DAG 见 pipeline.py；串行实现，
    detect/recognize 的并行化是 OPT-001 的增量）。Both analyze endpoints call
    this. Preprocessing (crop + resize) applied once at entry so YOLO and OCR
    share the same pixel space. Coordinates remapped back to original
    full-screen space before returning. fusion 参数来自管道配置（默认 = 历史
    硬编码值，行为冻结）。

    返回计时光标 (t0, t1, t2, t3, t_sp)：t0..t3 语义与引入前一致
    （yolo/ocr 段的 t1/t2 合一语义不变）；t_sp = screenparse 串行段的结束
    边界（变体关闭 = None）——Server-Timing 的 fusion 段从 t_sp 起算，
    screenparse 段 = t_sp - t2（各自独立 wall clock，不吞并）。
    """
    cfg = _get_config()
    t0 = time.perf_counter()

    detect_impl_device = detect_device(
        pipeline if pipeline is not None else _pipelines[DEFAULT_PIPELINE_KEY][0])
    # FSV-001 D3：Test B replacement——detect impl = screenparser 系时，FastScreen
    # 承担 detect（screenparse 推理计入 yolo;dur 段，与 baseline 同段语义可对拍）。
    detect_impl = (pipeline.detect.impl
                   if pipeline is not None and pipeline.detect is not None
                   else "torch-yolo")
    replacement_mode = detect_impl in SCREENPARSER_DETECT_IMPLS

    # ── Preprocessing ──
    proc_img, scale, top_px, _ = preprocess(
        image,
        max_width=cfg.max_width,
        crop_top_ratio=cfg.crop_top,
        crop_bottom_ratio=cfg.crop_bottom,
    )
    proc_w, proc_h = proc_img.size
    # FSV-001 WI-6（D11）：screenparse 输入域 = 原始 image（model card
    # operating point），输出坐标逆映射回 proc 空间。bottom_px/crop_h 按
    # preprocessing.preprocess 的同一公式推导（crop 是水平不变的纯 y 平移，
    # 逆映射只需 top_px + crop_h + 两轴比例）。
    bottom_px = int(orig_h * cfg.crop_bottom)
    crop_h = orig_h - top_px - bottom_px

    # OPT-001 S5：detect/recognize 受控并行（STAGES DAG 声明的无依赖边；
    # 合入门槛 = 输出逐字节等价，见 tests/bench 验证）。full-image OCR 路径
    # 与 YOLO 真正无数据依赖（proc_img 共享只读）；ROI 路径 OCR 依赖 YOLO
    # detections，保持串行（并行度为 1 是正确退化）。
    from concurrent.futures import ThreadPoolExecutor

    # FSV-001 D3（replacement）局部上下文：_detect 在 screenparser 系 impl 下
    # 产出 raw/mapped/structural（mapped re-id 为 det_{n} 全量进 detect 池），
    # 供后续组装 screenParse[] additive 键（rescue/corroboration 不适用）。
    replacement_ctx: dict[str, Any] = {}

    def _detect() -> list:
        if replacement_mode:
            from .screenparse import adapt, map_original_to_proc, \
                run_screenparse_on_image
            from .schema import Detection as _detection
            # FSV-001 WI-6（D11）：原图供推理（整屏 operating point），
            # 输出坐标逆映射回 proc 空间——replacement 下 mapped 全量即
            # detect 输出，必须与 YOLO 同空间（remap_coords 照常映回原屏）。
            raw_orig = run_screenparse_on_image(
                image, device=detect_impl_device)
            raw_dets, dropped = map_original_to_proc(
                raw_orig,
                orig_w=orig_w, orig_h=orig_h,
                top_px=top_px, bottom_px=bottom_px,
                proc_w=proc_w, proc_h=proc_h)
            mapped, structural = adapt(raw_dets)
            # B1：mapped 全量即 detect 输出——id 沿用 detect 阶段惯例 det_{n}
            # （replacement 语义：它就是 detect，非 Test A 的 fs_ 救援前缀）；
            # raw_label/raw_class_id 保留 55 类原名（证据 provenance，同
            # yolo/inference.py 的 raw 保留语义）。
            pool = [
                _detection(
                    id=f"det_{idx + 1}",
                    label=d.label,
                    confidence=d.confidence,
                    box=d.box,
                    raw_label=d.raw_label,
                    raw_class_id=d.raw_class_id,
                )
                for idx, d in enumerate(mapped)
            ]
            replacement_ctx.update(raw=raw_dets, mapped=pool,
                                   structural=structural,
                                   dropped=dropped)
            return pool
        return run_yolo_on_image(proc_img, device=detect_impl_device)

    def _recognize_full() -> list:
        return run_rapid_ocr_on_image(proc_img, text_score=cfg.ocr_text_score)

    can_parallel = (cfg.ocr_backend == "rapidocr" and cfg.ocr_mode != "roi")
    if can_parallel:
        with ThreadPoolExecutor(max_workers=2) as pool:
            det_future = pool.submit(_detect)
            ocr_future = pool.submit(_recognize_full)
            detections = det_future.result()
            ocr_tokens = ocr_future.result()
    else:
        detections = _detect()
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
                ocr_tokens = _recognize_full()
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
    # 并行路径计时：yolo = detect wall clock；ocr = overlap wall clock（两段
    # 同时跑，Server-Timing 的 ocr_ms 反映 overlap 后剩余，另加 parallel
    # 指示进 metadata 已由 deploymentId 差异承载（configId 含 detect impl）。
    t1 = time.perf_counter()
    t2 = t1  # 并行路径：两步 wall-clock 合一；串行路径保持原语义

    t2 = time.perf_counter()

    # ── FastScreen replacement 证据（FSV-001 D3；detect impl = screenparser 系）─
    # B1：FastScreen 即 detect——screenparse 推理已计入 yolo;dur 段
    # （t_sp 保持 None：不新增计时分段，与 baseline 同段语义可对拍，见 D7
    # latency 同段对拍）。rescue/corroboration 不适用（无既有 YOLO 输出可救援
    # /对照——mapped 全量即 detect 输出，见 D3）；structural 作 Optional Evidence
    # 进 screenParse[]（D4），mapped 已全量进 yolo[]。summary 注明 replacement 模式。
    t_sp: float | None = None
    screenparse_evidence: dict[str, Any] | None = None
    if replacement_mode:
        from .screenparse import build_screenparse_evidence
        screenparse_evidence = build_screenparse_evidence(
            replacement_ctx["raw"], replacement_ctx["mapped"],
            replacement_ctx["structural"], [], [], proc_w, proc_h,
            dropped_off_canvas=replacement_ctx.get("dropped", 0))
        screenparse_evidence["summary"]["mode"] = "replacement"

    # ── FastScreen 阶段（FSV-001 D2；Test A 集成变体专用）────────────────
    # 仅在管道配置含 screenparse 段时执行——默认管道（无段）代码路径零效果：
    # 不加载模型、不加 additive 键、不加计时分段（回归锚 = 默认输出逐字节
    # 不变）。screenparse 为串行段（detect/recognize 之后、fuse 之前），
    # 独立打点：t_sp = 该段结束边界，fusion 段从 t_sp 起算——yolo/ocr 段的
    # t0/t1/t2 语义保持不变。（replacement 模式与集成段互斥，lint 处 fail-
    # closed——见 pipeline.lint_against_config；此处两个声明/赋值不冲突。）
    if pipeline is not None and pipeline.screenparse is not None:
        from .screenparse import (
            adapt,
            build_screenparse_evidence,
            collect_corroborations,
            map_original_to_proc,
            run_screenparse_on_image,
            select_rescue,
        )
        sp_start = time.perf_counter()
        # device 跟随 detect impl 的推导（cpu 默认；D1 主表 CPU）。
        # FSV-001 WI-6（D11）：原图供推理（整屏 operating point，model card），
        # 输出坐标逆映射回 proc 空间再参与 rescue 合并/序列化——fusion/remap
        # 语义零变化（后续 remap_coords 照常把 proc 空间映回原屏）。
        raw_orig = run_screenparse_on_image(image, device=detect_impl_device)
        raw_dets, dropped = map_original_to_proc(
            raw_orig,
            orig_w=orig_w, orig_h=orig_h,
            top_px=top_px, bottom_px=bottom_px,
            proc_w=proc_w, proc_h=proc_h)
        mapped, structural = adapt(raw_dets)
        existing = list(detections)
        # rescue 规则（D2，确定性）：与现有池 IoU < rescueIouMax 且 conf >=
        # minRescueConf 的 mapped → 追加进 detections 池（参与 fuse；追加在
        # 尾部，id 前缀 fs_）。判定以 stage 入口快照为基准（见 adapter）。
        rescued = select_rescue(
            mapped, existing,
            min_rescue_conf=pipeline.screenparse.min_rescue_conf,
            rescue_iou_max=pipeline.screenparse.rescue_iou_max)
        # corroboration 诊断（不改写标签）：高 IoU 标签分歧只记录。
        corroborations = collect_corroborations(mapped, existing)
        detections.extend(rescued)
        screenparse_evidence = build_screenparse_evidence(
            raw_dets, mapped, structural, corroborations, rescued,
            proc_w, proc_h, dropped_off_canvas=dropped)
        t_sp = time.perf_counter()

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

    # FSV-001 D2：screenParse additive 证据（变体关闭时整体缺席——默认回归
    # 锚）。yolo[] 内的 fs_ 救援条目随 remap 回原屏空间；screenParse[] 本身
    # 保持 preprocessed 空间（原始适配证据，坐标契约注释见
    # build_screenparse_evidence）。remap/enforce_geometry 只触碰已知键，
    # 本键原样透传。
    if screenparse_evidence is not None:
        evidence["screenParse"] = screenparse_evidence

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
        return evidence, (t0, t1, t2, t3, t_sp), views
    return evidence, (t0, t1, t2, t3, t_sp)


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
            evidence, (t0, t1, t2, t3, t_sp), stage_views = pipeline
            response_body = dict(evidence, stageViews=stage_views)
        else:
            evidence, (t0, t1, t2, t3, t_sp) = pipeline
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
                fusion_ms=((t3 - t_sp) * 1000 if t_sp is not None
                           else (t3 - t2) * 1000),
                scroll_ms=(t4 - t3) * 1000,
                serialize_ms=(t5 - serialize_start) * 1000,
                gc_ms=(t6 - gc_start) * 1000,
                screenparse_ms=((t_sp - t2) * 1000 if t_sp is not None else None),
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
            evidence, (t0, t1, t2, t3, t_sp), stage_views = pipeline
            response_body = dict(evidence, stageViews=stage_views)
        else:
            evidence, (t0, t1, t2, t3, t_sp) = pipeline
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
                fusion_ms=((t3 - t_sp) * 1000 if t_sp is not None
                           else (t3 - t2) * 1000),
                scroll_ms=(t4 - t3) * 1000,
                serialize_ms=(t5 - serialize_start) * 1000,
                gc_ms=(t6 - gc_start) * 1000,
                screenparse_ms=((t_sp - t2) * 1000 if t_sp is not None else None),
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
                   serialize_ms: float = 0.0, gc_ms: float = 0.0,
                   screenparse_ms: float | None = None) -> str:
    """Server-Timing 头。screenparse 段默认缺席（变体关闭 → 与引入前字符串
    逐字节一致）；启用时插在 ocr 之后、fusion 之前（FSV-001 D2）。"""
    parts = [
        f"yolo;dur={yolo_ms:.1f}",
        f"ocr;dur={ocr_ms:.1f}",
    ]
    if screenparse_ms is not None:
        parts.append(f"screenparse;dur={screenparse_ms:.1f}")
    parts.append(f"fusion;dur={fusion_ms:.1f}")
    parts.append(f"scroll;dur={scroll_ms:.1f}")
    parts.append(f"serialize;dur={serialize_ms:.1f}")
    parts.append(f"gc;dur={gc_ms:.1f}")
    return ", ".join(parts)
