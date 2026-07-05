# Tier 2 · Patterns — Dispatch Table + Fallback Chain

> Update frequency: on implementation change (enum values, hook signatures, or fallback behavior altered)

## Pattern Definition

**Dispatch Table + Fallback Chain** is a two-phase execution pattern:

1. **Dispatch phase** — A `Dictionary<EnumKey, Func<Context, Result>>` maps each enum value to a hook function. Execution begins by looking up the key in the dictionary and invoking the corresponding hook.
2. **Fallback phase** — If the hook throws an exception, the exception is caught, logged (implicitly via the result description), and a **terminal fallback behavior** is returned instead. The exception never propagates to the caller.

The pattern guarantees that `Execute(key, ctx)` **always returns a result** — never throws. The caller receives either the hook's intentional output or a safe fallback, with no need for its own try/catch.

## Pattern Steps

```
1. Look up dispatch table by key
   └─ table.TryGetValue(key, out hook)
   └─ not found → invoke default hook (same enum value, no dynamic override)

2. Execute hook(context)
   └─ hook runs, returns Result normally

3. On exception → fallback terminal behavior
   └─ catch (Exception) { return FallbackResult; }
   └─ exception is not re-thrown, not wrapped, not propagated
```

The three-step sequence is always wrapped in a single `try { ... } catch { ... }` block inside the `Execute` method. No step can leak an exception upward.

## Four Instances in This Project

| Aspect | PopupActionExecutor | ContainerActionExecutor | RecoveryExecutor | GlobalFSM callback |
|--------|--------------------|------------------------|-----------------|--------------------|
| **Source file** | `StateMachine/PopupHandler.cs` | `StateMachine/ContainerHandler.cs` | `StateMachine/ErrorHandler.cs` | `StateMachine/GlobalFSM.cs` |
| **Enum key** | `PopupType` (5 values) | `FallbackAction` (4 values) | `ErrorStrategy` (5 values) | `GlobalState` (8 values) |
| **Hook type** | `Func<PopupContext, PopupHandlingResult>` | `Func<ContainerContext, ContainerActionResult>` | `Func<ErrorRecoveryContext, ErrorRecoveryResult>` | `Action<StateTransitionEventArgs>` |
| **Dispatch table field** | `_dispatchTable` (private `Dictionary`) | `_dispatchTable` (private `Dictionary`) | `_dispatchTable` (private `Dictionary`) | `_callbacks` (private `Dictionary<GlobalState, List<Action<...>>>`) |
| **Hook count** | 5 (Permission, Error, Ad, Dialog, Unknown) | 4 (Back, AutoEscape, Skip, Abort) | 5 (Retry, Backtrack, Skip, Continue, Abort) | Up to 8 (one list per GlobalState) |
| **Key-not-found default** | `DefaultUnknown` | `DefaultBack` | `DefaultAbort` | No invocation (empty callback list) |
| **Exception fallback** | `PopupHandlingResult(false, "back_fallback", ...)` | `DefaultBack(ctx)` → `ContainerActionResult(FallbackAction.Back, true, ...)` | `DefaultAbort(ctx)` → `ErrorRecoveryResult(ErrorStrategy.Abort, RecoveryOutcome.Failure, 0)` | Catch + swallow (no return value; `Action` not `Func`) |
| **Fallback semantics** | Navigate back (safest UI action) | Navigate back (safest container exit) | Abort traversal (safest termination) | Do nothing (callback failure must not disrupt FSM) |
| **Hook injection** | 5 optional constructor params | 4 optional constructor params | 5 optional constructor params | `RegisterStateCallback(state, callback)` method |
| **Statistics** | `PopupHandlerStatistics` (detected/handled per type) | Not yet (CompletionDetector is pure calc) | Not yet (ErrorClassifier is pure calc) | `_transitionHistory` (TransitionRecord list) |

### Instance Details

**PopupActionExecutor** — Each `PopupType` maps to a dismiss strategy. The fallback `"back_fallback"` is the universal safe action: pressing back dismisses any popup type. The `PopupHandler` 6-step pipeline (see `patterns/handler-pipeline.md`) wraps this executor in a broader try/catch that also falls back to `"back_fallback"` for any pipeline-step exception.

**ContainerActionExecutor** — Each `FallbackAction` maps to a container exit behavior. The fallback to `DefaultBack` means any unexpected failure still navigates out of the container. The default `DefaultBack` itself is the same function used for the explicit `FallbackAction.Back` key — the fallback path reuses the explicit hook rather than constructing a new result inline.

**RecoveryExecutor** — Each `ErrorStrategy` maps to a recovery action. The fallback to `DefaultAbort` means any unexpected failure terminates traversal. Unlike the other two executors, `DefaultAbort` sets `RecoveryOutcome.Failure` (not `Success`), reflecting that abort is a loss, not a graceful exit. The `Retry` hook also computes exponential backoff: `min(2^retryCount, 10)` seconds.

**GlobalFSM callback** — Not an executor in the strict sense: the dispatch key is the target `GlobalState`, and the "hook" is an `Action` (not `Func`), so there is no result to return. The fallback is simply swallowing the exception — callback failure must not disrupt the FSM transition. Multiple callbacks per state are invoked sequentially; each gets its own try/catch, so one failing callback does not prevent subsequent callbacks from running.

## Log-and-Continue Sub-pattern

All four instances share a structural invariant: **exceptions never propagate to the caller**.

| Instance | Propagation | What caller sees |
|----------|------------|-----------------|
| PopupActionExecutor | Stopped at `Execute` | `PopupHandlingResult` with `Success=false`, `Action="back_fallback"` |
| ContainerActionExecutor | Stopped at `Execute` | `ContainerActionResult` with `FallbackAction.Back`, `Success=true` |
| RecoveryExecutor | Stopped at `Execute` | `ErrorRecoveryResult` with `Strategy=Abort`, `Outcome=Failure` |
| GlobalFSM callback | Stopped at `InvokeCallbacks` | FSM transition completes normally |

The pattern intentionally does not include structured logging (no `ILogger` injection). The exception information is embedded in the result's `Description` field (for the three `Func`-based executors) or silently discarded (for the `Action`-based callbacks). This keeps the pattern pure and testable — no DI dependency, no side-effect channel beyond the result itself.

A secondary benefit: the caller's code remains flat. No try/catch wrapping is needed at the call site, because `Execute` guarantees a result. The `PopupHandler.HandlePopup` method does have its own top-level try/catch, but that protects the broader 6-step pipeline (see `patterns/handler-pipeline.md`), not the executor specifically.

## Hook Injection

All three `Func`-based executors accept optional hook overrides via constructor parameters:

```csharp
// PopupActionExecutor — 5 optional hooks
public PopupActionExecutor(
    Func<PopupContext, PopupHandlingResult>? permissionHook = null,
    Func<PopupContext, PopupHandlingResult>? errorHook = null,
    ...)

// ContainerActionExecutor — 4 optional hooks
public ContainerActionExecutor(
    Func<ContainerContext, ContainerActionResult>? backHook = null,
    ...)

// RecoveryExecutor — 5 optional hooks
public RecoveryExecutor(
    Func<ErrorRecoveryContext, ErrorRecoveryResult>? retryHook = null,
    ...)
```

Each parameter uses `?? DefaultXxx` to fill missing overrides with the production default. In tests, a hook can be substituted with a lambda that records invocations, returns controlled results, or deliberately throws to exercise the fallback path.

The GlobalFSM uses a different injection model: `RegisterStateCallback(GlobalState, Action<...>)` registers callbacks after construction. This allows incremental registration during the FSM lifecycle rather than requiring all hooks upfront.

## Statistics Tracking

| Instance | Tracking mechanism | What is tracked |
|----------|--------------------|----------------|
| PopupHandler | `_detectedCount`, `_handledCount`, `_handlingStatistics` dict | Per-PopupType detection + handling counts; handling rate via `HandlingRate` |
| ContainerHandler | (Not implemented yet) | CompletionDetector and FallbackDecider are pure calculation, no mutable state |
| ErrorHandler | (Not implemented yet) | ErrorClassifier and ErrorStrategySelector are pure calculation, no mutable state |
| GlobalFSM | `_transitionHistory` list | Every transition: `(fromState, toState, reason, timestamp)` |

The executors themselves do not track invocation counts — statistics are tracked at the handler level (PopupHandler) or FSM level (GlobalFSM), which owns the broader pipeline context. See `patterns/handler-pipeline.md` for how statistics fit into the pipeline lifecycle.

## Relationship to Handler Pipeline

This dispatch-table pattern is a **step within** the handler pipeline pattern, not an alternative. Each handler (PopupHandler, ContainerHandler, ErrorHandler) orchestrates a multi-step pipeline where one of the steps is "dispatch via executor." The executor is the decision-making core; the pipeline provides the surrounding lifecycle (detect, classify, preserve, dispatch, restore, validate).

For the full pipeline context, see `patterns/handler-pipeline.md`.
