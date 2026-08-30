using System.Collections.Immutable;
using UniClaw.Runtime.Harness;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Pure derivation of the functional-trajectory timeline from one finalized
/// <c>TraceRun</c> and its projected <c>RuntimeEventEnvelope</c> stream
/// (observability-trajectory work). Deterministic and fail-safe: empty input
/// yields an empty-but-valid timeline; no exception escapes for malformed
/// records.
/// </summary>
public static class RunTimelineProjector
{
    /// <summary>Project one run's timeline. Diagnostics carry truthful derivation notes.</summary>
    public static RunTimelineResult Project(
        TraceRun? trace,
        ImmutableArray<RuntimeEventEnvelope> events)
    {
        if (trace is null)
        {
            return new RunTimelineResult(
                TraceQueryStatus.Found,
                new RunTimeline
                {
                    Segments = [],
                    Markers = [.. events.OrderBy(e => e.Sequence).Select(ToMarker)],
                    TimeSummary = [],
                    Diagnostics = ["Trace is absent; timeline carries projected decision markers only."],
                },
                []);
        }

        var segments = trace.Spans
            .OrderBy(s => s.StartOffsetNs)
            .Select(s => new TimelineSegment(
                s.SpanId,
                s.ParentSpanId,
                s.Name,
                s.Layer,
                s.Component,
                s.Outcome,
                s.StartOffsetNs,
                s.DurationNs))
            .ToImmutableArray();

        var summary = segments
            .GroupBy(s => (s.Layer, s.Component))
            .Select(g => ComputeSummary(g.Key.Layer, g.Key.Component, g.ToArray()))
            .OrderBy(x => x.TotalNs, Comparer<long>.Default)
            .ToImmutableArray();

        return new RunTimelineResult(
            TraceQueryStatus.Found,
            new RunTimeline
            {
                Segments = segments,
                Markers = [.. events.OrderBy(e => e.Sequence).Select(ToMarker)],
                TimeSummary = summary,
                Diagnostics = [],
            },
            []);
    }

    /// <summary>Markers carry projected stream order (decision markers are untimed).</summary>
    private static TimelineMarker ToMarker(RuntimeEventEnvelope e)
        => new(e.EventId, e.Kind, e.Sequence, e.ObservationSequence, e.CorrelationId);

    private static StageTimeSummary ComputeSummary(string layer, string component, TimelineSegment[] segments)
    {
        var total = segments.Sum(s => s.DurationNs);
        return new StageTimeSummary(
            layer,
            component,
            segments.Length,
            total,
            segments.Length == 0 ? 0 : total / segments.Length,
            segments.Max(s => s.DurationNs));
    }
}