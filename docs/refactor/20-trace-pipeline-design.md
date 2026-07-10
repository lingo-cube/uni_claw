# Trace Pipeline Design — TraceCoordinator Fill + InMemoryTraceRecorder + TraceRecord Extension

> Date: 2026-07-10
> Status: Approved
> Depends on: phase22-refactoring (SpanType enum + PageTransition record + ExecutionRecord.SpanType + ITraceRecorder 2 new methods)

## Overview

Fill TraceCoordinator's 14 empty-shell methods with real logic that routes engine events to ITraceRecorder. Create InMemoryTraceRecorder as an independent ITraceRecorder implementation for in-memory trace collection. Extend TraceRecord with 4 new optional fields (SpanType, PageFrom, PageTo, PageTransitionType) for operation_rules and trace_integrity verification. Engine still manually constructs TraceRecord (ChildPushed/FrameCompleted are engine-internal state not captured by ITraceRecorder), but enriches it with TraceCoordinator's last-value queries.

---

## Section 1: InMemoryTraceRecorder + Data Flow Architecture

### Before (two disconnected trace systems)

```
Engine events → TraceCoordinator (14 empty shells) → ITraceRecorder (never called)
Engine events → List<TraceRecord> (engine in-memory) → TraversalResult.Trace
```

### After (single data source via ITraceRecorder interface)

```
Engine events → TraceCoordinator → InMemoryTraceRecorder (ITraceRecorder impl)
                                     │
                                     ├─ Raw record storage (StateTransition, ExecutionRecord, PageTransition, etc.)
                                     │
                                     └─ ExpectedBehavior.Verify reads via ITraceRecorder interface

Engine events → List<TraceRecord> (engine-constructed, enriched from TraceCoordinator) → TraversalResult.Trace
```

Key: TraceRecord is NOT a derived view from InMemoryTraceRecorder. It's still manually constructed by the engine because it contains engine-internal state (ChildPushed, FrameCompleted) that ITraceRecorder records don't capture. The InMemoryTraceRecorder provides the ITraceRecorder-native data path for ExpectedBehavior consumption.

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

    // ITraceRecorder 7 Record methods — synchronous in-memory append
    public Task<TraceSession> StartSessionAsync(string traceId, Dictionary<string, object>? metadata, CancellationToken ct)
    { _currentSession = new TraceSession(traceId, DateTimeOffset.UtcNow, null, metadata); return Task.FromResult(_currentSession); }

    public Task EndSessionAsync(CancellationToken ct)
    { if (_currentSession != null) _currentSession = _currentSession with { EndTime = DateTimeOffset.UtcNow }; return Task.CompletedTask; }

    public Task RecordTransitionAsync(StateTransition t, CancellationToken ct)
    { _transitions.Add(t); return Task.CompletedTask; }

    public Task RecordAICallAsync(AICallRecord r, CancellationToken ct)
    { _aiCalls.Add(r); return Task.CompletedTask; }

    public Task RecordExecutionAsync(ExecutionRecord r, CancellationToken ct)
    { _executions.Add(r); return Task.CompletedTask; }

    public Task RecordErrorAsync(ErrorRecord r, CancellationToken ct)
    { _errors.Add(r); return Task.CompletedTask; }

    public Task RecordPageTransitionAsync(PageTransition t, CancellationToken ct)
    { _pageTransitions.Add(t); return Task.CompletedTask; }

    // ITraceRecorder 5 Get methods — return copies
    public Task<List<StateTransition>> GetTransitionsAsync(CancellationToken ct)
    => Task.FromResult(_transitions.ToList());
    public Task<List<AICallRecord>> GetAICallsAsync(CancellationToken ct)
    => Task.FromResult(_aiCalls.ToList());
    public Task<List<ExecutionRecord>> GetExecutionsAsync(CancellationToken ct)
    => Task.FromResult(_executions.ToList());
    public Task<List<ErrorRecord>> GetErrorsAsync(CancellationToken ct)
    => Task.FromResult(_errors.ToList());
    public Task<List<PageTransition>> GetPageTransitionsAsync(CancellationToken ct)
    => Task.FromResult(_pageTransitions.ToList());

    // Export — JSON serialize all data
    public Task<string> ExportTraceAsync(string format, CancellationToken ct)
    => Task.FromResult(DomainJsonOptions.Serialize(...));

    public TraceSession? CurrentSession => _currentSession;
}
```

### TraversalEngine change

```csharp
// Before
var traceRecords = _config.TraceEnabled ? new List<TraceRecord>() : null;

// After
var inMemoryRecorder = _config.TraceEnabled ? new InMemoryTraceRecorder() : null;
_traceRecorder = inMemoryRecorder ?? _traceRecorder;  // Override with InMemoryTraceRecorder when TraceEnabled
// TraceCoordinator created in Initialize() — uses the resolved _traceRecorder
```

TraversalResult.Trace still comes from `traceRecords` (engine-maintained List<TraceRecord>), NOT from InMemoryTraceRecorder.

---

## Section 2: TraceCoordinator Fill + Engine Call Points

### Method classification

| Category | Methods | Count |
|----------|---------|-------|
| Already has real logic | RecordStateTransition | 1 |
| Fill with real logic | RecordRootNodePushed, RecordPageAnalysis, RecordActionExecution, RecordSkipSpan (→ SpanType.DfsForward), RecordErrorSpan, RecordDecision, RecordPageTransition, RecordDynamicLifecycle, RecordStateDecision, RecordStepStart, RecordStepEnd, RecordAICallSpan | 12 |
| Keep as empty shell (Phase 3) | RecordMetricsAsSpans, RecordExecutionSpan(generic object) | 2 |

### Filled methods — record construction mapping

| Method | → ITraceRecorder method | → Record type | SpanType (if applicable) |
|--------|------------------------|--------------|------------------------|
| RecordStateTransition | RecordTransitionAsync | StateTransition | — |
| RecordRootNodePushed | RecordTransitionAsync | StateTransition("init", "root_pushed", nodeId) | — |
| RecordPageAnalysis | RecordExecutionAsync | ExecutionRecord("page_analysis", "success/null", SpanType.PageAnalysis) | PageAnalysis |
| RecordActionExecution | RecordExecutionAsync | ExecutionRecord(action, "success/failure", null) | null (action-level, not semantic) |
| RecordSkipSpan | RecordExecutionAsync | ExecutionRecord("skip", "skipped", SpanType.DfsForward) | DfsForward (NOT SkipDangerous) |
| RecordErrorSpan | RecordErrorAsync | ErrorRecord(errorType, message, severity) | — |
| RecordDecision | RecordExecutionAsync | ExecutionRecord("decision", decision, SpanType.StateDecision) | StateDecision |
| RecordPageTransition | RecordPageTransitionAsync | PageTransition(from, to, type) | — |
| RecordDynamicLifecycle | RecordExecutionAsync | ExecutionRecord(event, "dynamic", SpanType.DfsForward, metadata) | DfsForward |
| RecordStateDecision | RecordExecutionAsync | ExecutionRecord("state_decision", decision, SpanType.StateDecision, metadata) | StateDecision |
| RecordStepStart | RecordExecutionAsync | ExecutionRecord("step_start", result, null) | null (framework) |
| RecordStepEnd | RecordExecutionAsync | ExecutionRecord("step_end", result, null) | null (framework) |
| RecordAICallSpan | RecordAICallAsync | AICallRecord(capability, providerId, success, latencyMs) | — |

### SpanType mapping rationale

Only semantic-level events map to SpanType. Framework-level events (step_start/end, action_execution) don't — they're timeline scaffolding, not semantic classification.

RecordSkipSpan maps to `SpanType.DfsForward` (NOT SkipDangerous). Reason: MatchResult represents DynamicMatcher matching events — a skip means the matcher didn't produce a child, which is a DFS traversal event (no forward progress). SkipDangerous is reserved for future ErrorHandler/StepOrchestrator decisions to skip high-risk operations.

### TraceCoordinator last-value helper

TraceCoordinator tracks recent values for engine TraceRecord enrichment:

```csharp
// Private state for last-value queries
private SpanType? _lastSpanType;
private string? _lastPageFrom;
private string? _lastPageTo;
private string? _lastPageTransitionType;

// Methods update these values when called
// e.g., RecordPageAnalysis sets _lastSpanType = SpanType.PageAnalysis
// RecordPageTransition sets _lastPageFrom/To/Type

// Public query methods (for engine TraceRecord construction)
public SpanType? GetLastSpanType() => _lastSpanType;
public string? GetLastPageFrom() => _lastPageFrom;
public string? GetLastPageTo() => _lastPageTo;
public string? GetLastPageTransitionType() => _lastPageTransitionType;
```

### Engine call points

**Existing (StepOrchestrator) — unchanged:**
- Step 2: `ctx.Trace.RecordStepStart(currentNodeId, "")`
- Step 7: `ctx.Trace.RecordStateTransition(fromState, nextState)`
- Step 14: `ctx.Trace.RecordStepEnd(currentNodeId, nextState)`
- Step 4 (path changed): `ctx.Trace.RecordPageAnalysis(pageAnalysis)` — already called but was empty shell
- Step 5 (action): `ctx.Trace.RecordActionExecution(action, target, success)` — already called but was empty shell

**New (TraversalEngine.RunAsync):**
- Page visit: `ctx.Trace.RecordPageTransition(fromPage, toPage, "forward"/"back")`
- CompletionPolicy check: `ctx.Trace.RecordDecision("completion_policy_check", _ctx)`

**Existing but now fills (DynamicChildManager):**
- `ctx.Trace.RecordDynamicLifecycle("generate", nodeId, parentId, ruleId, elementId)` — already called but was empty shell

### Metadata conversion

RecordStateDecision receives `Dictionary<string, string>? metadata` but ExecutionRecord.Metadata is `Dictionary<string, object>?`. Conversion:

```csharp
var metaObj = metadata?.ToDictionary(k => k.Key, v => (object)v.Value);
```

---

## Section 3: TraceRecord Extension + TraversalResult Integration

### TraceRecord extension (4 new optional fields)

```csharp
// Before (9 fields)
public sealed record class TraceRecord(
    int StepNumber, TraversalState FromState, TraversalState ToState,
    string? CurrentNodeId, string? CurrentPageId, string? ActionExecuted,
    bool ActionSuccess, bool ChildPushed, bool FrameCompleted);

// After (13 fields — 4 new optional, backward compatible)
public sealed record class TraceRecord(
    int StepNumber, TraversalState FromState, TraversalState ToState,
    string? CurrentNodeId, string? CurrentPageId, string? ActionExecuted,
    bool ActionSuccess, bool ChildPushed, bool FrameCompleted,
    SpanType? SpanType = null,           // ← semantic classification
    string? PageFrom = null,             // ← page navigation source
    string? PageTo = null,               // ← page navigation target
    string? PageTransitionType = null);  // ← nav type ("forward"/"back"/"sub_page")
```

### Why TraceRecord is NOT derived from InMemoryTraceRecorder

TraceRecord contains engine-internal state that InMemoryTraceRecorder cannot capture:

| Field | Source | InMemoryTraceRecorder has? |
|-------|--------|---------------------------|
| ChildPushed | stepResult.ChildPushed | ❌ No |
| FrameCompleted | stepResult.FrameCompleted | ❌ No |
| StepNumber | engine loop variable i+1 | ❌ No |

These are StepResult fields that only the engine loop knows. Merging StateTransition + ExecutionRecord + PageTransition into TraceRecord would require timestamp-based alignment which is fragile (3-5 ITraceRecorder records per step with slightly different timestamps). Keeping manual construction is simpler, more correct, and preserves backward compatibility.

### Engine TraceRecord construction (updated)

```csharp
// TraversalEngine.RunAsync — per-step loop (kept, enriched)
if (_config.TraceEnabled && traceRecords != null)
{
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
        SpanType: _stepCtx.Trace.GetLastSpanType(),                // ← from TraceCoordinator
        PageFrom: _stepCtx.Trace.GetLastPageFrom(),                // ← from TraceCoordinator
        PageTo: _stepCtx.Trace.GetLastPageTo(),                    // ← from TraceCoordinator
        PageTransitionType: _stepCtx.Trace.GetLastPageTransitionType())); // ← from TraceCoordinator
}
```

### TraversalResult.Trace — unchanged source

TraversalResult.Trace still comes from engine-maintained `List<TraceRecord>`:
```csharp
Trace: trace?.ToImmutableArray() ?? ImmutableArray<TraceRecord>.Empty
```

### ExpectedBehavior.Verify consumption paths

| Verification target | Read from |
|--------------------|-----------|
| 5 existing dimensions (completion, page_coverage, etc.) | TraversalResult.Trace (TraceRecord, unchanged) |
| operation_rules (restore_ops, skip_dangerous) | TraversalResult.Trace.SpanType field — filter SpanType.RestoreOp / SpanType.SkipDangerous |
| trace_integrity (span_types) | TraversalResult.Trace.SpanType distribution |
| trace_integrity (page_transitions) | TraversalResult.Trace.PageFrom/PageTo/PageTransitionType sequences |
| Detailed trace data (ExecutionRecord.Metadata, etc.) | Optional: pass InMemoryTraceRecorder to Verify, read via ITraceRecorder interface |

Most verification only needs TraversalResult.Trace (SpanType/PageTransition data embedded). InMemoryTraceRecorder native access is optional for advanced cases.

---

## Correctness Issues Resolved During Design

| # | Issue | Resolution |
|---|-------|-----------|
| 1 | TraceRecord cannot be fully derived from InMemoryTraceRecorder (ChildPushed/FrameCompleted missing) | Engine still manually constructs TraceRecord, enriched from TraceCoordinator last-value queries |
| 2 | RecordSkipSpan mapped to SkipDangerous (wrong semantics) | Corrected to SpanType.DfsForward — MatchResult skip is DFS traversal event, not danger-skip |
| 3 | `metadata?.ToObjectDictionary()` is fictional method | Corrected to `metadata?.ToDictionary(k => k.Key, v => (object)v.Value)` |
| 4 | RecordStateTransition already has real logic (was listed as empty shell) | Corrected — only 14 methods need filling (not 15) |
| 5 | RecordAICallSpan should be filled (not kept as empty shell) | Added to fill list — simulation/production both need AI call tracing |
| 6 | Merge-based ExportTraceRecords approach fragile (timestamp alignment) | Abandoned — engine manual construction is simpler and more correct |
