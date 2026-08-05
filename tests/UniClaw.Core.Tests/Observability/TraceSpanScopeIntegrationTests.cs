using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

public class TraceSpanScopeIntegrationTests
{
    [Fact(DisplayName = "Scope: 调用方 Push 后 scope 内 CurrentSpanId == spanId")]
    public async Task BeginSpanAsync_InsideScope_CurrentSpanIdMatchesSpanId()
    {
        var (recorder, service) = NewTrace();
        var context = EngineStepSpanContext.Instance;

        var scope = await recorder.BeginSpanAsync(SpanTypes.ActionWait, "wait");
        context.Push(scope.SpanId);
        Assert.Equal(scope.SpanId, context.CurrentSpanId);

        // Direct Pop — verify EngineStepSpanContext.Pop works standalone.
        context.Pop();
        Assert.Null(context.CurrentSpanId);

        // No-op: Push not called again, so DisposeAsync Pop is guarded.
        await scope.DisposeAsync();
    }

    [Fact(DisplayName = "Scope: DisposeAsync Pop 恢复父 span — 嵌套 BeginSpanAsync 回退")]
    public async Task Dispose_RestoresParentSpan()
    {
        var (recorder, service) = NewTrace();
        var context = EngineStepSpanContext.Instance;

        context.Push("parent-span");
        var child = await recorder.BeginSpanAsync(SpanTypes.ActionClick, "click");
        context.Push(child.SpanId);
        Assert.Equal(child.SpanId, context.CurrentSpanId);

        // Direct Pop — verify parent restored.
        context.Pop();
        Assert.Equal("parent-span", context.CurrentSpanId);

        await child.DisposeAsync();
        context.Pop(); // pop parent
    }

    [Fact(DisplayName = "Scope: CreateNoOp 不改变当前 span")]
    public async Task CreateNoOp_DoesNotChangeCurrentSpan()
    {
        var context = EngineStepSpanContext.Instance;

        context.Push("original-span");
        var noOp = TraceSpanScope.CreateNoOp();
        Assert.Null(noOp.SpanId);
        Assert.Equal("original-span", context.CurrentSpanId);

        await noOp.DisposeAsync();
        Assert.Equal("original-span", context.CurrentSpanId);

        context.Pop();
    }

    private static (InMemoryTraceRecorder Recorder, InMemoryTraceService Service) NewTrace()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceRecorder(storage), new InMemoryTraceService(storage));
    }
}
