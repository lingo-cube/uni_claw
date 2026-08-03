using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// TraceSpanScopeTests — TraceSpanScope + BeginSpanAsync extension
/// (trace-span-helpers tasks 1.1/1.2/1.4):
/// dispose auto-end "ok", explicit End with status/final attrs, double-end no-op,
/// null-recorder no-op scope, runtime spanType and runtime (method-call) parent.
/// </summary>
public class TraceSpanScopeTests
{
    [Fact(DisplayName = "Scope: dispose auto-ends the span with status ok")]
    public async Task Dispose_AutoEndsWithOk()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(SpanTypes.ActionWait, "wait"))
        {
            Assert.NotNull(scope.SpanId);
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal(SpanTypes.ActionWait, span.SpanType);
        Assert.Equal("wait", span.SpanName);
        Assert.NotNull(span.EndTime);
        Assert.Equal("ok", span.Status);
    }

    [Fact(DisplayName = "Scope: explicit End records status and merges final attributes")]
    public async Task ExplicitEnd_RecordsStatusAndFinalAttributes()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         SpanTypes.ActionClick,
                         "click",
                         attributes: new Dictionary<string, object> { ["action.type"] = "click" }))
        {
            await scope.End(
                "error",
                new Dictionary<string, object> { ["action.result"] = false });
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal("error", span.Status);
        Assert.Equal("click", span.Attributes!["action.type"]);
        Assert.Equal(false, span.Attributes["action.result"]);
    }

    [Fact(DisplayName = "Scope: double-end (explicit End then dispose) is a no-op")]
    public async Task DoubleEnd_IsNoOp()
    {
        var (recorder, service) = NewTrace();

        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(SpanTypes.ActionWait, "wait"))
        {
            await scope.End("ok", new Dictionary<string, object> { ["action.result"] = true });
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal("ok", span.Status);
        Assert.Equal(true, span.Attributes!["action.result"]);
    }

    [Fact(DisplayName = "Scope: null recorder yields a side-effect-free no-op scope")]
    public async Task NullRecorder_NoOpScope()
    {
        ITraceRecorder? nullRecorder = null;

        await using var scope = await nullRecorder.BeginSpanAsync(SpanTypes.ActionWait, "wait");

        Assert.Null(scope.SpanId);
        await scope.End("error", new Dictionary<string, object> { ["k"] = 1 });
    }

    [Fact(DisplayName = "Scope: runtime spanType and runtime method-call parent accepted")]
    public async Task RuntimeSpanType_AndRuntimeParent_Accepted()
    {
        var (recorder, service) = NewTrace();

        await recorder.RecordEventAsync(SpanTypes.EntryVisited);
        var parentSpanId = LatestVisitedSpanId(service);
        var runtimeSpanType = SpanTypes.ActionScroll;
        TraceSpanScope scope;
        await using (scope = await recorder.BeginSpanAsync(
                         runtimeSpanType,
                         parentSpanId: LatestVisitedSpanId(service)))
        {
        }

        var span = service.GetSpan(scope.SpanId);
        Assert.NotNull(span);
        Assert.Equal(SpanTypes.ActionScroll, span.SpanType);
        Assert.Equal(parentSpanId, span.ParentSpanId);

        static string? LatestVisitedSpanId(InMemoryTraceService trace)
        {
            var visited = trace.GetSpansByType(SpanTypes.EntryVisited);
            return visited.Count > 0 ? visited[^1].SpanId : null;
        }
    }

    private static (InMemoryTraceRecorder Recorder, InMemoryTraceService Service) NewTrace()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceRecorder(storage), new InMemoryTraceService(storage));
    }
}
