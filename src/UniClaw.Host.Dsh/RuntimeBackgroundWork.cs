namespace UniClaw.Host.Dsh;

public sealed record BackgroundWorkDrain(bool Settled, IReadOnlyList<string> PendingRunIds,
    IReadOnlyList<string> FailedRunIds);

/// <summary>Tracks the existing runtime background work through bounded Host shutdown.</summary>
public sealed class RuntimeBackgroundWork
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Task> _work = new(StringComparer.Ordinal);

    public void Track(string runId, Task work)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(work);
        lock (_gate)
        {
            foreach (var id in _work.Where(x => x.Value.IsCompletedSuccessfully).Select(x => x.Key).ToArray())
                _work.Remove(id);
            if (!_work.TryAdd(runId, work)) throw new InvalidOperationException("runtime-background-work-already-tracked");
        }
    }

    public async Task<BackgroundWorkDrain> DrainAsync(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        Task[] tasks;
        lock (_gate) tasks = _work.Values.ToArray();
        try { await Task.WhenAll(tasks).WaitAsync(timeout).ConfigureAwait(false); }
        catch (Exception) { /* Inspect every tracked task below, including partial failures. */ }
        lock (_gate)
        {
            var pending = _work.Where(x => !x.Value.IsCompleted).Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
            var failed = _work.Where(x => x.Value.IsFaulted || x.Value.IsCanceled).Select(x => x.Key).Order(StringComparer.Ordinal).ToArray();
            foreach (var item in _work.Where(x => x.Value.IsCompleted).ToArray())
            {
                if (item.Value.IsFaulted) _ = item.Value.Exception;
                _work.Remove(item.Key);
            }
            return new(pending.Length == 0, pending, failed);
        }
    }
}
