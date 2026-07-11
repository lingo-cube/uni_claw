# Trace Pipeline Design — Three-Layer Architecture + Node-Span Model

> Date: 2026-07-11 (revision of 2026-07-10 draft)
> Status: Approved
> Depends on: phase22-refactoring (SpanType enum + PageTransition record + ExecutionRecord.SpanType)
> Vision: see memory/trace-vision.md
> Previous version: docs/refactor/20-trace-pipeline-design.md (2026-07-10 draft)

## Overview

Refactor the trace pipeline into a **three-layer architecture**: ITraceStorage (shared data backend) → ITraceRecorder (write contract) → ITraceService (read+query facade). Adopt a **Node-Span conceptual model** where Node captures page hierarchy (DFS tree) and Span captures events per node visit. Extend all 5 ITraceRecorder record types with correlation keys (NodeId, StepSpanId, StepNumber, TraceId) and typed target fields (TargetType + TargetValue replacing object? Target). Add SpanId (unique per ExecutionRecord) and StepSpanId (per-engine-step grouping across all 5 record types). Delete TraceNode hierarchy (dead code). Simplify ITraceRecorder from 13 to 7 methods (pure write contract). Create ITraceService with 13 read+query methods.

This iteration covers: data model changes, three-layer architecture, TraceCoordinator fill, 6 basic query methods. Phase 3 will add: FsmAnalysis, ExecutionPlanDigest, PerformanceProfile, ReplayExecutor, GlobalFSM callback writing, VisitSpanId (per-node-visit), ParentSpanId (span causality tree).

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

SpanId generation moves to TraceCoordinator (`_spanCounter` incremental format). ITraceStorage + InMemoryTraceStorage replace TraceNode's storage role. ITraceService query methods replace TraceNode's tree query role.

---

## Section 1: Data Model — TraceContext + Record Field Revisions

### TraceContext — shared observability correlation envelope

All 5 ITraceRecorder record types share 4 common correlation fields (NodeId, StepSpanId, StepNumber, TraceId). These answer "when/where/how was this event recorded" — they are **observability correlation**, not core domain attributes. Encapsulating them into TraceContext separates concerns: core domain fields describe what the record IS, TraceContext describes how it relates to the engine context.

```csharp
/// <summary>
/// Trace correlation context — shared observability envelope for all 5 record types.
/// Encapsulates "when/where/how" correlation. Not core domain data.
/// Phase 3 will add VisitSpanId (per-node-visit) and ParentSpanId (span causality).
/// </summary>
public sealed record class TraceContext(
    string? NodeId = null,           // ← which node this event occurred at
    string? StepSpanId = null,       // ← per-engine-step grouping (assigned at StepStart)
    int? StepNumber = null,          // ← engine step counter (temporal position)
    string? TraceId = null);         // ← traversal session identifier
```

**Field boundary rule**: TraceContext contains ONLY fields shared by ALL 5 record types. Type-specific fields (FsmType, SpanId, ChildNodeId, ParentNodeId, PageId, TargetType/TargetValue, Depth, DurationMs, Tokens) stay on their respective record types.

**Phase 3 extension**: VisitSpanId and ParentSpanId are general correlation fields → add to TraceContext. No record type changes needed.

**Guard test**: `TraceContext_Has4Fields` verifies TraceContext has exactly 4 fields, preventing accidental addition of type-specific fields.

### ExecutionRecord (TraceContext + type-specific fields)

```csharp
public sealed record class ExecutionRecord(
    string Action,
    string Status,
    SpanType? SpanType = null,
    TraceContext? Context = null,       // ← 4 common correlation fields (NodeId, StepSpanId, StepNumber, TraceId)
    string? SpanId = null,             // ← ExecutionRecord-specific: unique identifier per record
    string? ChildNodeId = null,        // ← ExecutionRecord-specific: DfsForward pushed child ID
    string? ParentNodeId = null,       // ← ExecutionRecord-specific: DFS tree parent node ID
    string? PageId = null,             // ← ExecutionRecord-specific: page this node corresponds to
    TargetType? TargetType = null,     // ← ExecutionRecord-specific: replaces object? Target
    string? TargetValue = null,        // ← ExecutionRecord-specific: serialized target value
    int? Depth = null,                 // ← ExecutionRecord-specific: tree depth
    double DurationMs = 0,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);
```

Key changes:
- `TraceContext? Context` encapsulates 4 common correlation fields — replaces NodeId, StepSpanId, StepNumber, TraceId as separate parameters
- `SpanId, ChildNodeId, ParentNodeId, PageId, TargetType, TargetValue, Depth` stay on ExecutionRecord (type-specific, not shared by other 4 record types)
- `TargetType + TargetValue` replaces `object? Target`: structured, queryable, cacheable. Back/NoAction have TargetType=null.
- `ParentNodeId` semantics clarified: DFS tree parent for tree reconstruction (NOT "current node").

### StateTransition (TraceContext + FsmType)

```csharp
public sealed record class StateTransition(
    string FromState,
    string ToState,
    TraceContext? Context = null,      // ← 4 common correlation fields
    string? FsmType = null,            // ← StateTransition-specific: "TraversalFSM" (Phase 3 adds "GlobalFSM")
    string? Reason = null,
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);
```

FsmType stays on StateTransition (not in TraceContext) because only FSM transitions have an FSM type. ErrorRecord/AICallRecord don't need "which FSM produced this".

### ErrorRecord (TraceContext only — all correlation is common)

```csharp
public sealed record class ErrorRecord(
    string ErrorType,
    string ErrorMessage,
    ErrorSeverity Severity,
    TraceContext? Context = null,      // ← replaces NodeId+StepSpanId+StepNumber+TraceId (old ParentNodeId renamed to NodeId, now in Context)
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);
```

### PageTransition (TraceContext + DurationMs)

```csharp
public sealed record class PageTransition(
    string FromPage,
    string ToPage,
    string TransitionType,
    TraceContext? Context = null,      // ← 4 common correlation fields
    double? DurationMs = null,         // ← PageTransition-specific: navigation duration
    DateTimeOffset Timestamp = default,
    Dictionary<string, object>? Metadata = null);
```

### AICallRecord (TraceContext + Tokens)

```csharp
public sealed record class AICallRecord(
    string Capability,
    string ProviderId,
    bool Success,
    double LatencyMs,
    TraceContext? Context = null,      // ← 4 common correlation fields (replaces NodeId+StepSpanId+StepNumber+TraceId)
    int? Tokens = null,                // ← AICallRecord-specific: token consumption
    DateTimeOffset Timestamp = default);
```

### TraceRecord (unchanged from previous draft)

```csharp
public sealed record class TraceRecord(
    int StepNumber, TraversalState FromState, TraversalState ToState,
    string? CurrentNodeId, string? CurrentPageId, string? ActionExecuted,
    bool ActionSuccess, bool ChildPushed, bool FrameCompleted,
    ImmutableArray<SpanType> SpanTypes = default,       // ← all semantic events this step
    string? PageFrom = null,                            // ← page navigation source
    string? PageTo = null,                              // ← page navigation target
    string? PageTransitionType = null,                  // ← nav type
    double? StepDurationMs = null);                     // ← per-step duration
```

TraceRecord is NOT part of the ITraceRecorder data path — it's the engine's per-step in-memory record for TraversalResult.Trace. TraceContext does NOT apply to TraceRecord.

### Four-level identification system (via TraceContext)

| Level | Access path | Semantics | Scope |
|-------|-----------|-----------|-------|
| Structure | `record.Context?.NodeId` | DFS tree node | Same node across visits |
| Visit-group | `record.Context?.StepSpanId` | Per-engine-step grouping | One engine step iteration |
| Event | `execution.SpanId` (ExecutionRecord only) | Unique per ExecutionRecord | Single record |
| Time | `record.Context?.StepNumber` | Engine step counter | Sequential position |

Phase 3 will add `VisitSpanId` and `ParentSpanId` to TraceContext (general correlation fields — all types need them).

### TraceContext encapsulation rationale

| Dimension | Before (explicit fields) | After (TraceContext) |
|-----------|------------------------|---------------------|
| StateTransition parameter count | 9 | **6** |
| ErrorRecord parameter count | 8 | **5** |
| Correlation field duplication | 4×5=20 parameters | 1×5=5 parameters |
| Core domain clarity | ⚠️ domain + trace mixed | ✅ domain fields independent, trace correlation in Context |
| Phase 3 extension impact | Add 2 fields × 5 types = 10 parameter changes | Add 2 fields to TraceContext = 1 type change |
| TraceCoordinator construction | Fill 4 separate fields per record | `BuildCorrelation()` one-line |
| JSON serialization | Flat: `{"fromState":"X","nodeId":"Y",...}` | Nested: `{"fromState":"X","context":{"nodeId":"Y"},...}` — observability service clearly distinguishes domain data vs correlation |

### Dependency: ExecutionRecord → Domain.Common.TargetType

ExecutionRecord (Observability namespace) references TargetType (Domain.Models.Common namespace). This is a downward reference: Observability → Domain, allowed per D-17 (Observability is cross-cutting utility). No Guard test needed.

---

## Section 2: Three-Layer Architecture

### Architecture overview

```
┌─────────────────────────────────────────────────────────────┐
│                    ITraceStorage (shared storage interface)    │
│                                                             │
│  Write:  AddExecution, AddTransition, AddError,             │
│          AddPageTransition, AddAICall, SetSession, EndSession│
│  Read:   GetExecutions, GetTransitions, GetErrors,          │
│          GetPageTransitions, GetAICalls, CurrentSession      │
│  Export: Export(format)                                      │
│                                                             │
│  InMemoryTraceStorage : ITraceStorage                       │
│    + 5 flat lists + indexes (_byNodeId, _bySpanType)        │
│    + Index methods (concrete, NOT on ITraceStorage interface)│
│      GetByNodeId(nodeId), GetBySpanType(spanType)           │
└──────────┬──────────────────────┬───────────────────────────┘
           │                      │
           │ ITraceStorage        │ InMemoryTraceStorage (concrete)
           │ (interface)          │ (for index access)
           ▼                      ▼
┌──────────────────────┐  ┌──────────────────────────────────┐
│ InMemoryTraceRecorder│  │ InMemoryTraceService              │
│ : ITraceRecorder     │  │ : ITraceService                   │
│                      │  │                                    │
│ 7 write methods      │  │ 1 property + 12 read/query methods│
│ → _storage.AddXxx    │  │ → _storage.Get + index queries    │
│   + Task.Completed   │  │                                    │
└──────────────────────┘  └──────────────────────────────────┘

Injection paths:
  TraceCoordinator  →── ITraceRecorder  (write only)
  Analysis/Dashboard →── ITraceService   (read + query only)
  No component needs both interfaces
```

### ITraceStorage — shared storage interface (13 methods)

```csharp
public interface ITraceStorage
{
    // Session (2)
    void SetSession(TraceSession session);
    void EndSession();
    TraceSession? CurrentSession { get; }

    // Write — synchronous append (5)
    void AddExecution(ExecutionRecord r);
    void AddTransition(StateTransition t);
    void AddError(ErrorRecord r);
    void AddPageTransition(PageTransition t);
    void AddAICall(AICallRecord r);

    // Read — flat list access (5)
    IReadOnlyList<ExecutionRecord> GetExecutions();
    IReadOnlyList<StateTransition> GetTransitions();
    IReadOnlyList<ErrorRecord> GetErrors();
    IReadOnlyList<PageTransition> GetPageTransitions();
    IReadOnlyList<AICallRecord> GetAICalls();

    // Export (1)
    string Export(string format = "json");
}
```

Write methods are synchronous (in-memory). ITraceRecorder wraps these with Task.CompletedTask for async contract.

### InMemoryTraceStorage — implementation + indexes

```csharp
public sealed class InMemoryTraceStorage : ITraceStorage
{
    private TraceSession? _currentSession;
    private readonly List<ExecutionRecord> _executions = new();
    private readonly List<StateTransition> _transitions = new();
    private readonly List<ErrorRecord> _errors = new();
    private readonly List<PageTransition> _pageTransitions = new();
    private readonly List<AICallRecord> _aiCalls = new();

    // Indexes — built incrementally during Add (O(1) per write)
    private readonly Dictionary<string, List<ExecutionRecord>> _byNodeId = new();
    private readonly Dictionary<SpanType, List<ExecutionRecord>> _bySpanType = new();

    // ITraceStorage: Write (synchronous append + index update)
    public void AddExecution(ExecutionRecord r)
    {
        _executions.Add(r);
        if (r.Context?.NodeId != null)
        {
            if (!_byNodeId.ContainsKey(r.Context.NodeId)) _byNodeId[r.Context.NodeId] = new();
            _byNodeId[r.Context.NodeId].Add(r);
        }
        if (r.SpanType != null)
        {
            if (!_bySpanType.ContainsKey(r.SpanType.Value)) _bySpanType[r.SpanType.Value] = new();
            _bySpanType[r.SpanType.Value].Add(r);
        }
    }
    public void AddTransition(StateTransition t) => _transitions.Add(t);
    public void AddError(ErrorRecord r) => _errors.Add(r);
    public void AddPageTransition(PageTransition t) => _pageTransitions.Add(t);
    public void AddAICall(AICallRecord r) => _aiCalls.Add(r);
    public void SetSession(TraceSession session) => _currentSession = session;
    public void EndSession()
    { if (_currentSession != null) _currentSession = _currentSession with { EndTime = DateTimeOffset.UtcNow }; }

    // ITraceStorage: Read
    public IReadOnlyList<ExecutionRecord> GetExecutions() => _executions;
    public IReadOnlyList<StateTransition> GetTransitions() => _transitions;
    public IReadOnlyList<ErrorRecord> GetErrors() => _errors;
    public IReadOnlyList<PageTransition> GetPageTransitions() => _pageTransitions;
    public IReadOnlyList<AICallRecord> GetAICalls() => _aiCalls;
    public TraceSession? CurrentSession => _currentSession;

    // Index methods — InMemoryTraceStorage-specific, NOT on ITraceStorage interface
    // Different storage backends may not support index queries (ISP principle)
    public IReadOnlyList<ExecutionRecord> GetByNodeId(string nodeId)
        => _byNodeId.GetValueOrDefault(nodeId) ?? Array.Empty<ExecutionRecord>();
    public IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType)
        => _bySpanType.GetValueOrDefault(spanType) ?? Array.Empty<ExecutionRecord>();
}
```

Index design rationale:
- `_byNodeId`: groups ExecutionRecords by NodeId → used by GetNodeSpans, GetNodeVisitTimeline. Essential, highest frequency.
- `_bySpanType`: groups ExecutionRecords by SpanType → used by GetBySpanType, ReconstructTree (DfsForward edges). Essential, second highest frequency.
- No `_byStepSpanId` index: GetStepTimeline and GetStepSpanGroup computed at query time from flat lists + StepNumber/StepSpanId filter (O(n) for in-memory data, acceptable). Phase 3 may add if data volume grows.

### ITraceRecorder — pure write contract (7 methods)

```csharp
public interface ITraceRecorder
{
    // Session lifecycle (2)
    Task<TraceSession> StartSessionAsync(string traceId,
        Dictionary<string, object>? metadata = null, CancellationToken ct = default);
    Task EndSessionAsync(CancellationToken ct = default);

    // Write — 5 Record methods (append-only)
    Task RecordExecutionAsync(ExecutionRecord r, CancellationToken ct = default);
    Task RecordTransitionAsync(StateTransition t, CancellationToken ct = default);
    Task RecordErrorAsync(ErrorRecord r, CancellationToken ct = default);
    Task RecordPageTransitionAsync(PageTransition t, CancellationToken ct = default);
    Task RecordAICallAsync(AICallRecord r, CancellationToken ct = default);
}
```

Changes from previous draft (13→7):
- Removed: `CurrentSession` getter (moved to ITraceService — write side doesn't need session state)
- Removed: 5 `GetXxxAsync` methods (moved to ITraceService — read contract)
- Removed: `ExportTraceAsync` (moved to ITraceService — read/export operation)

### InMemoryTraceRecorder — minimal async wrapper

```csharp
public sealed class InMemoryTraceRecorder : ITraceRecorder
{
    private readonly ITraceStorage _storage;

    public InMemoryTraceRecorder(ITraceStorage storage) { _storage = storage; }

    public Task<TraceSession> StartSessionAsync(string traceId,
        Dictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var session = new TraceSession(traceId, DateTimeOffset.UtcNow, null, metadata);
        _storage.SetSession(session);
        return Task.FromResult(session);
    }

    public Task EndSessionAsync(CancellationToken ct = default)
    { _storage.EndSession(); return Task.CompletedTask; }

    public Task RecordExecutionAsync(ExecutionRecord r, CancellationToken ct = default)
    { _storage.AddExecution(r); return Task.CompletedTask; }

    public Task RecordTransitionAsync(StateTransition t, CancellationToken ct = default)
    { _storage.AddTransition(t); return Task.CompletedTask; }

    public Task RecordErrorAsync(ErrorRecord r, CancellationToken ct = default)
    { _storage.AddError(r); return Task.CompletedTask; }

    public Task RecordPageTransitionAsync(PageTransition t, CancellationToken ct = default)
    { _storage.AddPageTransition(t); return Task.CompletedTask; }

    public Task RecordAICallAsync(AICallRecord r, CancellationToken ct = default)
    { _storage.AddAICall(r); return Task.CompletedTask; }
}
```

Each method: `_storage.AddXxx()` + `Task.CompletedTask`. Zero business logic. Pure async-over-sync wrapper.

### ITraceService — pure read+query facade (1 property + 12 methods)

```csharp
public interface ITraceService
{
    // Session read (1 property)
    TraceSession? CurrentSession { get; }

    // Flat read (5)
    IReadOnlyList<ExecutionRecord> GetExecutions();
    IReadOnlyList<StateTransition> GetTransitions();
    IReadOnlyList<ErrorRecord> GetErrors();
    IReadOnlyList<PageTransition> GetPageTransitions();
    IReadOnlyList<AICallRecord> GetAICalls();

    // Node+Span queries (6)
    TraversalTree ReconstructTree();
    NodeSpans GetNodeSpans(string nodeId);
    NodeVisitTimeline GetNodeVisitTimeline(string nodeId);
    StepTimeline GetStepTimeline(int stepNumber);
    IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType);
    StepSpanGroup GetStepSpanGroup(string stepSpanId);

    // Export (1)
    string ExportTrace(string format = "json");
}
```

### InMemoryTraceService — query implementation

```csharp
public sealed class InMemoryTraceService : ITraceService
{
    // Inject InMemoryTraceStorage (concrete) — needs index methods not on ITraceStorage interface
    private readonly InMemoryTraceStorage _storage;

    public InMemoryTraceService(InMemoryTraceStorage storage) { _storage = storage; }

    public TraceSession? CurrentSession => _storage.CurrentSession;

    // Flat read — delegate to storage
    public IReadOnlyList<ExecutionRecord> GetExecutions() => _storage.GetExecutions();
    public IReadOnlyList<StateTransition> GetTransitions() => _storage.GetTransitions();
    public IReadOnlyList<ErrorRecord> GetErrors() => _storage.GetErrors();
    public IReadOnlyList<PageTransition> GetPageTransitions() => _storage.GetPageTransitions();
    public IReadOnlyList<AICallRecord> GetAICalls() => _storage.GetAICalls();

    // Queries — use indexes + flat reads (access correlation via Context?.)
    public TraversalTree ReconstructTree()
    {
        var edges = _storage.GetBySpanType(SpanType.DfsForward)
            .Where(e => e.ChildNodeId != null)
            .Select(e => new TreeEdge(e.Context?.NodeId ?? "", e.ChildNodeId!, e.Depth ?? 0, e.Context?.StepNumber ?? 0));
        var root = edges.FirstOrDefault()?.Parent ?? "";
        return new TraversalTree(edges.ToImmutableArray(), root);
    }

    public NodeSpans GetNodeSpans(string nodeId) => new NodeSpans(
        NodeId: nodeId,
        Executions: _storage.GetByNodeId(nodeId).ToImmutableArray(),
        Errors: _storage.GetErrors().Where(e => e.Context?.NodeId == nodeId).ToImmutableArray(),
        PageTransitions: _storage.GetPageTransitions().Where(p => p.Context?.NodeId == nodeId).ToImmutableArray(),
        Transitions: _storage.GetTransitions().Where(t => t.Context?.NodeId == nodeId).ToImmutableArray());

    public NodeVisitTimeline GetNodeVisitTimeline(string nodeId)
    {
        var nodeExecs = _storage.GetByNodeId(nodeId);
        var entry = nodeExecs.FirstOrDefault(e => e.SpanType == SpanType.DfsForward);
        var exit = nodeExecs.FirstOrDefault(e => e.SpanType == SpanType.DfsBacktrack);
        return new NodeVisitTimeline(nodeId, entry?.Context?.StepNumber, exit?.Context?.StepNumber,
            nodeExecs.ToImmutableArray());
    }

    public StepTimeline GetStepTimeline(int stepNumber) => new StepTimeline(
        StepNumber: stepNumber,
        Executions: _storage.GetExecutions().Where(e => e.Context?.StepNumber == stepNumber).ToImmutableArray(),
        Transitions: _storage.GetTransitions().Where(t => t.Context?.StepNumber == stepNumber).ToImmutableArray(),
        Errors: _storage.GetErrors().Where(e => e.Context?.StepNumber == stepNumber).ToImmutableArray(),
        PageTransitions: _storage.GetPageTransitions().Where(p => p.Context?.StepNumber == stepNumber).ToImmutableArray(),
        AICalls: _storage.GetAICalls().Where(a => a.Context?.StepNumber == stepNumber).ToImmutableArray());

    public IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType)
        => _storage.GetBySpanType(spanType);

    public StepSpanGroup GetStepSpanGroup(string stepSpanId) => new StepSpanGroup(
        StepSpanId: stepSpanId,
        StepNumber: _storage.GetExecutions().FirstOrDefault(e => e.Context?.StepSpanId == stepSpanId)?.Context?.StepNumber,
        Executions: _storage.GetExecutions().Where(e => e.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
        Transitions: _storage.GetTransitions().Where(t => t.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
        Errors: _storage.GetErrors().Where(e => e.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
        PageTransitions: _storage.GetPageTransitions().Where(p => p.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
        AICalls: _storage.GetAICalls().Where(a => a.Context?.StepSpanId == stepSpanId).ToImmutableArray());

    public string ExportTrace(string format = "json") => _storage.Export(format);
}
```

Injection asymmetry rationale:
- InMemoryTraceRecorder injects **ITraceStorage** (interface) — write side doesn't need indexes
- InMemoryTraceService injects **InMemoryTraceStorage** (concrete) — read side needs indexes
- Different ITraceStorage implementations can have different query strategies (DatabaseTraceStorage → SQL queries, FileTraceStorage → scan). Forcing all implementations to provide index methods violates ISP.

### DI registration

```csharp
services.AddSingleton<InMemoryTraceStorage>();
services.AddSingleton<ITraceStorage, InMemoryTraceStorage>();               // self-registration
services.AddSingleton<ITraceRecorder>(sp =>
    new InMemoryTraceRecorder(sp.GetRequiredService<ITraceStorage>()));
services.AddSingleton<ITraceService>(sp =>
    new InMemoryTraceService(sp.GetRequiredService<InMemoryTraceStorage>()));
```

---

## Section 3: Query Result Types (6 new records)

```csharp
// DFS tree — computed from flat records at query time (not stored)
public sealed record class TraversalTree(
    ImmutableArray<TreeEdge> Edges,
    string RootNodeId);

public sealed record class TreeEdge(
    string Parent,       // DFS parent node
    string Child,        // DFS child node
    int Depth,           // tree depth
    int EntryStep);      // step when child was pushed

// Node's all spans (Node → Span grouping)
public sealed record class NodeSpans(
    string NodeId,
    ImmutableArray<ExecutionRecord> Executions,
    ImmutableArray<ErrorRecord> Errors,
    ImmutableArray<PageTransition> PageTransitions,
    ImmutableArray<StateTransition> Transitions);

// Node visit timeline (enter → events → exit)
public sealed record class NodeVisitTimeline(
    string NodeId,
    int? EntryStep,
    int? ExitStep,
    ImmutableArray<ExecutionRecord> Spans);

// Step timeline (all events in one engine step)
public sealed record class StepTimeline(
    int StepNumber,
    ImmutableArray<ExecutionRecord> Executions,
    ImmutableArray<StateTransition> Transitions,
    ImmutableArray<ErrorRecord> Errors,
    ImmutableArray<PageTransition> PageTransitions,
    ImmutableArray<AICallRecord> AICalls);

// StepSpan grouping (all records with same StepSpanId)
public sealed record class StepSpanGroup(
    string StepSpanId,
    int? StepNumber,
    ImmutableArray<ExecutionRecord> Executions,
    ImmutableArray<StateTransition> Transitions,
    ImmutableArray<ErrorRecord> Errors,
    ImmutableArray<PageTransition> PageTransitions,
    ImmutableArray<AICallRecord> AICalls);
```

All are **computed records** — built from flat records + indexes at query time, not stored. Consistent with project immutable philosophy.

---

## Section 4: TraceCoordinator Changes

### New internal state

```csharp
public sealed class TraceCoordinator
{
    private readonly ITraceRecorder? _recorder;
    private readonly string? _traceId;
    private readonly ITraversalContext? _ctx;     // ← NEW: engine context reference
    private int _spanCounter = 0;                  // ← NEW: SpanId counter
    private string? _currentStepSpanId;             // ← NEW: per-step grouping ID

    // StepTraceSnapshot state (unchanged from previous draft)
    private readonly List<SpanType> _stepSpanTypes = new();
    private string? _stepPageFrom;
    private string? _stepPageTo;
    private string? _stepPageTransitionType;
    private readonly Stopwatch _stepStopwatch = new();

    public bool Active => _recorder != null && !string.IsNullOrWhiteSpace(_traceId);

    // Constructor extended
    public TraceCoordinator(ITraceRecorder? recorder = null, string? traceId = null,
        ITraversalContext? ctx = null)
    { _recorder = recorder; _traceId = traceId; _ctx = ctx; }
}
```

### BuildCorrelation — TraceContext construction helper

TraceCoordinator fills 4 common correlation fields via `BuildCorrelation()`, producing a single TraceContext object instead of 4 separate parameters per record:

```csharp
private TraceContext? BuildCorrelation()
{
    if (_ctx == null) return null;
    return new TraceContext(
        NodeId: _ctx.CurrentFrame?.NodeId,
        StepSpanId: _currentStepSpanId,
        StepNumber: _ctx.StepCount,
        TraceId: _traceId);
}
```

### SpanId generation

```csharp
private string NextSpanId() => $"{_traceId}-{++_spanCounter:D6}";
// Format: "abc-000001", "abc-000002" — fixed-width, sortable, readable
```

### StepSpanId lifecycle

StepSpanId = StepStart's SpanId. Assigned at RecordStepStart, released at RecordStepEnd.

```csharp
public void RecordStepStart(string nodeId, string result)
{
    if (!Active) return;
    var spanId = NextSpanId();
    _currentStepSpanId = spanId;          // StepSpanId = StepStart.SpanId
    _stepStopwatch.Restart();
    _stepSpanTypes.Clear();

    LogAndContinue(() => _recorder?.RecordExecutionAsync(new ExecutionRecord(
        Action: "step_start", Status: result, SpanType: null,
        Context: BuildCorrelation() with { StepSpanId = spanId },  // ← StepSpanId=SpanId for StepStart
        SpanId: spanId, ParentNodeId: null, ChildNodeId: null,
        PageId: _ctx?.CurrentPageId,
        Depth: _ctx?.NodeStack.Depth,
        Timestamp: DateTimeOffset.UtcNow)));
}

public void RecordStepEnd(string nodeId, string result)
{
    if (!Active) return;
    LogAndContinue(() => _recorder?.RecordExecutionAsync(new ExecutionRecord(
        Action: "step_end", Status: result, SpanType: null,
        Context: BuildCorolation(),                      // ← uses current StepSpanId
        SpanId: NextSpanId(),
        PageId: _ctx?.CurrentPageId,
        Depth: _ctx?.NodeStack.Depth,
        DurationMs: _stepStopwatch.Elapsed.TotalMilliseconds,
        Timestamp: DateTimeOffset.UtcNow)));
    _currentStepSpanId = null;            // release
}
```

### RecordActionExecution — typed signature change

```csharp
// Before: RecordActionExecution(string action, string target, bool success)
// After: typed parameters — enables TargetType + TargetValue extraction
public void RecordActionExecution(OperationType action, Target? target, bool success)
{
    if (!Active) return;
    var (targetType, targetValue) = SerializeTarget(target);
    LogAndContinue(() => _recorder?.RecordExecutionAsync(new ExecutionRecord(
        Action: action.ToString().ToLowerInvariant(),
        Status: success ? "success" : "failed",
        SpanId: NextSpanId(), StepSpanId: _currentStepSpanId,
        NodeId: _ctx?.CurrentFrame?.NodeId,
        ParentNodeId: _ctx?.CurrentFrame?.Parent?.NodeId,
        PageId: _ctx?.CurrentPageId,
        TargetType: targetType,
        TargetValue: targetValue,
        StepNumber: _ctx?.StepCount, TraceId: _traceId,
        Depth: _ctx?.NodeStack.Depth,
        Timestamp: DateTimeOffset.UtcNow)));
}

private (TargetType?, string?) SerializeTarget(Target? target)
{
    if (target == null) return (null, null);  // Back/NoAction → null
    var valueStr = target.Value switch
    {
        string s => s,                        // Text → "connect"
        Coordinate c => $"{c.X},{c.Y}",       // Coordinate → "100,200"
        int i => i.ToString(),                 // UiIndex → "3"
        _ => target.Value.ToString()           // fallback
    };
    return (target.By, valueStr);
}
```

### RecordAICallSpan — typed (with TraceContext)

```csharp
public void RecordAICallSpan(string capability, string providerId, bool success,
    double latencyMs, int? tokens = null)
{
    if (!Active) return;
    _stepSpanTypes.Add(SpanType.AICall);
    LogAndContinue(() => _recorder?.RecordAICallAsync(new AICallRecord(
        Capability: capability, ProviderId: providerId,
        Success: success, LatencyMs: latencyMs, Tokens: tokens,
        Context: BuildCorrelation(),             // ← 4 correlation fields in one object
        Timestamp: DateTimeOffset.UtcNow)));
}
```

### Complete method mapping table (TraceContext-based)

| Method | → Record type | Context (TraceContext) | SpanId | ChildNodeId | ParentNodeId | PageId | TargetType | TargetValue | FsmType |
|--------|-------------|----------------------|--------|-------------|-------------|--------|-----------|------------|---------|
| RecordStepStart | ExecutionRecord | BuildCorrelation() with StepSpanId=spanId | ✅ (=StepSpanId) | null | null | ✅ | null | null | — |
| RecordStepEnd | ExecutionRecord | BuildCorrelation() | ✅ | null | null | ✅ | null | null | — |
| RecordPageAnalysis | ExecutionRecord | BuildCorrelation() | ✅ | null | null | ✅ | null | null | — |
| RecordActionExecution | ExecutionRecord | BuildCorrelation() | ✅ | null | null | ✅ | ✅ | ✅ | — |
| RecordSkipSpan → DfsForward | ExecutionRecord | BuildCorrelation() | ✅ | matchResult.NodeId | null | ✅ | null | null | — |
| RecordErrorSpan | ErrorRecord | BuildCorrelation() | — | — | — | — | — | — | — |
| RecordDecision | ExecutionRecord | BuildCorrelation() | ✅ | null | null | ✅ | null | null | — |
| RecordPageTransition | PageTransition | BuildCorrelation() | — | — | — | — | — | — | — |
| RecordDynamicLifecycle → DfsForward | ExecutionRecord | BuildCorrelation() | ✅ | parentId param | ✅ | — | null | null | — |
| RecordStateDecision | ExecutionRecord | BuildCorrelation() | ✅ | null | null | ✅ | null | null | — |
| RecordStateTransition | StateTransition | BuildCorrelation() | — | — | — | — | — | — | "TraversalFSM" |
| RecordRootNodePushed | StateTransition | **null** (before step loop) | — | — | — | — | — | — | "TraversalFSM" |
| RecordAICallSpan | AICallRecord | BuildCorrelation() | — | — | — | — | — | — | — |

Note: RecordRootNodePushed Context=null (called before engine step loop starts).

### Engine call points (unchanged from previous draft)

- Step 2: `ctx.Trace.RecordStepStart(currentNodeId, "")`
- Step 7: `ctx.Trace.RecordStateTransition(fromState, nextState)`
- Step 14: `ctx.Trace.RecordStepEnd(currentNodeId, nextState)`
- Step 4 (path changed): `ctx.Trace.RecordPageAnalysis(pageAnalysis)`
- Step 5 (action): `ctx.Trace.RecordActionExecution(action, target, success)` ← typed signature
- TraversalEngine.RunAsync: `ctx.Trace.RecordPageTransition(fromPage, toPage, "forward"/"back")`
- TraversalEngine.RunAsync: `_stepCtx.Trace.RecordAICallSpan("vision", "provider", true, latencyMs)`
- DynamicChildManager: `ctx.Trace.RecordDynamicLifecycle("generate", nodeId, parentId, ruleId, elementId)`

---

## Section 5: StepTraceSnapshot + TraceRecord Extension (unchanged from previous draft)

### StepTraceSnapshot — replaces GetLastXxx()

```csharp
public sealed record class StepTraceSnapshot(
    ImmutableArray<SpanType> SpanTypes,
    string? PageFrom,
    string? PageTo,
    string? PageTransitionType,
    double? StepDurationMs);
```

TraceCoordinator maintains per-step event collection. Each Record method appends to `_stepSpanTypes` (not overwrites). Engine reads `GetStepSnapshot()` at step end, resets for next step.

### TraceRecord extension (14 fields)

```csharp
public sealed record class TraceRecord(
    int StepNumber, TraversalState FromState, TraversalState ToState,
    string? CurrentNodeId, string? CurrentPageId, string? ActionExecuted,
    bool ActionSuccess, bool ChildPushed, bool FrameCompleted,
    ImmutableArray<SpanType> SpanTypes = default,
    string? PageFrom = null, string? PageTo = null,
    string? PageTransitionType = null,
    double? StepDurationMs = null);
```

TraceRecord.SpanType field changed from `SpanType?` (single) to `ImmutableArray<SpanType>` (multi-value) to reflect that one step can produce multiple semantic events.

### TraversalResult.Trace — unchanged

```csharp
Trace: trace?.ToImmutableArray() ?? ImmutableArray<TraceRecord>.Empty
```

### Two data paths (unchanged semantics)

| Path | Purpose | Consumer |
|------|---------|---------|
| `TraversalResult.Trace` (TraceRecord) | Backward-compatible per-step view | ExpectedBehavior (5 existing dimensions) |
| `ITraceStorage` → `ITraceService` (5 record types + queries) | Canonical event-level data + derived queries | Analysis, Dashboard, Phase 3 advanced capabilities |

---

## Section 6: TraversalEngine Integration

### RunAsync changes

```csharp
// Before
var traceRecords = _config.TraceEnabled ? new List<TraceRecord>() : null;

// After (three-layer architecture)
var storage = _config.TraceEnabled ? new InMemoryTraceStorage() : null;
var recorder = storage != null ? new InMemoryTraceRecorder(storage) : null;
_traceRecorder = recorder ?? _traceRecorder;
// TraceCoordinator created in Initialize() — uses _traceRecorder (ITraceRecorder)
// InMemoryTraceStorage accessible for later ITraceService construction
```

TraceCoordinator constructor extended with ITraversalContext:
```csharp
// In Initialize():
_stepCtx.Trace = new TraceCoordinator(_traceRecorder, _ctx.TraceId, _ctx);
```

---

## Correctness & Extensibility Audit Resolutions

| # | Issue | Severity | Resolution |
|---|-------|----------|-----------|
| 1 | Last-value race condition | 🔴 | Replaced GetLastXxx() with StepTraceSnapshot (unchanged from previous draft) |
| 2 | Dual data paths | ⚠️ | Corrected — TraceRecord = backward-compatible per-step view; ITraceStorage/ITraceService = canonical event-level data + queries |
| 3 | TraceNode dead code | ⚠️ | Deleted — ITraceStorage + InMemoryTraceStorage replace storage role; ITraceService replaces query role |
| 4 | ExecutionRecord field order | ⚠️ | Documented dependency: phase22-refactoring adds SpanType first, this change adds remaining fields after |
| 5 | GetAwaiter().GetResult() sync-over-async | ⚠️ | InMemoryTraceRecorder now just `_storage.AddXxx() + Task.CompletedTask` — no GetResult needed at Recorder level. TraceCoordinator still uses LogAndContinue with GetResult — safe in CLI/test context |
| 6 | GetLastXxx() O(n) growth | 🔴 | Replaced by StepTraceSnapshot (unchanged) |
| 7 | InMemoryTraceRecorder flat list without tree queries | ⚠️ | Resolved by three-layer architecture: InMemoryTraceStorage has _byNodeId + _bySpanType indexes; InMemoryTraceService provides query methods |
| 8 | TraceNode vs ITraceRecorder records undefined | ⚠️ | Resolved by deleting TraceNode — ITraceStorage records are canonical model |
| 9 | RecordAICallSpan untyped `object ai` | ⚠️ | Changed to typed: (string capability, string providerId, bool success, double latencyMs, int? tokens) |
| 10 | TraceRecord SpanType single value | 🔴 | Changed to ImmutableArray<SpanType> (unchanged from previous draft) |
| 11 | ExecutionRecord.ParentNodeId naming confusion | ⚠️ | Renamed: NodeId moved into TraceContext (event-at-this-node), ParentNodeId stays on ExecutionRecord (DFS-tree-parent) |
| 12 | No SpanId for individual record identification | ⚠️ | Added SpanId to ExecutionRecord (generated by TraceCoordinator counter) |
| 13 | No per-step grouping key across 5 record types | ⚠️ | Added StepSpanId in TraceContext, shared across all 5 record types |
| 14 | ExecutionRecord.Target is object? | ⚠️ | Replaced by TargetType? + string? TargetValue on ExecutionRecord |
| 15 | DFS tree reconstruction depends on inference | ⚠️ | Added ChildNodeId to ExecutionRecord (DfsForward explicitly records pushed child) |
| 16 | Node ↔ Page association missing | ⚠️ | Added PageId to ExecutionRecord |
| 17 | StateTransition no FSM type tag | ⚠️ | Added FsmType on StateTransition ("TraversalFSM" this iteration; "GlobalFSM" Phase 3). TraceContext encapsulates NodeId/StepSpanId/StepNumber/TraceId |
| 18 | AICallRecord no correlation to node/step | ⚠️ | Added TraceContext to AICallRecord (encapsulates NodeId+StepSpanId+StepNumber+TraceId) |
| 19 | ITraceRecorder mixed write+read contract | ⚠️ | Split: ITraceRecorder (7 write) + ITraceService (13 read+query) + ITraceStorage (shared backend) |
| 20 | InMemoryTraceRecorder stores + reads + queries (3 responsibilities) | ⚠️ | Split into InMemoryTraceStorage (store+indexes) + InMemoryTraceRecorder (write wrapper) + InMemoryTraceService (read+query) |
| 21 | Correlation fields repeated across 5 record types (4×5=20 params) | ⚠️ | Encapsulated into TraceContext sealed record class (1×5=5 params). Core domain fields stay clean; observability correlation in Context?. Phase 3 extension: add VisitSpanId/ParentSpanId to TraceContext, not to 5 record types |

---

## Phase 3 Defer List

| Item | Reason |
|------|--------|
| ITraceService.FsmAnalysis() | Needs GlobalFSM callback writing to ITraceRecorder — engine RunAsync code change |
| ITraceService.DigestExecutionPlan() | Advanced analysis — depends on service layer being stable |
| ITraceService.GetPerformanceProfile() | Same |
| ITraceService.BuildReplayScript() | Replay system — independent component |
| ITraceService.BuildStateFixture() | Same |
| ReplayExecutor + ReplayScript + ReplayResult | New component, depends on ITraceService |
| GlobalFSM callbacks → ITraceRecorder | Engine RunAsync registration — high risk |
| VisitSpanId (per-node-visit) | TraceCoordinator must detect NodeId change across steps |
| ParentSpanId (span causality tree) | TraceCoordinator must maintain active span stack |
| AICallRecord.SpanId | Phase 3 alongside ParentSpanId |
| ExportTraceAsync async stream | Current string return sufficient |

---

## Implementation Dependency Chain

```
Phase 1 (delete dead code — zero risk):
  TraceNode.cs → UlidGenerator.cs → 8 tests → guard test

Phase 2 (data model — all record field changes):
  ExecutionRecord → StateTransition → ErrorRecord → PageTransition → AICallRecord → TraceRecord 5 new fields

Phase 3 (storage architecture — three-layer separation):
  ITraceStorage interface → InMemoryTraceStorage implementation → ITraceRecorder slim down
  → InMemoryTraceRecorder rewrite → ITraceService interface → InMemoryTraceService implementation
  → 6 query result types

Phase 4 (write logic — TraceCoordinator):
  TraceCoordinator refactor (SpanId/StepSpanId + ctx reference + typed signatures)
  → StepTraceSnapshot → TraversalEngine.RunAsync changes

Phase 5 (verification — tests + guards):
  Storage index tests → Service query tests → TraceCoordinator fill tests
  → Guard tests → 229 tests all green
```
