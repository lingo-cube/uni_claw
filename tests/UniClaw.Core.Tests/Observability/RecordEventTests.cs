using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// RecordEventTests — RecordEventAsync extension (trace-span-helpers task 1.3/1.4):
/// event span has EndTime null + DurationMs 0, spanName == spanType, recorded
/// attributes, runtime parent expression, and null-recorder no-op.
/// </summary>
public class RecordEventTests
{
    [Fact(DisplayName = "Event: span has EndTime null and DurationMs 0 with recorded attributes")]
    public async Task EventSpan_UnclosedWithDurationZero()
    {
        var (recorder, service) = NewTrace();

        var parentSpanId = await recorder.StartSpanAsync(SpanTypes.EngineStep, "step 1");
        await recorder.RecordEventAsync(
            SpanTypes.EntryVisited,
            parentSpanId,
            new Dictionary<string, object> { ["entry.name"] = "Settings", ["entry.depth"] = 2 });

        var events = service.GetSpansByType(SpanTypes.EntryVisited);
        var span = Assert.Single(events);
        Assert.Equal(SpanTypes.EntryVisited, span.SpanName); // spanName defaults to spanType
        Assert.Equal(parentSpanId, span.ParentSpanId);
        Assert.Null(span.EndTime);
        Assert.Equal(0, span.DurationMs);
        Assert.Equal("Settings", span.Attributes!["entry.name"]);
        Assert.Equal(2, span.Attributes["entry.depth"]);
    }

    [Fact(DisplayName = "Event: runtime method-call parent expression accepted")]
    public async Task EventSpan_RuntimeParentExpression_Accepted()
    {
        var (recorder, service) = NewTrace();

        await recorder.RecordEventAsync(
            SpanTypes.EntrySkipped,
            LatestVisitedSpanId(service));

        var span = Assert.Single(service.GetSpansByType(SpanTypes.EntrySkipped));
        Assert.Null(span.ParentSpanId); // no visited span recorded yet → null parent

        static string? LatestVisitedSpanId(InMemoryTraceService trace)
        {
            var visited = trace.GetSpansByType(SpanTypes.EntryVisited);
            return visited.Count > 0 ? visited[^1].SpanId : null;
        }
    }

    [Fact(DisplayName = "Event: null recorder is a side-effect-free no-op")]
    public async Task NullRecorder_NoOp()
    {
        ITraceRecorder? nullRecorder = null;

        await nullRecorder.RecordEventAsync(SpanTypes.EntryVisited, attributes: null);
    }

    private static (InMemoryTraceRecorder Recorder, InMemoryTraceService Service) NewTrace()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceRecorder(storage), new InMemoryTraceService(storage));
    }
}
