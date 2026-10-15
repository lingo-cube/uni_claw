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
        string? GuardVerdict = null);

    private readonly Func<AgentDecisionContext, AgentDecision?> _underlying;
    private readonly SettingsCoverageLedger _ledger;
    private readonly SettingsCoverageConfig _config;
    private readonly SettingsCoverageRunner.CoverageStepJournal? _stepJournal;
    private readonly List<ConsultRecord> _log = new();
    private readonly List<(string DecisionId, string? Directive, AgentActionStep Step)> _adoptedSteps = new();
    private string? _terminalJustification;
    private int _obstacleAttempts;
    private (int StepsVerified, int Frontier)? _lastProgressSignal;
    private int _stagnantConsults;
    private int _sameRouteConsults;
    private string? _lastRoute;

    /// <summary>AGT-009：advisory Plan 的最大项数（超出 fail closed）。</summary>
    internal const int MaxPlanItems = 4;

    public SettingsCoverageDirector(
        Func<AgentDecisionContext, AgentDecision?> underlying,
        SettingsCoverageLedger ledger,
        SettingsCoverageConfig config,
        SettingsCoverageRunner.CoverageStepJournal? stepJournal = null)
    {
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _stepJournal = stepJournal;
    }

    public IReadOnlyList<ConsultRecord> ConsultLog => _log;

    /// <summary>已采纳 Act 步骤（按采纳顺序；与 dispatch 顺序一一对应——
    /// grounding 失败不产生 receipt，由 runner 对照 receipts 处理）。</summary>
    public IReadOnlyList<(string DecisionId, string? Directive, AgentActionStep Step)> AdoptedSteps => _adoptedSteps;

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
            decision = SettingsActionGuard.GuardDecision(decision, context, actionPolicy, out guard);
        foreach (var step in AdvisorySteps(decision))
            _adoptedSteps.Add((context.DecisionId, null, step));
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
        _stepJournal?.Drain(_adoptedSteps);
        SyncBeforeSnapshot?.Invoke();
        _ledger.RecordConsultRound(context.DecisionId);

        var snapshot = _ledger.Snapshot(_config);
        var directive = snapshot.NextDirective;
        var kind = snapshot.NextDirectiveKind;

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
        if (_stagnantConsults >= _config.Bounds.StagnationStopConsults)
        {
            _terminalJustification = "bounded-stop:no-progress";
            _log.Add(new ConsultRecord(
                context.DecisionId, context.Phase.ToString(), null, null,
                "noAction", null, 0, "bounded-stop:no-progress"));
            return new AgentDecision.NoAction(new AgentNoActionProposal(
                context.DecisionId, "settings-coverage:bounded-stop:no-progress"));
        }
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

        // AGT-009 §1：弹窗在场优先于一切普通遍历指令——先清障，弹窗回到
        // absent 才恢复普通遍历。有界重试（config MaxObstacleRetries）；
        // 超界仍 present → fail closed bounded stop（理由进 ConsultLog）。
        if (snapshot.PopupState == "present")
        {
            if (_obstacleAttempts >= _config.PopupSettings.MaxObstacleRetries)
            {
                _terminalJustification = "bounded-stop:popup-not-cleared";
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), null, null,
                    "noAction", null, 0, "bounded-stop:popup-not-cleared"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    context.DecisionId, "settings-coverage:bounded-stop:popup-not-cleared"));
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
            }
            else if (_config.BackDescriptor is not null)
            {
                _obstacleAttempts++;
                directive =
                    $"Dismiss the popup overlay by tapping '<{_config.BackDescriptor}>' (back). "
                    + "Confirm-side buttons are not allowed for this task.";
                kind = "obstacle";
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
        }

        // AGT-006 修复：路由回退身份（页面无标题，如启动过渡屏）≠ 已知
        // 非根页——"back" 会指挥幻影目标。未知页面 → Defer 有界重观察，
        // 等屏幕定形；配额尽（DeferRoundsExhausted 相位）→ 诚实 bounded stop。
        var unknownRouteNeedsResample = snapshot.CurrentRoute == UnknownRouteIdentity
            && !snapshot.Visible.Contains(_config.ScrollContainerDescriptor)
            && !_config.TargetPages.Any(snapshot.Visible.Contains)
            && !_ledger.DynamicRootFrontier(_config).Any(snapshot.Visible.Contains);
        if (snapshot.PopupState != "present"
            && snapshot.CurrentRoute is not null
            && snapshot.CurrentRoute == UnknownRouteIdentity
            && (snapshot.StepsVerified == 0 || unknownRouteNeedsResample))
        {
            if (context.Phase == AgentDecisionPhase.DeferRoundsExhausted)
            {
                _terminalJustification = "bounded-stop:unknown-page";
                _log.Add(new ConsultRecord(
                    context.DecisionId, context.Phase.ToString(), null, null,
                    "noAction", null, 0, "bounded-stop:unknown-page"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    context.DecisionId, "settings-coverage:bounded-stop:unknown-page"));
            }
            // AGT-019 M1（Q7 裁决）：处理不了的默认升级 UniAgent——路由不明
            // 是强制升级谓词之一。升级咨询给选项集 {再观察, back（配置了
            // 才给）, 滚动（容器可见才给）}；选项外的 Act/Plan/Policy 照拒、
            // 零 Effect（004-A2 等价执法，仍返回安全 Defer）。
            var scrollVisible = snapshot.Visible.Contains(_config.ScrollContainerDescriptor);
            var backAvailable = _config.BackDescriptor is not null;
            var options = new List<string> { "re-observe (return a Defer for one bounded re-observation)" };
            if (backAvailable)
                options.Add($"back (tap '<{_config.BackDescriptor}>')");
            if (scrollVisible)
                options.Add($"scroll (swipe up on '<{_config.ScrollContainerDescriptor}>')");
            options.Add("declare blocked with a reason (the run ends honestly as blocked)");
            _log.Add(new ConsultRecord(
                context.DecisionId, context.Phase.ToString(),
                $"Observe again: current page identity is unknown ('{UnknownRouteIdentity}').",
                "defer-unknown-page", "defer", null, 1, null));
            var observeContext = context with
            {
                Objective = context.Objective
                    + "\n\nCOVERAGE DIRECTIVE (authoritative for this turn): "
                    + "the current page identity is unknown."
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
            // AGT-019 M1（Q1/B1）：blocked 诚实申报——同号即采纳透传，
            // Kernel 侧零 Effect 确定性终局（agent-blocked:{reason}）。
            if (agentDecision is AgentDecision.Blocked blockedAnswer && sameDecision)
            {
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
                var recoveryDirective = recovery.EffectClass == "swipe-up"
                    ? $"Scroll the container '<{_config.ScrollContainerDescriptor}>' on the unknown page."
                    : $"Navigate back from the unknown page by tapping '<{_config.BackDescriptor}>'.";
                _adoptedSteps.Add((context.DecisionId, recoveryDirective, recovery));
                _log[^1] = _log[^1] with { Directive = recoveryDirective, DecisionKind = "act" };
                return agentDecision;
            }

            // An agent response cannot authorize an action outside the option
            // set while route identity is unknown. Preserve the fail-closed
            // Defer even when the agent times out, returns null, or proposes
            // Act/Plan/Policy outside {back, scroll}.
            var reason = agentDecision is null
                ? "unknown-route-agent-no-response"
                : "unknown-route-agent-response-rejected";
            _log[^1] = _log[^1] with
            {
                DecisionKind = DecisionKind(agentDecision),
                DeviationReason = reason,
            };
            return new AgentDecision.Defer(context.DecisionId,
                new ObserveSpec(Subject: null, MaxRounds: 2));
        }


        // 覆盖完成后若不在根页，终局义务（end-on-route）要求先返回根页。
        if (directive is null
            && snapshot.CoverageComplete
            && snapshot.CurrentRoute is not null
            && snapshot.CurrentRoute != _config.RootRoute)
        {
            directive = $"Return to the Settings root page by tapping '<{_config.BackDescriptor}>'.";
            kind = "back";
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
        while (attempts <= _config.Bounds.MaxDirectiveRetries)
        {
            attempts++;
            var decision = _underlying(derived);
            AgentActionStep? adoptedStep = null;
            deviation = decision is null
                ? "no-response"
                : ValidateAdherence(decision, directive, kind!, out adoptedStep);
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
                    return null;
                }
                if (guard is { Verdict: SettingsActionGuardVerdict.NoAction })
                {
                    _log.Add(new ConsultRecord(
                        context.DecisionId, context.Phase.ToString(), directive, kind,
                        "noAction", null, attempts, guard.Reason, guard.PolicyDigest, "NO_ACTION"));
                    return new AgentDecision.NoAction(new AgentNoActionProposal(
                        context.DecisionId, $"settings-policy:{guard.Reason}"));
                }
                // Act 单步与受约束 Plan 的唯一 ActItem 都物化为 AgentActionStep
                // 归属（归因/journal 路径不变；Plan 的执行由 driver 负责）。
                _adoptedSteps.Add((context.DecisionId, directive, adopted!));
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
                    guard?.Verdict.ToString().ToUpperInvariant()));
                return decision;
            }
            lastDeviation = deviation;
            if (deviation == "no-response")
                break; // fail closed：底层无回答，纠正再咨询无意义
        }

        _log.Add(new ConsultRecord(
            context.DecisionId, context.Phase.ToString(), directive, kind,
            null, deviation, attempts, null));
        return null; // fail closed
    }

    /// <summary>
    /// AGT-009 §11：计划符合性校验。接受两种形态：
    /// (a) Act 单步（原行为）；(b) 受约束 advisory Plan——Items.Count &lt;= 4、
    /// 至多一个 ActItem 且必须满足与 Act 相同的 directive 匹配、其余项只能是
    /// ObserveItem/ControlItem。其余（多 ActItem、Act 失配、未知项、超尺寸）
    /// → deviation，fail closed 零 Effect。被采纳的 ActItem 经
    /// <paramref name="adopted"/> 物化为 AgentActionStep（归因/journal 不变）。
    /// </summary>
    private string? ValidateAdherence(AgentDecision decision, string directive, string kind, out AgentActionStep? adopted)
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
            var actDeviation = ValidateStepAgainstDirective(actStep, directive, kind);
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
        var deviation = ValidateStepAgainstDirective(step, directive, kind);
        if (deviation is null)
            adopted = step;
        return deviation;
    }

    /// <summary>单步与 directive 的匹配规则（Act 与 Plan ActItem 同律）。</summary>
    private string? ValidateStepAgainstDirective(AgentActionStep step, string directive, string kind) =>
        kind switch
        {
            "enter" or "re-enter" or "nested-enter" => IsTap(step) && step.TargetDescriptor == TargetOf(directive)
                ? null
                : $"directive-target-mismatch:{step.TargetDescriptor ?? "<null>"}",
            "scroll" => step.EffectClass == "swipe-up"
                && step.TargetDescriptor == _config.ScrollContainerDescriptor
                ? null
                : $"directive-scroll-mismatch:{step.EffectClass}:{step.TargetDescriptor ?? "<null>"}",
            "back" => IsTap(step) && step.TargetDescriptor == _config.BackDescriptor
                ? null
                : $"directive-back-mismatch:{step.TargetDescriptor ?? "<null>"}",
            "obstacle" => IsTap(step) && step.TargetDescriptor == TargetOf(directive)
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
        backAvailable && IsTap(step) && step.TargetDescriptor == _config.BackDescriptor
        || scrollVisible && step.EffectClass == "swipe-up" && step.TargetDescriptor == _config.ScrollContainerDescriptor;

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
        null => null,
        _ => null,
    };

    private static string? DecisionJustification(AgentDecision? decision) => decision switch
    {
        AgentDecision.Act act => act.Proposal.Justification,
        AgentDecision.Plan plan => plan.Proposal.Justification,
        AgentDecision.NoAction noAction => noAction.Proposal.Justification,
        _ => null,
    };

    private static string? TargetOf(string directive)
    {
        var start = directive.IndexOf('<');
        var end = directive.IndexOf('>');
        return start >= 0 && end > start ? directive[(start + 1)..end] : null;
    }

    private string TerminationReason(CoverageSnapshot snapshot) =>
        snapshot.MaxConsultRoundsReached ? "max-consult-rounds"
        : snapshot.MaxStepsReached ? "max-steps"
        : snapshot.MaxScrollsReached ? "max-scrolls"
        : snapshot.MaxConsecutiveFailuresReached ? "max-consecutive-failures"
        : "coverage-blocked";
}
