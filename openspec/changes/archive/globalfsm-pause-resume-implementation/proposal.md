## Why

TraversalEngine's PauseAsync/ResumeAsync are stubs — they only change GlobalState via GlobalFSM but do not actually suspend the step loop. The loop continues executing steps regardless of state, making pause/resume purely cosmetic. This blocks engine lifecycle completeness: callers cannot pause traversal for external interruptions (popups, app switches, user input) and resume later.

## What Changes

- **TaskCompletionSource gate** in TraversalEngine: PauseAsync creates an uncompleted TCS to block the step loop; ResumeAsync completes it to release the loop
- **Precondition validation**: PauseAsync throws when not Traversing; ResumeAsync throws when not Paused
- **B1 lifecycle hook call points**: OnPauseAsync and OnResumeAsync fire during pause/resume (hook after state change, gate open after hook)
- **Two-step termination alignment**: StopAsync already uses Traversing→Paused→Terminated (no change, but codified in spec)
- **Thread safety**: `_resumeSignal` declared `volatile` + TrySetResult for cross-thread safety
- **Step loop integration**: pause check inserted after OnBeforeStep, before ExecuteStepAsync
- **BREAKING**: PauseAsync/ResumeAsync change from sync stubs to async methods with precondition checks — callers must handle DomainValidationException

## Capabilities

### New Capabilities
- `pause-resume`: TaskCompletionSource-based gate mechanism, precondition validation, B1 hook integration, and step loop suspension for the GlobalFSM lifecycle

### Modified Capabilities
- `traversal-engine`: Replace stub PauseAsync/ResumeAsync definitions (previously "no precondition validation") with full gate-based implementation requirements including preconditions, hooks, and step loop pause check

## Impact

- **TraversalEngine.cs**: Add `_resumeSignal` volatile field + `CreateCompletedTCS()` helper; rewrite PauseAsync/ResumeAsync from sync stubs to async gate-based methods; insert pause check into RunAsync step loop
- **IGraphTraversalEngine.cs**: Interface unchanged (already async) — no breaking API change
- **Tests**: ~8 new tests in StateMachine test class covering gate lifecycle, preconditions, cancellation, hook firing
- **Dependencies**: Requires B1 (ITraversalHook) for OnPauseAsync/OnResumeAsync hook types; B1 interface defines them as stubs, this change makes them functional
