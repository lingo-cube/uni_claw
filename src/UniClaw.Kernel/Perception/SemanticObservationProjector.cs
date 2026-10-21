using System.Collections.Frozen;
using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Perception;

/// <summary>Projects supported meaning through P2; intent and operation success never become world facts.</summary>
public static class SemanticObservationProjector
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ObservationProposal> CreateProposals(
        SemanticPerceptionRequest request, SemanticPerceptionResult result,
        string producer = "uni.perception", ObservationContext context = ObservationContext.External)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(producer) || !Enum.IsDefined(context)
            || UniPerceptionCapability.ValidateSemanticRequest(request) is not null) return [];
        var normalized = UniPerceptionCapability.NormalizeSemanticResult(request, result);
        if (normalized.Status != CapabilityStatus.Completed || normalized.Observation is not { } observed) return [];
        var current = request.Current;
        var claim = new SemanticPageClaim(observed.PageKey, observed.PageName, observed.PagePurpose,
            observed.ObservedTitle, current.CaptureId, current.ObservationCycleId, current.SessionCorrelation);
        var leaves = current.SourceLeafEvidenceIds.Concat(request.Previous?.SourceLeafEvidenceIds ?? FrozenSet<string>.Empty)
            .ToFrozenSet(StringComparer.Ordinal);
        return new[]
        {
            new ObservationProposal(new("ui.semantic.page", JsonSerializer.Serialize(claim, Json)),
                IngressKind.Observation, context, new(producer, current.CaptureTime, $"capture:{current.CaptureId}",
                    new[] { "semantic.page:1", $"capture:{current.CaptureId}", $"request:{request.RequestId}" }))
            {
                DerivedFromEvidenceIds = observed.BasisEvidenceIds,
                TransitiveEvidenceBasis = leaves,
                FusionRule = "semantic.page",
                FusionVersion = "1",
            },
        };
    }
}
