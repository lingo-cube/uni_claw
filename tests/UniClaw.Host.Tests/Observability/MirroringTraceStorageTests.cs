using UniClaw.Core.Observability;
using UniClaw.Host.Observability;
using Xunit;

namespace UniClaw.Host.Tests.Observability;

public sealed class MirroringTraceStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"uniclaw-mirrored-trace-{Guid.NewGuid():N}");

    [Fact]
    public async Task Recorder_WritesQueryableMemoryAndDurableJsonl()
    {
        var traceId = $"trace-{Guid.NewGuid():N}";
        var memory = new InMemoryTraceStorage();
        var durable = new FileTraceStorage(new PhysicalFileProvider(), _root);
        var recorder = new InMemoryTraceRecorder(
            new MirroringTraceStorage(memory, durable));

        await recorder.StartSessionAsync(traceId);
        await recorder.RecordExecutionAsync(
            new ExecutionRecord("click", "success"));
        await recorder.EndSessionAsync();

        Assert.Single(memory.GetExecutions());
        Assert.True(File.Exists(Path.Combine(_root, traceId, "trace.jsonl")));
        Assert.Contains(
            "\"record_type\":\"execution\"",
            await File.ReadAllTextAsync(
                Path.Combine(_root, traceId, "trace.jsonl")),
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
