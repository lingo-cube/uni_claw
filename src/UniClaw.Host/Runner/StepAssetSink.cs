using System.Threading.Channels;

namespace UniClaw.Host.Runner;

/// <summary>
/// Run-scoped non-blocking writer for step evidence assets. Producers submit
/// write work; a single background writer persists it outside the traversal
/// critical path. Run finalization drains the channel before the result is
/// recorded. The bounded channel applies backpressure instead of dropping,
/// and <see cref="DrainAsync"/> is idempotent so it can be awaited from both
/// the normal path and a finally guard.
/// </summary>
public sealed class StepAssetSink : IAsyncDisposable
{
    private readonly Channel<Func<CancellationToken, Task>> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writer;
    private long _accepted;
    private long _failed;
    private Exception? _lastError;

    public StepAssetSink(int capacity = 256)
    {
        _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });
        _writer = Task.Run(() => WriteLoopAsync(_cts.Token));
    }

    /// <summary>Number of write work items accepted by the sink.</summary>
    public long AcceptedCount => Interlocked.Read(ref _accepted);

    /// <summary>Number of write work items that failed.</summary>
    public long FailedCount => Interlocked.Read(ref _failed);

    /// <summary>Last write failure observed by the background writer, if any.</summary>
    public Exception? LastError => _lastError;

    /// <summary>
    /// Submits evidence write work without awaiting completion. Returns false
    /// when the sink has already been completed (post-drain submission).
    /// </summary>
    public bool Submit(Func<CancellationToken, Task> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (_channel.Writer.TryWrite(write))
        {
            Interlocked.Increment(ref _accepted);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Completes the channel and awaits every accepted write. Idempotent:
    /// a second call returns as soon as the writer task has finished.
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryComplete();
        await _writer.WaitAsync(cancellationToken);
    }

    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var write in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await write(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Run cancellation: the work item itself aborted; remaining
                    // items still get their chance via the loop.
                    Interlocked.Increment(ref _failed);
                }
                catch (Exception ex)
                {
                    _lastError = ex;
                    Interlocked.Increment(ref _failed);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation of the writer loop: DrainAsync with a fresh token
            // still completes the channel and waits out the remaining items.
        }
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
