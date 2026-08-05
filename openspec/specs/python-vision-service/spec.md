## Purpose
Python FastAPI long-running service wrapping the YOLO+PaddleOCR pipeline, plus C# process lifecycle management (`PythonVisionService`). Together they provide a local HTTP endpoint for screenshot → evidence JSON conversion.

## Requirements

### Requirement: Python FastAPI server accepts screenshots and returns evidence

The Python server SHALL expose `POST /v1/analyze` accepting raw JPEG bytes with `Content-Type: image/jpeg`. It SHALL run the pipeline: YOLO detection → ROI-crop → multi-threaded OCR → fusion → scroll hints, and return `uniclaw.localVisionEvidence.v1` JSON with `200 OK`. It SHALL expose `GET /health` returning `{"status": "ok", "warm": true}` where `warm` is false until model warmup completes.

The response SHALL include a `Server-Timing` header with format `yolo;dur=X, ocr;dur=Y, fusion;dur=Z, scroll;dur=W` (milliseconds). Timing SHALL NOT appear in the JSON body.

The server SHALL read `label-mapping.json` at startup for `spatial.edgeThreshold`, `detection.confidence`, and `spatial.roiPadding`. It SHALL set `OMP_NUM_THREADS` before any numpy/ultralytics/paddleocr imports.

#### Scenario: Health check returns warm after startup

- **WHEN** `GET /health` is called after server startup and model warmup
- **THEN** response is `{"status": "ok", "warm": true}`

#### Scenario: Health check shows not warm during warmup

- **WHEN** `GET /health` is called immediately after server start (before warmup completes)
- **THEN** `warm` is `false`

#### Scenario: Analyze returns evidence for valid image

- **WHEN** `POST /v1/analyze` is called with a valid JPEG screenshot containing UI elements
- **THEN** response is 200, `Content-Type: application/json`, body contains `candidates` array with at least one element having `type`, `text`, `center`, `bounds`, and `confidence`

#### Scenario: Server-Timing header present

- **WHEN** `POST /v1/analyze` completes successfully
- **THEN** response includes `Server-Timing` header matching pattern `yolo;dur=..., ocr;dur=..., fusion;dur=..., scroll;dur=...`

#### Scenario: Timing not in JSON body

- **WHEN** `POST /v1/analyze` response body is inspected
- **THEN** no timing-related fields (e.g., `timing`, `duration`, `latency`) are present in the JSON

### Requirement: Zero-disk I/O pipeline

The server SHALL perform all image processing in memory: `PIL.Image.open(BytesIO(request_body))` → YOLO `predict(source=PIL.Image)` → `image.crop(box)` → `np.asarray(crop)` → `ocr.ocr(ndarray)`. No temporary files SHALL be written for normal operation. The only disk access SHALL be: (1) model weight files loaded once at startup, (2) `label-mapping.json` read once at startup.

#### Scenario: No temp files for image processing

- **WHEN** a request is processed through the full pipeline
- **THEN** no files are created in temporary directories for image or crop data

### Requirement: Model warmup at startup

The server SHALL warm up YOLO and OCR models during FastAPI lifespan before accepting requests. Warmup SHALL consist of: (1) a dummy `Image.new("RGB", (640, 640))` passed through `run_yolo_on_image`, (2) a dummy OCR call through `warmup_ocr()`. The health check `warm` SHALL be false until both complete.

#### Scenario: First request does not pay model load cost

- **WHEN** the first `POST /v1/analyze` request arrives after server startup
- **THEN** the response time reflects inference only, not model loading (YOLO and OCR already loaded)

### Requirement: Thread configuration

`OMP_NUM_THREADS` SHALL be set in the Python module BEFORE any library imports, defaulting to 4, overridable via `UNICLAW_OMP_THREADS` environment variable. uvicorn SHALL default to 1 worker (model ≈600MB per process), documented as configurable. `UNICLAW_OCR_PARALLEL` SHALL control OCR thread pool size, defaulting to 2.

#### Scenario: OMP threads set before imports

- **WHEN** server module is loaded
- **THEN** `os.environ["OMP_NUM_THREADS"]` is set before `import numpy` or `from ultralytics import YOLO`

### Requirement: Garbage collection per request

The server SHALL call `gc.collect()` after each request to mitigate PaddleOCR's known memory leak under sustained load.

#### Scenario: GC called after each request

- **WHEN** `POST /v1/analyze` completes (success or error)
- **THEN** `gc.collect()` is invoked before the response is returned

### Requirement: PythonVisionService manages process lifecycle

`PythonVisionService` SHALL be a `sealed class` in `UniClaw.Device` implementing `IPythonVisionService : IAsyncDisposable`, constructed with no required arguments. It SHALL expose `HttpClient HttpClient { get; }` and `bool IsRunning { get; }`.

`StartAsync(CancellationToken ct)` SHALL: (1) determine transport (UDS on macOS/Linux via `UnixDomainSocketEndPoint`, TCP on Windows via `127.0.0.1:{port}`), (2) resolve uvicorn path and start the Python process, (3) poll `GET /health` until `warm: true` or 30s timeout, (4) set `IsRunning = true`.

`DisposeAsync()` SHALL kill the Python process tree and set `IsRunning = false`.

#### Scenario: UDS on macOS

- **WHEN** `PythonVisionService.StartAsync` is called on macOS
- **THEN** HttpClient is configured with `SocketsHttpHandler.ConnectCallback` using `UnixDomainSocketEndPoint` at `/tmp/uniclaw-vision.sock` (or `UNICLAW_VISION_SOCK` env override)

#### Scenario: TCP on Windows

- **WHEN** `PythonVisionService.StartAsync` is called on Windows
- **THEN** HttpClient targets `http://127.0.0.1:8765` (or `UNICLAW_VISION_PORT` env override)

#### Scenario: StartAsync blocks until warm

- **WHEN** `StartAsync` is called and Python is starting up
- **THEN** it polls `/health` until `warm: true` is received or 30s timeout throws `TimeoutException`

#### Scenario: DisposeAsync kills process

- **WHEN** `DisposeAsync` is called while Python is running
- **THEN** the Python process tree is killed and `IsRunning` becomes false

### Requirement: Auto-restart with backoff

When the Python process exits unexpectedly, `PythonVisionService` SHALL attempt restart with exponential backoff: 0ms → 500ms → 1s → 3s → 10s (cap). Before restarting, it SHALL health-check the existing socket (reuse if process is alive). After `maxRestarts` (default 5) consecutive failures, it SHALL stop attempting and set `IsRunning = false`.

#### Scenario: Auto-restart on process exit

- **WHEN** Python process crashes after `StartAsync`
- **THEN** the service detects the exit and attempts restart after a backoff delay

#### Scenario: Health probe before restart

- **WHEN** process exit is detected
- **THEN** the service first probes `/health` on the existing socket; if alive, reuses without restart

#### Scenario: Max restarts exceeded

- **WHEN** the process has been restarted 5 times and crashes again
- **THEN** `IsRunning` becomes false and no further restarts are attempted

### Requirement: Residual socket cleanup

Before starting the Python process, `PythonVisionService` SHALL unlink any existing UDS socket file at the target path to prevent "address already in use" errors.

#### Scenario: Stale socket removed

- **WHEN** a previous Python instance left `/tmp/uniclaw-vision.sock` on disk
- **THEN** `StartAsync` removes the stale file before launching the new process

### Requirement: Environment variable overrides

`PythonVisionService` SHALL support environment variable overrides for socket path (`UNICLAW_VISION_SOCK`), TCP port (`UNICLAW_VISION_PORT`), and uvicorn path (`UNICLAW_UVICORN_PATH`).

#### Scenario: Custom socket path

- **WHEN** `UNICLAW_VISION_SOCK` is set to `/custom/path.sock`
- **THEN** the UDS endpoint uses that path instead of the default
