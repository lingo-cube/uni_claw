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
    public const string NavigationCondition = "navigation";
    public const string ReadOnlyCondition = "readOnly";
    public const string Description = "持续菜单遍历；候选条件navigation表示当前根页Task显式目标，readOnly表示只读switch；两者仅在本Source的TraversalRule中求值，不属于全局If条件。非任务导航范围Blocked；动态未分类为Unknown。live发现穷尽未证明，报告部分覆盖。";
    public static string Describe(SettingsCoverageConfig config) => config.NavigationScope is null
        ? Description
        : "持续菜单遍历；启用Task逐route导航白名单：当前route白名单ui.element为navigation，switch仅作readOnly，其余未授权入口Blocked；安全skip不等于覆盖完成，live发现穷尽未证明，Discovery仍为false。";
    public static TraversalObservation? Project(string source, UniKernel kernel, SettingsCoverageConfig config)
    {
        if (source != Source || kernel.CurrentBelief is not { } belief || belief.ObservationNeed is not null) return null;
        var graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView?.Version}");
        if (graph?.CurrentContainerId is not { } current) return null;
        var routeKnown = belief.WorldState.TryGetValue(ProductAssociationStrategy.ScreenRouteSubject, out var route)
            && !belief.Conflicts.Any(conflict => conflict.Subject == ProductAssociationStrategy.ScreenRouteSubject);
        var scoped = config.NavigationScope;
        var scopeRoute = routeKnown ? route!.Value : null;
        var scopeEntries = scopeRoute is not null && scoped?.NavigationByRoute.TryGetValue(scopeRoute, out var entries) == true
            ? entries : Array.Empty<string>();
        var scopeSafe = scoped is null || SettingsNavigationScope.TryGetSafeContext(
            scopeRoute, !routeKnown, belief.WorldState.TryGetValue(ProductAssociationStrategy.PopupSubject, out var popup)
                ? popup.Value : null, belief.Conflicts.Any(conflict => conflict.Subject == ProductAssociationStrategy.PopupSubject),
            out _);
        var atRoot = routeKnown && route!.Value == config.RootRoute;
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
            var navigation = scoped is not null
                ? scopeSafe && item.Role == "ui.element"
                    && scopeEntries.Contains(item.SemanticDescriptor!, StringComparer.Ordinal)
                : atRoot && item.Role == "ui.element"
                    && config.TargetPages.Contains(item.SemanticDescriptor!, StringComparer.Ordinal);
            var readOnly = item.Role == "switch";
            // Explicitly scoped tasks may omit other controls. Dynamic/unclassified
            // actions remain Unknown; exclusion never certifies them as safe navigation.
            var outsideScope = scoped is not null
                ? !readOnly && !navigation
                : routeKnown && !readOnly && !navigation
                    && (atRoot
                        ? !config.DynamicRootEntriesSettings.AllVisible
                            || config.DynamicRootEntriesSettings.Exclusions.Contains(item.SemanticDescriptor!, StringComparer.Ordinal)
                        : !config.DeepTraversalSettings.Enabled && !config.DynamicNestedEntriesSettings.IsActive);
            var target = new PlanCandidate($"{current}:{item.Role}:{item.SemanticDescriptor}",
                item.Role, item.SemanticDescriptor!,
                ambiguous ? PlanCandidateDisposition.Unknown
                    : forbidden || outsideScope ? PlanCandidateDisposition.Blocked : PlanCandidateDisposition.Ready,
                item.EvidenceBasis,
                ambiguous ? "ambiguous-current-target" : forbidden ? "task-policy-exclusion"
                    : outsideScope ? "outside-task-navigation-scope" : null);
            return new TraversalCandidate(target, new Dictionary<string, PolicyTruth>
            {
                [ReadOnlyCondition] = readOnly ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                [NavigationCondition] = forbidden || outsideScope || readOnly ? PolicyTruth.Violated
                    : navigation ? PolicyTruth.Satisfied : PolicyTruth.Unknown,
            });
        }).GroupBy(item => item.Target.Key, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        // A complete hierarchy capture still does not prove that virtual/scrollable
        // menus have been exhausted. Live scope therefore remains honestly partial.
        return new TraversalObservation(graph, candidates, false, Array.Empty<string>());
    }
}
