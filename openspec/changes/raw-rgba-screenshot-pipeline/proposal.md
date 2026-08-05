## Why

当前 ADB 截图链路存在三次不必要的编解码（device PNG encode → C# SKBitmap.Decode → JPEG encode → Python PIL decode），累计约 15-33ms 开销，且所有中间格式仅在最终存盘时需要持久化。`adb exec-out screencap`（不带 `-p`）直接输出 RGBA 裸流，配合 PIL `Image.frombytes` 可实现零拷贝直通——C# 只做传输，Python 做预处理，仅在存盘时一次性编码 PNG。同链路可节省 3 次编解码，延迟从 ~235ms 降至 ~75ms（含 YOLO+OCR 50ms 不变部分）。

## What Changes

- **新增** `adb exec-out screencap`（无 `-p`）裸流捕获路径：解析 12 字节 framebuffer header，输出 `RawScreenBuffer { Pixels, Width, Height, PixelFormat }`
- **新增** `IScreenCapture.CaptureRawAsync()` 接口方法 + `IAdbSession.CaptureRawScreenBufferAsync()` 接口方法
- **新增** `POST /v1/analyze_raw` Python endpoint：接收 `application/octet-stream` raw RGBA body + dimension headers → `Image.frombytes` 零解码 → PIL crop/resize → YOLO+OCR
- **新增** `label-mapping.json` `spatial.preprocessing` 段：`maxWidth` / `cropTopRatio` / `cropBottomRatio`（Python 侧单点读取，环境变量可覆盖）
- **新增** `LocalVisionProvider.CompleteVisionRawAsync()`：发送 raw RGBA 到 `/v1/analyze_raw`，Content-Type: `application/octet-stream`
- **修改** `PageAnalyzer` 双路径选择：`UNICLAW_RAW_SCREEN_BUFFER=1` 走 raw 路径（不经过 `ImageResizer`），`0` 走旧路径
- **修改** `RunAssetHook`：raw 路径下存盘前用 SkiaSharp `Encode(SKEncodedImageFormat.Png)` 一次性编码
- **不动** `ImageResizer`：不加新方法，旧路径继续使用
- **不动** 旧 `CaptureScreenshotAsync`（PNG）、旧 `POST /v1/analyze`（encoded image）

## Capabilities

### New Capabilities
- `raw-rgba-capture`: ADB framebuffer raw capture（`exec-out screencap` 无 `-p`）→ header 解析 → raw RGBA bytes 原样转发至 Python，C# 侧零像素操作

### Modified Capabilities
- `local-vision-provider`: `LocalVisionProvider` 新增 `CompleteVisionRawAsync` 方法——POST raw RGBA bytes + dimension headers 到 `/v1/analyze_raw`，其余 pipeline（evidence 反序列化、4 步映射）复用
- `python-vision-service`: 新增 `POST /v1/analyze_raw` endpoint，提取 `_run_pipeline` 公共函数，新增 `_preprocess`（PIL crop/resize），预处理参数从 `label-mapping.json` `spatial.preprocessing` 读取
- `label-mapping-config`: `label-mapping.json` schema 新增 `spatial.preprocessing` 字段（`maxWidth` / `cropTopRatio` / `cropBottomRatio`），Python 和 C# 共享读取

## Impact

- `src/UniClaw.Core/UniBrain/` — `RawScreenBuffer` 新类型、`IScreenCapture` 新方法、`PageAnalyzer` 双路径
- `src/UniClaw.Device/` — `IAdbSession` 新方法、`ProcessAdbSession` / `AdvancedSharpAdbSession` 实现、`AdbScreenCapture` 委托
- `src/UniClaw.LocalVisionProvider/` — `LocalVisionProvider` 新增 `CompleteVisionRawAsync`
- `src/UniClaw.Host/Hooks/` — `RunAssetHook` raw→PNG 编码
- `tools/local_vision/server.py` — 新增 endpoint + 提取公共 pipeline + 新增 `_preprocess`
- `tools/local_vision/label-mapping.json` — 新增 `spatial.preprocessing`
- `.claude/settings.local.json` — 放行 `adb exec-out screencap`（无 `-p`）
- `tests/UniClaw.Host.Tests/Device/` — raw capture 集成测试
- `tools/local_vision/tests/test_server.py` — raw endpoint 测试
