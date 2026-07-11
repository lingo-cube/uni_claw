using System.Collections.Immutable;
using System.Linq;

namespace UniClaw.Core.Observability;

/// <summary>
/// InMemoryTraceService — injects InMemoryTraceStorage (concrete, not interface) per D-2b.
/// Service gets index access (GetByNodeId, GetBySpanType) that are not on ITraceStorage.
/// Different storage backends have different query strategies (SQL for DB, scan for file).
/// Flat read methods delegate to _storage.GetXxx(). Query methods use _storage indexes
/// where available and flat list + LINQ filtering with TraceContext access pattern.
/// </summary>
public sealed class InMemoryTraceService : ITraceService
{
    private readonly InMemoryTraceStorage _storage;

    /// <summary>Construct InMemoryTraceService injecting InMemoryTraceStorage concrete</summary>
    public InMemoryTraceService(InMemoryTraceStorage storage)
    {
        _storage = storage;
    }

    // ── Session (1 property) ──────────────────────────────

    public TraceSession? CurrentSession => _storage.CurrentSession;

    // ── Flat read (5 methods) ─────────────────────────────

    public IReadOnlyList<ExecutionRecord> GetExecutions() => _storage.GetExecutions();
    public IReadOnlyList<StateTransition> GetTransitions() => _storage.GetTransitions();
    public IReadOnlyList<ErrorRecord> GetErrors() => _storage.GetErrors();
    public IReadOnlyList<PageTransition> GetPageTransitions() => _storage.GetPageTransitions();
    public IReadOnlyList<AICallRecord> GetAICalls() => _storage.GetAICalls();

    // ── Node+Span queries (6 methods) ─────────────────────

    /// <summary>
    /// ReconstructTree — DFS traversal tree from DfsForward edges via Context.NodeId + ChildNodeId.
    /// </summary>
    public TraversalTree ReconstructTree()
    {
        var dfsEdges = _storage.GetBySpanType(SpanType.DfsForward)
            .Where(r => r.ChildNodeId != null)
            .Select(r => new TreeEdge(
                Parent: r.Context?.NodeId,
                Child: r.ChildNodeId!,
                Depth: r.Depth,
                EntryStep: r.Context?.StepNumber))
            .ToImmutableArray();

        // Root: the first DfsForward edge parent that has no incoming edge
        var rootCandidates = dfsEdges
            .Where(e => e.Parent != null)
            .Select(e => e.Parent!)
            .Distinct()
            .ToList();
        var children = dfsEdges.Select(e => e.Child).ToHashSet();
        var rootNodeId = rootCandidates.FirstOrDefault(p => !children.Contains(p)) ?? "";

        return new TraversalTree(dfsEdges, rootNodeId);
    }

    /// <summary>
    /// GetNodeSpans — aggregate all 5 record types by nodeId via Context?.NodeId.
    /// Executions use _storage.GetByNodeId index; others filter flat lists.
    /// </summary>
    public NodeSpans GetNodeSpans(string nodeId)
    {
        return new NodeSpans(
            NodeId: nodeId,
            Executions: _storage.GetByNodeId(nodeId).ToImmutableArray(),
            Errors: _storage.GetErrors()
                .Where(r => r.Context?.NodeId == nodeId).ToImmutableArray(),
            PageTransitions: _storage.GetPageTransitions()
                .Where(r => r.Context?.NodeId == nodeId).ToImmutableArray(),
            Transitions: _storage.GetTransitions()
                .Where(r => r.Context?.NodeId == nodeId).ToImmutableArray(),
            AICalls: _storage.GetAICalls()
                .Where(r => r.Context?.NodeId == nodeId).ToImmutableArray());
    }

    /// <summary>
    /// GetNodeVisitTimeline — find entry and exit from DfsForward/DfsBacktrack.
    /// </summary>
    public NodeVisitTimeline GetNodeVisitTimeline(string nodeId)
    {
        var nodeRecords = _storage.GetByNodeId(nodeId);
        var dfsForward = nodeRecords.FirstOrDefault(r => r.SpanType == SpanType.DfsForward);
        var dfsBacktrack = nodeRecords.FirstOrDefault(r => r.SpanType == SpanType.DfsBacktrack);

        return new NodeVisitTimeline(
            NodeId: nodeId,
            EntryStep: dfsForward?.Context?.StepNumber,
            ExitStep: dfsBacktrack?.Context?.StepNumber);
    }

    /// <summary>
    /// GetStepTimeline — aggregate all 5 record types by stepNumber via Context?.StepNumber.
    /// </summary>
    public StepTimeline GetStepTimeline(int stepNumber)
    {
        return new StepTimeline(
            StepNumber: stepNumber,
            Executions: _storage.GetExecutions()
                .Where(r => r.Context?.StepNumber == stepNumber).ToImmutableArray(),
            Transitions: _storage.GetTransitions()
                .Where(r => r.Context?.StepNumber == stepNumber).ToImmutableArray(),
            Errors: _storage.GetErrors()
                .Where(r => r.Context?.StepNumber == stepNumber).ToImmutableArray(),
            PageTransitions: _storage.GetPageTransitions()
                .Where(r => r.Context?.StepNumber == stepNumber).ToImmutableArray(),
            AICalls: _storage.GetAICalls()
                .Where(r => r.Context?.StepNumber == stepNumber).ToImmutableArray());
    }

    /// <summary>
    /// GetBySpanType — delegate to _storage.GetBySpanType index.
    /// </summary>
    public IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType)
        => _storage.GetBySpanType(spanType);

    /// <summary>
    /// GetStepSpanGroup — aggregate all 5 record types by stepSpanId via Context?.StepSpanId.
    /// </summary>
    public StepSpanGroup GetStepSpanGroup(string stepSpanId)
    {
        return new StepSpanGroup(
            StepSpanId: stepSpanId,
            Executions: _storage.GetExecutions()
                .Where(r => r.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
            Transitions: _storage.GetTransitions()
                .Where(r => r.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
            Errors: _storage.GetErrors()
                .Where(r => r.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
            PageTransitions: _storage.GetPageTransitions()
                .Where(r => r.Context?.StepSpanId == stepSpanId).ToImmutableArray(),
            AICalls: _storage.GetAICalls()
                .Where(r => r.Context?.StepSpanId == stepSpanId).ToImmutableArray());
    }

    // ── Export (1 method) ─────────────────────────────────

    public string ExportTrace() => _storage.Export();
}
