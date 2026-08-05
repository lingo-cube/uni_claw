# vision-screen-state-provider Specification

## Purpose
Thin `IScreenStateProvider` implementation that reads scroll state from a previously analyzed `PageAnalysis`. Enables local-vision (no UIAutomator) to participate in `InterceptionHandler.TryHandleScrollAsync`'s scroll gating logic without adding UIA dependencies.

## ADDED Requirements

### Requirement: VisionScreenStateProvider implements IScreenStateProvider as thin wrapper

`VisionScreenStateProvider` SHALL be a `sealed class` in `UniClaw.Core/Traversal/` implementing `IScreenStateProvider`, constructed with `Func<PageAnalysis?> getCurrentAnalysis`. It SHALL delegate to the latest `PageAnalysis` for scroll state:

- `HasScroll()` SHALL return `getCurrentAnalysis()?.HasScroll ?? false`
- `IsEndOfList()` SHALL return `getCurrentAnalysis()?.IsEndOfList ?? true` (default true = no more content when analysis is unavailable)
- `GetScrollProgress()` SHALL return `0.0` (local-vision has no scrollbar position tracking)
- `GetScrollSwipeConfig()` SHALL return `null` (use engine defaults unless overridden by subclass)

#### Scenario: HasScroll delegates to PageAnalysis

- **WHEN** `PageAnalysis.HasScroll` is `true` and `VisionScreenStateProvider.HasScroll()` is called
- **THEN** returns `true`

#### Scenario: IsEndOfList delegates to PageAnalysis

- **WHEN** `PageAnalysis.IsEndOfList` is `false` and `VisionScreenStateProvider.IsEndOfList()` is called
- **THEN** returns `false`

#### Scenario: Null analysis defaults safely

- **WHEN** `getCurrentAnalysis()` returns null (no analysis yet)
- **THEN** `HasScroll()` returns `false`, `IsEndOfList()` returns `true`, `GetScrollProgress()` returns `0.0`, `GetScrollSwipeConfig()` returns `null`

### Requirement: VisionScreenStateProvider does NOT implement IObservableScreenStateProvider

`VisionScreenStateProvider` SHALL NOT implement `IObservableScreenStateProvider`. This ensures `InterceptionHandler.TryHandleScrollAsync` skips the UIA fingerprint fast path and falls through to the AI seen-set diffing safe path.

#### Scenario: Not IObservableScreenStateProvider

- **WHEN** `VisionScreenStateProvider` type is inspected via reflection
- **THEN** `typeof(IObservableScreenStateProvider).IsAssignableFrom(typeof(VisionScreenStateProvider))` is false

#### Scenario: InterceptionHandler takes AI path

- **WHEN** `InterceptionHandler.TryHandleScrollAsync` executes with a `VisionScreenStateProvider` as `ctx.ScreenState`
- **THEN** the `IObservableScreenStateProvider` type-check at line 451 evaluates to false, and execution falls through to the AI re-analysis path

### Requirement: VisionScreenStateProvider is in Traversal namespace not UniBrain

`VisionScreenStateProvider` SHALL be declared in `UniClaw.Core.Traversal` namespace (alongside `IScreenStateProvider`). It SHALL NOT be in any UniBrain namespace. This SHALL pass `ArchitectureGuardTests.UniBrain_DoesNotReferenceTraversal`.

#### Scenario: Namespace location passes ArchitectureGuard

- **WHEN** ArchitectureGuard tests run
- **THEN** `UniBrain_DoesNotReferenceTraversal` passes (UniBrain directory does not contain `VisionScreenStateProvider`)

### Requirement: GetScrollSwipeConfig returns null by default

`VisionScreenStateProvider.GetScrollSwipeConfig()` SHALL return `null`. This causes `InterceptionHandler` to use `ctx.ScrollSwipe` (engine default) for swipe coordinates. Subclasses MAY override to return page-specific `ScrollSwipeConfig` (e.g., with `MaxEmptyScrollRetries`).

#### Scenario: Engine default scroll config used

- **WHEN** `VisionScreenStateProvider.GetScrollSwipeConfig()` returns null in TryHandleScrollAsync
- **THEN** `ctx.ScrollSwipe` (engine default) is used for swipe coordinates
