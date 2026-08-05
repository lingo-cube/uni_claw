## ADDED Requirements

### Requirement: LocalVisionProvider supports raw RGBA vision completion

`LocalVisionProvider` SHALL expose `CompleteVisionRawAsync(ModelRequest request, RawScreenBuffer raw, CancellationToken ct)` that POSTs `raw.Pixels` to `/v1/analyze_raw` with `Content-Type: application/octet-stream` and custom headers `X-Image-Width` (raw.Width), `X-Image-Height` (raw.Height), `X-Image-Pixel-Format` (raw.PixelFormat). On HTTP 2xx, it SHALL deserialize the response as `LocalVisionEvidence`, map via the 4-step pipeline, and return `ModelResponse(Success: true)`. On HTTP non-2xx, transport error, or timeout, it SHALL return `ModelResponse(Success: false, ErrorMessage: ...)` without throwing.

The method SHALL reuse the existing `Server-Timing` parsing, evidence deserialization, 4-step mapping pipeline, and trace recording logic from `CompleteVisionAsync`.

#### Scenario: Raw vision analysis returns PageAnalysisDto JSON

- **WHEN** `CompleteVisionRawAsync` is called with valid `RawScreenBuffer` and Python returns 200 with valid evidence JSON
- **THEN** `ModelResponse.Success` is true, `Mode` is "vision", `LatencyMs` is non-zero, and `Content` is valid `PageAnalysisDto` JSON

#### Scenario: Raw vision HTTP headers include dimensions

- **WHEN** `CompleteVisionRawAsync` sends the HTTP request
- **THEN** the request includes `X-Image-Width: {raw.Width}`, `X-Image-Height: {raw.Height}`, `X-Image-Pixel-Format: {raw.PixelFormat}`, and `Content-Type: application/octet-stream`

#### Scenario: Raw vision HTTP failure is graceful

- **WHEN** Python returns HTTP 500 or connection is refused for raw endpoint
- **THEN** `ModelResponse.Success` is false, `ErrorMessage` is set, and no exception is thrown

### Requirement: PageAnalyzer raw path bypasses ImageResizer

When `UNICLAW_RAW_SCREEN_BUFFER=1`, `PageAnalyzer.AnalyzeOnceAsync` SHALL call `CaptureRawAsync` instead of `CaptureAsync`, and SHALL pass the `RawScreenBuffer` directly to `CompleteVisionRawAsync` WITHOUT going through `ImageResizer.ResizeToMaxWidth`. The `maxWidth`/`cropTop`/`cropBottom`/`jpegQuality` variables SHALL NOT be applied in the raw path — preprocessing is delegated to Python.

When `UNICLAW_RAW_SCREEN_BUFFER` is absent or not `"1"`, the existing `CaptureAsync` → `ImageResizer` → `CompleteVisionAsync` path SHALL remain unchanged.

#### Scenario: Raw path skips ImageResizer

- **WHEN** `UNICLAW_RAW_SCREEN_BUFFER=1` and a screenshot is captured
- **THEN** `ImageResizer.ResizeToMaxWidth` is NOT called and `RawScreenBuffer.Pixels` are forwarded unmodified to `CompleteVisionRawAsync`

#### Scenario: Old path unchanged when flag is off

- **WHEN** `UNICLAW_RAW_SCREEN_BUFFER` is not set to `"1"`
- **THEN** the existing `CaptureAsync` → `ImageResizer.ResizeToMaxWidth` → `CompleteVisionAsync` path executes identically to before
