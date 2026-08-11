using UniClaw.Runtime.Capabilities.Perception.Vision;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// Deterministic mock for ISwitchStateReader contract validation.
/// Bound to a specific PerceptionFrame. Returns predetermined values.
/// </summary>
public sealed class MockSwitchStateReader : ISwitchStateReader
{
    private readonly bool? _defaultResult;

    public PerceptionFrame Frame { get; }

    public MockSwitchStateReader(bool? defaultResult = null)
    {
        _defaultResult = defaultResult;
        Frame = new PerceptionFrame();
    }

    public ValueTask<bool?> ReadAsync(
        ElementBounds switchBounds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!switchBounds.IsValid)
            return ValueTask.FromResult<bool?>(null);

        return ValueTask.FromResult(_defaultResult);
    }

    /// <summary>Always returns true (visually ON).</summary>
    public static MockSwitchStateReader AlwaysOn => new(true);

    /// <summary>Always returns false (visually OFF).</summary>
    public static MockSwitchStateReader AlwaysOff => new(false);

    /// <summary>Always returns null (UNKNOWN).</summary>
    public static MockSwitchStateReader AlwaysUnknown => new(null);
}
