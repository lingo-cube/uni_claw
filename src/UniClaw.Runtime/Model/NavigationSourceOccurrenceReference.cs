namespace UniClaw.Runtime.Model;

/// <summary>
/// CALLER_SOURCE_PROVENANCE_CONTRACT — minimal immutable caller-side reference to
/// one navigation source occurrence.
///
/// A reference names WHICH source inside ONE accepted Observation a caller means.
/// <see cref="OccurrenceLocalIdentity"/> is observation-local only ("the 3rd
/// navigation candidate in Observation N"); it is NEVER a cross-viewport or
/// global logical identity.
///
/// Ownership: the caller (BranchInventoryEvaluator / test goal) may only EXPLAIN
/// where a branch points; it cannot assert equivalence or declare a logical
/// source. Equivalence is decided by the Agent-run-local
/// <see cref="UniClaw.Runtime.Agent.SourceGroundingValidator"/> + normalizer.
/// </summary>
public sealed record NavigationSourceOccurrenceReference
{
    /// <summary>Sequence of the accepted Observation that contains the
    /// occurrence. Must belong to the current Container's accepted viewport
    /// observations of the current run.</summary>
    public long ObservationSequence { get; }

    /// <summary>Observation-local occurrence identity (e.g. "nav:3"). Must not
    /// be used across viewports or runs.</summary>
    public string OccurrenceLocalIdentity { get; }

    /// <summary>Creates an observation-local navigation occurrence reference.</summary>
    /// <param name="observationSequence">Sequence number of the source observation.</param>
    /// <param name="occurrenceLocalIdentity">Observation-local occurrence identity.</param>
    public NavigationSourceOccurrenceReference(long observationSequence, string occurrenceLocalIdentity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(observationSequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceLocalIdentity);
        ObservationSequence = observationSequence;
        OccurrenceLocalIdentity = occurrenceLocalIdentity;
    }
}
