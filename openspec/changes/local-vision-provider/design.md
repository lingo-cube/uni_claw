# Design: Local Vision Provider

## Context

The `tools/local_vision/` directory already contains a working YOLO+PaddleOCR pipeline (`analyze.py` + `fusion.py`) producing `uniclaw.localVisionEvidence.v1` evidence JSON. However, it can only be invoked via CLI — one image at a time. The C# engine (`PageAnalyzer` → `IModelProvider.CompleteVisionAsync`) needs a local vision provider to enable zero-cloud-cost visual understanding.

Current architecture: `PageAnalyzer` calls `IModelProvider.CompleteVisionAsync(fullImageBytes)` → provider returns `ModelResponse` with JSON content → `PageAnalyzer` deserializes to `PageAnalysisDto` → maps to `PageAnalysis` domain model. Existing providers: `AnthropicModelProvider` (UniClaw.ClaudeProvider), `DeepSeekModelProvider` (UniClaw.DeepSeekProvider) — both in independent assemblies, wired by `UniBrainFactory` from host-assembled `IReadOnlyDictionary<string, IModelProvider>`.

Key architectural constraints from `ArchitectureGuardTests`:
- `UniBrain/` cannot reference `Traversal/` (test: `UniBrain_DoesNotReferenceTraversal`)
- Core cannot reference `System.Diagnostics.Process`
- Device cannot reference `IModelProvider`, `PageAnalysisDto`, `ElementTypeMapper`
- Dependency direction: `Host → Device → Core`, `Core → Core interfaces`

## Goals / Non-Goals

**Goals:**
- Wrap Python YOLO+OCR pipeline as a long-running FastAPI service
- Implement `IModelProvider` in a new `UniClaw.LocalVisionProvider` assembly (aligned with ClaudeProvider/DeepSeekProvider pattern)
- Provide zero-disk-I/O pipeline: PIL BytesIO → YOLO → ROI crop → ndarray OCR → evidence JSON
- Multi-threaded OCR with per-thread PaddleOCR instances (`threading.local()` — true parallelism via GIL release during C++ inference)
- UDS (macOS/Linux) / TCP (Windows) dual-mode transport with auto-restart
- Shared `label-mapping.json` as single source of truth for YOLO label → AI type mappings and spatial parameters
- `VisionScreenStateProvider` as thin wrapper reading scroll state from `PageAnalysis`
- Graceful failure semantics (return `Success=false`, never throw — consistent with AnthropicModelProvider)
- Conservative scroll gate: single-frame uncertainty biases toward "scrollable", end-of-list confirmed by engine's temporal seen-set diffing

**Non-Goals:**
- Replacing cloud AI for text/multimodal reasoning (`CompleteTextAsync` / `CompleteMultimodalAsync` → `NotImplementedException`)
- Scroll-band OCR (R-5: deferred to v1.1; v1 only ROI-crop OCR)
- Items `parent` inference from bounding-box containment (v1 always null — engine has no consumer)
- Python making cross-frame judgments (scroll timing remains engine responsibility)
- Modifying `PageAnalyzer` (zero changes)

## Decisions

### D-1: Mapping logic in C#, Python returns raw evidence

**Choice**: Python returns YOLO labels + OCR text as-is in evidence JSON. C# `LocalVisionProvider` maps labels → AI types via `label-mapping.json`.

**Rationale**: `ElementTypeMapper` is the C# single source of truth for AI types. Mapping logic is xUnit-testable. Different vehicle head units may have different YOLO labels — changing JSON doesn't require Python redeployment.

**Alternatives considered**: Python doing the mapping → would duplicate `ElementTypeMapper` logic, untestable from C#, and require Python redeployment for label changes.

### D-2: Provider in independent assembly

**Choice**: `UniClaw.LocalVisionProvider` as a separate C# project (not in Core, not in Device).

**Rationale**: Aligns with existing pattern (`UniClaw.ClaudeProvider`, `UniClaw.DeepSeekProvider` are independent assemblies). `UniBrainFactory` only receives host-assembled provider dictionary. Core stays "pure logic, zero I/O." Provider needs `HttpClient` — that's transport, not core logic.

### D-3: UDS (Unix) / TCP (Windows) dual-mode transport

**Choice**: Unix Domain Socket on macOS/Linux, TCP loopback on Windows. Environment variables `UNICLAW_VISION_SOCK` / `UNICLAW_VISION_PORT` override defaults.

**Rationale**: UDS has lower latency and no port conflicts on dev machines. Windows lacks UDS server support in Python's uvicorn. Env var overrides allow CI/test customization.

### D-4: ROI-crop OCR with threading.local()

**Choice**: After YOLO detection, crop each bounding box region → run OCR only on crops. ThreadPool with `threading.local()` for per-thread PaddleOCR instances.

**Rationale**: Full-image OCR wastes ~80% of compute on non-interactive regions. `threading.local()` avoids PaddleOCR's C++ thread-safety issues (documented in [PaddleOCR#16238](https://github.com/PaddlePOWER/PaddleOCR/issues/16238)). C++ inference releases GIL → true parallelism.

**Performance**: 12 detections: ~40ms (4 workers) vs ~800ms (full-image OCR). Spatial matching layer eliminated entirely.

### D-5: Server-Timing header for latency, not JSON body

**Choice**: Python returns timing data in W3C `Server-Timing` response header. C# parses and writes to trace sub-spans. JSON body contains only visual evidence.

**Rationale**: Vision API's responsibility is "what was seen", not "how fast". Separation of concerns. C# can consume timing optionally without affecting JSON schema compatibility.

### D-6: Graceful failure (Success=false, no throw)

**Choice**: When HTTP to Python fails or returns non-2xx, `LocalVisionProvider.CompleteVisionAsync` returns `ModelResponse` with `Success=false` rather than throwing.

**Rationale**: Consistent with `AnthropicModelProvider` behavior. `PageAnalyzer` already has `MaxAnalyzeAttempts=2` retry loop. Throwing would break the existing retry contract.

### D-7: Conservative scroll gating

**Choice**: Single-frame scroll detection biases toward "scrollable". Empty recognition → `has_scroll: true, is_end_of_list: false` (allow swipe attempt). End-of-list confirmed by engine's temporal seen-set diffing (`InterceptionHandler.TryHandleScrollAsync` already implements this with `GetElementIds` using `item.Name` as fingerprint).

**Rationale**: False-positive "end" terminates traversal prematurely. False-positive "scrollable" costs one extra swipe — engine's seen-set diffing catches it. OCR text from local-vision is a natural fingerprint for diffing.

**Alternatives considered**: Making definitive judgments from single frames → would require high confidence thresholds that would cause false "end" detections on real vehicle UIs with variable layouts.

### D-8: Scroll confirmation with configurable retries

**Choice**: `ScrollSwipeConfig.MaxEmptyScrollRetries` (int, default 1) — requires N+1 consecutive empty diffs before confirming end-of-list. 0 restores immediate conclusion. `VisionScreenStateProvider.GetScrollSwipeConfig()` can return this configuration.

**Rationale**: Single empty diff may be transient (loading, animation). Two consecutive confirmations balance latency against false positives. Configurable so different scenarios can tune aggressiveness.

### D-9: Shared label-mapping.json as single source of truth

**Choice**: Single JSON file at `tools/local_vision/label-mapping.json`. Both Python server and C# provider read it. Python reads at startup (lifespan); C# reads at construction (fail-fast validation).

**Rationale**: Eliminates dual-threshold divergence risk. `spatial.edgeThreshold` used by Python for `candidatesNearBottom` and by C# for scroll logic — same value guaranteed. Path overridable via `UNICLAW_LABEL_MAPPING` env var.

### D-10: Trace headers as protocol reservation (v1 not sent)

**Choice**: `X-Uniclaw-Trace-Id` / `X-Uniclaw-Step-Id` headers defined in HTTP protocol. v1: C# does NOT send them (no injection source — `IModelProvider.CompleteVisionAsync` signature lacks trace context). Python transparently passes through and echoes in metadata. Enablement: when per-call context mechanism lands.

**Rationale**: Protocol design upfront avoids breaking changes later. Current observation chain already covered by `ObservingModelProvider` / `AICallRecord`. No dead code — headers are optional on both sides.

### D-11: VisionScreenStateProvider in Traversal/, not UniBrain/

**Choice**: `VisionScreenStateProvider.cs` placed in `src/UniClaw.Core/Traversal/` (alongside `IScreenStateProvider`).

**Rationale**: `UniBrainGuardTests.UniBrain_DoesNotReferenceTraversal` forbids UniBrain directory from referencing Traversal types. Implementing `IScreenStateProvider` requires `using UniClaw.Core.Traversal`. `PageAnalysis` is in `Domain.Models.Content` — Traversal referencing it has no Guard conflict.

### D-12: Python dependency management

**Choice**: `OMP_NUM_THREADS=4` set at module top, BEFORE any numpy/ultralytics/paddleocr import. `gc.collect()` per request. Model warmup in FastAPI lifespan.

**Rationale**: OpenMP thread count is frozen at library initialization. Manual GC mitigates PaddleOCR's known memory leak under sustained load. Lifespan warmup prevents first-request timeout (Ultralytics first load: 5-10s).

### D-13: OCR thread pool — module-level long-lived executor

**Choice**: Module-level `ThreadPoolExecutor` created once, warmed with dummy tasks during lifespan startup (each worker thread initializes its `threading.local` PaddleOCR instance). Requests reuse the same executor.

**Rationale**: Eliminates per-request thread pool creation overhead. Thread-local PaddleOCR instances survive across requests (no per-request GC of model weights). Warmup ensures first real request doesn't pay instance creation cost.

### D-14: ROI padding configurable from label-mapping.json

**Choice**: `spatial.roiPadding: { x: 0.15, y: 0.10, minPx: 8, maxPx: 64 }` in label-mapping.json. Python computes padding as `max(x * box_width, y * box_height, minPx)` clamped to `maxPx`.

**Rationale**: Replaces hardcoded 4px padding that may be inadequate for larger screens. Proportional to box size. Single configuration point shared with C#.

### D-15: YOLO confidence threshold configurable

**Choice**: `detection.confidence` (default 0.35) in label-mapping.json. Python reads at startup. No additional fusion-stage filtering.

**Rationale**: One threshold, one location. Eliminates magic number 0.35 scattered in Python code. Different environments may need different sensitivity.

## Risks / Trade-offs

- **[Memory] PaddleOCR per-thread instances**: 2 workers ≈ 600MB, 4 workers ≈ 1.2GB. Mitigation: default 2 workers (`UNICLAW_OCR_PARALLEL`), single uvicorn worker default. Configurable for high-memory environments.
- **[Startup latency] First YOLO load**: 5-10s for Ultralytics model download/load. Mitigation: FastAPI lifespan warmup before health check reports `warm: true`. `StartAsync` gates on `warm`.
- **[Reliability] Python process crashes**: PaddleOCR/Ultralytics may crash under extreme load. Mitigation: auto-restart with exponential backoff (0ms → 500ms → 1s → 3s → 10s cap), max 5 restarts. Pre-restart health probe (reuse if still alive). Pre-startup unlink residual UDS socket.
- **[Accuracy] YOLO may miss elements**: Detection depends on model quality. Mitigation: conservative scroll gate (uncertainty → allow swipe). Engine's temporal seen-set diffing catches false scrolls. Scroll-band OCR (R-5) deferred to v1.1 for additional coverage.
- **[Portability] UDS vs TCP**: UDS not available on Windows. Mitigation: platform-detection switch, env var overrides. Health check protocol identical regardless of transport.
- **[Breaking] ScrollSwipeConfig field addition**: `MaxEmptyScrollRetries` is additive only (default preserves current effective behavior of 1 confirmation). No existing serialized configs affected.
- **[Coupling] Both C# and Python read label-mapping.json**: If format changes, both sides must be updated. Mitigation: schema version field (`uniclaw.labelMapping.v1`). Python reads at startup (restart picks up changes). C# validates at construction (fail-fast on mismatch).

## Open Questions

1. **Scroll-band OCR (R-5)**: Deferred to v1.1. Trigger: real-vehicle testing shows YOLO missing text lines that cause scroll diff failures. `token.scope` field already reserved.
2. **Python package distribution**: How is the Python environment provisioned on target machines? (venv? Docker? embedded Python?) Out of scope for this design — `PythonVisionService` assumes `uvicorn` on PATH.
3. **Multi-instance**: Could multiple uvicorn workers improve throughput? Not needed for current single-device use case. Single worker default (model ~600MB/process). Documented as configurable.
