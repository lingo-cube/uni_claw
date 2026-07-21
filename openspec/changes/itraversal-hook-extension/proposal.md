## Why

TraversalEngine has no extensibility mechanism — no way for upper layers (metrics, logging, custom orchestrators) to observe or react to engine lifecycle events without modifying engine internals. P4-B2 (Pause/Resume) already wired `OnPauseAsync`/`OnResumeAsync` hooks, and the `ITraversalHook` interface, `TraversalHookBase`, `TraversalErrorContext`, and `FireAsync` dispatch method are already implemented. But 5 remaining lifecycle nodes (OnBeforeRun, OnAfterRun, OnBeforeStep, OnAfterStep, OnError) have no call points in the engine, and hook registration uses a mutable `RegisterHook()` method instead of an immutable config field. This change completes the hook infrastructure.

## What Changes

- Add `Hooks: ImmutableArray<ITraversalHook>` init-only field to `TraversalEngineConfig` (replacing mutable `RegisterHook()` + `List<>`)
- Delete `RegisterHook()` method and `List<ITraversalHook> _hooks` field from TraversalEngine
- Wire 5 call points: OnBeforeRun (before step loop), OnAfterStep (before termination checks), OnAfterRun (at each Done() call site), OnBeforeStep (after pause-gate, before vision), OnError fatal (in catch block)
- Wire OnError recoverable via engine-level intercept (check `stepResult.NextState == ErrorHandling` in step loop, not inside FSM)
- Improve `FireAsync` catch block: add `Console.WriteLine` (consistent with TraceCoordinator dispatch-table pattern)
- Migrate existing `RegisterHook()` callers (PauseResumeTests) to config field
- Add ~10-11 tests in TraversalHookTests.cs

## Capabilities

### New Capabilities
- `traversal-hook`: Lifecycle hook wiring — 5 call point insertions + config field migration + recoverable OnError engine-level intercept

### Modified Capabilities
- `traversal-engine`: Add `Hooks` config field (replacing RegisterHook); wire 5+1 lifecycle call points; improve FireAsync catch block

## Impact

- **Modified files**: `TraversalEngine.cs` (delete RegisterHook/List, add 6 call points, improve FireAsync, migrate to ImmutableArray), `TraversalEngineConfig.cs` (add Hooks field), `TraversalEnginePauseResumeTests.cs` (migrate RegisterHook → config)
- **New files**: `TraversalHookTests.cs`
- **No breaking changes**: empty Hooks = zero overhead; engine behavior identical when no hooks registered
