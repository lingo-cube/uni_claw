namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Container Identity Candidate Policy (V1 + V2 safety hardening).
///
/// V1 order (unchanged baseline):
///   1. minimum evidence → abstain
///   2. structural type compatibility filter
///   3. top eligible candidate
///   4. previous verified identity conflict rejection (fail-closed)
///   5. per-identity (or single) acceptance threshold
///
/// V2 safety-hardening additions (SEMANTIC_SAFETY_HARDENING_APPLY), both
/// configurable and OFF in the V1 profile:
///   A. Evidence sufficiency (generic vs identity-discriminative evidence) —
///      evaluated against the top eligible identity; insufficient → ABSTAIN.
///   B. Margin-based abstention — top1−top2 ambiguity among eligible candidates;
///      insufficient margin → ABSTAIN.
///
/// The policy never forms Runtime belief and never mutates world state.
/// ABSTAIN is a normal success path.
/// </summary>
public sealed class ContainerIdentityCandidatePolicy : IContainerIdentityCandidatePolicy
{
    private readonly CandidatePolicyOptions _options;

    /// <summary>Creates the policy with the given options (default = V1 baseline).</summary>
    public ContainerIdentityCandidatePolicy(CandidatePolicyOptions? options = null)
    {
        _options = options ?? new CandidatePolicyOptions();
    }

    private sealed record DecisionState(
        SemanticCandidate? Top,
        EvidenceAssessment? Evidence,
        bool MarginInsufficient,
        bool ConflictRejected,
        bool ThresholdRejected,
        int EligibleCount);

    /// <inheritdoc />
    public CandidatePolicyResult Decide(CandidateEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // R4 — minimum evidence abstention (V1).
        if (_options.MinimumEvidenceAbstention && !context.HasAnyEvidence)
        {
            return CandidatePolicyResult.Abstain();
        }

        // R1 — structural type compatibility (enabled by option; the legacy
        // reference profile keeps this off).
        var ranked = context.RankedCandidates;
        var eligibleSource = _options.StructuralCompatibility
            ? ranked.Where(c => PrototypeOf(context, c) is { } prototype
                                && ContextTypes(context).Overlaps(prototype.ElementTypes))
            : ranked;
        var eligible = eligibleSource.ToList();

        var top = eligible.FirstOrDefault();
        if (top is null)
        {
            return CandidatePolicyResult.Abstain();
        }

        // Hardening A — evidence sufficiency for the claimed identity.
        if (_options.EvidenceSufficiency is { Enabled: true } sufficiency)
        {
            var assessment = EvidenceSufficiencyEvaluator.Evaluate(
                context.TextFragments,
                context.StructuralSignals,
                sufficiency,
                top.IdentityCandidate);
            if (!assessment.IsSufficient)
            {
                return CandidatePolicyResult.Abstain();
            }
        }

        // Hardening B — margin-based abstention (top1−top2 ambiguity).
        if (_options.MinimumTop1Top2Margin is { } minimumMargin && eligible.Count >= 2)
        {
            var second = eligible[1];
            var margin = top.SimilarityScore - second.SimilarityScore;
            if (margin < minimumMargin)
            {
                return CandidatePolicyResult.Abstain();
            }
        }

        // R2 — previous verified identity conflict rejection (fail-closed).
        if (_options.PreviousIdentityConflictRejection
            && context.PreviousVerifiedIdentity is not null
            && !string.Equals(top.IdentityCandidate, context.PreviousVerifiedIdentity, StringComparison.Ordinal))
        {
            return CandidatePolicyResult.Abstain();
        }

        // R3 — per-identity (or single) acceptance threshold.
        var threshold = ThresholdFor(top.IdentityCandidate);
        if (top.SimilarityScore < threshold)
        {
            return CandidatePolicyResult.Abstain();
        }

        return CandidatePolicyResult.Accept(top);
    }

    private double ThresholdFor(string identity)
    {
        if (_options.PerIdentityThresholds is { } map
            && map.TryGetValue(identity, out var perIdentity))
        {
            return perIdentity;
        }

        return _options.AcceptanceThreshold;
    }

    private static ContainerIdentityPrototype? PrototypeOf(CandidateEvaluationContext context, SemanticCandidate candidate)
        => context.PrototypesById.TryGetValue(candidate.PatternReference, out var prototype) ? prototype : null;

    private static IEnumerable<string> ContextTypes(CandidateEvaluationContext context)
        => context.ObservedElementTypes;
}

/// <summary>Shared helper for element-type overlap (structural compatibility).</summary>
internal static class ElementTypeOverlap
{
    /// <summary>True when <paramref name="observed"/> and <paramref name="prototype"/> share at least one type.</summary>
    public static bool Overlaps(this IEnumerable<string> observed, System.Collections.Immutable.ImmutableArray<string> prototype)
    {
        foreach (var type in observed)
        {
            if (prototype.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}