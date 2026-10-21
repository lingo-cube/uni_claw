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
    string? Directive)
{
    internal CoverageDirective? SelectedDirective { get; init; }
}

public sealed record CoverageItem(string Requirement, string Detail, bool Covered, string? Evidence);

public sealed record CoverageReport(
    string Status,                          // "CoverageComplete" | "BoundedStop"
    IReadOnlyList<CoverageItem> Items,
    double CoverageRate,                    // covered/total（无 item 时 0）
    double StepSuccessRate,                 // verified/executed（executed=0 时 0）
    IReadOnlyList<string> UncoveredItems,
    string? FirstDivergence,
    int StepsExecuted, int StepsVerified, int ScrollsUsed, int ScrollAttempts, int ConsultRounds,
    IReadOnlyList<string>? DynamicRootFrontier = null,
    IReadOnlyList<string>? DynamicRootVisited = null,
    IReadOnlyDictionary<string, string>? DynamicRootSkipReasons = null,
    IReadOnlyList<string>? DynamicNestedFrontier = null,
    IReadOnlyList<string>? DynamicNestedVisited = null,
    IReadOnlyDictionary<string, string>? DynamicNestedSkipReasons = null,
    IReadOnlyList<string>? ReachedTargets = null,
    IReadOnlyList<string>? AgentSkippedTargets = null,
    IReadOnlyList<string>? StructuralSkippedTargets = null);

public sealed record CoverageSnapshot(
    bool CoverageComplete,
    bool MaxStepsReached,
    bool MaxScrollsReached,
    bool MaxConsecutiveFailuresReached,
    bool MaxConsultRoundsReached,
    string? CurrentRoute,
    int StepsVerified,
    string? NextDirective,
    string? NextDirectiveKind,              // "enter"|"scroll"|"back"|"re-enter"|"nested-enter"|"obstacle"|null
    string? PopupState = null,              // AGT-009：最新弹窗分类声明（present/absent/null=无声明）
    IReadOnlyList<string>? VisibleDescriptors = null) // AGT-009：最新观察的可见 descriptor（清障目标发现）
{
    internal CoverageDirective? SelectedDirective { get; init; }

    /// <summary>AGT-009：可见 descriptor（无观察时为空集，不返回 null）。</summary>
    public IReadOnlyList<string> Visible => VisibleDescriptors ?? Array.Empty<string>();
}

public sealed partial class SettingsCoverageLedger
{
    /// <summary>
    /// TRAJECTORY-001 D3 裁决（M5 进度轴组合）：清障验证不算 Coverage
    /// Progress。新记录依指令种类判定，历史文本经兼容读取；obstacle
    /// 步骤即使 Verified=true 也不计入覆盖进度轴——
    /// RUN-006 实证：弹窗循环里每个清障 tap 都"验证成功"，若计入进度轴
    /// 会把零覆盖推进误报为有进展。StepsVerified 保留为事实字段（不受此
    /// 裁决影响）。停滞判定消费方：Runner 的 InterleaveBookkeeper 进度
    /// 指纹路径（(entered, frontier) 指纹天然不含 obstacle 步骤）。
    /// </summary>
    internal static bool CountsAsCoverageProgress(CoverageStepRecord step, SettingsCoverageConfig config)
    {
        if (!step.Verified)
            return false;
        if (step.SelectedDirective is { Kind: CoverageDirectiveKind.Obstacle }
            || step.SelectedDirective is null && LegacyCoverageDirective.IsObstacle(step.Directive))
            return false;
        return true;
    }

    private sealed record EnteredEntry(string RouteAfter, string? ReceiptId, long Sequence);

    private readonly List<(long Sequence, string Route, IReadOnlyList<(string Role, string? Descriptor, string? NativeRid)> Occurrences)> _observations = new();
    private readonly List<(long Sequence, string State)> _popupStates = new();
    private readonly List<(long Sequence, CoverageStepRecord Step)> _steps = new();
    private readonly List<CoverageEvent> _events = new();
    private readonly Dictionary<string, TargetCoverageMark> _targetMarks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recordedReceiptIds = new(StringComparer.Ordinal);
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

    /// <summary>Owner-owned append-only facts, in monotonically increasing seq order.</summary>
    public IReadOnlyList<CoverageEvent> Events => _events;

    public long LastEventSequence => _sequence;

    /// <summary>Read-only verified route/action history; projection grants no new navigation authority.</summary>
    internal IReadOnlyList<CoverageStepRecord> VerifiedSteps => _steps.Where(item => item.Step.Verified).Select(item => item.Step).ToArray();

    /// <summary>Read-only replay cursor over this run's owner stream.</summary>
    public IReadOnlyList<CoverageEvent> ReadEvents(long afterSequence = 0, int limit = 100)
    {
        var boundedLimit = Math.Clamp(limit, 1, 1000);
        return _events.Where(item => item.Sequence > afterSequence)
            .Take(boundedLimit)
            .ToArray();
    }

    /// <summary>Read-only view derived from accepted observation events. It adds
    /// no state owner and keeps discovery tied to the original ledger sequence.</summary>
    public IReadOnlyList<CoverageObservedElement> ObservedElements => _observations
        .SelectMany(observation => observation.Occurrences
            .Where(item => item.Descriptor is not null)
            .Select(item => new CoverageObservedElement(
                observation.Sequence, observation.Route, item.Role, item.Descriptor!, item.NativeRid)))
        .ToArray();

    /// <summary>Latest accepted mark per target. Structural exclusions are not included.</summary>
    public IReadOnlyDictionary<string, TargetCoverageMark> TargetMarks => _targetMarks;

    public IReadOnlyList<string> AgentSkippedTargets => _targetMarks.Values
        .Where(mark => mark.Kind == TargetCoverageMarkKind.Skipped)
        .Select(mark => mark.Target)
        .OrderBy(target => target, StringComparer.Ordinal)
        .ToArray();

    public void RecordObservation(string route, IReadOnlyList<(string Role, string? Descriptor)> visibleOccurrences)
        => RecordObservation(route, visibleOccurrences.Select(o => (o.Role, o.Descriptor, (string?)null)).ToList());

    /// <summary>AGT-011 §5：带 NativeLocator rid 的观察（框架按钮判据）。</summary>
    public void RecordObservation(string route, IReadOnlyList<(string Role, string? Descriptor, string? NativeRid)> visibleOccurrences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        var sequence = ++_sequence;
        _observations.Add((sequence, route, visibleOccurrences));
        _events.Add(new CoverageEvent(
            sequence,
            CoverageEventType.ObservationAccepted,
            Owner: "settings-coverage.observation",
            Route: route));
    }

    public void RecordStep(CoverageStepRecord step)
    {
        if (step.ReceiptId is { Length: > 0 } receiptId && !_recordedReceiptIds.Add(receiptId))
            return;

        var sequence = ++_sequence;
        _steps.Add((sequence, step));
        _events.Add(new CoverageEvent(
            sequence,
            CoverageEventType.EffectReceiptProduced,
            Owner: "settings-coverage.kernel",
            Target: step.TargetDescriptor,
            Route: step.RouteAfter,
            DecisionId: step.DecisionId,
            ReceiptId: step.ReceiptId,
            CaptureId: step.PostCaptureId));

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

        _events.Add(new CoverageEvent(
            ++_sequence,
            CoverageEventType.PostActionVerified,
            Owner: "settings-coverage.verification",
            Target: step.TargetDescriptor,
            Route: step.RouteAfter,
            DecisionId: step.DecisionId,
            ReceiptId: step.ReceiptId,
            CaptureId: step.PostCaptureId,
            Verified: step.Verified,
            Reason: step.FailureReason));

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

    /// <summary>
    /// AGT-009：记录每周期弹窗分类声明（"present"/"absent"）。小 additive API——
    /// director 经 Snapshot.PopupState 分支，不经 Kernel。
    /// </summary>
    public void RecordPopupState(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (state is not ("present" or "absent"))
            throw new ArgumentException($"popup state must be 'present' or 'absent', got '{state}'", nameof(state));
        var sequence = ++_sequence;
        _popupStates.Add((sequence, state));
        _events.Add(new CoverageEvent(
            sequence,
            CoverageEventType.PopupStateObserved,
            Owner: "settings-coverage.popup",
            Reason: state));
    }

    /// <summary>
    /// Marks an Agent-owned coverage decision. This is a zero-Effect event and
    /// never writes to the structural skip projections.
    /// </summary>
    public bool TryMarkTargetSkipped(
        string target,
        string decisionId,
        string reason,
        IReadOnlyList<string>? evidenceRefs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return TryMarkTarget(
            target,
            TargetCoverageMarkKind.Skipped,
            decisionId,
            reason,
            evidenceRefs ?? Array.Empty<string>(),
            route: null);
    }

    /// <summary>Marks a verified route entry or overlay reachability fact.</summary>
    public bool TryMarkTargetReached(
        string target,
        TargetCoverageMarkKind kind,
        string? decisionId = null,
        string? route = null,
        IReadOnlyList<string>? evidenceRefs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (kind is not (TargetCoverageMarkKind.RouteEntered or TargetCoverageMarkKind.ReachedOverlay))
            throw new ArgumentException("Reached marks must be RouteEntered or ReachedOverlay", nameof(kind));
        return TryMarkTarget(target, kind, decisionId, reason: null,
            evidenceRefs ?? Array.Empty<string>(), route);
    }

    private bool TryMarkTarget(
        string target,
        TargetCoverageMarkKind kind,
        string? decisionId,
        string? reason,
        IReadOnlyList<string> evidenceRefs,
        string? route)
    {
        if (_targetMarks.ContainsKey(target))
            return false;
        var sequence = ++_sequence;
        var mark = new TargetCoverageMark(target, kind, decisionId, reason,
            evidenceRefs.ToArray(), sequence);
        _targetMarks[target] = mark;
        _events.Add(new CoverageEvent(
            sequence,
            CoverageEventType.TargetCoverageMarked,
            Owner: "settings-coverage.coverage",
            Target: target,
            Route: route,
            DecisionId: decisionId,
            Reason: reason,
            EvidenceRefs: mark.EvidenceRefs,
            MarkKind: kind));
        return true;
    }

    /// <summary>AGT-009：最新弹窗声明（从未记录 → null = 无声明）。</summary>
    public string? LatestPopupState =>
        _popupStates.Count > 0 ? _popupStates[^1].State : null;

    public CoverageSnapshot Snapshot(SettingsCoverageConfig config)
    {
        var (items, _, _, _, _, _, _) = Recompute(config);
        var coverageComplete = items.All(item => item.Covered);
        var currentRoute = _observations.Count > 0 ? _observations[^1].Route : null;

        // In goal-directed mode, the current grant and consultation budget are
        // Runtime-owned. Host stops only at an explicitly authorized total
        // ceiling. Legacy mode retains its configured final ceiling behavior.
        var maxStepsCeiling = config.Bounds.GoalDirectedContinuationEnabled
            ? config.Bounds.AuthorizedTotalSteps
            : config.Bounds.MaxTotalSteps ?? config.Bounds.MaxSteps;
        var maxRoundsCeiling = config.Bounds.GoalDirectedContinuationEnabled
            ? config.Bounds.AuthorizedTotalConsultRounds
            : config.Bounds.MaxTotalConsultRounds ?? config.Bounds.MaxConsultRounds;
        var maxStepsReached = maxStepsCeiling is { } stepCeiling && _stepsExecuted >= stepCeiling;
        var maxScrollsReached = _scrollAttempts >= config.Bounds.MaxScrolls;
        var maxConsecutiveFailuresReached = _consecutiveFailures >= config.Bounds.MaxConsecutiveFailures;
        var maxConsultRoundsReached = maxRoundsCeiling is { } roundCeiling && _consultRounds >= roundCeiling;

        var terminated =
            (config.Termination.OnCoverageComplete && coverageComplete)
            || (config.Termination.OnMaxSteps && maxStepsReached)
            || (config.Termination.OnMaxScrolls && maxScrollsReached
                && !config.Bounds.GoalDirectedContinuationEnabled)
            || (config.Termination.OnConsecutiveFailures && maxConsecutiveFailuresReached
                && !config.Bounds.GoalDirectedContinuationEnabled);

        CoverageDirective? directive = null;
        if (!coverageComplete && !terminated)
        {
            // The scroll-attempt limit constrains only further swipes. Preserve
            // its unmet coverage fact, but continue other visible branches.
            var scrollSatisfied = items.First(item => item.Requirement == "scroll-discovered-entry").Covered
                || config.Bounds.GoalDirectedContinuationEnabled && maxScrollsReached;
            var backSatisfied = items.First(item => item.Requirement == "back-navigation").Covered;
            var repeatedSatisfied = items.First(item => item.Requirement == "repeated-entry").Covered;
            directive = NextDirective(config, scrollSatisfied, backSatisfied, repeatedSatisfied, currentRoute);
        }

        return new CoverageSnapshot(
            CoverageComplete: coverageComplete,
            MaxStepsReached: maxStepsReached,
            MaxScrollsReached: maxScrollsReached,
            MaxConsecutiveFailuresReached: maxConsecutiveFailuresReached,
            MaxConsultRoundsReached: maxConsultRoundsReached,
            CurrentRoute: currentRoute,
            StepsVerified: _steps.Count(entry => entry.Step.Verified),
            NextDirective: directive?.Text,
            NextDirectiveKind: directive?.KindName,
            PopupState: LatestPopupState,
            VisibleDescriptors: CurrentVisibleDescriptors()?.ToList()) { SelectedDirective = directive };
    }

    public CoverageReport Report(SettingsCoverageConfig config)
    {
        var (items, backVerified, enteredPages, scrollCandidates, lastVerifiedScrollSequence, _, _) = Recompute(config);
        var coveredCount = items.Count(item => item.Covered);
        var coverageComplete = items.Count > 0 && coveredCount == items.Count;
        var uncovered = items.Where(item => !item.Covered).Select(item => item.Requirement).ToList();
        var structuralSkipped = DynamicRootSkipReasons(config).Keys
            .Concat(DynamicNestedSkipReasons(config).Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToArray();
        var reachedTargets = _targetMarks.Values
            .Where(mark => mark.Kind is TargetCoverageMarkKind.RouteEntered or TargetCoverageMarkKind.ReachedOverlay)
            .Select(mark => mark.Target)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToArray();

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
            ConsultRounds: _consultRounds,
            DynamicRootFrontier: DynamicRootFrontier(config),
            DynamicRootVisited: DynamicRootVisited(config),
            DynamicRootSkipReasons: DynamicRootSkipReasons(config),
            DynamicNestedFrontier: DynamicNestedFrontier(config),
            DynamicNestedVisited: DynamicNestedVisited(config),
            DynamicNestedSkipReasons: DynamicNestedSkipReasons(config),
            ReachedTargets: reachedTargets,
            AgentSkippedTargets: AgentSkippedTargets,
            StructuralSkippedTargets: structuralSkipped);
    }

    /// <summary>动态根页候选中尚未验证进入的 frontier。仅在显式开启
    /// dynamicRootEntries/all-visible 时返回内容。</summary>
    public IReadOnlyList<string> DynamicRootFrontier(SettingsCoverageConfig config)
    {
        if (!config.DynamicRootEntriesSettings.AllVisible)
            return Array.Empty<string>();
        var visited = DynamicRootVisited(config);
        var skipped = DynamicRootSkipReasons(config);
        return DynamicRootCandidates(config)
            .Where(candidate => !visited.Contains(candidate, StringComparer.Ordinal)
                && !IsAgentSkipped(candidate)
                && !IsTargetReached(candidate)
                && !skipped.ContainsKey(candidate))
            .ToArray();
    }

    /// <summary>动态根页候选中已由真实 root→non-root verified step 进入的入口。</summary>
    public IReadOnlyList<string> DynamicRootVisited(SettingsCoverageConfig config)
    {
        if (!config.DynamicRootEntriesSettings.AllVisible)
            return Array.Empty<string>();
        var entered = AllEnteredDescriptors(config);
        return DynamicRootCandidates(config)
            .Where(entered.Contains)
            .ToArray();
    }

    /// <summary>记录结构性跳过原因，便于区分“未观察到”与“观察到但安全策略
    /// 排除”。原因只来自 Host 可证明的字段（role/rid/descriptor），不作
    /// 视觉或 Kernel 语义推断。</summary>
    public IReadOnlyDictionary<string, string> DynamicRootSkipReasons(SettingsCoverageConfig config)
    {
        if (!config.DynamicRootEntriesSettings.AllVisible)
            return new Dictionary<string, string>(StringComparer.Ordinal);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, route, occurrences) in _observations)
        {
            if (!IsCanonicalRoot(config, route))
                continue;
            foreach (var (role, descriptor, nativeRid) in occurrences)
            {
                if (descriptor is null || reasons.ContainsKey(descriptor))
                    continue;
                var reason = DynamicRootSkipReason(config, role, descriptor, nativeRid);
                if (reason is not null)
                    reasons[descriptor] = reason;
            }
        }
        var dynamicCandidates = DynamicRootCandidates(config);
        foreach (var descriptor in FailedEntryDescriptors(config))
        {
            if (dynamicCandidates.Contains(descriptor, StringComparer.Ordinal))
                reasons.TryAdd(descriptor, "entry-verification-failed");
        }
        return reasons;
    }

    private (List<CoverageItem> Items, int BackVerified, Dictionary<string, List<EnteredEntry>> EnteredPages,
        List<string> ScrollCandidates, long LastVerifiedScrollSequence, bool RootSeen, List<string> SecondLevelRoutes)
        Recompute(SettingsCoverageConfig config)
    {
        var rootSeen = false;
        var backVerified = 0;
        var enteredPages = new Dictionary<string, List<EnteredEntry>>(StringComparer.Ordinal);
        var secondLevelRoutes = new HashSet<string>(StringComparer.Ordinal);
        var nestedEntered = new HashSet<string>(StringComparer.Ordinal);

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

            if (config.DeepTraversalSettings.Enabled
                && step.RouteBefore is not null
                && step.RouteAfter is not null
                && !IsRoot(config, step.RouteBefore)
                && !IsRoot(config, step.RouteAfter))
            {
                var parent = RoutePage(step.RouteBefore);
                var key = NestedKey(parent, descriptor);
                if (parent is not null && descriptor is not null
                    && AllowedNested(config, parent, descriptor)
                    && key is not null)
                    nestedEntered.Add(key);
            }
        }

        var scrollCandidates = ScrollCandidates(config);
        var dynamicFrontier = DynamicRootFrontier(config);

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
        var targetPagesEntered = config.TargetPages.Count(page => enteredPages.ContainsKey(page) || IsAgentSkipped(page));
        items.Add(new(
            Requirement: "first-level-all-visible",
            Detail: $"entered {targetPagesEntered}/{config.TargetPages.Count}",
            Covered: config.TargetPages.All(page => enteredPages.ContainsKey(page) || IsAgentSkipped(page)),
            Evidence: enteredEvidence.Length > 0 ? enteredEvidence : null));

        var enteredCandidates = scrollCandidates
            .Where(candidate => _lastVerifiedScrollSequence >= 0
                && ((enteredPages.TryGetValue(candidate, out var entries)
                    && entries.Any(entry => entry.Sequence > _lastVerifiedScrollSequence))
                    || IsAgentSkipped(candidate)))
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

        if (config.DeepTraversalSettings.Enabled)
        {
            var nestedTargets = NestedTargets(config);
            var expected = nestedTargets.Sum(pair => pair.Value.Count);
            items.Add(new(
                Requirement: "nested-pages",
                Detail: $"entered {nestedEntered.Count}/{expected} explicit nested entries",
                Covered: nestedEntered.Count >= expected,
                Evidence: nestedEntered.Count > 0 ? string.Join(";", nestedEntered) : null));
        }

        if (config.DynamicRootEntriesSettings.AllVisible)
        {
            var visited = DynamicRootVisited(config);
            var skipped = DynamicRootSkipReasons(config);
            items.Add(new(
                Requirement: "dynamic-root-entries",
                Detail: $"visited {visited.Count}, frontier {dynamicFrontier.Count}, skipped {skipped.Count}",
                Covered: dynamicFrontier.Count == 0,
                Evidence: visited.Count == 0 && dynamicFrontier.Count == 0
                    ? null
                    : $"visited={string.Join(",", visited)};frontier={string.Join(",", dynamicFrontier)}"));
        }

        if (config.DynamicNestedEntriesSettings.IsActive)
        {
            var visited = DynamicNestedVisited(config);
            var frontier = DynamicNestedFrontier(config);
            var skipped = DynamicNestedSkipReasons(config);
            items.Add(new(
                Requirement: "dynamic-nested-entries",
                Detail: $"visited {visited.Count}, frontier {frontier.Count}, skipped {skipped.Count}",
                Covered: frontier.Count == 0,
                Evidence: visited.Count == 0 && frontier.Count == 0
                    ? null
                    : $"visited={string.Join(",", visited)};frontier={string.Join(",", frontier)}"));
        }

        return (items, backVerified, enteredPages, scrollCandidates, _lastVerifiedScrollSequence, rootSeen, secondLevelRoutes.ToList());
    }

    private bool NestedWasEntered(SettingsCoverageConfig config, string parent, string child)
    {
        foreach (var (_, step) in _steps)
        {
            if (!step.Verified || !IsTapOrClick(step.EffectClass)
                || step.TargetDescriptor != child || step.RouteBefore is null || step.RouteAfter is null
                || IsRoot(config, step.RouteBefore) || IsRoot(config, step.RouteAfter))
                continue;
            if (RoutePage(step.RouteBefore) == parent)
                return true;
        }
        return false;
    }

    private static Dictionary<string, List<string>> NestedTargets(SettingsCoverageConfig config)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var raw in config.DeepTraversalSettings.Entries)
        {
            var separator = raw.IndexOf('>');
            if (separator < 0)
                separator = raw.IndexOf("::", StringComparison.Ordinal);
            var width = raw.Contains("::", StringComparison.Ordinal) ? 2 : 1;
            if (separator <= 0 || separator + width >= raw.Length)
                continue;
            var parent = raw[..separator].Trim();
            var child = raw[(separator + width)..].Trim();
            if (parent.Length == 0 || child.Length == 0)
                continue;
            if (!map.TryGetValue(parent, out var children))
                map[parent] = children = new List<string>();
            if (config.DeepTraversalSettings.MaxEntriesPerPage <= 0
                || children.Count < config.DeepTraversalSettings.MaxEntriesPerPage)
                children.Add(child);
        }
        return map;
    }

    private static bool AllowedNested(SettingsCoverageConfig config, string parent, string child) =>
        NestedTargets(config).TryGetValue(parent, out var children) && children.Contains(child, StringComparer.Ordinal);

    private static string? NestedKey(string? parent, string? child) =>
        parent is null || child is null ? null : $"{parent}>{child}";

    private static string? RoutePage(string route)
    {
        const string marker = "|route:";
        var index = route.IndexOf(marker, StringComparison.Ordinal);
        if (index >= 0)
            return route[(index + marker.Length)..];
        const string routeKeyMarker = "|rk1:";
        index = route.IndexOf(routeKeyMarker, StringComparison.Ordinal);
        if (index < 0)
            return null;
        var start = index + routeKeyMarker.Length;
        var end = route.IndexOf('|', start);
        return (end < 0 ? route[start..] : route[start..end]).Trim();
    }

    /// <summary>AGT-011 §5：框架按钮 rid（android:id/button1|2|…，AlertDialog
    /// 标准 ID）——语料实证对话框按钮以此可分（Button 类不出现在 occurrence
    /// 投影中，rid 是可用载体）；平台级事实，非 UI 风格。</summary>
    private static bool IsFrameworkButtonRid(string? nativeRid) =>
        nativeRid is not null
        && System.Text.RegularExpressions.Regex.IsMatch(
            nativeRid, @"^android:id/button\d+$");

    /// <summary>滚动候选发现：仅统计已验证 scroll 之后的根页观察里出现、
    /// 且不在 TargetPages 内的 descriptor（滚动前观察不产生候选）。</summary>
    private List<string> ScrollCandidates(SettingsCoverageConfig config)
    {
        var scrollCandidates = new List<string>();
        foreach (var (sequence, route, occurrences) in _observations)
        {
            if (!IsCanonicalRoot(config, route)
                || _lastVerifiedScrollSequence < 0
                || sequence < _lastVerifiedScrollSequence)
                continue;
            foreach (var (role, descriptor, nativeRid) in occurrences)
            {
                if (role != "ui.element"
                    || descriptor is null
                    || descriptor == config.BackDescriptor // AGT-006 补：返回键不是遍历入口
                    || descriptor == config.ScrollContainerDescriptor
                    || IsFrameworkButtonRid(nativeRid) // AGT-011 §5：对话框按钮不是遍历入口
                    || config.TargetPages.Contains(descriptor, StringComparer.Ordinal)
                    || (config.Coverage.ExplicitScrollCandidates.Count > 0
                        && !config.Coverage.ExplicitScrollCandidates.Contains(descriptor, StringComparer.Ordinal))
                    || scrollCandidates.Contains(descriptor))
                    continue;
                scrollCandidates.Add(descriptor);
            }
        }
        return scrollCandidates;
    }

    private List<string> DynamicRootCandidates(SettingsCoverageConfig config)
    {
        if (!config.DynamicRootEntriesSettings.AllVisible)
            return new List<string>();
        var candidates = new List<string>();
        foreach (var (_, route, occurrences) in _observations)
        {
            if (!IsCanonicalRoot(config, route))
                continue;
            foreach (var (role, descriptor, nativeRid) in occurrences)
            {
                if (descriptor is null
                    || DynamicRootSkipReason(config, role, descriptor, nativeRid) is not null
                    || candidates.Contains(descriptor, StringComparer.Ordinal))
                    continue;
                candidates.Add(descriptor);
            }
        }
        return candidates;
    }

    public IReadOnlyList<string> DynamicNestedFrontier(SettingsCoverageConfig config)
    {
        if (!config.DynamicNestedEntriesSettings.IsActive)
            return Array.Empty<string>();
        var visited = DynamicNestedVisited(config);
        var skipped = DynamicNestedSkipReasons(config);
        return DynamicNestedCandidates(config)
            .Where(key => !visited.Contains(key, StringComparer.Ordinal)
                && !IsAgentSkipped(key)
                && !IsTargetReached(key)
                && !skipped.ContainsKey(key))
            .ToArray();
    }

    public IReadOnlyList<string> DynamicNestedVisited(SettingsCoverageConfig config)
    {
        if (!config.DynamicNestedEntriesSettings.IsActive)
            return Array.Empty<string>();
        return DynamicNestedCandidates(config)
            .Where(key => _steps.Any(entry => entry.Step.Verified
                && IsTapOrClick(entry.Step.EffectClass)
                && entry.Step.TargetDescriptor is not null
                && key == $"{entry.Step.RouteBefore}>{entry.Step.TargetDescriptor}"
                && entry.Step.RouteBefore is not null
                && entry.Step.RouteAfter is not null
                && !IsRoot(config, entry.Step.RouteBefore)
                && !IsRoot(config, entry.Step.RouteAfter)))
            .ToArray();
    }

    public IReadOnlyDictionary<string, string> DynamicNestedSkipReasons(SettingsCoverageConfig config)
    {
        if (!config.DynamicNestedEntriesSettings.IsActive)
            return new Dictionary<string, string>(StringComparer.Ordinal);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in DynamicNestedCandidates(config, includeSkipped: true))
        {
            var separator = key.LastIndexOf('>');
            var route = key[..separator];
            var descriptor = key[(separator + 1)..];
            foreach (var (_, observedRoute, occurrences) in _observations.Where(o => o.Route == route))
            {
                var occurrence = occurrences.FirstOrDefault(o => o.Descriptor == descriptor);
                if (occurrence.Descriptor is null) continue;
                var reason = DynamicNestedSkipReason(config, occurrence.Role, descriptor, occurrence.NativeRid);
                if (reason is not null) reasons[key] = reason;
            }
        }
        foreach (var (_, step) in _steps.Where(s => !s.Step.Verified && IsTapOrClick(s.Step.EffectClass)))
        {
            if (step.RouteBefore is null || step.TargetDescriptor is null || IsRoot(config, step.RouteBefore)) continue;
            var key = $"{step.RouteBefore}>{step.TargetDescriptor}";
            if (DynamicNestedCandidates(config, includeSkipped: true).Contains(key, StringComparer.Ordinal))
                reasons.TryAdd(key, "entry-verification-failed");
        }
        return reasons;
    }

    // Depth is derived only from verified navigation edges. MaxDepth counts
    // nested-enter layers after the first-level page (1 therefore permits A -> B).
    internal IReadOnlyDictionary<string, int> KnownNavigationDepths(SettingsCoverageConfig config)
    {
        var depths = new Dictionary<string, int>(StringComparer.Ordinal) { [config.RootRoute] = 0 };
        var edges = _steps.Select(item => item.Step).Where(step => step.Verified
            && CountsAsCoverageProgress(step, config) && IsTapOrClick(step.EffectClass)
            && step.TargetDescriptor != config.BackDescriptor
            && step.RouteBefore is not null && step.RouteAfter is not null && step.RouteBefore != step.RouteAfter).ToArray();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var step in edges)
            {
                var before = IsRoot(config, step.RouteBefore!) ? 0
                    : depths.TryGetValue(step.RouteBefore!, out var known) ? known : (int?)null;
                if (before is null || IsRoot(config, step.RouteAfter!)) continue;
                var candidate = before.Value + 1;
                if (!depths.TryGetValue(step.RouteAfter!, out var current) || candidate < current)
                {
                    depths[step.RouteAfter!] = candidate;
                    changed = true;
                }
            }
        }
        return depths;
    }

    private List<string> DynamicNestedCandidates(SettingsCoverageConfig config, bool includeSkipped = false)
    {
        var candidates = new List<string>();
        var pageCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var depths = KnownNavigationDepths(config);
        foreach (var (_, route, occurrences) in _observations)
        {
            if (IsRoot(config, route)) continue;
            if (depths.TryGetValue(route, out var depth) && depth > config.DynamicNestedEntriesSettings.MaxDepth) continue;
            // Candidate scope is per page for the whole run, not per capture.
            // The excluded-element audit keeps its existing observation projection.
            var pageCount = includeSkipped ? 0 : pageCounts.GetValueOrDefault(route);
            foreach (var (role, descriptor, nativeRid) in occurrences)
            {
                if (descriptor is null || (!includeSkipped && DynamicNestedSkipReason(config, role, descriptor, nativeRid) is not null)) continue;
                if (includeSkipped || DynamicNestedSkipReason(config, role, descriptor, nativeRid) is null)
                {
                    var key = $"{route}>{descriptor}";
                    if (!candidates.Contains(key, StringComparer.Ordinal) && pageCount < config.DynamicNestedEntriesSettings.MaxEntriesPerPage)
                    { candidates.Add(key); pageCount++; }
                }
            }
            if (!includeSkipped) pageCounts[route] = pageCount;
        }
        return candidates;
    }

    private static string? DynamicNestedSkipReason(SettingsCoverageConfig config, string role, string descriptor, string? nativeRid)
    {
        if (role != "ui.element") return "non-ui-element-role";
        if (descriptor == config.BackDescriptor) return "back-descriptor";
        if (descriptor == config.ScrollContainerDescriptor) return "scroll-container";
        if (role == "switch") return "switch-control";
        if (IsFrameworkButtonRid(nativeRid)) return "framework-control";
        return null;
    }

    private static string? DynamicRootSkipReason(
        SettingsCoverageConfig config, string role, string descriptor, string? nativeRid)
    {
        if (role != "ui.element")
            return "non-ui-element-role";
        if (descriptor == config.BackDescriptor)
            return "back-descriptor";
        if (descriptor == config.ScrollContainerDescriptor)
            return "scroll-container";
        if (IsFrameworkButtonRid(nativeRid))
            return "framework-control";
        if (config.TargetPages.Contains(descriptor, StringComparer.Ordinal))
            return "configured-target";
        if (config.DynamicRootEntriesSettings.Exclusions.Contains(descriptor, StringComparer.Ordinal))
            return "explicit-exclusion";
        return null;
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

    /// <summary>AGT-019 Q4（owner 订正）：最新观察里带 NativeRid 的元素
    /// = 双源（XML 连接，ElementEpistemic.Observed 等价）；缺失 = 视觉
    /// 单源（Partial 等价）。skip-first 排序依据。</summary>
    private HashSet<string> CurrentDualSourceDescriptors()
    {
        for (var i = _observations.Count - 1; i >= 0; i--)
        {
            var (_, _, occurrences) = _observations[i];
            return occurrences
                .Where(o => o.Role == "ui.element" && o.Descriptor is not null && o.NativeRid is not null)
                .Select(o => o.Descriptor!)
                .ToHashSet(StringComparer.Ordinal);
        }
        return new HashSet<string>(StringComparer.Ordinal);
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
    /// <summary>Owner-derived successful root arrivals and explicit Agent skips;
    /// used to project the pending Plan frontier without another task store.</summary>
    public IReadOnlySet<string> EnteredRootDescriptors(SettingsCoverageConfig config) => AllEnteredDescriptors(config);

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
        foreach (var target in AgentSkippedTargets)
            entered.Add(target);
        return entered;
    }

    private bool IsAgentSkipped(string target) =>
        FindTargetMark(target)?.Kind == TargetCoverageMarkKind.Skipped;

    private bool IsTargetReached(string target) =>
        FindTargetMark(target)?.Kind is TargetCoverageMarkKind.RouteEntered or TargetCoverageMarkKind.ReachedOverlay;

    private TargetCoverageMark? FindTargetMark(string target)
    {
        if (_targetMarks.TryGetValue(target, out var exact))
            return exact;
        var separator = target.LastIndexOf('>');
        var descriptor = separator >= 0 ? target[(separator + 1)..] : target;
        return _targetMarks.TryGetValue(descriptor, out var nested)
            ? nested : null;
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
        foreach (var target in AgentSkippedTargets)
            if (config.TargetPages.Contains(target, StringComparer.Ordinal))
                entered.Add(target);
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

    private bool DirectiveMatches(SettingsCoverageConfig config, CoverageStepRecord step) =>
        step.SelectedDirective is { } directive
            ? directive.MatchesRecordedStep(step)
            : LegacyCoverageDirective.Matches(config, step);

    private bool IsRoot(SettingsCoverageConfig config, string route) =>
        string.Equals(route, config.RootRoute, StringComparison.Ordinal)
        // Live UiAutomator can temporarily return identity-only evidence when
        // the XML route probe is unavailable.  The Host may treat the stable
        // Settings package identity as root for safe return/termination, but
        // it must not invent a child route from that fallback.
        || string.Equals(route, "android.settings", StringComparison.Ordinal);

    private static bool IsCanonicalRoot(SettingsCoverageConfig config, string route) =>
        string.Equals(route, config.RootRoute, StringComparison.Ordinal);

    private static bool IsTapOrClick(string effectClass) => effectClass is "tap" or "click";
}
