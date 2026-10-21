namespace UniClaw.Host.SettingsCoverage;

public sealed partial class SettingsCoverageLedger
{
    private CoverageDirective? NextDirective(
        SettingsCoverageConfig config,
        bool scrollSatisfied,
        bool backSatisfied,
        bool repeatedSatisfied,
        string? currentRoute)
    {
        var enteredPages = EnteredPagesSnapshot(config);
        var visible = CurrentVisibleDescriptors();
        if (visible is null)
            return null; // 尚无根页观察：等待，不指挥

        // AGT-019 Q4 skip-first：同轮可见候选双源（NativeRid 在场）优先，
        // 单源跳过不丢弃；OrderBy 稳定排序保确定性（同输入同指令）。
        var dualSource = CurrentDualSourceDescriptors();
        string? ByConfidence(IEnumerable<string> candidates, Func<string, bool> eligible) =>
            candidates.Where(eligible)
                .OrderBy(descriptor => dualSource.Contains(descriptor) ? 0 : 1)
                .FirstOrDefault();

        // 深度遍历只接受配置中显式声明的 parent > child；未知 UI 不会被提升为目标。
        if (config.DeepTraversalSettings.Enabled && currentRoute is not null && !IsRoot(config, currentRoute))
        {
            var currentPage = RoutePage(currentRoute);
            var nestedParents = NestedTargets(config);
            if (currentPage is not null && nestedParents.ContainsKey(currentPage)
                && nestedParents[currentPage].Any(child => !NestedWasEntered(config, currentPage, child)))
            {
                var child = ByConfidence(
                    nestedParents[currentPage],
                    child => !NestedWasEntered(config, currentPage, child) && visible.Contains(child));
                if (child is not null)
                    return new CoverageDirective(CoverageDirectiveKind.NestedEnter, child, $"Enter the nested Settings entry '<{child}>' by tapping it.");
            }
        }

        if (config.DynamicNestedEntriesSettings.IsActive && currentRoute is not null && !IsRoot(config, currentRoute))
        {
            var nested = ByConfidence(
                DynamicNestedFrontier(config)
                    .Where(key => key.StartsWith(currentRoute + ">", StringComparison.Ordinal))
                    .Select(key => key[(currentRoute.Length + 1)..]),
                visible.Contains);
            if (nested is not null)
                return new CoverageDirective(CoverageDirectiveKind.NestedEnter, nested, $"Enter the dynamically discovered nested Settings entry '<{nested}>' by tapping it.");
        }

        // 离开根页时返回；nested 页面同样 fail closed 地只允许配置的 back。
        if (currentRoute is not null && !IsRoot(config, currentRoute))
            return new CoverageDirective(CoverageDirectiveKind.Back, config.BackDescriptor, $"Return to the Settings root page by tapping '<{config.BackDescriptor}>'");

        // 只指挥当前可见的目标（不可见目标经滚动露出后再指挥）。
        var firstNotEntered = ByConfidence(
            config.TargetPages,
            page => !enteredPages.Contains(page) && visible.Contains(page));
        if (firstNotEntered is not null)
            return new CoverageDirective(CoverageDirectiveKind.Enter, firstNotEntered, $"Enter the Settings first-level entry '<{firstNotEntered}>' by tapping it.");

        if (config.DynamicRootEntriesSettings.AllVisible)
        {
            var dynamic = ByConfidence(DynamicRootFrontier(config), visible.Contains);
            if (dynamic is not null)
                return new CoverageDirective(CoverageDirectiveKind.Enter, dynamic, $"Enter the dynamically discovered Settings entry '<{dynamic}>' by tapping it.");
        }

        if (!scrollSatisfied)
        {
            // 已发现的滚动候选优先进入；无候选且滚动预算未尽则继续滚动；
            // 预算尽 = 覆盖被阻塞（bounded stop）。
            var allEntered = AllEnteredDescriptors(config);
            // AGT-007 加固：进入验证失败过的候选不再重试（真机实证：底部
            // 候选可能因路由指纹碰撞永远无法验证进入，重试只会耗尽失败预算）。
            var failedCandidates = FailedEntryDescriptors(config);
            var candidate = ByConfidence(
                ScrollCandidates(config),
                descriptor => !allEntered.Contains(descriptor)
                    && !failedCandidates.Contains(descriptor)
                    && visible.Contains(descriptor));
            if (candidate is not null)
                return new CoverageDirective(CoverageDirectiveKind.Enter, candidate, $"Enter the scroll-discovered Settings entry '<{candidate}>' by tapping it.");
            if (_scrollAttempts < config.Bounds.MaxScrolls)
                return new CoverageDirective(CoverageDirectiveKind.Scroll, config.ScrollContainerDescriptor, $"Scroll the Settings list down with swipe-up on '<{config.ScrollContainerDescriptor}>'");
            return null;
        }

        if (!repeatedSatisfied)
        {
            var firstEntered = config.TargetPages
                .FirstOrDefault(page => enteredPages.Contains(page)
                    && !IsAgentSkipped(page) && visible.Contains(page));
            if (firstEntered is not null)
                return new CoverageDirective(CoverageDirectiveKind.ReEnter, firstEntered, $"Enter '<{firstEntered}>' again to verify repeated entry.");
        }

        if (config.DeepTraversalSettings.Enabled)
        {
            var pendingParent = NestedTargets(config)
                .FirstOrDefault(pair => pair.Value.Any(child => !NestedWasEntered(config, pair.Key, child))
                    && visible.Contains(pair.Key));
            if (!string.IsNullOrWhiteSpace(pendingParent.Key))
                return new CoverageDirective(CoverageDirectiveKind.ReEnter, pendingParent.Key, $"Re-enter the Settings page '<{pendingParent.Key}>' to inspect explicit nested entries.");
        }

        return null;
    }

}
