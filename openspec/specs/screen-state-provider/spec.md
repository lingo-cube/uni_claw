## ADDED Requirements

### Requirement: IScreenStateProvider defines 4 scroll/device state query methods

IScreenStateProvider SHALL define exactly 4 methods:
- `bool HasScroll()`
- `double GetScrollProgress()`
- `bool IsEndOfList()`
- `ScrollSwipeConfig? GetScrollSwipeConfig()`

IScreenStateProvider SHALL be in `UniClaw.Core.Traversal` namespace (NOT UniBrain namespace). IScreenStateProvider SHALL NOT be on IUniBrain — scroll is device/platform state query, not AI judgment.

#### Scenario: HasScroll reports whether current page is scrollable
- **WHEN** IScreenStateProvider.HasScroll() is called
- **THEN** returns true if page has scrollable content, false otherwise

#### Scenario: GetScrollProgress reports scroll position
- **WHEN** IScreenStateProvider.GetScrollProgress() is called
- **THEN** returns double 0-1 representing current scroll position (0=top, 1=bottom)

#### Scenario: IsEndOfList reports whether scroll reached bottom
- **WHEN** IScreenStateProvider.IsEndOfList() is called
- **THEN** returns true if no more scrollable content below, false otherwise

#### Scenario: GetScrollSwipeConfig returns page-level scroll configuration
- **WHEN** IScreenStateProvider.GetScrollSwipeConfig() is called
- **THEN** returns ScrollSwipeConfig? with page-specific swipe parameters, or null (use engine default)

### Requirement: IScreenStateProvider is independent from IUniBrain

IScreenStateProvider SHALL be a separate injection point from IUniBrain. StepContext SHALL have distinct properties for each:
- `IUniBrain Brain` (AI capabilities)
- `IScreenStateProvider ScreenState` (device state queries)

#### Scenario: StepContext carries two independent injection points
- **WHEN** StepContext is constructed
- **THEN** Brain property provides IUniBrain for AI calls
- **THEN** ScreenState property provides IScreenStateProvider for scroll queries

#### Scenario: MockScreenStateProvider returns programmed values
- **WHEN** Simulation constructs MockScreenStateProvider
- **THEN** HasScroll/GetScrollProgress/IsEndOfList/GetScrollSwipeConfig return programmed values without AI invocation

### Requirement: IScreenStateProvider method count is locked at 4

IScreenStateProvider SHALL define exactly 4 methods. Adding/removing methods SHALL require constitution change flow. ArchitectureGuard test SHALL verify method count.

#### Scenario: ArchitectureGuard enforces 4-method lock
- **WHEN** IScreenStateProvider interface is inspected by guard test
- **THEN** test asserts exactly 4 public methods exist on the interface
