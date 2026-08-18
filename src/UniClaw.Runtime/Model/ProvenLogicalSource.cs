using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// One logical source class proven by the FROZEN forward discovery epoch.
///
/// DISCOVERED != CURRENTLY_VISIBLE: the class proves the source exists in the
/// accepted discovery world evidence; it does not claim the source is in any
/// later viewport.
///
/// The <see cref="Signature"/> is the forward-normalized resolution KEY, not the
/// identity: the class identity is the evidence-built set of discovery-epoch
/// occurrence references (<see cref="FrozenOccurrences"/>) that the forward
/// ordered-overlap equivalence merged into this single source. Two classes can
/// never share a signature in a resolved normalization; a fresh occurrence must
/// still uniquely re-establish exactly ONE class (never guess by signature).
/// </summary>
/// <param name="Signature">Merged forward-normalized signature (resolution key).</param>
/// <param name="FrozenOccurrences">Discovery-epoch observation-local occurrence references merged into this source.</param>
public sealed record ProvenLogicalSource(
    string Signature,
    ImmutableArray<ProvenSourceOccurrence> FrozenOccurrences);

/// <summary>Discovery-epoch observation-local occurrence reference (provenance of one frozen class).</summary>
/// <param name="ObservationSequence">Observation sequence in the frozen discovery epoch.</param>
/// <param name="OccurrenceIdentity">Observation-local occurrence identity ("nav:{ordinal}").</param>
public sealed record ProvenSourceOccurrence(
    long ObservationSequence,
    string OccurrenceIdentity);
