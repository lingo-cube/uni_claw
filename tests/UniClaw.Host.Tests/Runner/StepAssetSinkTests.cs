using UniClaw.Host.Runner;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

public sealed class StepAssetSinkTests
{
    [Fact]
    public async Task Submit_DoesNotBlockOnSlowWriter_DrainFlushes()
    {
        await using var sink = new StepAssetSink();
        var completed = false;

        // Slow write: submitting must return before the work finishes.
        var accepted = sink.Submit(async ct =>
        {
            await Task.Delay(100, ct);
            completed = true;
        });

        Assert.True(accepted);
        Assert.False(completed);

        await sink.DrainAsync();
        Assert.True(completed);
    }

    [Fact]
    public async Task Drain_FlushesAllAcceptedWritesInOrder()
    {
        await using var sink = new StepAssetSink();
        var order = new List<int>();

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            sink.Submit(_ =>
            {
                order.Add(index);
                return Task.CompletedTask;
            });
        }

        await sink.DrainAsync();

        Assert.Equal([0, 1, 2, 3, 4], order);
        Assert.Equal(5, sink.AcceptedCount);
        Assert.Equal(0, sink.FailedCount);
    }

    [Fact]
    public async Task Submit_AfterDrain_ReturnsFalse()
    {
        await using var sink = new StepAssetSink();
        await sink.DrainAsync();

        Assert.False(sink.Submit(_ => Task.CompletedTask));
    }

    [Fact]
    public async Task Drain_IsIdempotent()
    {
        await using var sink = new StepAssetSink();
        sink.Submit(_ => Task.CompletedTask);

        await sink.DrainAsync();
        await sink.DrainAsync();

        Assert.Equal(1, sink.AcceptedCount);
    }

    [Fact]
    public async Task FailedWrite_IsCounted_AndRecordedInRunDiagnostics()
    {
        await using var sink = new StepAssetSink();
        var failure = new InvalidOperationException("disk full");
        sink.Submit(_ => throw failure);

        // Drain must not throw on writer failure; the error surfaces via counters.
        await sink.DrainAsync();

        Assert.Equal(1, sink.FailedCount);
        Assert.Same(failure, sink.LastError);
    }

    [Fact]
    public async Task CanceledWrite_IsCountedAsFailure_RemainingWritesStillRun()
    {
        await using var sink = new StepAssetSink();
        var order = new List<int>();
        sink.Submit(ct => throw new OperationCanceledException(ct));
        sink.Submit(_ =>
        {
            order.Add(1);
            return Task.CompletedTask;
        });

        await sink.DrainAsync();

        Assert.Equal(1, sink.FailedCount);
        Assert.Equal([1], order);
    }
}
