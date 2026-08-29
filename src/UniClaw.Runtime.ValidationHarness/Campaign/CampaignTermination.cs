using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// Closed set of campaign-loop terminations (Phase 2.6, spec "The loop SHALL
/// terminate on bounded scope exhaustion, an explicitly unsafe remaining
/// frontier, or an evidenced Runtime/Contract gap"). <see cref="BoundedStop"/>
/// is the runner-owned hard safety bound (max rounds reached) — the loop never
/// falls through implicitly.
/// </summary>
public enum CampaignTerminationKind
{
    /// <summary>The planned scope is exhausted: every reachable, safe item has
    /// been traversed and the planner proves boundedness (no remaining unknowns).
    /// A planner decision.</summary>
    BoundedScopeExhaustion = 0,

    /// <summary>The remaining frontier is explicitly unsafe (e.g. the only
    /// remaining nodes are known state-mutating or external-boundary classes);
    /// continuing would cross prohibited effects. A planner decision, carrying
    /// the reason + supporting evidence references.</summary>
    UnsafeRemainingFrontier = 1,

    /// <summary>This round's evidence revealed a gap between Runtime behavior and
    /// the Contract/spec requirements — the loop cannot honestly continue.
    /// Carries the reason + supporting evidence references.</summary>
    EvidencedRuntimeContractGap = 2,

    /// <summary>Runner-owned safety stop: the hard MaxRounds bound was reached
    /// (the planner did not terminate explicitly in time). Recorded with reason —
    /// never an implicit fall-through.</summary>
    BoundedStop = 3,
}

/// <summary>
/// Immutable campaign termination record (Phase 2.6): the kind, a human-readable
/// reason, and the evidence references that support the decision. Every loop
/// exit is recorded as one of these — there is no unrecorded stop path
/// ("Never an implicit fall-through").
/// </summary>
public sealed record CampaignTermination(
    CampaignTerminationKind Kind,
    string Reason,
    ImmutableArray<string> EvidenceRefs)
{
    /// <summary>Bounded scope exhaustion (planner decision).</summary>
    public static CampaignTermination BoundedScopeExhaustion(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new CampaignTermination(CampaignTerminationKind.BoundedScopeExhaustion, reason, ImmutableArray<string>.Empty);
    }

    /// <summary>Explicitly unsafe remaining frontier, with the supporting
    /// evidence references (which nodes are unsafe / why).</summary>
    public static CampaignTermination UnsafeRemainingFrontier(string reason, IEnumerable<string> evidenceRefs)
        => new(CampaignTerminationKind.UnsafeRemainingFrontier, RequireReason(reason), ToEvidence(evidenceRefs));

    /// <summary>Evidenced Runtime/Contract gap, with the supporting evidence
    /// references (the exact offending evidence / first divergence point).</summary>
    public static CampaignTermination EvidencedRuntimeContractGap(string reason, IEnumerable<string> evidenceRefs)
        => new(CampaignTerminationKind.EvidencedRuntimeContractGap, RequireReason(reason), ToEvidence(evidenceRefs));

    /// <summary>Runner-owned bounded safety stop (max rounds reached).</summary>
    public static CampaignTermination BoundedStop(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new CampaignTermination(CampaignTerminationKind.BoundedStop, reason, ImmutableArray<string>.Empty);
    }

    /// <summary>The hard MaxRounds bound stop, reason "max rounds exceeded" —
    /// the runner's closure guarantee: without an explicit termination the loop
    /// still stops at the bound, recorded as a bounded stop.</summary>
    public static CampaignTermination MaxRoundsExceeded(int maxRounds)
        => BoundedStop($"max rounds exceeded (hard bound {maxRounds}); the planner must terminate explicitly before the bound.");

    private static string RequireReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return reason;
    }

    private static ImmutableArray<string> ToEvidence(IEnumerable<string> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        return evidenceRefs.ToImmutableArray();
    }
}