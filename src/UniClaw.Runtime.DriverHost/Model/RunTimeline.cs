using System.Collections.Immutable;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Derived, in-process, read-only functional-trajectory timeline for one run.
/// Pure derivation over the registered finalized <c>TraceRun</c> and the
/// projected <c>RuntimeEventEnvelope</c> stream — NO new emission, NO wire
/// surface, NO authority:
/// <list type="bullet">
/// <item><see cref="Segments"/> — timed span segments (monotonic offsets; the
/// "where time went" spine).</item>
/// <item><see cref="Markers"/> — ordered semantic decision markers (the
/// "what happened" trajectory; untimed — envelope order only).</item>
/// <item><see cref="TimeSummary"/> — duration statistics by (layer, component)
/// for quick 耗时归因.</item>
/// </list>
/// The two tracks are NOT time-merged: segments carry real offsets, markers
/// carry only their projected Sequence (DecisionRecords have no timestamp), so
/// interleaving times would be fabrication — consumers place markers by
/// sequence/observation-anchor themselves.
/// </summary>
public sealed record RunTimeline
{
    /// <summary>Timed span segments, ordered by monotonic start offset.</summary>
    public ImmutableArray<TimelineSegment> Segments { get; init; } = [];

    /// <summary>Ordered semantic decision markers from the projected event stream.</summary>
    public ImmutableArray<TimelineMarker> Markers { get; init; } = [];

    /// <summary>Duration statistics by (layer, component) over the timed segments.</summary>
    public ImmutableArray<StageTimeSummary> TimeSummary { get; init; } = [];

    /// <summary>Truthful derivation diagnostics (never runtime authority).</summary>
    public ImmutableArray<string> Diagnostics { get; init; } = [];
}

/// <summary>One timed span segment on the functional-trajectory timeline.</summary>
public sealed record TimelineSegment(
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Layer,
    string Component,
    string Outcome,
    long StartOffsetNs,
    long DurationNs);

/// <summary>One ordered semantic decision marker (untimed — projected stream order).</summary>
public sealed record TimelineMarker(
    string EventId,
    RuntimeEventKind Kind,
    long Sequence,
    long? ObservationSequence,
    string? CorrelationId);

/// <summary>Aggregated duration evidence for one stage class (耗时归因).</summary>
public sealed record StageTimeSummary(
    string Layer,
    string Component,
    int SpanCount,
    long TotalNs,
    long AverageNs,
    long MaxNs);

/// <summary>Read result mirroring the trace read-model surface.</summary>
public sealed record RunTimelineResult(
    TraceQueryStatus Status,
    RunTimeline? Timeline,
    ImmutableArray<string> Diagnostics)
{
    /// <summary>Unknown-run result.</summary>
    public static RunTimelineResult Unavailable(string message)
        => new(TraceQueryStatus.Unavailable, null, [message]);
}