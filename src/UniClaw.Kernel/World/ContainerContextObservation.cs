using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.World;

/// <summary>Observed context anchors; canonical endpoints remain a World decision.</summary>
public sealed record ContainerContextObservation(string ParentContextEvidenceId, string ChildContextEvidenceId);

public sealed record ContainerContextDecision(string SourceRevisionId, string EvidenceId,
    string? ParentContainerId, string? ChildContainerId, bool Applied, string Reason);

public static class ContainerContextCodec
{
    public const string Subject = "ui.container.context";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public static string Encode(ContainerContextObservation value)
    {
        Validate(value);
        return JsonSerializer.Serialize(value, Json);
    }

    public static bool TryRead(string value, out ContainerContextObservation? observation)
    {
        observation = null;
        try { observation = JsonSerializer.Deserialize<ContainerContextObservation>(value, Json); Validate(observation!); return true; }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        { observation = null; return false; }
    }

    /// <summary>Build an observed direct-parent proposal from already admitted source fields.</summary>
    public static ObservationProposal CreateProposal(EvidenceRecord parent, EvidenceRecord child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);
        if (parent.Provenance.Hierarchy is not { } p || child.Provenance.Hierarchy is not { } c
            || c.ParentLocalIndex != p.NodeLocalIndex || p.CaptureId != c.CaptureId
            || p.SessionCorrelation != c.SessionCorrelation || p.ObservationCycleId != c.ObservationCycleId
            || p.DeviceId != c.DeviceId || p.CaptureTimestamp != c.CaptureTimestamp || p.Space != c.Space
            || !parent.Provenance.TransformationLineage.Contains(PageInterpretationCodec.NativeContextProfile)
            || !child.Provenance.TransformationLineage.Contains(PageInterpretationCodec.NativeContextProfile))
            throw new ArgumentException("context relation requires two source-bound native direct-parent anchors");
        var leaves = (parent.TransitiveEvidenceBasis.Count > 0 ? parent.TransitiveEvidenceBasis : new[] { parent.EvidenceId }.ToImmutableHashSet())
            .Concat(child.TransitiveEvidenceBasis.Count > 0 ? child.TransitiveEvidenceBasis : new[] { child.EvidenceId }.ToImmutableHashSet())
            .ToImmutableHashSet(StringComparer.Ordinal);
        return new(new(Subject, Encode(new(parent.EvidenceId, child.EvidenceId))), IngressKind.Observation,
            parent.Context, new("native-context-projector", p.CaptureTimestamp, "context:" + p.CaptureId,
                ["native-context-containment:v1", "capture:" + p.CaptureId]))
        { DerivedFromEvidenceIds = [parent.EvidenceId, child.EvidenceId], TransitiveEvidenceBasis = leaves,
          FusionRule = "native-context-containment", FusionVersion = "1" };
    }

    private static void Validate(ContainerContextObservation value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ParentContextEvidenceId)
            || string.IsNullOrWhiteSpace(value.ChildContextEvidenceId) || value.ParentContextEvidenceId == value.ChildContextEvidenceId)
            throw new ArgumentException("context needs distinct exact source anchors");
    }
}
