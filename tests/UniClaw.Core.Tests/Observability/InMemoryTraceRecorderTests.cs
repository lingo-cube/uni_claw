using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// InMemoryTraceRecorder tests — async wrapper delegates to storage,
/// StartSessionAsync creates session.
/// </summary>
public class InMemoryTraceRecorderTests
{
    [Fact(DisplayName = "Recorder: RecordExecutionAsync delegates to storage")]
    public async Task RecordExecutionAsync_DelegatesToStorage()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var r = new ExecutionRecord("click", "success",
            Context: new TraceContext(NodeId: "wifi_node"));
        await recorder.RecordExecutionAsync(r);

        var executions = storage.GetExecutions();
        Assert.Single(executions);
        Assert.Equal("click", executions[0].Action);
    }

    [Fact(DisplayName = "Recorder: StartSessionAsync creates session via storage")]
    public async Task StartSessionAsync_CreatesSessionViaStorage()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var session = await recorder.StartSessionAsync("abc-123");

        Assert.Equal("abc-123", session.TraceId);
        Assert.Equal("abc-123", storage.CurrentSession?.TraceId);
    }

    [Fact(DisplayName = "Recorder: EndSessionAsync ends session via storage")]
    public async Task EndSessionAsync_EndsSessionViaStorage()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        await recorder.StartSessionAsync("abc-123");
        await recorder.EndSessionAsync();

        Assert.NotNull(storage.CurrentSession?.EndTime);
        Assert.True(storage.CurrentSession?.IsCompleted);
    }

    [Fact(DisplayName = "Recorder: RecordTransitionAsync delegates to storage")]
    public async Task RecordTransitionAsync_DelegatesToStorage()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var t = new StateTransition("NodeSelect", "Execute");
        await recorder.RecordTransitionAsync(t);

        Assert.Single(storage.GetTransitions());
    }

    [Fact(DisplayName = "Recorder: RecordErrorAsync delegates to storage")]
    public async Task RecordErrorAsync_DelegatesToStorage()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var e = new ErrorRecord("type", "msg", ErrorSeverity.Warning);
        await recorder.RecordErrorAsync(e);

        Assert.Single(storage.GetErrors());
    }

    [Fact(DisplayName = "Recorder: RecordPageTransitionAsync delegates to storage")]
    public async Task RecordPageTransitionAsync_DelegatesToStorage()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var pt = new PageTransition("home", "wifi", "forward");
        await recorder.RecordPageTransitionAsync(pt);

        Assert.Single(storage.GetPageTransitions());
    }

    [Fact(DisplayName = "Recorder: RecordAICallAsync delegates to storage")]
    public async Task RecordAICallAsync_DelegatesToStorage()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var ai = new AICallRecord("vision", "provider", true, 230.5);
        await recorder.RecordAICallAsync(ai);

        Assert.Single(storage.GetAICalls());
    }
}
