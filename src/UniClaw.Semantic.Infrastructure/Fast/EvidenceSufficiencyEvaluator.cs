using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Deterministic evidence-sufficiency evaluator (mechanism B).
///
/// Counts:
///   TotalEvidenceCount        = text fragments + distinct structural signals
///   NonGenericTextCount       = text fragments NOT in the generic token set
///   DiscriminativeAnchorCount = text fragments that are exclusive anchors of
///                               the identity being claimed (top candidate)
///   StructuralSignalCount     = distinct switch-state signals (device state
///                               evidence); bare element-type labels are generic
///                               and do NOT count as discriminative signal
///
/// Sufficiency rules (all configurable):
///   near-empty (0 text fragments when RequireTextEvidence) → insufficient
///   nonGeneric + anchors below MinNonGenericText           → insufficient
///   anchors + switch signals below MinDiscriminativeSignal → insufficient
///   total evidence score below MinEvidenceScore            → insufficient
///
/// The evaluator is a pure function of the observation fields + options; it
/// never decides identity and never forms evidence.
/// </summary>
public static class EvidenceSufficiencyEvaluator
{
    /// <summary>Evaluates evidence sufficiency for the claimed (top) identity.</summary>
    public static EvidenceAssessment Evaluate(
        IEnumerable<string> textFragments,
        IEnumerable<string> structuralFeatures,
        EvidenceSufficiencyOptions options,
        string? claimedIdentity)
    {
        ArgumentNullException.ThrowIfNull(options);

        var texts = textFragments
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .ToList();

        var structural = structuralFeatures.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var switchSignals = structural.Count(s => s.StartsWith("switch:", StringComparison.OrdinalIgnoreCase));

        if (options.RequireTextEvidence && texts.Count == 0)
        {
            return EvidenceAssessment.NearEmpty();
        }

        var anchors = ImmutableHashSet<string>.Empty;
        if (claimedIdentity is not null
            && options.PerIdentityAnchors.TryGetValue(claimedIdentity, out var anchorsForIdentity))
        {
            anchors = anchorsForIdentity;
        }

        // Effective anchors exclude generic vocabulary: a generic word that also
        // happens to be a title (e.g. "settings") is not identity PROOF by itself.
        var effectiveAnchors = anchors.Where(a => !options.GenericTokens.Contains(a)).ToImmutableHashSet();
        var anchorCount = texts.Count(effectiveAnchors.Contains);
        var genericCount = texts.Count(options.GenericTokens.Contains);
        var nonGenericCount = texts.Count - genericCount;

        // Discriminative signal = exclusive anchors + device-state switch signals.
        var discriminativeSignal = anchorCount + Math.Min(switchSignals, 2);

        // Simple explainable evidence score: non-generic text + 2×structural + 2×anchor bonus.
        var evidenceScore = nonGenericCount + 2 * structural.Count + (anchorCount > 0 ? 2 + anchorCount : 0);

        var total = texts.Count + structural.Count;

        if (nonGenericCount + anchorCount < options.MinNonGenericText)
        {
            return EvidenceAssessment.Insufficient(total, nonGenericCount, anchorCount, switchSignals,
                "generic-only: observed text is generic UI vocabulary");
        }

        if (discriminativeSignal < options.MinDiscriminativeSignal)
        {
            return EvidenceAssessment.Insufficient(total, nonGenericCount, anchorCount, switchSignals,
                "no discriminative evidence: no identity anchor and no switch-state signal");
        }

        if (evidenceScore < options.MinEvidenceScore)
        {
            return EvidenceAssessment.Insufficient(total, nonGenericCount, anchorCount, switchSignals,
                "evidence score below minimum");
        }

        return EvidenceAssessment.Sufficient(total, nonGenericCount, anchorCount, switchSignals);
    }
}