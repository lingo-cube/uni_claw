# Capability: Scroll-Aware Traversal — Delta

## MODIFIED Requirements

### Requirement: IVisionProvider shall expose scroll state query methods

The system SHALL extend IVisionProvider with four scroll-aware methods: `HasScroll()` to check if current page has scrollable content, `GetScrollProgress()` to retrieve current scroll progress (0.0-1.0), `IsEndOfList()` to check if traversal has reached the end of scrollable content, and `GetScrollSwipeConfig()` (virtual, returns `ScrollSwipeConfig?`) to provide page-level scroll region coordinates.

#### Scenario: GetScrollSwipeConfig returns null by default
- **WHEN** `IVisionProvider.GetScrollSwipeConfig()` is called on the default interface implementation
- **THEN** the method returns null (use engine-level default)

#### Scenario: Mock implementation returns page-specific ScrollSwipeConfig
- **WHEN** `ScrollableMockVisionService.GetScrollSwipeConfig()` is called
- **THEN** it delegates to `SimulatedScreen.GetScrollSwipeConfig(CurrentPageId)`

### Requirement: StepOrchestrator shall integrate scroll operations with configurable coordinates

The system SHALL execute scroll swipe operations through `IActionExecutor.SwipeAsync` with coordinates resolved from `IVisionProvider.GetScrollSwipeConfig() ?? ctx.ScrollSwipe`. Scroll coordinates SHALL NOT be hardcoded as `const` values. The default `ScrollSwipeConfig` SHALL produce the same coordinates as the previous hardcoded constants: `(0.5, 0.7) → (0.5, 0.3), 300ms`.

#### Scenario: Scroll swipe uses configured coordinates
- **WHEN** `TryHandleScrollAsync` executes a swipe
- **THEN** coordinates come from the resolved `ScrollSwipeConfig`, not from hardcoded `const` fields

#### Scenario: Default config produces identical behavior to previous consts
- **WHEN** no page-level config exists and engine default is `new ScrollSwipeConfig()`
- **THEN** the swipe is `(0.5, 0.7) → (0.5, 0.3), 300ms` — identical to previous hardcoded behavior

#### Scenario: Page-level config overrides engine default for specific pages
- **WHEN** a page has a configured `ScrollSwipeConfig` with `StartY=0.85, EndY=0.55`
- **THEN** swipe on that page uses the override coordinates

### Requirement: TryHandleScroll executes scroll as async operation+judgment

`TryHandleScrollAsync` SHALL be an `internal static async Task<bool>` method operating on the "scroll = action + judgment" model. It SHALL:
1. Check scrollability and end-of-list (sync, via `IVisionProvider`)
2. Resolve swipe coordinates from config
3. Execute swipe asynchronously via `await ctx.Action.SwipeAsync(...)`
4. Re-analyze page asynchronously via `await ctx.Vision.AnalyzeCurrentPageAsync()`
5. Judge whether new elements were revealed via seen-set diff

#### Scenario: Scroll operation is awaited not blocked
- **WHEN** `TryHandleScrollAsync` executes
- **THEN** all `IActionExecutor` and `IVisionProvider` calls use `await`
- **AND** no `.GetAwaiter().GetResult()` is present in the method body

#### Scenario: Scroll still detects no-new-elements correctly
- **WHEN** a swipe reveals no new elements (all seen before)
- **THEN** `TryHandleScrollAsync` returns false, triggering frame completion

## ADDED Requirements

### Requirement: SimulatedScreen supports page-level ScrollSwipe configuration for scroll-aware mock testing

`SimulatedScreen` SHALL support per-page `ScrollSwipeConfig` via `WithScrollablePage(pageId, source, scrollSwipe)`. `ScrollableMockVisionService` SHALL expose the current page's config via `GetScrollSwipeConfig()` override. Tests SHALL be able to configure different scroll regions for different pages in the same fixture.

#### Scenario: Test configures custom scroll region for a page
- **WHEN** a test creates a `SimulatedScreen` with `.WithScrollablePage("bottom_sheet", generator, scrollSwipe: customConfig)`
- **THEN** scroll operations on that page use `customConfig` coordinates
- **AND** other scrollable pages without explicit config use the engine default

#### Scenario: Default scroll pages work without explicit config
- **WHEN** a test creates a `SimulatedScreen` with `.WithScrollablePage("default_list", generator)` (no scrollSwipe)
- **THEN** scroll operations on that page use the engine-level `ScrollSwipeConfig` default
