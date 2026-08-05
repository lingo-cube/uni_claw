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

### Requirement: VisionScreenStateProvider implements IObservableScreenStateProvider

VisionScreenStateProvider SHALL implement both IScreenStateProvider and IObservableScreenStateProvider. The constructor SHALL accept an optional IObservableScreenStateProvider? parameter for UIA redundancy. The interface method count for IScreenStateProvider SHALL remain 4. The RefreshAsync method SHALL be additive to the existing 4 scroll query methods.

#### Scenario: Type implements both interfaces
- **WHEN** VisionScreenStateProvider type is inspected
- **THEN** `typeof(IObservableScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider))` is true
- **THEN** `typeof(IScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider))` is true

#### Scenario: ArchitectureGuard 4-method lock preserved
- **WHEN** `ArchitectureGuardTests.IScreenStateProvider_Has4Methods` runs
- **THEN** the test passes — IScreenStateProvider still defines exactly 4 methods

### Requirement: IObservableScreenStateProvider extends the locked IScreenStateProvider

A new Core interface `IObservableScreenStateProvider` SHALL live in the `UniClaw.Core.Traversal` namespace and SHALL inherit `IScreenStateProvider`. By inheriting the locked interface, `IObservableScreenStateProvider` SHALL expose the 4 locked methods (`HasScroll`, `GetScrollProgress`, `IsEndOfList`, `GetScrollSwipeConfig`) unchanged, and SHALL add exactly one new method:

- `Task<ScreenStateResult> RefreshAsync(string? previousHierarchyXml, bool afterScroll, CancellationToken ct)`

Adding `IObservableScreenStateProvider` SHALL NOT modify `IScreenStateProvider`'s 4-method lock. The `ArchitectureGuard` test that asserts `IScreenStateProvider` has exactly 4 public methods SHALL remain green after this interface is introduced (the guard inspects `IScreenStateProvider`, not its inheritors).

#### Scenario: IObservableScreenStateProvider inherits the 4 locked methods

- **WHEN** `IObservableScreenStateProvider` is inspected via reflection
- **THEN** it inherits `IScreenStateProvider` and the 4 locked methods (`HasScroll`, `GetScrollProgress`, `IsEndOfList`, `GetScrollSwipeConfig`) are available on it unchanged
- **THEN** no method is added to, removed from, or redefined on `IScreenStateProvider` itself

#### Scenario: IObservableScreenStateProvider adds exactly RefreshAsync

- **WHEN** the members declared directly on `IObservableScreenStateProvider` are enumerated
- **THEN** exactly one method is declared: `RefreshAsync(string?, bool, CancellationToken)` returning `Task<ScreenStateResult>`

#### Scenario: 4-method lock on IScreenStateProvider is preserved

- **WHEN** the `ArchitectureGuard` test inspects `IScreenStateProvider` after `IObservableScreenStateProvider` is added
- **THEN** the test asserts exactly 4 public methods on `IScreenStateProvider` and passes
- **THEN** no new method is declared on `IScreenStateProvider`

### Requirement: ScreenStateResult is a Core-lifted sealed record

`ScreenStateResult` SHALL be a `sealed record` in the `UniClaw.Core.Traversal` namespace (Core, not Device). It SHALL replace the Device-only `AdbScreenStateResult` as the return type of `IObservableScreenStateProvider.RefreshAsync`. `ScreenStateResult` SHALL carry exactly these fields:

- `bool Succeeded`
- `string Status`
- `string? HierarchyXml`
- `string? HierarchyFingerprint`
- `bool HasScroll`
- `bool IsEndOfList`
- `string? Failure`

On a successful refresh, `Succeeded` SHALL be `true`, `Status` SHALL describe the outcome, `HierarchyXml`/`HierarchyFingerprint`/`HasScroll`/`IsEndOfList` SHALL be populated, and `Failure` SHALL be `null`. On a failed refresh, `Succeeded` SHALL be `false`, `Failure` SHALL carry the failure reason, and `HierarchyXml`/`HierarchyFingerprint` MAY be `null`.

#### Scenario: ScreenStateResult carries refresh outcome fields

- **WHEN** a successful `RefreshAsync` completes
- **THEN** the returned `ScreenStateResult` has `Succeeded` = true, a non-empty `Status`, populated `HierarchyXml` and `HierarchyFingerprint`, `HasScroll`/`IsEndOfList` reflecting the screen, and `Failure` = null

#### Scenario: ScreenStateResult failure path sets Failure and Succeeded false

- **WHEN** `RefreshAsync` fails (e.g., device unreachable or hierarchy dump error)
- **THEN** the returned `ScreenStateResult` has `Succeeded` = false and a non-null `Failure` carrying the failure reason
- **THEN** `HierarchyXml` and `HierarchyFingerprint` may be null on the failure path

### Requirement: AdbScreenStateProvider implements IObservableScreenStateProvider

The concrete `AdbScreenStateProvider` SHALL implement `IObservableScreenStateProvider`. Its 4 locked `IScreenStateProvider` methods (`HasScroll`, `GetScrollProgress`, `IsEndOfList`, `GetScrollSwipeConfig`) SHALL remain unchanged in signature and behavior. It SHALL additionally implement `RefreshAsync(string?, bool, CancellationToken)` returning `ScreenStateResult`, replacing any prior return of the Device-only `AdbScreenStateResult` with the Core-lifted `ScreenStateResult`.

#### Scenario: AdbScreenStateProvider implements the new interface while keeping locked methods

- **WHEN** `AdbScreenStateProvider` is inspected for implemented interfaces
- **THEN** it declares `IObservableScreenStateProvider` (and therefore `IScreenStateProvider`)
- **THEN** its 4 locked methods are unchanged in signature and behavior
- **THEN** it provides a `RefreshAsync` implementation returning `ScreenStateResult`

### Requirement: Host programs against IObservableScreenStateProvider

Host SHALL program against `IObservableScreenStateProvider`, not the concrete `AdbScreenStateProvider`. Host SHALL NOT cast `IScreenStateProvider` (or `IObservableScreenStateProvider`) to the concrete `AdbScreenStateProvider`. `HostRunServices.ScreenState` SHALL be typed `IObservableScreenStateProvider`. The `ScenarioObservation` constructor parameter that consumes screen state SHALL be typed `IObservableScreenStateProvider`.

#### Scenario: Host does not cast to the concrete provider

- **WHEN** Host source is inspected for casts to `AdbScreenStateProvider`
- **THEN** no cast from `IScreenStateProvider` or `IObservableScreenStateProvider` to `AdbScreenStateProvider` exists
- **THEN** Host consumes `RefreshAsync` through the `IObservableScreenStateProvider` seam

#### Scenario: HostRunServices and ScenarioObservation use the interface seam

- **WHEN** `HostRunServices.ScreenState` is inspected
- **THEN** it is typed `IObservableScreenStateProvider`
- **WHEN** the `ScenarioObservation` constructor parameter carrying screen state is inspected
- **THEN** it is typed `IObservableScreenStateProvider`


### Requirement: VisionScreenStateProvider as new IScreenStateProvider implementation

A new `VisionScreenStateProvider` SHALL be added to `UniClaw.Core/Traversal/` implementing `IScreenStateProvider`. It SHALL NOT implement `IObservableScreenStateProvider`. It SHALL delegate `HasScroll()` and `IsEndOfList()` to a `Func<PageAnalysis?>` injected at construction. It SHALL NOT modify the `IScreenStateProvider` interface — the 4-method lock SHALL remain unbroken.

This implementation SHALL coexist with existing `DefaultScreenStateProvider` (all-virtual defaults), `MockScreenStateProvider` (programmable), and `AdbScreenStateProvider` (UIAutomator-based) — each serving different device scenarios.

#### Scenario: New implementation does not change interface

- **WHEN** `ArchitectureGuardTests.IScreenStateProvider_Has4Methods` runs after `VisionScreenStateProvider` is added
- **THEN** the test passes — 4 methods on the interface, unchanged

#### Scenario: Four implementations coexist

- **WHEN** all `IScreenStateProvider` implementations are enumerated
- **THEN** `DefaultScreenStateProvider`, `MockScreenStateProvider`, `AdbScreenStateProvider`, and `VisionScreenStateProvider` each implement the 4-method interface
