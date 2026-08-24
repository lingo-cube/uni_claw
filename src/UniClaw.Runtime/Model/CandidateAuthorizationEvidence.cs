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

    /// <summary>
    /// Authorization kind (EBD). AUTHORIZED_CHILD denotes a recursively
    /// authorized child candidate within the current bounded domain.
    /// AUTHORIZED_BOUNDARY = an authorized crossing out of the current
    /// Runtime-owned foreground (grants the single-Tap-in / single-SystemBack-
    /// return boundary handling ONLY — NEVER authority over the external page's
    /// internal content). Defaults to AuthorizedChild for backward compat.
    /// </summary>
    public AuthorizationKind Kind { get; }

    /// <summary>Create one bounded authorization-evidence result.</summary>
    public CandidateAuthorizationEvidence(bool? authorized, string reason,
        AuthorizationKind kind = AuthorizationKind.AuthorizedChild)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Authorized = authorized;
        Reason = reason;
        Kind = kind;
    }
}

/// <summary>Recursive/traversal authority vs boundary-crossing authority.</summary>
public enum AuthorizationKind
{
    /// <summary>Authorization to traverse a recursive child destination.</summary>
    AuthorizedChild = 0,
    /// <summary>Authorization for a bounded external boundary crossing.</summary>
    AuthorizedBoundary = 1,
}
