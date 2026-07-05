# Tasks: Phase 2.3-sim — Simulation Infrastructure

## 1. IVisionProvider Interface Completion

- [x] 1.1 Rename `IVisionProvider.GetCurrentPageAnalysisAsync` → `AnalyzeCurrentPageAsync` in StepContext.cs
- [x] 1.2 Add `FindAppEntryAsync(string targetApp, CancellationToken ct)` to IVisionProvider
- [x] 1.3 Create `AppEntryPoint` record class (double X, double Y)
- [x] 1.4 Update `MockVisionProvider` in tests to implement renamed method

## 2. StateFixture Data Model

- [x] 2.1 Create `Simulation/` directory under `src/UniClaw.Core/`
- [x] 2.2 Implement `PageElement` record class (Id, Type, Text, X, Y, ActionTarget?)
- [x] 2.3 Implement `PageState` record class (PageName, Elements, IsComplete)
- [x] 2.4 Implement `PageTransition` record class (Id, Trigger, FromPage, ToPage, Action)
- [x] 2.5 Implement `StateFixture` record class (InitialPage, Pages, Transitions) with `_transitionIndex` and `ResolveTarget`/`GetPage` methods
- [x] 2.6 Implement internal `StateFixtureDto` for JSON deserialization
- [x] 2.7 Implement `StateFixture.FromJson(string json)` factory method
- [x] 2.8 Implement `StateFixtureBuilder` (Fluent API: `.Page(...).Element(...).Transition(...).Build()`)

## 3. StatefulMockVisionService

- [x] 3.1 Create `StatefulMockVisionService : IVisionProvider` class
- [x] 3.2 Implement constructor (StateFixture → _currentPageId = fixture.InitialPage, _navigationHistory)
- [x] 3.3 Implement `AnalyzeCurrentPageAsync` — look up current page → BuildPageAnalysis
- [x] 3.4 Implement `FindAppEntryAsync` — return `AppEntryPoint(0.5, 0.5)`
- [x] 3.5 Implement `SimulateAction(elementId, action)` — ResolveTarget → push history → update page
- [x] 3.6 Implement `NavigateBack()` — pop history → update page
- [x] 3.7 Implement `FindElementAt(x, y)` — tolerance ±0.05 coordinate matching
- [x] 3.8 Implement `BuildPageAnalysis` — element type → MenuItem mapping (8 types: button/switch/toggle/back_button/icon/input/text/tab)
- [x] 3.9 Implement `Reset()` — reset to initial page and clear history

## 4. StatefulMockActionExecutor

- [x] 4.1 Create `StatefulMockActionExecutor : IActionExecutor` class
- [x] 4.2 Implement constructor (StatefulMockVisionService → _vision, _history)
- [x] 4.3 Implement `TapAsync(x, y)` — FindElementAt → SimulateAction("click") → record
- [x] 4.4 Implement `PressBackAsync()` — NavigateBack → record
- [x] 4.5 Implement `SwipeAsync` / `InputTextAsync` / `LongPressAsync` / `WaitAsync` — record only (no state change)
- [x] 4.6 Implement `GetHistory()` — return _history

## 5. Integration Points

- [x] 5.1 Create `SimpleNodeRegistry : INodeRegistry` (Dictionary-backed, ~15 lines)
- [x] 5.2 Change StepOrchestrator line 41: `Step()` → `Step(ctx)`
- [x] 5.3 Verify all existing 464 tests still pass after StepOrchestrator change

## 6. Unit Tests

- [x] 6.1 Write `StateFixtureTests` — JSON deserialization, ResolveTarget hit/miss, Builder equivalence (4 scenarios)
- [x] 6.2 Write `StatefulMockVisionTests` — page switch via SimulateAction, NavigateBack, FindElementAt match/no-match, Reset, BuildPageAnalysis mapping (7 scenarios)
- [x] 6.3 Write `StatefulMockActionTests` — TapAsync triggers page change, TapAsync empty area returns false, PressBackAsync delegates, GetHistory ordering (4 scenarios)

## 7. E2E Tests

- [x] 7.1 Create `tests/.../Fixtures/two-page-app.json` — 2 pages (home + settings), 2 transitions (go + back)
- [x] 7.2 Write `SimulationE2ETests.TwoPageLinearTraversal` — home→click Settings→settings→click Back→home, verify actions + pages
- [x] 7.3 Write `SimulationE2ETests.StaticChildrenTraversal` — root with 2 children, HandleBranch STATIC → select each → FrameComplete
- [x] 7.4 Write `SimulationE2ETests.NullElementTapReturnsFalse` — click empty area → TapAsync false → ResultVerify (not ErrorHandling)

## 8. Documentation

- [x] 8.1 Update `docs/system/layers/state-machine.md` — IVisionProvider interface change + Simulation namespace
- [x] 8.2 Update `docs/system/patterns/fsm-design.md` — add simulation infrastructure to architecture overview
