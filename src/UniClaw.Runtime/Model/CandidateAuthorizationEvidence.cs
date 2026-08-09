namespace UniClaw.Runtime.Model;

/// <summary>
/// SC-P3-CAND-006 bounded pre-dispatch authorization evidence.
/// This immutable value is consumed by Agent semantic authority; it is not an action result,
/// required-work marker, policy rule, persistent status, or completion judgement.
/// </summary>
public sealed record CandidateAuthorizationEvidence
{
    /// <summary>true = authorized; false = positively rejected; null = unresolved.</summary>
    public bool? Authorized { get; }

    /// <summary>Deterministic non-empty explanation of the bounded outcome.</summary>
    public string Reason { get; }

    /// <summary>Create one bounded authorization-evidence result.</summary>
    public CandidateAuthorizationEvidence(bool? authorized, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Authorized = authorized;
        Reason = reason;
    }
}
