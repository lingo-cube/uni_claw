using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Agent-run-local deterministic source-occurrence normalizer for accepted
/// same-Container viewport Observations.
///
/// It uses:
/// - InteractionAffordanceAnalyzer to identify NAVIGATION_CANDIDATE occurrences
/// - exact structured signatures
/// - unique ordered overlap between adjacent viewports
///
/// It never uses bounds, node path, or destination as logical identity.
/// </summary>
public sealed record SourceNormalizationResult(
    ImmutableArray<string> UniqueSourceSignatures,
    ImmutableArray<SourceEquivalenceEvidence> EquivalenceEvidence,
    int UnresolvedCount,
    bool IsResolved)
{
    public static SourceNormalizationResult Unresolved(string reason)
        => new([], [], 1, false);
}

public static class SourceEquivalenceNormalizer
{
    public static SourceNormalizationResult Normalize(ImmutableArray<Observation> acceptedObservations)
    {
        if (acceptedObservations.IsDefaultOrEmpty)
            return SourceNormalizationResult.Unresolved("No accepted viewport observations.");

        // Convert each Observation to an ordered list of occurrence signatures.
        var sequences = ImmutableArray.CreateBuilder<ImmutableArray<string>>();
        foreach (var observation in acceptedObservations)
        {
            var signatures = ExtractNavigationSignatures(observation);
            if (signatures.IsDefaultOrEmpty)
                return SourceNormalizationResult.Unresolved(
                    $"Observation {observation.SequenceNumber} has no structured navigation candidates.");
            if (signatures.Distinct(StringComparer.Ordinal).Count() != signatures.Length)
                return SourceNormalizationResult.Unresolved(
                    $"Observation {observation.SequenceNumber} contains duplicate structured navigation signatures; equivalence is ambiguous.");
            sequences.Add(signatures);
        }

        var current = sequences[0];
        var evidence = ImmutableArray.CreateBuilder<SourceEquivalenceEvidence>();
        for (int i = 1; i < sequences.Count; i++)
        {
            var next = sequences[i];
            var overlapLength = FindUniqueSuffixPrefixOverlap(current, next);
            if (overlapLength is null)
            {
                return SourceNormalizationResult.Unresolved(
                    $"Adjacent viewport overlap is ambiguous or absent between sequence {i - 1} and {i}.");
            }

            // Record SAME_SOURCE evidence for each overlapped occurrence.
            for (int k = 0; k < overlapLength.Value; k++)
            {
                var oldId = $"{acceptedObservations[i - 1].SequenceNumber}:{current.Length - overlapLength.Value + k}";
                var newId = $"{acceptedObservations[i].SequenceNumber}:{k}";
                evidence.Add(new SourceEquivalenceEvidence(
                    oldId,
                    newId,
                    SourceEquivalenceKind.SameSource,
                    "Unique ordered overlap of exact structured signatures."));
            }

            // Append only newly appearing sources.
            var combined = ImmutableArray.CreateBuilder<string>();
            combined.AddRange(current);
            for (int k = overlapLength.Value; k < next.Length; k++)
                combined.Add(next[k]);
            current = combined.ToImmutable();
        }

        return new SourceNormalizationResult(current, evidence.ToImmutable(), 0, true);
    }

    private static ImmutableArray<string> ExtractNavigationSignatures(Observation observation)
    {
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var affordance in affordances)
        {
            if (affordance.Classification != InteractionAffordanceKind.NavigationCandidate)
                continue;
            if (affordance.SourceElementIndex < 0
                || affordance.SourceElementIndex >= observation.StructuredElements.Length)
            {
                continue;
            }
            var raw = observation.StructuredElements[affordance.SourceElementIndex];
            builder.Add(BuildSignature(raw));
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Deterministic occurrence derivation for ONE accepted Observation.
    /// Returns the ordered NAVIGATION_CANDIDATE occurrences with
    /// observation-local identities ("nav:1".."nav:n") and exact structured
    /// signatures. Occurrence identity is observation-local only.
    /// </summary>
    public static ImmutableArray<NavigationSourceOccurrence> OccurrencesOf(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        var builder = ImmutableArray.CreateBuilder<NavigationSourceOccurrence>();
        int ordinal = 0;
        foreach (var affordance in affordances)
        {
            if (affordance.Classification != InteractionAffordanceKind.NavigationCandidate)
                continue;
            if (affordance.SourceElementIndex < 0
                || affordance.SourceElementIndex >= observation.StructuredElements.Length)
            {
                continue;
            }
            ordinal++;
            var raw = observation.StructuredElements[affordance.SourceElementIndex];
            builder.Add(new NavigationSourceOccurrence(
                observation.SequenceNumber,
                $"nav:{ordinal}",
                BuildSignature(raw),
                ordinal));
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// STABLE SOURCE EQUIVALENCE KEY (evidence-contract repair): the identity
    /// key for source equivalence is
    ///   TitleText | Class | ResourceId | ContentDescription.
    /// The RAW DESCRIPTIVE / LIVE SummaryText is explicitly EXCLUDED: a live
    /// summary value ("38% used - 9.97 GB free", "Charged") changes between
    /// observations of the SAME logical source and would otherwise break the
    /// unique ordered-overlap equivalence chain. SummaryText remains raw
    /// evidence on StructuredElementEvidence (description / state evidence /
    /// diagnostics) but NEVER creates or disambiguates source identity: two
    /// elements that collide on the stable key remain AMBIGUOUS and fail
    /// closed (summary cannot create identity; summary cannot resolve identity
    /// ambiguity). Bounds / node path / viewport ordinal / destination are
    /// never identity.
    /// </summary>
    internal static string BuildSignature(StructuredElementEvidence raw)
        => string.Join("|",
            raw.TitleText ?? "",
            raw.Class ?? "",
            raw.ResourceId ?? "",
            raw.ContentDescription ?? "");

    /// <summary>
    /// Finds the unique maximal length L such that the suffix of current of
    /// length L exactly equals the prefix of next of length L.
    /// Returns null when zero or multiple overlaps are possible.
    /// </summary>
    private static int? FindUniqueSuffixPrefixOverlap(
        ImmutableArray<string> current,
        ImmutableArray<string> next)
    {
        int? best = null;
        int max = Math.Min(current.Length, next.Length);
        for (int length = max; length >= 1; length--)
        {
            bool match = true;
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(
                        current[current.Length - length + i],
                        next[i],
                        StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                if (best is not null)
                    return null; // ambiguous
                best = length;
            }
        }
        return best;
    }
}
