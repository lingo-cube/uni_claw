namespace UniClaw.Runtime.Model;

/// <summary>
/// One appearance of a navigation source inside one accepted Observation.
/// Occurrence identity is observation-local only and must not be used as
/// cross-viewport logical identity.
/// </summary>
/// <param name="ObservationSequence">Accepted Observation sequence.</param>
/// <param name="OccurrenceIdentity">Observation-local occurrence identity.</param>
/// <param name="StructuredSignature">Exact deterministic structured signature.</param>
/// <param name="OrderedPosition">Order within the viewport candidate sequence.</param>
public sealed record NavigationSourceOccurrence(
    long ObservationSequence,
    string OccurrenceIdentity,
    string StructuredSignature,
    int OrderedPosition);
