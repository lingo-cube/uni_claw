## Context

当前 ADB 截图链路 `adb exec-out screencap -p` → device PNG encode → C# `SKBitmap.Decode` → crop/resize → JPEG encode → HTTP POST `/v1/analyze` → Python `Image.open(BytesIO)` decode → YOLO+OCR。全链路 3 次编解码，其中 PNG→JPEG 转换还引入有损压缩。

`adb exec-out screencap`（不带 `-p`）直接输出 12 字节 framebuffer header + RGBA 裸像素。PIL `Image.frombytes` 是纯内存包装（零解码），C# 只需解析 header 后原样转发，Python 侧用 PIL 原生 crop/resize 统一预处理。

PRD: `docs/prd/2026-08-04-raw-rgba-screenshot-pipeline-prd.md`

## Goals / Non-Goals

**Goals:**
- 消除全链路 3 次编解码：device PNG encode、C# SKBitmap.Decode + JPEG encode、Python PIL decode
- C# 侧零像素操作：只解析 12B header → 转发 raw bytes + dimensions
- Python 侧统一预处理：PIL `frombytes`（0ms）→ crop → resize → `convert("RGB"）`
- 仅在存储边界一次性编码 PNG（`RunAssetHook`）
- 平行新增，旧路径保留为 fallback（`UNICLAW_RAW_SCREEN_BUFFER=0` 默认走旧路径）
- 端到端 evidence JSON 一致性：raw 路径和 PNG 路径对同一帧输出相同 candidates

**Non-Goals:**
- 不删除旧 `CaptureScreenshotAsync` / `/v1/analyze` / `ImageResizer`
- 不修改 `ImageResizer`——不加 `ProcessRaw` 方法，旧路径继续用
- 不新增 NuGet 依赖（SkiaSharp 已有）
- 不改变 `fuse_evidence`、`run_yolo_on_image`、`run_rapid_ocr_on_image` 签名

## Decisions

### D-1: 预处理归 Python 而非 C#（SkiaSharp）

**选择**: Python PIL crop/resize

**备选**: C# `ImageResizer.ProcessRaw`（SkiaSharp `SetPixels` → crop → resize → raw RGBA）

**理由**: C# 职责是 ADB 传输，不是图像处理。PIL 是图像处理的自然位置，且 Python 已在做 `convert("RGB")`，预处理统一在一处。C# 无需建 `SKBitmap`、无需了解像素格式——只解析 12B header，之后全是 `byte[]` 转发。SkiaSharp `SetPixels` + crop + resize + `Bytes` 比 PIL `frombytes` + `crop` + `resize` 多一层 C# 内存分配。

### D-2: 预处理参数放 `label-mapping.json` `spatial.preprocessing`

**选择**: `label-mapping.json` 新增 `spatial.preprocessing: { maxWidth, cropTopRatio, cropBottomRatio }`

**备选**: 环境变量 `UNICLAW_IMAGE_MAX_WIDTH` / `UNICLAW_IMAGE_CROP_TOP` / `UNICLAW_IMAGE_CROP_BOTTOM`（当前方式）；硬编码默认值

**理由**: 与 `edgeThreshold`、`roiPadding` 同放 `spatial` 段，Python 侧单点读取，C# 侧同步可读。环境变量作为覆盖层（env > config > default），保持向后兼容。

### D-3: Python 新 endpoint `/v1/analyze_raw` 而非修改 `/v1/analyze`

**选择**: 新增独立 endpoint

**备选**: 修改 `/v1/analyze`，通过 `Content-Type` 自动检测（`image/jpeg` → `Image.open`，`application/octet-stream` → `Image.frombytes`）

**理由**: 旧 endpoint 保持兼容；raw endpoint 维度来自 headers（`X-Image-Width`/`X-Image-Height`），body 语义不同（raw RGBA vs encoded bytes），分 endpoint 语义更清晰。提取 `_run_pipeline` 公共函数避免代码分叉。

### D-4: 特性开关 `UNICLAW_RAW_SCREEN_BUFFER`（默认 0）

**选择**: 环境变量 toggle

**备选**: 编译时常量、配置文件字段

**理由**: 无需重新编译即可切回；CI 可分别跑两条路径；验证通过后改默认值为 `1`。

### D-5: `RawScreenBuffer` 放 `UniClaw.Core.UniBrain`

**选择**: Core namespace，与 `IScreenCapture` 同包

**备选**: `UniClaw.Device` namespace

**理由**: Core 持有截图抽象（`IScreenCapture`），`RawScreenBuffer` 是其返回类型，应同包。Device 层引用 Core，方向正确。

### D-6: 存储边界用 SkiaSharp 编码 PNG

**选择**: `RunAssetHook` 内 `SKBitmap.Encode(SKEncodedImageFormat.Png)`

**备选**: Python 返回 PNG bytes（存盘前的 raw→PNG 也在 Python 做）

**理由**: 存盘是 C# 侧的持久化职责（`TracePipeline` → `FileAssetStore`），不应回传 Python 再编码。SkiaSharp 已在 `UniClaw.Core.csproj`，零新增依赖。

### D-7: `RawScreenBuffer` 存储全分辨率 raw bytes

**选择**: C# 转发未裁剪、未缩放的全分辨率 RGBA

**备选**: C# 侧先 crop/resize 再转发

**理由**: D-1 已决定预处理归 Python。全分辨率转发使 `RunAssetHook` 存盘时拿到完整截图（与旧路径 `before.png` 分辨率一致），且 Python crop/resize 参数可随时调整而不影响已存盘资产。

## Risks / Trade-offs

| 风险 | 缓解 |
|------|------|
| Raw RGBA 数据量约为 PNG 的 3-5 倍（1080×2400×4 ≈ 10MB vs PNG ~2MB） | UDS 回环带宽 ~GB/s，额外传输 < 1ms；HTTP body 在内存中，不落盘 |
| `Image.frombytes("RGBA", w, h, body)` 假设 body 尺寸精确匹配 `w*h*4` | Python 启动时校验 `len(body) == w*h*4`，不匹配 → HTTP 400 |
| C# 和 Python crop/resize 实现差异可能导致 evidence 略微不同 | 验收 P6：同一帧 PNG 路径 vs raw 路径 candidates 数量一致、坐标误差 < 0.002 |
| `screencap` 裸流 pixel_format ≠ 1（非 RGBA_8888） | C# 侧抛 `AdbCommandException`，Python 侧 HTTP 400，明确报错而非静默错误 |
| `ImageResizer` 旧路径与 Python `_preprocess` 参数不一致 | `label-mapping.json` `spatial.preprocessing` 单点真源，env 覆盖层两边一致 |

## Migration Plan

1. **Phase 1（本 PRD）**: 平行实现 raw 路径，`UNICLAW_RAW_SCREEN_BUFFER=0` 默认走旧路径
2. **Phase 2（验证）**: Python unit test + C# integration test + 端到端 evidence 对比
3. **Phase 3（压测）**: 100 次连续请求对比旧/新路径延迟分布（P50/P95/P99）、Python 内存增长
4. **Phase 4（切换）**: 验证通过后默认值改为 `1`；旧路径保留至少一个 release cycle 作为 fallback
5. **Rollback**: 设 `UNICLAW_RAW_SCREEN_BUFFER=0` 即时切回旧路径，无需改代码或重启服务

## Open Questions

- Q1: `spatial.preprocessing.maxWidth: 720` 与旧路径 `ImageResizer.DefaultMaxWidth`（也是 720）一致，但旧路径还做了 JPEG quality=85 有损压缩。raw 路径无 JPEG 编码——YOLO 推理精度是否因无损输入而提升？需压测对比验证。
- Q2: `RunAssetHook` raw→PNG 编码是否需要与旧路径的 JPEG 存盘行为保持一致？当前旧路径存的是 resize 后的 bytes（可能是 JPEG），raw 路径存的是全分辨率 PNG——文件更大但质量更高。
