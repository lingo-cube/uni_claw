using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

/// <summary>
/// AdbActionExecutor — IActionExecutor implementation using ADB input commands.
/// Stub: throws NotImplementedException. Real implementation requires adb + Android SDK.
/// </summary>
public sealed class AdbActionExecutor : IActionExecutor
{
    /// <inheritdoc />
    public Task<bool> TapAsync(double x, double y, CancellationToken ct = default)
        => throw new NotImplementedException("ADB tap not yet implemented.");

    /// <inheritdoc />
    public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int durationMs, CancellationToken ct = default)
        => throw new NotImplementedException("ADB swipe not yet implemented.");

    /// <inheritdoc />
    public Task<bool> PressBackAsync(CancellationToken ct = default)
        => throw new NotImplementedException("ADB back not yet implemented.");

    /// <inheritdoc />
    public Task<bool> InputTextAsync(string text, CancellationToken ct = default)
        => throw new NotImplementedException("ADB input text not yet implemented.");

    /// <inheritdoc />
    public Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken ct = default)
        => throw new NotImplementedException("ADB long press not yet implemented.");

    /// <inheritdoc />
    public Task WaitAsync(int ms, CancellationToken ct = default)
        => Task.Delay(ms, ct);

    /// <inheritdoc />
    public List<ActionRecord> GetHistory() => new();
}
