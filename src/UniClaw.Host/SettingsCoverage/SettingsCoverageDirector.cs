using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>
/// AGT-005 — 覆盖遍历 director：包在 ConsultAgent 缝上的计划执法者。
/// 职责（全部留痕、零 Kernel 权威）：
/// 1. 每次咨询前从账本取 NextDirective，注入派生 AgentDecisionContext
///    （Objective 追加 COVERAGE DIRECTIVE 段；DecisionId/协议字段不动）。
/// 2. 计划符合性校验：模型的 Act 提案必须与当前 directive 语义一致
///    （单步、目标/effect 匹配、导航/滚动不带 desiredState）。偏离 →
///    有界纠正再咨询（MaxDirectiveRetries，同 DecisionId）；仍偏离 →
///    返回 null（fail closed），偏离进入 ConsultLog。
/// 3. 终局：directive 为 null 时返回 NoAction——coverage complete（在根页）
///    或终止条件触发（bounded stop），理由随 justification 留痕。
/// </summary>
public sealed class SettingsCoverageDirector
{
    public sealed record ConsultRecord(
        string DecisionId,
        string Phase,
        string? Directive,
        string? DirectiveKind,
        string? DecisionKind,
        string? DeviationReason,
        int Attempts,
        string? Justification,
        string? PolicyDigest = null,
        string? GuardVerdict = null)
    {
        internal CoverageDirective? SelectedDirective { get; init; }
    }

    private readonly Func<AgentDecisionContext, AgentDecision?> _underlying;
    private readonly SettingsCoverageLedger _ledger;
    private readonly SettingsCoverageConfig _config;
    private readonly SettingsCoverageRunner.CoverageStepJournal? _stepJournal;
    private readonly List<ConsultRecord> _log = new();
    private readonly List<CoverageStepAttribution> _adoptedSteps = new();
    private string? _terminalJustification;
    private int _obstacleAttempts;
    private (int StepsVerified, int Frontier)? _lastProgressSignal;
    private int _stagnantConsults;
    private int _sameRouteConsults;
    private string? _lastRoute;
    private string? _lastPopupState;
    private CoverageDirectiveKind? _lastAdoptedDirectiveKind;
    // Failure context is consumed by Kernel after one consultation. Retain it
    // only across that recovery's bounded Defer chain, never as a new Run phase.
    private string? _stepRecoveryReason;
    private readonly Queue<string> _recentCycleFacts = new();
    // TRAJECTORY-001：目标尝试计数（跨周期持久，不分 verified 与否——
    // SIM 循环每步都 verified 但零推进，现有连续未验证检测抓不到交替模式）。
    private readonly Dictionary<string, int> _targetAttemptCounts = new(StringComparer.Ordinal);
    private const int MaxTargetAttempts = 3;

    /// <summary>AGT-009：advisory Plan 的最大项数（超出 fail closed）。</summary>
    internal const int MaxPlanItems = 4;

    public SettingsCoverageDirector(
        Func<AgentDecisionContext, AgentDecision?> underlying,
        SettingsCoverageLedger ledger,
        SettingsCoverageConfig config,
        SettingsCoverageRunner.CoverageStepJournal? stepJournal = null,
        Func<(ObservationDiagnostics? Diagnostics, ViewportSummary? Viewport)>? localViewSources = null,
        Func<string?>? dialogTitleSource = null)
    {
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _stepJournal = stepJournal;
        _localViewSources = localViewSources;
        _dialogTitleSource = dialogTitleSource;
    }

    private readonly Func<(ObservationDiagnostics? Diagnostics, ViewportSummary? Viewport)>? _localViewSources;

    /// <summary>TRAJECTORY-001 D2：最新周期对话框标题（alertTitle 文本）；
    /// null = 无弹窗或无标题声明。</summary>
    private readonly Func<string?>? _dialogTitleSource;

    public IReadOnlyList<ConsultRecord> ConsultLog => _log;

    /// <summary>已采纳 Act 步骤（按采纳顺序；与 dispatch 顺序一一对应——
    /// grounding 失败不产生 receipt，由 runner 对照 receipts 处理）。</summary>
    public IReadOnlyList<(string DecisionId, string? Directive, AgentActionStep Step)> AdoptedSteps =>
        _adoptedSteps.Select(item => (item.DecisionId, item.Directive, item.Step)).ToArray();

    /// <summary>Set by the composition only when RunDriverInputs.GuardStep is wired.</summary>
    public bool CurrentStepGuardInstalled { get; set; }

    public string? TerminalJustification => _terminalJustification;

    /// <summary>
    /// 直连认知测试的 advisory 留痕：不生成 coverage directive、不校验计划、
    /// 不代替 Kernel 作任何授权。仅把模型实际返回的 Act/Plan ActItem 交给
    /// 既有 receipt↔step 归属账本，避免把 direct 模式误写成 director 证据。
    /// </summary>
    public AgentDecision? RecordAdvisoryDecision(AgentDecisionContext context, AgentDecision? decision)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ledger.RecordConsultRound(context.DecisionId);
        SettingsActionGuardResult? guard = null;
        if (_config.ActionPolicy is { } actionPolicy)
        {
            var original = decision;
            decision = SettingsActionGuard.GuardDecision(decision, context, actionPolicy, out guard, CurrentStepGuardInstalled);
            if (decision is null && ReviewResponseOf(original) is { } rejectedReview)
                decision = new AgentDecision.NoAction(
                    new AgentNoActionProposal(context.DecisionId, $"settings-policy:{guard?.Reason ?? "rejected"}"),
                    rejectedReview);
            else
                decision = TransferReviewResponse(original, decision);
        }
        foreach (var step in AdvisorySteps(decision))
            _adoptedSteps.Add(new(context.DecisionId, null, step));
        _log.Add(new ConsultRecord(
            context.DecisionId,
            context.Phase.ToString(),
            Directive: null,
            DirectiveKind: "advisory-direct",
            DecisionKind: DecisionKind(decision),
            DeviationReason: decision is null
                ? guard is null ? "no-response" : $"policy-guard:{guard.SemanticAction}:{guard.Reason}"
                : null,
            Attempts: 1,
            Justification: DecisionJustification(decision),
            PolicyDigest: guard?.PolicyDigest,
            GuardVerdict: guard?.Verdict.ToString().ToUpperInvariant()));
        return decision;
    }

    /// <summary>路由回退身份（DeriveRouteKey 无标题时的值）——
    /// 语义 = 当前页面身份未知（AGT-006）。</summary>
    internal const string UnknownRouteIdentity = "android.settings";


    /// <summary>快照前幂等补记钩子（runner 注入 bookkeeper.SyncNow——
    /// 保证首个观察在首次咨询前已入账本）。</summary>
    public Action? SyncBeforeSnapshot { get; set; }

    public AgentDecision? Consult(AgentDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 咨询开始 = 上一步骤的 verification 已可用：先把已配对的 receipts
        // 连同 verification/归属写入账本，快照才能反映最新覆盖进度。
        _stepJournal?.DrainAttributed(_adoptedSteps);
        SyncBeforeSnapshot?.Invoke();
        _ledger.RecordConsultRound(context.DecisionId);

        var snapshot = _ledger.Snapshot(_config);
        var directive = snapshot.NextDirective;
        var kind = snapshot.NextDirectiveKind;
        var selectedDirective = snapshot.SelectedDirective;
        // Popup clearance has its own typed present→absent evidence. Its
        // dismissed button disappearing can fail the generic target check;
        // that does not leave an unresolved navigation recovery behind.
        var popupCleared = _lastAdoptedDirectiveKind == CoverageDirectiveKind.Obstacle
            && _lastPopupState == "present" && snapshot.PopupState == "absent";
        if (popupCleared)
            _stepRecoveryReason = null;
        else if (context.Phase is AgentDecisionPhase.VerificationFailed or AgentDecisionPhase.StepRejected)
            _stepRecoveryReason = context.FailureReason ?? context.Phase.ToString();

        // AGT-019 M5（Q6）：no-progress 双零窗口——事实计数（新完成验证
        // 增量与 frontier 计数增量同时为零才计停滞，避开合法长验证段误报）；
        // 窗口达注入阈值 → 只注入事实计数（禁 Host 判语）；达硬停阈值 →
        // bounded-stop 同族机械兜底。
        var frontierCount = _ledger.DynamicRootFrontier(_config).Count
            + _ledger.DynamicNestedFrontier(_config).Count;
        if (_lastProgressSignal is { } prior
            && prior.StepsVerified == snapshot.StepsVerified
            && prior.Frontier == frontierCount)
            _stagnantConsults++;
        else
            _stagnantConsults = 0;
        _lastProgressSignal = (snapshot.StepsVerified, frontierCount);
        _sameRouteConsults = snapshot.CurrentRoute is not null && snapshot.CurrentRoute == _lastRoute
            ? _sameRouteConsults + 1
            : 0;
        _lastRoute = snapshot.CurrentRoute;
        // AGT-019 M2-②（B3）：局部视图（观察阶梯默认形态）——四族补缺中
        // 有真源的三族：容器结构（屏身份 + 可滚动性）、近周期趋势（本
        // Director 的有界事实行，窗口 3）；诊断/视口源在感知管线 seam
        //（后续片接入），诚实空不伪造。claims/elements 由既有字段承载。
        _recentCycleFacts.Enqueue(
            $"route={snapshot.CurrentRoute ?? "unknown"};verified={snapshot.StepsVerified};frontier={frontierCount}");
        while (_recentCycleFacts.Count > 3)
            _recentCycleFacts.Dequeue();
        var (diagnostics, viewport) = _localViewSources?.Invoke() ?? (null, null);
        context = context with
        {
            LocalView = new LocalObservationView(
                Containers: new[]
                {
                    new ContainerSummary(
                        context.Screen?.ContainerId ?? snapshot.CurrentRoute ?? "unknown",
                        IsScrollable: snapshot.Visible.Contains(_config.ScrollContainerDescriptor)),
                },
                Viewport: viewport,
                Diagnostics: diagnostics,
                RecentCycles: _recentCycleFacts.ToArray()),
        };
        // No-progress is evidence for Agent review, never a Host direction
        // verdict. Keep the legacy threshold readable for config compatibility,
        // but do not turn it into a bounded stop.
        // AGT-019 M5（B4）：注入阈值达成 → 停滞事实进协议字段
        // ConsultationProgress（取代 Slice A 的 Objective 文本注入——
        // 消冗余）；Progress 缺席（非 Kernel 正常组合）则诚实不造字段。
        if (_stagnantConsults >= _config.Bounds.StagnationInjectConsults
            && context.Progress is { } progress)
        {
            context = context with
            {
                Progress = progress with
                {
                    StagnantConsultations = _stagnantConsults,
                    SameRouteConsultations = _sameRouteConsults,
                },
            };
        }

        // Runtime normally resolves this barrier before consultation. An
        // unresolved Owner projection can never authorize a Host direction.
        if (snapshot.PopupState is "unknown" or "conflict")
        {
            _terminalJustification = "bounded-stop:popup-evidence-unresolved";
            _log.Add(new ConsultRecord(context.DecisionId, context.Phase.ToString(), null, null,
                "noAction", null, 0, _terminalJustification));
            return new AgentDecision.NoAction(new(context.DecisionId, _terminalJustification));
        }

        // Goal-directed Host mode contributes facts and guards effects, while
        // the Agent owns path choice and completion judgement. Every call is
        // one consultation; Runtime owns any correction or budget review.
        if (_config.Bounds.GoalDirectedContinuationEnabled)
        {
            var response = _underlying(context);
            var guarded = response;
            if (_config.ActionPolicy is { } reviewPolicy)
            {
                guarded = SettingsActionGuard.GuardDecision(response, context, reviewPolicy, out var reviewGuard, CurrentStepGuardInstalled);
                if (guarded is null)
                    guarded = new AgentDecision.NoAction(
                        new AgentNoActionProposal(context.DecisionId,
                            $"settings-policy:{reviewGuard?.Reason ?? "review-action-rejected"}"),
                        ReviewResponseOf(response));
                else
                    guarded = TransferReviewResponse(response, guarded);
            }
            foreach (var step in AdvisorySteps(guarded))
                _adoptedSteps.Add(new(context.DecisionId, null, step));
            _log.Add(new ConsultRecord(
                context.DecisionId, context.Phase.ToString(), null,
                context.BudgetReview is { Required: true } ? "budget-review" : "goal-directed",
                DecisionKind(guarded), null, 1, DecisionJustification(guarded)));
            return guarded;
        }

        // AGT-009 §1：弹窗在场优先于一切普通遍历指令——先清障，弹窗回到
        // absent 才恢复普通遍历。有界重试（config MaxObstacleRetries）；
        // 超界仍 present → fail closed bounded stop（理由进 ConsultLog）。
        if (snapshot.PopupState == "present")
        {
            // TRAJECTORY-001 D2：prior popup 声明留档（到达判据之一）。
            var priorPopupState = _lastPopupState;
            _lastPopupState = "present";
            if (_obstacleAttempts >= _config.PopupSettings.MaxObstacleRetries)
            {
                _terminalJustification = "bounded-stop:popup-not-cleared";
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), null, null,
                    "noAction", null, 0, "bounded-stop:popup-not-cleared"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    context.DecisionId, "settings-coverage:bounded-stop:popup-not-cleared"));
            }
            // TRAJECTORY-001 D2（到达即完成）：我们自己的 enter-tap 之后弹窗
            // 立即出现（prior absent → present），且对话框标题（alertTitle）
            // 与被进入目标模糊匹配（词重叠 ≥50%，& ≈ and）→ 该目标实际已
            // 到达目标页面（弹窗就是页面自身的确认/信息对话框）。落
            // ReachedOverlay 零 Effect mark，再走下方安全 dismiss（CANCEL/
            // back）关闭对话框继续遍历。不匹配 → 诚实落入既有 obstacle
            // 清障逻辑（不猜）。
            if (priorPopupState == "absent"
                && _lastAdoptedDirectiveKind is CoverageDirectiveKind.Enter or CoverageDirectiveKind.NestedEnter or CoverageDirectiveKind.ReEnter
                && _adoptedSteps.Count > 0
                && _adoptedSteps[^1].Step.TargetDescriptor is { } arrivalTarget
                && _dialogTitleSource?.Invoke() is { Length: > 0 } dialogTitle
                && TitleMatchesTarget(dialogTitle, arrivalTarget))
            {
                _ledger.TryMarkTargetReached(
                    arrivalTarget, TargetCoverageMarkKind.ReachedOverlay,
                    context.DecisionId, snapshot.CurrentRoute);
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), null,
                    "arrival-detected", null, null, 0,
                    $"arrival-detected: dialog title '{dialogTitle}' matches target '{arrivalTarget}'"));
            }
            // AGT-019 M4（Q5 裁决）：弹窗处置任务型白名单——遍历型任务没有
            // confirm 权限。候选词表先过禁手清单（确认侧目标永不中选）；
            // 无安全词可用时用返回键 dismiss（back/cancel 直点）；返回键也
            // 未配置 → 暂停+重授权是唯一出路（不变量 47），绝不点 confirm。
            var deny = _config.PopupSettings.DenyTargets;
            var obstacle = _config.PopupSettings.Targets
                .FirstOrDefault(target => snapshot.Visible.Contains(target) && !deny.Contains(target));
            if (obstacle is not null)
            {
                _obstacleAttempts++;
                directive =
                    $"Clear the popup overlay by tapping '<{obstacle}>' before resuming traversal.";
                kind = "obstacle";
                selectedDirective = new(CoverageDirectiveKind.Obstacle, obstacle, directive);
            }
            else if (_config.BackDescriptor is not null)
            {
                _obstacleAttempts++;
                directive =
                    $"Dismiss the popup overlay by tapping '<{_config.BackDescriptor}>' (back). "
                    + "Confirm-side buttons are not allowed for this task.";
                kind = "obstacle";
                selectedDirective = new(CoverageDirectiveKind.Obstacle, _config.BackDescriptor, directive);
            }
            else
            {
                _terminalJustification = "bounded-stop:popup-confirm-requires-reauthorization";
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), null, "obstacle",
                    "noAction", null, 0, "bounded-stop:popup-confirm-requires-reauthorization"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    context.DecisionId, "settings-coverage:bounded-stop:popup-confirm-requires-reauthorization"));
            }
        }
        else if (snapshot.PopupState == "absent")
        {
            _obstacleAttempts = 0; // 清障成功：恢复普通遍历预算
            _lastPopupState = "absent";
        }

        // AGT-006 修复：路由回退身份（页面无标题，如启动过渡屏）≠ 已知
        // 非根页——"back" 会指挥幻影目标。未知页面 → Defer 有界重观察，
        // 等屏幕定形；配额尽（DeferRoundsExhausted 相位）→ 诚实 bounded stop。
        var unknownRouteNeedsResample = snapshot.CurrentRoute == UnknownRouteIdentity
            && !snapshot.Visible.Contains(_config.ScrollContainerDescriptor)
            && !_config.TargetPages.Any(snapshot.Visible.Contains)
            && !_ledger.DynamicRootFrontier(_config).Any(snapshot.Visible.Contains);
        var recoveringFailedStep = _stepRecoveryReason is not null;
        if (snapshot.PopupState != "present"
            && (recoveringFailedStep || snapshot.CurrentRoute == UnknownRouteIdentity
                && (snapshot.StepsVerified == 0 || unknownRouteNeedsResample)))
        {
            if (context.Phase == AgentDecisionPhase.DeferRoundsExhausted)
            {
                _terminalJustification = recoveringFailedStep
                    ? "bounded-stop:step-recovery-exhausted" : "bounded-stop:unknown-page";
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), null, null,
                    "noAction", null, 0, _terminalJustification));
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    context.DecisionId, $"settings-coverage:{_terminalJustification}"));
            }
            // AGT-019 M1（Q7 裁决）：处理不了的默认升级 UniAgent——路由不明
            // 是强制升级谓词之一。升级咨询给选项集 {再观察, back（配置了
            // 才给）, 滚动（容器可见才给）}；选项外的 Act/Plan/Policy 照拒、
            // 零 Effect（004-A2 等价执法，仍返回安全 Defer）。
            var scrollVisible = snapshot.Visible.Contains(_config.ScrollContainerDescriptor);
            var backAvailable = _config.BackDescriptor is not null
                && (!recoveringFailedStep || snapshot.Visible.Contains(_config.BackDescriptor));
            var options = new List<string> { "re-observe (return a Defer for one bounded re-observation)" };
            if (backAvailable)
                options.Add($"back (tap '<{_config.BackDescriptor}>')");
            if (scrollVisible)
                options.Add($"scroll (swipe up on '<{_config.ScrollContainerDescriptor}>')");
            options.Add("declare blocked with a reason (the run ends honestly as blocked)");
            if (recoveringFailedStep)
                options.Add("stop honestly (return noAction with the recovery limitation)");
            var recoveryKind = recoveringFailedStep ? "failed-step-recovery" : "defer-unknown-page";
            var recoveryReason = recoveringFailedStep
                ? $"the previous action failed ({_stepRecoveryReason}); ordinary coverage traversal is paused for recovery"
                : "the current page identity is unknown";
            _log.Add(new ConsultRecord(
                context.DecisionId, context.Phase.ToString(),
                recoveringFailedStep ? recoveryReason
                    : $"Observe again: current page identity is unknown ('{UnknownRouteIdentity}').",
                recoveryKind, "defer", null, 1, _stepRecoveryReason));
            var observeContext = context with
            {
                Objective = context.Objective
                    + "\n\nCOVERAGE DIRECTIVE (authoritative for this turn): "
                    + recoveryReason + "."
                    + "\nChoose EXACTLY ONE recovery option: " + string.Join("; ", options) + "."
                    + "\nAny other Act, Plan, or Policy response is rejected without Effect."
            };
            var agentDecision = _underlying(observeContext);
            var sameDecision = string.Equals(
                DecisionIdOf(agentDecision),
                context.DecisionId,
                StringComparison.Ordinal);
            if (agentDecision is AgentDecision.Defer && sameDecision)
                return agentDecision;
            if (recoveringFailedStep && agentDecision is AgentDecision.NoAction noAction && sameDecision)
            {
                _stepRecoveryReason = null;
                _log[^1] = _log[^1] with { DecisionKind = "noAction", Justification = noAction.Proposal.Justification };
                return agentDecision;
            }
            // AGT-019 M1（Q1/B1）：blocked 诚实申报——同号即采纳透传，
            // Kernel 侧零 Effect 确定性终局（agent-blocked:{reason}）。
            if (agentDecision is AgentDecision.Blocked blockedAnswer && sameDecision)
            {
                _stepRecoveryReason = null;
                _log[^1] = _log[^1] with
                {
                    DecisionKind = "blocked",
                    Justification = blockedAnswer.Reason,
                };
                return agentDecision;
            }
            if (agentDecision is AgentDecision.Act act
                && sameDecision
                && act.Proposal.Steps.Count == 1
                && IsUnknownRouteRecoveryStep(act.Proposal.Steps[0], backAvailable, scrollVisible))
            {
                var recovery = act.Proposal.Steps[0];
                var guard = _config.ActionPolicy is { } recoveryPolicy
                    ? SettingsActionGuard.Evaluate(recovery, context, recoveryPolicy) : null;
                if (guard is not null && guard.Verdict != SettingsActionGuardVerdict.Allow)
                {
                    _log[^1] = _log[^1] with
                    {
                        DecisionKind = "defer", DeviationReason = $"policy-guard:{guard.SemanticAction}:{guard.Reason}",
                        PolicyDigest = guard.PolicyDigest, GuardVerdict = guard.Verdict.ToString().ToUpperInvariant(),
                    };
                    return TransferReviewResponse(agentDecision,
                        new AgentDecision.Defer(context.DecisionId, new ObserveSpec(null, 2)));
                }
                var location = recoveringFailedStep ? "during failed-step recovery" : "on the unknown page";
                var recoveryDirective = recovery.EffectClass == "swipe-up"
                    ? $"Scroll the container '<{_config.ScrollContainerDescriptor}>' {location}."
                    : $"Navigate back {location} by tapping '<{_config.BackDescriptor}>'.";
                var recoverySelection = new CoverageDirective(
                    recovery.EffectClass == "swipe-up" ? CoverageDirectiveKind.Scroll : CoverageDirectiveKind.Back,
                    recovery.TargetDescriptor, recoveryDirective);
                _adoptedSteps.Add(new(context.DecisionId, recoveryDirective, recovery, recoverySelection));
                _log[^1] = _log[^1] with
                { Directive = recoveryDirective, DecisionKind = "act", SelectedDirective = recoverySelection };
                _stepRecoveryReason = null;
                return agentDecision;
            }

            // An agent response cannot authorize an action outside the option
            // set while route identity is unknown. Preserve the fail-closed
            // Defer even when the agent times out, returns null, or proposes
            // Act/Plan/Policy outside {back, scroll}.
            var reason = agentDecision is null
                ? recoveringFailedStep ? "failed-step-agent-no-response" : "unknown-route-agent-no-response"
                : recoveringFailedStep ? "failed-step-agent-response-rejected" : "unknown-route-agent-response-rejected";
            _log[^1] = _log[^1] with
            {
                DecisionKind = DecisionKind(agentDecision),
                DeviationReason = reason,
            };
            return TransferReviewResponse(agentDecision, new AgentDecision.Defer(context.DecisionId,
                new ObserveSpec(Subject: null, MaxRounds: 2)));
        }


        // 本地候选排除不是等待：写入成功后在同次咨询中重新选择。
        // 每次迭代排除一个候选；账本拒绝写入则停止，不能伪造 skip 日志。
        while (kind is "enter" or "nested-enter" or "re-enter")
        {
            var target = selectedDirective?.TargetDescriptor;
            if (target is null)
                break;

            var isSwitch = _config.ActionPolicy?.DeclaredTargetSwitchLabels.Any(label =>
                string.Equals(label, target, StringComparison.OrdinalIgnoreCase)) == true
                || (context.Elements ?? Array.Empty<ElementSummary>()).Any(element =>
                    string.Equals(element.Text?.Trim(), target, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(element.Role, "switch", StringComparison.OrdinalIgnoreCase)
                        || element.Checkable == true));
            _targetAttemptCounts.TryGetValue(target, out var attemptsForTarget);
            if (!isSwitch)
            {
                attemptsForTarget++;
                _targetAttemptCounts[target] = attemptsForTarget;
            }
            if (!isSwitch && attemptsForTarget <= MaxTargetAttempts)
                break;

            var reason = isSwitch
                ? "switch-target: navigation on toggle control not guardable"
                : $"auto-skip: {attemptsForTarget} attempts without coverage completion";
            var evidence = isSwitch ? "switch-target" : $"target-attempts:{attemptsForTarget}";
            if (!_ledger.TryMarkTargetSkipped(target, context.DecisionId, reason, new[] { evidence }))
            {
                _terminalJustification = "bounded-stop:coverage-mark-conflict";
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), directive, kind,
                    "noAction", "coverage-mark-conflict", 0,
                    $"coverage mark rejected for '{target}'; original mark preserved"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    context.DecisionId, $"settings-coverage:{_terminalJustification}"));
            }
            _log.Add(new ConsultRecord(
                context.DecisionId, context.Phase.ToString(), directive, kind,
                "skip", null, 0, reason + $"; target '{target}'"));
            snapshot = _ledger.Snapshot(_config);
            directive = snapshot.NextDirective;
            kind = snapshot.NextDirectiveKind;
            selectedDirective = snapshot.SelectedDirective;
        }

        // 子页待办耗尽后返回；不得构造缺失的返回控件。
        if (directive is null
            && snapshot.CurrentRoute is not null
            && snapshot.CurrentRoute != _config.RootRoute
            && _config.BackDescriptor is not null)
        {
            directive = $"Return to the Settings root page by tapping '<{_config.BackDescriptor}>'.";
            kind = "back";
            selectedDirective = new(CoverageDirectiveKind.Back, _config.BackDescriptor, directive);
        }

        if (directive is null)
        {
            var justification = snapshot.CoverageComplete
                ? "coverage-complete"
                : $"bounded-stop:{TerminationReason(snapshot)}";
            _terminalJustification = justification;
            _log.Add(new ConsultRecord(
                context.DecisionId, context.Phase.ToString(), null, null,
                "noAction", null, 0, justification));
            return new AgentDecision.NoAction(new AgentNoActionProposal(
                context.DecisionId, $"settings-coverage:{justification}"));
        }

        var derived = context with
        {
            Objective = context.Objective
                + "\n\nCOVERAGE DIRECTIVE (authoritative for this turn): "
                + directive
                + "\nRespond with an act proposal containing EXACTLY ONE step that performs "
                + "this directive. Copy targetDescriptor exactly from the element list. "
                + "Navigation and scroll targets must omit desiredState (typed switch states "
                + "only; never invent one)."
                + (_config.ActionPolicy is { } policy
                    ? "\n\nSETTINGS ACTION POLICY (read-only; Runtime Guard is authoritative): "
                      + policy.AgentProjection()
                    : string.Empty),
        };

        var attempts = 0;
        string? deviation = null;
        string? lastDeviation = null;
        BudgetReviewResponse? lastReviewResponse = null;
        var retryLimit = _config.Bounds.GoalDirectedContinuationEnabled
            ? 0
            : _config.Bounds.MaxDirectiveRetries;
        while (attempts <= retryLimit)
        {
            attempts++;
            var decision = _underlying(derived);
            lastReviewResponse = ReviewResponseOf(decision) ?? lastReviewResponse;
            // TRAJECTORY-001：Skip/Blocked 是零 Effect 介入裁决——在
            // adherence/guard/journal 之前直接透传（它们没有 action step，
            // 走 Act 管线会空引用）。Kernel 已有处理逻辑。
            if (decision is AgentDecision.Skip skipDecision)
            {
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), directive, kind,
                    "skip", null, attempts, skipDecision.Reason));
                return decision;
            }
            if (decision is AgentDecision.Blocked blockedDecision)
            {
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), directive, kind,
                    "blocked", null, attempts, blockedDecision.Reason));
                return decision;
            }
            AgentActionStep? adoptedStep = null;
            deviation = decision is null
                ? "no-response"
                : ValidateAdherence(decision, selectedDirective!, kind!, out adoptedStep);
            var adopted = adoptedStep;
            if (deviation is null)
            {
                var guard = _config.ActionPolicy is { } actionPolicy
                    ? SettingsActionGuard.Evaluate(adopted!, context, actionPolicy)
                    : null;
                if (guard is { Verdict: SettingsActionGuardVerdict.Reject })
                {
                    lastDeviation = $"policy-guard:{guard.SemanticAction}:{guard.Reason}";
                    _log.Add(new ConsultRecord(
                        context.DecisionId, context.Phase.ToString(), directive, kind,
                        decision is AgentDecision.Plan ? "plan" : "act",
                        lastDeviation, attempts, null, guard.PolicyDigest, "REJECT"));
                    return _config.Bounds.GoalDirectedContinuationEnabled
                        && ReviewResponseOf(decision) is { } reviewResponse
                            ? new AgentDecision.NoAction(new AgentNoActionProposal(
                                context.DecisionId, $"settings-policy:{guard.SemanticAction}:{guard.Reason}"), reviewResponse)
                            : null;
                }
                if (guard is { Verdict: SettingsActionGuardVerdict.NoAction })
                {
                    _log.Add(new ConsultRecord(
                        context.DecisionId, context.Phase.ToString(), directive, kind,
                        "noAction", null, attempts, guard.Reason, guard.PolicyDigest, "NO_ACTION"));
                    return new AgentDecision.NoAction(new AgentNoActionProposal(
                        context.DecisionId, $"settings-policy:{guard.Reason}"), ReviewResponseOf(decision));
                }
                // Act 单步与受约束 Plan 的唯一 ActItem 都物化为 AgentActionStep
                // 归属（归因/journal 路径不变；Plan 的执行由 driver 负责）。
                _adoptedSteps.Add(new(context.DecisionId, directive, adopted!, selectedDirective));
                // TRAJECTORY-001 D2：记录采纳指令 kind（到达判据之一）。
                _lastAdoptedDirectiveKind = selectedDirective!.Kind;
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), directive, kind,
                    decision is AgentDecision.Plan ? "plan" : "act",
                    lastDeviation, attempts,
                    decision switch
                    {
                        AgentDecision.Act act => act.Proposal.Justification,
                        AgentDecision.Plan plan => plan.Proposal.Justification,
                        _ => null,
                    },
                    guard?.PolicyDigest,
                    guard?.Verdict.ToString().ToUpperInvariant()) { SelectedDirective = selectedDirective });
                return decision;
            }
            lastDeviation = deviation;
            if (deviation == "no-response")
                break; // fail closed：底层无回答，纠正再咨询无意义
        }

        _log.Add(new ConsultRecord(
            context.DecisionId, context.Phase.ToString(), directive, kind,
            null, deviation, attempts, null));
        return _config.Bounds.GoalDirectedContinuationEnabled
            && lastReviewResponse is { } pendingReview
                ? new AgentDecision.NoAction(new AgentNoActionProposal(
                    context.DecisionId, $"settings-directive-rejected:{deviation ?? "unknown"}"), pendingReview)
                : null; // fail closed
    }

    /// <summary>
    /// AGT-009 §11：计划符合性校验。接受两种形态：
    /// (a) Act 单步（原行为）；(b) 受约束 advisory Plan——Items.Count &lt;= 4、
    /// 至多一个 ActItem 且必须满足与 Act 相同的 directive 匹配、其余项只能是
    /// ObserveItem/ControlItem。其余（多 ActItem、Act 失配、未知项、超尺寸）
    /// → deviation，fail closed 零 Effect。被采纳的 ActItem 经
    /// <paramref name="adopted"/> 物化为 AgentActionStep（归因/journal 不变）。
    /// </summary>
    private string? ValidateAdherence(AgentDecision decision, CoverageDirective directive, string kind, out AgentActionStep? adopted)
    {
        adopted = null;
        if (decision is AgentDecision.Plan plan)
        {
            if (plan.Proposal.Items.Count > MaxPlanItems)
                return $"plan-oversized:{plan.Proposal.Items.Count}";
            AgentActionStep? actStep = null;
            foreach (var item in plan.Proposal.Items)
            {
                switch (item)
                {
                    case PlanItem.ActItem act:
                        if (actStep is not null)
                            return "plan-multiple-acts";
                        actStep = new AgentActionStep(act.TargetRole, act.TargetDescriptor,
                            act.EffectClass, act.DesiredState);
                        break;
                    case PlanItem.ObserveItem:
                    case PlanItem.ControlItem:
                        break;
                    default:
                        return "plan-unknown-item";
                }
            }
            if (actStep is null)
                return "plan-requires-single-act";
            if (actStep.DesiredState is not null)
                return "plan-act-must-not-carry-desiredState";
            var actDeviation = ValidateStepAgainstDirective(actStep, directive);
            if (actDeviation is not null)
                return $"plan-{actDeviation}";
            adopted = actStep;
            return null;
        }
        if (decision is not AgentDecision.Act actDecision)
            return $"expected-act-for-directive:{kind}";
        if (actDecision.Proposal.Steps.Count != 1)
            return $"directive-requires-single-step:{actDecision.Proposal.Steps.Count}";
        var step = actDecision.Proposal.Steps[0];
        if (step.DesiredState is not null)
            return "directive-step-must-not-carry-desiredState";
        var deviation = ValidateStepAgainstDirective(step, directive);
        if (deviation is null)
            adopted = step;
        return deviation;
    }

    /// <summary>单步与 directive 的匹配规则（Act 与 Plan ActItem 同律）。</summary>
    private string? ValidateStepAgainstDirective(AgentActionStep step, CoverageDirective directive) =>
        directive.Kind switch
        {
            CoverageDirectiveKind.Enter or CoverageDirectiveKind.ReEnter or CoverageDirectiveKind.NestedEnter => IsTap(step) && step.TargetDescriptor == directive.TargetDescriptor
                ? null
                : $"directive-target-mismatch:{step.TargetDescriptor ?? "<null>"}",
            CoverageDirectiveKind.Scroll => step.EffectClass == "swipe-up"
                && step.TargetDescriptor == _config.ScrollContainerDescriptor
                ? null
                : $"directive-scroll-mismatch:{step.EffectClass}:{step.TargetDescriptor ?? "<null>"}",
            CoverageDirectiveKind.Back => IsTap(step) && step.TargetDescriptor == _config.BackDescriptor
                ? null
                : $"directive-back-mismatch:{step.TargetDescriptor ?? "<null>"}",
            CoverageDirectiveKind.Obstacle => IsTap(step) && step.TargetDescriptor == directive.TargetDescriptor
                ? null
                : $"directive-obstacle-mismatch:{step.TargetDescriptor ?? "<null>"}",
            _ => "unknown-directive-kind",
        };

    private static bool IsTap(AgentActionStep step) =>
        step.EffectClass is "tap" or "click";

    /// <summary>AGT-019 M1：路由不明升级咨询的选项集执法——仅接受单步
    /// back（tap 返回键，须已配置）或滚动（swipe-up 滚动容器，须容器
    /// 可见）；其余响应零 Effect（004-A2 等价，防漂移与幻影目标）。</summary>
    private bool IsUnknownRouteRecoveryStep(AgentActionStep step, bool backAvailable, bool scrollVisible) =>
        step.DesiredState is null
        && (backAvailable && IsTap(step) && step.TargetDescriptor == _config.BackDescriptor
            || scrollVisible && step.EffectClass == "swipe-up" && step.TargetDescriptor == _config.ScrollContainerDescriptor);

    private static IEnumerable<AgentActionStep> AdvisorySteps(AgentDecision? decision) =>
        decision switch
        {
            AgentDecision.Act act => act.Proposal.Steps,
            AgentDecision.Plan plan => plan.Proposal.Items
                .OfType<PlanItem.ActItem>()
                .Select(item => new AgentActionStep(
                    item.TargetRole, item.TargetDescriptor, item.EffectClass, item.DesiredState)),
            _ => Array.Empty<AgentActionStep>(),
        };

    private static string? DecisionKind(AgentDecision? decision) => decision switch
    {
        AgentDecision.Act => "act",
        AgentDecision.Plan => "plan",
        AgentDecision.NoAction => "noAction",
        AgentDecision.Defer => "defer",
        AgentDecision.Policy => "policy",
        AgentDecision.Skip => "skip",
        null => null,
        _ => "unknown",
    };

    private static string? DecisionIdOf(AgentDecision? decision) => decision switch
    {
        AgentDecision.Act act => act.Proposal.DecisionId,
        AgentDecision.Plan plan => plan.DecisionId,
        AgentDecision.NoAction noAction => noAction.Proposal.DecisionId,
        AgentDecision.Defer defer => defer.DecisionId,
        AgentDecision.Policy policy => policy.DecisionId,
        AgentDecision.Blocked blocked => blocked.DecisionId,
        AgentDecision.Skip skip => skip.DecisionId,
        null => null,
        _ => null,
    };

    private static BudgetReviewResponse? ReviewResponseOf(AgentDecision? decision) => decision switch
    {
        AgentDecision.Act x => x.ReviewResponse,
        AgentDecision.NoAction x => x.ReviewResponse,
        AgentDecision.Defer x => x.ReviewResponse,
        AgentDecision.Policy x => x.ReviewResponse,
        AgentDecision.Plan x => x.ReviewResponse,
        AgentDecision.ContinuePlan x => x.ReviewResponse,
        AgentDecision.Blocked x => x.ReviewResponse,
        AgentDecision.Skip x => x.ReviewResponse,
        _ => null,
    };

    private static AgentDecision? TransferReviewResponse(AgentDecision? source, AgentDecision? destination)
    {
        var review = ReviewResponseOf(source);
        if (review is null)
            return destination;
        return destination switch
        {
            AgentDecision.Act x => x with { ReviewResponse = review },
            AgentDecision.NoAction x => x with { ReviewResponse = review },
            AgentDecision.Defer x => x with { ReviewResponse = review },
            AgentDecision.Policy x => x with { ReviewResponse = review },
            AgentDecision.Plan x => x with { ReviewResponse = review },
            AgentDecision.ContinuePlan x => x with { ReviewResponse = review },
            AgentDecision.Blocked x => x with { ReviewResponse = review },
            AgentDecision.Skip x => x with { ReviewResponse = review },
            _ => destination,
        };
    }

    private static string? DecisionJustification(AgentDecision? decision) => decision switch
    {
        AgentDecision.Act act => act.Proposal.Justification,
        AgentDecision.Plan plan => plan.Proposal.Justification,
        AgentDecision.NoAction noAction => noAction.Proposal.Justification,
        AgentDecision.Skip skip => skip.Reason,
        _ => null,
    };

    /// <summary>
    /// TRAJECTORY-001 D2 — 对话框标题与 directive 目标的模糊匹配：小写化、
    /// "&" 归一为 "and"、按非字母数字切词；词集交集/并集（Jaccard）≥ 50%
    /// 判匹配（"SIM label and color" ≈ "SIM name &amp; color"：3/5 = 60%）。
    /// 任一侧无词 → 不匹配（不猜）。
    /// </summary>
    internal static bool TitleMatchesTarget(string title, string target)
    {
        var titleWords = Words(title);
        var targetWords = Words(target);
        if (titleWords.Count == 0 || targetWords.Count == 0)
            return false;
        var intersection = titleWords.Count(targetWords.Contains);
        var union = titleWords.Union(targetWords).Count();
        return union > 0 && (double)intersection / union >= 0.5;

        static HashSet<string> Words(string raw)
        {
            var text = new string(raw
                .Replace("&", " and ", StringComparison.Ordinal)
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
                .ToArray());
            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                words.Add(word.ToLowerInvariant());
            return words;
        }
    }

    private string TerminationReason(CoverageSnapshot snapshot) =>
        snapshot.MaxConsultRoundsReached ? "max-consult-rounds"
        : snapshot.MaxStepsReached ? "max-steps"
        : snapshot.MaxScrollsReached ? "max-scrolls"
        : snapshot.MaxConsecutiveFailuresReached ? "max-consecutive-failures"
        : "coverage-blocked";
}
