using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

/// <summary>
/// Projects raw, source-correlated observation primitives into the V2 capability input.
/// This type deliberately performs no semantic classification.
/// </summary>
public static class SemanticObservationFactProjector
{
    /// <summary>Projects one immutable observation into the external capability input context.</summary>
    public static ExternalSemanticCapabilityContext Project(
        Observation observation,
        IEnumerable<SemanticVerifiedHistoryReference>? verifiedHistory = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var sources = observation.Sources;
        var primary = sources.Where(s => s.Tier == ObservationSourceTier.PrimaryVision && s.Available).ToArray();
        if (primary.Length != 1 || primary[0].ObservationSequence != observation.SequenceNumber)
            throw new InvalidOperationException("A single correlated primary source is required.");

        var frame = primary[0].FrameReference;
        if (sources.Any(s => s.Available &&
            (s.ObservationSequence != observation.SequenceNumber || !string.Equals(s.FrameReference, frame, StringComparison.Ordinal))))
            throw new InvalidOperationException("Available observation sources are not frame-correlated.");

        var sourceMetadata = sources.Select(ToMetadata).ToImmutableArray();
        var observationReference = new SemanticObservationReference(
            CreateObservationId(observation.SequenceNumber, frame), observation.SequenceNumber, frame);
        var facts = ImmutableArray.CreateBuilder<SemanticObservationFact>();
        var vision = primary[0];
        for (var index = 0; index < observation.Elements.Length; index++)
            AddVisionFacts(facts, observation.Elements[index], index, vision, observation.SequenceNumber, frame);

        var auxiliary = sources.FirstOrDefault(s => s.Tier == ObservationSourceTier.AuxiliaryStructured && s.Available);
        if (auxiliary is not null)
        {
            for (var index = 0; index < observation.StructuredElements.Length; index++)
                AddStructuredFacts(facts, observation.StructuredElements[index], index, auxiliary, observation.SequenceNumber, frame);
        }

        return new ExternalSemanticCapabilityContext(observationReference, sourceMetadata, verifiedHistory, facts.ToImmutable());
    }

    private static SemanticSourceMetadata ToMetadata(ObservationSourceMetadata source) =>
        new(source.SourceId, source.Tier == ObservationSourceTier.PrimaryVision ? SemanticSourceTier.Primary : SemanticSourceTier.Auxiliary,
            source.Available, source.FrameReference);

    private static void AddVisionFacts(ImmutableArray<SemanticObservationFact>.Builder facts, ObservedElement element,
        int index, ObservationSourceMetadata source, long sequence, string frame)
    {
        var occurrence = CreateOccurrenceId(source.SourceId, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(facts, occurrence, SemanticObservationFactKind.Text, source, sequence, frame, rawText: element.Text, rawProviderType: element.PerceptionType);
        if (!string.IsNullOrWhiteSpace(element.PerceptionType))
            Add(facts, occurrence, SemanticObservationFactKind.ClassName, source, sequence, frame, rawClassName: element.PerceptionType);
        if (element.SwitchState is not null)
            Add(facts, occurrence, SemanticObservationFactKind.BooleanState, source, sequence, frame, primitiveState: element.SwitchState, rawProviderType: element.PerceptionType);
        if (element.Bounds is { IsValid: true } bounds)
            Add(facts, occurrence, SemanticObservationFactKind.Geometry, source, sequence, frame, bounds: Normalize(bounds));
    }

    private static void AddStructuredFacts(ImmutableArray<SemanticObservationFact>.Builder facts, StructuredElementEvidence element,
        int index, ObservationSourceMetadata source, long sequence, string frame)
    {
        var identity = string.IsNullOrWhiteSpace(element.SourceNodeIdentity) ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : element.SourceNodeIdentity;
        var occurrence = CreateOccurrenceId(source.SourceId, identity!);
        var parentOccurrence = string.IsNullOrWhiteSpace(element.ParentSourceNodeIdentity)
            ? null
            : CreateOccurrenceId(source.SourceId, element.ParentSourceNodeIdentity);
        Add(facts, occurrence, SemanticObservationFactKind.Text, source, sequence, frame, rawText: element.RawText,
            rawClassName: element.Class, rawResourceName: element.ResourceId, rawContentDescription: element.ContentDescription,
            clickable: element.Clickable, checkable: element.Checkable, enabled: element.Enabled, focusable: element.Focusable,
            parentOccurrenceId: parentOccurrence);
        if (!string.IsNullOrWhiteSpace(element.Class))
            Add(facts, occurrence, SemanticObservationFactKind.ClassName, source, sequence, frame, rawClassName: element.Class,
                clickable: element.Clickable, checkable: element.Checkable, enabled: element.Enabled, focusable: element.Focusable,
                parentOccurrenceId: parentOccurrence);
        if (!string.IsNullOrWhiteSpace(element.ResourceId))
            Add(facts, occurrence, SemanticObservationFactKind.ResourceName, source, sequence, frame, rawResourceName: element.ResourceId, parentOccurrenceId: parentOccurrence);
        if (!string.IsNullOrWhiteSpace(element.ContentDescription))
            Add(facts, occurrence, SemanticObservationFactKind.ContentDescription, source, sequence, frame, rawContentDescription: element.ContentDescription, parentOccurrenceId: parentOccurrence);
        if (element.Checked is not null || element.Clickable is not null || element.Checkable is not null)
            Add(facts, occurrence, SemanticObservationFactKind.BooleanState, source, sequence, frame, primitiveState: element.Checked,
                clickable: element.Clickable, checkable: element.Checkable, enabled: element.Enabled, focusable: element.Focusable,
                parentOccurrenceId: parentOccurrence);
        if (element.Bounds is { IsValid: true } bounds)
            Add(facts, occurrence, SemanticObservationFactKind.Geometry, source, sequence, frame, bounds: Normalize(bounds), parentOccurrenceId: parentOccurrence);
    }

    private static void Add(ImmutableArray<SemanticObservationFact>.Builder facts, string occurrence, SemanticObservationFactKind kind,
        ObservationSourceMetadata source, long sequence, string frame, string? rawText = null, string? rawClassName = null,
        string? rawResourceName = null, string? rawContentDescription = null, bool? primitiveState = null,
        SemanticNormalizedBounds? bounds = null, string? rawProviderType = null, bool? clickable = null,
        bool? checkable = null, bool? enabled = null, bool? focusable = null, string? parentOccurrenceId = null) =>
        facts.Add(new SemanticObservationFact(occurrence, kind, source.SourceId,
            source.Tier == ObservationSourceTier.PrimaryVision ? SemanticSourceTier.Primary : SemanticSourceTier.Auxiliary,
            source.Provenance, sequence, frame, rawText, rawClassName, rawResourceName, rawContentDescription,
            primitiveState, bounds, rawProviderType, clickable, checkable, enabled, focusable, parentOccurrenceId));

    private static SemanticNormalizedBounds Normalize(ElementBounds bounds) =>
        new(bounds.X1, bounds.Y1, bounds.X2 - bounds.X1, bounds.Y2 - bounds.Y1);

    /// <summary>Creates the stable observation-local occurrence identifier used by this projector.</summary>
    public static string CreateOccurrenceId(string sourceId, string sourceOccurrenceIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceOccurrenceIdentity);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceId + "\0" + sourceOccurrenceIdentity));
        return Convert.ToHexString(bytes.AsSpan(0, 12));
    }

    /// <summary>Returns the current structured-element index for a correlated occurrence.</summary>
    public static bool TryResolveStructuredIndex(Observation observation, string occurrenceId, out int index)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        index = -1;
        var source = observation.Sources.FirstOrDefault(s =>
            s.Tier == ObservationSourceTier.AuxiliaryStructured && s.Available &&
            s.ObservationSequence == observation.SequenceNumber);
        if (source is null) return false;
        for (var i = 0; i < observation.StructuredElements.Length; i++)
        {
            var identity = string.IsNullOrWhiteSpace(observation.StructuredElements[i].SourceNodeIdentity)
                ? i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : observation.StructuredElements[i].SourceNodeIdentity!;
            if (string.Equals(CreateOccurrenceId(source.SourceId, identity), occurrenceId, StringComparison.Ordinal))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>Resolves a primary-vision occurrence to the current visual element index.</summary>
    public static bool TryResolveVisualIndex(Observation observation, string occurrenceId, out int index)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        index = -1;
        var source = observation.Sources.FirstOrDefault(s =>
            s.Tier == ObservationSourceTier.PrimaryVision && s.Available &&
            s.ObservationSequence == observation.SequenceNumber);
        if (source is null) return false;
        for (var i = 0; i < observation.Elements.Length; i++)
        {
            if (string.Equals(CreateOccurrenceId(source.SourceId, i.ToString(System.Globalization.CultureInfo.InvariantCulture)), occurrenceId, StringComparison.Ordinal))
            {
                index = observation.Elements[i].Index;
                return true;
            }
        }
        return false;
    }

    private static string CreateObservationId(long sequence, string frame) =>
        CreateOccurrenceId(frame, sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
