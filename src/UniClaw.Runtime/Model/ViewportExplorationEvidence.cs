namespace UniClaw.Runtime.Model;

/// <summary>
/// SC-P3-CAND-007 bounded same-Container exploration evidence consumed by Agent authority.
/// This value is not a viewport identity, dispatch result, progress counter, or completion judgement.
/// </summary>
public sealed record ViewportExplorationEvidence
{
    /// <summary>true = one further movement is positively justified; false = positively exhausted; null = unresolved.</summary>
    public bool? ContinueExploration { get; }

    /// <summary>Deterministic non-empty explanation of the bounded evidence outcome.</summary>
    public string Reason { get; }

    /// <summary>Create one bounded viewport-exploration evidence result.</summary>
    public ViewportExplorationEvidence(bool? continueExploration, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ContinueExploration = continueExploration;
        Reason = reason;
    }
}
