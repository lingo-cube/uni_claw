using UniClaw.Runtime.Model;
using UniClaw.Runtime.Capabilities.Perception.Vision;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// Deterministic mock for ISwitchStateReader component proofs.
/// Returns predetermined values keyed by a label or always-ON/always-OFF/always-null.
///
/// This is NOT a production implementation — it exists for contract validation
/// and Runtime integration falsification.
/// </summary>
public sealed class MockSwitchStateReader : ISwitchStateReader
{
    private readonly bool? _defaultResult;

    public MockSwitchStateReader(bool? defaultResult = null)
    {
        _defaultResult = defaultResult;
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
    public static MockSwitchStateReader AlwaysOn { get; } = new(true);

    /// <summary>Always returns false (visually OFF).</summary>
    public static MockSwitchStateReader AlwaysOff { get; } = new(false);

    /// <summary>Always returns null (UNKNOWN).</summary>
    public static MockSwitchStateReader AlwaysUnknown { get; } = new(null);
}
