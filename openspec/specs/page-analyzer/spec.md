## ADDED Requirements

### Requirement: IPageAnalyzer defines 3 methods for page perception and verification

IPageAnalyzer SHALL define exactly 3 async methods:
- `Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)`
- `Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)`
- `Task<PageTypeVerification> VerifyPageTypeAsync(PageAnalysis pageAnalysis, string expectedType, string? expectedPageName = null, CancellationToken ct = default)`

IPageAnalyzer SHALL NOT include scroll-related methods (HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig). IPageAnalyzer SHALL NOT include VerifyPageWithVisionAsync (Host layer convenience method, YAGNI).

#### Scenario: AnalyzeCurrentPageAsync returns PageAnalysis from screenshot analysis
- **WHEN** IPageAnalyzer.AnalyzeCurrentPageAsync is called
- **THEN** implementation captures screenshot, invokes AI model, returns PageAnalysis or null on failure

#### Scenario: FindAppEntryAsync returns target app icon coordinates
- **WHEN** IPageAnalyzer.FindAppEntryAsync("Settings") is called
- **THEN** returns AppEntryPoint with icon coordinates, or null if not found

#### Scenario: VerifyPageTypeAsync validates page type from metadata
- **WHEN** IPageAnalyzer.VerifyPageTypeAsync(pageAnalysis, "settings_list") is called
- **THEN** returns PageTypeVerification with IsMatch, Confidence, ActualType, Reasoning

#### Scenario: IPageAnalyzer has zero scroll methods
- **WHEN** IPageAnalyzer interface is inspected
- **THEN** it does not contain HasScroll, GetScrollProgress, IsEndOfList, or GetScrollSwipeConfig methods

### Requirement: AppEntryPoint is sealed record class with coordinate fields

AppEntryPoint SHALL be a sealed record class with:
- `string AppName`
- `double X` (normalized 0-1)
- `double Y` (normalized 0-1)
- `double Confidence`

AppEntryPoint SHALL use DomainValidationException for X/Y range validation (0-1) and Confidence range validation (0-1).

#### Scenario: AppEntryPoint validates coordinate ranges
- **WHEN** AppEntryPoint is constructed with X=1.5
- **THEN** DomainValidationException is thrown with FieldName="X" and IllegalValue=1.5

#### Scenario: AppEntryPoint validates confidence range
- **WHEN** AppEntryPoint is constructed with Confidence=-0.1
- **THEN** DomainValidationException is thrown with FieldName="Confidence" and IllegalValue=-0.1

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

### Requirement: PageAnalysis shape contract is test-enforced across observation paths

A Core-defined shape contract (enforced by tests, not prose) SHALL govern both the AI observation path and the UIAutomator observation path. Both paths SHALL fill the contracted fields `Level1Menus` / `Level2Menus` / `Items` / `CurrentPath` / `HasScroll` / `IsEndOfList` to a common rule, so the same `PageAnalysis` record shape is produced regardless of which path observed it. A contract test SHALL run both the AI observation path and the UIAutomator observation path over the same fixture and assert structural equivalence on the fields the runner and safety gate consume. "Mock green" SHALL imply "real-path-shape green": if the contract test passes for the mock/AI path on a fixture, the contract test SHALL also pass for the real/UIAutomator path on that same fixture.

#### Scenario: Both paths fill the contracted fields

- **WHEN** the AI observation path and the UIAutomator observation path each produce a `PageAnalysis` for the same page fixture
- **THEN** both `PageAnalysis` instances populate `Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, and `IsEndOfList`, and neither leaves any contracted field at its default/empty value when the page has that content

#### Scenario: Contract test passes for both paths on the same fixture

- **WHEN** the Core-defined contract test runs both the AI observation path and the UIAutomator observation path over the same fixture
- **THEN** the contract test asserts structural equivalence on the fields the runner and safety gate consume (`Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, `IsEndOfList`) and passes for both paths

#### Scenario: Mock-path shape equivalence implies real-path shape equivalence

- **WHEN** the mock/AI observation path satisfies the shape contract on a given fixture (mock green)
- **THEN** the real/UIAutomator observation path also satisfies the shape contract on that same fixture, so "mock green" implies "real-path-shape green" for the contracted fields

### Requirement: UIAutomator observation path fills Level1Menus and Level2Menus

The UIAutomator observation path SHALL populate `Level1Menus` and `Level2Menus` on the produced `PageAnalysis`, matching the AI observation path. The UIAutomator path SHALL NOT leave `Level1Menus` or `Level2Menus` empty when the page under observation has level-1 or level-2 menus.

#### Scenario: UIAutomator path produces non-empty Level1Menus and Level2Menus where the page has them

- **WHEN** the UIAutomator observation path observes a page that has level-1 and level-2 menus
- **THEN** the resulting `PageAnalysis` has non-empty `Level1Menus` and non-empty `Level2Menus`, matching the shape produced by the AI observation path for the same page

### Requirement: UIAutomator observation path derives Direction from layout

The UIAutomator observation path SHALL derive `Direction` from layout instead of hardcoding `Direction.Left`. The UIAutomator path SHALL NOT assign `Direction.Left` by default without consulting the layout.

#### Scenario: UIAutomator path sets Direction from layout instead of a hardcoded Left

- **WHEN** the UIAutomator observation path observes a page and computes `Direction`
- **THEN** the `Direction` value is derived from the observed layout, not assigned as a hardcoded `Direction.Left` independent of layout

#### Scenario: Left-layout page yields Direction.Left via derivation

- **WHEN** the UIAutomator observation path observes a page whose layout indicates a left direction
- **THEN** the resulting `PageAnalysis.Direction` equals `Direction.Left` as the product of layout derivation, not as a hardcoded default

#### Scenario: Different layout yields the derived Direction value

- **WHEN** the UIAutomator observation path observes a page whose layout indicates a direction other than left
- **THEN** the resulting `PageAnalysis.Direction` equals the value derived from that layout rather than `Direction.Left`

### Requirement: PageAnalysis shape contract guards the runner and safety gate consumers

The runner and the safety gate SHALL consume `PageAnalysis` only through the contracted fields (`Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, `IsEndOfList`). The shape contract SHALL make observation-path failure observable rather than maskable: an observation path that omits a contracted field SHALL fail the contract test rather than produce a silently partial `PageAnalysis` that the runner or safety gate consumes as if it were complete. This supports spec defect D4.

#### Scenario: A path that omits a contracted field fails the contract test

- **WHEN** an observation path produces a `PageAnalysis` that omits a contracted field the runner or safety gate consumes
- **THEN** the Core-defined contract test fails for that path, rather than allowing a silently partial `PageAnalysis` to be consumed by the runner or safety gate

#### Scenario: Runner and safety gate consume only contracted fields

- **WHEN** the runner or the safety gate reads a `PageAnalysis`
- **THEN** it reads only the contracted fields (`Level1Menus`, `Level2Menus`, `Items`, `CurrentPath`, `HasScroll`, `IsEndOfList`), so a path that fails the shape contract cannot be masked by a consumer that tolerates missing fields
