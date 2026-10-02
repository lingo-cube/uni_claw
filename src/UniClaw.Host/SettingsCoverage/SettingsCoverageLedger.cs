namespace UniClaw.Host.SettingsCoverage;

public sealed record CoverageStepRecord(
    int Index,
    string DecisionId,
    string? DshSessionId,
    string TargetRole,
    string? TargetDescriptor,
    string EffectClass,
    string? DesiredState,
    bool PreActionTargetUnique,
    string? ReceiptId,
    string? ReceiptOutcome,
    string? ReceiptCommand,
    string? RouteBefore,
    string? RouteAfter,
    string? PostCaptureId,
    bool Verified,
    string? FailureReason,
    string? Directive);

public sealed record CoverageItem(string Requirement, string Detail, bool Covered, string? Evidence);

public sealed record CoverageReport(
    string Status,                          // "CoverageComplete" | "BoundedStop"
    IReadOnlyList<CoverageItem> Items,
    double CoverageRate,                    // covered/total（无 item 时 0）
    double StepSuccessRate,                 // verified/executed（executed=0 时 0）
    IReadOnlyList<string> UncoveredItems,
    string? FirstDivergence,
    int StepsExecuted, int StepsVerified, int ScrollsUsed, int ScrollAttempts, int ConsultRounds);

public sealed record CoverageSnapshot(
    bool CoverageComplete,
    bool MaxStepsReached,
    bool MaxScrollsReached,
    bool MaxConsecutiveFailuresReached,
    bool MaxConsultRoundsReached,
    string? CurrentRoute,
    int StepsVerified,
    string? NextDirective,
    string? NextDirectiveKind);             // "enter"|"scroll"|"back"|"re-enter"|null

public sealed class SettingsCoverageLedger
{
    private sealed record EnteredEntry(string RouteAfter, string? ReceiptId, long Sequence);

    private readonly List<(long Sequence, string Route, IReadOnlyList<(string Role, string? Descriptor)> Occurrences)> _observations = new();
    private readonly List<(long Sequence, CoverageStepRecord Step)> _steps = new();
    private readonly HashSet<int> _duplicateEffectStepIndexes = new();
    private long _sequence;
    private int _consultRounds;
    private int _stepsExecuted;
    private int _stepsVerified;
    private int _consecutiveFailures;
    private int _scrollAttempts;
    private int _scrollsUsed;
    private long _lastVerifiedScrollSequence = -1;
    private bool _duplicateEffectRecorded;

    public void RecordObservation(string route, IReadOnlyList<(string Role, string? Descriptor)> visibleOccurrences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        _observations.Add((++_sequence, route, visibleOccurrences));
    }

    public void RecordStep(CoverageStepRecord step)
    {
        _steps.Add((++_sequence, step));

        _stepsExecuted++;
        if (step.Verified)
        {
            _stepsVerified++;
            _consecutiveFailures = 0;
        }
        else
        {
            _consecutiveFailures++;
        }

        if (step.EffectClass is "swipe-up" or "swipe-down")
        {
            _scrollAttempts++;
            if (step.Verified)
            {
                _scrollsUsed++;
                _lastVerifiedScrollSequence = _steps[^1].Sequence;
            }
        }

        // Duplicate-effect detection: two consecutive dispatched steps with the
        // same target and no verified step in between. Only the first such
        // event is recorded.
        if (!_duplicateEffectRecorded
            && step.ReceiptId is not null
            && _steps.Count >= 2)
        {
            var previous = _steps[^2].Step;
            if (previous.ReceiptId is not null
                && !previous.Verified
                && !step.Verified
                && previous.TargetRole == step.TargetRole
                && previous.TargetDescriptor == step.TargetDescriptor
                && previous.EffectClass == step.EffectClass)
            {
                _duplicateEffectStepIndexes.Add(_steps.Count - 2);
                _duplicateEffectStepIndexes.Add(_steps.Count - 1);
                _duplicateEffectRecorded = true;
            }
        }
    }

    public void RecordConsultRound(string decisionId) => _consultRounds++;

    public CoverageSnapshot Snapshot(SettingsCoverageConfig config)
    {
        var (items, _, _, _, _, _, _) = Recompute(config);
        var coverageComplete = items.All(item => item.Covered);
        var currentRoute = _observations.Count > 0 ? _observations[^1].Route : null;

        var maxStepsReached = _stepsExecuted >= config.Bounds.MaxSteps;
        var maxScrollsReached = _scrollAttempts >= config.Bounds.MaxScrolls;
        var maxConsecutiveFailuresReached = _consecutiveFailures >= config.Bounds.MaxConsecutiveFailures;
        var maxConsultRoundsReached = _consultRounds >= config.Bounds.MaxConsultRounds;

        var terminated =
            (config.Termination.OnCoverageComplete && coverageComplete)
            || (config.Termination.OnMaxSteps && maxStepsReached)
            || (config.Termination.OnMaxScrolls && maxScrollsReached)
            || (config.Termination.OnConsecutiveFailures && maxConsecutiveFailuresReached);

        string? directive = null;
        string? directiveKind = null;
        if (!coverageComplete && !terminated)
        {
            var scrollSatisfied = items.First(item => item.Requirement == "scroll-discovered-entry").Covered;
            var backSatisfied = items.First(item => item.Requirement == "back-navigation").Covered;
            var repeatedSatisfied = items.First(item => item.Requirement == "repeated-entry").Covered;
            (directive, directiveKind) = NextDirective(config, scrollSatisfied, backSatisfied, repeatedSatisfied, currentRoute);
        }

        return new CoverageSnapshot(
            CoverageComplete: coverageComplete,
            MaxStepsReached: maxStepsReached,
            MaxScrollsReached: maxScrollsReached,
            MaxConsecutiveFailuresReached: maxConsecutiveFailuresReached,
            MaxConsultRoundsReached: maxConsultRoundsReached,
            CurrentRoute: currentRoute,
            StepsVerified: _steps.Count(entry => entry.Step.Verified),
            NextDirective: directive,
            NextDirectiveKind: directiveKind);
    }

    public CoverageReport Report(SettingsCoverageConfig config)
    {
        var (items, backVerified, enteredPages, scrollCandidates, lastVerifiedScrollSequence, _, _) = Recompute(config);
        var coveredCount = items.Count(item => item.Covered);
        var coverageComplete = items.Count > 0 && coveredCount == items.Count;
        var uncovered = items.Where(item => !item.Covered).Select(item => item.Requirement).ToList();

        return new CoverageReport(
            Status: coverageComplete ? "CoverageComplete" : "BoundedStop",
            Items: items,
            CoverageRate: items.Count > 0 ? (double)coveredCount / items.Count : 0,
            StepSuccessRate: _stepsExecuted > 0 ? (double)_stepsVerified / _stepsExecuted : 0,
            UncoveredItems: uncovered,
            FirstDivergence: FirstDivergence(config),
            StepsExecuted: _stepsExecuted,
            StepsVerified: _stepsVerified,
            ScrollsUsed: _scrollsUsed,
            ScrollAttempts: _scrollAttempts,
            ConsultRounds: _consultRounds);
    }

    private (List<CoverageItem> Items, int BackVerified, Dictionary<string, List<EnteredEntry>> EnteredPages,
        List<string> ScrollCandidates, long LastVerifiedScrollSequence, bool RootSeen, List<string> SecondLevelRoutes)
        Recompute(SettingsCoverageConfig config)
    {
        var rootSeen = false;
        var backVerified = 0;
        var enteredPages = new Dictionary<string, List<EnteredEntry>>(StringComparer.Ordinal);
        var secondLevelRoutes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (sequence, route, occurrences) in _observations)
        {
            if (IsRoot(config, route))
                rootSeen = true;
        }

        // Enter/back bookkeeping over the step timeline.
        foreach (var (sequence, step) in _steps)
        {
            var descriptor = step.TargetDescriptor;
            if (descriptor is null || !step.Verified || !IsTapOrClick(step.EffectClass))
                continue;

            if (step.RouteBefore is not null
                && IsRoot(config, step.RouteBefore)
                && step.RouteAfter is not null
                && !IsRoot(config, step.RouteAfter))
            {
                if (!enteredPages.TryGetValue(descriptor, out var entries))
                {
                    entries = new List<EnteredEntry>();
                    enteredPages[descriptor] = entries;
                }
                entries.Add(new EnteredEntry(step.RouteAfter, step.ReceiptId, sequence));
                secondLevelRoutes.Add(step.RouteAfter);
            }
            else if (descriptor == config.BackDescriptor
                && step.EffectClass == "tap"
                && step.RouteBefore is not null
                && !IsRoot(config, step.RouteBefore)
                && step.RouteAfter is not null
                && IsRoot(config, step.RouteAfter))
            {
                backVerified++;
            }
        }

        var scrollCandidates = ScrollCandidates(config);

        var items = new List<CoverageItem>
        {
            new(
                Requirement: "root-page",
                Detail: rootSeen ? "root route observed" : "root route not observed",
                Covered: rootSeen,
                Evidence: rootSeen ? config.RootRoute : null),
        };

        var enteredEvidence = string.Join(";",
            enteredPages.SelectMany(kv => kv.Value.Select(entry => $"entered:{kv.Key}->{entry.RouteAfter}")));
        var targetPagesEntered = config.TargetPages.Count(page => enteredPages.ContainsKey(page));
        items.Add(new(
            Requirement: "first-level-all-visible",
            Detail: $"entered {targetPagesEntered}/{config.TargetPages.Count}",
            Covered: config.TargetPages.All(page => enteredPages.ContainsKey(page)),
            Evidence: enteredEvidence.Length > 0 ? enteredEvidence : null));

        var enteredCandidates = scrollCandidates
            .Where(candidate => _lastVerifiedScrollSequence >= 0
                && enteredPages.TryGetValue(candidate, out var entries)
                && entries.Any(entry => entry.Sequence > _lastVerifiedScrollSequence))
            .ToList();
        items.Add(new(
            Requirement: "scroll-discovered-entry",
            Detail: $"entered {enteredCandidates.Count}/{config.Coverage.ScrollDiscoveredEntries} scroll-discovered",
            Covered: enteredCandidates.Count >= config.Coverage.ScrollDiscoveredEntries,
            Evidence: enteredCandidates.Count > 0 ? string.Join(";", enteredCandidates) : null));

        items.Add(new(
            Requirement: "second-level-pages",
            Detail: $"distinct second-level routes {secondLevelRoutes.Count}/{config.Coverage.SecondLevelPages}",
            Covered: secondLevelRoutes.Count >= config.Coverage.SecondLevelPages,
            Evidence: secondLevelRoutes.Count > 0 ? string.Join(";", secondLevelRoutes) : null));

        items.Add(new(
            Requirement: "back-navigation",
            Detail: $"back verified {backVerified}",
            Covered: !config.Coverage.BackNavigation || backVerified >= 1,
            Evidence: backVerified > 0 ? config.BackDescriptor : null));

        var repeatedDescriptors = enteredPages
            .Where(kv => kv.Value.Count >= 2
                && kv.Value.Select(entry => entry.ReceiptId).Distinct().Count() >= 2)
            .Select(kv => kv.Key)
            .ToList();
        items.Add(new(
            Requirement: "repeated-entry",
            Detail: $"satisfied {repeatedDescriptors.Count}/{config.Coverage.RepeatedEntries}",
            Covered: config.Coverage.RepeatedEntries == 0 || repeatedDescriptors.Count >= config.Coverage.RepeatedEntries,
            Evidence: repeatedDescriptors.Count > 0 ? string.Join(";", repeatedDescriptors) : null));

        return (items, backVerified, enteredPages, scrollCandidates, _lastVerifiedScrollSequence, rootSeen, secondLevelRoutes.ToList());
    }

    private (string? Directive, string? Kind) NextDirective(
        SettingsCoverageConfig config,
        bool scrollSatisfied,
        bool backSatisfied,
        bool repeatedSatisfied,
        string? currentRoute)
    {
        // 离开根页时恒先返回（既是 back-navigation 覆盖项的来源，也是
        // 继续遍历的前提——进入/滚动指令只在根页可 grounding）。
        if (currentRoute is not null && !IsRoot(config, currentRoute))
            return ($"Return to the Settings root page by tapping '<{config.BackDescriptor}>'", "back");

        var enteredPages = EnteredPagesSnapshot(config);
        var visible = CurrentVisibleDescriptors();
        if (visible is null)
            return (null, null); // 尚无根页观察：等待，不指挥

        // 只指挥当前可见的目标（不可见目标经滚动露出后再指挥）。
        var firstNotEntered = config.TargetPages
            .FirstOrDefault(page => !enteredPages.Contains(page) && visible.Contains(page));
        if (firstNotEntered is not null)
            return ($"Enter the Settings first-level entry '<{firstNotEntered}>' by tapping it.", "enter");

        if (!scrollSatisfied)
        {
            // 已发现的滚动候选优先进入；无候选且滚动预算未尽则继续滚动；
            // 预算尽 = 覆盖被阻塞（bounded stop）。
            var allEntered = AllEnteredDescriptors(config);
            // AGT-007 加固：进入验证失败过的候选不再重试（真机实证：底部
            // 候选可能因路由指纹碰撞永远无法验证进入，重试只会耗尽失败预算）。
            var failedCandidates = FailedEntryDescriptors(config);
            var candidate = ScrollCandidates(config)
                .FirstOrDefault(descriptor => !allEntered.Contains(descriptor)
                    && !failedCandidates.Contains(descriptor)
                    && visible.Contains(descriptor));
            if (candidate is not null)
                return ($"Enter the scroll-discovered Settings entry '<{candidate}>' by tapping it.", "enter");
            if (_scrollsUsed < config.Bounds.MaxScrolls)
                return ($"Scroll the Settings list down with swipe-up on '<{config.ScrollContainerDescriptor}>'", "scroll");
            return (null, null);
        }

        if (!repeatedSatisfied)
        {
            var firstEntered = config.TargetPages
                .FirstOrDefault(page => enteredPages.Contains(page) && visible.Contains(page));
            if (firstEntered is not null)
                return ($"Enter '<{firstEntered}>' again to verify repeated entry.", "re-enter");
        }

        return (null, null);
    }

    /// <summary>滚动候选发现：仅统计已验证 scroll 之后的根页观察里出现、
    /// 且不在 TargetPages 内的 descriptor（滚动前观察不产生候选）。</summary>
    private List<string> ScrollCandidates(SettingsCoverageConfig config)
    {
        var scrollCandidates = new List<string>();
        foreach (var (sequence, route, occurrences) in _observations)
        {
            if (!IsRoot(config, route)
                || _lastVerifiedScrollSequence < 0
                || sequence < _lastVerifiedScrollSequence)
                continue;
            foreach (var (role, descriptor) in occurrences)
            {
                if (role != "ui.element"
                    || descriptor is null
                    || descriptor == config.BackDescriptor // AGT-006 补：返回键不是遍历入口
                    || config.TargetPages.Contains(descriptor, StringComparer.Ordinal)
                    || scrollCandidates.Contains(descriptor))
                    continue;
                scrollCandidates.Add(descriptor);
            }
        }
        return scrollCandidates;
    }

    /// <summary>最新一次观察的可见可进入 descriptor 集（ui.element 角色——
    /// scrollable 容器自身不是遍历入口；无观察 → null）。</summary>
    private HashSet<string>? CurrentVisibleDescriptors()
    {
        for (var i = _observations.Count - 1; i >= 0; i--)
        {
            var (_, _, occurrences) = _observations[i];
            var descriptors = occurrences
                .Where(o => o.Role == "ui.element" && o.Descriptor is not null)
                .Select(o => o.Descriptor!)
                .ToHashSet(StringComparer.Ordinal);
            return descriptors;
        }
        return null;
    }

    /// <summary>AGT-007：进入验证失败的 descriptor（tap 未通过验证）——
    /// 指令引擎跳过，防不可进入候选的重试循环。</summary>
    private HashSet<string> FailedEntryDescriptors(SettingsCoverageConfig config)
    {
        var failed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, step) in _steps)
        {
            if (step.TargetDescriptor is null
                || step.Verified
                || !IsTapOrClick(step.EffectClass))
                continue;
            if (step.RouteBefore is not null && IsRoot(config, step.RouteBefore))
                failed.Add(step.TargetDescriptor);
        }
        return failed;
    }

    /// <summary>AGT-006 修复：全部已进入 descriptor（不限 TargetPages——
    /// 滚动候选不在 TargetPages 内，指令引擎用它防候选重复进入）。</summary>
    private HashSet<string> AllEnteredDescriptors(SettingsCoverageConfig config)
    {
        var entered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, step) in _steps)
        {
            if (step.TargetDescriptor is null
                || !step.Verified
                || !IsTapOrClick(step.EffectClass))
                continue;
            if (step.RouteBefore is not null
                && IsRoot(config, step.RouteBefore)
                && step.RouteAfter is not null
                && !IsRoot(config, step.RouteAfter))
                entered.Add(step.TargetDescriptor);
        }
        return entered;
    }

    private HashSet<string> EnteredPagesSnapshot(SettingsCoverageConfig config)
    {
        var entered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (sequence, step) in _steps)
        {
            var descriptor = step.TargetDescriptor;
            if (descriptor is null
                || !step.Verified
                || !IsTapOrClick(step.EffectClass)
                || !config.TargetPages.Contains(descriptor, StringComparer.Ordinal))
                continue;
            if (step.RouteBefore is not null
                && IsRoot(config, step.RouteBefore)
                && step.RouteAfter is not null
                && !IsRoot(config, step.RouteAfter))
            {
                entered.Add(descriptor);
            }
        }
        return entered;
    }

    private string? FirstDivergence(SettingsCoverageConfig config)
    {
        for (var index = 0; index < _steps.Count; index++)
        {
            var step = _steps[index].Step;
            if (_duplicateEffectStepIndexes.Contains(index))
                return $"step {step.Index} decision {step.DecisionId}: duplicate-effect-no-interleaving-verified-step";
            if (!step.Verified)
                return $"step {step.Index} decision {step.DecisionId}: {step.FailureReason ?? "deviation-from-directive"}";
            if (!DirectiveMatches(config, step))
                return $"step {step.Index} decision {step.DecisionId}: deviation-from-directive";
        }
        return null;
    }

    private bool DirectiveMatches(SettingsCoverageConfig config, CoverageStepRecord step)
    {
        if (step.Directive is null)
            return true;
        var kind = DirectiveKind(step.Directive);
        var descriptor = DirectiveDescriptor(step.Directive);
        return kind switch
        {
            "scroll" => step.EffectClass is "swipe-up" or "swipe-down",
            "back" => IsTapOrClick(step.EffectClass) && step.TargetDescriptor == config.BackDescriptor,
            "enter" or "re-enter" => IsTapOrClick(step.EffectClass) && step.TargetDescriptor == descriptor,
            _ => true,
        };
    }

    private static string? DirectiveDescriptor(string directive)
    {
        var start = directive.IndexOf('<');
        var end = directive.IndexOf('>');
        return start >= 0 && end > start ? directive[(start + 1)..end] : null;
    }

    private static string? DirectiveKind(string directive)
    {
        if (directive.Contains("Scroll", StringComparison.Ordinal))
            return "scroll";
        if (directive.StartsWith("Return to", StringComparison.Ordinal))
            return "back";
        if (directive.Contains(" again", StringComparison.Ordinal))
            return "re-enter";
        if (directive.StartsWith("Enter", StringComparison.Ordinal))
            return "enter";
        return null;
    }

    private bool IsRoot(SettingsCoverageConfig config, string route) =>
        string.Equals(route, config.RootRoute, StringComparison.Ordinal);

    private static bool IsTapOrClick(string effectClass) => effectClass is "tap" or "click";
}
