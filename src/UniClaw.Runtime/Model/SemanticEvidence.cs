namespace UniClaw.Runtime.Model;

/// <summary>
/// One evidence source's evaluation of a semantic claim. Qualitative, not numeric —
/// follows the existing <see cref="TargetGroundingEvidence"/> (bool? + Reason) pattern
/// but adds Source attribution and explicit Stance.
/// Evidence ≠ Claim: this is a source's stance about a claim, not the claim itself.
/// Claim ≠ Belief: belief is the fusion of multiple evidence stances.
/// Belief ≠ Truth: the external world remains authoritative (I-4).
/// </summary>
public sealed record SemanticEvidence
{
    /// <summary>Which evidence channel produced this stance (e.g. "LOCAL_IDENTITY", "TRANSITION", "TEXT_SEMANTIC").</summary>
    public string Source { get; }

    /// <summary>The semantic claim being evaluated (e.g. "page is WifiSub", "same page as before").</summary>
    public string Claim { get; }

    /// <summary>This source's stance on the claim.</summary>
    public SemanticEvidenceStance Stance { get; }

    /// <summary>Optional observable support, contradiction, or limitation explanation.</summary>
    public string? Reason { get; }

    /// <summary>Creates one qualitative semantic evidence value.</summary>
    /// <param name="source">Which evidence channel produced this stance.</param>
    /// <param name="claim">The semantic claim being evaluated.</param>
    /// <param name="stance">This source's stance on the claim.</param>
    /// <param name="reason">Optional observable support, contradiction, or limitation explanation.</param>
    public SemanticEvidence(string source, string claim, SemanticEvidenceStance stance, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(claim);
        Source = source;
        Claim = claim;
        Stance = stance;
        Reason = reason;
    }
}
