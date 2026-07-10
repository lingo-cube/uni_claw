# Tier 2 · Patterns — Handler Pipeline

> Update frequency: on pipeline-step addition/removal, classifier sub-method change, or executor hook signature change.
> Triggers: any commit that modifies `PopupHandler.HandlePopup`, `CompletionDetector.DetectCompletion`, `ErrorClassifier.Classify`, or their executor dispatch tables.

## Pattern Definition

The **Handler Pipeline** is a 5-step sequential orchestration pattern:

```
detect → classify → decide → execute → statistics
```

**PopupHandler** follows this sequence as a unified 6-step pipeline (with a StateRestorer lifecycle wrapping the execute step). **Container and Error** do NOT follow this pattern as unified pipelines — they are 3 independent sub-components without a wrapper class (→ D-16). An external caller must invoke each step manually in sequence.

For PopupHandler, the pipeline guarantees ordered execution — each step completes before the next begins. No step runs concurrently or short-circuits into a later step (except `detect` returning early on null/empty input). For Container/Error, sequential ordering depends on the caller; no single method chains the three components.

## Five-Step Pipeline Breakdown

### 1. Detect

Converts raw input into a primary enum value. The detector is a pure function with no side effects. It may return an "Unknown" sentinel when input is null, empty, or unrecognized, causing an early return before classification begins.

| Handler | Detector class | Input type | Output type | Priority mechanism | Unknown behavior |
|---------|---------------|-----------|------------|--------------------|-----------------|
| Popup | `PopupDetector` | `string` (popup text) | `PopupType` (5 values) | 4-type priority loop + regex matching per type | Returns `PopupType.Unknown`; empty/null input also returns Unknown, then pipeline returns `PopupHandlingResult(false, "no_popup", ...)` |
| Container | `CompletionDetector` | `CompletionContext` (7 fields) | `CompletionResult` | 5-priority if-chain (Timeout > MaxDepth > NoChildren > AllVisited > Incomplete) | `Incomplete` sentinel — not truly "unknown", but the "not done yet" state |
| Error | `ErrorClassifier` | `ErrorClassificationContext` (4 fields) | `ErrorType` (6 values) | 7-priority substring matching chain + exception-type fallback | Returns `ErrorType.Unknown` as catch-all at priority 7 |

### 2. Classify

Enriches the detection result with secondary classifications. The classifier is also pure — it accepts the detection output plus optional context and produces a multi-field classification record. Sub-methods are called in fixed order; each contributes one field to the final record.

### 3. Decide (strategy selection)

Maps the classification to a dispatch key. This step may involve a priority chain with applicability checks — the first applicable strategy wins. Pure calculation, no side effects.

### 4. Execute (dispatch)

Invokes the hook associated with the dispatch key via a Dispatch Table (see `patterns/dispatch-table.md`). This is the only impure step — hooks may interact with the traversal context. Wrapped in try/catch so exceptions never propagate to the caller.

### 5. Statistics

Increments counters tracking handler invocations. Only PopupHandler currently implements this step; ContainerHandler and ErrorHandler classifiers are pure calculation with no mutable state.

## Handler Comparison Table

| Aspect | PopupHandler | Container (3 sub-components) | Error (3 sub-components) |
|--------|-------------|---------------------------|--------------------------|
| **Source file** | `StateMachine/PopupHandler.cs` | `StateMachine/ContainerHandler.cs` | `StateMachine/ErrorHandler.cs` |
| **Pipeline orchestrator** | `PopupHandler.HandlePopup()` (explicit 6-step method) | `ContainerHandler.HandleContainer()` (explicit 3-step method) | `ErrorHandler.HandleError()` (explicit 3-step method) |
| **Input type** | `string popupText` + `ITraversalContext` + `List<string>? availableButtons` | `CompletionContext` (7-field record) | `ErrorClassificationContext` (4-field record) |
| **Detector class** | `PopupDetector` (4-type priority loop, regex per type) | `CompletionDetector` (5-priority if-chain) | `ErrorClassifier` (7-priority substring chain) |
| **Classifier class** | `PopupClassifier` (5 sub-methods) | Not separate — `CompletionDetector` output is the classification | Not separate — `ErrorClassifier` output is the classification |
| **Classifier sub-methods** | 5: determine_type → find_dismiss → strategy → urgency → blocking | 0 (detection = classification) | 0 (detection = classification) |
| **Decision class** | Conditional logic in classifier (`DetermineDismissStrategy(PopupType, string? dismissTarget)` — D-10) | `FallbackDecider` (4-case priority) | `ErrorStrategySelector` (per-type priority chain + applicability check) |
| **Decision priority levels** | 2: target-based conditional (has dismiss target → AutoClose; no target → type fallback) — D-10 | 4: Timeout/MaxDepth → BACK; AllVisited → use suggested; canContinue=false → BACK; else → SKIP | 6 type-specific chains, each 1-3 strategies long, with `IsApplicable` guard per strategy |
| **Executor class** | `PopupActionExecutor` | `ContainerActionExecutor` | `RecoveryExecutor` |
| **Dispatch key enum** | `PopupType` (5 values) | `FallbackAction` (4 values) | `ErrorStrategy` (5 values) |
| **Dispatch hooks** | 5: Permission, Error, Ad, Dialog, Unknown | 4: Back, AutoEscape, Skip, Abort | 5: Retry, Backtrack, Skip, Continue, Abort |
| **Classification result type** | `PopupClassification` (5 fields) | `CompletionResult` (4 fields) | `ErrorType` (single enum value) |
| **Context type** | `PopupContext` (Classification + ITraversalContext) | `ContainerContext` (NodeId, Depth, ITraversalContext) | `ErrorRecoveryContext` (ErrorType, RetryCount, Exception?) |
| **Execution result type** | `PopupHandlingResult` (Success, Action, Description) | `ContainerActionResult` (Action, Success, Description) | `ErrorRecoveryResult` (Strategy, Outcome, BackoffDelaySeconds) |
| **Fallback behavior (executor)** | Exception → `PopupHandlingResult(false, "back_fallback", ...)` | Exception → `DefaultBack(ctx)` — same as explicit Back hook | Exception → `DefaultAbort(ctx)` — sets `Outcome=Failure` |
| **Fallback behavior (top-level)** | Top-level try/catch → `PopupHandlingResult(false, "back_fallback", ...)` for any step exception | No top-level wrapper (executor is the only impure step) | No top-level wrapper (executor is the only impure step) |
| **Statistics tracked** | `_detectedCount`, `_handledCount`, `_handlingStatistics` (per PopupType), `HandlingRate` | Not tracked (pure calculation) | Not tracked (pure calculation) |
| **Statistics result type** | `PopupHandlerStatistics` (3 fields + computed `HandlingRate`) | N/A | N/A |
| **State preservation** | Yes — `StateRestorer` lifecycle (PopupHandler-specific) | No | No |
| **Special features** | Regex pattern matching (4 popup types x 5-6 patterns) | `CompletionContext` includes `ElapsedMs`/`TimeoutMs` for timeout detection | Exponential backoff on Retry: `min(2^retryCount, 10)` seconds |

## Dispatch Table Pattern in Each Handler

Each handler's executor follows the Dispatch Table pattern documented in `patterns/dispatch-table.md`. The common structure is:

```csharp
Dictionary<EnumKey, Func<Context, Result>> _dispatchTable;
```

Populated in the constructor with `?? DefaultXxx` for each optional hook parameter. Execution always proceeds through `TryGetValue` lookup, with a key-not-found default and an exception-catching fallback.

### PopupActionExecutor

- **Key**: `PopupType` (5 values: Permission, Error, Ad, Dialog, Unknown)
- **Hooks**: 5 optional constructor params → `?? DefaultXxx`
- **Key-not-found**: `DefaultUnknown`
- **Exception fallback**: Inline `PopupHandlingResult(false, "back_fallback", "Exception during popup handling")`
- **Fallback semantics**: Navigate back — the universally safe popup dismissal

### ContainerActionExecutor

- **Key**: `FallbackAction` (4 values: Back, AutoEscape, Skip, Abort)
- **Hooks**: 4 optional constructor params → `?? DefaultXxx`
- **Key-not-found**: `DefaultBack` — reuses the explicit Back hook rather than constructing a new result inline
- **Exception fallback**: `DefaultBack(ctx)` — same reuse pattern
- **Fallback semantics**: Navigate back — the universally safe container exit

### RecoveryExecutor

- **Key**: `ErrorStrategy` (5 values: Retry, Backtrack, Skip, Continue, Abort)
- **Hooks**: 5 optional constructor params → `?? DefaultXxx`
- **Key-not-found**: `DefaultAbort`
- **Exception fallback**: `DefaultAbort(ctx)` — sets `RecoveryOutcome.Failure` (not `Success`)
- **Fallback semantics**: Abort traversal — the universally safe termination
- **Difference**: Unlike Popup/Container whose fallback produces a `Success=true` result, RecoveryExecutor's abort fallback signals `Failure`, reflecting that abort is a loss, not a graceful exit.

## Fallback Chain

Each handler has a **at most two-layer fallback** system:

- **PopupHandler**: 2 layers (executor catch + pipeline catch)
- **Container/Error**: 2 layers (executor catch + pipeline catch in HandleContainer/HandleError)

### Layer 1: Executor-level fallback

Inside the executor's `Execute` method, exceptions are caught and replaced with a terminal fallback result. The exception never propagates beyond `Execute`. See `patterns/dispatch-table.md` for the detailed three-step sequence (lookup → invoke → catch-fallback).

### Layer 2: Pipeline-level fallback (PopupHandler only)

`PopupHandler.HandlePopup` wraps the entire 6-step pipeline in its own try/catch:

```csharp
try
{
    // Step 1: detect
    // Step 2: classify
    // Step 3: preserve
    // Step 4: handle (dispatch)
    // Step 5: restore
    // Step 6: validate
}
catch (Exception ex)
{
    return new PopupHandlingResult(false, "back_fallback",
        $"Unhandled exception during popup handling: {ex.GetType().Name}: {ex.Message}");
}
```

This means any exception from **any** pipeline step — not just the executor — is caught. The executor already catches its own exceptions internally, so the top-level catch primarily protects the classify, preserve, restore, and validate steps.

Container and Error now have pipeline-level fallback wrappers via `ContainerHandler.HandleContainer()` and `ErrorHandler.HandleError()`. The executor-level catch is the inner fallback layer; the pipeline catch is the outer layer (→ D-16 Fixed).

### Fallback Semantics Table

| Handler | Executor fallback | Pipeline fallback | Fallback action semantics |
|---------|------------------|-------------------|--------------------------|
| Popup | `PopupHandlingResult(false, "back_fallback", ...)` | Same result (also `"back_fallback"`) | Press back — dismisses any popup type |
| Container | `DefaultBack(ctx)` → `ContainerActionResult(Back, true, ...)` | `ContainerActionResult(Back, false, ...)` — "Unhandled exception..." with `ex.GetType().Name:ex.Message` | Press back — exits any container |
| Error | `DefaultAbort(ctx)` → `ErrorRecoveryResult(Abort, Failure, 0)` | `ErrorRecoveryResult(Abort, Failure, 0, "Unhandled exception...")` — with Description field | Abort traversal — safest termination |

Note that PopupHandler's executor fallback and pipeline fallback produce structurally identical results (`Action="back_fallback"`). The difference is the `Description` field: the executor fallback says "Exception during popup handling" while the pipeline fallback includes `ex.GetType().Name` and `ex.Message`.

## StateRestorer Lifecycle (PopupHandler-Specific)

PopupHandler has a unique 3-step lifecycle that wraps the execute step:

```
preserve → (execute) → restore → validate
```

This is steps 3-6 in the 6-step pipeline, making the full sequence:

```
detect → classify → preserve → handle → restore → validate
```

### Preserve (`StateRestorer.PreserveState`)

Before executing the popup action, the handler saves the complete traversal context state into an internal `Dictionary<string, PreservedState>`. The preserved state includes:

| Field saved | Source | Purpose |
|-------------|--------|---------|
| `StateId` | `Guid.NewGuid().ToString("N")` | Unique key for restoration |
| `CurrentNodeId` | `context.CurrentFrame?.NodeId` | Track which node was active before popup |
| `NodeStackFrames` | All `IStackFrame` objects (peeked from index 0 to depth-1) | Full stack content, not just depth counter |
| `CurrentState` | `context.GlobalState` | FSM state before popup |
| `ExecutionResult` | `context.LastError?.Message` | Error state before popup |
| `Timestamp` | `DateTimeOffset.UtcNow` | Audit trail |

### Restore (`StateRestorer.RestoreState`)

After the popup action, the handler restores the context from preserved state:

1. **Restore CurrentFrame** — from the top of preserved stack frames
2. **Restore NodeStack** — clear then push bottom-first to restore correct order
3. **Restore GlobalState** — direct assignment from preserved value
4. **Restore LastError** — reconstruct from preserved `ExecutionResult` string

The NodeStack restoration is bottom-first because the preserved frames are in top-to-bottom order (Peek(0)=top). Reversing the order and pushing each frame restores the correct stack structure.

### Validate (`StateRestorer.ValidateRestoredState`)

After restoration, the handler validates state integrity with two levels of checks:

| Check level | What it compares | Failure behavior |
|-------------|-----------------|-----------------|
| **Structural** (always) | CurrentFrame.NodeId is non-null/non-empty; NodeStack.Depth >= 1; GlobalState is defined enum value | Collects error string into list |
| **Value comparison** (when stateId provided) | `CurrentFrame.NodeId` vs `PreservedState.CurrentNodeId`; `GlobalState` vs `PreservedState.CurrentState`; `NodeStack.Depth` vs `PreservedState.NodeStackFrames.Count` | Collects mismatch description into list |

If any check fails, `ValidateRestoredState` returns `StateValidationResult(false, errors)`, and `HandlePopup` returns `PopupHandlingResult(false, "validation_failed", ...)` with the error list in the description.

### Why Only PopupHandler Has This Lifecycle

Popup handling modifies the traversal context (pressing back, closing dialogs) and must restore the context to its pre-popup state so the traversal can continue from where it was interrupted. Container and Error handlers either operate on immutable context records (Container) or terminate the traversal (Error abort) — neither needs a preserve/restore cycle.

## Statistics Tracking

| Handler | Tracking mechanism | What is tracked | Computed metrics |
|---------|-------------------|-----------------|-----------------|
| Popup | `_detectedCount` (int), `_handledCount` (int), `_handlingStatistics` (Dictionary<PopupType, int>) | Per-PopupType detection counts; total detected vs handled | `HandlingRate = HandledCount / DetectedCount` (0.0 if none detected) |
| Container | None | CompletionDetector and FallbackDecider are pure calculation with no mutable state | N/A |
| Error | None | ErrorClassifier and ErrorStrategySelector are pure calculation with no mutable state | N/A |

### PopupHandler Statistics Lifecycle

Statistics are incremented at two points in the pipeline:

1. **After detect** (step 1): `_detectedCount++` and `_handlingStatistics[popupType]++` — counts every popup that enters the pipeline, regardless of outcome.
2. **After successful execute** (step 4, post-validate): `_handledCount++` — only incremented when `handlingResult.Success` is true and validation passes.

This means `HandlingRate` reflects the ratio of fully successful popup dismissals to total detected popups. Popups that fail classification, fail validation, or throw exceptions in any pipeline step count as detected but not handled.

### Statistics Result Type

```csharp
public sealed record class PopupHandlerStatistics(
    int DetectedCount,
    int HandledCount,
    Dictionary<PopupType, int> HandlingStatistics)
{
    public double HandlingRate => DetectedCount > 0 ? HandledCount / (double)DetectedCount : 0.0;
}
```

The `PopupHandler.GetStatistics()` method returns a snapshot. The dictionary is copied (`new Dictionary<PopupType, int>(_handlingStatistics)`) to prevent mutation of the handler's internal state through the returned object.

## Relationship to Dispatch Table Pattern

The handler pipeline and dispatch table patterns are **layered, not alternatives**. Each handler's pipeline orchestrates multiple steps; one of those steps is "dispatch via executor," which uses the dispatch-table pattern internally. The pipeline provides the surrounding lifecycle (detect, classify, preserve, restore, validate), while the dispatch table provides the decision-making core.

For the dispatch-table internals (hook injection, exception fallback, key-not-found defaults), see `patterns/dispatch-table.md`.

## Constitution Constraints

Several project constitution rules govern this pattern:

| Constraint | How it applies |
|------------|---------------|
| **Domain.Vision ↔ Domain.Content zero direct import** | Handlers operate on `ITraversalContext` and their own enums — no cross-domain imports between Vision and Content types |
| **All records sealed record class + ImmutableArray** | Classification results (`PopupClassification`, `CompletionResult`, `CompletionContext`) use `sealed record class`; pattern registries use `ImmutableArray<string>` |
| **All validation DomainValidationException** | Not used here — handlers use try/catch with fallback results rather than fail-fast exceptions, because handler failures are recoverable (not domain-level constraint violations) |
| **No ToDictionary/FromDictionary** | Handlers do not convert domain models to dictionaries; they use typed records and enum-keyed dispatch tables (which are internal execution infrastructure, not serialization) |
| **Enum value locks** | `PopupType` (5), `UrgencyLevel` (3 — D-11), `BlockingType` (3), `DismissStrategy` (4), `ErrorType` (6), `ErrorStrategy` (5), `FallbackAction` (4), `CompletionReason` (4) — adding values requires spec review, not ad-hoc extension |
