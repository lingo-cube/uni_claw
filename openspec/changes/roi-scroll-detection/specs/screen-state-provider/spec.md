# screen-state-provider delta Specification

## MODIFIED Requirements

### Requirement: VisionScreenStateProvider implements IObservableScreenStateProvider

VisionScreenStateProvider SHALL implement both IScreenStateProvider and IObservableScreenStateProvider. The interface method count for IScreenStateProvider SHALL remain 4. The RefreshAsync method SHALL be additive to the existing 4 scroll query methods. RefreshAsync SHALL return the vision-derived scroll state directly, with no UIAutomator redundancy side channel — the constructor SHALL NOT accept a redundant UIA provider parameter, and RefreshAsync SHALL NOT depend on UIAutomator availability.

#### Scenario: Type implements both interfaces
- **WHEN** VisionScreenStateProvider type is inspected
- **THEN** `typeof(IObservableScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider))` is true
- **THEN** `typeof(IScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider))` is true

#### Scenario: ArchitectureGuard 4-method lock preserved
- **WHEN** `ArchitectureGuardTests.IScreenStateProvider_Has4Methods` runs
- **THEN** the test passes — IScreenStateProvider still defines exactly 4 methods

#### Scenario: RefreshAsync returns vision state without UIA fallback
- **WHEN** `VisionScreenStateProvider.RefreshAsync` is called
- **THEN** it returns `ScreenStateResult` derived solely from the vision state (PageAnalysis), with no attempt to consult a UIA provider
- **THEN** the returned `ScreenStateResult` SHALL NOT contain `HierarchyXml` or `HierarchyFingerprint`

### Requirement: IObservableScreenStateProvider extends the locked IScreenStateProvider

A new Core interface `IObservableScreenStateProvider` SHALL live in the `UniClaw.Core.Traversal` namespace and SHALL inherit `IScreenStateProvider`. By inheriting the locked interface, `IObservableScreenStateProvider` SHALL expose the 4 locked methods (`HasScroll`, `GetScrollProgress`, `IsEndOfList`, `GetScrollSwipeConfig`) unchanged, and SHALL add exactly one new method:

- `Task<ScreenStateResult> RefreshAsync(CancellationToken cancellationToken = default)`

Adding `IObservableScreenStateProvider` SHALL NOT modify `IScreenStateProvider`'s 4-method lock. The `ArchitectureGuard` test that asserts `IScreenStateProvider` has exactly 4 public methods SHALL remain green after this interface is introduced (the guard inspects `IScreenStateProvider`, not its inheritors).

#### Scenario: IObservableScreenStateProvider inherits the 4 locked methods

- **WHEN** `IObservableScreenStateProvider` is inspected via reflection
- **THEN** it inherits `IScreenStateProvider` and the 4 locked methods (`HasScroll`, `GetScrollProgress`, `IsEndOfList`, `GetScrollSwipeConfig`) are available on it unchanged
- **THEN** no method is added to, removed from, or redefined on `IScreenStateProvider` itself

#### Scenario: IObservableScreenStateProvider adds exactly RefreshAsync

- **WHEN** the members declared directly on `IObservableScreenStateProvider` are enumerated
- **THEN** exactly one method is declared: `RefreshAsync(CancellationToken)` returning `Task<ScreenStateResult>`

#### Scenario: 4-method lock on IScreenStateProvider is preserved

- **WHEN** the `ArchitectureGuard` test inspects `IScreenStateProvider` after `IObservableScreenStateProvider` is added
- **THEN** the test asserts exactly 4 public methods on `IScreenStateProvider` and passes
- **THEN** no new method is declared on `IScreenStateProvider`

### Requirement: ScreenStateResult is a Core-lifted sealed record

`ScreenStateResult` SHALL be a `sealed record` in the `UniClaw.Core.Traversal` namespace (Core, not Device). It SHALL replace the Device-only `AdbScreenStateResult` as the return type of `IObservableScreenStateProvider.RefreshAsync`. `ScreenStateResult` SHALL carry exactly these fields:

- `bool Succeeded`
- `string Status`
- `bool HasScroll`
- `bool IsEndOfList`
- `ScreenFailure? Failure`

On a successful refresh, `Succeeded` SHALL be `true`, `Status` SHALL describe the outcome, `HasScroll`/`IsEndOfList` SHALL be populated, and `Failure` SHALL be `null`. On a failed refresh, `Succeeded` SHALL be `false`, `Failure` SHALL carry the failure reason, and `HasScroll`/`IsEndOfList` SHALL NOT be relied upon.

#### Scenario: ScreenStateResult carries refresh outcome fields

- **WHEN** a successful `RefreshAsync` completes
- **THEN** the returned `ScreenStateResult` has `Succeeded` = true, a non-empty `Status`, `HasScroll`/`IsEndOfList` reflecting the screen, and `Failure` = null

#### Scenario: ScreenStateResult failure path sets Failure and Succeeded false

- **WHEN** `RefreshAsync` fails (e.g., screen capture or vision analysis error)
- **THEN** the returned `ScreenStateResult` has `Succeeded` = false and a non-null `Failure` carrying the failure reason

## REMOVED Requirements

### Requirement: AdbScreenStateProvider implements IObservableScreenStateProvider

The concrete `AdbScreenStateProvider` SHALL implement `IObservableScreenStateProvider`. Its 4 locked `IScreenStateProvider` methods (`HasScroll`, `GetScrollProgress`, `IsEndOfList`, `GetScrollSwipeConfig`) SHALL remain unchanged in signature and behavior. It SHALL additionally implement `RefreshAsync(string?, bool, CancellationToken)` returning `ScreenStateResult`, replacing any prior return of the Device-only `AdbScreenStateResult` with the Core-lifted `ScreenStateResult`.

#### Scenario: AdbScreenStateProvider implements the new interface while keeping locked methods

- **WHEN** `AdbScreenStateProvider` is inspected for implemented interfaces
- **THEN** it declares `IObservableScreenStateProvider` (and therefore `IScreenStateProvider`)
- **THEN** its 4 locked methods are unchanged in signature and behavior
- **THEN** it provides a `RefreshAsync` implementation returning `ScreenStateResult`

**Reason:** `AdbScreenStateProvider` exists solely to perform UIAutomator XML dumps (`src/UniClaw.Device/AdbScreenStateProvider.cs`). UIAutomator is Android-only and unavailable on devices/WebView pages without dump support, and its fingerprint fast path and `RefreshAsync(string? previousHierarchyXml, bool afterScroll, ...)` semantics are being removed. Per the PRD (§4.1) the entire file is deleted — scroll-end detection moves to vision-only ROI comparison, so no UIAutomator-based provider remains.

**Migration:** Delete `src/UniClaw.Device/AdbScreenStateProvider.cs`. `HostCommands` SHALL no longer construct or register `AdbScreenStateProvider`; Host and `ScenarioObservation` continue to program against the `IObservableScreenStateProvider` seam, with `VisionScreenStateProvider` as the sole production implementation. Scroll-end detection is performed by `InterceptionHandler.TryHandleScrollAsync` via ROI snapshot comparison (`RoiSelector`, `StableFrameCapturer`, `SnapshotComparer`) instead of UIA hierarchy dumps. `IAdbSession.DumpUiHierarchyAsync()` and its implementations (`ProcessAdbSession`, `AdvancedSharpAdbSession`) SHALL be removed alongside the provider.
