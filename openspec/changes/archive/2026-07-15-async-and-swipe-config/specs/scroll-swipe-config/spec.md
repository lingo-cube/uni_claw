# Capability: Scroll Swipe Config

滑动坐标从硬编码常量提升为可配置 `ScrollSwipeConfig`，支持引擎级默认 + `IVisionProvider` 页面级覆盖。Mock 通过 `SimulatedScreen.WithScrollablePage()` 按页面适配不同滚动区域。

## ADDED Requirements

### Requirement: ScrollSwipeConfig is a sealed record class with normalized coordinates

The system SHALL define `ScrollSwipeConfig` as a `sealed record class` with 5 fields: `StartX`, `StartY`, `EndX`, `EndY` (all `double`, normalized 0-1), and `DurationMs` (int, milliseconds). Default values SHALL be `StartX=0.5, StartY=0.7, EndX=0.5, EndY=0.3, DurationMs=300` — matching the current hardcoded constants.

#### Scenario: Default ScrollSwipeConfig matches current hardcoded values
- **WHEN** `new ScrollSwipeConfig()` is instantiated with no arguments
- **THEN** StartX is 0.5, StartY is 0.7, EndX is 0.5, EndY is 0.3, DurationMs is 300

#### Scenario: ScrollSwipeConfig is sealed record class
- **WHEN** the type declaration of `ScrollSwipeConfig` is inspected
- **THEN** it is `sealed record class`

#### Scenario: ScrollSwipeConfig fields are immutably init-only
- **WHEN** a `ScrollSwipeConfig` instance is created and an attempt is made to reassign one of its fields
- **THEN** the compiler rejects the assignment (record fields are init-only)

### Requirement: TraversalEngineConfig exposes ScrollSwipe as engine-level default

`TraversalEngineConfig` SHALL expose a `ScrollSwipe` property of type `ScrollSwipeConfig`, defaulting to `new ScrollSwipeConfig()`. This value SHALL be used as the fallback when no page-level override is provided.

#### Scenario: TraversalEngineConfig default ScrollSwipe is the standard config
- **WHEN** `new TraversalEngineConfig()` is instantiated
- **THEN** `ScrollSwipe` is a `ScrollSwipeConfig` with values (0.5, 0.7, 0.5, 0.3, 300)

#### Scenario: TraversalEngineConfig accepts custom ScrollSwipe
- **WHEN** `new TraversalEngineConfig { ScrollSwipe = new ScrollSwipeConfig(StartY: 0.85, EndY: 0.55) }` is constructed
- **THEN** the engine-level default uses the custom coordinates

### Requirement: IVisionProvider exposes GetScrollSwipeConfig as page-level override

`IVisionProvider` SHALL expose a `virtual` method `GetScrollSwipeConfig()` returning `ScrollSwipeConfig?`. The default implementation SHALL return `null` (meaning "use engine-level default"). Implementations MAY override to return page-specific scroll region coordinates.

#### Scenario: Default implementation returns null
- **WHEN** `IVisionProvider.GetScrollSwipeConfig()` is called on the default implementation
- **THEN** the method returns null

#### Scenario: Mock implementation returns page-specific config
- **WHEN** `ScrollableMockVisionService.GetScrollSwipeConfig()` is called on a page with a configured `ScrollSwipeConfig`
- **THEN** the method returns the page's `ScrollSwipeConfig`

#### Scenario: Mock returns null for pages without explicit config
- **WHEN** `ScrollableMockVisionService.GetScrollSwipeConfig()` is called on a page without a configured `ScrollSwipeConfig`
- **THEN** the method returns null (fallback to engine-level default)

### Requirement: StepContext carries ScrollSwipe as engine-level fallback

`StepContext` SHALL include a `ScrollSwipe` property of type `ScrollSwipeConfig` (15th field). `TraversalEngine.RunAsync()` SHALL populate it from `_config.ScrollSwipe` when constructing the `StepContext`.

#### Scenario: StepContext contains ScrollSwipe field
- **WHEN** `StepContext` is inspected for field declarations
- **THEN** it contains a `ScrollSwipe` field of type `ScrollSwipeConfig`

#### Scenario: ScrollSwipe is populated from engine config
- **WHEN** `StepContext` is constructed by `TraversalEngine.RunAsync()`
- **THEN** `ScrollSwipe` is set to `_config.ScrollSwipe`

### Requirement: TryHandleScroll merges engine and page-level config

`TryHandleScrollAsync` SHALL resolve swipe coordinates by calling `ctx.Vision.GetScrollSwipeConfig()` first, falling back to `ctx.ScrollSwipe` when null. It SHALL use the resolved config's `StartX`, `StartY`, `EndX`, `EndY`, and `DurationMs` for the `SwipeAsync` call.

#### Scenario: Page-level config takes priority over engine default
- **WHEN** `ctx.Vision.GetScrollSwipeConfig()` returns a non-null `ScrollSwipeConfig`
- **THEN** `TryHandleScrollAsync` uses the page-level config's coordinates

#### Scenario: Engine default used when page-level config is null
- **WHEN** `ctx.Vision.GetScrollSwipeConfig()` returns null
- **THEN** `TryHandleScrollAsync` uses `ctx.ScrollSwipe` (engine-level default)

### Requirement: SimulatedScreen supports page-level ScrollSwipe configuration

`SimulatedScreen` SHALL store a `Dictionary<string, ScrollSwipeConfig>` for page-level scroll swipe overrides. `WithScrollablePage()` SHALL accept an optional `ScrollSwipeConfig? scrollSwipe = null` parameter. When provided, the config SHALL be stored and retrievable via `GetScrollSwipeConfig(string pageId)`.

#### Scenario: WithScrollablePage stores scroll swipe config
- **WHEN** `WithScrollablePage("bottom_sheet", generator, scrollSwipe: customConfig)` is called
- **THEN** `GetScrollSwipeConfig("bottom_sheet")` returns `customConfig`

#### Scenario: WithScrollablePage without config stores nothing
- **WHEN** `WithScrollablePage("default_list", generator)` is called without scrollSwipe
- **THEN** `GetScrollSwipeConfig("default_list")` returns null

### Requirement: Hardcoded swipe coordinate constants are deleted

The 5 `const` fields `ScrollSwipeStartX`, `ScrollSwipeStartY`, `ScrollSwipeEndX`, `ScrollSwipeEndY`, `ScrollSwipeDurationMs` in `StepOrchestrator` SHALL be deleted. All swipe coordinate resolution SHALL go through `ScrollSwipeConfig`.

#### Scenario: No hardcoded consts remain in StepOrchestrator
- **WHEN** `StepOrchestrator` source is inspected for the 5 const fields
- **THEN** none of them exist; all coordinate references use `ScrollSwipeConfig`
