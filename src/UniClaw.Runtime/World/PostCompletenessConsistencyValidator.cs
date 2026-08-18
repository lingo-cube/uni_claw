using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// POST-COMPLETENESS CONSISTENCY — the non-monotonic evidence extension.
///
/// After a Container's DISCOVERY EPOCH is frozen (first forward exploration ->
/// forward normalization -> unique logical-source inventory -> positive
/// exhaustion -> <see cref="ContainerInventoryCompletenessEvidence"/>), later
/// same-Container fresh evidence (ScrollBackward revisit, parent return,
/// any fresh Observation) is validated ONLY for consistency against the proven
/// inventory. This validator NEVER re-normalizes the discovery history — the
/// forward ordered-overlap normalizer consumes only the frozen epoch.
///
/// A fresh Observation is CONSISTENT iff:
///   1. same-Container continuity holds (verdict supplied by the Agent),
///   2. it contains zero UNRESOLVED interactive UNKNOWN affordances (an
///      occurrence the Agent explicitly resolved contextually — a
///      PARENT_RETURN_CONTROL — is a RESOLVED_NON_INVENTORY_CONTROL and is not
///      an Unknown obligation),
///   3. every fresh NAVIGATION_CANDIDATE occurrence uniquely re-establishes
///      EXACTLY ONE frozen logical source class (evidence-backed resolution:
///      the class was built from the forward equivalence evidence; the
///      occurrence's signature is the resolution KEY into those classes, never
///      the identity),
///   4. no fresh occurrence introduces a NEW logical source,
///   5. no fresh occurrence maps ambiguously (never guess by signature),
///   6. LOCAL_CONTROL elements may be present; they produce no occurrence and
///      never enter the child inventory.
///
/// Any violation INVALIDATES completeness (fail closed). The fresh view may
/// contain only a SUBSET of the proven sources — the current viewport is not
/// required to contain the full inventory.
/// </summary>
public static class PostCompletenessConsistencyValidator
{
    public sealed record ConsistencyResult(bool Consistent, string Reason)
    {
        public static ConsistencyResult ConsistentOk(string reason) => new(true, reason);
        public static ConsistencyResult Invalidated(string reason) => new(false, reason);
    }

    private enum ResolutionKind
    {
        NoClass,
        Unique,
        Ambiguous,
    }

    /// <summary>
    /// Builds the FROZEN logical source classes from the discovery epoch. Each
    /// unique forward-normalized signature becomes one class carrying the
    /// discovery-epoch occurrence references that the forward equivalence merged
    /// into it (provenance). Called exactly once per Container, at completeness.
    /// </summary>
    public static ImmutableArray<ProvenLogicalSource> BuildFrozenSources(
        ImmutableArray<Observation> discoveryObservations,
        SourceNormalizationResult normalization)
    {
        ArgumentNullException.ThrowIfNull(normalization);
        if (!normalization.IsResolved)
            return [];

        var builder = ImmutableArray.CreateBuilder<ProvenLogicalSource>();
        foreach (var signature in normalization.UniqueSourceSignatures)
        {
            var occurrences = ImmutableArray.CreateBuilder<ProvenSourceOccurrence>();
            foreach (var observation in discoveryObservations)
            {
                foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
                {
                    if (string.Equals(occurrence.StructuredSignature, signature, StringComparison.Ordinal))
                    {
                        occurrences.Add(new ProvenSourceOccurrence(
                            occurrence.ObservationSequence, occurrence.OccurrenceIdentity));
                    }
                }
            }
            builder.Add(new ProvenLogicalSource(signature, occurrences.ToImmutable()));
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Validates ONE post-completeness fresh Observation against the frozen
    /// discovery epoch. Returns CONSISTENT or INVALIDATED; never mutates the
    /// epoch, never re-normalizes the discovery history, never expands the
    /// inventory.
    /// </summary>
    /// <param name="freshObservation">The post-completeness fresh Observation (revisit / parent return / any same-Container fresh evidence).</param>
    /// <param name="discoveryEvidence">The frozen ContainerInventoryCompletenessEvidence of the current Container.</param>
    /// <param name="continuityVerified">Same-Container continuity verdict already established by the Agent (TryVerifyViewportContinuity).</param>
    /// <param name="agentResolvedDispositions">Agent-explicit contextual dispositions for occurrences of <paramref name="freshObservation"/>
    /// (the Agent — the contextual authority — resolved them before calling the Validator; e.g. a PARENT_RETURN_CONTROL).
    /// Occurrence-scoped: only dispositions whose ObservationSequence equals the fresh Observation's sequence apply.
    /// The Validator never interprets the underlying element itself.</param>
    public static ConsistencyResult Validate(
        Observation freshObservation,
        ContainerInventoryCompletenessEvidence discoveryEvidence,
        bool continuityVerified,
        ImmutableArray<ContextualInteractionDisposition> agentResolvedDispositions = default)
    {
        ArgumentNullException.ThrowIfNull(freshObservation);
        ArgumentNullException.ThrowIfNull(discoveryEvidence);

        // B.1 same-Container continuity.
        if (!continuityVerified)
        {
            return ConsistencyResult.Invalidated(
                "same-Container continuity FAILED for post-completeness fresh evidence; completeness invalidated.");
        }

        // B.2 no UNRESOLVED interactive UNKNOWN. An interactive element that
        // the Agent EXPLICITLY resolved (occurrence-scoped, current observation
        // only) as a PARENT_RETURN_CONTROL is a RESOLVED_NON_INVENTORY_CONTROL
        // — it is not an Unknown interaction obligation and is excluded from
        // the Unknown inconsistency accounting. This Validator never performs
        // its own parent-return semantic interpretation (no content-desc /
        // class / title logic here): it consumes ONLY the Agent's explicit
        // disposition. Any other UNKNOWN invalidates (fail closed).
        var effectiveDispositions = agentResolvedDispositions.IsDefault
            ? ImmutableArray<ContextualInteractionDisposition>.Empty
            : agentResolvedDispositions;
        var resolvedParentReturnIndices = effectiveDispositions
            .Where(d => d.ObservationSequence == freshObservation.SequenceNumber)
            .Where(d => d.Kind == ContextualInteractionDispositionKind.ParentReturnControl)
            .Select(d => d.StructuredElementIndex)
            .ToHashSet();
        foreach (var affordance in InteractionAffordanceAnalyzer.Analyze(freshObservation))
        {
            if (affordance.Classification == InteractionAffordanceKind.Unknown)
            {
                if (resolvedParentReturnIndices.Contains(affordance.SourceElementIndex))
                    continue;
                return ConsistencyResult.Invalidated(
                    $"post-completeness fresh evidence contains an UNRESOLVED interactive UNKNOWN affordance (element {affordance.SourceElementIndex}); completeness invalidated.");
            }
        }

        // B.3/B.4/B.5: each fresh NAVIGATION_CANDIDATE occurrence must uniquely
        // re-establish exactly one frozen logical source class; a previously
        // unknown candidate or an ambiguous mapping invalidates completeness.
        // LOCAL_CONTROL elements produce no occurrence (B.6) and are ignored.
        var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(freshObservation);
        foreach (var occurrence in occurrences)
        {
            var resolution = ResolveFrozenClass(occurrence, discoveryEvidence);
            if (resolution == ResolutionKind.NoClass)
            {
                return ConsistencyResult.Invalidated(
                    $"post-completeness fresh occurrence '{occurrence.OccurrenceIdentity}' does not resolve to any proven frozen logical source (previously-unknown NAVIGATION_CANDIDATE); completeness invalidated.");
            }
            if (resolution == ResolutionKind.Ambiguous)
            {
                return ConsistencyResult.Invalidated(
                    $"post-completeness fresh occurrence '{occurrence.OccurrenceIdentity}' maps ambiguously to multiple frozen logical source classes; completeness invalidated (no signature guessing).");
            }
        }

        return ConsistencyResult.ConsistentOk(
            $"post-completeness fresh evidence is CONSISTENT with the frozen discovery epoch (navigation occurrences={occurrences.Length}); no inventory change.");
    }

    private static ResolutionKind ResolveFrozenClass(
        NavigationSourceOccurrence occurrence,
        ContainerInventoryCompletenessEvidence discoveryEvidence)
    {
        int matches = 0;
        foreach (var source in discoveryEvidence.ProvenLogicalSources)
        {
            if (string.Equals(source.Signature, occurrence.StructuredSignature, StringComparison.Ordinal))
            {
                matches++;
                if (matches > 1)
                    return ResolutionKind.Ambiguous;
            }
        }
        return matches == 0 ? ResolutionKind.NoClass : ResolutionKind.Unique;
    }
}
