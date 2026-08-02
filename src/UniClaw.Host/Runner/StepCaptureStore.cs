using UniClaw.Core.Traversal;

namespace UniClaw.Host.Runner;

/// <summary>
/// Run-scoped holder for the freshest pre-action device screen state. The
/// before-step evidence hook captures the hierarchy once; page analysis
/// consumes the same result instead of issuing a duplicate ADB refresh.
/// Invalidated whenever a device-changing action succeeds, so only the
/// pre-action state of the current step is ever reused.
/// </summary>
public sealed class StepCaptureStore : IScreenStateCache
{
    private readonly object _gate = new();
    private ScreenStateResult? _before;
    private bool _valid;

    /// <summary>Stores the pre-action screen state for the current step.</summary>
    public void SetBefore(ScreenStateResult state)
    {
        lock (_gate)
        {
            _before = state ?? throw new ArgumentNullException(nameof(state));
            _valid = true;
        }
    }

    /// <summary>Marks the stored state stale (device-changing action succeeded).</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _valid = false;
        }
    }

    /// <summary>
    /// Returns the stored pre-action state when it is still valid.
    /// </summary>
    public bool TryGetBefore(out ScreenStateResult? state)
    {
        lock (_gate)
        {
            if (_valid && _before is not null)
            {
                state = _before;
                return true;
            }

            state = null;
            return false;
        }
    }
}
