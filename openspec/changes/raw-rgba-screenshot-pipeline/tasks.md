## 1. Core Type & Interface

- [x] 1.1 Create `RawScreenBuffer` record struct in `src/UniClaw.Core/UniBrain/RawScreenBuffer.cs` with `Pixels` (byte[]), `Width` (int), `Height` (int), `PixelFormat` (int)
- [x] 1.2 Add `CaptureRawScreenBufferAsync(CancellationToken ct)` to `IAdbSession` interface
- [x] 1.3 Add `CaptureRawAsync(CancellationToken ct)` to `IScreenCapture` interface

## 2. ADB Raw Capture Implementation

- [x] 2.1 Implement `ProcessAdbSession.CaptureRawScreenBufferAsync`: execute `adb exec-out screencap` (no `-p`), parse 12B header via `BinaryPrimitives.ReadUInt32LittleEndian`, validate `pixel_format == 1`, return `RawScreenBuffer`
- [x] 2.2 Implement `AdvancedSharpAdbSession.CaptureRawScreenBufferAsync`: same logic via `_binaryRunner`
- [x] 2.3 Implement `AdbScreenCapture.CaptureRawAsync` delegation to `IAdbSession.CaptureRawScreenBufferAsync`
- [x] 2.4 Add `Bash(adb -s 127.0.0.1:6555 exec-out screencap)` to `.claude/settings.local.json` allowlist

## 3. Python Server — New Endpoint

- [x] 3.1 Extract `_run_pipeline(image, width, height)` shared function from `/v1/analyze` body (YOLO → OCR → fusion → evidence + scroll hints)
- [x] 3.2 Add `_preprocess(image: Image.Image) -> Image.Image`: crop top/bottom → resize to maxWidth via PIL
- [x] 3.3 Add `POST /v1/analyze_raw` endpoint: read `X-Image-Width`/`X-Image-Height`/`X-Image-Pixel-Format` headers, `Image.frombytes("RGBA", w, h, body)`, call `_preprocess`, `convert("RGB")`, `_run_pipeline`
- [x] 3.4 Add body size validation (`len(body) == w*h*4`) and pixel_format validation (only accept `1`), return HTTP 400 on mismatch
- [x] 3.5 Load `spatial.preprocessing` from `label-mapping.json` at startup, with env var overrides (`UNICLAW_IMAGE_MAX_WIDTH`/`UNICLAW_IMAGE_CROP_TOP`/`UNICLAW_IMAGE_CROP_BOTTOM`)

## 4. Label Mapping Config

- [x] 4.1 Add `"preprocessing": {"maxWidth": 720, "cropTopRatio": 0.0625, "cropBottomRatio": 0.0625}` to `tools/local_vision/label-mapping.json` under `spatial`
- [x] 4.2 Update C# `LabelMappingConfig` class if needed to deserialize new `preprocessing` fields (backward compatible)

## 5. C# Provider — Raw HTTP

- [x] 5.1 Add `CompleteVisionRawAsync(ModelRequest, RawScreenBuffer, CancellationToken)` to `LocalVisionProvider`
- [x] 5.2 Implement raw HTTP POST: `ByteArrayContent(raw.Pixels)`, `Content-Type: application/octet-stream`, `X-Image-Width`/`X-Image-Height`/`X-Image-Pixel-Format` headers, POST to `/v1/analyze_raw`
- [x] 5.3 Reuse existing `Server-Timing` parsing, evidence deserialization, 4-step mapping pipeline

## 6. PageAnalyzer Dual Path

- [x] 6.1 Read `UNICLAW_RAW_SCREEN_BUFFER` env var in `PageAnalyzer.AnalyzeOnceAsync`
- [x] 6.2 Raw path: `CaptureRawAsync` → `CompleteVisionRawAsync` (no `ImageResizer`)
- [x] 6.3 Old path: unchanged, gated by flag != "1"
- [x] 6.4 Ensure compile: `PageAnalyzer` references `IScreenCapture` (already injected)

## 7. RunAssetHook PNG Encoding

- [x] 7.1 Support raw path in `RunAssetHook`: conditionally call `CaptureRawAsync` instead of `CaptureAsync`
- [x] 7.2 Implement `EncodeRawToPng(RawScreenBuffer)`: `SKBitmap` + `SetPixels` + `Encode(SKEncodedImageFormat.Png)` → `AssetSubmission`
- [x] 7.3 Preserve `before.png` / `after.png` file naming; downstream consumers (trace viewer, PIL) unchanged

## 8. Python Unit Tests

- [x] 8.1 Test `POST /v1/analyze_raw` with synthetic RGBA bytes + dimension headers → returns valid evidence JSON
- [x] 8.2 Test body size mismatch → HTTP 400 with correct detail message
- [x] 8.3 Test `pixel_format != 1` → HTTP 400
- [x] 8.4 Test `_preprocess` output dimensions match C# `ImageResizer.ResizeToMaxWidth` with same crop/resize params (tolerance: 1px)
- [x] 8.5 Test `/v1/analyze` behavior unchanged after `_run_pipeline` extraction (regression)
- [x] 8.6 Test roundtrip: same screenshot via `/v1/analyze` (PNG bytes) and `/v1/analyze_raw` (RGBA bytes + same crop/resize) → candidates count equal, center coordinates within 0.002

## 9. C# Integration Tests

- [ ] 9.1 Test `CaptureRawScreenBufferAsync` on real emulator (adb-read scope): header parse → `PixelFormat == 1`, `Pixels.Length == Width * Height * 4`
- [ ] 9.2 Test `AdbScreenCapture.CaptureRawAsync` returns valid `RawScreenBuffer` from emulator
- [x] 9.3 Test `LocalVisionProvider.CompleteVisionRawAsync` with mock HTTP → correct endpoint + headers
- [ ] 9.4 Test `RunAssetHook` raw path → output `.png` decodable by PIL, dimensions match `RawScreenBuffer`
- [ ] 9.5 Regression: `UNICLAW_RAW_SCREEN_BUFFER=0` (default) → all existing tests pass (old path unchanged)

## 10. End-to-End Verification

- [ ] 10.1 Scenario-locate integration test with `UNICLAW_RAW_SCREEN_BUFFER=1`
- [ ] 10.2 Same-frame evidence comparison: capture raw + PNG simultaneously, diff candidates count and coordinates
- [ ] 10.3 `Server-Timing` header check: yolo/ocr/fusion stages show no regression vs old path

## 11. Benchmark & Stress Test (压测)

- [x] 11.1 Write benchmark script: 50 consecutive requests per path, record per-request latency (`tools/local_vision/benchmark_raw.py`)
- [x] 11.2 Measure latency distribution: P50 -484ms (-19.4%), P95 -727ms (-22.3%), P99 -1248ms (-32.2%), raw ≤ JPEG ✅
- [x] 11.3 Measure bytes-on-wire: raw 8.1MB vs JPEG 140KB (58×), localhost transfer negligible
- [ ] 11.4 Measure Python memory: RSS before → after 100 requests via `/v1/analyze_raw`, verify < 20% growth (same as old endpoint `gc.collect()` guarantee)
- [x] 11.5 Measure `Image.frombytes` vs `Image.open(BytesIO)` wall time: frombytes 3117µs, open(JPEG) 60µs — net benefit from preprocessing, not decode
- [ ] 11.6 YOLO inference quality check: compare detection counts and confidence scores between raw (lossless) and JPEG (quality=85) inputs on same frame — verify raw ≥ JPEG in detection count (lossless should not lose detections)
- [ ] 11.7 Cold-start overhead: measure first raw request latency (includes `_preprocess` + `frombytes`) vs first JPEG request (includes `Image.open` decode), verify no regression
- [x] 11.8 Document benchmark results in `docs/validation/raw-rgba-benchmark-2026-08-04.md`

## 12. Archive & Cleanup

- [ ] 12.1 After Phase 3 benchmark passes, set `UNICLAW_RAW_SCREEN_BUFFER` default to `1`
- [ ] 12.2 Update docs/validation to record benchmark results
- [ ] 12.3 Mark old path (`-p` + `ImageResizer` + JPEG) as deprecated in code comments; keep for one release cycle as fallback

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Core/UniBrain/` | `docs/prd/2026-08-04-raw-rgba-screenshot-pipeline-prd.md` |
| `src/UniClaw.Device/` | `docs/prd/2026-08-04-raw-rgba-screenshot-pipeline-prd.md` |
| `src/UniClaw.LocalVisionProvider/` | `docs/prd/2026-08-03-local-vision-provider-prd.md` |
| `tools/local_vision/` | `docs/prd/2026-08-03-local-vision-host-wiring-prd.md` |
