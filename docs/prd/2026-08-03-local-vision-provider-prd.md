# Local Vision Provider — Python YOLO+OCR 视觉服务集成

> 日期: 2026-08-03
> 状态: approved
> 范围: `tools/local_vision/server.py` (Python) + `src/UniClaw.Device/` (C#) + `src/UniClaw.Core/UniBrain/` (Core) + `src/UniClaw.Core/Traversal/` (VisionScreenStateProvider)

## 1. Motivation

`tools/local_vision/` 已有完整的 YOLO+PaddleOCR 管线（`analyze.py` + `fusion.py`），输出 `uniclaw.localVisionEvidence.v1` 证据 JSON，但**零 C# 集成**——当前只能通过 CLI 一次一图调用。

目标：将 Python 管线包装为常驻 FastAPI 服务，C# 侧通过 `IModelProvider` 接缝注入，`PageAnalyzer` 零改动即可在云端 AI 和本地视觉之间切换。

## 2. Architecture

### 2.1 模块分层（高内聚低耦合）

```
┌─────────────────────────────────────────────────────────────┐
│  Core (UniClaw.Core)               ← 纯逻辑, 零直接 I/O     │
│      (UniBrain/ + Traversal/)         网络经注入 HttpClient   │
│                                                              │
│  LocalVisionProvider : IModelProvider         (UniBrain/)    │
│    职责: HTTP → evidence → PageAnalysisDto 映射              │
│    依赖: HttpClient (注入), LabelMappingConfig               │
│    不依赖: PythonVisionService, Process, ADB                 │
│                                                              │
│  VisionScreenStateProvider : IScreenStateProvider (Traversal/)│
│    职责: PageAnalysis → HasScroll/IsEndOfList 读取           │
│    依赖: Func<PageAnalysis?> (注入)                          │
│    不依赖: UIAutomator, ADB, Python                          │
│    位置: Traversal/（UniBrain 目录受 Guard 约束禁止引用      │
│          Traversal 类型，见 UniBrainGuardTests）             │
└─────────────────────────────────────────────────────────────┘
          ▲                              ▲
          │ 注入 HttpClient              │ 注入委托
          │                              │
┌─────────────────────────────────────────────────────────────┐
│  Device (UniClaw.Device)             ← I/O 适配层            │
│                                                              │
│  PythonVisionService : IPythonVisionService                  │
│    职责: Python 进程生命周期 + HttpClient 工厂               │
│    依赖: Process (System.Diagnostics)                        │
│    不依赖: IModelProvider, PageAnalysis, YOLO, OCR           │
│    输出: HttpClient (已配好 UDS/TCP)                         │
│                                                              │
│  AdvancedSharpAdbSession : IAdbSession  ← 独立 PRD          │
│    职责: ADB TCP 长连接 + 命令执行 + 自愈                    │
│    依赖: AdvancedSharpAdbClient (NuGet)                      │
│    不依赖: Vision, Python, PageAnalysis                      │
└─────────────────────────────────────────────────────────────┘
          ▲
          │ HTTP POST image/jpeg  (+ trace headers)
          │
┌─────────────────────────────────────────────────────────────┐
│  Python (tools/local_vision/)        ← 视觉推理引擎          │
│                                                              │
│  server.py: FastAPI                                          │
│    职责: 接收图片 → YOLO → ROI OCR → 返回 evidence JSON      │
│    依赖: ultralytics, paddleocr, PIL                         │
│    不依赖: C#, ADB, PageAnalysis                             │
│    输出: uniclaw.localVisionEvidence.v1 JSON                 │
└─────────────────────────────────────────────────────────────┘
```

**依赖方向单向**：`Host → Device → Core`，`Core → Core 接口`。Python 完全独立。无循环引用。

### 2.2 拓扑图

```
┌──────────────────────────────────────────────────────────┐
│                    C# UniClaw.Core                        │
│                                                           │
│  PageAnalyzer (不动)                                      │
│    ├─ IScreenCapture.CaptureAsync() → byte[]              │
│    ├─ IPromptLibrary.GetTemplate(AnalyzeVisual)           │
│    ├─ IModelProvider.CompleteVisionAsync(req, bytes) ─────┼───┐
│    └─ JsonSerializer.Deserialize<PageAnalysisDto>(json)   │   │
│                                                           │   │
│  LocalVisionProvider : IModelProvider  ◀──────────────────┼───┤
│    ├─ HttpClient + trace headers → POST /v1/analyze       │   │
│    ├─ LabelMappingConfig : YOLO label → AI type           │   │
│    ├─ 空间推理: Y 轴聚类 → menus                          │   │
│    ├─ 滚动判断: candidates 位置 → has_scroll/is_end_of_list│   │
│    ├─ Stopwatch 量延迟 → ModelResponse.LatencyMs          │   │
│    └─ → PageAnalysisDto JSON                              │   │
│                                                           │   │
│  VisionScreenStateProvider : IScreenStateProvider           │   │
│    └─ 薄包装: 从 PageAnalysis 读取 HasScroll/IsEndOfList   │   │
│                                                           │   │
│  PythonVisionService : IPythonVisionService                │   │
│    ├─ Process 生命周期 (start/auto-restart/stop)          │   │
│    ├─ UDS/TCP 双模式 HttpClient 工厂                      │   │
│    └─ 健康检查 + 退避重试                                  │   │
└──────────────────────────────────────────────────────────┘   │
                                                                │
┌───────────────────────────────────────────────────────────┐   │
│            Python FastAPI (tools/local_vision/)            │   │
│                                                           │   │
│  POST /v1/analyze   ← raw JPEG bytes                     │◀──┘
│    ├─ PIL.Image.open(BytesIO(...)) — 零磁盘 I/O           │
│    ├─ run_yolo()     → Detections[]                       │
│    ├─ run_ocr_on_crops() → ROI 裁剪 + 多线程 OCR           │
│    ├─ fuse_evidence_from_crops() → candidates[] (无空间匹配) │
│    ├─ build_scroll_hints() → scrollHints{}                │
│    └─ → evidence JSON (uniclaw.localVisionEvidence.v1)    │
│                                                           │
│  GET /health → {"status": "ok"}                           │
│                                                           │
│  Response Headers: Server-Timing                          │
│  OMP_NUM_THREADS=4  gc.collect() per request              │
└───────────────────────────────────────────────────────────┘
```

## 3. HTTP 协议

### 3.1 Request（C# → Python）

```
POST /v1/analyze
Content-Type: image/jpeg
X-Uniclaw-Trace-Id: engine-abc123
X-Uniclaw-Step-Id: abc123-000042

<raw JPEG bytes>
```

| Header | 含义 |
|--------|------|
| `X-Uniclaw-Trace-Id` | 当前 traversal run 的 trace ID（v1 预留，C# 不发送；见 D-14） |
| `X-Uniclaw-Step-Id` | 当前 engine.step 的 span ID（v1 预留，C# 不发送；见 D-14） |

### 3.2 Response（Python → C#）

```
200 OK
Content-Type: application/json
Server-Timing: yolo;dur=45.2, ocr;dur=68.7, fusion;dur=2.3, scroll;dur=0.4

{"candidates": [...], "scrollHints": {...}, ...}
```

| Header | 含义 | 标准 |
|--------|------|------|
| `Server-Timing` | 各阶段延迟（yolo/ocr/fusion/scroll） | [W3C Server-Timing](https://w3c.github.io/server-timing/) |

**JSON body 不含 timing**——视觉 API 的职责是"看到什么"，不是"算得多快"。timing 走 header，C# 侧按需消费，不影响 JSON schema。

C# `LocalVisionProvider` 解析 `Server-Timing` 后写入 trace 子 span：

```csharp
var sw = Stopwatch.StartNew();
var httpResp = await _httpClient.PostAsync("/v1/analyze", content, ct);
sw.Stop();

// 解析 Server-Timing → 写入 trace（有就写，没有也不影响主流程）
if (httpResp.Headers.TryGetValues("Server-Timing", out var timings))
{
    foreach (var entry in ParseServerTiming(timings))
    {
        _traceRecorder?.StartSpanAsync($"ai.{entry.Name}", parentSpanId: aiCallSpanId,
            new Dictionary<string, object> { ["ai.latency_ms"] = entry.DurationMs });
    }
}

return new ModelResponse(
    ...
    LatencyMs: sw.Elapsed.TotalMilliseconds,
    Success: true);
```

## 4. Python FastAPI 服务

### 4.1 新增文件: `tools/local_vision/server.py`

复用现有 `backends.py`（`run_yolo`、`run_paddle_ocr` 保留；新增 `run_yolo_on_image`、`warmup_ocr`、`_call_paddle_ocr` 的 ndarray 支持，见 §5.3）、`fusion.py`（`fuse_evidence` 保留，新增 `fuse_evidence_from_crops`）、`schema.py`。新增 `server.py`：

```python
# server.py — FastAPI wrapper around existing analyze pipeline
# ⚠️ OMP_NUM_THREADS 必须在任何 numpy/ultralytics/paddleocr import 之前设置，
#    OpenMP 线程数在库初始化时固化，之后设置无效。
import os

os.environ["OMP_NUM_THREADS"] = "4"

import gc
import json
import time
from contextlib import asynccontextmanager
from io import BytesIO
from pathlib import Path

from fastapi import FastAPI, Request, Response
from PIL import Image

from .backends import run_ocr_on_crops, run_yolo_on_image, warmup_ocr
from .fusion import fuse_evidence_from_crops

_MODEL_PATH = "yolo11n.pt"
_WARMUP_IMAGE = Image.new("RGB", (640, 640), (0, 0, 0))
_SPATIAL: dict[str, float] = {}
_DETECTION_CONF = 0.35  # R-17: label-mapping.json detection.confidence 覆盖
_ROI_PADDING_SPEC: dict[str, float] = {}  # R-14: spatial.roiPadding 覆盖


def _load_spatial() -> None:
    """读取共享配置（与 C# 单点真源；UNICLAW_LABEL_MAPPING 可覆盖路径）。"""
    global _SPATIAL, _DETECTION_CONF, _ROI_PADDING_SPEC
    path = Path(
        os.environ.get("UNICLAW_LABEL_MAPPING", "tools/local_vision/label-mapping.json"))
    data = json.loads(path.read_text(encoding="utf-8"))
    _SPATIAL = data.get("spatial", {})
    _DETECTION_CONF = data.get("detection", {}).get("confidence", _DETECTION_CONF)
    _ROI_PADDING_SPEC = _SPATIAL.get("roiPadding", {})


def _roi_padding_px(box_width: float, box_height: float) -> int:
    """R-14: ROI padding 按框尺寸比例（x/y），像素下限/上限钳制，替换硬编码 4px。"""
    spec = _ROI_PADDING_SPEC
    px = max(
        spec.get("x", 0.15) * box_width,
        spec.get("y", 0.1) * box_height,
        float(spec.get("minPx", 8)),
    )
    return int(min(px, spec.get("maxPx", 64)))


def warmup_yolo() -> None:
    """预热 YOLO（模块级缓存模型 + 一次空图推理；首次 load 5-10s）。"""
    run_yolo_on_image(_WARMUP_IMAGE, model_path=_MODEL_PATH,
                      image_size=640, confidence=_DETECTION_CONF, device="cpu")


@asynccontextmanager
async def lifespan(app: FastAPI):
    _load_spatial()
    warmup_yolo()
    warmup_ocr()
    yield


app = FastAPI(lifespan=lifespan)


@app.post("/v1/analyze")
async def analyze(request: Request):
    image_bytes = await request.body()
    image = Image.open(BytesIO(image_bytes))
    width, height = image.size

    t0 = time.perf_counter()

    # Step 1: YOLO（PIL 内存推理，零磁盘）
    detections = run_yolo_on_image(image, model_path=_MODEL_PATH,
                                   image_size=640, confidence=0.35, device="cpu")
    t1 = time.perf_counter()

    # Step 2: ROI 裁剪 → 多线程 OCR（ndarray 直传，零磁盘）
    crops_ocr = run_ocr_on_crops(image, detections, language="ch")
    t2 = time.perf_counter()

    # Step 3: 融合（无需空间匹配）
    evidence = fuse_evidence_from_crops(
        detections, crops_ocr,
        image_width=width, image_height=height)
    t3 = time.perf_counter()

    evidence["metadata"] = _metadata(width, height)
    evidence["scrollHints"] = _scroll_hints(evidence["candidates"])
    t4 = time.perf_counter()

    gc.collect()

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


@app.get("/health")
async def health():
    return {"status": "ok"}


def _scroll_hints(candidates: list[dict[str, object]]) -> dict[str, object]:
    """滚动原始可观测值（判断在 C# 侧，见 §4.5）。"""
    threshold = _SPATIAL.get("edgeThreshold", 0.92)
    return {
        "totalCandidates": len(candidates),
        "candidatesNearBottom": sum(
            1 for c in candidates
            if (c.get("center") or {}).get("y", 0.0) > threshold),
        "scrollbarDetected": any(c.get("type") == "scrollbar" for c in candidates),
    }


def _metadata(width: int, height: int) -> dict[str, object]:
    return {"schema": "uniclaw.localVisionEvidence.v1",
            "width": width, "height": height}


def _server_timing(yolo_ms, ocr_ms, fusion_ms, scroll_ms) -> str:
    return f"yolo;dur={yolo_ms:.1f}, ocr;dur={ocr_ms:.1f}, " \
           f"fusion;dur={fusion_ms:.1f}, scroll;dur={scroll_ms:.1f}"
```

### 4.2 关键决策

| 决策 | 理由 |
|------|------|
| 图片走 HTTP body，不存盘 | PIL 直接从 `BytesIO` 读，每请求零磁盘 I/O（YOLO/OCR 均内存直传，见 §5.2）；模型权重文件为启动时一次性落盘例外 |
| `gc.collect()` per request | PaddleOCR 长周期压测已知内存泄漏，手动回收 |
| 模型预热在 lifespan | Ultralytics 首次 load 可能 5-10s，预热避免首次调用超时 |
| `OMP_NUM_THREADS=4`（模块顶部、库 import 之前） | 防止 AI 推理抢占 C# 控制算力；OpenMP 线程数在 numpy 导入时固化，之后设置无效。PythonVisionService 启动进程时注入环境变量作双保险 |
| evidence schema 不变 | 复用 `uniclaw.localVisionEvidence.v1`，`fusion.py` 零改动；新增 `scrollHints` 字段 |
| `scrollHints` 只含原始值 | Python 不做滚动判断——`totalCandidates`、`candidatesNearBottom`、`scrollbarDetected`，判断在 C#。`candidatesNearBottom` 阈值读自共享 `label-mapping.json` 的 `spatial.edgeThreshold`（单点真源，见 §4.5） |
| timing 不进入 JSON body | 视觉 API 职责是"看到什么"；timing 走 `Server-Timing` header，按需消费 |
| `X-Uniclaw-Trace-Id`/`X-Uniclaw-Step-Id` 协议预留 | v1 无注入源（IModelProvider 签名不含 trace context），C# 不发送；Python 透传 + echo 进 evidence metadata 供日志关联；启用时机：per-call context 落地后 |

### 4.3 启动命令

```bash
# Unix (macOS/Linux) — UDS
uvicorn server:app --uds /tmp/uniclaw-vision.sock

# Windows — TCP
uvicorn server:app --host 127.0.0.1 --port 8765
```

### 4.4 新增依赖 (`requirements.txt`)

```
fastapi
uvicorn[standard]
# 已有: ultralytics, paddleocr, pillow
```

### 4.5 Evidence scrollHints 字段

Python 只返回原始可观测值，**不做滚动判断**。判断逻辑在 C# `LocalVisionProvider` 中（见 §6.2 Step 3）。

```json
{
  "candidates": [...],
  "scrollHints": {
    "totalCandidates": 12,
    "candidatesNearBottom": 3,
    "scrollbarDetected": true
  }
}
```

| 字段 | 类型 | 含义 |
|------|------|------|
| `totalCandidates` | int | YOLO 检测到的交互元素总数 |
| `candidatesNearBottom` | int | 中心点 Y > `spatial.edgeThreshold`（label-mapping.json，默认 0.92）的候选数 |
| `scrollbarDetected` | bool | YOLO 是否检测到 scrollbar 控件 |

C# 侧判断逻辑：
- `has_scroll`: `totalCandidates > estimatedVisibleCapacity` 或 `scrollbarDetected`
- `is_end_of_list`: `candidatesNearBottom == 0`
- `estimatedVisibleCapacity = image_height / avgItemHeight`，`avgItemHeight` = 候选框高度中位数（`boundsPx` y2-y1）
- 无候选（`totalCandidates == 0`）→ **不确定方向：** `has_scroll: true`、`is_end_of_list: false`（尝试滚动，由引擎 seen-set 差分兜底；识别空 ≠ 已到底）

## 5. ROI 裁剪 + 多线程 OCR (`backends.py`)

### 5.1 动机

当前 `run_paddle_ocr(full_image)` 对整张 1080P 图做 OCR（~800ms），然后 `fuse_evidence` 用空间距离把 OCR token 匹配回 YOLO 框。两个问题：

- **慢**：大部分 OCR 结果（状态栏小字、导航栏）不在 YOLO 检测框内，白算
- **多一层匹配**：IoU + 距离计算是纯开销，且会因 OCR 坐标偏移产生误匹配

改为：YOLO 检测后立即裁剪每个检测框区域 → 只对裁剪区做 OCR → 自动关联。

### 5.2 零拷贝数据流

YOLO 输出 box 坐标直接作为裁剪指针，中间无文件落盘、无 HTTP、无序列化：

```
image (PIL, 内存)
  │
  ├─► YOLO 输出 box 坐标 (float[4] × N)
  │      │
  │      └─► 每个 box 坐标直接作为裁剪指针 → image.crop(x1,y1,x2,y2)
  │              │
  │              ├─ Thread-1: np.asarray(crop) → _get_ocr().predict() → tokens
  │              ├─ Thread-2: np.asarray(crop) → _get_ocr().predict() → tokens
  │              └─ ...
  │
  └─► List[List[OcrToken]] 直接喂 fuse_evidence_from_crops()
```

唯一的"拷贝"是 `image.crop()` 从原图提取子区域——OCR 模型需要独立的像素缓冲区，语义必需。

### 5.3 `run_ocr_on_crops` 实现

```python
# backends.py 新增（import 合并进文件顶部现有导入；新增 numpy）
import os
import threading
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any

import numpy as np

from paddleocr import PaddleOCR
from PIL import Image

from .schema import Box, Detection, OcrToken

# ── YOLO 内存推理（零磁盘）──────────────────────────────────
_yolo_model_cache: dict[str, Any] = {}


def _get_yolo_model(model_path: str) -> Any:
    try:
        from ultralytics import YOLO
    except ImportError as exc:
        raise RuntimeError(
            "ultralytics is not installed. Install tools/local_vision/requirements.txt."
        ) from exc
    if model_path not in _yolo_model_cache:
        _yolo_model_cache[model_path] = YOLO(model_path)
    return _yolo_model_cache[model_path]


def run_yolo_on_image(
    image: Image.Image,
    *,
    model_path: str,
    image_size: int,
    confidence: float,
    device: str,
) -> list[Detection]:
    """PIL Image 内存推理（零磁盘），模型模块级缓存复用。

    ultralytics `predict(source=...)` 原生接受 PIL Image，内部按 RGB 处理。
    """
    results = _get_yolo_model(model_path).predict(
        source=image, imgsz=image_size, conf=confidence,
        device=device, verbose=False)
    detections: list[Detection] = []
    for result in results:
        names = result.names
        boxes = result.boxes
        if boxes is None:
            continue
        for box in boxes:
            xyxy = [float(v) for v in box.xyxy[0].tolist()]
            cls = int(box.cls[0].item())
            conf = float(box.conf[0].item())
            raw_label = str(names.get(cls, cls))
            detections.append(
                Detection(
                    id=f"det_{len(detections) + 1}",
                    label=normalize_yolo_label(raw_label),
                    confidence=conf,
                    box=Box.from_list(xyxy),
                )
            )
    return detections


# ── ROI 裁剪 + 多线程 OCR（零磁盘）──────────────────────────
_ocr_local = threading.local()


def _get_ocr(language: str = "ch") -> Any:
    """每个线程懒加载独立的 PaddleOCR 实例（线程安全）。"""
    if not hasattr(_ocr_local, "instance"):
        _ocr_local.instance = _create_paddle_ocr(PaddleOCR, language)
    return _ocr_local.instance


def warmup_ocr(language: str = "ch") -> None:
    """预热 OCR：PaddleOCR 构造即加载检测/识别模型（首次 2-5s）。"""
    _get_ocr(language)


def run_ocr_on_crops(
    image: Image.Image,
    detections: list[Detection],
    *,
    language: str = "ch",
    padding: int = 4,
    max_workers: int | None = None,
) -> list[list[OcrToken]]:
    """对每个 YOLO 检测框区域做 OCR。返回与 detections 对齐的 token 列表。"""
    if not detections:
        return []

    workers = max_workers if max_workers is not None else _ocr_parallelism()

    # Step 1: 并行裁剪 (PIL 线程安全, CPU 轻量)
    with ThreadPoolExecutor(max_workers=workers) as pool:
        crops = list(pool.map(
            lambda d: _crop_padded(image, d.box, padding),
            detections,
        ))

    # Step 2: 并行 OCR (每线程独立 PaddleOCR 实例, C++ 推理时 GIL 释放 → 真并行)
    with ThreadPoolExecutor(max_workers=workers) as pool:
        results = list(pool.map(
            lambda pair: _ocr_one_crop(pair[0], pair[1], language),
            [(crop, det) for crop, det in zip(crops, detections)
             if crop is not None],
        ))

    # 重建与 detections 对齐的结果列表
    aligned: list[list[OcrToken]] = []
    idx = 0
    for crop in crops:
        if crop is None:
            aligned.append([])
        else:
            aligned.append(results[idx])
            idx += 1
    return aligned


def _ocr_one_crop(
    crop: Image.Image,
    detection: Detection,
    language: str,
) -> list[OcrToken]:
    """对单个裁剪区域运行 OCR，token 坐标回原图。"""
    if crop.width < 4 or crop.height < 4:
        return []
    ocr = _get_ocr(language)
    tokens = _run_ocr_on_pil(ocr, crop)
    return [_offset_token(t, detection.box.x1, detection.box.y1) for t in tokens]


def _ocr_parallelism() -> int:
    """从环境变量读取 OCR 并行度，默认 2。"""
    env = os.environ.get("UNICLAW_OCR_PARALLEL", "2")
    try:
        n = int(env)
        return max(1, min(n, 8))
    except ValueError:
        return 2


def _crop_padded(
    image: Image.Image,
    box: Box,
    padding: int,
) -> Image.Image | None:
    x1 = max(0, int(box.x1) - padding)
    y1 = max(0, int(box.y1) - padding)
    x2 = min(image.width, int(box.x2) + padding)
    y2 = min(image.height, int(box.y2) + padding)
    if x2 <= x1 or y2 <= y1:
        return None
    return image.crop((x1, y1, x2, y2))


def _run_ocr_on_pil(ocr: Any, crop: Image.Image) -> list[OcrToken]:
    """对 PIL Image 运行 PaddleOCR。

    ndarray 直传（paddleocr 2.8 `ocr.ocr` 的 `_check_img` 原生支持 ndarray）
    → 每请求零磁盘；文件路径仅作旧 API / 未知版本 fallback。
    """
    try:
        raw = _call_paddle_ocr(ocr, np.asarray(crop)[:, :, ::-1])
    except (TypeError, ValueError):
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as f:
            crop.save(f, format="PNG")
            tmp_path = f.name
        try:
            raw = _call_paddle_ocr(ocr, Path(tmp_path))
        finally:
            os.unlink(tmp_path)
    return _normalize_paddle_result(raw)


def _offset_token(token: OcrToken, dx: float, dy: float) -> OcrToken:
    """crop 坐标系 token → 原图坐标系（回移裁剪偏移）。"""
    return OcrToken(
        id=token.id,
        text=token.text,
        confidence=token.confidence,
        box=Box(token.box.x1 + dx, token.box.y1 + dy,
                token.box.x2 + dx, token.box.y2 + dy),
    )
```

**配套改动**（backends.py 内既有函数）：
- `_call_paddle_ocr(ocr, source: Path | np.ndarray)`：入口处 `Path → str` 归一化（paddleocr 2.8 的 `_check_img` 只接受 `str`/`ndarray`，不接受 `Path` 对象），ndarray 原样直传；内部调用链不变（`ocr.ocr(...)` → `ocr.predict(...)` fallback）。
- `np.asarray(crop)[:, :, ::-1]`：PIL 是 RGB，paddleocr 模型训练/推理按 BGR（`cv2.imread` 语义）——显式翻转与文件路径路径逐像素一致，避免 ndarray 直传的颜色通道偏差。
- `run_yolo`（CLI 整图版）保留原签名，与 `run_yolo_on_image` 共享 `_get_yolo_model` 缓存。

### 5.4 为什么是多线程而非多进程

| | 共享 PaddleOCR 实例 | 每线程独立实例（本方案） | 多进程 |
|---|---|---|---|
| 线程安全 | ❌ C++ 内部状态冲突 → crash | ✅ 实例隔离 | ✅ 进程隔离 |
| 并行 | ❌ | ✅ C++ 推理时 GIL 释放 | ✅ |
| 内存 | ~300MB | ~600MB (2 workers) ~1.2GB (4 workers) | ~1.2GB + IPC 开销 |
| 复杂度 | — | `threading.local()` 几行代码 | `multiprocessing` + pickle 序列化 |

PaddleOCR 底层 C++ 不是线程安全的（[GitHub Issue #16238](https://github.com/PaddlePaddle/PaddleOCR/issues/16238)），但 `threading.local()` 每线程独立实例完全规避这个问题。C++ 推理时 GIL 释放，各线程真正并行。

### 5.5 性能对比

| 场景 | 当前 (全图 OCR + 空间匹配) | ROI 串行 | ROI 并行 (2 workers) | ROI 并行 (4 workers) |
|---|---|---|---|---|
| 12 个检测框 | ~800ms OCR + ~5ms 匹配 | ~120ms (12×10ms) | ~70ms | ~40ms |
| 25 个检测框 | ~800ms OCR + ~12ms 匹配 | ~250ms | ~140ms | ~75ms |
| 空间匹配 | 需要 | 不需要 (0ms) | 不需要 (0ms) | 不需要 (0ms) |

`UNICLAW_OCR_PARALLEL` 环境变量控制并行度，默认 2（平衡内存和速度）。

### 5.6 `fuse_evidence_from_crops` 简化

`fusion.py` 新增 `fuse_evidence_from_crops`，与现有 `fuse_evidence` 并行保留（CLI 模式仍用全图 OCR）：

```python
def fuse_evidence_from_crops(
    detections: list[Detection],
    crops_ocr: list[list[OcrToken]],
    *,
    image_width: int,
    image_height: int,
    promote_unmatched_ocr: bool = False,
) -> dict[str, Any]:
    """
    YOLO 框 + 裁剪 OCR 结果直接融合。
    无需空间匹配——每个 crop 的 OCR token 已自动关联对应 YOLO 框。
    """
```

核心变化：`for detection, tokens in zip(detections, crops_ocr)` — 直接关联，`_match_score` 调用删除。`_apply_chevron_heuristic` 保留（同行 text_block → menu_item 重分类仍需）。

## 6. Label Mapping 配置

### 6.1 配置文件: `tools/local_vision/label-mapping.json`

```json
{
  "schema": "uniclaw.labelMapping.v1",
  "mappings": {
    "button":    "menu_item",
    "list_item": "menu_item",
    "tab":       "menu_item",
    "icon":      "menu_item",
    "toolbar":   "menu_item",
    "back":      "menu_item",
    "switch":    "toggle",
    "checkbox":  "toggle",
    "input":     "input",
    "slider":    "slider",
    "text_block": "text"
  },
  "nonItemLabels": ["popup"],
  "spatial": {
    "level1MaxY": 0.08,
    "edgeThreshold": 0.92
  }
}
```

### 6.2 加载与校验

```csharp
// LocalVisionProvider 构造期
var path = configPath
    ?? Environment.GetEnvironmentVariable("UNICLAW_LABEL_MAPPING")
    ?? "tools/local_vision/label-mapping.json";

var json = File.ReadAllText(path);
_config = JsonSerializer.Deserialize<LabelMappingConfig>(json, DomainJsonOptions.Default)
    ?? throw new DomainValidationException("labelMapping", null, "config null or invalid.");

// fail-fast: 每个 mapping value 必须通过 ElementTypeMapper 校验
foreach (var (_, aiType) in _config.Mappings)
{
    if (!ElementTypeMapper.IsValidType(aiType))
        throw new DomainValidationException("labelMapping", aiType,
            $"'{aiType}' is not a recognized AI type.");
}
```

**关键决策：**

- **构造期 fail-fast**。全量校验 mapping 值 → 配置错误不等到运行时才发现。
- **`spatial` 参数可调**。不同车机屏幕比例可能需要不同的 `level1MaxY`（顶部 tab 栏 Y 阈值）和 `edgeThreshold`（边缘贴附阈值）。
- **`nonItemLabels`**。`popup` 只设置 `is_popup` 标志，不进入 items 数组。
- **缺失 label fallback**。YOLO label 不在 mapping 表中 → 默认 `text`（`ElementTypeMapper` 合法类型，非 `info`——`info` 不在 `IsValidType` 集合内），记录 warning 日志。
- **路径可覆盖**。`UNICLAW_LABEL_MAPPING` 环境变量或构造器参数。

## 7. LocalVisionProvider : IModelProvider

### 7.1 接口实现

放独立项目 `src/UniClaw.LocalVisionProvider/LocalVisionProvider.cs`（**不是** `Core/UniBrain/`）。

> 与代码库既有模式一致：`AnthropicModelProvider`（UniClaw.ClaudeProvider）、`DeepSeekModelProvider`（UniClaw.DeepSeekProvider）均在独立程序集，`UniBrainFactory` 只接收 Host 装配好的 `IReadOnlyDictionary<string, IModelProvider>`（"避免 Core 依赖传输层细节"）。Core 保持"纯逻辑、零 I/O"边界真正成立。

```
实现 IModelProvider:
  CompleteVisionAsync     → HTTP → Python → evidence → PageAnalysisDto JSON → ModelResponse
  CompleteTextAsync       → NotImplementedException
  CompleteMultimodalAsync → NotImplementedException
```

### 7.2 映射管道（4 步）

```
candidates[] + scrollHints      ──────►  PageAnalysisDto JSON
─────────────────────────                ────────────────────
type: "switch"   ── Step 1: YOLO → type
text: "Wi-Fi"
center: (0.5,0.15)                      items: [
                                          { name:"Wi-Fi", type:"toggle",
                                            coordinate:{x:0.5,y:0.15} },
                                        ]

type: "tab"      ── Step 2: Y 轴聚类 → menus
center: (0.15,0.05)                     level1_menus: [{...}]
type: "tab"                             level1_dir: "horizontal"
center: (0.35,0.05)

scrollHints{}   ── Step 3: scroll 判断
  totalCandidates: 12                   has_scroll: true
  candidatesNearBottom: 3               is_end_of_list: false
  scrollbarDetected: true

type:"popup"     ── Step 4: popup 检测    is_popup: true
```

**Step 1 — YOLO label → AI type 映射**：查 `LabelMappingConfig.Mappings`。默认表见 §6.1。

**Step 2 — Y 轴聚类 → menu 结构**：
- `center.y < level1MaxY` 的候选 → `level1_menus`
- 有 level1_menus 且横向布局（X 方差 > Y 方差）→ `level1_dir: "left"` / `"right"`（按候选 X 均值判定侧向）
- **无 level1_menus → `level1_dir: null`**（PageAnalyzer 回落 Direction.Left，既有行为；不发明 top/bottom 规则）
- `level1_menus` 每项 `active` 省略 → false（YOLO 无法推断选中态，诚实缺省）
- items 的 `parent`：**v1 恒 null**（契约可选字段；box 包含推断不可靠且引擎无消费者，延后）
- 其余 → `items`（含 `type` 映射值 + `name`/`coordinate`）
- `level2_dir`/`level2_menus`：本版本输出 null/空数组（无二级菜单概念）

> ⚠️ 契约事实（对照 `PageAnalyzer.PageAnalysisDto` + `Schemas.cs`）：`level1_dir` 合法值只有 `left|right|top|bottom`（**没有 "horizontal"/"vertical"**）；`current_path` 是**数组**（`List<string>`）；items 字段为 `name/type/coordinate/parent`（`active` 只属于 menus）。

**Step 3 — scroll 检测（门禁保守化，最终结论交给引擎时序差分）**：
- `totalCandidates > estimatedVisibleCapacity` 或 `scrollbarDetected` → `has_scroll: true`
- **不确定 → 偏向"可滚动"**：`is_end_of_list` 单帧**不轻易为 true**；无候选/识别空 → `has_scroll: true`、`is_end_of_list: false`（放行 swipe，避免提前终止遍历）
- `estimatedVisibleCapacity = image_height / avgItemHeight`；`avgItemHeight` = 候选框高度中位数（`boundsPx` y2-y1）
- **"到底"由引擎已有时序机制确认**：`InterceptionHandler.TryHandleScrollAsync` 在 swipe 后做 seen-set 差分（`GetElementIds` 用 `item.Name`，local-vision 的 OCR 文本天然可作指纹）——差分无新增 → 才结束帧。单帧门禁（`HasScroll/IsEndOfList`）只负责"是否值得尝试一次 swipe"

**Step 4 — popup 检测**：
- 存在 type 为 `popup` 的检测框 → `is_popup: true`，提取最近的 close 候选作为 `close_button`

**序列化 DTO**：`PageAnalyzer.PageAnalysisDto` 是 PageAnalyzer 的私有嵌套类型，Provider 必须自持同形状的序列化 DTO。多词键必须显式 `[JsonPropertyName]` 锚定（`level1_dir`/`level1_menus`/`has_scroll`/`is_end_of_list`/`is_popup`/`close_button`/`back_button`/`popup_info`/`current_path`）——`DomainJsonOptions.CamelCase` 仅对单词属性生效，不锚定会静默回落 default（PageAnalyzer 已踩过此坑）。`level1_menus` 项含 `name`/`coordinate`/`active`。

### 7.3 ModelResponse 构造

```csharp
public async Task<ModelResponse> CompleteVisionAsync(
    ModelRequest request, byte[] imageData, CancellationToken ct = default)
{
    var sw = Stopwatch.StartNew();

    var content = new ByteArrayContent(imageData);
    content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
    // v1 不发送 X-Uniclaw-Trace-Id/Step-Id（无注入源，协议预留，见 D-14）

    var httpResp = await _httpClient.PostAsync("/v1/analyze", content, ct);
    sw.Stop();
    if (!httpResp.IsSuccessStatusCode)
    {
        // graceful 失败语义（与 AnthropicModelProvider 一致）：不抛，返回 Success=false，由调用方重试
        return new ModelResponse(Content: "", ProviderId: "local-vision",
            Mode: "vision", InputTokens: 0, OutputTokens: 0,
            LatencyMs: sw.Elapsed.TotalMilliseconds, Success: false);
    }

    var evidenceJson = await httpResp.Content.ReadAsStringAsync(ct);
    var evidence = JsonSerializer.Deserialize<LocalVisionEvidence>(
        evidenceJson, DomainJsonOptions.Default);

    // 解析 Server-Timing → 写入 trace 子 span
    if (httpResp.Headers.TryGetValues("Server-Timing", out var timings))
        WriteTimingSpans(timings);

    var dto = MapToPageAnalysisDto(evidence);

    return new ModelResponse(
        Content: JsonSerializer.Serialize(dto, DomainJsonOptions.Default),
        ProviderId: "local-vision",
        Mode: "vision",
        InputTokens: 0,
        OutputTokens: 0,
        LatencyMs: sw.Elapsed.TotalMilliseconds,
        Success: true);
}
```

### 7.4 与 PageAnalyzer 的兼容性

`PageAnalyzer` 不动——它不关心 provider 是云端还是本地：

1. `_screenCapture.CaptureAsync()` → `byte[]`
2. `_promptLibrary.GetTemplate(AnalyzeVisual)` → prompt（本地 provider 忽略）
3. `_modelProvider.CompleteVisionAsync(req, bytes)` → `ModelResponse.Content` (JSON)
4. `JsonSerializer.Deserialize<PageAnalysisDto>(json)` → 经 `ElementTypeMapper` 派生 → `PageAnalysis`

关键：Step 1 的 YOLO label → AI type 映射在 provider 内部完成，输出的是 AI 兼容的 type 字符串（`menu_item`、`toggle`、`input`...），所以 `ElementTypeMapper.IsValidType()` 和 `ToMenuItemType()` 照常工作。

### 7.5 VisionScreenStateProvider : IScreenStateProvider

放 `src/UniClaw.Core/Traversal/VisionScreenStateProvider.cs`（与 `IScreenStateProvider` 同目录）。**不能放 `UniBrain/`**：`UniBrainGuardTests.UniBrain_DoesNotReferenceTraversal`（ArchitectureGuardTests）禁止 UniBrain 目录引用 `UniClaw.Core.Traversal`，而实现 `IScreenStateProvider` 必然 `using UniClaw.Core.Traversal`——放 UniBrain 会触发 CI 阻塞。`PageAnalysis` 属 `Domain.Models.Content`，Traversal 引用它无 Guard 冲突（`Traversal_ReferencesUniBrainForIUniBrain` 仅限制具体类型 `UniBrainService`）。

local-vision 没有 UIAutomator，无法用 `AdbScreenStateProvider`。但 `InterceptionHandler.TryHandleScrollAsync` 需要 `IScreenStateProvider.HasScroll()` / `IsEndOfList()` 做快速门禁。

`VisionScreenStateProvider` 是一个 **薄包装**——从已分析的 `PageAnalysis` 读取滚动字段，不依赖 UIA：

```csharp
public sealed class VisionScreenStateProvider : IScreenStateProvider
{
    private readonly Func<PageAnalysis?> _getCurrentAnalysis;

    public VisionScreenStateProvider(Func<PageAnalysis?> getCurrentAnalysis)
    {
        _getCurrentAnalysis = getCurrentAnalysis
            ?? throw new ArgumentNullException(nameof(getCurrentAnalysis));
    }

    public bool HasScroll() =>
        _getCurrentAnalysis()?.HasScroll ?? false;

    public bool IsEndOfList() =>
        _getCurrentAnalysis()?.IsEndOfList ?? true;

    // local-vision 不支持 UIA fingerprint 快速路径
    // → InterceptionHandler 自动走 AI seen-set 差分路径
    public double GetScrollProgress() => 0.0;
    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
}
```

**与 `InterceptionHandler` 的兼容性**：

`InterceptionHandler.TryHandleScrollAsync` 有三条路径（不动）：

```
Line 438: ctx.ScreenState.HasScroll() / IsEndOfList()
          → VisionScreenStateProvider 从 PageAnalysis 回答 ✓

Line 451: if (ctx.ScreenState is IObservableScreenStateProvider)
          → VisionScreenStateProvider 不实现此接口
          → 跳过 UIA fingerprint 快速路径 ✓

Line 476: else → AI seen-set 差分
          → 全量重新分析，回退到安全路径 ✓
```

`VisionScreenStateProvider` 不实现 `IObservableScreenStateProvider`，所以 `InterceptionHandler` 自动跳过 UIA fingerprint 快速路径，走 seen-set 差分 + 全量重分析的安全路径。

## 8. PythonVisionService 进程管理

### 8.1 接口

放 `src/UniClaw.Device/IPythonVisionService.cs`。

```csharp
public interface IPythonVisionService : IAsyncDisposable
{
    HttpClient HttpClient { get; }
    Task StartAsync(CancellationToken ct = default);
    bool IsRunning { get; }
}
```

### 8.2 实现

放 `src/UniClaw.Device/PythonVisionService.cs`。

**UDS/TCP 双模式 HttpClient 工厂**：

| 平台 | 模式 | 地址 |
|------|------|------|
| macOS / Linux | HTTP over UDS | `/tmp/uniclaw-vision.sock` |
| Windows | TCP loopback | `127.0.0.1:8765` |

UDS 实现：`SocketsHttpHandler.ConnectCallback` → `new UnixDomainSocketEndPoint(socketPath)`。

**生命周期状态机**：

```
Stopped
  │ StartAsync()
  ▼
Starting ── health check OK ──► Running
  │                                │
  │ health check 超时              │ process.Exited
  │ (retry < maxRestarts)          │ (自动拉起, 退避)
  │                                │
  └──► (重试)                      ▼
                             Starting (backoff)
                                   │
                                   │ restartCount > maxRestarts
                                   ▼
                                 Stopped (放弃)

DisposeAsync():
  Running/Starting
    │ Kill(entireProcessTree: true)
    ▼
  Stopped
```

**自动拉起退避序列**：

| 尝试 | 延迟 | 说明 |
|------|------|------|
| 1 | 0ms | 即时重连（可能只是进程瞬时退出） |
| 2 | 500ms | 短退避 |
| 3 | 1s | |
| 4 | 3s | |
| 5+ | 10s | 上限，超过 `maxRestarts`（默认 5）放弃 |

## 9. Acceptance Criteria

### 9.1 硬性验收（单元测试全绿）

| # | 标准 | 验证方式 |
|---|---|---|
| V1 | `label-mapping.json` 反序列化成功，`"switch"` → `"toggle"`，`"button"` → `"menu_item"` | C# 单元测试 |
| V2 | 配置中 mapping value 非法 → 构造期 `DomainValidationException` | C# 单元测试 |
| V3 | 未知 YOLO label → 默认 `text`，记录 warning 日志 | C# 单元测试 |
| V4 | Mock evidence（12 个 candidate）→ `MapToPageAnalysisDto` 输出合法 `PageAnalysisDto`，含 `items`、`level1_menus` | C# 单元测试 |
| V5 | Y<0.08 的候选 → `level1_menus`；其余 → `items` | C# 单元测试 |
| V6 | `scrollHints.totalCandidates=15, scrollbarDetected=true` → `has_scroll: true` | C# 单元测试 |
| V7 | `scrollHints.candidatesNearBottom=0` → `is_end_of_list: true` | C# 单元测试 |
| V8 | `VisionScreenStateProvider` 从 `PageAnalysis.HasScroll=true` → `HasScroll()` 返回 `true` | C# 单元测试 |
| V9 | `VisionScreenStateProvider` 不实现 `IObservableScreenStateProvider`（反射断言） | C# 单元测试 |
| V10 | Python `server.py` 导入无错误，`GET /health` 返回 `{"status": "ok"}` | Python 单元测试 |
| V11 | Python 已知截图 → `POST /v1/analyze` → evidence `candidates` 非空，每个含 `type`/`text`/`center`/`bounds`/`confidence` | Python 单元测试（已有测试图片） |
| V12 | `scrollHints` 字段存在，`totalCandidates` > 0 | Python 单元测试 |
| V13 | Response header 含 `Server-Timing`，格式 `yolo;dur=..., ocr;dur=..., fusion;dur=...` | Python 单元测试 |
| V14 | `run_ocr_on_crops` 输入 3 个 mock detections → 返回 3 个 token 列表，坐标已 offset 到原图 | Python 单元测试 |
| V15 | `fuse_evidence_from_crops` 输入 detections + crops_ocr → candidates 数量 = detections 数量 | Python 单元测试 |
| V16 | `ArchitectureGuardTests` 全绿 | `dotnet test --filter ArchitectureGuard` |
| V17 | `Core` 项目不引用 `Process`、无 `PythonVisionService` 的 using | using 检查 |
| V18 | `Device` 项目不引用 `PageAnalysisDto`、`ElementTypeMapper`、`IModelProvider` | using 检查 |
| V19 | Provider 自持序列化 DTO 输出 JSON，snake_case 多词键（`level1_dir`/`has_scroll`/`is_end_of_list`/`is_popup`/`close_button`）与 `PageAnalyzer` 反序列化契约一致 | C# 单元测试 |
| V20 | `run_yolo_on_image` 接受 PIL Image 内存推理（零磁盘），模型模块级缓存复用 | Python 单元测试 |
| V21 | `_run_ocr_on_pil` ndarray 直传零临时文件；`_call_paddle_ocr` 兼容 `Path` 与 `ndarray` | Python 单元测试 |
| V22 | `candidatesNearBottom` 阈值读自 `label-mapping.json` `spatial.edgeThreshold`（单点真源） | Python 单元测试 |

### 9.2 集成验收（emulator-gated，不阻塞合入）

| # | 标准 |
|---|---|
| I1 | `PythonVisionService.StartAsync` → 进程启动 + 健康检查通过 |
| I2 | `PythonVisionService` 进程异常退出 → 自动拉起（退避序列验证） |
| I3 | Python 未启动时 `LocalVisionProvider` 调 HTTP → `HttpRequestException` → `PageAnalyzer` 重试后抛 `DomainValidationException` |

### 9.3 性能指标（待办，不阻塞合入）

> 记入 `docs/validation/unit_test_status.md` backlog。

| # | 标准 | 目标 | 验证方式 |
|---|---|---|---|
| P1 | 单次 `/v1/analyze` 延迟（12 个检测框，`UNICLAW_OCR_PARALLEL=2`） | < 200ms | benchmark 脚本 |
| P2 | `run_ocr_on_crops` 延迟（12 个框，2 workers） | < 100ms | Python benchmark |
| P3 | `LocalVisionProvider` HTTP 往返延迟计入 `ModelResponse.LatencyMs` | 非零 | C# 单元测试（已有 V4 mock 覆盖） |
| P4 | 连续 100 次请求后 Python 进程内存增长 | < 20% | 压测脚本 |

## 10. Error Handling

| 故障 | 处理 |
|------|------|
| Python 进程启动失败 | `StartAsync` → `InvalidOperationException` |
| 健康检查超时（30s） | `StartAsync` → `TimeoutException` |
| 进程异常退出 | `OnProcessExited` → 自动拉起（退避 + 上限） |
| HTTP 请求失败 | `LocalVisionProvider` → `HttpRequestException` → `PageAnalyzer` 重试（已有 `MaxAnalyzeAttempts=2`） |
| 配置映射表 value 非法 | 构造期 `DomainValidationException` fail-fast |
| YOLO label 不在映射表 | 默认 `text` + warning 日志 |
| PaddleOCR 线程崩溃 | 单线程 crash 不影响其他线程；该 crop 返回空 token 列表 |

## 11. Decisions

| ID | 决策 | 理由 |
|----|------|------|
| D-1 | 映射逻辑放 C# 侧，Python 只返回 evidence | `ElementTypeMapper` 是 C# 单点真源；页面推理逻辑可 xUnit 覆盖 |
| D-2 | 映射配制成 JSON 文件 | 不同车机 YOLO label 可能不同，无需改代码 |
| D-3 | UDS (Unix) / TCP (Windows) 双模式 | macOS 开发 + Windows 部署，走操作系统原生最优路径 |
| D-4 | `gc.collect()` per request | PaddleOCR 已知内存泄漏，手动回收 |
| D-5 | 自动拉起有上限 | 无限重连 = 死循环，比快速失败更危险 |
| D-6 | 构造期 fail-fast 校验 label mapping | 配置错误不等到运行时 |
| D-7 | `CompleteTextAsync` / `CompleteMultimodalAsync` → NotImplementedException | 本地视觉不做文本推理 |
| D-8 | 滚动检测复用 `IScreenStateProvider`，不新增接口 | `ScrollableMockVisionService` 已有 `IPageAnalyzer + IScreenStateProvider` 同体模式 |
| D-9 | `VisionScreenStateProvider` 不实现 `IObservableScreenStateProvider` | 自动走 `InterceptionHandler` 的 seen-set 差分安全路径 |
| D-10 | Python 只返回 `scrollHints` 原始值，判断逻辑在 C# | Python 不做业务判断；C# 可 xUnit 测试滚动逻辑 |
| D-11 | ROI 裁剪 + 多线程 OCR（`threading.local()`） | 零空间匹配开销；C++ 推理时 GIL 释放真并行；默认 2 workers 平衡内存 |
| D-12 | `run_ocr_on_crops` 不替换 `run_paddle_ocr` | CLI 模式仍用全图 OCR；server 模式用 ROI 路径 |
| D-13 | timing 走 `Server-Timing` header，不进 JSON body | W3C 标准；视觉 API 职责是"看到什么"；C# 按需解析 |
| D-14 | `X-Uniclaw-Trace-Id` / `X-Uniclaw-Step-Id` 协议预留（v1 不发送） | 无注入源（签名不含 trace context）且观察链已有承载（ObservingModelProvider/AICallRecord）；Python 透传 + echo 进 metadata；待 per-call context 落地后启用 |
| D-15 | 模块分层：Core（逻辑）/ Device（I/O 适配）/ Python（推理引擎） | 依赖单向，无循环引用；Core 零直接 I/O 依赖 |
| D-16 | `run_yolo_on_image` + 模块级模型缓存；`run_yolo`（CLI）保留并共享缓存 | server 模式零磁盘 + 预热生效；CLI 行为不变 |
| D-17 | `_call_paddle_ocr` 支持 `Path \| np.ndarray`（Path 归一化为 str，ndarray 直传） | paddleocr 2.8 `_check_img` 原生支持 ndarray；消除每 crop 临时文件 |
| D-18 | `OMP_NUM_THREADS` 在模块顶部、任何库 import 之前设置 | OpenMP 线程数在库初始化时固化，之后设置无效 |
| D-19 | `candidatesNearBottom` 阈值读自共享 `label-mapping.json` `spatial.edgeThreshold` | 与 C# 单点真源，消除双阈值分歧 |
| D-20 | `VisionScreenStateProvider` 放 `Traversal/` 而非 `UniBrain/` | UniBrainGuardTests 禁止 UniBrain 引用 Traversal；PageAnalysis 属 Domain，无冲突 |

## 12. 审阅修订（2026-08-03，对抗审阅后采纳）

> 以下修订来自一次对抗审阅（10 条意见）。正文已同步修正的标注 ✔；仅本节省略正文改动的标注 →。

| ID | 修订 | 正文状态 |
|----|------|---------|
| R-1 | `LocalVisionProvider` 移到独立项目 `UniClaw.LocalVisionProvider`（与 ClaudeProvider/DeepSeekProvider 模式一致），不占 Core | ✔ §7.1 |
| R-2 | 契约修正：`level1_dir` 只输出 `left/right/top/bottom`（横向布局按 X 均值定 left/right）；`current_path` 是数组；items 含 `type`/`parent`；补 `level2_dir`/`level2_menus`（空数组）；menus `active` 默认 false；parent 按 box 包含关系推断 | ✔ §7.2 |
| R-3 | 失败语义 graceful：HTTP 失败 → `Success=false`（与 AnthropicModelProvider 一致），不抛 | ✔ §7.3 |
| R-4 | 滚动门禁保守化：不确定/空识别 → `has_scroll:true, is_end_of_list:false`（放行 swipe）；"到底"由引擎 seen-set 差分确认（`InterceptionHandler` 已实现，用 `item.Name` 作指纹） | ✔ §4.5/§7.2 |
| R-5 | 两类 OCR 加 `scope`：`roi`（框内，可关联 candidate）/ `scroll`（滚动扫描）；`scroll` 永不提升为 candidate（`promote_unmatched_ocr` 保持 False） | **延后（v1.1）**：v1 仅 roi 模式，`token.scope` 字段预留（additive，默认 `"roi"`）。触发条件：真机观测到 YOLO 漏检文本行导致滚动差分失效 |
| R-6 | `metadata` 扩展：`{schema, pipeline:{name,version}, models:{yolo,ocr}, configHash}`（configHash = label-mapping.json SHA-256） | → |
| R-7 | `candidate.confidence` 补 `confidenceDetail:{yolo,ocr}`；文档注明"工程评分，仅排序/过滤/调试，非概率"（C# 契约不含 confidence） | → |
| R-8 | 并发参数可配化：`OMP_NUM_THREADS` 改 env（默认 4）；uvicorn 单 worker 为默认值+理由，非硬约束；`gc.collect()` P95 抖动进 P4 压测 | → |
| R-9 | 生命周期：UDS/TCP 地址 env 可覆盖；自愈前先 health 探测（进程活着即复用，避免重复拉起）；启动前 unlink 残留 socket；`/health` 返回 `warm` 字段，`StartAsync` 就绪门 = warm | → |
| R-10 | 版本分层：schema v1（协议，冻结）+ pipeline/model/config 版本（metadata 追踪，可演进） | → |
| R-11 | trace header 保留为协议预留：v1 C# 不发送（无注入源），Python 透传 + echo 进 metadata（日志关联）；删除 `_traceId`/`_stepSpanId` 实例字段设计（签名不通，无可写方）；启用时机 = per-call context 落地 | ✔ D-14 / §3.1 / §7.3 |
| R-12 | F3 决议：滚动确认改为**连续 N 次差分无新增才到底 + 空帧立即重试（不消耗预算）**。N 进 `ScrollSwipeConfig.MaxEmptyScrollRetries`（**配置化，默认 1** = 共 2 次确认；0 = 恢复现状立即到底）；解析链不变（provider 页级 → run 级 → 默认）。`VisionScreenStateProvider.GetScrollSwipeConfig()` 后续可返回带 N 的配置 | ✔ 不变量 1 |
| R-13 | F1 决议：OCR 线程实例预热改为**模块级长生命周期 ThreadPoolExecutor**（预热注入 dummy 任务，让每个 worker 线程建立 `threading.local` 实例；请求期复用同一 executor）。比"预热覆盖 worker"更优：顺带消除每请求线程池创建 + 实例随线程 GC 的问题 | → |
| R-14 | F2 决议：ROI 裁剪 padding 配置化 —— `label-mapping.json spatial.roiPadding`（x/y 比例 + 像素下限/上限），替换硬编码 4px；label 侧扩展（F4，文本在框外）延后 | → |
| R-15 | D2 决议：items `parent` **v1 恒 null**（契约可选字段，引擎无消费者——仅 DTO 透传，已验证；box 包含推断不可靠，延后） | ✔ §7.2 |
| R-16 | D3 决议：无 level1_menus → `level1_dir: null`（PageAnalyzer 回落 Direction.Left，既有行为）；**删除"纵向 → top/bottom"发明规则** | ✔ §7.2 |
| R-17 | F5 决议：YOLO `confidence` 参数**配置化**（server 从 `label-mapping.json detection.confidence` 读取，替换硬编码 0.35）——一个阈值、一个位置，不新增融合过滤层；riskFlags 保持调试用途 | → |

**新增验收**（并入 §9.1）：

| # | 标准 |
|---|---|
| V23 | 黄金样本契约测试：Provider 输出与 `HostCommands.SettingsAnalysisJson` 结构一致（level1_dir 枚举、current_path 数组、items 含 type） |
| V24 | 空识别（totalCandidates=0）→ `has_scroll:true, is_end_of_list:false` |
| V25 | 横向布局候选 → `level1_dir` 为 `left` 或 `right`（非 horizontal） |
| V26 | HTTP 非 2xx → `ModelResponse.Success=false`（不抛） |
| V27 | `promote_unmatched_ocr=False` 时 OCR-only token 不提升为 candidate（fuse_evidence_from_crops 测试） |

**修订后的不变量**（替换 §9 相关约束）：

1. `PageAnalyzer` 零改动；`InterceptionHandler` 仅允许最小改动：滚动空帧重试 + 连续 N 次无新增才到底（N 配置化，默认 1）
2. Core 无 `Process`、无 PythonVisionService using、无传输层 provider
3. 空结果 ≠ 已到底；不确定偏"尝试滚动"
4. Python 不做跨帧判断（滚动时序由引擎 seen-set 差分承担）
5. C# 不修改原始 evidence（原样进 trace，与 `trace-span-helpers` 配合）
6. OCR-only token 永不提升为 candidate（`promote_unmatched_ocr` 恒 False；R-5 延后期间此约束唯一承载点）
