# Tasks: Local Vision Provider

## 1. Python — label-mapping.json (shared config)

- [x] 1.1 Create `tools/local_vision/label-mapping.json` with schema `uniclaw.labelMapping.v1`, default YOLO→AI type mappings, `nonItemLabels: ["popup"]`, `spatial: {level1MaxY: 0.08, edgeThreshold: 0.92, roiPadding: {x: 0.15, y: 0.10, minPx: 8, maxPx: 64}}`, and `detection: {confidence: 0.35}`
- [x] 1.2 Add Python unit test: config loads successfully, `edgeThreshold` read correctly, `detection.confidence` read correctly

## 2. Python — backends.py ROI-OCR additions

- [x] 2.1 Add `run_yolo_on_image(image: Image.Image, *, model_path, image_size, confidence, device) -> list[Detection]` — PIL memory inference with module-level model cache (`_get_yolo_model`) — V20
- [x] 2.2 Add `run_ocr_on_crops(image, detections, *, language, padding, max_workers) -> list[list[OcrToken]]` — ThreadPool parallel crop + OCR with per-thread `threading.local()` PaddleOCR instances — V14
- [x] 2.3 Add `_run_ocr_on_pil` — ndarray direct pass to `_call_paddle_ocr`, file fallback only for incompatible versions; `np.asarray(crop)[:, :, ::-1]` RGB→BGR conversion — V21
- [x] 2.4 Modify `_call_paddle_ocr(ocr, source: Path | np.ndarray)` — Path→str normalization, ndarray passthrough — V21
- [x] 2.5 Add `warmup_ocr(language)` — init thread-local PaddleOCR instance; add module-level `ThreadPoolExecutor` with warmup dummy tasks during lifespan — R-13
- [x] 2.6 Add `_roi_padding_px(box_width, box_height) -> int` — computes padding from `spatial.roiPadding` config (x/y ratio, min/max clamp) — R-14
- [x] 2.7 Preserve existing `run_yolo` (CLI file-path) and `run_paddle_ocr` — shared model cache with new functions
- [x] 2.8 Add Python unit tests: `run_yolo_on_image` returns valid detections (V20), `run_ocr_on_crops` returns aligned token lists (V14), ndarray path no temp files (V21), `_roi_padding_px` computes from config (R-14)

## 3. Python — fusion.py additions

- [x] 3.1 Add `fuse_evidence_from_crops(detections, crops_ocr, *, image_width, image_height, promote_unmatched_ocr=False) -> dict` — direct zip association, no spatial matching — V15
- [x] 3.2 Preserve `_apply_chevron_heuristic` (same-row text_block → menu_item)
- [x] 3.3 Ensure `promote_unmatched_ocr=False` prevents OCR-only token promotion to candidates — V27
- [x] 3.4 Add Python unit tests: output candidate count == detection count (V15), `promote_unmatched_ocr=False` blocks promotion (V27), evidence schema fields present (V11)

## 4. Python — server.py FastAPI service

- [x] 4.1 Create `tools/local_vision/server.py` with FastAPI app, `OMP_NUM_THREADS=4` before imports (D-18), lifespan loading label-mapping.json + model warmup
- [x] 4.2 Implement `POST /v1/analyze` — BytesIO→YOLO→ROI OCR→fuse→scrollHints→evidence JSON + `Server-Timing` header (yolo/ocr/fusion/scroll durations)
- [x] 4.3 Implement `GET /health` — `{"status": "ok", "warm": true/false}`, warm=false until lifespan warmup completes — R-9
- [x] 4.4 Add `_scroll_hints(candidates)` — `totalCandidates`/`candidatesNearBottom`/`scrollbarDetected`, threshold from `spatial.edgeThreshold` (V22)
- [x] 4.5 Add `_metadata(width, height)` — schema version, pipeline info, models, configHash (SHA-256) — R-6
- [x] 4.6 Add `gc.collect()` per request (D-4)
- [x] 4.7 Add Python unit tests: health returns warm (V10), analyze returns evidence with candidates (V11), scrollHints present (V12), Server-Timing header present (V13), edgeThreshold read from config (V22)

## 5. Python — requirements.txt

- [x] 5.1 Add `fastapi` and `uvicorn[standard]` to `tools/local_vision/requirements.txt`

## 6. C# — UniClaw.LocalVisionProvider project

- [x] 6.1 Create `src/UniClaw.LocalVisionProvider/` project (class library), add to solution `src/UniClaw.Core.sln`, reference `UniClaw.Core`
- [x] 6.2 Implement `LabelMappingConfig` — deserialize from JSON, validate all mapping values against `ElementTypeMapper.IsValidType()` at construction (fail-fast) — V1, V2
- [x] 6.3 Implement `LocalVisionEvidence` + `LocalVisionProvider` — `IModelProvider` with HTTP POST to Python, graceful failure (`Success=false` on non-2xx, no throw) — V26
- [x] 6.4 Implement 4-step mapping pipeline: label mapping (V1, V3), Y-axis clustering (V5), scroll gate detection (V6, V7, V24), popup detection
- [x] 6.5 Implement `PageAnalysisDto` serializer — `[JsonPropertyName]` on multi-word keys: `level1_dir`, `level1_menus`, `level2_dir`, `level2_menus`, `current_path`, `is_popup`, `popup_info`, `close_button`, `back_button`, `has_scroll`, `is_end_of_list` — V19, V23
- [x] 6.6 Implement `Server-Timing` parsing → `ITraceRecorder.RecordEventAsync` sub-spans (ai.yolo, ai.ocr, ai.fusion, ai.scroll) — uses staged `trace-span-helpers` extensions

## 7. C# — VisionScreenStateProvider

- [x] 7.1 Create `src/UniClaw.Core/Traversal/VisionScreenStateProvider.cs` — `sealed class` implementing `IScreenStateProvider`, injected `Func<PageAnalysis?>`, delegate HasScroll/IsEndOfList to PageAnalysis — V8
- [x] 7.2 Ensure NOT implementing `IObservableScreenStateProvider` — V9
- [x] 7.3 `GetScrollProgress()` returns 0.0, `GetScrollSwipeConfig()` returns null
- [x] 7.4 Verify `ArchitectureGuardTests.UniBrain_DoesNotReferenceTraversal` still passes (file in Traversal/, not UniBrain/) — V16

## 8. C# — PythonVisionService (Device layer)

- [x] 8.1 Create `src/UniClaw.Device/IPythonVisionService.cs` — `IAsyncDisposable` with `HttpClient`, `StartAsync(ct)`, `IsRunning`
- [x] 8.2 Create `src/UniClaw.Device/PythonVisionService.cs` — process lifecycle, UDS/TCP dual-mode HttpClient factory, health-check gating on `warm:true`, auto-restart with backoff (0/500ms/1s/3s/10s, max 5), residual socket cleanup — R-9
- [x] 8.3 Env var overrides: `UNICLAW_VISION_SOCK`, `UNICLAW_VISION_PORT`, `UNICLAW_UVICORN_PATH`

## 9. C# — ScrollSwipeConfig + InterceptionHandler

- [x] 9.1 Add `int MaxEmptyScrollRetries = 1` to `ScrollSwipeConfig` — R-12
- [x] 9.2 Update `InterceptionHandler.TryHandleScrollAsync` to use `MaxEmptyScrollRetries` for consecutive empty-diff confirmation before end-of-list (minimal change, R-12 invariant)
- [x] 9.3 Empty-frame immediately retry without consuming budget

## 10. C# — SpanTypes catalog (ai.* timing spans)

- [x] 10.1 Add `AiYolo = "ai.yolo"`, `AiOcr = "ai.ocr"`, `AiFusion = "ai.fusion"`, `AiScroll = "ai.scroll"` to `SpanTypes` catalog
- [x] 10.2 Verify `ArchitectureGuardTests` span count assertion updated (catalog grows by 4)

## 11. C# — Host assembly wiring

- [x] 11.1 Register `LocalVisionProvider` in `HostCommands.CreateProviders()` under key `"local-vision"`, wrapped in `ObservingModelProvider`
- [x] 11.2 Route `analyze_visual` → `"local-vision"` when `UNICLAW_VISION_MODE=local`
- [x] 11.3 Wire `PythonVisionService` lifecycle (start before engine, dispose on host shutdown)
- [x] 11.4 Inject `VisionScreenStateProvider` when using local vision mode (no UIA available)

## 12. C# — Unit tests (LocalVisionProvider)

- [x] 12.1 `LabelMappingConfig` deserialization test — V1 (valid mapping loads)
- [x] 12.2 `LabelMappingConfig` invalid value → `DomainValidationException` — V2
- [x] 12.3 Unknown YOLO label → default `"text"` + warning log — V3
- [x] 12.4 Mock evidence (12 candidates) → `MapToPageAnalysisDto` valid output with items, level1_menus — V4
- [x] 12.5 Y<0.08 candidates → `level1_menus`, rest → `items` — V5
- [x] 12.6 `scrollHints.totalCandidates=15, scrollbarDetected=true` → `has_scroll: true` — V6
- [x] 12.7 `scrollHints.candidatesNearBottom=0` → `is_end_of_list: true` — V7
- [x] 12.8 Empty recognition (`totalCandidates=0`) → `has_scroll: true, is_end_of_list: false` — V24
- [x] 12.9 Golden sample contract test: provider output vs `HostCommands.SettingsAnalysisJson` structure — V23
- [x] 12.10 Horizontal layout candidates → `level1_dir` is `"left"` or `"right"` — V25
- [x] 12.11 HTTP non-2xx → `ModelResponse.Success=false` (no throw) — V26
- [x] 12.12 Multi-word JSON keys snake_case verified — V19

## 13. C# — Unit tests (VisionScreenStateProvider)

- [x] 13.1 `HasScroll()` delegates to `PageAnalysis.HasScroll` — V8
- [x] 13.2 `IsEndOfList()` delegates to `PageAnalysis.IsEndOfList`
- [x] 13.3 Reflection assert: does NOT implement `IObservableScreenStateProvider` — V9

## 14. C# — Unit tests (ScrollSwipeConfig + InterceptionHandler)

- [x] 14.1 `ScrollSwipeConfig` default `MaxEmptyScrollRetries` == 1
- [x] 14.2 `ScrollSwipeConfig.MaxEmptyScrollRetries=0` restores immediate conclusion
- [x] 14.3 Scroll retry logic: N consecutive empty diffs required (covered by existing `ScrollLoopTerminationTests`: `TryHandleScroll_AllSeen_Stops`, `TryHandleScroll_AccumulatesSeenAcrossScrolls_UntilExhausted`)

## 15. Architecture & Integration

- [x] 15.1 Run `ArchitectureGuardTests` — all pass (V16): `UniBrain_DoesNotReferenceTraversal`, `IScreenStateProvider_Has4Methods`, method count locks, namespace isolation
- [x] 15.2 Verify Core project has no `Process` reference, no `PythonVisionService` using — V17
- [x] 15.3 Verify Device project has no `IModelProvider`, `PageAnalysisDto`, `ElementTypeMapper` using — V18
- [x] 15.4 Verify `PageAnalyzer` zero changes (diff is empty) — R-1 invariant
- [x] 15.5 Verify `InterceptionHandler` minimal changes only (scroll retry logic, no structural changes)
