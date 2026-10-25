using System.Threading.Channels;

namespace UniClaw.Kernel.Runtime;

/// <summary>Limits for optional observation callbacks, never a business/storage commit timeout.</summary>
public sealed record DiagnosticObservationOptions
{
    public int Capacity { get; init; } = 32;
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(3);

    internal void Validate()
    {
        if (Capacity < 1 || Capacity > 4096) throw new ArgumentOutOfRangeException(nameof(Capacity));
        if (OperationTimeout <= TimeSpan.Zero || OperationTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(OperationTimeout));
        if (CloseTimeout <= TimeSpan.Zero || CloseTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(CloseTimeout));
    }
}

/// <summary>Non-authoritative capture/close result. Pending/unsettled work is never a successful flush.</summary>
public sealed record DiagnosticObservationStatus(int Accepted, int Completed, int Failed, int Dropped,
    int TimedOut, int Pending, bool Closed, bool Unsettled, string? LastError);

/// <summary>One bounded, caller-closed consumer per driver; no task/thread per record.</summary>
internal sealed class DiagnosticObservationQueue
{
    private readonly DiagnosticObservationOptions _options;
    private readonly Channel<Func<CancellationToken, ValueTask>> _channel;
    private readonly object _sync = new();
    private Task? _worker;
    private Task? _unsettled;
    private Task? _closeCancellation;
    private CancellationTokenSource? _activeCancellation;
    private bool _closed, _abandoned;
    private int _accepted, _completed, _failed, _dropped, _discarded, _timedOut;
    private string? _lastError;

    internal DiagnosticObservationQueue(DiagnosticObservationOptions options)
    {
        options.Validate();
        _options = options;
        _channel = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(new BoundedChannelOptions(options.Capacity)
        { SingleReader = false, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
    }

    internal DiagnosticObservationStatus Status
    {
        get
        {
            lock (_sync) return new(_accepted, _completed, _failed, _dropped, _timedOut,
                Math.Max(0, _accepted - _completed - _failed - _discarded), _closed,
                _unsettled is { IsCompleted: false } || _closeCancellation is { IsCompleted: false }
                    || (_abandoned && _worker is { IsCompleted: false }), _lastError);
        }
    }

    internal void TryObserve(Func<CancellationToken, ValueTask> observation)
    {
        lock (_sync)
        {
            if (_closed || !_channel.Writer.TryWrite(observation)) { _dropped++; return; }
            _accepted++;
            // Offload invocation too: a legacy synchronous observer cannot occupy the Driver thread.
            _worker ??= Task.Run(ConsumeAsync);
        }
    }

    internal async ValueTask<DiagnosticObservationStatus> CompleteAsync()
    {
        Task? worker;
        lock (_sync)
        {
            _closed = true;
            _channel.Writer.TryComplete();
            worker = _worker;
        }
        if (worker is not null)
        {
            try { await worker.WaitAsync(_options.CloseTimeout).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                CancellationTokenSource? cancellation;
                lock (_sync)
                {
                    if (!_abandoned) { _timedOut++; _lastError = "observer-close-timeout"; }
                    _abandoned = true;
                    DiscardPending();
                    cancellation = _activeCancellation;
                }
                // Do not execute arbitrary cancellation registrations on the closing caller.
                if (cancellation is not null)
                {
                    var pendingCancellation = CancelSafelyAsync(cancellation);
                    lock (_sync) _closeCancellation = pendingCancellation;
                }
            }
        }
        return Status;
    }

    private async Task ConsumeAsync()
    {
        await foreach (var observation in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            CancellationTokenSource cancellation;
            lock (_sync)
            {
                if (_abandoned) { _dropped++; _discarded++; continue; }
                cancellation = _activeCancellation = new CancellationTokenSource();
            }
            Task? operation = null;
            var disposeHere = true;
            try
            {
                operation = observation(cancellation.Token).AsTask();
                await operation.WaitAsync(_options.OperationTimeout).ConfigureAwait(false);
                lock (_sync) _completed++;
            }
            catch (TimeoutException) when (operation is not null)
            {
                lock (_sync)
                {
                    _failed++; _timedOut++; _lastError = "observer-operation-timeout";
                    _abandoned = _closed = true;
                    _channel.Writer.TryComplete();
                    DiscardPending();
                }
                // Keep ownership and observe eventual failure; at most one non-cooperative
                // callback remains, and no more callbacks are admitted after this timeout.
                disposeHere = false;
                var settling = SettleAsync(operation!, cancellation);
                lock (_sync) _unsettled = settling;
                return;
            }
            catch (Exception error)
            {
                lock (_sync) { _failed++; _lastError = error.GetType().Name; }
            }
            finally
            {
                lock (_sync) _activeCancellation = null;
                if (disposeHere) cancellation.Dispose();
            }
        }
    }

    private static async Task SettleAsync(Task operation, CancellationTokenSource cancellation)
    {
        try
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            await operation.ConfigureAwait(false);
        }
        catch (Exception) { /* Already reported as failed; observe eventual callback failure. */ }
        finally { cancellation.Dispose(); }
    }

    private static async Task CancelSafelyAsync(CancellationTokenSource cancellation)
    {
        try { await cancellation.CancelAsync().ConfigureAwait(false); }
        catch (Exception) { /* Closing diagnostics cannot fail the original run. */ }
    }

    private void DiscardPending()
    {
        while (_channel.Reader.TryRead(out _)) { _dropped++; _discarded++; }
    }
}
