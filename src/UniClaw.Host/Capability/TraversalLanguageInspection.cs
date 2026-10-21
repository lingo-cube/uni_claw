using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Host.Capability;

/// <summary>Explicit Runtime Integration → Product Runtime typed adapter. Registry
/// lookup remains in the existing task binding factory; this adapter never dispatches
/// arbitrary capability operations. Its declared input is the current target label,
/// not all pixels or the whole page.</summary>
public sealed class TraversalLanguageInspection
{
    private readonly Func<LanguageInspectionBinding?> _binding;
    private readonly UniKernel _kernel;
    private readonly EvidenceLedger _evidence;

    public TraversalLanguageInspection(UniKernel kernel, EvidenceLedger evidence, Func<LanguageInspectionBinding?> binding)
    {
        _kernel = kernel;
        _evidence = evidence;
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

    /// <summary>The existing deterministic inspector completes synchronously;
    /// the execution adapter participates in the typed asynchronous driver seam
    /// without changing the capability's business protocol or writing Owners.</summary>
    public ValueTask<Finding?> InspectAsync(
        PlanLanguageHandler handler, PlanHandlerInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Inspect(handler, input));
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

        // The post-operation observation may belong to a different page. A
        // same-label occurrence there is not the original bound target.
        var targetInCurrentScope = (input.TargetContainerId ?? input.ContainerId) == input.ContainerId;
        var matches = targetInCurrentScope
            ? belief.Occurrences?.Where(item => item.OwningContainerId == input.ContainerId
                && item.Role == input.Target.TargetRole
                && item.SemanticDescriptor == input.Target.TargetDescriptor).ToArray() ?? []
            : Array.Empty<UniClaw.Kernel.World.UiRealization.OccurrenceBelief>();
        // A semantic descriptor can fall back to a resource id. Inspect only
        // an actual accepted text claim from the target's current capture;
        // never certify a locator string as visible language.
        var captureId = matches.Length == 1
            ? matches[0].EvidenceBasis.Select(reference => _evidence.CanonicalRecords.GetValueOrDefault(reference))
                .Select(record => record?.Provenance.Hierarchy?.CaptureId).FirstOrDefault(id => id is not null)
            : null;
        var text = captureId is null ? null : _evidence.CanonicalRecords.Values.FirstOrDefault(record =>
            record.Provenance.Hierarchy is { } hierarchy && hierarchy.CaptureId == captureId
            && hierarchy.Field is "text" or "content_desc" or "hint"
            && record.Claim.Value == matches[0].SemanticDescriptor);
        var items = text is not null
            ? new[] { new ObservationTextItem(matches[0].OccurrenceId, text.EvidenceId, text.Claim.Value, null) }
            : Array.Empty<ObservationTextItem>();
        return binding.Inspector.Inspect(new LanguageInspectorRequest(correlation,
            handler.ProtocolVersion, binding.ExpectedLanguage,
            items.Length == 1 ? InspectorCoverage.Full : InspectorCoverage.Missing,
            items.Length == 1 ? InspectorInputState.Accepted : InspectorInputState.MissingInput, items));
    }
}
