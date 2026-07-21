# P4-B1: ITraversalHook Extension Model — Design Spec

> Date: 2026-07-21 (updated from 2026-07-20)
> Priority: P4 (engine extensibility, no current pain point)
> Branch: feature/refactor
> Decision: D-91 Lifecycle Hook-only (no Decorator needed — Hook with Run-level nodes covers both)

## 1. Summary

Wire 5 remaining lifecycle hook call points into TraversalEngine, migrate hook registration from mutable `RegisterHook()` to immutable `TraversalEngineConfig.Hooks` config field, and add ~9-10 tests. Interface (`ITraversalHook`), base class (`TraversalHookBase`), error type (`TraversalErrorContext`), dispatch method (`FireAsync`), and 2 B2 call points (`OnPauseAsync`/`OnResumeAsync`) **already exist** — this change completes the remaining infrastructure.

## 2. Decision Rationale

| Option | Mechanism | Verdict | Reason |
|--------|-----------|---------|--------|
| Decorator | Wrap `ITraversalEngine` with onion layers | ❌ | Only intercepts RunAsync-level, can't meet step-level needs (metrics, pause/resume) |
| Lifecycle Hook | Inject `ITraversalHook` at engine lifecycle points | ✅ | Covers Run+Step+State granularity; Hook with Run-level nodes covers Decorator's capability |
| Hybrid | Decorator (Run-level) + Hook (Step-level) | ❌ | Two mechanisms overlap at Run level — Hook alone covers both; hybrid adds learning cost without benefit |

**Key insight**: Hook with `OnBeforeRun`/`OnAfterRun` lifecycle nodes IS equivalent to Decorator wrapping RunAsync. No need for both mechanisms. The only thing Decorator adds over Hook is **short-circuit** (not calling inner.RunAsync), which is YAGNI — no current need to prevent engine execution from outside.

## 3. Current Implementation State

The following are **already implemented** (B2 wired pause/resume hooks; interface + base class created alongside):

| Component | File | Status |
|-----------|------|--------|
| `ITraversalHook` interface (7 methods) | `Traversal/ITraversalHook.cs` L11-27 | ✅ Done |
| `TraversalHookBase` abstract base (all no-op) | Same file L32-61 | ✅ Done |
| `TraversalErrorContext` sealed record (4 fields) | Same file L68-72 | ✅ Done |
| `_hooks` field | `Traversal/TraversalEngine.cs` L38 → `List<ITraversalHook>` | ✅ Done (but mutable — needs migration) |
| `RegisterHook()` method | Same file L522 | ✅ Done (needs deletion → migrate to config) |
| `FireAsync` method | Same file L531-544 | ✅ Done (catch block needs improvement) |
| `OnPauseAsync` call point | Same file L408 (PauseAsync) | ✅ Done (B2) |
| `OnResumeAsync` call point | Same file L424 (ResumeAsync) | ✅ Done (B2) |

**NOT yet implemented** — this change's scope:

| Component | Status |
|-----------|--------|
| `TraversalEngineConfig.Hooks` config field | ❌ Need to add + migrate from `RegisterHook()` |
| OnBeforeRun call point | ❌ Need to wire |
| OnAfterRun call point | ❌ Need to wire |
| OnBeforeStep call point | ❌ Need to wire |
| OnAfterStep call point | ❌ Need to wire |
| OnError (fatal) call point | ❌ Need to wire |
| OnError (recoverable) call point | ❌ Need to wire |
| TraversalHookTests.cs | ❌ Need to create |

## 4. Changes Required

### 4.1 Hook Registration Migration: RegisterHook → Config Field

**Problem**: Current `RegisterHook()` + `List<ITraversalHook>` is mutable, violating record init-only principle and introducing concurrency risk (hooks can be added mid-run).

**Change**:
```csharp
// TraversalEngineConfig — add Hooks field
public sealed record class TraversalEngineConfig
{
    // ... existing fields ...
    /// <summary>Lifecycle hooks — immutable, set at construction. Empty = zero overhead.</summary>
    public ImmutableArray<ITraversalHook> Hooks { get; init; } = ImmutableArray<ITraversalHook>.Empty;
}
```

- Delete `RegisterHook()` method and `_hooks` field from TraversalEngine
- Add `_hooks = _config.Hooks` field assignment in Initialize() (ImmutableArray, not List)
- `FireAsync` uses `_hooks.Length` (ImmutableArray has `.Length`, zero-overhead empty check)
- Hook order = config registration order, first registered = first called

**Migration for existing callers**: `TraversalEnginePauseResumeTests` uses `engine.RegisterHook(hook)` → change to `TraversalEngineConfig { Hooks = [hook] }` in engine constructor.

### 4.2 FireAsync Catch Block Improvement

**Problem**: Current `catch { }` silently swallows hook exceptions. Inconsistent with dispatch-table pattern (TraceCoordinator uses `Console.WriteLine`).

**Change**:
```csharp
private async Task FireAsync(Func<ITraversalHook, Task> selector)
{
    if (_hooks.Length == 0) return;  // ImmutableArray.Length for zero-overhead
    foreach (var hook in _hooks)
    {
        try { await selector(hook); }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hook Warning] {ex.GetType().Name}: {ex.Message}");
            // Log-and-Continue — hook 异常不传播，不影响引擎主流程
        }
    }
}
```

Consistent with dispatch-table.md §Log-and-Continue sub-pattern and TraceCoordinator's `Console.WriteLine` approach.

### 4.3 Five Call Point Insertions

**TraversalEngine.cs** (5 call points):

| Lifecycle Node | Insertion Point | Exact Location | Notes |
|----------------|----------------|----------------|-------|
| OnBeforeRun | Inside `try` block, before `for` loop | After L231 local vars, before L234 `for (int i...)` | Hook sees plan + context before step loop |
| OnAfterStep | After step processing, BEFORE termination checks | After L290 (`lastPageId = GetCurrentPageId()`), before L293 (`if stepResult.FrameCompleted...`) | Must fire before termination to cover the last step |
| OnAfterRun | Inside `Done()`, after TraversalResult construction | After L480 (TraversalResult constructed), before `return` | Done() must be refactored: build result → fire hook → return |
| OnError (fatal) | `catch(Exception)` block, before Done(Error) | L362-366, before `return Done(Reasons.Error, ...)` | `IsRecoverable=false` |
| OnBeforeStep | After pause-gate, before vision analysis | After L246 (`ct.ThrowIfCancellationRequested()`), before L250 (`_vision.AnalyzeCurrentPageAsync`) | Hook sees context before expensive vision call |

**Recoverable OnError** — **方案 A: 引擎层拦截** (recommended):

| Lifecycle Node | Insertion Point | Exact Location | Notes |
|----------------|----------------|----------------|-------|
| OnError (recoverable) | Step loop, after stepResult returns ErrorHandling | After `stepResult = await _orchestrator.ExecuteStepAsync(...)`, check `stepResult.NextState == ErrorHandling` | `IsRecoverable=true`; FSM 内部无需访问 hooks |

**Why 方案 A over 方案 B**:
- 方案 B (传递 hooks 到 FSM) 让 FSM 依赖 hook，违反 FSM 独立性原则 (C-4) 和职责隔离
- 方案 A 时机稍晚（error 已被 FSM 处理后）但可接受 — hook 是观察者，不是决策者
- 引擎层拦截只需在 step loop 加一个 `if` check，零侵入

```csharp
// Step loop (方案 A):
var stepResult = await _orchestrator.ExecuteStepAsync(_stepCtx);

// Recoverable error detection (方案 A)
if (stepResult.NextState == TraversalState.ErrorHandling && _ctx.LastError != null)
{
    await FireAsync(h => h.OnErrorAsync(
        new TraversalErrorContext(
            ErrorType: _ctx.LastError.GetType().Name,
            Message: _ctx.LastError.Message,
            NodeId: _ctx.CurrentFrame?.NodeId,
            IsRecoverable: true),
        _ctx));
}
```

### 4.4 Done() Refactoring for OnAfterRun

Done() currently constructs TraversalResult inline and returns immediately. Needs to:
1. Build result into a local variable
2. Fire `await FireAsync(h => h.OnAfterRunAsync(result))`
3. Return the result

```csharp
private TraversalResult Done(string reason, ...)
{
    // ... GlobalState mapping (existing) ...

    var result = new TraversalResult(...);  // construct into local

    // OnAfterRun — fire after result construction, before return
    // Done() is called from RunAsync which is async, so FireAsync await is safe
    // But: Done() is currently synchronous — need to make it async or
    //       fire OnAfterRun at each Done() call site in RunAsync instead

    return result;
}
```

**Design decision**: Make `Done()` async is invasive (all call sites change). Simpler: **fire OnAfterRun at each `return Done(...)` call site in RunAsync**, not inside Done() itself. This means ~5 call sites need `await FireAsync(h => h.OnAfterRunAsync(result))` before returning. Each call site: `var result = Done(...); await FireAsync(h => h.OnAfterRunAsync(result)); return result;`

**Alternative**: Refactor Done() to return `Task<TraversalResult>`. This is cleaner but changes more code. Both approaches are equivalent in hook coverage. **Choosing call-site approach** for minimal invasiveness.

### 4.5 OnBeforeRun at RunAsync Entry

```csharp
public async Task<TraversalResult> RunAsync(CancellationToken ct = default)
{
    // ... local var initialization ...

    // OnBeforeRun — fire before step loop starts
    await FireAsync(h => h.OnBeforeRunAsync(_plan, _ctx));

    try
    {
        for (int i = 0; i < _config.MaxSteps; i++)
        { ... }
    }
    ...
}
```

Note: OnBeforeRun fires **inside** the `try` block? No — if OnBeforeRun throws, it should NOT be caught by the RunAsync catch block (which converts to Done(Error)). OnBeforeRun failure should propagate to caller. **Decision**: fire OnBeforeRun **outside** try block, before the step loop. If a hook throws here, it's a setup failure, not a traversal error.

But FireAsync catches hook exceptions (Log-and-Continue). So even if a hook throws in OnBeforeRun, the engine continues. This is consistent with the dispatch-table pattern. **No special handling needed** — OnBeforeRun goes before `try`, FireAsync handles its own exceptions.

## 5. TraversalErrorContext Naming

The interface's error parameter type is `TraversalErrorContext` (not `ErrorContext`). This avoids confusion with `StateMachine.Error.ErrorContext` (a full mutable error tracking state). The design spec originally called it `ErrorContext`; the code uses `TraversalErrorContext` which is better. **No rename needed** — keep `TraversalErrorContext`.

## 6. Testing Strategy

### Test File

`tests/UniClaw.Core.Tests/Traversal/TraversalHookTests.cs`

### Test Matrix

| Test Group | Coverage | Est. Count |
|------------|----------|------------|
| Empty Hook list | `Hooks=[]` → engine runs normally, zero overhead, `_hooks.Length == 0` shortcut | 1 |
| Single Hook counting | 1 `CountingHook` → each OnXxx fires with correct count | 1 |
| Multiple Hook order | HookA + HookB → A fires before B (registration order) | 1 |
| Hook throws exception | Hook throws → engine continues + `Console.WriteLine` warning | 1 |
| OnBeforeRun/OnAfterRun timing | OnBeforeRun fires before step loop; OnAfterRun fires after Done() at each exit path | 2 |
| OnBeforeStep/OnAfterStep timing | OnBeforeStep before vision; OnAfterStep after step, including terminating step | 1 |
| OnError recoverable | FSM ErrorHandling state → `IsRecoverable=true` | 1 |
| OnError fatal | Engine-level exception → `IsRecoverable=false` | 1 |
| TraversalHookBase no-op | Inherit base without override → all `Task.CompletedTask` | 1 |
| Config field registration | `TraversalEngineConfig { Hooks = [hook] }` → hook available; `RegisterHook()` no longer exists | 1 |

**Total: ~10-11 tests**. Existing `TraversalEnginePauseResumeTests` `CaptureHook` usage migrates from `RegisterHook()` to config field.

## 7. Out of Scope

- ❌ IVerificationHook (test-level verify hook — independent change)
- ❌ Decorator pattern addition (YAGNI — Hook covers Run-level needs)
- ❌ Hook parallel execution (sequential is sufficient)
- ❌ Hook priority/ordering mechanism (registration order = ordering, YAGNI)
- ❌ Concrete Hook implementations (MetricsHook, LoggingHook — upper layer or independent change)
- ❌ Hook short-circuit capability (no current need to prevent engine execution)
- ❌ Making Done() async (call-site approach is sufficient for OnAfterRun)
