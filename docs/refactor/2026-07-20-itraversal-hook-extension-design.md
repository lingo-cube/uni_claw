# P4-B1: ITraversalHook Extension Model — Design Spec

> Date: 2026-07-20
> Priority: P4 (engine extensibility, no current pain point)
> Branch: feature/refactor
> Decision: D-91 Lifecycle Hook-only (no Decorator needed — Hook with Run-level nodes covers both)

## 1. Summary

Define a Lifecycle Hook model (`ITraversalHook`) as the sole engine extensibility mechanism. Hook covers Run/Step/State three levels of granularity with 7 active lifecycle nodes + 2 B2-dependent nodes. No Decorator pattern needed — Hook with Run-level nodes (OnBeforeRun/OnAfterRun) provides the same capability.

## 2. Decision Rationale

| Option | Mechanism | Verdict | Reason |
|--------|-----------|---------|--------|
| Decorator | Wrap `ITraversalEngine` with onion layers | ❌ | Only intercepts RunAsync-level, can't meet step-level needs (metrics, pause/resume) |
| Lifecycle Hook | Inject `ITraversalHook` at engine lifecycle points | ✅ | Covers Run+Step+State granularity; Hook with Run-level nodes covers Decorator's capability |
| Hybrid | Decorator (Run-level) + Hook (Step-level) | ❌ | Two mechanisms overlap at Run level — Hook alone covers both; hybrid adds learning cost without benefit |

**Key insight**: Hook with `OnBeforeRun`/`OnAfterRun` lifecycle nodes IS equivalent to Decorator wrapping RunAsync. No need for both mechanisms. The only thing Decorator adds over Hook is **short-circuit** (not calling inner.RunAsync), which is YAGNI — no current need to prevent engine execution from outside.

## 3. ITraversalHook Interface

```csharp
public interface ITraversalHook
{
    // Run 级 — engine start/complete
    Task OnBeforeRunAsync(TraversalPlan plan, ITraversalContext context);
    Task OnAfterRunAsync(TraversalResult result);

    // Step 级 — before/after each engine step
    Task OnBeforeStepAsync(ITraversalContext context);
    Task OnAfterStepAsync(ITraversalContext context);

    // State 级 — errors
    Task OnErrorAsync(ErrorContext error, ITraversalContext context);

    // State 级 — pause/resume (B2-dependent, not yet functional)
    Task OnPauseAsync(ITraversalContext context);    // ← stub until B2
    Task OnResumeAsync(ITraversalContext context);   // ← stub until B2
}
```

### ErrorContext (summary type, not full ErrorRecord)

```csharp
public sealed record class ErrorContext(
    string ErrorType,
    string Message,
    string? NodeId,
    bool IsRecoverable  // true = FSM-level (engine continues), false = engine-level fatal (engine terminates)
);
```

- Hook observes errors, not analyzes them. Full data available from ITraceStorage if needed.
- `IsRecoverable` flag distinguishes FSM recoverable errors (StepAsync catch → ErrorHandling state) from engine fatal errors (RunAsync catch → Done(Error)).

### TraversalHookBase (no-op abstract base)

```csharp
public abstract class TraversalHookBase : ITraversalHook
{
    public virtual Task OnBeforeRunAsync(TraversalPlan plan, ITraversalContext context) => Task.CompletedTask;
    public virtual Task OnAfterRunAsync(TraversalResult result) => Task.CompletedTask;
    public virtual Task OnBeforeStepAsync(ITraversalContext context) => Task.CompletedTask;
    public virtual Task OnAfterStepAsync(ITraversalContext context) => Task.CompletedTask;
    public virtual Task OnErrorAsync(ErrorContext error, ITraversalContext context) => Task.CompletedTask;
    public virtual Task OnPauseAsync(ITraversalContext context) => Task.CompletedTask;
    public virtual Task OnResumeAsync(ITraversalContext context) => Task.CompletedTask;
}
```

Implementers override only what they need. Default is no-op (Task.CompletedTask).

### Removed Lifecycle Nodes

- **OnBeforeVerify / OnAfterVerify**: Verification (ExpectedBehavior.Verify) happens in the test harness, outside TraversalEngine. There is no engine-level verify phase. These nodes have zero natural insertion point in engine code. If needed, a separate `IVerificationHook` interface can be defined in the test layer as an independent change.

## 4. Hook Registration & Call Points

### Registration

```csharp
// TraversalEngineConfig (existing, extended)
public sealed record class TraversalEngineConfig
{
    // ... existing fields ...
    public IReadOnlyList<ITraversalHook> Hooks { get; init; } = [];  // ← new
}
```

- `Hooks` is immutable (record init-only) — set at construction, not modified during run
- Empty list = zero overhead (`if (_hooks.Length == 0) return`)
- Order = registration order, first registered = first called

### Engine Call Points (2 files modified)

**TraversalEngine.cs** (5 call points):

| Lifecycle Node | Call Point | Location |
|----------------|------------|----------|
| OnBeforeRun | RunAsync entry | Before step loop starts |
| OnAfterStep | Step post-processing | After step processing, BEFORE termination checks |
| OnAfterRun | Done() helper | After GlobalState transition, before TraversalResult return |
| OnError (fatal) | RunAsync catch(Exception) | IsRecoverable=false |
| OnBeforeStep | Step loop entry | Before orchestrator.ExecuteStepAsync |

**TraversalFSM.cs or StepOrchestrator.cs** (1 call point):

| Lifecycle Node | Call Point | Location |
|----------------|------------|----------|
| OnError (recoverable) | StepAsync catch block | IsRecoverable=true |

**Timing Note**: OnAfterStep must fire BEFORE termination checks to cover the terminating step. Otherwise the last step's OnAfterStep would be skipped because Done() returns early.

### FireAsync Convenience Method

```csharp
private async Task FireAsync(Func<ITraversalHook, Task> action)
{
    if (_hooks.Length == 0) return;
    foreach (var hook in _hooks)
    {
        try { await action(hook); }
        catch (Exception ex) { /* log warning, don't interrupt engine */ }
    }
}
```

- Sequential execution (not parallel) — hooks may have inter-dependencies
- Hook exceptions: catch + log warning, don't interrupt engine
- Empty list: zero overhead (`if (_hooks.Length == 0) return`)

## 5. Testing Strategy

### New Files

```
src/UniClaw.Core/
  Traversal/
    ITraversalHook.cs              ← interface (7 async + 2 B2-dependent)
    TraversalHookBase.cs           ← abstract base (all no-op)
    ErrorContext.cs                ← error summary record

tests/UniClaw.Core.Tests/
  Traversal/
    TraversalHookTests.cs          ← hook registration/call/fire tests
```

### Test Matrix

| Test Group | Coverage | Est. Count |
|------------|----------|------------|
| Empty Hook list | TraversalEngineConfig.Hooks=[] → engine runs normally, zero overhead | 1 |
| Single Hook counting | 1 CountingHook → each OnXxx fires with correct count | 1 |
| Multiple Hook order | HookA + HookB → A fires before B | 1 |
| Hook throws exception | Hook throws → engine continues + warning logged | 1 |
| OnBeforeRun/OnAfterRun timing | Hook fires at RunAsync entry/exit | 1 |
| OnBeforeStep/OnAfterStep timing | Hook fires before/after step, including terminating step | 1 |
| OnError recoverable | FSM-level error → IsRecoverable=true | 1 |
| OnError fatal | Engine-level error → IsRecoverable=false | 1 |
| TraversalHookBase no-op | Inherit base without override → all Task.CompletedTask | 1 |

**Total: ~9-10 tests**. Light — Hook is pipeline mechanism, not complex business logic.

## 6. Out of Scope

- ❌ IVerificationHook (test-level verify hook — independent change)
- ❌ OnPause/OnResume actual implementation (depends on B2: GlobalFSM completion)
- ❌ Decorator pattern addition (YAGNI — Hook covers Run-level needs)
- ❌ Hook parallel execution (sequential is sufficient)
- ❌ Hook priority/ordering mechanism (registration order = ordering, YAGNI)
- ❌ Concrete Hook implementations (MetricsHook, LoggingHook — upper layer or independent change)
- ❌ Hook short-circuit capability (no current need to prevent engine execution)
