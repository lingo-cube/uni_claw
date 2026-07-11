namespace UniClaw.Core.Observability;

/// <summary>
/// TraceContext — observability correlation envelope shared by ALL 5 ITraceRecorder record types.
/// Encapsulates "when/where/how was this event recorded" — observability correlation, not core domain data.
/// Field boundary rule: ONLY fields shared by ALL 5 types belong here.
/// Type-specific fields (FsmType, SpanId, ChildNodeId, ParentNodeId, PageId,
/// TargetType/TargetValue, Depth, DurationMs, Tokens) stay on their respective record types.
/// Phase 3 will add VisitSpanId + ParentSpanId as general correlation (4→6 fields).
/// </summary>
/// <param name="NodeId">The node the event occurred at (NOT DFS parent)</param>
/// <param name="StepSpanId">Per-engine-step grouping key (= StepStart's SpanId)</param>
/// <param name="StepNumber">Step ordinal in traversal session</param>
/// <param name="TraceId">Trace session identifier</param>
public sealed record class TraceContext(
    string? NodeId = null,
    string? StepSpanId = null,
    int? StepNumber = null,
    string? TraceId = null);
