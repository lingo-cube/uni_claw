using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>Task-owned positive navigation allowlist, keyed by observed route.</summary>
public sealed record SettingsNavigationScope(
    IReadOnlyDictionary<string, IReadOnlyList<string>> NavigationByRoute,
    bool AllowRootScroll = true)
{
    internal static bool TryGetSafeContext(
        AgentDecisionContext context, out string routeValue)
    {
        routeValue = string.Empty;
        var claims = context.CurrentWorldClaims;
        claims.TryGetValue(ProductAssociationStrategy.ScreenRouteSubject, out var route);
        claims.TryGetValue(ProductAssociationStrategy.PopupSubject, out var popup);
        return TryGetSafeContext(route?.Value, route?.InConflict == true,
            popup?.Value, popup?.InConflict == true, out routeValue);
    }

    internal static bool TryGetSafeContext(string? route, bool routeConflict,
        string? popup, bool popupConflict, out string routeValue)
    {
        routeValue = string.Empty;
        if (routeConflict || string.IsNullOrWhiteSpace(route)
            || !route.StartsWith("android.settings|rk1:", StringComparison.Ordinal)
            || popupConflict || !string.Equals(popup, "absent", StringComparison.OrdinalIgnoreCase))
            return false;
        routeValue = route;
        return true;
    }

    public PlanStepGuardResult EvaluateCurrentStep(
        AgentActionStep step, AgentDecisionContext context, SettingsCoverageConfig config)
    {
        var refs = new[] { "settings-navigation-scope" };
        var claims = context.CurrentWorldClaims;
        if (!TryGetSafeContext(context, out var routeValue))
            return Reject("route-unknown-or-conflicted", refs);
        if (step.DesiredState is not null)
            return Reject("desired-state-not-permitted", refs);
        var matching = (context.Elements ?? Array.Empty<ElementSummary>())
            .Where(element => string.Equals(element.Text?.Trim(), step.TargetDescriptor?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matching.Any(element => element.Checkable == true)
            || string.Equals(step.TargetRole, "switch", StringComparison.OrdinalIgnoreCase))
            return Reject("switch-or-checkable-not-permitted", refs);

        var target = step.TargetDescriptor?.Trim();
        if (string.IsNullOrWhiteSpace(target))
            return Reject("task-navigation-not-authorized", refs);
        var isRoot = string.Equals(routeValue, config.RootRoute, StringComparison.Ordinal);
        if (step.EffectClass is "tap" or "click")
        {
            if (!string.Equals(step.TargetRole, "ui.element", StringComparison.Ordinal))
                return Reject("task-navigation-not-authorized", refs);
            var visible = matching.Length == 1;
            var allowedBack = !isRoot && string.Equals(target, config.BackDescriptor, StringComparison.Ordinal) && visible;
            var allowedNavigation = NavigationByRoute.TryGetValue(routeValue, out var entries)
                && entries.Contains(target, StringComparer.Ordinal) && visible;
            return allowedBack || allowedNavigation
                ? new PlanStepGuardResult(PlanStepGuardVerdict.Allow, "task navigation scope allows current target", refs)
                : Reject("task-navigation-not-authorized", refs);
        }
        if (step.EffectClass == "swipe-up" && isRoot && AllowRootScroll
            && step.TargetRole == "scrollable" && matching.Length == 1
            && string.Equals(target, config.ScrollContainerDescriptor, StringComparison.Ordinal))
            return new PlanStepGuardResult(PlanStepGuardVerdict.Allow, "root scroll allowed by task scope", refs);
        return Reject("task-navigation-not-authorized", refs);
    }

    private static PlanStepGuardResult Reject(string reason, IReadOnlyList<string> refs) =>
        new(PlanStepGuardVerdict.Reject, reason, refs);
}
