# server.py — FastAPI wrapper around existing analyze pipeline
# ⚠️ OMP_NUM_THREADS 必须在任何 numpy/ultralytics/paddleocr import 之前设置，
#    OpenMP 线程数在库初始化时固化，之后设置无效（D-18）。
import os

os.environ["OMP_NUM_THREADS"] = os.environ.get(
    "UNICLAW_OMP_THREADS", os.environ.get("OMP_NUM_THREADS", "4"))

import gc
import hashlib
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

from .backends import (
    configure_roi_padding,
    run_ocr_on_crops,
    run_rapid_ocr_on_image,
    run_yolo_on_image,
    warmup_ocr,
    warmup_rapid_ocr,
    _crop_padded,
    _rapid_ocr_one_crop,
    _roi_padding_px,
)
from .schema import Detection, Box, OcrToken
from .fusion import (
    DEFAULT_INTERACTIVE_LABELS,
    fuse_evidence,
    fuse_evidence_from_crops,
)

_MODEL_PATH = os.environ.get("UNICLAW_YOLO_MODEL", "artifacts/local-vision/models/android_ui_detection_yolov8/best.pt")
# D-198: OCR 后端默认 paddleocr → rapidocr — paddleocr 2.10 每请求内存泄漏
# (D-4 手动 gc 仅缓兵)，长跑服务 OOM 死亡 (实测集成 run 中途 1ms 连接失败，
# engine.run 崩溃)。RapidOCR (ONNX Runtime) 实例线程安全、无泄漏、
# 内存 ~300-500MB、单图 10-25ms，中英文混排原生支持 (语言参数 no-op，
# _OCR_LANG 仅 paddleocr 分支生效，保留给 UNICLAW_OCR_LANG 覆盖)。
_OCR_LANG = os.environ.get("UNICLAW_OCR_LANG", "en")
# OCR 后端选择：rapidocr（默认，ONNX Runtime，D-198）或 paddleocr（Paddle Inference C++）。
# UNICLAW_OCR_BACKEND 环境变量可临时切回 paddleocr 对比。
_OCR_BACKEND = os.environ.get("UNICLAW_OCR_BACKEND", "rapidocr").lower()
# RapidOCR 识别置信度阈值（默认 0.5），低于此值的 token 丢弃。仅 rapidocr 后端生效。
_OCR_TEXT_SCORE = float(os.environ.get("UNICLAW_OCR_TEXT_SCORE", "0.5"))
_IMAGE_SIZE = 640
_WARMUP_IMAGE = Image.new("RGB", (640, 640), (0, 0, 0))

_SPATIAL: dict[str, Any] = {}
_DETECTION_CONF = 0.35  # R-17: 由 label-mapping.json detection.confidence 覆盖
# OCR 模式: "full" = 全图一次 DBNet（默认，RapidOCR 最优）；"roi" = YOLO 文本框合并后逐框 OCR
_OCR_MODE = os.environ.get("UNICLAW_OCR_MODE", "full")
# raw RGBA 预处理参数（label-mapping.json spatial.preprocessing；env > config > default，D-2）
_MAX_WIDTH = 720
_CROP_TOP = 0.0
_CROP_BOTTOM = 0.0
_CONFIG_HASH = ""
_WARM = False  # R-9: 预热完成前 /health 返回 warm=false
# ROI-OCR 文本标签白名单：非文本标签（icon/switch/checkbox/popup/image）不跑 OCR
_TEXT_LIKELY_LABELS = frozenset({"text_block", "input", "button", "list_item", "toolbar", "tab"})


def _load_spatial() -> None:
    """读取共享配置（与 C# 单点真源；UNICLAW_LABEL_MAPPING 可覆盖路径）。"""
    global _SPATIAL, _DETECTION_CONF, _CONFIG_HASH, _MAX_WIDTH, _CROP_TOP, _CROP_BOTTOM
    path = Path(
        os.environ.get("UNICLAW_LABEL_MAPPING", "tools/local_vision/label-mapping.json"))
    content = path.read_bytes()
    _CONFIG_HASH = hashlib.sha256(content).hexdigest()
    data = json.loads(content.decode("utf-8"))
    _SPATIAL = data.get("spatial", {})
    _DETECTION_CONF = data.get("detection", {}).get("confidence", _DETECTION_CONF)
    configure_roi_padding(_SPATIAL.get("roiPadding", {}))
    # raw RGBA 预处理参数：env > config > default（D-2）
    _PREPROCESS = _SPATIAL.get("preprocessing", {})
    _MAX_WIDTH = int(os.environ.get("UNICLAW_IMAGE_MAX_WIDTH",
        _PREPROCESS.get("maxWidth", 720)))
    _CROP_TOP = float(os.environ.get("UNICLAW_IMAGE_CROP_TOP",
        _PREPROCESS.get("cropTopRatio", 0.0)))
    _CROP_BOTTOM = float(os.environ.get("UNICLAW_IMAGE_CROP_BOTTOM",
        _PREPROCESS.get("cropBottomRatio", 0.0)))


def warmup_yolo() -> None:
    """预热 YOLO（模块级缓存模型 + 一次空图推理；首次 load 5-10s）。"""
    run_yolo_on_image(_WARMUP_IMAGE, model_path=_MODEL_PATH,
                      image_size=_IMAGE_SIZE, confidence=_DETECTION_CONF,
                      device="cpu")


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _WARM
    _load_spatial()
    warmup_yolo()
    if _OCR_BACKEND == "rapidocr":
        warmup_rapid_ocr()
    else:
        warmup_ocr(language=_OCR_LANG)
    _WARM = True
    yield


app = FastAPI(lifespan=lifespan)

_logger = logging.getLogger("uniclaw.local_vision")


@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception):
    """未捕获异常 → 500 响应 body 带根因摘要（C# LocalVisionProvider 透传 errBody，
    测试失败消息即可显示真实错误，无需翻阅 uvicorn stderr）；完整 traceback 落日志。"""
    if isinstance(exc, HTTPException):
        return JSONResponse(status_code=exc.status_code, content={"detail": exc.detail})
    _logger.error("unhandled exception in %s:\n%s", request.url.path, traceback.format_exc())
    return JSONResponse(
        status_code=500,
        content={"detail": f"{type(exc).__name__}: {exc}"})


def _preprocess(image: Image.Image) -> tuple[Image.Image, float, float, int]:
    """Crop top/bottom + resize to max width. PIL zero-decode path.

    Returns:
        (preprocessed_image, scale, top_px, orig_h)
        scale = orig_w / preproc_w (both axes, >1 = downscaled)
        top_px = pixels cropped from top in original coordinates
        orig_h = original full-screen height (before any crop)
    """
    orig_w, orig_h = image.size

    # Step 1: crop
    top_px = int(orig_h * _CROP_TOP)
    bottom_px = int(orig_h * _CROP_BOTTOM)
    if top_px > 0 or bottom_px > 0:
        crop_h = orig_h - top_px - bottom_px
        if crop_h > 0:
            image = image.crop((0, top_px, orig_w, orig_h - bottom_px))
    else:
        top_px = 0

    # Step 2: resize
    scale = 1.0
    if _MAX_WIDTH > 0 and image.width > _MAX_WIDTH:
        scale = image.width / _MAX_WIDTH
        new_h = int(image.height / scale)
        image = image.resize((_MAX_WIDTH, new_h), Image.LANCZOS)

    return image, scale, top_px, orig_h


def _remap_coords(
    evidence: dict[str, Any],
    scale: float,
    top_px: float,
    orig_w: int,
    orig_h: int,
) -> None:
    """将 evidence 中所有坐标从预处理像素空间映射回原始全屏像素空间。

    映射:  x_orig = x_preproc * scale
           y_orig = y_preproc * scale + top_px
    归一化: norm_x = x_orig / orig_w, norm_y = y_orig / orig_h

    对 boundsPx/centerPx 做像素级重映射后，重新计算归一化 bounds/center/coordinate。
    幂等: scale==1.0 且 top_px==0 时直接返回。
    """
    if scale == 1.0 and top_px == 0.0:
        return

    def _remap_item(obj: dict[str, Any]) -> None:
        # ── pixel coords: boundsPx ──
        if "boundsPx" in obj and isinstance(obj["boundsPx"], list) and len(obj["boundsPx"]) == 4:
            x1 = obj["boundsPx"][0] * scale
            y1 = obj["boundsPx"][1] * scale + top_px
            x2 = obj["boundsPx"][2] * scale
            y2 = obj["boundsPx"][3] * scale + top_px
            obj["boundsPx"] = [round(x1), round(y1), round(x2), round(y2)]

            # Recompute normalized bounds from remapped pixel coords
            obj["bounds"] = {
                "x1": round(x1 / orig_w, 6),
                "y1": round(y1 / orig_h, 6),
                "x2": round(x2 / orig_w, 6),
                "y2": round(y2 / orig_h, 6),
            }

        # ── pixel coords: centerPx ──
        if "centerPx" in obj and isinstance(obj["centerPx"], list) and len(obj["centerPx"]) == 2:
            cx = obj["centerPx"][0] * scale
            cy = obj["centerPx"][1] * scale + top_px
            obj["centerPx"] = [round(cx), round(cy)]

            # Recompute normalized center from remapped pixel coords
            obj["center"] = {
                "x": round(cx / orig_w, 6) if orig_w else 0.0,
                "y": round(cy / orig_h, 6) if orig_h else 0.0,
            }

        # coordinate (same as center in candidate schema)
        if "coordinate" in obj and isinstance(obj["coordinate"], dict):
            if "centerPx" in obj:
                cx = obj["centerPx"][0]
                cy = obj["centerPx"][1]
            else:
                cx = obj["coordinate"].get("x", 0.0) * orig_w * scale
                cy = obj["coordinate"].get("y", 0.0) * orig_h * scale + top_px
            obj["coordinate"] = {
                "x": round(cx / orig_w, 6) if orig_w else 0.0,
                "y": round(cy / orig_h, 6) if orig_h else 0.0,
            }

    for c in evidence.get("candidates", []):
        _remap_item(c)
    for d in evidence.get("yolo", []):
        _remap_item(d)
    for t in evidence.get("ocr", []):
        _remap_item(t)

    if "image" in evidence:
        evidence["image"]["width"] = orig_w
        evidence["image"]["height"] = orig_h
    if "metadata" in evidence:
        evidence["metadata"]["width"] = orig_w
        evidence["metadata"]["height"] = orig_h


def _merge_adjacent_boxes(dets: list[Detection]) -> list[Detection]:
    """合并垂直相邻、水平重叠的 YOLO 文本框，减少 DBNet 调用次数。

    Settings 首页 16 个 text_block → 6 个合并框（相邻行间距 < 1.5×行高
    且水平重叠 > 30% → 合并）。纯图标/开关已在调用侧过滤。
    """
    if not dets:
        return []
    sorted_dets = sorted(dets, key=lambda d: (d.box.y1, d.box.x1))
    merged = [sorted_dets[0]]
    for d in sorted_dets[1:]:
        prev = merged[-1]
        avg_h = ((prev.box.y2 - prev.box.y1) + (d.box.y2 - d.box.y1)) / 2
        y_gap = d.box.y1 - prev.box.y2
        x_overlap = max(0, min(prev.box.x2, d.box.x2) - max(prev.box.x1, d.box.x1))
        x_ratio = x_overlap / max(
            prev.box.x2 - prev.box.x1, d.box.x2 - d.box.x1, 1)
        if y_gap < avg_h * 1.5 and x_ratio > 0.3:
            merged[-1] = Detection(
                id="merged", label="merged", confidence=0.5,
                box=Box(min(prev.box.x1, d.box.x1), min(prev.box.y1, d.box.y1),
                        max(prev.box.x2, d.box.x2), max(prev.box.y2, d.box.y2)))
        else:
            merged.append(d)
    return merged


def _run_pipeline(
    image: Image.Image,
    orig_w: int,
    orig_h: int,
) -> tuple[dict[str, Any], tuple[float, float, float, float]]:
    """Shared YOLO → OCR → fusion pipeline. Both endpoints call this.

    Preprocessing (crop top/bottom + resize) is applied once at entry so YOLO
    and OCR share the same pixel space.  Coordinates are remapped back to the
    original full-screen space before returning.
    """
    t0 = time.perf_counter()

    # ── Preprocessing (crop + resize) — unified for YOLO and OCR ──
    proc_img, scale, top_px, _ = _preprocess(image)
    proc_w, proc_h = proc_img.size

    # Step 1: YOLO on preprocessed image
    detections = run_yolo_on_image(proc_img, model_path=_MODEL_PATH,
                                   image_size=_IMAGE_SIZE,
                                   confidence=_DETECTION_CONF, device="cpu")
    t1 = time.perf_counter()

    # Step 2: OCR
    if _OCR_BACKEND == "rapidocr":
        if _OCR_MODE == "roi":
            # ── ROI-OCR: 过滤文本标签 → 合并相邻框 → 逐框 OCR ──
            # 2026-08-05 benchmark: DBNet 次数是唯一主导因子。全图 1 次 ≈ 2.2s，
            # 原始 16 框 ≈ 20s（16×DBNet），合并后 6 框 ≈ 5.7s（6×DBNet）。
            # ROI 模式仅在文字稀疏页面有意义（地图/相册，≤ 2 crop）。
            text_dets = [d for d in detections if d.label in _TEXT_LIKELY_LABELS]
            merged = _merge_adjacent_boxes(text_dets)
            padding = _roi_padding_px(100, 20)
            ocr_tokens: list[OcrToken] = []
            for m in merged:
                crop = _crop_padded(proc_img, m.box, padding)
                if crop is not None:
                    ocr_tokens.extend(_rapid_ocr_one_crop(crop, m, _OCR_TEXT_SCORE))
        else:
            # ── Full-image OCR（默认）: 预处理图 1 次 DBNet ──
            # YOLO + OCR 共享预处理后的图像（720×1120, 39% 面积），
            # 实测 YOLO + OCR 合计从 ~6s → ~4s（1.5×）。
            ocr_tokens = run_rapid_ocr_on_image(proc_img, text_score=_OCR_TEXT_SCORE)
    else:
        # paddleocr 回退路径
        _NON_TEXT_LABELS = frozenset({"imageview", "line"})
        ocr_detections = [d for d in detections if d.label not in _NON_TEXT_LABELS]
        ocr_crops = run_ocr_on_crops(proc_img, ocr_detections, language=_OCR_LANG)
        ocr_idx = 0
        aligned_ocr: list[list[OcrToken]] = []
        for d in detections:
            if d.label in _NON_TEXT_LABELS:
                aligned_ocr.append([])
            else:
                aligned_ocr.append(ocr_crops[ocr_idx])
                ocr_idx += 1
    t2 = time.perf_counter()

    # Step 3: 融合 (in preprocessed pixel space)
    if _OCR_BACKEND == "rapidocr":
        evidence = fuse_evidence(
            detections, ocr_tokens,
            image_width=proc_w, image_height=proc_h,
            interactive_labels=DEFAULT_INTERACTIVE_LABELS | {"text_block", "text"},
            promote_unmatched_ocr=True)
    else:
        evidence = fuse_evidence_from_crops(
            detections, aligned_ocr,
            image_width=proc_w, image_height=proc_h)
    t3 = time.perf_counter()

    # ── Remap all coords back to original full-screen space ──
    _remap_coords(evidence, scale, top_px, orig_w, orig_h)

    evidence["metadata"] = _metadata(orig_w, orig_h)
    evidence["scrollHints"] = _scroll_hints(evidence["candidates"])
    return evidence, (t0, t1, t2, t3)


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
        gc.collect()  # D-4: PaddleOCR 已知内存泄漏，每请求手动回收。RapidOCR 无此问题但仍保留无害回收。


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


@app.get("/health")
async def health():
    return {"status": "ok", "warm": _WARM}


def _scroll_hints(candidates: list[dict[str, Any]]) -> dict[str, Any]:
    """滚动原始可观测值（判断在 C# 侧）。

    `candidatesNearBottom` 阈值读自共享 label-mapping.json 的
    `spatial.edgeThreshold`（单点真源，V22/D-19）。
    """
    threshold = _SPATIAL.get("edgeThreshold", 0.92)
    return {
        "totalCandidates": len(candidates),
        "candidatesNearBottom": sum(
            1 for c in candidates
            if (c.get("center") or {}).get("y", 0.0) > threshold),
        "scrollbarDetected": any(c.get("type") == "scrollbar" for c in candidates),
    }


def _metadata(width: int, height: int) -> dict[str, Any]:
    """R-6: schema 版本 + pipeline 信息 + models + configHash（SHA-256）。"""
    return {
        "schema": "uniclaw.localVisionEvidence.v1",
        "width": width,
        "height": height,
        "pipeline": {"name": "local-vision", "version": "1.0"},
        "models": {"yolo": _MODEL_PATH, "ocr": _OCR_BACKEND},
        "configHash": _CONFIG_HASH,
    }


def _server_timing(yolo_ms: float, ocr_ms: float, fusion_ms: float, scroll_ms: float) -> str:
    return f"yolo;dur={yolo_ms:.1f}, ocr;dur={ocr_ms:.1f}, " \
           f"fusion;dur={fusion_ms:.1f}, scroll;dur={scroll_ms:.1f}"
