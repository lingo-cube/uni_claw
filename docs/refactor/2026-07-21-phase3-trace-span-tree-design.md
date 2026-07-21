# Phase 3-A & 3-B: Trace Span Tree Extension — PRD

> Date: 2026-07-21
> Status: Draft
> Prerequisites: C-9/C-10/C-8 (trace-collection-completion, 42/42 tasks, 833 tests pass)
> Decisions: D-115 (TraceHandlerAttribute documentation-only), D-116 (TraceContext +2 fields)
> Design: `/Users/fran/.claude/plans/melodic-coalescing-corbato.md`

## 1. Summary

C-9/C-10/C-8 completed the trace collection pipeline (Handler lifecycle, Operation timing, State flow), leaving three gaps:

| Gap | Current State | Target |
|-----|--------------|--------|
| **No span-tree correlation** | TraceContext has 4 fields, cannot express parent-child span relationships | 6 fields: +VisitSpanId, +ParentSpanId |
| **HandlerTraceWriter Context=null** | Handler lifecycle ExecutionRecords have no TraceContext — uncorrelated with engine state | HandlerTraceWriter accepts TraceContext |
| **6 manual trace injection points** | Orchestration layer has repeated RecordHandlerLifecycleAsync boilerplate | Roslyn source generator automates 3 handler wrappers |

Phase 3-A fixes the data layer. Phase 3-B fixes the automation layer. Together they enable automatic span-tree construction across all 5 record types without per-type schema changes.

## 2. Design Decisions

### 2.1 Stack-based ParentSpanId (not AsyncLocal)

**Decision:** TraceCoordinator maintains an internal `Stack<string?> _spanStack`. `PushSpan()` generates a SpanId and pushes it. `BuildCorrelation()` reads the stack top as `ParentSpanId`. `PopSpan()` pops on exit.

**Rationale:** The entire Observability layer uses explicit mutable state (`_currentStepSpanId` is a field on TraceCoordinator). AsyncLocal introduces an implicit propagation model inconsistent with this pattern. Stack is inspectable in debugger, immune to `Task.Run`/`ConfigureAwait(false)` boundary issues, and the manual push/pop pairs are eliminated in Phase 3-B by the source generator.

### 2.2 Source generator with auto-extract + extraMetadata (方案 D)

**Decision:** The Roslyn source generator inspects the handler return type at compile time, generates metadata extraction code for all readable properties (null-skipping, enum→string), and accepts an optional `extraMetadata` dictionary for cross-source fields (e.g., `consecutive_errors` from context, not result). Properties to exclude are marked `[TraceIgnore]`.

**Rationale:** 方案 A (ITraceHandlerMetadata interface) pollutes handler result types with Observability concerns — violating D-111 (trace injection at orchestration layer, not inside handlers). 方案 B (attribute-based field mapping) uses string member names with no compile-time safety. 方案 C (callback lambda) keeps the orchestration layer writing metadata lambdas per handler. 方案 D maximizes automation while keeping handlers pure: the generator handles timing + metadata extraction from result type, the orchestration layer only supplies cross-source fields via a 1-line dictionary.

### 2.3 HandlerTraceWriter accept TraceContext

**Decision:** `RecordHandlerLifecycleAsync` gains an optional `TraceContext? context = null` parameter (方案 A: explicit parameter). The orchestration layer (which already constructs TraceContext via BuildCorrelation) passes it in.

**Rationale:** HandlerTraceWriter is stateless (方案 B constructor injection couples it to engine lifecycle). Explicit parameter keeps HandlerTraceWriter testable with null context and mirrors the existing pattern: every `ITraceRecorder.RecordExecutionAsync` already receives Context from BuildCorrelation.

## 3. Phase 3-A: TraceContext +2 Fields + HandlerTraceWriter Fix

### 3.1 TraceContext Extension

```csharp
// Before
public sealed record class TraceContext(
    string? NodeId = null, string? StepSpanId = null,
    int? StepNumber = null, string? TraceId = null);

// After
public sealed record class TraceContext(
    string? NodeId = null, string? StepSpanId = null,
    int? StepNumber = null, string? TraceId = null,
    string? VisitSpanId = null,    // Node visit span
    string? ParentSpanId = null);  // Parent span in span tree
```

### 3.2 TraceCoordinator Changes

Three new members on `TraceCoordinator`:

| Member | Purpose |
|--------|---------|
| `_currentVisitSpanId` | Set on node entry (RecordSkipSpanAsync/RecordDynamicLifecycleAsync), cleared on exit |
| `_spanStack` | Stack of active span IDs — BuildCorrelation reads top as ParentSpanId |
| `PushSpan()` | Generates SpanId, pushes to stack, returns it |
| `PopSpan(spanId)` | Pops if top matches |
| `ClearVisitSpan()` | Nulls _currentVisitSpanId |

`BuildCorrelation()` updated to read `_currentVisitSpanId` and `_spanStack.Peek()` as ParentSpanId.

### 3.3 HandlerTraceWriter Fix

```csharp
// Before
Task RecordHandlerLifecycleAsync(string action, SpanType spanType,
    string status = "ok", Dictionary<string, object>? metadata = null,
    CancellationToken cancellationToken = default);

// After
Task RecordHandlerLifecycleAsync(string action, SpanType spanType,
    string status = "ok", Dictionary<string, object>? metadata = null,
    TraceContext? context = null,
    CancellationToken cancellationToken = default);
```

HandlerTraceWriter now sets ExecutionRecord.Context — fixing the correlation gap where handler lifecycle records had null NodeId/StepSpanId/StepNumber/TraceId.

### 3.4 Auto-compatible

No changes needed for:
- **JSONL serialization**: STJ automatically serializes 6 fields (camelCase, omit null via WhenWritingNull)
- **5 record types**: All have `TraceContext? Context = null` with default — new fields default to null
- **Existing `new TraceContext(...)` calls**: Positional params unchanged; new fields default to null
- **6 query methods**: None depend on VisitSpanId/ParentSpanId yet

## 4. Phase 3-B: Roslyn Source Generator

### 4.1 Scope

**Automated (3 handler pipeline methods):**

| Handler | Method | [TraceHandler] | Return Type |
|---------|--------|---------------|-------------|
| PopupHandler | HandlePopup | `[TraceHandler(SpanType.PopupHandling, "handle_popup")]` | PopupHandlingResult |
| ErrorHandler | HandleError | `[TraceHandler(SpanType.ErrorHandling, "handle_error")]` | ErrorRecoveryResult |
| ContainerHandler | HandleContainer | `[TraceHandler(SpanType.ContainerHandling, "handle_container")]` | ContainerActionResult |

**Remains manual (3 DfsBacktrack insertion points):**
- `TraversalEngine.RunAsync` — leaf_execution_complete (inside if block)
- `InterceptionHandler.OnDynamicMatchNodeSelect` — pop_only (inside if block)
- `InterceptionHandler.OnDynamicMatchNodeSelect` — press_back (inside if block)

**Out of scope:** IVisionProvider (async already, RecordAICallSpanAsync unused in production).

### 4.2 Project Structure

```
src/UniClaw.Core.SourceGen/          ← NEW netstandard2.0 project
  UniClaw.Core.SourceGen.csproj
  TraceHandlerGenerator.cs            ← IIncrementalGenerator
  Emitter.cs                          ← C# code generation

src/UniClaw.Core/UniClaw.Core.csproj ← +ProjectReference (OutputItemType=Analyzer)
src/UniClaw.Core/Observability/      ← +TraceIgnoreAttribute.cs
```

### 4.3 Generated Code Pattern

For each method decorated with `[TraceHandler]`, the generator emits:

1. **async wrapper method** (`HandleXxxTracedAsync`) — original stays sync, wrapper is async for trace calls
2. **Auto-extracted metadata** — all readable properties of the return type (null-skipped, enum→string, struct→ToString)
3. **extraMetadata** parameter — for cross-source fields the orchestration layer supplies
4. **PushSpan/PopSpan** in try/finally — sets up ParentSpanId for all nested spans
5. **Exception handling** — records "fail" + exception type, then rethrows

```
[TraceHandler] on method
    ↓ compile time
Generated partial class + async wrapper
    ↓ orchestration layer calls wrapper
PushSpan → method → auto-metadata + extraMetadata → RecordHandlerLifecycle → PopSpan
```

### 4.4 New Types

**`TraceIgnoreAttribute`** — marks a return type property to exclude from auto-generated metadata:
```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class TraceIgnoreAttribute : Attribute { }
```

## 5. Migration Path

```
Phase 3-A
  ├── TraceContext: 4→6 fields (new fields default null)
  ├── TraceCoordinator: +SpanStack + PushSpan/PopSpan/ClearVisitSpan
  ├── HandlerTraceWriter: +TraceContext parameter
  ├── Guard: TraceContext_Has6Fields
  └── 833 tests green (no behavioral change)

Phase 3-B Step 1: Deploy generator project (zero output yet)
Phase 3-B Step 2: Decorate first handler + mark class partial
Phase 3-B Step 3: Switch orchestration to generated wrapper
Phase 3-B Steps 4-6: Repeat for remaining handlers
  └── 833 tests green throughout (manual + generated coexist)
```

**Rollback at any step:** Remove `[TraceHandler]`, revert orchestration call site back to manual pattern.

## 6. Test Strategy

### Phase 3-A (10 tests)

| # | Test |
|---|------|
| 1 | VisitSpanId set on node entry (RecordSkipSpanAsync) |
| 2 | VisitSpanId cleared on exit (ClearVisitSpan) |
| 3 | ParentSpanId from stack (PushSpan → BuildCorrelation) |
| 4 | ParentSpanId null when stack empty |
| 5 | Nested: ParentSpanId = grandparent on 2nd push |
| 6 | 6-field TraceContext serialization round-trip |
| 7 | null fields omitted from JSON |
| 8 | VisitSpanId lifecycle (entry → child spans → exit) |
| 9 | ArchitectureGuard: TraceContext_Has6Fields |
| 10 | Backward compat: 4-field JSON deserializes (new fields null) |

### Phase 3-B (12 tests)

| # | Test |
|---|------|
| 1 | Generator detects [TraceHandler] on methods |
| 2 | Generator emits partial class with traced wrapper |
| 3 | Wrapper contains PushSpan before body, PopSpan in finally |
| 4 | Wrapper records lifecycle on success |
| 5 | Wrapper records error on exception |
| 6 | Wrapper preserves return value |
| 7 | Auto-extract: result properties → metadata dict |
| 8 | [TraceIgnore] excludes property from auto-extract |
| 9 | Null properties skipped |
| 10 | extraMetadata merged with auto-extracted |
| 11 | Manual calls coexist with generated wrappers |
| 12 | End-to-end: PopupHandler traced via generated wrapper |

## 7. File Change List

### Phase 3-A (8 files)

| File | Change |
|------|--------|
| `Observability/TraceContext.cs` | +2 fields |
| `Observability/HandlerTraceWriter.cs` | +TraceContext parameter |
| `Observability/IHandlerTraceWriter.cs` | +TraceContext parameter |
| `Traversal/TraversalEngine.cs` | TraceCoordinator: +SpanStack + PushSpan/PopSpan/ClearVisitSpan + BuildCorrelation update |
| `StateMachine/TraversalFSM.cs` | HandlerTrace calls pass context |
| `Traversal/InterceptionHandler.cs` | HandlerTrace calls pass context |
| `tests/.../ArchitectureGuardTests.cs` | 4→6 fields guard |
| `tests/.../TraceCollectionCompletionTests.cs` | 4→6 fields guard |

### Phase 3-B (11 files)

| File | Change |
|------|--------|
| `src/UniClaw.Core.SourceGen/UniClaw.Core.SourceGen.csproj` | New project (netstandard2.0) |
| `src/.../TraceHandlerGenerator.cs` | IIncrementalGenerator |
| `src/.../Emitter.cs` | Source code emission |
| `src/.../TraceIgnoreAttribute.cs` | New attribute |
| `src/UniClaw.Core/UniClaw.Core.csproj` | +ProjectReference → SourceGen |
| `src/UniClaw.Core.sln` | +SourceGen project |
| `Observability/TraceHandlerAttribute.cs` | XML doc update |
| `StateMachine/PopupHandler.cs` | +partial + [TraceHandler] |
| `StateMachine/ErrorHandler.cs` | +partial + [TraceHandler] |
| `StateMachine/ContainerHandler.cs` | +partial + [TraceHandler] |
| `StateMachine/TraversalFSM.cs` | Switch to generated wrappers |

## 8. Success Criteria

- [ ] TraceContext has 6 fields (ArchitectureGuard confirms)
- [ ] HandlerTraceWriter sets Context on ExecutionRecord
- [ ] ParentSpanId automatically flows to nested spans via SpanStack
- [ ] Roslyn source generator compiles and emits wrapper methods
- [ ] 3 handler pipeline methods traced via generated wrappers
- [ ] 833 existing tests pass throughout migration (no regression)
- [ ] Phase 3-A backward compatible: old 4-field JSONL still readable
- [ ] Phase 3-B non-breaking: manual C-10 calls coexist with generated code during migration
