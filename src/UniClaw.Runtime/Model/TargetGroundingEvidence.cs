namespace UniClaw.Runtime.Model;

/// <summary>
/// CP12 local target-hypothesis evidence. It is qualitative evidence only: it is neither
/// GoalEvidence, world truth, stable identity, nor completion evidence.
/// </summary>
public sealed record TargetGroundingEvidence
{
    /// <summary>true = supported/confirmed; false = inconsistent/rejected; null = insufficient/unconfirmed.</summary>
    public bool? Supported { get; }

    /// <summary>Deterministic, non-empty observable support, contradiction, or limitation explanation.</summary>
    public string Reason { get; }

    /// <summary>Creates one qualitative target-grounding evidence value.</summary>
    public TargetGroundingEvidence(bool? supported, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Supported = supported;
        Reason = reason;
    }
}
