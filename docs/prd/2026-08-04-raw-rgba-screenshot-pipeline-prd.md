# Raw RGBA Screenshot Pipeline — ADB 裸流直通视觉推理

> 日期: 2026-08-04
> 状态: proposed
> 范围: `src/UniClaw.Device/` (C#) + `src/UniClaw.Core/UniBrain/` (Core) + `src/UniClaw.LocalVisionProvider/` (C#) + `src/UniClaw.Host/Hooks/` (C#) + `tools/local_vision/` (Python)

## 1. Motivation

当前截图链路存在三次不必要的编解码，每次截屏都在设备和主机之间反复压缩/解压：

```
adb exec-out screencap -p  →  device PNG encode (~2-5ms)
  →  C# SKBitmap.Decode(PNG) (~3-8ms)  →  crop/resize  →  JPEG encode (~5-10ms)
  →  POST /v1/analyze (JPEG)  →  Python PIL.Image.open(BytesIO) decode (~5-10ms)
  →  YOLO + OCR
  →  RunAssetHook: 再次 JPEG/PNG encode → 存盘
```

总编解码开销约 15-33ms，且所有中间格式（PNG → JPEG → PIL Image）都是临时态，仅在最终存盘时需要持久化格式。

`adb exec-out screencap`（**不带 `-p`**）直接输出 12 字节 header（width/height/pixel_format，little-endian uint32）+ RGBA 裸像素。利用 PIL `Image.frombytes` 内存包装实现零拷贝直通——**C# 侧只做传输，像素级别的 crop/resize 也归 Python**，因为 PIL 天然是图像处理的正确位置，且 Python 已经在做 `convert("RGB")`，预处理统一在一处。

```
adb exec-out screencap（无 -p）→ raw RGBA (12B header + pixels)
  → C# 解析 header → 原样转发 raw bytes + width/height（零像素操作）
  → HTTP body 直传（application/octet-stream + dimension headers）
  → Python Image.frombytes('RGBA', w, h, body)（0ms 内存包装）
  → PIL crop/resize（配置从 label-mapping.json 读取）
  → convert("RGB") → YOLO + OCR
  → RunAssetHook: SKBitmap.Encode(PNG) → 存盘（唯一一次编码，全链路仅此处）
```

## 2. Architecture

### 2.1 数据流对比

```
── 旧路径（有损 + 三次编解码）──
Device          C# Core              HTTP              Python               Disk
  │               │                   │                 │                    │
  ├─PNG encode──►├─PNG decode────────┤                 │                    │
  │               ├─crop/resize──────┤                 │                    │
  │               ├─JPEG encode──────►├─/v1/analyze────►├─JPEG decode───────┤
  │               │                   │                 ├─convert("RGB")    │
  │               │                   │                 ├─YOLO+OCR          │
  │               │                   │                 │              PNG encode→before.png

── 新路径（零编解码 + 预处理归 Python + 仅存盘一次编码）──
Device          C# Core              HTTP              Python               Disk
  │               │                   │                 │                    │
  ├─raw RGBA─────►├─header parse─────┤                 │                    │
  │               │  (width/height)  │                 │                    │
  │               │                   ├─/v1/analyze_raw►├─frombytes(0ms)    │
  │               │                   │ X-Image-Width   ├─crop/resize(PIL)  │
  │               │                   │ X-Image-Height  ├─convert("RGB")    │
  │               │                   │ body: raw RGBA  ├─YOLO+OCR          │
  │               │                   │                 │              PNG encode→before.png
```

C# 侧不再做任何像素操作——不建 `SKBitmap`、不 crop、不 resize。`ImageResizer` 不动，旧路径继续使用。

### 2.2 模块分层（改动范围）

```
┌─────────────────────────────────────────────────────────────┐
│  Device (UniClaw.Device)                                    │
│                                                              │
│  IAdbSession  ← 新增 CaptureRawScreenBufferAsync()           │
│  ProcessAdbSession  ← adb exec-out screencap, 解析 12B header│
│  AdvancedSharpAdbSession  ← 同上（经 _binaryRunner）         │
│  AdbScreenCapture  ← 新增 CaptureRawAsync() 委托             │
│  AdbCommandRunner  ← 不动（binary capture 通道已满足）       │
└─────────────────────────────────────────────────────────────┘
          ▲
          │ RawScreenBuffer { Pixels, Width, Height, PixelFormat }
          │
┌─────────────────────────────────────────────────────────────┐
│  Core (UniClaw.Core/UniBrain/)                              │
│                                                              │
│  RawScreenBuffer (新 record struct)                          │
│  IScreenCapture  ← 新增 CaptureRawAsync()                    │
│  PageAnalyzer  ← UNICLAW_RAW_SCREEN_BUFFER 双路径选择         │
│    旧路径: CaptureAsync → ImageResizer.ResizeToMaxWidth →    │
│            CompleteVisionAsync                               │
│    新路径: CaptureRawAsync → CompleteVisionRawAsync           │
│            (不经过 ImageResizer，raw bytes 原样转发)          │
└─────────────────────────────────────────────────────────────┘
          ▲
          │ RawScreenBuffer（原样，未裁剪未缩放）
          │
┌─────────────────────────────────────────────────────────────┐
│  Provider (UniClaw.LocalVisionProvider)                      │
│                                                              │
│  LocalVisionProvider  ← 新增 CompleteVisionRawAsync()         │
│    Content-Type: application/octet-stream                    │
│    Headers: X-Image-Width, X-Image-Height, X-Image-Pixel-Format│
│    Endpoint: POST /v1/analyze_raw                            │
└─────────────────────────────────────────────────────────────┘
          ▲
          │ HTTP (UDS/TCP)
          │
┌─────────────────────────────────────────────────────────────┐
│  Python (tools/local_vision/server.py)                       │
│                                                              │
│  POST /v1/analyze_raw  (新增)                                │
│    width  = int(headers["X-Image-Width"])                    │
│    height = int(headers["X-Image-Height"])                   │
│    image  = Image.frombytes("RGBA", (w, h), body)  ← 0ms    │
│    image  = _preprocess(image)   ← crop + resize (PIL 零解码)│
│    image  = image.convert("RGB")                             │
│    → _run_pipeline(image, w, h)  ← 与 /v1/analyze 共享       │
│                                                              │
│  POST /v1/analyze  (不动)                                    │
│  GET /health  (不动)                                         │
└─────────────────────────────────────────────────────────────┘
```

## 3. 详细设计

### 3.1 新类型: `RawScreenBuffer`

**文件**: `src/UniClaw.Core/UniBrain/RawScreenBuffer.cs`（新文件）

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// Raw screen buffer captured via adb exec-out screencap (no -p).
/// Carries parsed dimensions from the 12-byte Android framebuffer header
/// plus the raw RGBA pixel payload. C# side performs zero pixel operations —
/// crop/resize happen in Python where PIL is the natural image-processing layer.
/// </summary>
public readonly record struct RawScreenBuffer(
    byte[] Pixels,      // width * height * 4 bytes, RGBA_8888
    int Width,
    int Height,
    int PixelFormat     // 1 = RGBA_8888 (Android PIXEL_FORMAT_RGBA_8888)
);
```

### 3.2 ADB 裸流捕获

#### `screencap` 裸流格式（无 `-p`）

```
Byte 0-3:   width       (uint32, little-endian)
Byte 4-7:   height      (uint32, little-endian)
Byte 8-11:  pixel_format (uint32, little-endian, 1 = RGBA_8888)
Byte 12-:   RGBA pixels  (width * height * 4 bytes, row-major)
```

#### `IAdbSession` 新增方法

```csharp
// IAdbSession.cs
Task<RawScreenBuffer> CaptureRawScreenBufferAsync(CancellationToken ct = default);
```

#### `ProcessAdbSession` 实现

```csharp
public async Task<RawScreenBuffer> CaptureRawScreenBufferAsync(CancellationToken ct = default)
{
    var result = await _runner.RunAsync(
        new AdbCommandRequest(
            ImmutableArray.Create("exec-out", "screencap"),  // 无 -p
            CaptureBinaryOutput: true),
        ct);
    ThrowIfCancelled(result, ct);

    if (!result.Succeeded)
        throw new AdbCommandException("ADB raw screencap", ...);

    if (result.BinaryOutput.IsDefaultOrEmpty)
        throw new AdbCommandException("ADB raw screencap", ...);

    var bytes = result.BinaryOutput.AsSpan();
    if (bytes.Length < 12)
        throw new AdbCommandException("ADB raw screencap header too short", ...);

    var width  = BinaryPrimitives.ReadUInt32LittleEndian(bytes[0..4]);
    var height = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]);
    var fmt    = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..12]);

    if (fmt != 1)
        throw new AdbCommandException(
            $"Unsupported pixel format: {fmt} (expected 1 = RGBA_8888)", ...);

    return new RawScreenBuffer(
        Pixels: bytes[12..].ToArray(),
        Width: (int)width,
        Height: (int)height,
        PixelFormat: (int)fmt);
}
```

`AdvancedSharpAdbSession` 实现完全相同，经已有 `_binaryRunner` 执行。

**C# 侧到此为止——不再碰像素。** 以下所有图像处理在 Python 侧完成。

### 3.3 `IScreenCapture` 新增方法 + `PageAnalyzer` 双路径

`IScreenCapture` 新增 `CaptureRawAsync`；`PageAnalyzer` 通过 `UNICLAW_RAW_SCREEN_BUFFER` 选择路径：

```csharp
var useRaw = Environment.GetEnvironmentVariable("UNICLAW_RAW_SCREEN_BUFFER") == "1";

if (useRaw && _screenCapture is IScreenCapture rawCap)
{
    // 新路径：raw bytes 原样转发，不经 ImageResizer
    var raw = await rawCap.CaptureRawAsync(ct);
    modelResponse = await _modelProvider.CompleteVisionRawAsync(
        modelRequest, raw, ct);
}
else
{
    // 旧路径（不动）
    var raw = await _screenCapture.CaptureAsync(ct);
    var bytes = ImageResizer.ResizeToMaxWidth(raw, maxWidth, cropTop, cropBottom, jpegQuality);
    modelResponse = await _modelProvider.CompleteVisionAsync(
        modelRequest, bytes, ct);
}
```

**`ImageResizer` 不动**——旧路径继续使用，不加 `ProcessRaw` 方法。raw 路径完全不经过它。

### 3.4 `LocalVisionProvider` 新增 raw 重载

```csharp
public async Task<ModelResponse> CompleteVisionRawAsync(
    ModelRequest request, RawScreenBuffer raw, CancellationToken ct = default)
{
    var sw = Stopwatch.StartNew();
    try
    {
        using var content = new ByteArrayContent(raw.Pixels);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.Add("X-Image-Width", raw.Width.ToString(CultureInfo.InvariantCulture));
        content.Headers.Add("X-Image-Height", raw.Height.ToString(CultureInfo.InvariantCulture));
        content.Headers.Add("X-Image-Pixel-Format", raw.PixelFormat.ToString(CultureInfo.InvariantCulture));

        using var httpResp = await _http.PostAsync("/v1/analyze_raw", content, ct);
        // ── 后续与 CompleteVisionAsync 完全相同 ──
        // Server-Timing 解析 → evidence 反序列化 → 4 步映射管道
        ...
    }
    catch (HttpRequestException ex) { ... }
}
```

### 3.5 预处理配置 — `label-mapping.json`

crop/resize 参数与 `edgeThreshold`、`roiPadding` 同放 `spatial` 段，Python 侧单点读取：

```json
{
  "schema": "uniclaw.labelMapping.v1",
  "mappings": { ... },
  "nonItemLabels": ["popup"],
  "spatial": {
    "level1MaxY": 0.08,
    "edgeThreshold": 0.92,
    "roiPadding": { "x": 0.15, "y": 0.1, "minPx": 8, "maxPx": 64 },
    "preprocessing": {
      "maxWidth": 720,
      "cropTopRatio": 0.0625,
      "cropBottomRatio": 0.0625
    }
  }
}
```

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `preprocessing.maxWidth` | 720 | 等比缩放目标宽度（px），0 = 不缩放 |
| `preprocessing.cropTopRatio` | 0.0625 | 顶部裁剪比例（status bar + search chrome，≈120px/1920） |
| `preprocessing.cropBottomRatio` | 0.0625 | 底部裁剪比例（nav bar / 空白间距，≈120px/1920） |

环境变量仍可覆盖：`UNICLAW_IMAGE_MAX_WIDTH`、`UNICLAW_IMAGE_CROP_TOP`、`UNICLAW_IMAGE_CROP_BOTTOM` —— Python 启动时读取 `os.environ`，fallback 到 `label-mapping.json` 的 `spatial.preprocessing`，再 fallback 到代码默认值。

### 3.6 Python 新 endpoint: `POST /v1/analyze_raw`

预处理 + pipeline 提取：

```python
# ── 预处理参数解析（启动时一次性加载）──
_PREPROCESS = _SPATIAL.get("preprocessing", {})
_MAX_WIDTH = int(os.environ.get("UNICLAW_IMAGE_MAX_WIDTH",
    _PREPROCESS.get("maxWidth", 720)))
_CROP_TOP = float(os.environ.get("UNICLAW_IMAGE_CROP_TOP",
    _PREPROCESS.get("cropTopRatio", 0.0625)))
_CROP_BOTTOM = float(os.environ.get("UNICLAW_IMAGE_CROP_BOTTOM",
    _PREPROCESS.get("cropBottomRatio", 0.0625)))


def _preprocess(image: Image.Image) -> Image.Image:
    """Crop top/bottom + resize to max width. PIL 零解码路径。"""
    w, h = image.size

    # Step 1: crop
    top_px = int(h * _CROP_TOP)
    bottom_px = int(h * _CROP_BOTTOM)
    if top_px > 0 or bottom_px > 0:
        crop_h = h - top_px - bottom_px
        if crop_h > 0:
            image = image.crop((0, top_px, w, h - bottom_px))

    # Step 2: resize (PIL thumbnail 保持宽高比)
    if _MAX_WIDTH > 0 and image.width > _MAX_WIDTH:
        ratio = _MAX_WIDTH / image.width
        new_h = int(image.height * ratio)
        image = image.resize((_MAX_WIDTH, new_h), Image.LANCZOS)

    return image


def _run_pipeline(
    image: Image.Image,
    width: int,
    height: int,
) -> dict[str, Any]:
    """公共 YOLO → OCR → fusion 管线，两个 endpoint 共享。"""
    t0 = time.perf_counter()
    detections = run_yolo_on_image(image, model_path=_MODEL_PATH,
                                   image_size=_IMAGE_SIZE,
                                   confidence=_DETECTION_CONF, device="cpu")
    t1 = time.perf_counter()

    if _OCR_BACKEND == "rapidocr":
        ocr_tokens = run_rapid_ocr_on_image(image, text_score=_OCR_TEXT_SCORE)
    else:
        _NON_TEXT_LABELS = frozenset({"imageview", "line"})
        ocr_detections = [d for d in detections if d.label not in _NON_TEXT_LABELS]
        ocr_crops = run_ocr_on_crops(image, ocr_detections, language=_OCR_LANG)
        # 重建对齐 ...
    t2 = time.perf_counter()

    if _OCR_BACKEND == "rapidocr":
        evidence = fuse_evidence(detections, ocr_tokens,
                                 image_width=width, image_height=height,
                                 interactive_labels=DEFAULT_INTERACTIVE_LABELS | {"text_block", "text"},
                                 promote_unmatched_ocr=True)
    else:
        evidence = fuse_evidence_from_crops(detections, aligned_ocr,
                                            image_width=width, image_height=height)
    t3 = time.perf_counter()

    evidence["metadata"] = _metadata(width, height)
    evidence["scrollHints"] = _scroll_hints(evidence["candidates"])
    return evidence


@app.post("/v1/analyze_raw")
async def analyze_raw(request: Request):
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

    # 0ms — PIL frombytes 纯内存包装
    image = Image.frombytes("RGBA", (width, height), body)

    # 预处理（crop + resize）— PIL 原生操作
    image = _preprocess(image)
    preproc_w, preproc_h = image.size

    # 去 alpha
    image = image.convert("RGB")

    try:
        evidence = _run_pipeline(image, preproc_w, preproc_h)
        ...
    finally:
        gc.collect()
```

旧 `/v1/analyze` 改为调用同一 `_run_pipeline` 公共函数（内部重构，行为不变）。

### 3.7 存储边界 PNG 编码 — `RunAssetHook`

C# 侧唯一一次像素操作：把 raw RGBA 编码为 PNG 存盘。用 SkiaSharp（已有依赖）：

```csharp
private byte[] EncodeRawToPng(RawScreenBuffer raw)
{
    using var bitmap = new SKBitmap(raw.Width, raw.Height,
        SKColorType.Rgba8888, SKAlphaType.Unpremul);
    bitmap.SetPixels(raw.Pixels.AsSpan());
    return bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();
}
```

文件名仍为 `before.png` / `after.png`，内容也是标准 PNG，下游（文件管理器、PIL、trace viewer）无需感知。

### 3.8 特性开关

| 环境变量 | 默认值 | 说明 |
|---------|--------|------|
| `UNICLAW_RAW_SCREEN_BUFFER` | `0` | `1` 启用 raw RGBA 全链路；`0` 走旧 PNG→JPEG 路径 |

验证通过后将默认值改为 `1`，旧路径保留为 fallback。

## 4. Acceptance Criteria

### 4.1 Python 侧

| # | 标准 | 验证方式 |
|---|---|---|
| P1 | `POST /v1/analyze_raw` 接收 raw RGBA + headers → 返回合法 evidence JSON | Python unit test（TestClient） |
| P2 | 同一帧截图分别走 `/v1/analyze`（PNG bytes）和 `/v1/analyze_raw`（RGBA bytes + 同 crop/resize 参数）→ evidence candidates 数量一致、坐标误差 < 0.002 | Python unit test |
| P3 | `Image.frombytes("RGBA", w, h, body)` + `_preprocess` 零临时文件、零磁盘 I/O | Python unit test |
| P4 | 旧 `/v1/analyze` endpoint 行为不变 | 现有 test_server.py 全绿 |
| P5 | body 尺寸不匹配 `width*height*4` → HTTP 400 | Python unit test |
| P6 | `_preprocess` 输出尺寸与 C# `ImageResizer.ResizeToMaxWidth` 同参数输出一致（宽高误差 ≤1px） | Python unit test |

### 4.2 C# 侧

| # | 标准 | 验证方式 |
|---|---|---|
| C1 | `CaptureRawScreenBufferAsync` → `adb exec-out screencap`（无 `-p`）→ header 解析正确 | 集成测试（adb-read scope） |
| C2 | `RawScreenBuffer.PixelFormat` = 1（RGBA_8888） | 集成测试 |
| C3 | `LocalVisionProvider.CompleteVisionRawAsync` POST 到 `/v1/analyze_raw`，headers（Width/Height/PixelFormat）正确 | C# unit test（mock HTTP） |
| C4 | `UNICLAW_RAW_SCREEN_BUFFER=0` 时走旧路径（行为不变） | regression：现有测试全绿 |
| C5 | `RunAssetHook` raw 路径输出 `.png` 可被 PIL 正常打开，尺寸与原始 raw buffer 一致 | 集成测试 |

### 4.3 端到端

| # | 标准 | 验证方式 |
|---|---|---|
| E1 | `UNICLAW_RAW_SCREEN_BUFFER=1` → scenario-locate 完整跑通 | 集成测试 |
| E2 | 同一帧 raw 路径和 PNG 路径的 evidence JSON `candidates` 数量一致、坐标误差 < 0.001 | 对比脚本 |
| E3 | `Server-Timing` 各阶段耗时无退化（yolo/ocr/fusion） | 性能对比 |

## 5. 不改动的部分

- `ImageResizer` — 不动（旧路径继续使用，不新增 `ProcessRaw`）
- `run_yolo_on_image` / `run_rapid_ocr_on_image` — 已接受 PIL Image，RGBA→RGB 已有 `convert("RGB")`
- `fuse_evidence` — 只需要 width/height 整数，不接触像素
- `TracePipeline` / `FileAssetStore` — 格式无关的字节传输
- `AdbCommandRunner` — binary capture 通道无需改动
- 旧 `CaptureScreenshotAsync`（PNG）— 保留不动
- 旧 `POST /v1/analyze` — 保留不动（内部重构为 `_run_pipeline` 共享，行为不变）
- 所有现有测试 — 旧路径行为不变

## 6. Decisions

| ID | 决策 | 理由 |
|----|------|------|
| D-1 | 平行新增，不删除旧路径 | 渐进迁移，旧路径作为 fallback；特性开关控制 |
| D-2 | 特性开关用环境变量 `UNICLAW_RAW_SCREEN_BUFFER` | 无需改代码即可切回；CI 可分别跑两条路径 |
| D-3 | C# 侧只解析 header + 转发 raw bytes，零像素操作 | 职责清晰：C# 是 ADB 传输层，Python 是图像处理层。PIL 是做 crop/resize 的自然位置，且 Python 已在做 `convert("RGB")`，预处理统一在一处 |
| D-4 | RawScreenBuffer 放 `UniClaw.Core.UniBrain` | `IScreenCapture` 同 namespace；Core 持有截图抽象 |
| D-5 | 存储边界用 SkiaSharp 编码 PNG（复用已有依赖） | 已在 `UniClaw.Core.csproj`；存盘是 C# 侧的持久化职责，无需回传 Python |
| D-6 | Python endpoint 新增 `/v1/analyze_raw` 而非修改 `/v1/analyze` | 旧 endpoint 保持兼容；raw endpoint 维度来自 headers，body 语义不同 |
| D-7 | 提取 `_run_pipeline` 公共函数 | 两个 endpoint 共享 YOLO → OCR → fusion 逻辑，避免分叉 |
| D-8 | 预处理参数放 `label-mapping.json` `spatial.preprocessing` 段 | 与 `edgeThreshold`、`roiPadding` 同位置，Python 侧单点读取；环境变量可覆盖 |
| D-9 | `UNICLAW_RAW_SCREEN_BUFFER` 默认 `0` | 先验证再推广；风险可控 |
| D-10 | `ImageResizer` 不新增 `ProcessRaw` 方法 | C# 不做像素操作；旧路径继续用 `ResizeToMaxWidth`，raw 路径完全绕开 |

## 7. Error Handling

| 故障 | 处理 |
|------|------|
| screencap header 不足 12 字节 | `AdbCommandException`（"header too short"） |
| pixel_format ≠ 1 | `AdbCommandException`（"unsupported pixel format"） |
| HTTP 非 2xx | `ModelResponse.Success = false`（与现有 graceful 语义一致） |
| Python body 尺寸不匹配 | HTTP 400（`expected W×H×4, got N`） |
| Python pixel_format ≠ 1 | HTTP 400（`Unsupported pixel format`） |
| HTTP 传输错误 | `ModelResponse.Success = false`（`HttpRequestException` 捕获） |
