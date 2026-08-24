using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
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
    /// <summary>Creates an unresolved normalization result with the supplied reason.</summary>
    /// <param name="reason">Diagnostic reason for the unresolved result.</param>
    public static SourceNormalizationResult Unresolved(string reason)
        => new([], [], 1, false);
}

/// <summary>Normalizes accepted viewport observations into source-equivalence evidence.</summary>
public static class SourceEquivalenceNormalizer
{
    /// <summary>Produces deterministic source normalization for accepted observations.</summary>
    /// <param name="acceptedObservations">Accepted observations in run order.</param>
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
            var canonical = affordance.CanonicalOccurrence;
            if (canonical is null) continue;
            var signature = OccurrenceSignature(observation, canonical);
            if (signature is null) continue;
            builder.Add(signature);
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Deterministic occurrence derivation for ONE accepted Observation.
    /// Returns the ordered NAVIGATION_CANDIDATE occurrences with
    /// observation-local identities ("nav:1".."nav:n") and exact structured
    /// signatures. Occurrence identity is observation-local only. Occurrences
    /// of both source tiers are enumerated; callers MUST filter
    /// <see cref="NavigationSourceOccurrence.EligibleForAuthorization"/> before
    /// any authorization-bearing use (auxiliary occurrences are never
    /// authorization-eligible).
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
            var canonical = affordance.CanonicalOccurrence;
            if (canonical is null) continue;
            var signature = OccurrenceSignature(observation, canonical);
            if (signature is null) continue;
            ordinal++;
            builder.Add(new NavigationSourceOccurrence(
                observation.SequenceNumber,
                $"nav:{ordinal}",
                signature,
                ordinal,
                canonical));
        }
        return builder.ToImmutable();
    }

    /// <summary>Derives the equivalence signature for a canonical occurrence from
    /// its own source channel: Vision elements use Text|PerceptionType, auxiliary
    /// structured elements use RawText|Class|ResourceId|ContentDescription.</summary>
    private static string? OccurrenceSignature(Observation observation, CanonicalObservationOccurrence canonical)
    {
        if (canonical.Reference.SourceKind == ObservationSourceKind.PrimaryVision)
        {
            if (canonical.Reference.ElementIndex < observation.Elements.Length)
                return BuildSignature(observation.Elements[canonical.Reference.ElementIndex]);
            return null;
        }
        if (canonical.Reference.ElementIndex < observation.StructuredElements.Length)
            return BuildSignature(observation.StructuredElements[canonical.Reference.ElementIndex]);
        return null;
    }

    /// <summary>
    /// STABLE SOURCE EQUIVALENCE KEY (evidence-contract repair): the identity
    /// key for a primary Vision occurrence is
    ///   Text | PerceptionType.
    /// Bounds / node path / viewport ordinal / destination are never identity.
    /// </summary>
    internal static string BuildSignature(ObservedElement raw) =>
        string.Join("|", raw.Text ?? "", raw.PerceptionType ?? "", "", "");

    /// <summary>
    /// STABLE SOURCE EQUIVALENCE KEY for an auxiliary structured occurrence:
    ///   RawText | Class | ResourceId | ContentDescription.
    /// Bounds / node path / viewport ordinal / destination are never identity.
    /// </summary>
    internal static string BuildSignature(StructuredElementEvidence raw) =>
        string.Join("|", raw.RawText ?? "", raw.Class ?? "", raw.ResourceId ?? "", raw.ContentDescription ?? "");

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
