using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>Read-only projection of existing coverage-owner facts. A candidate
/// is a semantic target, never a locator, permission or completion claim.</summary>
public static class SettingsPlanFacts
{
    public const string RootNavigationSource = "settings.root-navigation";
    public const string CurrentNavigationSource = "settings.current-navigation";
    public const string HasNavigationCondition = "settings.has-ready-navigation";
    public const string AtRootCondition = "settings.at-root";
    public static IReadOnlyDictionary<string, string> ConditionDescriptions { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HasNavigationCondition] = "当前观察页在配置深度/范围内仍有Ready导航候选；未知事实返回Unknown，不由空集合推断全任务完成。",
            [AtRootCondition] = "当前观察位于任务根页；非根页须有已验证导航链支持。False可用于声明返回策略，不自行执行返回。",
        };
    public static IReadOnlyDictionary<string, string> SourceDescriptions { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CurrentNavigationSource] = "当前观察页配置范围内尚未验证进入的导航入口；结合If/Repeat跨页推进时，每次ForEach最多选1项，下一次循环重新读取新页。switch等记录型控件来自原观察账本，不作为导航点击。返回路径由已验证导航记录提供；空集合不证明全树完成。",
            [RootNavigationSource] = "当前Settings根页已观察、在配置范围内且尚未进入的导航入口；每轮重读。每次body内先进入候选，再点击当前Navigate up返回；每个Act均由Runtime自动取得新观察并独立验证，因此仅确认同一路由不必再加Observe。若确有额外欠证仍须补证。返回不能放在forEach之后，选择下一候选前必须已回到同一根页。集合暂空不证明未知范围穷尽。",
        };

    public static PlanCandidateSet? Project(string source, SettingsCoverageLedger ledger, SettingsCoverageConfig config)
    {
        if (source == CurrentNavigationSource) return ProjectCurrent(ledger, config);
        if (source != RootNavigationSource) return null;
        var latest = ledger.Events.LastOrDefault(item => item.Type == CoverageEventType.ObservationAccepted);
        if (latest?.Route is not { } route) return null;
        var evidence = new[] { $"coverage-event:{latest.Sequence}" };
        if (!string.Equals(route, config.RootRoute, StringComparison.Ordinal))
            return new PlanCandidateSet(route, $"coverage:{latest.Sequence}", new[]
            {
                new PlanCandidate("scope:root-unavailable", "page", route, PlanCandidateDisposition.Unknown,
                    evidence, "source requires an observed Settings root; current page is different or unknown"),
            });
        var entered = ledger.EnteredRootDescriptors(config);
        var skips = ledger.DynamicRootSkipReasons(config);
        var visible = ledger.ObservedElements.Where(item => item.EventSequence == latest.Sequence)
            .GroupBy(item => item.Descriptor, StringComparer.Ordinal);
        var candidates = new List<PlanCandidate>();
        foreach (var group in visible)
        {
            var target = group.Key;
            if (entered.Contains(target)) continue;
            var configured = config.TargetPages.Contains(target, StringComparer.Ordinal);
            if (!configured && !config.DynamicRootEntriesSettings.AllVisible) continue;
            var first = group.First();
            // Framework controls and headings are not dynamic task candidates.
            // Explicitly configured exclusions remain visible as blocked facts.
            if (!configured && skips.TryGetValue(target, out var structural)
                && structural is "non-ui-element-role" or "back-descriptor" or "scroll-container" or "framework-control") continue;
            var reasons = new List<string>();
            if (group.Count() != 1) reasons.Add("ambiguous current semantic target");
            if (first.Role != "ui.element") reasons.Add("candidate is not a navigation element");
            if (skips.TryGetValue(target, out var skip) && skip != "configured-target") reasons.Add(skip);
            if (config.ActionPolicy is { } policy && policy.ForbiddenTargetPatterns.Any(pattern =>
                target.Contains(pattern, StringComparison.OrdinalIgnoreCase))) reasons.Add("forbidden by Settings action policy");
            var refs = evidence.Concat(config.ActionPolicy is { } p ? new[] { $"settings-policy:{p.Digest}" } : Array.Empty<string>()).ToArray();
            var disposition = group.Count() != 1 ? PlanCandidateDisposition.Unknown
                : reasons.Count > 0 ? PlanCandidateDisposition.Blocked : PlanCandidateDisposition.Ready;
            candidates.Add(new PlanCandidate($"root:{target}", first.Role, target, disposition, refs,
                reasons.Count == 0 ? null : string.Join("; ", reasons)));
        }
        return new PlanCandidateSet(route, $"coverage:{latest.Sequence}", candidates);
    }
    public static PlanConditionResult? EvaluateCondition(string condition, SettingsCoverageLedger ledger, SettingsCoverageConfig config)
    {
        if (!ConditionDescriptions.ContainsKey(condition)) return null;
        var latest = ledger.Events.LastOrDefault(item => item.Type == CoverageEventType.ObservationAccepted);
        var scope = latest?.Route ?? "settings:unobserved";
        var revision = $"coverage:{ledger.LastEventSequence}";
        var refs = latest is null ? Array.Empty<string>() : new[] { $"coverage-event:{latest.Sequence}" };
        if (latest?.Route is not { } route || NavigationPath(ledger, config).LastOrDefault() != route)
            return new PlanConditionResult(PolicyTruth.Unknown, scope, revision, refs, "current route lacks a verified navigation context");
        if (condition == AtRootCondition)
            return new PlanConditionResult(route == config.RootRoute ? PolicyTruth.Satisfied : PolicyTruth.Violated, scope, revision, refs);
        var candidates = ProjectCurrent(ledger, config);
        if (candidates is null || candidates.Candidates.Any(c => c.Disposition == PlanCandidateDisposition.Unknown))
            return new PlanConditionResult(PolicyTruth.Unknown, scope, revision, refs, "current navigation candidates remain unknown");
        return new PlanConditionResult(candidates.Candidates.Any(c => c.Disposition == PlanCandidateDisposition.Ready)
            ? PolicyTruth.Satisfied : PolicyTruth.Violated, scope, revision,
            refs.Concat(candidates.Candidates.SelectMany(c => c.EvidenceRefs)).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static PlanCandidateSet? ProjectCurrent(SettingsCoverageLedger ledger, SettingsCoverageConfig config)
    {
        var latest = ledger.Events.LastOrDefault(item => item.Type == CoverageEventType.ObservationAccepted);
        if (latest?.Route is not { } route) return null;
        if (route == config.RootRoute) return Project(RootNavigationSource, ledger, config);
        var refs = new[] { $"coverage-event:{latest.Sequence}" };
        var path = NavigationPath(ledger, config);
        if (path.LastOrDefault() != route)
            return new PlanCandidateSet(route, $"coverage:{ledger.LastEventSequence}", new[]
            {
                new PlanCandidate("scope:current-unavailable", "page", route, PlanCandidateDisposition.Unknown,
                    refs, "current page has no verified root-to-current navigation history"),
            });
        if (!config.DynamicNestedEntriesSettings.IsActive
            || ledger.KnownNavigationDepths(config).GetValueOrDefault(route, path.Count - 1) > config.DynamicNestedEntriesSettings.MaxDepth)
            return new PlanCandidateSet(route, $"coverage:{ledger.LastEventSequence}", Array.Empty<PlanCandidate>());
        var pending = ledger.DynamicNestedFrontier(config).ToHashSet(StringComparer.Ordinal);
        var candidates = ledger.ObservedElements.Where(item => item.EventSequence == latest.Sequence)
            .GroupBy(item => item.Descriptor, StringComparer.Ordinal)
            .Where(group => pending.Contains($"{route}>{group.Key}"))
            .Select(group =>
            {
                var first = group.First();
                var forbidden = config.ActionPolicy?.ForbiddenTargetPatterns.Any(pattern =>
                    first.Descriptor.Contains(pattern, StringComparison.OrdinalIgnoreCase)) == true;
                return new PlanCandidate($"{route}>{first.Descriptor}", first.Role, first.Descriptor,
                    group.Count() != 1 ? PlanCandidateDisposition.Unknown : forbidden ? PlanCandidateDisposition.Blocked : PlanCandidateDisposition.Ready,
                    refs.Concat(config.ActionPolicy is { } policy ? new[] { $"settings-policy:{policy.Digest}" } : Array.Empty<string>()).ToArray(),
                    group.Count() != 1 ? "ambiguous current semantic target" : forbidden ? "forbidden by Settings action policy" : null);
            }).ToArray();
        return new PlanCandidateSet(route, $"coverage:{ledger.LastEventSequence}", candidates);
    }

    private static IReadOnlyList<string> NavigationPath(SettingsCoverageLedger ledger, SettingsCoverageConfig config)
    {
        var path = new List<string> { config.RootRoute };
        foreach (var step in ledger.VerifiedSteps)
        {
            if (step.RouteBefore is null || step.RouteAfter is null || step.RouteBefore == step.RouteAfter) continue;
            if (path[^1] != step.RouteBefore) return Array.Empty<string>();
            var ancestor = path.IndexOf(step.RouteAfter);
            if (ancestor >= 0) path.RemoveRange(ancestor + 1, path.Count - ancestor - 1);
            else if (step.TargetDescriptor == config.BackDescriptor) return Array.Empty<string>();
            else if (step.EffectClass is "tap" or "click") path.Add(step.RouteAfter);
            else return Array.Empty<string>();
        }
        return path;
    }

}
