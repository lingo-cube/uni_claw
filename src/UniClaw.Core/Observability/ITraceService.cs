namespace UniClaw.Core.Observability;

/// <summary>
/// ITraceService — pure read+query facade for trace data.
/// 1 property (CurrentSession) + 12 methods: 5 flat read + 6 Node+Span queries + 1 export.
/// SHALL NOT include any write or session lifecycle methods (StartSessionAsync, EndSessionAsync,
/// Record methods belong on ITraceRecorder). ITraceService is the read+query contract
/// for analysis, dashboard, and ExpectedBehavior consumers.
/// </summary>
public interface ITraceService
{
    // ── Session (1 property) ──────────────────────────────

    /// <summary>Current trace session (null if not started)</summary>
    TraceSession? CurrentSession { get; }

    // ── Flat read (5 methods) ─────────────────────────────

    /// <summary>Get all execution records</summary>
    IReadOnlyList<ExecutionRecord> GetExecutions();

    /// <summary>Get all state transitions</summary>
    IReadOnlyList<StateTransition> GetTransitions();

    /// <summary>Get all error records</summary>
    IReadOnlyList<ErrorRecord> GetErrors();

    /// <summary>Get all page transitions</summary>
    IReadOnlyList<PageTransition> GetPageTransitions();

    /// <summary>Get all AI call records</summary>
    IReadOnlyList<AICallRecord> GetAICalls();

    // ── Node+Span queries (6 methods) ─────────────────────

    /// <summary>Reconstruct DFS traversal tree from DfsForward edges</summary>
    TraversalTree ReconstructTree();

    /// <summary>Aggregate all 5 record types by nodeId via Context?.NodeId</summary>
    NodeSpans GetNodeSpans(string nodeId);

    /// <summary>Find entry and exit steps for a node visit</summary>
    NodeVisitTimeline GetNodeVisitTimeline(string nodeId);

    /// <summary>Aggregate all 5 record types by stepNumber via Context?.StepNumber</summary>
    StepTimeline GetStepTimeline(int stepNumber);

    /// <summary>Get execution records by SpanType</summary>
    IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType);

    /// <summary>Aggregate all 5 record types by stepSpanId via Context?.StepSpanId</summary>
    StepSpanGroup GetStepSpanGroup(string stepSpanId);

    // ── Export (1 method) ─────────────────────────────────

    /// <summary>Export trace data as JSON string</summary>
    string ExportTrace();
}
