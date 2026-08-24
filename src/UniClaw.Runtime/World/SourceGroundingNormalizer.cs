using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>Pure observation-local canonical occurrence projection.</summary>
public static class SourceGroundingNormalizer
{
    /// <summary>
    /// Normalizes one immutable observation into canonical occurrences.
    /// When the observation declares no source metadata, the observation's own
    /// channels are treated as implicit: <see cref="Observation.Elements"/> are
    /// the implicit primary Vision channel and
    /// <see cref="Observation.StructuredElements"/> the implicit auxiliary
    /// channel. Explicit source metadata always takes precedence and remains
    /// strictly enforced (single primary source, frame correlation).
    /// </summary>
    public static ImmutableArray<CanonicalObservationOccurrence> Normalize(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var sources = observation.Sources;
        var primaries = sources.Where(s =>
            s.Tier == ObservationSourceTier.PrimaryVision && s.Available &&
            s.ObservationSequence == observation.SequenceNumber).ToArray();
        var primary = primaries.Length == 1 ? primaries[0] : null;

        var auxiliary = sources.FirstOrDefault(s =>
            s.Tier == ObservationSourceTier.AuxiliaryStructured && s.Available &&
            s.ObservationSequence == observation.SequenceNumber &&
            (primary is null || string.Equals(s.FrameReference, primary.FrameReference, StringComparison.Ordinal)));

        // Source-less compatibility: observations that declare no source
        // metadata still expose their elements/structured channels. The
        // elements are the only visual evidence present, so they are canonical
        // primary occurrences; structured evidence remains auxiliary.
        if (primary is null && sources.IsEmpty && observation.Elements.Length > 0)
            primary = Implicit(observation, ObservationSourceTier.PrimaryVision, "implicit-vision");
        if (auxiliary is null && sources.IsEmpty && observation.StructuredElements.Length > 0)
            auxiliary = Implicit(observation, ObservationSourceTier.AuxiliaryStructured, "implicit-structured");

        var result = ImmutableArray.CreateBuilder<CanonicalObservationOccurrence>();
        if (primary is null)
        {
            if (auxiliary is null) return [];
            for (var i = 0; i < observation.StructuredElements.Length; i++)
            {
                var item = observation.StructuredElements[i];
                var local = string.IsNullOrWhiteSpace(item.SourceNodeIdentity) ? i.ToString() : item.SourceNodeIdentity!;
                result.Add(new CanonicalObservationOccurrence(new ObservationOccurrenceReference(auxiliary.SourceId, local, i,
                    observation.SequenceNumber, auxiliary.FrameReference, ObservationSourceKind.AuxiliaryStructured, auxiliary.Provenance), item.Bounds));
            }
            return result.ToImmutable();
        }
        for (var index = 0; index < observation.Elements.Length; index++)
        {
            var element = observation.Elements[index];
            var reference = new ObservationOccurrenceReference(primary.SourceId, index.ToString(), index,
                observation.SequenceNumber, primary.FrameReference, ObservationSourceKind.PrimaryVision, primary.Provenance);
            var supports = ImmutableArray<ObservationOccurrenceReference>.Empty;
            if (auxiliary is not null)
            {
                var matches = observation.StructuredElements
                    .Select((item, i) => (item, i))
                    .Where(x => string.Equals(x.item.RawText, element.Text, StringComparison.Ordinal)
                        && x.item.Bounds is { } ab && element.Bounds is { } vb && Overlaps(ab, vb))
                    .ToArray();
                if (matches.Length == 1)
                {
                    var item = matches[0];
                    var local = string.IsNullOrWhiteSpace(item.item.SourceNodeIdentity)
                        ? item.i.ToString() : item.item.SourceNodeIdentity!;
                    supports = [new ObservationOccurrenceReference(auxiliary.SourceId, local, item.i,
                        observation.SequenceNumber, auxiliary.FrameReference, ObservationSourceKind.AuxiliaryStructured, auxiliary.Provenance)];
                }
            }
            result.Add(new CanonicalObservationOccurrence(reference, element.Bounds, supports));
        }
        if (auxiliary is not null)
            for (var i = 0; i < observation.StructuredElements.Length; i++)
            {
                var item = observation.StructuredElements[i];
                if (result.Any(c => c.AuxiliarySupports.Any(s => s.ElementIndex == i))) continue;
                var local = string.IsNullOrWhiteSpace(item.SourceNodeIdentity) ? i.ToString() : item.SourceNodeIdentity!;
                var reference = new ObservationOccurrenceReference(auxiliary.SourceId, local, i, observation.SequenceNumber,
                    auxiliary.FrameReference, ObservationSourceKind.AuxiliaryStructured, auxiliary.Provenance);
                result.Add(new CanonicalObservationOccurrence(reference, item.Bounds));
            }
        return result.ToImmutable();
    }

    private static ObservationSourceMetadata Implicit(Observation observation, ObservationSourceTier tier, string sourceId) =>
        new(tier, true, observation.SequenceNumber, "implicit-frame", 1, 1, "implicit", sourceId);

    private static bool Overlaps(ElementBounds a, ElementBounds b) => a.X1 <= b.X2 && b.X1 <= a.X2 && a.Y1 <= b.Y2 && b.Y1 <= a.Y2;

}
