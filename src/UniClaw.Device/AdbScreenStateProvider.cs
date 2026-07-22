using UniClaw.Core.Traversal;

namespace UniClaw.Device;

/// <summary>
/// AdbScreenStateProvider — IScreenStateProvider implementation using ADB.
/// Reads scroll state from device UI hierarchy (uiautomator dump).
/// Stub: returns default values. Real implementation requires adb + uiautomator.
/// </summary>
public sealed class AdbScreenStateProvider : IScreenStateProvider
{
    /// <inheritdoc />
    public bool HasScroll() => false;

    /// <inheritdoc />
    public double GetScrollProgress() => 0.0;

    /// <inheritdoc />
    public bool IsEndOfList() => true;

    /// <inheritdoc />
    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
}
