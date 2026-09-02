using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// V1 deterministic reference matcher (legacy InMemory algorithm, re-homed).
///
/// It is the honest home of the legacy overlap scoring used by the previous
/// <c>InMemoryVectorSemanticIndex</c>: deterministic text/type/structural overlap
/// similarity. It is NOT a vector index (no vectors, no distance metric), so it
/// is classified as a deterministic reference matcher and used for the V1
/// pipeline path and for benchmark comparison.
///
/// Roles: retrieval only. It computes similarity and ranks; it does NOT accept,
/// does NOT threshold, does NOT form evidence. Prototypes are read from an
/// <see cref="IContainerIdentityPrototypeStore"/> — they are never owned here.
/// </summary>
public sealed class DeterministicSemanticMatcher
{
    /// <summary>
    /// Ranks all prototypes against a query by the legacy overlap score
    /// (matches / total query features, identical arithmetic to the legacy
    /// matcher), aggregated to IDENTITY level: for each identity the max score
    /// over its state prototypes wins (identity-max aggregation, Profile V3).
    /// With single-prototype stores this is byte-identical to per-prototype
    /// ranking (compatibility preserved). Scores &gt; 0 are returned, best first;
    /// no threshold is applied.
    /// </summary>
    public IReadOnlyList<SemanticCandidate> Match(
        ContainerSemanticQuery query,
        IContainerIdentityPrototypeStore prototypes)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(prototypes);

        var bestByIdentity = new Dictionary<string, SemanticCandidate>(StringComparer.Ordinal);
        foreach (var prototype in prototypes.All())
        {
            var score = Score(query, prototype);
            if (score <= 0d)
            {
                continue;
            }

            var candidate = new SemanticCandidate(
                prototype.IdentityCandidate,
                score,
                prototype.PrototypeId);
            if (!bestByIdentity.TryGetValue(prototype.IdentityCandidate, out var best)
                || score > best.SimilarityScore)
            {
                bestByIdentity[prototype.IdentityCandidate] = candidate;
            }
        }

        return bestByIdentity.Values
            .OrderByDescending(c => c.SimilarityScore)
            .ToImmutableArray();
    }

    private static double Score(ContainerSemanticQuery query, ContainerIdentityPrototype prototype)
    {
        var total = query.TextFragments.Length
                    + query.ElementTypes.Length
                    + query.StructuralFeatures.Length;
        if (total == 0)
        {
            return 0d;
        }

        var matches = 0;
        foreach (var text in query.TextFragments)
        {
            if (prototype.TextFragments.Contains(text, StringComparer.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        foreach (var type in query.ElementTypes)
        {
            if (prototype.ElementTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        foreach (var feature in query.StructuralFeatures)
        {
            if (prototype.StructuralFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        return (double)matches / total;
    }
}