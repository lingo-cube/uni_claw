namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Policy outcome: an accepted candidate, or abstain. ABSTAIN is a normal,
/// first-class success path of Semantic Perception — the pipeline legitimately
/// produces no evidence when the evidence is not reliable enough.
/// </summary>
public sealed record CandidatePolicyResult
{
    /// <summary>Accepted candidate, or null when abstaining.</summary>
    public SemanticCandidate? AcceptedCandidate { get; }

    /// <summary>True when the policy abstains (no evidence should be formed).</summary>
    public bool IsAbstain { get; }

    private CandidatePolicyResult(SemanticCandidate? accepted, bool abstain)
    {
        AcceptedCandidate = accepted;
        IsAbstain = abstain;
    }

    /// <summary>Accepts a candidate.</summary>
    public static CandidatePolicyResult Accept(SemanticCandidate candidate) => new(candidate, false);

    /// <summary>Abstains (no claim).</summary>
    public static CandidatePolicyResult Abstain() => new(null, true);
}