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
)
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
_CONFIG_HASH = ""
_WARM = False  # R-9: 预热完成前 /health 返回 warm=false


def _load_spatial() -> None:
    """读取共享配置（与 C# 单点真源；UNICLAW_LABEL_MAPPING 可覆盖路径）。"""
    global _SPATIAL, _DETECTION_CONF, _CONFIG_HASH
    path = Path(
        os.environ.get("UNICLAW_LABEL_MAPPING", "tools/local_vision/label-mapping.json"))
    content = path.read_bytes()
    _CONFIG_HASH = hashlib.sha256(content).hexdigest()
    data = json.loads(content.decode("utf-8"))
    _SPATIAL = data.get("spatial", {})
    _DETECTION_CONF = data.get("detection", {}).get("confidence", _DETECTION_CONF)
    configure_roi_padding(_SPATIAL.get("roiPadding", {}))


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


@app.post("/v1/analyze")
async def analyze(request: Request):
    try:
        image_bytes = await request.body()
        image = Image.open(BytesIO(image_bytes))
        width, height = image.size

        t0 = time.perf_counter()

        # Step 1: YOLO（PIL 内存推理，零磁盘）
        detections = run_yolo_on_image(image, model_path=_MODEL_PATH,
                                       image_size=_IMAGE_SIZE,
                                       confidence=_DETECTION_CONF, device="cpu")
        t1 = time.perf_counter()

        if _OCR_BACKEND == "rapidocr":
            # D-XXX: 全图一次 det + 批量 rec（~2.8s）取代逐 crop det（实测
            # 26 框 × 0.94s ≈ 19s，每 crop 都被放大到 736×736 跑 DBNet）。
            # det 行是真实文本行，token 全图坐标，融合层空间匹配关联 YOLO。
            # 文本行本身可点击 (Settings 行为 text_block)，纳入 interactive。
            ocr_tokens = run_rapid_ocr_on_image(image, text_score=_OCR_TEXT_SCORE)
        else:
            # Step 2: ROI 裁剪 → 多线程 OCR（paddleocr 回退路径，跳过纯图标/线）
            _NON_TEXT_LABELS = frozenset({"imageview", "line"})
            ocr_detections = [d for d in detections if d.label not in _NON_TEXT_LABELS]
            ocr_crops = run_ocr_on_crops(image, ocr_detections, language=_OCR_LANG)
            # 重建与原始 detections 对齐
            ocr_idx = 0
            aligned_ocr: list[list[OcrToken]] = []
            for d in detections:
                if d.label in _NON_TEXT_LABELS:
                    aligned_ocr.append([])
                else:
                    aligned_ocr.append(ocr_crops[ocr_idx])
                    ocr_idx += 1
        t2 = time.perf_counter()

        # Step 3: 融合
        if _OCR_BACKEND == "rapidocr":
            evidence = fuse_evidence(
                detections, ocr_tokens,
                image_width=width, image_height=height,
                interactive_labels=DEFAULT_INTERACTIVE_LABELS | {"text_block", "text"},
                promote_unmatched_ocr=True)
        else:
            evidence = fuse_evidence_from_crops(
                detections, aligned_ocr,
                image_width=width, image_height=height)
        t3 = time.perf_counter()

        evidence["metadata"] = _metadata(width, height)
        evidence["scrollHints"] = _scroll_hints(evidence["candidates"])
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
