using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

namespace UniClaw.Runtime.Model;

public enum ObservationSourceKind { PrimaryVision, AuxiliaryStructured }

public sealed record ObservationOccurrenceReference
{
    public string SourceId { get; }
    public string SourceLocalOccurrenceId { get; }
    public int ElementIndex { get; }
    public long ObservationSequence { get; }
    public string Frame { get; }
    public ObservationSourceKind SourceKind { get; }
    public string Provenance { get; }

    public ObservationOccurrenceReference(string sourceId, string localId, int elementIndex, long sequence, string frame, ObservationSourceKind sourceKind, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId); ArgumentException.ThrowIfNullOrWhiteSpace(localId);
        ArgumentException.ThrowIfNullOrWhiteSpace(frame); ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentOutOfRangeException.ThrowIfNegative(elementIndex); ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        if (!Enum.IsDefined(sourceKind)) throw new ArgumentOutOfRangeException(nameof(sourceKind));
        SourceId = sourceId; SourceLocalOccurrenceId = localId; ElementIndex = elementIndex; ObservationSequence = sequence;
        Frame = frame; SourceKind = sourceKind; Provenance = provenance;
    }
}

public sealed record CanonicalObservationOccurrence
{
    public ObservationOccurrenceReference Reference { get; }
    public ElementBounds? Bounds { get; }
    public ImmutableArray<ObservationOccurrenceReference> AuxiliarySupports { get; }
    public bool PrimarySupport => Reference.SourceKind == ObservationSourceKind.PrimaryVision;
    public SemanticSourceTier SourceTier => PrimarySupport ? SemanticSourceTier.Primary : SemanticSourceTier.Auxiliary;
    public bool EligibleForAuthorization => PrimarySupport;
    public string OccurrenceId => SemanticObservationFactProjector.CreateOccurrenceId(Reference.SourceId, Reference.SourceLocalOccurrenceId);

    public CanonicalObservationOccurrence(ObservationOccurrenceReference reference, ElementBounds? bounds, IEnumerable<ObservationOccurrenceReference>? auxiliarySupports = null)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference)); Bounds = bounds;
        var supports = (auxiliarySupports ?? []).ToImmutableArray();
        if (supports.Any(s => s.SourceKind != ObservationSourceKind.AuxiliaryStructured || s.ObservationSequence != reference.ObservationSequence || !string.Equals(s.Frame, reference.Frame, StringComparison.Ordinal) || string.Equals(s.SourceId, reference.SourceId, StringComparison.Ordinal)))
            throw new ArgumentException("Auxiliary support must be distinct, structured, and frame-correlated.", nameof(auxiliarySupports));
        AuxiliarySupports = supports;
    }
}
