using UniClaw.Core.Domain;

namespace UniClaw.Core.Observability;

/// <summary>
/// TraceSpan — OpenTelemetry-style span with parent-child tree, timing, and attribute bag.
/// A parallel system to the existing ExecutionRecord/SpanType enum — coexisting, not replacing.
/// SpanType is a dotted string namespace (e.g. "engine.run", "entry.observed"), intentionally
/// NOT the constitution-locked SpanType enum (design.md D1, D5 of trace-span-observability).
/// EndTime is mutable via EndSpan (EndTime is set when the span is closed); Status and Attributes
/// may be updated at close time. TraceSpan is a sealed record class for structural equality;
/// EndSpan uses `with` to replace the span in storage.
/// </summary>
/// <param name="SpanId">Unique span identifier (counter-based, same pattern as ExecutionRecord.SpanId)</param>
/// <param name="ParentSpanId">Parent span identifier (null for root spans)</param>
/// <param name="SpanType">Dotted string span type (e.g. "engine.run", "entry.generate")</param>
/// <param name="SpanName">Human-readable span name (e.g. "step 4: click Network")</param>
/// <param name="StartTime">Span start timestamp (UTC)</param>
/// <param name="EndTime">Span end timestamp (null while open, set by EndSpan)</param>
/// <param name="Status">Span status: "ok" | "error" | "deny" | "skip"</param>
/// <param name="Context">Observability correlation envelope (TraceContext: NodeId, StepSpanId, StepNumber, TraceId)</param>
/// <param name="Attributes">Key-value attribute bag (null = empty)</param>
public sealed record class TraceSpan(
    string SpanId,
    string? ParentSpanId,
    string SpanType,
    string SpanName,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    string Status,
    TraceContext? Context,
    Dictionary<string, object>? Attributes = null)
{
    /// <summary>Duration in milliseconds (0 when EndTime is null — span still open).</summary>
    public double DurationMs =>
        EndTime.HasValue ? (EndTime.Value - StartTime).TotalMilliseconds : 0;

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "ok", "error", "deny", "skip"
    };

    /// <summary>
    /// Validate Status at construction time (fail-fast).
    /// Only "ok" / "error" / "deny" / "skip" are valid span transition statuses.
    /// </summary>
    public bool Validate()
    {
        if (!ValidStatuses.Contains(Status))
            throw new DomainValidationException(nameof(Status), Status,
                $"Span status must be one of [ok, error, deny, skip]. Got: '{Status}'.");
        return true;
    }
}
