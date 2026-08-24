using System.Reflection;
using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using Xunit;

namespace UniClaw.Runtime.Tests.Observability;

public sealed class TraceSpanReadModelTests
{
    [Fact]
    public void Summary_PreservesSchemaIdentityCountDiagnostics_AndNoOutcome()
    {
        var trace = ReadOnlyObservabilityFixtures.CompletedTrace() with { Diagnostics = ["late listener"] };
        var result = Register(trace).GetTraceSummary(ReadOnlyObservabilityFixtures.RunId);
        Assert.Equal(TraceQueryStatus.Found, result.Status);
        Assert.Equal(1, result.Summary!.SchemaVersion); Assert.Equal(trace.TraceRunId, result.Summary.TraceRunId);
        Assert.Equal(trace.TraceId, result.Summary.TraceId); Assert.Equal(trace.RunId, result.Summary.RunId);
        Assert.Equal(trace.Spans.Length, result.Summary.SpanCount); Assert.Equal(trace.Diagnostics, result.Summary.Diagnostics);
        Assert.DoesNotContain(typeof(TraceRunSummary).GetProperties(BindingFlags.Public | BindingFlags.Instance), p => p.Name is "Outcome" or "Result");
    }

    [Fact]
    public void Summary_UnknownAndZeroSpanAreUnavailable()
    { var o = new DriverHostObservability(); Assert.Equal(TraceQueryStatus.Unavailable, o.GetTraceSummary("unknown").Status); o.RegisterRun("empty", ReadOnlyObservabilityFixtures.EmptyTrace(), ReadOnlyObservabilityFixtures.CompletedRun()); Assert.Equal(TraceQueryStatus.Unavailable, o.GetTraceSummary("empty").Status); }

    [Fact]
    public void FinalizedZeroSpanTraceIsAvailableAfterReplacement()
    { var o = new DriverHostObservability(); var trace = ReadOnlyObservabilityFixtures.EmptyTrace() with { Diagnostics = ["listener failed"] }; o.RegisterRun("empty-final", trace, ReadOnlyObservabilityFixtures.CompletedRun()); o.ReplaceRunProjection("empty-final", trace, ReadOnlyObservabilityFixtures.CompletedRun()); var summary = o.GetTraceSummary("empty-final"); Assert.Equal(TraceQueryStatus.Found, summary.Status); Assert.Equal(0, summary.Summary!.SpanCount); Assert.Equal(trace.Diagnostics, summary.Summary.Diagnostics); var page = o.GetTraceSpans("empty-final"); Assert.Equal(TraceQueryStatus.Found, page.Status); Assert.Empty(page.Spans); Assert.False(page.HasMore); Assert.Null(page.NextCursor); }

    [Fact]
    public void Summary_InvalidSchemaAndIdentitiesFailClosed()
    { var o = new DriverHostObservability(); o.RegisterRun("bad", ReadOnlyObservabilityFixtures.CompletedTrace() with { SchemaVersion = 2 }, ReadOnlyObservabilityFixtures.CompletedRun()); o.RegisterRun("blank", ReadOnlyObservabilityFixtures.CompletedTrace() with { TraceRunId = " " }, ReadOnlyObservabilityFixtures.CompletedRun()); Assert.Equal(TraceQueryStatus.InvalidRequest, o.GetTraceSummary("bad").Status); Assert.Equal(TraceQueryStatus.InvalidRequest, o.GetTraceSummary("blank").Status); Assert.Equal(TraceQueryStatus.InvalidRequest, o.GetTraceSummary(" ").Status); Assert.Equal(TraceQueryStatus.InvalidRequest, o.GetTraceSpans(" ").Status); }

    [Fact]
    public void EqualStartOffsetsAreOrderedByOrdinalSpanId()
    { var t = Custom(new TraceSpan { SpanId = "z", Name = "RefreshSnapshot", Layer = "CONTAINER", Component = "container.refresh", StartOffsetNs = 4 }, new TraceSpan { SpanId = "a", Name = "RefreshSnapshot", Layer = "CONTAINER", Component = "container.refresh", StartOffsetNs = 4 }); Assert.Equal(["a", "z"], Register(t).GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 10).Spans.Select(x => x.Span.SpanId)); }

    [Fact]
    public void PageSizeOneExhaustsWithoutDuplicatesAndPageSizeTwoHasMore()
    { var o = Register(ReadOnlyObservabilityFixtures.CompletedTrace()); var ids = new List<string>(); TraceSpanCursor? c = null; do { var p = o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c); Assert.Equal(TraceQueryStatus.Found, p.Status); ids.AddRange(p.Spans.Select(x => x.Span.SpanId)); c = p.NextCursor; if (!p.HasMore) Assert.Null(p.NextCursor); } while (c is not null); Assert.Equal(["s1", "s2", "s3"], ids); Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count()); Assert.True(o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 2).HasMore); }

    [Fact]
    public void RepeatingCursorReturnsSamePageWithoutChangingSource()
    { var t = ReadOnlyObservabilityFixtures.CompletedTrace(); var o = Register(t); var c = o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1).NextCursor; var a = o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c); var b = o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c); Assert.Equal(a.Status, b.Status); Assert.Equal(a.Spans.Select(x => x.Span.SpanId), b.Spans.Select(x => x.Span.SpanId)); Assert.Equal(a.NextCursor, b.NextCursor); Assert.Equal(a.HasMore, b.HasMore); Assert.Equal(t.Spans, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 10).Spans.Select(x => x.Span).ToImmutableArray()); }

    [Fact]
    public void FiltersAreExactConjunctiveAndKeepSequence()
    { var t = Custom(new TraceSpan { SpanId = "root", Name = "RefreshSnapshot", Layer = "CONTAINER", Component = "container.refresh" }, new TraceSpan { SpanId = "failed", ParentSpanId = "root", Name = "ExecuteAsync", Layer = "ENVIRONMENT", Component = "environment.execute", Outcome = "FAILED", StartOffsetNs = 1 }, new TraceSpan { SpanId = "other", ParentSpanId = "root", Name = "ExecuteAsync", Layer = "ENVIRONMENT", Component = "environment.execute", StartOffsetNs = 2 }); var x = Register(t).GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 10, filter: new TraceSpanFilter("ExecuteAsync", "ENVIRONMENT", "environment.execute", "FAILED", "root")); Assert.Equal("failed", x.Spans.Single().Span.SpanId); Assert.Equal(2, x.Spans.Single().Sequence); }

    [Theory]
    [InlineData("Name", "bogus")]
    [InlineData("Layer", "bogus")]
    [InlineData("Component", "bogus")]
    [InlineData("Outcome", "bogus")]
    [InlineData("ParentSpanId", " ")]
    public void UnsupportedOrBlankFilterValuesAreInvalid(string kind, string value)
    { var f = kind switch { "Name" => new TraceSpanFilter(Name: value), "Layer" => new TraceSpanFilter(Layer: value), "Component" => new TraceSpanFilter(Component: value), "Outcome" => new TraceSpanFilter(Outcome: value), _ => new TraceSpanFilter(ParentSpanId: value) }; Assert.Equal(TraceQueryStatus.InvalidRequest, Register(ReadOnlyObservabilityFixtures.CompletedTrace()).GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 10, filter: f).Status); }

    [Theory][InlineData(0)][InlineData(257)] public void PageSizeBoundsAreInvalid(int size) => Assert.Equal(TraceQueryStatus.InvalidRequest, Register(ReadOnlyObservabilityFixtures.CompletedTrace()).GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, size).Status);

    [Fact]
    public void CursorMismatchCoversRunTraceFilterAndBounds()
    { var o = Register(ReadOnlyObservabilityFixtures.CompletedTrace()); var c = o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1).NextCursor!; Assert.Equal(TraceQueryStatus.CursorMismatch, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c with { RunId = "other" }).Status); Assert.Equal(TraceQueryStatus.CursorMismatch, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c with { TraceRunId = "other" }).Status); Assert.Equal(TraceQueryStatus.CursorMismatch, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c with { LastSequence = -1 }).Status); Assert.Equal(TraceQueryStatus.CursorMismatch, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c with { LastSequence = 4 }).Status); Assert.Equal(TraceQueryStatus.Found, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c with { LastSequence = 0 }).Status); Assert.Equal(TraceQueryStatus.CursorMismatch, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c, new TraceSpanFilter(Name: "RefreshSnapshot")).Status); }

    [Fact]
    public void ReplacingTraceInvalidatesOldCursor()
    { var o = Register(ReadOnlyObservabilityFixtures.CompletedTrace()); var c = o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1).NextCursor!; o.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace() with { TraceRunId = "replacement" }, ReadOnlyObservabilityFixtures.CompletedRun()); Assert.Equal(TraceQueryStatus.CursorMismatch, o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1, c).Status); }

    [Fact]
    public void FailedSpanPayloadIsReturnedVerbatimWithoutRuntimeResult()
    { var s = new TraceSpan { SpanId = "failed", Name = "ExecuteAsync", Layer = "ENVIRONMENT", Component = "environment.execute", Outcome = "FAILED", DurationNs = 42, Attributes = [new TraceSpanAttribute { Key = "reason", Value = "timeout" }], Events = [new ObservabilityEvent { EventId = "e1", SpanId = "failed", TimestampOffsetNs = 7 }] }; var r = Register(Custom(s)).GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 10).Spans.Single().Span; Assert.Equal("FAILED", r.Outcome); Assert.Null(typeof(TraceSpanEnvelope).GetProperty("Result")); Assert.Equal(s.Attributes, r.Attributes); Assert.Equal(s.Events, r.Events); Assert.Equal(s.DurationNs, r.DurationNs); }

    [Fact]
    public void QueriesDoNotChangeRunsSnapshotsOrEvents()
    { var o = Register(ReadOnlyObservabilityFixtures.CompletedTrace()); var ids = o.RegisteredRunIds; var snapshot = o.GetRunSnapshot(ReadOnlyObservabilityFixtures.RunId); var events = o.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId).Events; _ = o.GetTraceSummary(ReadOnlyObservabilityFixtures.RunId); _ = o.GetTraceSpans(ReadOnlyObservabilityFixtures.RunId, 1); Assert.Equal(ids, o.RegisteredRunIds); Assert.Equal(snapshot, o.GetRunSnapshot(ReadOnlyObservabilityFixtures.RunId)); Assert.Equal(events, o.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId).Events); }

    private static DriverHostObservability Register(TraceRun t) { var o = new DriverHostObservability(); o.RegisterRun(ReadOnlyObservabilityFixtures.RunId, t, ReadOnlyObservabilityFixtures.CompletedRun()); return o; }
    private static TraceRun Custom(params TraceSpan[] spans) => new() { TraceRunId = "trace-custom", TraceId = ReadOnlyObservabilityFixtures.TraceId, RunId = ReadOnlyObservabilityFixtures.RunId, Spans = spans.ToImmutableArray() };
}
