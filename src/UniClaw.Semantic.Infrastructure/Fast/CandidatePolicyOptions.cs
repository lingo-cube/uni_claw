namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Candidate Policy configuration — expresses the EXISTING V1 semantics plus
/// the SEMANTIC_SAFETY_HARDENING_APPLY mechanisms:
///
/// V1 semantics (moved into the policy layer by the responsibility separation gate):
/// - acceptance threshold (per-identity or single),
/// - structural type compatibility,
/// - previous-verified-identity conflict rejection (fail-closed),
/// - minimum-evidence abstention.
///
/// Hardening mechanisms (this gate; both CONFIGURABLE and OFF by default so the
/// legacy/V1 behavior is the untouched baseline):
/// - Margin-based abstention: MinimumTop1Top2Margin — top1/top2 ambiguity.
/// - Evidence sufficiency: min evidence rules + generic-vs-discriminative model.
///
/// Everything is versioned via <see cref="CandidatePolicies"/> and bound
/// by the pipeline profile; rollback = select the V1 profile. No magic numbers
/// in policy code.
/// </summary>
public sealed record CandidatePolicyOptions
{
    /// <summary>Per-identity threshold map (null → single threshold below).</summary>
    public IReadOnlyDictionary<string, double>? PerIdentityThresholds { get; init; }

    /// <summary>Single acceptance threshold used when no per-identity maps exist. Default 0.3.</summary>
    public double AcceptanceThreshold { get; init; } = 0.3;

    /// <summary>Enabled: candidate requires at least one overlapping element type.</summary>
    public bool StructuralCompatibility { get; init; } = true;

    /// <summary>Enabled: when a previous verified identity exists, a conflicting top candidate is rejected (fail-closed).</summary>
    public bool PreviousIdentityConflictRejection { get; init; } = true;

    /// <summary>Enabled: no text + no types + no structural evidence → abstain.</summary>
    public bool MinimumEvidenceAbstention { get; init; } = true;

    /// <summary>
    /// Margin-based abstention (hardening A): minimum acceptable top1−top2
    /// similarity margin among the eligible candidates. Null = disabled (V1).
    /// When fewer than two eligible candidates exist there is no ambiguity and
    /// the margin constraint does not apply.
    /// </summary>
    public double? MinimumTop1Top2Margin { get; init; }

    /// <summary>
    /// Evidence sufficiency (hardening B): generic-vs-discriminative evidence
    /// model. Null = disabled (V1).
    /// </summary>
    public EvidenceSufficiencyOptions? EvidenceSufficiency { get; init; }
}