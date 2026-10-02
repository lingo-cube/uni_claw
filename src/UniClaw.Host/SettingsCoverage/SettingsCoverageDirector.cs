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
        string? Justification);

    private readonly Func<AgentDecisionContext, AgentDecision?> _underlying;
    private readonly SettingsCoverageLedger _ledger;
    private readonly SettingsCoverageConfig _config;
    private readonly SettingsCoverageRunner.CoverageStepJournal? _stepJournal;
    private readonly List<ConsultRecord> _log = new();
    private readonly List<(string DecisionId, string? Directive, AgentActionStep Step)> _adoptedSteps = new();
    private string? _terminalJustification;
    private int _obstacleAttempts;

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

    /// <summary>路由回退身份（DeriveScreenIdentity 无标题时的值）——
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
            // 清障目标从当前可见元素发现（config 词汇优先序，确定性）；
            // 词汇内无可见目标时无法 grounding——回落普通遍历指令。
            var obstacle = _config.PopupSettings.Targets
                .FirstOrDefault(target => snapshot.Visible.Contains(target));
            if (obstacle is not null)
            {
                _obstacleAttempts++;
                directive =
                    $"Clear the popup overlay by tapping '<{obstacle}>' before resuming traversal.";
                kind = "obstacle";
            }
        }
        else if (snapshot.PopupState == "absent")
        {
            _obstacleAttempts = 0; // 清障成功：恢复普通遍历预算
        }

        // AGT-006 修复：路由回退身份（页面无标题，如启动过渡屏）≠ 已知
        // 非根页——"back" 会指挥幻影目标。未知页面 → Defer 有界重观察，
        // 等屏幕定形；配额尽（DeferRoundsExhausted 相位）→ 诚实 bounded stop。
        if (snapshot.CurrentRoute is not null
            && snapshot.CurrentRoute == UnknownRouteIdentity
            && snapshot.StepsVerified == 0)
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
            _log.Add(new ConsultRecord(
                context.DecisionId, context.Phase.ToString(),
                $"Observe again: current page identity is unknown ('{UnknownRouteIdentity}').",
                "defer-unknown-page", "defer", null, 1, null));
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
                + "only; never invent one).",
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
                    }));
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
            "enter" or "re-enter" => IsTap(step) && step.TargetDescriptor == TargetOf(directive)
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
