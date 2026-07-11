# Trace Pipeline Design — TraceCoordinator Fill + InMemoryTraceRecorder + TraceRecord Extension

> Date: 2026-07-10
> Status: Revised (post correctness/extensibility audit)
> Depends on: phase22-refactoring (SpanType enum + PageTransition record + ExecutionRecord.SpanType + ITraceRecorder 2 new methods)
> Vision: see memory/trace-vision.md

## Overview

Fill TraceCoordinator's 14 empty-shell methods with real logic that routes engine events to ITraceRecorder. Create InMemoryTraceRecorder as an independent ITraceRecorder implementation with tree-index support. Extend TraceRecord with 4 new optional fields. Delete TraceNode hierarchy (dead code — never populated) and UlidGenerator (only consumer was TraceNode). Replace TraceCoordinator's GetLastXxx() last-value pattern with StepTraceSnapshot (solves race condition and scales for future trace dimensions). Add ParentNodeId to ExecutionRecord for tree reconstruction. Change RecordAICallSpan from untyped `object ai` to typed parameters.

---

## Section 0: TraceNode Deletion + Model Unification

### Delete TraceNode hierarchy (SessionNode/StepNode/SpanNode)

TraceNode.cs defines a 3-type hierarchy with SpanId/ParentSpanId tree structure, but:
- **Never populated** by engine or any production code
- **3-type model too coarse** for user vision (needs distinct AI call, page transition, error event types)
- **SpanNode.SpanType is string?**, not our SpanType enum — semantic mismatch
- **Tests verify dead code**: TraceNodeTests (3 tests) + TraceNodeHierarchy_ExactlyThreeSubtypes guard test

Delete:
- `src/UniClaw.Core/Trace/TraceNode.cs` (TraceNode abstract + SessionNode + StepNode + SpanNode)
- `src/UniClaw.Core/Common/UlidGenerator.cs` (only consumer was TraceNode.SpanId — zero production references)
- `tests/...TraceNodeTests` (3 tests)
- `tests/...UlidGeneratorTests` (5 tests)
- `TraceNodeHierarchy_ExactlyThreeSubtypes` guard test

ITraceRecorder records become the **single canonical trace data model**. Tree support is built into InMemoryTraceRecorder (ParentNodeId index), not a separate hierarchy.

---

## Section 1: InMemoryTraceRecorder + Data Flow Architecture

### After (ITraceRecorder as canonical model, TraceNode deleted)

```
Engine events → TraceCoordinator → InMemoryTraceRecorder (ITraceRecorder impl)
                                     │
                                     ├─ Flat record storage (StateTransition, ExecutionRecord, PageTransition, etc.)
                                     ├─ Tree index: _byParent[parentId] → List<ExecutionRecord>  (O(1) tree queries)
                                     │
                                     └─ ExpectedBehavior.Verify reads via ITraceRecorder interface

Engine events → List<TraceRecord> (engine-constructed, enriched from StepTraceSnapshot) → TraversalResult.Trace
```

Two data paths exist (engine TraceRecord + ITraceRecorder records) but they serve different purposes:
- **TraversalResult.Trace** (TraceRecord): backward-compatible per-step view for baseline tests + ExpectedBehavior
- **ITraceRecorder records**: canonical event-level data with SpanType, ParentNodeId, Metadata for tree reconstruction and analysis

### InMemoryTraceRecorder

```csharp
public sealed class InMemoryTraceRecorder : ITraceRecorder
{
    private TraceSession? _currentSession;
    private readonly List<StateTransition> _transitions = new();
    private readonly List<AICallRecord> _aiCalls = new();
    private readonly List<ExecutionRecord> _executions = new();
    private readonly List<ErrorRecord> _errors = new();
    private readonly List<PageTransition> _pageTransitions = new();

    // Tree index: ParentNodeId → child ExecutionRecords (O(1) tree queries)
    private readonly Dictionary<string, List<ExecutionRecord>> _byParent = new();

    // ITraceRecorder 7 Record methods — synchronous in-memory append + tree index update
    public Task RecordExecutionAsync(ExecutionRecord r, CancellationToken ct)
    {
        _executions.Add(r);
        // Update tree index if ParentNodeId present
        if (r.ParentNodeId != null)
        {
            if (!_byParent.ContainsKey(r.ParentNodeId))
                _byParent[r.ParentNodeId] = new List<ExecutionRecord>();
            _byParent[r.ParentNodeId].Add(r);
        }
        return Task.CompletedTask;
    }
    // ... other Record methods similarly append to their lists

    // Tree query methods (not on ITraceRecorder interface — InMemoryTraceRecorder-specific)
    public IReadOnlyList<ExecutionRecord> GetChildrenOf(string parentId)
        => _byParent.GetValueOrDefault(parentId) ?? Array.Empty<ExecutionRecord>();

    public IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType)
        => _executions.Where(e => e.SpanType == spanType).ToList();

    // Type pruning: build specialized sub-tree for a given SpanType
    // Returns ExecutionRecords with SpanType==X, preserving ParentNodeId for tree structure
    public IReadOnlyList<ExecutionRecord> PruneBySpanType(SpanType spanType)
        => _executions.Where(e => e.SpanType == spanType).ToList();

    // ITraceRecorder 5 Get methods — return copies
    // ... (as in previous version)

    public TraceSession? CurrentSession => _currentSession;
}
```

### TraversalEngine change

```csharp
// Before
var traceRecords = _config.TraceEnabled ? new List<TraceRecord>() : null;

// After
var inMemoryRecorder = _config.TraceEnabled ? new InMemoryTraceRecorder() : null;
_traceRecorder = inMemoryRecorder ?? _traceRecorder;
// TraceCoordinator created in Initialize() — uses the resolved _traceRecorder
```

---

## Section 2: TraceCoordinator Fill + Engine Call Points

### Method classification

| Category | Methods | Count |
|----------|---------|-------|
| Already has real logic | RecordStateTransition | 1 |
| Fill with real logic | RecordRootNodePushed, RecordPageAnalysis, RecordActionExecution, RecordSkipSpan (→ SpanType.DfsForward), RecordErrorSpan, RecordDecision, RecordPageTransition, RecordDynamicLifecycle, RecordStateDecision, RecordStepStart, RecordStepEnd, RecordAICallSpan | 12 |
| Keep as empty shell (Phase 3) | RecordMetricsAsSpans, RecordExecutionSpan(generic object) | 2 |

### RecordAICallSpan — changed from `object ai` to typed parameters

```csharp
// Before (untyped)
public void RecordAICallSpan(object ai)

// After (typed — enables actual AI call tracking)
public void RecordAICallSpan(string capability, string providerId, bool success, double latencyMs, int? tokens = null)
{
    LogAndContinue(() => _recorder?.RecordAICallAsync(
        new AICallRecord(capability, providerId, success, latencyMs, tokens, DateTimeOffset.UtcNow))
        .GetAwaiter().GetResult());
}
```

Engine tracks IVisionProvider latency:

```csharp
var aiSw = Stopwatch.StartNew();
var pageAnalysis = await _vision.AnalyzeCurrentPageAsync(ct);
aiSw.Stop();
_ctx.SetCurrentPageAnalysis(pageAnalysis);
_stepCtx.Trace.RecordAICallSpan("vision", "provider", true, aiSw.Elapsed.TotalMilliseconds);
```

### ExecutionRecord extension — 3 correlation fields for observability consumption

TraceRecorder 只负责记录数据，不负责可观测服务。但可观测服务消费数据时需要关联键。当前 ITraceRecorder records 语义丰富但关联薄弱 — 缺 StepNumber、TraceId、Depth。补充这 3 个 optional 字段让未来可观测服务能无缝消费，对 TraceRecorder 实现无负担（构造 record 时多传几个参数）。

```csharp
// After phase22-refactoring + trace-pipeline:
public sealed record class ExecutionRecord(
    string Action,
    string Status,
    SpanType? SpanType = null,           // phase22-refactoring
    string? ParentNodeId = null,          // trace-pipeline: tree reconstruction key
    int? StepNumber = null,               // trace-pipeline: correlation key — which engine step produced this record
    string? TraceId = null,               // trace-pipeline: correlation key — which traversal session
    int? Depth = null,                    // trace-pipeline: tree depth — how deep in the traversal tree
    object? Target = null,
    double DurationMs = 0,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);
```

All 5 new fields are optional (default null), backward compatible. TraceCoordinator fills them from engine context:
- `ParentNodeId` → `_ctx.CurrentFrame?.NodeId` (current node)
- `StepNumber` → `_ctx.StepCount` (engine step counter)
- `TraceId` → `_traceId` (session trace ID)
- `Depth` → `_ctx.NodeStack.Depth` (tree depth)

Similarly extend StateTransition and ErrorRecord with correlation fields:

```csharp
// StateTransition extension (2 new fields)
public sealed record class StateTransition(
    string FromState,
    string ToState,
    string? NodeId = null,
    DateTimeOffset Timestamp = default,
    string? Reason = null,
    int? StepNumber = null,               // ← correlation: which step
    string? TraceId = null,               // ← correlation: which session
    Dictionary<string, object>? Metadata = null);

// ErrorRecord extension (3 new fields)
public sealed record class ErrorRecord(
    string ErrorType,
    string ErrorMessage,
    ErrorSeverity Severity,
    DateTimeOffset Timestamp = default,
    string? ParentNodeId = null,           // ← which node the error occurred under
    int? StepNumber = null,               // ← which step
    string? TraceId = null,               // ← which session
    Dictionary<string, object>? Metadata = null);
```

ErrorRecord gains ParentNodeId (fills the "which node caused this error" gap identified in observability analysis) and StepNumber/TraceId correlation keys.

PageTransition also gains correlation fields:

```csharp
// PageTransition extension (2 new fields) — phase22-refactoring defined 7 fields, trace-pipeline adds 2
public sealed record class PageTransition(
    string FromPage,
    string ToPage,
    string TransitionType,
    string? NodeId = null,
    double? DurationMs = null,
    DateTimeOffset Timestamp = default,
    int? StepNumber = null,               // ← which step
    string? TraceId = null,               // ← which session
    Dictionary<string, object>? Metadata = null);
```

AICallRecord already has no correlation gap — it's uniquely identified by Capability+ProviderId+Timestamp and doesn't need ParentNodeId (AI calls are session-level, not node-level).

### Observability consumption capability assessment (post-correlation fields)

| Capability | Before | After | Key improvement |
|-----------|--------|-------|-----------------|
| Tree reconstruction | 8/10 | ✅ 10/10 | Depth field enables direct depth labeling without path computation |
| Type pruning | 10/10 | ✅ 10/10 | No change needed |
| Time-series analysis | 5/10 | ⚠️ 7/10 | StepNumber enables per-step time bucketing; DurationMs=0 still limits operation-level analysis |
| Event correlation | 4/10 | ✅ 9/10 | StepNumber + TraceId link ITraceRecorder records to engine steps/sessions; ErrorRecord.ParentNodeId fills "which node" gap |
| Export/interchange | 4/10 | ⚠️ 7/10 | Correlation keys enable structured export; ExportTraceAsync and exposure path still need design |
| Query flexibility | 5/10 | ⚠️ 8/10 | StepNumber/TraceId enable efficient composite queries (e.g., "all errors on step 5" = filter by StepNumber+SpanType) |

### StepTraceSnapshot — replaces GetLastXxx() pattern

GetLastXxx() has a **race condition**: multiple events per step overwrite each other's last values (e.g., RecordPageAnalysis sets SpanType=PageAnalysis, then RecordStepEnd overwrites it to null). StepTraceSnapshot collects ALL events within a step:

```csharp
/// <summary>
/// Step trace snapshot — all trace events for one engine step.
/// Engine reads at step end to enrich TraceRecord construction.
/// </summary>
public sealed record class StepTraceSnapshot(
    ImmutableArray<SpanType> SpanTypes,
    string? PageFrom,
    string? PageTo,
    string? PageTransitionType,
    double? StepDurationMs);
```

TraceCoordinator maintains per-step event collection:

```csharp
// TraceCoordinator internal state
private readonly List<SpanType> _stepSpanTypes = new();
private string? _stepPageFrom;
private string? _stepPageTo;
private string? _stepPageTransitionType;
private readonly Stopwatch _stepStopwatch = new();

// Each Record method appends to _stepSpanTypes (not overwrites)
public void RecordPageAnalysis(PageAnalysis? pageAnalysis)
{
    _stepSpanTypes.Add(SpanType.PageAnalysis);  // ← append, not overwrite
    LogAndContinue(() => ...);
}

public void RecordStepStart(string nodeId, string result)
{
    _stepStopwatch.Restart();
    // framework event — no SpanType appended
    LogAndContinue(() => ...);
}

// Engine reads at step end:
public StepTraceSnapshot GetStepSnapshot()
{
    var snapshot = new StepTraceSnapshot(
        SpanTypes: _stepSpanTypes.ToImmutableArray(),
        PageFrom: _stepPageFrom,
        PageTo: _stepPageTo,
        PageTransitionType: _stepPageTransitionType,
        StepDurationMs: _stepStopwatch.Elapsed.TotalMilliseconds);
    // Reset for next step
    _stepSpanTypes.Clear();
    _stepPageFrom = null;
    _stepPageTo = null;
    _stepPageTransitionType = null;
    return snapshot;
}
```

### Engine TraceRecord construction (updated with StepTraceSnapshot)

```csharp
// TraversalEngine.RunAsync — per-step loop
if (_config.TraceEnabled && traceRecords != null)
{
    var snapshot = _stepCtx.Trace.GetStepSnapshot();  // ← collects all step events, resets for next step
    traceRecords.Add(new TraceRecord(
        StepNumber: i + 1,
        FromState: fromState,
        ToState: stepResult.NextState,
        CurrentNodeId: _ctx.CurrentFrame?.NodeId,
        CurrentPageId: GetCurrentPageId(),
        ActionExecuted: GetLastAction(),
        ActionSuccess: GetLastActionSuccess(),
        ChildPushed: stepResult.ChildPushed,
        FrameCompleted: stepResult.FrameCompleted,
        SpanTypes: snapshot.SpanTypes,                        // ← all SpanTypes this step
        PageFrom: snapshot.PageFrom,                          // ← from snapshot
        PageTo: snapshot.PageTo,                              // ← from snapshot
        PageTransitionType: snapshot.PageTransitionType,      // ← from snapshot
        StepDurationMs: snapshot.StepDurationMs));            // ← per-step stopwatch
}
```

Note: TraceRecord.SpanType field changed from `SpanType?` (single) to `ImmutableArray<SpanType>` (multi-value) to reflect that one step can produce multiple semantic events.

### TraceRecord extension (final — 5 new fields)

```csharp
// Before (9 fields)
public sealed record class TraceRecord(
    int StepNumber, TraversalState FromState, TraversalState ToState,
    string? CurrentNodeId, string? CurrentPageId, string? ActionExecuted,
    bool ActionSuccess, bool ChildPushed, bool FrameCompleted);

// After (14 fields — 5 new, backward compatible)
public sealed record class TraceRecord(
    int StepNumber, TraversalState FromState, TraversalState ToState,
    string? CurrentNodeId, string? CurrentPageId, string? ActionExecuted,
    bool ActionSuccess, bool ChildPushed, bool FrameCompleted,
    ImmutableArray<SpanType> SpanTypes = default,       // ← all semantic events this step (empty = none)
    string? PageFrom = null,                            // ← page navigation source
    string? PageTo = null,                              // ← page navigation target
    string? PageTransitionType = null,                  // ← nav type
    double? StepDurationMs = null);                     // ← per-step duration
```

### Filled methods — record construction mapping (with correlation fields)

TraceCoordinator fills correlation fields from engine context in every Record call:
- `ParentNodeId` → `_ctx.CurrentFrame?.NodeId`
- `StepNumber` → `_ctx.StepCount`
- `TraceId` → `_traceId`
- `Depth` → `_ctx.NodeStack.Depth`

| Method | → ITraceRecorder method | → Record type | SpanType | ParentNodeId | StepNumber | TraceId | Depth |
|--------|------------------------|--------------|----------|-------------|------------|---------|-------|
| RecordStateTransition | RecordTransitionAsync | StateTransition | — | NodeId param | ✅ | ✅ | — |
| RecordRootNodePushed | RecordTransitionAsync | StateTransition | — | nodeId param | ✅ | ✅ | 0 (root) |
| RecordPageAnalysis | RecordExecutionAsync | ExecutionRecord | PageAnalysis | ✅ | ✅ | ✅ | ✅ |
| RecordActionExecution | RecordExecutionAsync | ExecutionRecord | null | ✅ | ✅ | ✅ | ✅ |
| RecordSkipSpan | RecordExecutionAsync | ExecutionRecord | DfsForward | ✅ | ✅ | ✅ | ✅ |
| RecordErrorSpan | RecordErrorAsync | ErrorRecord | — | ✅ | ✅ | ✅ | — |
| RecordDecision | RecordExecutionAsync | ExecutionRecord | StateDecision | ctx.CurrentFrame?.NodeId | ✅ | ✅ | ✅ |
| RecordPageTransition | RecordPageTransitionAsync | PageTransition | — | ✅ | ✅ | ✅ | — |
| RecordDynamicLifecycle | RecordExecutionAsync | ExecutionRecord | DfsForward | parentId param | ✅ | ✅ | ✅ |
| RecordStateDecision | RecordExecutionAsync | ExecutionRecord | StateDecision | nodeId param | ✅ | ✅ | ✅ |
| RecordStepStart | RecordExecutionAsync | ExecutionRecord | null | ✅ | ✅ | ✅ | ✅ |
| RecordStepEnd | RecordExecutionAsync | ExecutionRecord | null | ✅ | ✅ | ✅ | ✅ |
| RecordAICallSpan | RecordAICallAsync | AICallRecord | — | — | — | — | — |

### Engine call points

**Existing (StepOrchestrator) — unchanged:**
- Step 2: `ctx.Trace.RecordStepStart(currentNodeId, "")`
- Step 7: `ctx.Trace.RecordStateTransition(fromState, nextState)`
- Step 14: `ctx.Trace.RecordStepEnd(currentNodeId, nextState)`
- Step 4 (path changed): `ctx.Trace.RecordPageAnalysis(pageAnalysis)`
- Step 5 (action): `ctx.Trace.RecordActionExecution(action, target, success)`

**New (TraversalEngine.RunAsync):**
- Page visit: `ctx.Trace.RecordPageTransition(fromPage, toPage, "forward"/"back")`
- CompletionPolicy check: `ctx.Trace.RecordDecision("completion_policy_check", _ctx)`
- AI call: `_stepCtx.Trace.RecordAICallSpan("vision", "provider", true, aiSw.Elapsed.TotalMilliseconds)` after `AnalyzeCurrentPageAsync()`

**Existing but now fills (DynamicChildManager):**
- `ctx.Trace.RecordDynamicLifecycle("generate", nodeId, parentId, ruleId, elementId)` — already called

### Metadata conversion

```csharp
var metaObj = metadata?.ToDictionary(k => k.Key, v => (object)v.Value);
```

---

## Section 3: TraversalResult Integration + ExpectedBehavior Consumption

### TraversalResult.Trace — unchanged source

```csharp
Trace: trace?.ToImmutableArray() ?? ImmutableArray<TraceRecord>.Empty
```

Engine still maintains `List<TraceRecord> traceRecords`. InMemoryTraceRecorder provides canonical ITraceRecorder data separately.

### ExpectedBehavior.Verify consumption paths

| Verification target | Read from |
|--------------------|-----------|
| 5 existing dimensions | TraversalResult.Trace (TraceRecord, unchanged) |
| operation_rules (restore_ops) | TraversalResult.Trace.SpanTypes — filter SpanType.RestoreOp |
| operation_rules (skip_dangerous) | TraversalResult.Trace.SpanTypes — filter SpanType.SkipDangerous |
| trace_integrity (span_types) | TraversalResult.Trace.SpanTypes distribution |
| trace_integrity (page_transitions) | TraversalResult.Trace.PageFrom/PageTo sequences |
| Tree reconstruction (advanced) | InMemoryTraceRecorder.GetChildrenOf(parentId) — O(1) tree queries |
| Type pruning (advanced) | InMemoryTraceRecorder.PruneBySpanType(spanType) |

---

## Correctness & Extensibility Audit Resolutions

| # | Issue | Severity | Resolution |
|---|-------|----------|-----------|
| 1 | Last-value race condition — multiple events per step overwrite each other's values | 🔴 | Replaced GetLastXxx() with StepTraceSnapshot — collects all events per step, engine reads at step end |
| 2 | Dual data paths (engine TraceRecord + ITraceRecorder) claimed as "single data source" | ⚠️ | Corrected description — two paths serve different purposes; TraceRecord is backward-compatible per-step view, ITraceRecorder is canonical event-level data |
| 3 | TraceNode hierarchy is dead code (never populated) | ⚠️ | Deleted — TraceNode.cs, UlidGenerator.cs, 8 tests removed; ITraceRecorder records become single canonical model |
| 4 | ExecutionRecord field order across two changes | ⚠️ | Documented dependency: phase22-refactoring adds SpanType first, trace-pipeline adds ParentNodeId after; sequential implementation required |
| 5 | GetAwaiter().GetResult() sync-over-async | ⚠️ | Current scope: CLI/test only (no SynchronizationContext), safe. Production extension: Phase 3 async TraceCoordinator |
| 6 | GetLastXxx() O(n) method growth pattern | 🔴 | Replaced by StepTraceSnapshot — new dimensions add to snapshot record, not new methods |
| 7 | InMemoryTraceRecorder flat list doesn't support efficient tree queries | ⚠️ | Added _byParent Dictionary<string, List<ExecutionRecord>> tree index — O(1) parent→children lookup |
| 8 | TraceNode vs ITraceRecorder records relationship undefined | ⚠️ | Resolved by deleting TraceNode — ITraceRecorder records are now the single model |
| 9 | RecordAICallSpan untyped `object ai` parameter | ⚠️ | Changed to typed: (string capability, string providerId, bool success, double latencyMs, int? tokens) |
| 10 | TraceRecord SpanType single value can't represent multi-event steps | 🔴 | Changed from SpanType? to ImmutableArray<SpanType> — captures all semantic events within one step |

## [Trace] Attribute — Future Extension (Phase 2.3)

Declarative trace annotation on handler methods and engine behaviors. Methods annotated with `[Trace(SpanType.X)]` automatically produce ExecutionRecord entries when called. Implementation: source generator or runtime reflection. Details in memory/trace-vision.md. NOT in current scope — recorded as future design direction.
