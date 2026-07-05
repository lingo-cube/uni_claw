# Capability Spec: Simulation Infrastructure (Phase 2.3-sim)

## Overview

Provides state-aware mock implementations of `IVisionProvider` and `IActionExecutor` backed by a fixture-driven page state / transition data model. Enables end-to-end traversal testing without real ADB, AI, or device interaction. Aligned with Python `src/simulation/` core components.

## ADDED Requirements

### Requirement: StateFixture defines page states and transition rules

The system SHALL provide a `StateFixture` data model consisting of `InitialPage` (string), `Pages` (immutable dictionary of page ID → `PageState`), and `Transitions` (immutable array of `PageTransition`).

`PageState` SHALL contain `PageName` (string), `Elements` (immutable array of `PageElement`), and `IsComplete` (bool). `PageElement` SHALL contain `Id`, `Type`, `Text`, `X`, `Y`, and optional `ActionTarget`.

`PageTransition` SHALL contain `Id`, `Trigger` (element ID), `FromPage`, `ToPage`, and `Action`.

The fixture SHALL build a runtime index `(FromPage, Trigger, Action) → ToPage` from the `Transitions` array. `ResolveTarget(fromPage, elementId, action)` SHALL query this index and return the target page ID or null.

#### Scenario: Fixture resolves valid transition

- **WHEN** `ResolveTarget("home", "btn_settings", "click")` is called on a fixture where a transition `(home, btn_settings, click) → settings` exists
- **THEN** it SHALL return `"settings"`

#### Scenario: Fixture returns null for unknown transition

- **WHEN** `ResolveTarget("home", "nonexistent", "click")` is called
- **THEN** it SHALL return null

#### Scenario: Fixture loads from JSON

- **WHEN** `StateFixture.FromJson(json)` is called with valid JSON containing `initialPage`, `pages`, and `transitions`
- **THEN** it SHALL return a fully initialized `StateFixture` with populated index

#### Scenario: Fixture builder produces equivalent fixture

- **WHEN** `StateFixtureBuilder` is used to define pages and transitions programmatically
- **THEN** the built `StateFixture` SHALL be functionally equivalent to one loaded from equivalent JSON

### Requirement: StatefulMockVisionService implements IVisionProvider with page state machine

The system SHALL provide `StatefulMockVisionService` implementing `IVisionProvider`. It SHALL maintain `_currentPageId` (initialized to `fixture.InitialPage`) and `_navigationHistory` (stack of page IDs).

`AnalyzeCurrentPageAsync()` SHALL look up the current page from the fixture, build a `PageAnalysis` via the element-to-MenuItem mapping, and return it. If the current page is not found, it SHALL return null.

`FindAppEntryAsync(targetApp)` SHALL return `AppEntryPoint(0.5, 0.5)` — the simulated screen center.

`SimulateAction(elementId, action)` SHALL query `ResolveTarget(_currentPageId, elementId, action)`. If found, it SHALL push `_currentPageId` onto `_navigationHistory`, update `_currentPageId` to the target page, and return true. If not found, it SHALL return false.

`NavigateBack()` SHALL pop `_navigationHistory`. If the stack is non-empty, it SHALL set `_currentPageId` to the popped value and return true. If empty, it SHALL return false.

`FindElementAt(x, y)` SHALL return the first element in the current page whose coordinates are within ±0.05 of (x, y), or null if none match.

#### Scenario: AnalyzeCurrentPage returns page analysis for current page

- **WHEN** `AnalyzeCurrentPageAsync()` is called and `_currentPageId` is `"home"`
- **THEN** it SHALL return a `PageAnalysis` with `Items` populated from the home page's `Elements` (excluding tabs and back_button)

#### Scenario: SimulateAction switches page on matching transition

- **WHEN** `SimulateAction("btn_settings", "click")` is called and a transition `(home, btn_settings, click) → settings` exists
- **THEN** `_currentPageId` SHALL change to `"settings"` and the method SHALL return true

#### Scenario: SimulateAction returns false on unknown transition

- **WHEN** `SimulateAction("nonexistent", "click")` is called
- **THEN** the method SHALL return false and `_currentPageId` SHALL remain unchanged

#### Scenario: NavigateBack returns to previous page

- **WHEN** `NavigateBack()` is called after `SimulateAction` successfully switched from `"home"` to `"settings"`
- **THEN** `_currentPageId` SHALL return to `"home"` and the method SHALL return true

#### Scenario: NavigateBack returns false on empty history

- **WHEN** `NavigateBack()` is called with an empty navigation history
- **THEN** the method SHALL return false

#### Scenario: FindElementAt matches within tolerance

- **WHEN** `FindElementAt(0.51, 0.89)` is called on a page with an element at (0.5, 0.9)
- **THEN** it SHALL return that element

#### Scenario: FindElementAt returns null outside tolerance

- **WHEN** `FindElementAt(0.9, 0.9)` is called on a page with an element at (0.5, 0.9)
- **THEN** it SHALL return null

#### Scenario: BuildPageAnalysis maps element types to MenuItem correctly

- **WHEN** a page has elements of types `button`, `switch`, `back_button`, `tab`, `text`
- **THEN** the resulting `PageAnalysis` SHALL have `tab` elements in `Level1Menus` (as `MenuInfo`)
- **AND** non-tab elements in `Items` (as `MenuItem` with correct `MenuItemType` and `ExpectedAction`)
- **AND** `back_button` element coordinates extracted to `BackButton`

### Requirement: StatefulMockActionExecutor implements IActionExecutor with vision coordination

The system SHALL provide `StatefulMockActionExecutor` implementing `IActionExecutor`. It SHALL hold a reference to `StatefulMockVisionService` and delegate page state changes to it.

`TapAsync(x, y)` SHALL call `vision.FindElementAt(x, y)`. If an element is found, it SHALL call `vision.SimulateAction(element.Id, "click")`. It SHALL record an `ActionRecord` and return whether the element was found.

`PressBackAsync()` SHALL call `vision.NavigateBack()` and return its result.

`SwipeAsync`, `InputTextAsync`, `LongPressAsync`, `WaitAsync` SHALL record an `ActionRecord` and return `true` (scroll simulation deferred). `LongPressAsync` SHALL additionally call `vision.FindElementAt` for recording but SHALL NOT trigger page transitions.

`GetHistory()` SHALL return all recorded `ActionRecord` entries.

#### Scenario: TapAsync on matching element triggers page transition

- **WHEN** `TapAsync(0.5, 0.9)` is called and `vision.FindElementAt(0.5, 0.9)` finds element `"btn_settings"`
- **THEN** `vision.SimulateAction("btn_settings", "click")` SHALL be called
- **AND** an `ActionRecord` with `Action="tap"`, `Success=true` SHALL be recorded
- **AND** the method SHALL return true

#### Scenario: TapAsync on empty area returns false

- **WHEN** `TapAsync(0.9, 0.9)` is called and `vision.FindElementAt` returns null
- **THEN** an `ActionRecord` with `Success=false` SHALL be recorded
- **AND** the method SHALL return false

#### Scenario: PressBackAsync delegates to vision

- **WHEN** `PressBackAsync()` is called
- **THEN** `vision.NavigateBack()` SHALL be called and the return value propagated

#### Scenario: GetHistory returns all recorded actions

- **WHEN** `TapAsync` is called twice and `PressBackAsync` once
- **THEN** `GetHistory()` SHALL return 3 `ActionRecord` entries in order
