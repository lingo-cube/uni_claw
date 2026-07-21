using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// C-9: IHandlerTraceWriter + HandlerTraceWriter 单元测试。
/// </summary>
public class HandlerTraceWriterTests
{
    [Fact(DisplayName = "C-9: HandlerTraceWriter 委托 ITraceRecorder.RecordExecutionAsync")]
    public async Task RecordHandlerLifecycleAsync_DelegatesToRecorder()
    {
        var recorder = new SpyTraceRecorder();
        var writer = new HandlerTraceWriter(recorder);

        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        await writer.RecordHandlerLifecycleAsync(
            "handle_test", SpanType.PopupHandling, "success", metadata);

        Assert.NotNull(recorder.LastExecution);
        Assert.Equal("handle_test", recorder.LastExecution.Action);
        Assert.Equal("success", recorder.LastExecution.Status);
        Assert.Equal(SpanType.PopupHandling, recorder.LastExecution.SpanType);
        Assert.Equal("value", recorder.LastExecution.Metadata?["key"]);
    }

    [Fact(DisplayName = "C-9: HandlerTraceWriter null recorder = no-op")]
    public async Task RecordHandlerLifecycleAsync_NullRecorder_NoOp()
    {
        // Should not throw
        var writer = new HandlerTraceWriter(null);
        await writer.RecordHandlerLifecycleAsync("test", SpanType.DfsBacktrack);
    }

    [Fact(DisplayName = "C-9: IHandlerTraceWriter interface has one method")]
    public void IHandlerTraceWriter_HasOneMethod()
    {
        var methods = typeof(IHandlerTraceWriter).GetMethods();
        Assert.Single(methods);
    }

    [Fact(DisplayName = "Phase 3-A: HandlerTraceWriter populates Context on ExecutionRecord")]
    public async Task RecordHandlerLifecycleAsync_WithContext_SetsContext()
    {
        var recorder = new SpyTraceRecorder();
        var writer = new HandlerTraceWriter(recorder);

        var traceCtx = new TraceContext(
            NodeId: "wifi_node",
            StepSpanId: "abc-000005",
            StepNumber: 5,
            TraceId: "abc");

        await writer.RecordHandlerLifecycleAsync(
            "handle_test", SpanType.PopupHandling, "success", context: traceCtx);

        Assert.NotNull(recorder.LastExecution);
        Assert.NotNull(recorder.LastExecution.Context);
        Assert.Equal("wifi_node", recorder.LastExecution.Context.NodeId);
        Assert.Equal("abc-000005", recorder.LastExecution.Context.StepSpanId);
        Assert.Equal(5, recorder.LastExecution.Context.StepNumber);
        Assert.Equal("abc", recorder.LastExecution.Context.TraceId);
    }

    [Fact(DisplayName = "Phase 3-A: HandlerTraceWriter null context = Context null on record")]
    public async Task RecordHandlerLifecycleAsync_NullContext_ContextNull()
    {
        var recorder = new SpyTraceRecorder();
        var writer = new HandlerTraceWriter(recorder);

        await writer.RecordHandlerLifecycleAsync("handle_test", SpanType.PopupHandling, "ok");

        Assert.NotNull(recorder.LastExecution);
        Assert.Null(recorder.LastExecution.Context);
    }

    /// <summary>
    /// SpyTraceRecorder — 捕获最后一条 ExecutionRecord 用于验证。
    /// </summary>
    private sealed class SpyTraceRecorder : ITraceRecorder
    {
        public ExecutionRecord? LastExecution { get; private set; }

        public Task RecordExecutionAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
        {
            LastExecution = record;
            return Task.CompletedTask;
        }

        public Task<TraceSession> StartSessionAsync(string traceId, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TraceSession(traceId, DateTimeOffset.UtcNow));

        public Task EndSessionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordTransitionAsync(StateTransition transition, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordErrorAsync(ErrorRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordPageTransitionAsync(PageTransition transition, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordAICallAsync(AICallRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
