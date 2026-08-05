using System.Diagnostics;
using System.Threading.Channels;

namespace UniClaw.Core.Observability;

/// <summary>
/// Run-scoped non-blocking asset submission pipeline. Producers call <see cref="Submit"/>
/// without awaiting; a single background writer batches submissions and persists them
/// outside the critical path. Run finalization calls <see cref="DrainAsync"/> before the
/// result is recorded. The bounded channel drops submissions under saturation (counted in
/// <see cref="PipelineStats.Dropped"/>) instead of applying backpressure to the producer.
/// </summary>
public sealed class TracePipeline : ITracePipeline, IAsyncDisposable
{
    /// <summary>Bounded channel capacity — submissions beyond this are dropped, never blocked.</summary>
    private const int ChannelCapacity = 256;

    /// <summary>Maximum batch size — a batch is flushed once this many items accumulate.</summary>
    private const int BatchCapacity = 64;

    /// <summary>Maximum time a pending batch stays unflushed before it is written.</summary>
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(50);

    private readonly IAssetStore _store;
    private readonly string _runId;
    private readonly IPipelineFailureSink? _failureSink;
    private readonly Channel<AssetSubmission> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writer;

    private long _accepted;
    private long _dropped;
    private long _writeFailures;

    public TracePipeline(IAssetStore store, string runId, IPipelineFailureSink? failureSink = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runId);
        _store = store;
        _runId = runId;
        _failureSink = failureSink;
        _channel = Channel.CreateBounded<AssetSubmission>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });
        _writer = Task.Run(() => WriteLoopAsync(_cts.Token));
    }

    /// <inheritdoc />
    public bool Submit(AssetSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (_channel.Writer.TryWrite(submission))
        {
            Interlocked.Increment(ref _accepted);
            return true;
        }

        // Channel saturated (or already completed): the submission is dropped by design
        // (P2: zero main-path latency) and counted in post-drain stats.
        Interlocked.Increment(ref _dropped);
        return false;
    }

    /// <inheritdoc />
    public async Task DrainAsync(CancellationToken ct = default)
    {
        _channel.Writer.TryComplete();
        await _writer.WaitAsync(ct);
    }

    /// <inheritdoc />
    public PipelineStats Stats =>
        new()
        {
            Accepted = Interlocked.Read(ref _accepted),
            Dropped = Interlocked.Read(ref _dropped),
            WriteFailures = Interlocked.Read(ref _writeFailures),
        };

    /// <summary>
    /// Single-reader writer loop: batch-accumulates submissions and flushes when
    /// <see cref="FlushInterval"/> has elapsed since the last flush or the batch reaches
    /// <see cref="BatchCapacity"/> items. When the channel completes (DrainAsync), the
    /// remaining buffer is flushed before the writer task ends, so every accepted
    /// submission reaches the store.
    /// </summary>
    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        var batch = new List<AssetSubmission>(BatchCapacity);
        var lastFlush = Stopwatch.GetTimestamp();

        try
        {
            while (true)
            {
                // Flush immediately when the interval already elapsed with a pending batch
                // (e.g. the previous flush cycle itself took longer than the interval).
                var elapsed = Stopwatch.GetElapsedTime(lastFlush);
                if (batch.Count > 0 && elapsed >= FlushInterval)
                {
                    await FlushAsync(batch, cancellationToken);
                    lastFlush = Stopwatch.GetTimestamp();
                    continue;
                }

                // Wait for the next submission, but no longer than the remaining flush
                // interval so a small trickle still honours the 50ms rule.
                var waitTime = FlushInterval - elapsed;
                var waitTask = _channel.Reader.WaitToReadAsync(cancellationToken).AsTask();

                if (waitTime > TimeSpan.Zero
                    && await Task.WhenAny(waitTask, Task.Delay(waitTime, cancellationToken)) != waitTask)
                {
                    // No item arrived within the interval — flush the pending batch.
                    if (batch.Count > 0)
                    {
                        await FlushAsync(batch, cancellationToken);
                    }

                    lastFlush = Stopwatch.GetTimestamp();
                    continue;
                }

                if (!await waitTask)
                {
                    // Channel completed and drained — flush the remainder, then exit.
                    if (batch.Count > 0)
                    {
                        await FlushAsync(batch, cancellationToken);
                    }

                    break;
                }

                // Drain everything currently available into the batch.
                while (_channel.Reader.TryRead(out var submission))
                {
                    batch.Add(submission);
                    if (batch.Count >= BatchCapacity)
                    {
                        await FlushAsync(batch, cancellationToken);
                        lastFlush = Stopwatch.GetTimestamp();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Writer loop cancelled (Dispose): items still in the buffer are dropped by
            // design — shutdown is best-effort.
        }
    }

    /// <summary>
    /// Writes every submission in the batch to <see cref="IAssetStore"/>. Failures are
    /// counted in <see cref="PipelineStats.WriteFailures"/>; non-cancellation failures are
    /// additionally reported to the <see cref="IPipelineFailureSink"/>. The loop keeps
    /// going for the remaining items.
    /// </summary>
    private async Task FlushAsync(List<AssetSubmission> batch, CancellationToken cancellationToken)
    {
        foreach (var submission in batch)
        {
            try
            {
                await _store.WriteAsync(_runId, submission.RelativePath, submission.Bytes, cancellationToken, submission.Append);
            }
            catch (OperationCanceledException)
            {
                // The individual write aborted (e.g. run cancellation); count it and
                // continue with the remaining items.
                Interlocked.Increment(ref _writeFailures);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _writeFailures);
                _failureSink?.OnWriteFailed(submission, ex);
            }
        }

        batch.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _writer;
        }
        catch
        {
            // Best-effort shutdown.
        }

        _cts.Dispose();
    }
}
