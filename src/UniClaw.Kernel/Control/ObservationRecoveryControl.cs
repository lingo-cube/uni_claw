using System.Diagnostics;

namespace UniClaw.Kernel.Control;

/// <summary>Control-owned subquota for read-only supplementation. Neither a new
/// attempt nor a scene change replenishes the run quota or an active deadline.</summary>
internal sealed class ObservationRecoveryControl
{
    private readonly TimeSpan _timeout;
    private readonly int _maxRequests;
    private readonly Func<long> _timestamp;
    private readonly long _frequency;
    private string? _slot;
    private long _started;
    private bool _expired;

    internal ObservationRecoveryControl(TimeSpan timeout, int maxRequests,
        Func<long>? timestamp = null, long? frequency = null)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (maxRequests < 0 || maxRequests > 16)
            throw new ArgumentOutOfRangeException(nameof(maxRequests));
        _timeout = timeout;
        _maxRequests = maxRequests;
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
        _frequency = frequency ?? Stopwatch.Frequency;
    }

    internal int RequestsUsed { get; private set; }
    private TimeSpan Remaining => _timeout - TimeSpan.FromSeconds((double)(_timestamp() - _started) / _frequency);

    internal bool TrySchedule(string slot, out TimeSpan remaining)
    {
        if (_slot is null)
        {
            _slot = slot;
            _started = _timestamp();
        }
        remaining = Remaining;
        if (_slot != slot || _expired || remaining <= TimeSpan.Zero || RequestsUsed >= _maxRequests)
        {
            _expired = true;
            return false;
        }
        RequestsUsed++;
        return true;
    }

    internal bool IsExpired(string slot)
    {
        if (_slot != slot) return false;
        if (Remaining <= TimeSpan.Zero) _expired = true;
        return _expired;
    }

    internal bool CanAccept(string slot) => _slot == slot && !_expired && Remaining > TimeSpan.Zero;

    internal void Complete(string slot)
    {
        if (_slot != slot || _expired) return;
        _slot = null;
    }
}
