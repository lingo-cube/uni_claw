## Purpose
C# `IModelProvider` implementation that sends screenshots to a local Python FastAPI vision service via HTTP and maps YOLO+OCR evidence JSON to `PageAnalysisDto` for consumption by `PageAnalyzer`.

## Requirements

### Requirement: LocalVisionProvider implements IModelProvider as independent assembly

`LocalVisionProvider` SHALL be a `sealed class` in project `UniClaw.LocalVisionProvider` implementing `IModelProvider`, constructed with `HttpClient http`, `ITraceRecorder? traceRecorder`, and `string? labelMappingConfigPath`. `ProviderId` SHALL return `"local-vision"`.

`CompleteVisionAsync` SHALL POST the raw image bytes to `/v1/analyze` with `Content-Type: image/jpeg`. On HTTP 2xx, it SHALL deserialize the response body as `LocalVisionEvidence`, map it to `PageAnalysisDto` via the 4-step mapping pipeline, serialize to JSON, and return `ModelResponse(Success: true, LatencyMs: elapsed, Mode: "vision", InputTokens: 0, OutputTokens: 0)`. On HTTP non-2xx, transport error, or timeout, it SHALL return `ModelResponse(Success: false, ErrorMessage: ...)` without throwing exceptions.

`CompleteTextAsync` and `CompleteMultimodalAsync` SHALL throw `NotImplementedException`.

`LocalVisionProvider` SHALL NOT reference `PythonVisionService`, `System.Diagnostics.Process`, `ADB`, or any Device-layer types. Its only external dependency SHALL be `HttpClient` (injected).

#### Scenario: Successful vision analysis returns PageAnalysisDto JSON

- **WHEN** `CompleteVisionAsync` is called with valid JPEG bytes and Python returns 200 with valid evidence JSON
- **THEN** `ModelResponse.Success` is true, `Mode` is "vision", `LatencyMs` is non-zero, `ProviderId` is "local-vision", and `Content` is valid `PageAnalysisDto` JSON that `PageAnalyzer` can deserialize

#### Scenario: HTTP failure is graceful not thrown

- **WHEN** Python returns HTTP 500 or connection is refused
- **THEN** `ModelResponse.Success` is false, `ErrorMessage` is set, and no exception is thrown

#### Scenario: Text and multimodal throw NotImplementedException

- **WHEN** `CompleteTextAsync` or `CompleteMultimodalAsync` is called
- **THEN** `NotImplementedException` is thrown

#### Scenario: Provider lives in independent assembly

- **WHEN** UniClaw.LocalVisionProvider assembly is inspected
- **THEN** it SHALL NOT reference `UniClaw.Device` and SHALL NOT contain `using System.Diagnostics`

### Requirement: 4-step mapping pipeline from evidence to PageAnalysisDto

`LocalVisionProvider` SHALL implement a 4-step mapping pipeline:

**Step 1 — YOLO label → AI type**: Each candidate's `type` (YOLO label) SHALL be looked up in `LabelMappingConfig.Mappings`. Unknown labels SHALL default to `"text"` with a warning log. The mapped value SHALL be validated against `ElementTypeMapper.IsValidType()`.

**Step 2 — Y-axis clustering → menus**: Candidates with `center.y < spatial.level1MaxY` SHALL be placed in `level1_menus`. When level1_menus is non-empty and the X-variance exceeds Y-variance, `level1_dir` SHALL be `"left"` (mean X < 0.5) or `"right"` (mean X ≥ 0.5). When level1_menus is empty, `level1_dir` SHALL be `null`. `level2_dir` SHALL be `null` and `level2_menus` SHALL be an empty array. Menus `active` SHALL default to `false`.

**Step 3 — Scroll gate detection**: `has_scroll` SHALL be true when `totalCandidates > estimatedVisibleCapacity` OR `scrollbarDetected` is true. When `totalCandidates == 0` or the estimate is ambiguous, `has_scroll` SHALL be true and `is_end_of_list` SHALL be false (conservative bias toward scrollable). `is_end_of_list` SHALL only be true when `totalCandidates > 0` AND `candidatesNearBottom == 0` AND `scrollbarDetected` is false. `estimatedVisibleCapacity` SHALL equal `image_height / avgItemHeight` where `avgItemHeight` is the median candidate bounding-box height.

**Step 4 — Popup detection**: When any candidate has `type == "popup"`, `is_popup` SHALL be true and the nearest candidate SHALL be identified as `close_button`.

The output `PageAnalysisDto` JSON SHALL use `[JsonPropertyName]` attributes on all multi-word keys (snake_case), matching the `PageAnalyzer.PageAnalysisDto` deserialization contract.

#### Scenario: YOLO label mapped to AI type

- **WHEN** evidence contains a candidate with `type: "switch"`
- **THEN** the output item has `type: "toggle"` (per label-mapping.json default)

#### Scenario: Unknown YOLO label defaults to text

- **WHEN** evidence contains a candidate with `type: "unknown_widget"` not in label-mapping.json
- **THEN** the output item has `type: "text"` and a warning is logged

#### Scenario: Top-region candidates become level1_menus

- **WHEN** evidence contains 3 candidates with `center.y < 0.08` and X-variance > Y-variance
- **THEN** `level1_menus` contains those 3 items, `level1_dir` is "left" or "right", and they are excluded from `items`

#### Scenario: No top-region candidates means null level1_dir

- **WHEN** evidence contains no candidates with `center.y < 0.08`
- **THEN** `level1_dir` is null, `level1_menus` is an empty array

#### Scenario: Scroll detected from candidate overflow

- **WHEN** `scrollHints.totalCandidates` is 15 and estimated visible capacity is 10
- **THEN** `has_scroll` is true

#### Scenario: Scroll detected from scrollbar

- **WHEN** `scrollHints.scrollbarDetected` is true even with few candidates
- **THEN** `has_scroll` is true

#### Scenario: End of list detected

- **WHEN** `scrollHints.totalCandidates` is 8, `candidatesNearBottom` is 0, and `scrollbarDetected` is false
- **THEN** `is_end_of_list` is true

#### Scenario: Empty recognition biases toward scrollable

- **WHEN** `scrollHints.totalCandidates` is 0
- **THEN** `has_scroll` is true and `is_end_of_list` is false

#### Scenario: Popup detected

- **WHEN** any candidate has `type: "popup"`
- **THEN** `is_popup` is true and `close_button` references the nearest candidate

#### Scenario: PageAnalysisDto JSON keys match contract

- **WHEN** the serialized DTO JSON is inspected
- **THEN** multi-word keys use snake_case (`"level1_dir"`, `"has_scroll"`, `"is_end_of_list"`, `"is_popup"`, `"close_button"`, `"back_button"`, `"popup_info"`, `"current_path"`, `"level1_menus"`, `"level2_menus"`, `"level2_dir"`)

#### Scenario: Golden sample contract test

- **WHEN** Provider output JSON is compared to `HostCommands.SettingsAnalysisJson` structure
- **THEN** `level1_dir` is one of `null|"left"|"right"|"top"|"bottom"`, `current_path` is an array, items contain `type` field, `level2_dir`/`level2_menus` are present

### Requirement: Items contain name, type, coordinate, and parent fields

Each item in the output `items` array SHALL contain `name` (OCR text or empty string), `type` (mapped AI type), `coordinate` (normalized `x`/`y` from candidate center), and `parent` (v1 always null — engine has no consumer). Items `active` SHALL NOT be present on items (active is menu-only).

#### Scenario: Item has required fields

- **WHEN** a candidate with OCR text "Settings" and type mapped to "menu_item" is processed
- **THEN** the output item has `name: "Settings"`, `type: "menu_item"`, `coordinate: {x, y}`, and `parent: null`

### Requirement: Construct-time label mapping validation

`LocalVisionProvider` SHALL load `label-mapping.json` at construction and validate every mapping value against `ElementTypeMapper.IsValidType()`. An invalid value SHALL throw `DomainValidationException` immediately (fail-fast). The config path SHALL be resolved from: constructor argument → `UNICLAW_LABEL_MAPPING` environment variable → `"tools/local_vision/label-mapping.json"` default.

#### Scenario: Valid mapping loads successfully

- **WHEN** `label-mapping.json` contains `"switch": "toggle"` and `ElementTypeMapper.IsValidType("toggle")` is true
- **THEN** construction succeeds without exception

#### Scenario: Invalid mapping value fails fast

- **WHEN** `label-mapping.json` contains `"some_label": "invalid_type"` and `ElementTypeMapper.IsValidType("invalid_type")` is false
- **THEN** `DomainValidationException` is thrown at construction with FieldName set to the invalid value

### Requirement: Server-Timing parsed into trace sub-spans

When `ITraceRecorder` is injected (non-null), `LocalVisionProvider` SHALL parse the `Server-Timing` response header (format: `yolo;dur=X, ocr;dur=Y, fusion;dur=Z, scroll;dur=W`) and record one event span per timing entry as `ai.call` children. The span type SHALL follow the pattern `ai.<stage>` (e.g. `ai.yolo`, `ai.ocr`, `ai.fusion`, `ai.scroll`). When `ITraceRecorder` is null, timing SHALL be silently skipped.

#### Scenario: Server-Timing parsed into sub-spans

- **WHEN** Python returns `Server-Timing: yolo;dur=45.2, ocr;dur=68.7` and `ITraceRecorder` is available
- **THEN** two event spans with spanType `ai.yolo` (dur=45.2) and `ai.ocr` (dur=68.7) are recorded as children of the `ai.call` span

#### Scenario: Missing Server-Timing is no-op

- **WHEN** Python response has no `Server-Timing` header
- **THEN** no sub-spans are recorded and `ModelResponse` is still returned successfully

#### Scenario: Null recorder skips timing

- **WHEN** `ITraceRecorder` is null
- **THEN** `Server-Timing` parsing is skipped entirely and no exception is thrown
