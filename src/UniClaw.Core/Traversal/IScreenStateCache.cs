namespace UniClaw.Core.Traversal;

/// <summary>
/// Run-scoped cache of the freshest pre-action screen state. The before-step
/// evidence hook captures the hierarchy once; page analysis consumes the same
/// result instead of issuing a duplicate ADB refresh (zero-extra-dump hot path).
/// </summary>
public interface IScreenStateCache
{
    /// <summary>
    /// Returns the stored pre-action state when it is still valid.
    /// </summary>
    bool TryGetBefore(out ScreenStateResult? state);
}
