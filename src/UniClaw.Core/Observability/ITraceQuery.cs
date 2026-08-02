namespace UniClaw.Core.Observability;

/// <summary>
/// ITraceQuery — read-only span-tree query surface for analyzers and consumers.
/// Inherits ITraceService (12 existing query methods + 1 property) per design D3.
/// The 5 new span-tree methods operate on the TraceSpan list parallel to ExecutionRecord.
/// ITraceQuery : ITraceService is the default; if it grows fat, switch to composition
/// without breaking ITraceService consumers (escape hatch, design.md §11).
/// </summary>
public interface ITraceQuery : ITraceService
{
    /// <summary>
    /// Get the root span (ParentSpanId == null). Returns null when no spans are recorded.
    /// </summary>
    TraceSpan? GetRootSpan();

    /// <summary>
    /// Get all spans matching a dotted spanType string (e.g. "entry.observed").
    /// The spanType must be a member of the SpanTypes catalog.
    /// </summary>
    IReadOnlyList<TraceSpan> GetSpansByType(string spanType);

    /// <summary>
    /// Get all child spans whose ParentSpanId matches the given parent spanId.
    /// Returns empty list when no children exist or the parent spanId is not found.
    /// </summary>
    IReadOnlyList<TraceSpan> GetChildSpans(string parentSpanId);

    /// <summary>
    /// Get a single span by its SpanId. Returns null when no span with that id exists.
    /// </summary>
    TraceSpan? GetSpan(string spanId);

    /// <summary>
    /// Get all recorded spans in insertion order.
    /// </summary>
    IReadOnlyList<TraceSpan> GetAllSpans();
}
