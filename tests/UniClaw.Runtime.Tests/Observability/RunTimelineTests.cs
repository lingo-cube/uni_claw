using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using Xunit;

namespace UniClaw.Runtime.Tests.Observability;

/// <summary>
/// Functional-trajectory timeline read model — derived, in-process, read-only:
/// timed segments from a finalized TraceRun, ordered decision markers from the
/// projected event stream, and stage duration summaries. Built from public
/// models only (no live Agent / no recorder).
/// </summary>
public sealed class RunTimelineTests
{
    [Fact]
    public void Timeline_SegmentsOrderedByStartOffset()
    {
        var observability = Observability();
        var timeline = observability.GetRunTimeline(ReadOnlyObservabilityFixtures.RunId);

        Assert.Equal(TraceQueryStatus.Found, timeline.Status);
        var segments = timeline.Timeline!.Segments;

        Assert.Equal(3, segments.Length);
        Assert.True(segments.Select(s => s.Name).SequenceEqual(
            ["RunSemanticGoal", "RefreshSnapshot", "LoweredAction"]));
        Assert.True(segments.Select(s => s.StartOffsetNs).SequenceEqual([0L, 10L, 20L]));
        // Structural outcome preserved; attribution stable.
        Assert.All(segments, s => Assert.NotEmpty(s.Layer));
        Assert.All(segments, s => Assert.NotEmpty(s.Component));
        Assert.Contains(segments, s => s.Component == "container.refresh" && s.DurationNs == 5);
    }

    [Fact]
    public void Timeline_MarkersFollowProjectedSequenceOrder()
    {
        var observability = Observability();
        var timeline = observability.GetRunTimeline(ReadOnlyObservabilityFixtures.RunId);

        var markers = timeline.Timeline!.Markers;
        Assert.NotEmpty(markers);
        Assert.Equal(markers.OrderBy(m => m.Sequence).Select(m => m.Sequence),
            markers.Select(m => m.Sequence));
        Assert.Contains(markers, m => m.Kind == RuntimeEventKind.RunCompleted);
        Assert.All(markers, m => Assert.Equal(ReadOnlyObservabilityFixtures.TraceId, m.CorrelationId));
    }

    [Fact]
    public void Timeline_StageTimeSummary_ComputesTotalAvgMax()
    {
        var observability = Observability();
        var timeline = observability.GetRunTimeline(ReadOnlyObservabilityFixtures.RunId);

        var summary = timeline.Timeline!.TimeSummary;
        var agent = Assert.Single(summary, s => s.Component == "agent.execution");
        Assert.Equal("AGENT", agent.Layer);
        Assert.Equal(1, agent.SpanCount);
        Assert.Equal(100, agent.TotalNs);
        Assert.Equal(100, agent.AverageNs);
        Assert.Equal(100, agent.MaxNs);

        var traversal = Assert.Single(summary, s => s.Component == "traversal.execution");
        Assert.Equal(8, traversal.TotalNs);
    }

    [Fact]
    public void Timeline_EmptyTrace_YieldsSegmentsEmpty_MarkersPresent()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.EmptyTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());

        var timeline = observability.GetRunTimeline(ReadOnlyObservabilityFixtures.RunId);
        Assert.Equal(TraceQueryStatus.Found, timeline.Status);
        Assert.Empty(timeline.Timeline!.Segments);
        Assert.Empty(timeline.Timeline.TimeSummary);
        Assert.NotEmpty(timeline.Timeline.Markers);
    }

    [Fact]
    public void Timeline_UnknownRun_Unavailable()
    {
        var observability = new DriverHostObservability();
        var timeline = observability.GetRunTimeline("no-such-run");
        Assert.Equal(TraceQueryStatus.Unavailable, timeline.Status);
        Assert.Null(timeline.Timeline);
    }

    private static DriverHostObservability Observability()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(
            ReadOnlyObservabilityFixtures.RunId,
            ReadOnlyObservabilityFixtures.CompletedTrace(),
            ReadOnlyObservabilityFixtures.CompletedRun());
        return observability;
    }
}