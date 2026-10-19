using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.Capability;

/// <summary>Explicit Runtime Integration → Product Runtime typed adapter. Registry
/// lookup remains in the existing task binding factory; this adapter never dispatches
/// arbitrary capability operations. Its declared input is the current target label,
/// not all pixels or the whole page.</summary>
public sealed class TraversalLanguageInspection
{
    private readonly Func<LanguageInspectionBinding?> _binding;
    private readonly UniKernel _kernel;

    public TraversalLanguageInspection(UniKernel kernel, Func<LanguageInspectionBinding?> binding)
    {
        _kernel = kernel;
        _binding = binding;
    }

    public bool CanBind(PlanLanguageHandler handler)
    {
        var binding = _binding();
        return binding is not null && binding.RunId == _kernel.RunId
            && handler.CapabilityId == binding.CapabilityId
            && handler.ProtocolVersion == LanguageInspectionProtocol.Version
            && string.Equals(handler.ExpectedLanguage, binding.ExpectedLanguage, StringComparison.OrdinalIgnoreCase)
            && binding.Inspector.Description.Health != HealthStatus.Unhealthy;
    }

    public Finding? Inspect(PlanLanguageHandler handler, PlanHandlerInput input)
    {
        if (!CanBind(handler)) return null;
        var binding = _binding()!;
        var belief = _kernel.CurrentBelief;
        var correlation = new CapabilityCorrelation(input.RunId, operationId: input.OperationId,
            domainCorrelation: input.SourceRevisionId);
        if (input.RunId != binding.RunId || belief?.RevisionId != input.SourceRevisionId
            || CurrentPageAnchor.Resolve(belief) != input.ContainerId)
            return new Finding("traversal-language-input-stale", correlation, handler.ProtocolVersion,
                binding.CapabilityId, FindingDisposition.Unknown, CapabilityStatus.Unknown,
                "operation input is not the current accepted World view");

        var route = belief.WorldState.GetValueOrDefault(
            UniClaw.Kernel.World.UiRealization.ProductAssociationStrategy.ScreenRouteSubject)?.Value;
        if (route is not null && binding.IgnoreRoutes.Contains(route, StringComparer.Ordinal))
            return new Finding("traversal-language-not-applicable", correlation, handler.ProtocolVersion,
                binding.CapabilityId, FindingDisposition.NotApplicable, CapabilityStatus.Completed,
                "current route is excluded by the explicit task binding");

        var matches = belief.Occurrences?.Where(item => item.OwningContainerId == input.ContainerId
            && item.Role == input.Target.TargetRole
            && item.SemanticDescriptor == input.Target.TargetDescriptor).ToArray() ?? [];
        var items = matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].SemanticDescriptor)
            ? new[] { new ObservationTextItem(matches[0].OccurrenceId,
                matches[0].EvidenceBasis.First(), matches[0].SemanticDescriptor, null) }
            : Array.Empty<ObservationTextItem>();
        return binding.Inspector.Inspect(new LanguageInspectorRequest(correlation,
            handler.ProtocolVersion, binding.ExpectedLanguage,
            items.Length == 1 ? InspectorCoverage.Full : InspectorCoverage.Missing,
            items.Length == 1 ? InspectorInputState.Accepted : InspectorInputState.MissingInput, items));
    }
}
