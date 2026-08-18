using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.Agent;

/// <summary>
/// CALLER_SOURCE_PROVENANCE_CONTRACT — Agent-owned grounding validator.
///
/// The Agent is the only authority that accepts a caller branch grounding.
/// A grounding is Valid only when ALL of the following hold for the current
/// run and current Container:
///   1. the referenced Observation belongs to the current run (the accepted
///      observation set is run-local by construction),
///   2. it belongs to the current Container's accepted viewport observations,
///   3. it is an accepted viewport observation (same set membership),
///   4. the referenced occurrence actually exists in that Observation,
///   5. the occurrence is a NAVIGATION_CANDIDATE (LOCAL_CONTROL/UNKNOWN fail),
///   6. the occurrence resolves to a run-local logical source via the current
///      normalization result (and is not already claimed by another branch).
///
/// The caller may only EXPLAIN where a branch points; the caller can never
/// assert equivalence or declare a logical source
/// (<see cref="BranchSourceGroundingEvidence"/> carries no equivalence).
///
/// Identity invariants: occurrence identity is observation-local only.
/// Bounds, node path, viewport index and destination are never logical identity.
/// </summary>
public static class SourceGroundingValidator
{
    public enum SourceGroundingStatus
    {
        Valid,
        Invalid,
        Unresolved,
    }

    /// <summary>
    /// Immutable three-way grounding outcome. <see cref="SourceElementIndex"/>
    /// is the validated element index in the referenced Observation when Valid.
    /// </summary>
    public sealed record SourceGroundingResult(
        SourceGroundingStatus Status,
        string Reason,
        int? SourceElementIndex);

    /// <summary>
    /// Validates one caller branch grounding against the run-local accepted
    /// observation set and the current normalization result.
    /// </summary>
    /// <param name="acceptedObservations">The current Container's accepted
    /// viewport observations (run-local by construction; caller passes
    /// container.ViewportExplorationObservations in production).</param>
    /// <param name="grounding">Caller grounding claim (branch identity +
    /// occurrence reference).</param>
    /// <param name="normalization">Current SourceEquivalenceNormalizer result
    /// for the same accepted observations.</param>
    /// <param name="alreadyClaimedLogicalSources">Logical sources already
    /// claimed by previously validated branches in this run (null = none). A
    /// grounding that resolves into an already-claimed source is rejected
    /// (PROV-10 / PROV-14: no duplicate grounding, caller cannot re-assert).</param>
    public static SourceGroundingResult Validate(
        ImmutableArray<Observation> acceptedObservations,
        BranchSourceGroundingEvidence grounding,
        SourceNormalizationResult normalization,
        ImmutableHashSet<string>? alreadyClaimedLogicalSources = null)
    {
        ArgumentNullException.ThrowIfNull(grounding);
        ArgumentNullException.ThrowIfNull(normalization);

        // Condition 1/2/3: the referenced Observation must be an accepted
        // viewport observation of the current run's current Container.
        var reference = grounding.SourceOccurrenceReference;
        Observation? source = null;
        foreach (var observation in acceptedObservations)
        {
            if (observation.SequenceNumber == reference.ObservationSequence)
            {
                source = observation;
                break;
            }
        }
        if (source is null)
        {
            return new SourceGroundingResult(
                SourceGroundingStatus.Invalid,
                $"Grounding references Observation {reference.ObservationSequence} which is not an accepted viewport observation of the current Container/run.",
                null);
        }

        if (!normalization.IsResolved)
        {
            return new SourceGroundingResult(
                SourceGroundingStatus.Unresolved,
                "Source normalization is unresolved; occurrence cannot be resolved to a logical source.",
                null);
        }

        // Condition 4 + 5: the occurrence must actually exist and be a
        // NAVIGATION_CANDIDATE (derivation only emits NavigationCandidate).
        var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(source);
        NavigationSourceOccurrence? occurrence = null;
        foreach (var candidate in occurrences)
        {
            if (string.Equals(
                    candidate.OccurrenceIdentity,
                    reference.OccurrenceLocalIdentity,
                    StringComparison.Ordinal))
            {
                occurrence = candidate;
                break;
            }
        }
        if (occurrence is null)
        {
            return new SourceGroundingResult(
                SourceGroundingStatus.Invalid,
                $"Grounding references occurrence '{reference.OccurrenceLocalIdentity}' which does not exist as a NAVIGATION_CANDIDATE in Observation {reference.ObservationSequence}.",
                null);
        }

        // Condition 6: the occurrence must resolve to a run-local logical source
        // via the current normalization result.
        var resolved = TryResolveLogicalSource(occurrence, normalization);
        if (resolved is null)
        {
            return new SourceGroundingResult(
                SourceGroundingStatus.Unresolved,
                $"Occurrence '{reference.OccurrenceLocalIdentity}' signature is not unambiguously present in the normalization result.",
                null);
        }

        if (alreadyClaimedLogicalSources is not null
            && alreadyClaimedLogicalSources.Contains(resolved))
        {
            return new SourceGroundingResult(
                SourceGroundingStatus.Invalid,
                $"Grounding resolves to logical source '{resolved}' which is already claimed by another branch; duplicate grounding rejected.",
                null);
        }

        return new SourceGroundingResult(
            SourceGroundingStatus.Valid,
            $"Grounding valid: occurrence '{reference.OccurrenceLocalIdentity}' -> logical source '{resolved}'.",
            occurrence.OrderedPosition >= 0
                ? SourceElementIndexOf(source, occurrence)
                : null);
    }

    /// <summary>
    /// Resolves an occurrence to its run-local logical source label from the
    /// normalization result. Returns null when the signature is absent or
    /// ambiguous (multiple distinct logical sources share the signature).
    /// </summary>
    public static string? TryResolveLogicalSource(
        NavigationSourceOccurrence occurrence,
        SourceNormalizationResult normalization)
    {
        string? match = null;
        foreach (var signature in normalization.UniqueSourceSignatures)
        {
            if (string.Equals(signature, occurrence.StructuredSignature, StringComparison.Ordinal))
            {
                if (match is not null)
                    return null; // ambiguous
                match = signature;
            }
        }
        return match;
    }

    /// <summary>
    /// Legacy-interface resolution helper: maps a caller branch identity to the
    /// unique NAVIGATION_CANDIDATE occurrence in an accepted Observation whose
    /// occurrence resolves to a normalized logical source. Used by the run's
    /// dispatch path when callers still declare (identity, sequence) without an
    /// explicit occurrence reference; returns null when not unique.
    /// </summary>
    public static NavigationSourceOccurrence? TryResolveOccurrenceForBranch(
        Observation observation,
        string branchIdentity,
        SourceNormalizationResult normalization)
    {
        if (!normalization.IsResolved)
            return null;
        NavigationSourceOccurrence? best = null;
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
        {
            if (!string.Equals(occurrence.StructuredSignature, branchIdentity, StringComparison.Ordinal)
                && !occurrence.StructuredSignature.StartsWith(branchIdentity + "|", StringComparison.Ordinal))
            {
                continue;
            }
            if (TryResolveLogicalSource(occurrence, normalization) is null)
                return null; // occurrence not in normalized inventory -> cannot ground
            if (best is not null)
                return null; // ambiguous: multiple occurrences match the branch label
            best = occurrence;
        }
        return best;
    }

    private static int? SourceElementIndexOf(Observation observation, NavigationSourceOccurrence occurrence)
    {
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        int ordinal = 0;
        foreach (var affordance in affordances)
        {
            if (affordance.Classification != InteractionAffordanceKind.NavigationCandidate)
                continue;
            ordinal++;
            if (string.Equals($"nav:{ordinal}", occurrence.OccurrenceIdentity, StringComparison.Ordinal))
                return affordance.SourceElementIndex;
        }
        return null;
    }
}
