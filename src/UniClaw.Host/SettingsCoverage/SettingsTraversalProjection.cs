using UniClaw.Kernel;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>Current World references plus explicit task classification. This
/// projection contains no visited/progress state. Unclassified clickable controls
/// stay Unknown; absence of a forbidden word does not establish navigation safety.</summary>
public static class SettingsTraversalProjection
{
    public const string Source = "settings.menu";
    public static TraversalObservation? Project(string source, UniKernel kernel, SettingsCoverageConfig config)
    {
        if (source != Source || kernel.CurrentBelief is not { } belief) return null;
        var graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView?.Version}");
        if (graph?.CurrentContainerId is not { } current) return null;
        var occurrences = belief.Occurrences?.Where(item => item.OwningContainerId == current
            && item.SemanticDescriptor is not null && item.SemanticDescriptor != config.BackDescriptor
            && item.Role != "scrollable").ToArray() ?? [];
        var candidates = occurrences.Select(item =>
        {
            var forbidden = config.ActionPolicy?.ForbiddenTargetPatterns.Any(pattern =>
                item.SemanticDescriptor!.Contains(pattern, StringComparison.OrdinalIgnoreCase)) == true;
            var ambiguous = occurrences.Count(other => other.Role == item.Role
                && other.SemanticDescriptor == item.SemanticDescriptor) != 1;
            // Task-declared targets are positive classification evidence. Generic
            // clickable/label observations cannot establish safe navigation.
            var navigation = item.Role == "ui.element"
                && config.TargetPages.Contains(item.SemanticDescriptor!, StringComparer.Ordinal);
            var readOnly = item.Role == "switch";
            var target = new PlanCandidate($"{current}:{item.Role}:{item.SemanticDescriptor}",
                item.Role, item.SemanticDescriptor!,
                ambiguous ? PlanCandidateDisposition.Unknown
                    : forbidden ? PlanCandidateDisposition.Blocked : PlanCandidateDisposition.Ready,
                item.EvidenceBasis,
                ambiguous ? "ambiguous-current-target" : forbidden ? "task-policy-exclusion" : null);
            return new TraversalCandidate(target, new Dictionary<string, PolicyTruth>
            {
                ["readOnly"] = readOnly ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                ["navigation"] = forbidden || readOnly ? PolicyTruth.Violated
                    : navigation ? PolicyTruth.Satisfied : PolicyTruth.Unknown,
            });
        }).GroupBy(item => item.Target.Key, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        // A complete hierarchy capture still does not prove that virtual/scrollable
        // menus have been exhausted. Live scope therefore remains honestly partial.
        return new TraversalObservation(graph, candidates, false, Array.Empty<string>());
    }
}
