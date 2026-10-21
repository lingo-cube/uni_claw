using UniClaw.Kernel.Capability;
using System.Diagnostics;

namespace UniClaw.Kernel.Runtime;

/// <summary>Mechanical lifetime of one typed language-handler call. Completion is
/// observed by the driver; this object never writes Run or World state.</summary>
internal sealed class PlanLanguageHandlerCall : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    private Task<Finding?> _completion = Task.FromResult<Finding?>(null);
    private int _closed;
    private long _completedTimestamp;

    internal PlanLanguageHandlerCall(string callId, CancellationTokenSource cancellation)
    {
        CallId = callId;
        _cancellation = cancellation;
    }

    internal string CallId { get; }
    internal Task<Finding?> Completion => _completion;
    internal CancellationToken Token => _cancellation.Token;
    internal bool IsClosed => Volatile.Read(ref _closed) != 0;
    internal long? CompletedTimestamp
    {
        get
        {
            var timestamp = Interlocked.Read(ref _completedTimestamp);
            return timestamp == 0 ? null : timestamp;
        }
    }
    internal string? CancellationFailure { get; private set; }
    internal Exception? Failure { get; private set; }

    internal void Cancel()
    {
        if (IsClosed) return;
        try { _cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { CancellationFailure = ex.GetType().Name; }
    }

    internal void Close() => Interlocked.Exchange(ref _closed, 1);

    internal void Observe(Task<Finding?> completion)
    {
        _completion = ObserveCompletionAsync(completion);
    }

    private async Task<Finding?> ObserveCompletionAsync(Task<Finding?> completion)
    {
        try { return await completion.ConfigureAwait(false); }
        catch (Exception ex)
        {
            Failure = ex;
            return null;
        }
        finally { Interlocked.Exchange(ref _completedTimestamp, Stopwatch.GetTimestamp()); }
    }

    public void Dispose()
    {
        Close();
        _cancellation.Dispose();
    }
}
