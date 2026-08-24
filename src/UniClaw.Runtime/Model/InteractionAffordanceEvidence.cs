using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Runtime semantic interpretation of admitted canonical observation evidence.
/// It answers only what interaction role is supported; it is not authorization,
/// destination truth, completion, or Goal evidence.
/// </summary>
public enum InteractionAffordanceKind
{
    /// <summary>Structurally non-interactive (not clickable/checkable/focusable,
    /// no switch/checkable child evidence) — decoration/title/status text. Must
    /// NOT block Container completeness.</summary>
    NonInteractive,
    /// <summary>A candidate that may navigate to another container.</summary>
    NavigationCandidate,
    /// <summary>A control whose effect is local to the current container.</summary>
    LocalControl,
    /// <summary>An explicit parent-return affordance supplied by semantic evidence.</summary>
    ParentReturnControl,
    /// <summary>The affordance could not be classified.</summary>
    Unknown,
}

/// <summary>
/// Immutable Agent-consumed evidence that an accepted canonical UI occurrence is
/// a navigation candidate, a local control, or unknown.
/// </summary>
public sealed record InteractionAffordanceEvidence
{
    public CanonicalObservationOccurrence CanonicalOccurrence { get; }
    public long SourceObservationSequence => CanonicalOccurrence.Reference.ObservationSequence;
    public int SourceElementIndex => CanonicalOccurrence.Reference.ElementIndex;
    public SemanticSourceTier SourceTier => CanonicalOccurrence.PrimarySupport ? SemanticSourceTier.Primary : SemanticSourceTier.Auxiliary;
    public bool EligibleForAuthorization => CanonicalOccurrence.EligibleForAuthorization;
    public InteractionAffordanceKind Classification { get; }
    public string Reason { get; }
    public string? SourceResourceId { get; }
    public string? DestinationSemanticPage { get; }

    public InteractionAffordanceEvidence(CanonicalObservationOccurrence canonicalOccurrence,
        InteractionAffordanceKind classification, string reason, string? sourceResourceId = null,
        string? destinationSemanticPage = null)
    {
        CanonicalOccurrence = canonicalOccurrence ?? throw new ArgumentNullException(nameof(canonicalOccurrence));
        Reason = string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("Reason required.", nameof(reason)) : reason;
        Classification = classification; SourceResourceId = sourceResourceId; DestinationSemanticPage = destinationSemanticPage;
    }
}
