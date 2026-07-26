## ADDED Requirements

### Requirement: IScreenCapture defines the screenshot capture seam

`IScreenCapture` SHALL be a Core interface in namespace `UniClaw.Core.UniBrain` (co-located with its sole Core consumer `PageAnalyzer`). It SHALL define exactly one method:

- `Task<byte[]> CaptureAsync(CancellationToken ct = default)` — captures the current screen and returns the image as a PNG/JPEG byte stream.

`IScreenCapture` SHALL be a pure abstraction: Core SHALL hold only the interface; concrete device implementations (e.g. `AdbScreenCapture`) SHALL live in the host layer and SHALL NOT be referenced from Core (§12-B screenshot ownership). Placement in UniBrain (not Traversal) preserves D-130 Locked charter invariant "UniBrain namespace does not depend on StateMachine/Traversal" — `PageAnalyzer` consumes the seam without importing `UniClaw.Core.Traversal`. `IPageAnalyzer.AnalyzeCurrentPageAsync` SHALL NOT take a screenshot parameter — the screenshot is obtained via `IScreenCapture` inside the provider-side implementation, leaving the `IPageAnalyzer` signature unchanged.

#### Scenario: IScreenCapture is a Core-only abstraction

- **WHEN** the `src/UniClaw.Core/UniBrain/` directory is inspected
- **THEN** it contains `IScreenCapture` with a single `CaptureAsync(CancellationToken)` method returning `byte[]`, and Core references no concrete capture implementation

#### Scenario: IPageAnalyzer signature is unchanged

- **WHEN** `IPageAnalyzer.AnalyzeCurrentPageAsync` is inspected
- **THEN** its signature remains `Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)` — no screenshot parameter is added

### Requirement: PageAnalyzer real implementation consumes IModelProvider, IPromptLibrary, and IScreenCapture

`PageAnalyzer` SHALL be a `sealed class` implementing `IPageAnalyzer`. It SHALL be constructed with `IModelProvider modelProvider`, `IPromptLibrary promptLibrary`, and `IScreenCapture screenCapture`. It SHALL be provider-agnostic — it SHALL NOT reference any concrete provider type (DeepSeek / Claude / Mock) and SHALL NOT call `IModelRouter.Resolve` from within its method bodies; the `IModelProvider` it consumes is the product of assembly-time `router.Resolve(ModelCapabilities.AnalyzeVisual)` (already wrapped by `ObservingModelProvider`). A null `modelProvider`, `promptLibrary`, or `screenCapture` SHALL throw `DomainValidationException`.

`AnalyzeCurrentPageAsync` SHALL:
1. Capture the screenshot via `screenCapture.CaptureAsync(ct)` (the byte stream consumed by the multimodal transport).
2. Obtain the prompt template via `promptLibrary.GetTemplate(ModelCapabilities.AnalyzeVisual)`; if null, throw `DomainValidationException` indicating the prompt template is missing (before any model call).
3. Resolve the template with an empty variable map (the screenshot travels as the `CompleteVisionAsync` byte parameter, not as a prompt variable).
4. Build a `ModelRequest` with `Prompt = resolved.User`, `SystemPrompt = resolved.System`, `Schema = Schemas.AnalyzeVisual`, `Capability = ModelCapabilities.AnalyzeVisual`.
5. Call `modelProvider.CompleteVisionAsync(modelRequest, screenshotBytes, ct)`.
6. If `resp.Success == false`, throw `DomainValidationException` carrying `resp.ErrorMessage`.
7. Deserialize `resp.Content` (JSON) into an internal `PageAnalysisDto`, then map it to a `PageAnalysis`.

The item mapping SHALL derive `expected_action`, `expects_page_change`, and `expects_state_change` deterministically from the item's `type` — the AI SHALL produce only `type`; the prompt SHALL NOT contain a type→action prose mapping. Specifically, for each item: `Type = ElementTypeMapper.ToMenuItemType(dto.Type)`, `ExpectedAction = ElementTypeMapper.ToExpectedAction(dto.Type)`, and the change flags derived from `ExpectedAction` as: `Navigate` → `ExpectsPageChange=true, ExpectsStateChange=false`; `Action` → `ExpectsPageChange=true, ExpectsStateChange=false`; `Toggle` → `ExpectsPageChange=false, ExpectsStateChange=true`; `None` → both `false`. An unrecognized `type` string SHALL throw `DomainValidationException`.

Mapping SHALL further enforce fail-fast: `Coordinate(x, y)` with x or y outside [0, 1] SHALL throw `DomainValidationException`; an unrecognized `level1_dir` / `level2_dir` value SHALL throw `DomainValidationException` via `Direction` parsing; a null `Items` collection or an item with an empty/whitespace `type` SHALL throw `DomainValidationException`.

The two other `IPageAnalyzer` methods (`FindAppEntryAsync`, `VerifyPageTypeAsync`) SHALL throw `NotImplementedException` carrying a message indicating the method is pending a future slice.

The `Schemas.AnalyzeVisual` constant SHALL declare a JSON schema mirroring `PageAnalysisDto` whose `items` list only `name` / `type` / `coordinate` / `parent` (it SHALL NOT list `expected_action` / `expects_page_change` / `expects_state_change`).

#### Scenario: Happy path derives PageAnalysis with ElementTypeMapper derivation

- **WHEN** `AnalyzeCurrentPageAsync` is called, `screenCapture.CaptureAsync` returns specific bytes, the resolved template is present, and the resolved provider returns JSON containing an item with `type="switch"` plus `level1_dir="left"`, `level1_menus`, `current_path`, `is_popup=false`, `has_scroll=false`, `is_end_of_list=false`
- **THEN** returns a `PageAnalysis` whose item has `Type=Switch`, `ExpectedAction=Toggle`, `ExpectsStateChange=true`, `ExpectsPageChange=false`; `Level1Dir=Left`; and the byte stream passed to `CompleteVisionAsync` equals the bytes from `CaptureAsync`

#### Scenario: ElementTypeMapper derivation covers the four ExpectedAction branches

- **WHEN** the resolved provider returns items whose `type` strings map to each of `Navigate`, `Action`, `Toggle`, and `None`
- **THEN** the resulting `MenuItem` flags match the §12-A derivation table: `Navigate`/`Action` set `ExpectsPageChange=true`; `Toggle` sets `ExpectsStateChange=true`; `None` sets both `false`

#### Scenario: Missing prompt template fails fast

- **WHEN** `promptLibrary.GetTemplate(AnalyzeVisual)` returns null
- **THEN** `DomainValidationException` is thrown before any screenshot capture or model call is made

#### Scenario: Model call failure propagates

- **WHEN** the resolved provider returns a `ModelResponse` with `Success = false` and `ErrorMessage`
- **THEN** `DomainValidationException` is thrown carrying the ErrorMessage

#### Scenario: Invalid type fails fast

- **WHEN** the resolved provider returns an item with `type="not_a_real_type"` (not recognized by `ElementTypeMapper`)
- **THEN** `DomainValidationException` is thrown

#### Scenario: Out-of-range coordinate fails fast

- **WHEN** the resolved provider returns an item whose `coordinate` has `x=1.5`
- **THEN** `DomainValidationException` is thrown (Coordinate 0-1 validation)

#### Scenario: Provider-agnostic with no in-method routing

- **WHEN** `PageAnalyzer` is constructed with an `IModelProvider` that is the product of assembly-time `router.Resolve(AnalyzeVisual)`
- **THEN** `AnalyzeCurrentPageAsync` calls `CompleteVisionAsync` directly on the injected provider without invoking `IModelRouter.Resolve` and without referencing any concrete provider type

#### Scenario: Vision-mode observation record is produced

- **WHEN** `AnalyzeCurrentPageAsync` runs through an assembly-time router whose `AnalyzeVisual` route targets a provider wrapped by `ObservingModelProvider`, with an `InMemoryTraceRecorder`
- **THEN** the recorder receives an `AICallRecord` with `mode="vision"` and `capability=analyze_visual`

#### Scenario: Other two interface methods are not implemented

- **WHEN** `FindAppEntryAsync` or `VerifyPageTypeAsync` is called on a `PageAnalyzer` instance
- **THEN** `NotImplementedException` is thrown indicating the method is pending a future slice
