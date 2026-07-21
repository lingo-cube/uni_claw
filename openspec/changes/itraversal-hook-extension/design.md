## Context

`ITraversalHook` interface, `TraversalHookBase`, `TraversalErrorContext`, and `FireAsync` dispatch method are already implemented in code (created alongside B2 pause/resume work). OnPauseAsync/OnResumeAsync are wired in PauseAsync/ResumeAsync methods. The remaining work is: (1) migrate registration from mutable `RegisterHook()` to immutable `TraversalEngineConfig.Hooks` config field, (2) wire 5 call points for the remaining lifecycle nodes, (3) wire recoverable OnError at engine level, (4) improve FireAsync catch block, and (5) add tests.

TraversalEngine uses dispatch-table pattern (see `patterns/dispatch-table.md`) for TraceCoordinator Log-and-Continue. FireAsync should be consistent with this pattern.

## Goals / Non-Goals

**Goals:**
- Complete the 5 unwired lifecycle hook call points in TraversalEngine
- Migrate hook registration to immutable config field (sealed record init-only)
- Wire recoverable OnError at engine level (not inside FSM — preserves FSM independence)
- Make FireAsync exception handling consistent with dispatch-table pattern
- Add tests verifying all call points fire correctly, including terminating step

**Non-Goals:**
- IVerificationHook (test-level verify hook — independent change)
- Decorator pattern (YAGNI — Hook covers Run-level)
- Hook parallel execution (sequential is sufficient)
- Hook priority/ordering (registration order = ordering)
- Concrete Hook implementations (MetricsHook, LoggingHook — upper layer)
- Making Done() async (call-site approach is sufficient)

## Decisions

### D-A: Hooks registration via config field, not RegisterHook method

**Choice**: `TraversalEngineConfig.Hooks: ImmutableArray<ITraversalHook> { get; init; } = Empty`

**Alternatives considered**:
- Mutable `RegisterHook()` + `List<>` (current code): violates record init-only principle, concurrency risk (hooks added mid-run), `_hooks.Length` unavailable on `List`
- Constructor parameter: equivalent to config field, but config is the established pattern for engine settings

**Rationale**: Immutable config field is consistent with `TraversalEngineConfig`'s existing init-only pattern. `ImmutableArray` provides `.Length` for zero-overhead empty check. No concurrency risk.

### D-B: Recoverable OnError wired at engine level, not inside FSM

**Choice**: Check `stepResult.NextState == ErrorHandling && _ctx.LastError != null` in RunAsync step loop, fire `OnErrorAsync(TraversalErrorContext(..., IsRecoverable=true))`.

**Alternatives considered**:
- Pass hooks into TraversalFSM constructor: earlier timing but FSM gains hook dependency, violating FSM independence principle (C-4) and increasing FSM complexity
- Pass hooks into StepOrchestrator: same problem, StepOrchestrator already delegates to FSM

**Rationale**: Hook is engine-level extensibility, not FSM-level. Engine observes FSM state transitions — intercepting ErrorHandling in the step loop is the natural engine-level point. Timing delay (one iteration) is acceptable since hooks are observers, not decision-makers.

### D-C: OnAfterRun fired at Done() call sites, not inside Done()

**Choice**: Each `return Done(...)` in RunAsync becomes `var result = Done(...); await FireAsync(h => h.OnAfterRunAsync(result)); return result;`

**Alternatives considered**:
- Make Done() async (return `Task<TraversalResult>`): cleaner but invasive — all Done() signatures change, 5+ call sites need `await`
- Fire inside Done() before return: requires making Done() async too, or using sync-over-async hack

**Rationale**: Call-site approach is minimally invasive — only RunAsync call sites change (which are already async). Done() signature stays synchronous.

### D-D: OnBeforeRun fires outside try block

**Choice**: `await FireAsync(h => h.OnBeforeRunAsync(_plan, _ctx))` before `try { for (...) }`

**Rationale**: If a hook throws in OnBeforeRun, FireAsync catches it (Log-and-Continue). No special handling needed. Firing outside try means the engine's catch(Exception) block doesn't convert a hook failure into Done(Error).

### D-E: FireAsync catch block uses Console.WriteLine

**Choice**: `catch (Exception ex) { Console.WriteLine($"[Hook Warning] {ex.GetType().Name}: {ex.Message}"); }`

**Alternatives considered**:
- Pure silent `catch { }`: current code; inconsistent with TraceCoordinator dispatch-table pattern
- ILogger injection: dispatch-table pattern explicitly avoids DI dependency for side-effect channels

**Rationale**: Consistent with TraceCoordinator's `Console.WriteLine` approach in dispatch-table.md §Log-and-Continue. No DI dependency, observable in console logs.

## Risks / Trade-offs

- [OnAfterStep fires before termination checks] → Must ensure insertion point is after L290 (page-visit tracking) and before L293 (termination). If inserted wrong, terminating step's OnAfterStep would be skipped.
- [Recoverable OnError timing] → One iteration delay compared to FSM-level intercept. Acceptable for observation hooks. If sub-millisecond timing matters for metrics hooks, FSM-level approach (D-B alternative) would be needed — YAGNI now.
- [ImmutableArray requires builder for test setup] → `ImmutableArray.Create(hook)` or `[hook]` initializer. Slightly more verbose than List, but consistent with project's ImmutableArray convention.
- [Done() call sites scattered] → 5+ call sites need OnAfterRun fire. If a new Done() call site is added without OnAfterRun, the hook silently misses that exit path. Mitigated by test coverage + TraversalHookTests verifying all exit paths.
