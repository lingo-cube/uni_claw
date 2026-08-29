using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>Reduces admitted typed semantic evidence to generic interaction affordances.</summary>
public static class InteractionAffordanceAnalyzer
{
    /// <summary>Fail-closed compatibility path when no semantic capability is available.</summary>
    public static ImmutableArray<InteractionAffordanceEvidence> Analyze(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return Reduce(observation, observation.AdmittedSemanticEvidence.Evidence);
    }

    public static ImmutableArray<InteractionAffordanceEvidence> Analyze(
        Observation observation, SemanticCapabilityEvaluationBatch? batch)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return Reduce(observation, batch?.Accepted ?? []);
    }

    private static ImmutableArray<InteractionAffordanceEvidence> Reduce(
        Observation observation, IEnumerable<SemanticEvidenceV2Envelope> envelopes)
    {
        var canonical = SourceGroundingNormalizer.Normalize(observation);
        var primary = new Dictionary<string, InteractionAffordanceKind>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var supportingChildren = new HashSet<string>(StringComparer.Ordinal);
        foreach (var envelope in envelopes)
        {
            if (envelope.Provenance.Tier != SemanticSourceTier.Primary) continue;
            if (envelope.Candidate is ContainerRelationCandidateEvidence relation
                && relation.RelationKind == ContainerRelationKind.Child)
            {
                supportingChildren.Add(relation.RelatedOccurrenceId);
                continue;
            }

            var occurrenceId = CandidateOccurrenceId(envelope.Candidate);
            if (string.IsNullOrWhiteSpace(occurrenceId)) continue;
            var occurrence = canonical.FirstOrDefault(c => c.OccurrenceId == occurrenceId && c.EligibleForAuthorization);
            if (occurrence is null || !string.Equals(occurrence.Reference.SourceId, envelope.Provenance.SourceId, StringComparison.Ordinal)
                || envelope.Provenance.Tier != SemanticSourceTier.Primary
                || !string.Equals(occurrence.Reference.Frame, envelope.Provenance.FrameId, StringComparison.Ordinal)) continue;
            if (primary.TryGetValue(occurrenceId, out var prior))
            {
                conflicts.Add(occurrenceId);
                continue;
            }
            primary[occurrenceId] = MapCandidate(envelope.Candidate);
        }
        foreach (var id in conflicts) primary.Remove(id);
        var result = ImmutableArray.CreateBuilder<InteractionAffordanceEvidence>(observation.Elements.Length + observation.StructuredElements.Length);
        for (var index = 0; index < observation.Elements.Length; index++)
        {
            var occurrence = canonical.FirstOrDefault(c => c.Reference.SourceKind == ObservationSourceKind.PrimaryVision && c.Reference.ElementIndex == index);
            var admitted = InteractionAffordanceKind.Unknown;
            var hasAdmitted = occurrence is not null && primary.TryGetValue(occurrence.OccurrenceId, out admitted);
            if (!hasAdmitted && occurrence is not null && supportingChildren.Contains(occurrence.OccurrenceId))
                continue; // relation-only supporting child: not an affordance, not Unknown
            var kind = hasAdmitted
                ? admitted : InteractionAffordanceKind.Unknown;
            if (occurrence is null) continue;
            result.Add(new(occurrence, kind,
                hasAdmitted ? "Accepted primary visual semantic affordance evidence." : "No accepted primary visual semantic affordance evidence was available."));
        }
        for (var index = 0; index < observation.StructuredElements.Length; index++)
        {
            var raw = observation.StructuredElements[index];
            var occurrence = canonical.FirstOrDefault(c => c.Reference.SourceKind == ObservationSourceKind.AuxiliaryStructured && c.Reference.ElementIndex == index);
            if (occurrence is not null) result.Add(new(occurrence, Fallback(raw),
                "Auxiliary structured evidence is not authorization eligible.", raw.ResourceId));
        }
        return result.ToImmutable();
    }

    private static InteractionAffordanceKind Map(ElementAffordanceKind kind) => kind switch
    {
        ElementAffordanceKind.NonInteractive => InteractionAffordanceKind.NonInteractive,
        ElementAffordanceKind.NavigationCandidate => InteractionAffordanceKind.NavigationCandidate,
        ElementAffordanceKind.LocalControl => InteractionAffordanceKind.LocalControl,
        ElementAffordanceKind.ParentReturnControl => InteractionAffordanceKind.ParentReturnControl,
        _ => InteractionAffordanceKind.Unknown,
    };

    private static string? CandidateOccurrenceId(SemanticCandidateEvidence candidate) => candidate switch
    {
        ElementAffordanceCandidateEvidence affordance => affordance.OccurrenceId,
        ContainerRelationCandidateEvidence relation when relation.RelationKind == ContainerRelationKind.ReturnToParent => relation.RelatedOccurrenceId,
        _ => null,
    };

    private static InteractionAffordanceKind MapCandidate(SemanticCandidateEvidence candidate) => candidate switch
    {
        ElementAffordanceCandidateEvidence affordance => Map(affordance.AffordanceKind),
        ContainerRelationCandidateEvidence when candidate is ContainerRelationCandidateEvidence { RelationKind: ContainerRelationKind.ReturnToParent } => InteractionAffordanceKind.ParentReturnControl,
        _ => InteractionAffordanceKind.Unknown,
    };

    /// <summary>
    /// Generic structural classification for raw auxiliary/structured evidence
    /// when no admitted semantic candidate covers the occurrence. This is a
    /// mechanical relevance gate only — it never interprets scenario meaning:
    ///   - non-interactive elements (no clickable/checkable/focusable/switch
    ///     signal) are NON_INTERACTIVE and never block completeness;
    ///   - checkable / switch-family / stable search-role tokens are
    ///     LOCAL_CONTROL;
    ///   - a clickable focusable LinearLayout row carrying text is a
    ///     NAVIGATION_CANDIDATE;
    ///   - any other interactive element is UNKNOWN (fail closed).
    /// The result is always auxiliary-tier and never authorization-eligible.
    /// </summary>
    private static InteractionAffordanceKind Fallback(StructuredElementEvidence raw)
    {
        if (raw.Clickable != true && raw.Checkable != true && raw.Focusable != true
            && !HasSwitchClass(raw.Class))
            return InteractionAffordanceKind.NonInteractive;
        if (raw.Checkable == true || HasSwitchClass(raw.Class))
            return InteractionAffordanceKind.LocalControl;
        if (raw.Clickable == true && HasSearchRole(raw))
            return InteractionAffordanceKind.LocalControl;
        if (raw.Clickable == true && raw.Focusable == true
            && string.Equals(raw.Class, "android.widget.LinearLayout", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(raw.RawText))
            return InteractionAffordanceKind.NavigationCandidate;
        return InteractionAffordanceKind.Unknown;
    }

    /// <summary>
    /// STABLE SEARCH-ROLE STRUCTURED TOKEN detection (role-based; RawText /
    /// package / page are never consulted): the SearchView / SearchBar view
    /// families, or the standard "search_action_bar" resource-id semantic leaf.
    /// Generic clickable ViewGroups without such a token remain UNKNOWN.
    /// </summary>
    private static bool HasSearchRole(StructuredElementEvidence raw)
    {
        if (raw.Class is not null
            && (raw.Class.Contains("SearchView", StringComparison.Ordinal)
                || raw.Class.Contains("SearchBar", StringComparison.Ordinal)))
            return true;
        if (raw.ResourceId is { } resourceId)
        {
            var leaf = resourceId;
            var colon = leaf.LastIndexOf(':');
            var slash = leaf.LastIndexOf('/');
            var cut = Math.Max(colon, slash);
            if (cut >= 0)
                leaf = leaf[(cut + 1)..];
            if (string.Equals(leaf, "search_action_bar", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool HasSwitchClass(string? className)
        => className is not null
            && (className.Contains("Switch", StringComparison.Ordinal)
                || className.Contains("CheckBox", StringComparison.Ordinal));
}
