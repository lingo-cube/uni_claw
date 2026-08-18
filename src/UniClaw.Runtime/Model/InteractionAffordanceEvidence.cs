namespace UniClaw.Runtime.Model;

/// <summary>
/// Runtime semantic interpretation of structured external UI evidence.
/// It answers only what interaction role is supported; it is not authorization,
/// destination truth, completion, or Goal evidence.
/// </summary>
public enum InteractionAffordanceKind
{
    /// <summary>Structurally non-interactive (not clickable/checkable/focusable,
    /// no switch/checkable child evidence) — decoration/title/status text. Must
    /// NOT block Container completeness.</summary>
    NonInteractive,
    NavigationCandidate,
    LocalControl,
    Unknown,
}

/// <summary>
/// Immutable Agent-consumed evidence that an accepted structured UI source is
/// a Settings navigation candidate, a local control, or unknown.
/// </summary>
/// <param name="SourceObservationSequence">Accepted Observation sequence.</param>
/// <param name="SourceElementIndex">Index into Observation.StructuredElements.</param>
/// <param name="Classification">Ternary affordance classification.</param>
/// <param name="Reason">Deterministic evidence reason.</param>
/// <param name="SourceResourceId">Optional resource-id for traceability.</param>
/// <param name="DestinationSemanticPage">Optional destination; discovery does not require it.</param>
public sealed record InteractionAffordanceEvidence(
    long SourceObservationSequence,
    int SourceElementIndex,
    InteractionAffordanceKind Classification,
    string Reason,
    string? SourceResourceId = null,
    string? DestinationSemanticPage = null);
