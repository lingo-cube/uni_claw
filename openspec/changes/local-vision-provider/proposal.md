# Proposal: Local Vision Provider

## Why

`tools/local_vision/` already has a complete YOLO+PaddleOCR pipeline (`analyze.py` + `fusion.py`) producing `uniclaw.localVisionEvidence.v1` evidence JSON, but with **zero C# integration** — it can only be invoked via CLI one image at a time. Integrating it as an `IModelProvider` enables the engine to switch between cloud AI and local vision without modifying `PageAnalyzer`, reducing cloud API costs and latency for visual understanding of mobile app screens.

## What Changes

- **New `UniClaw.LocalVisionProvider` project**: Implements `IModelProvider` via HTTP to a Python FastAPI service. Maps YOLO+OCR evidence JSON → `PageAnalysisDto`. Independent assembly aligned with existing `UniClaw.ClaudeProvider` / `UniClaw.DeepSeekProvider` pattern.
- **New Python FastAPI service** (`tools/local_vision/server.py`): Long-running HTTP service wrapping the existing YOLO+PaddleOCR pipeline. ROI-crop OCR (zero disk I/O), multi-threaded with per-thread PaddleOCR instances. Returns `uniclaw.localVisionEvidence.v1` JSON + `Server-Timing` header.
- **New `PythonVisionService`** (`UniClaw.Device`): Manages Python process lifecycle — start, health-check, auto-restart with backoff, UDS/TCP dual-mode HttpClient factory. Environment-variable-configurable socket/port.
- **New `VisionScreenStateProvider`** (`UniClaw.Core/Traversal/`): Thin wrapper reading `HasScroll`/`IsEndOfList` from `PageAnalysis`. Does NOT implement `IObservableScreenStateProvider` → automatically falls through to engine's seen-set diffing safe path.
- **New `label-mapping.json`** (`tools/local_vision/`): Shared configuration (C# + Python) mapping YOLO labels → AI element types. Single source of truth for spatial parameters (`level1MaxY`, `edgeThreshold`, `roiPadding`) and detection confidence threshold.
- **Modified `InterceptionHandler.TryHandleScrollAsync`**: Minimal change — N consecutive empty scroll diffs before confirming end-of-list (configurable via `ScrollSwipeConfig.MaxEmptyScrollRetries`, default 1 = 2 confirmations). Empty frames immediately retry without consuming budget.
- **Modified `ScrollSwipeConfig`**: New `MaxEmptyScrollRetries` field (int, default 1).
- **Trace span helpers** (already staged in `openspec/changes/trace-span-helpers/`): `ITraceRecorder` extensions used by `LocalVisionProvider` to write `Server-Timing` sub-spans.

## Capabilities

### New Capabilities

- `local-vision-provider`: C# `IModelProvider` implementation that sends screenshots to a local Python vision service via HTTP and maps YOLO+OCR evidence to `PageAnalysisDto`. Includes label mapping, Y-axis menu clustering, scroll gate detection, popup detection. Graceful failure (returns `Success=false`, never throws). Independent assembly at `src/UniClaw.LocalVisionProvider/`.
- `python-vision-service`: Python FastAPI long-running service (`tools/local_vision/server.py`) + C# process manager (`UniClaw.Device/PythonVisionService`). UDS/TCP dual-mode, auto-restart with backoff, health-check gating (`warm` field), `Server-Timing` response header. Zero-disk I/O pipeline (PIL BytesIO → YOLO → ROI crop → ndarray OCR).
- `vision-screen-state-provider`: Thin `IScreenStateProvider` implementation at `UniClaw.Core/Traversal/` that reads scroll state from `PageAnalysis`. Does NOT implement `IObservableScreenStateProvider` (safe-path fallback). Complements `AdbScreenStateProvider` for non-UIA scenarios.
- `label-mapping-config`: Shared JSON configuration (`tools/local_vision/label-mapping.json`) consumed by both C# and Python. YOLO label → AI type mappings, spatial parameters (`level1MaxY`, `edgeThreshold`, `roiPadding`), detection confidence. C# validates at construction (fail-fast). Python reads at startup.
- `roi-ocr-pipeline`: Python ROI-crop + multi-threaded OCR (`backends.py` additions). `run_yolo_on_image` (PIL memory, model cache), `run_ocr_on_crops` (ThreadPool, per-thread PaddleOCR instances via `threading.local()`), `fuse_evidence_from_crops` (direct zip association, no spatial matching). Existing `run_yolo`/`run_paddle_ocr`/`fuse_evidence` preserved for CLI mode.

### Modified Capabilities

- `scroll-swipe-config`: New `MaxEmptyScrollRetries` field (int, default 1). Controls how many consecutive empty-scroll-diff observations are required before `IsEndOfList` is confirmed. 0 restores current behavior (immediate conclusion).
- `trace-span`: `LocalVisionProvider` consumes `ITraceRecorder` extension methods (from staged `trace-span-helpers` change) to write YOLO/OCR/fusion/scroll timing sub-spans parsed from `Server-Timing` header.
- `screen-state-provider`: New `VisionScreenStateProvider` implementation. No interface changes — additive only.
- `model-provider`: New `LocalVisionProvider` implementation. No interface changes — additive only.

## Impact

- **New projects**: `src/UniClaw.LocalVisionProvider/` (C# class library), `UniClaw.Device` additions (`PythonVisionService.cs`, `IPythonVisionService.cs`)
- **New files**: `tools/local_vision/server.py`, `tools/local_vision/label-mapping.json`
- **Modified files**: `tools/local_vision/backends.py` (new functions, existing preserved), `tools/local_vision/fusion.py` (new `fuse_evidence_from_crops`), `src/UniClaw.Device/` (new service), `src/UniClaw.Core/Traversal/VisionScreenStateProvider.cs` (new), `src/UniClaw.Core/Traversal/InterceptionHandler.cs` (scroll retry logic), `ScrollSwipeConfig` (new field)
- **Dependencies**: `fastapi`, `uvicorn[standard]` added to Python `requirements.txt`
- **Architecture invariants preserved**: Core has no `Process` reference; Device has no `IModelProvider`/`PageAnalysis` reference; Python independent of C#/ADB; dependency direction `Host → Device → Core`, `Core → Core interfaces`
