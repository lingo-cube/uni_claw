using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Runtime;

/// <summary>
/// legal activation 结果（P24；幂等语义见 baseline §24.1 不变量 44）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record ActivationResult(
    bool Accepted,
    string? RunId,
    bool AlreadyActivated,
    string? Reason);

/// <summary>
/// 一次 Drive（self-drive until stable）的终态分类。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public enum RunDriveStatus
{
    /// <summary>Run 达成 terminal（outcome 分类见 RuntimeOutcome.Classification）。</summary>
    Completed,

    /// <summary>外部输入耗尽：合法等待（初始观察或 post-action evidence），零新 Effect。</summary>
    WaitingForInput,

    /// <summary>P25 no-response / correlation 失配 / 越权 proposal：fail closed。</summary>
    AgentDecisionFailed,

    /// <summary>接地/控制链无法形成合法 act（fail closed，零或零新 Effect）。</summary>
    GroundingFailed,

    /// <summary>Assurance/Gate 拒绝 dispatch（fail closed）。</summary>
    GateRejected,

    /// <summary>投递结果未确认（UnknownOutcome/Failed）：恢复屏障，禁止 blind redispatch。</summary>
    UnconfirmedDelivery,

    /// <summary>terminal 评估证据不足或竞争失败：保持非终态，不猜测。</summary>
    TerminalNotProven,

    /// <summary>post-action Evidence/Reconciliation/Assurance 未清偿，禁止下一 Effect。</summary>
    VerificationFailed,

    /// <summary>stimulus 消费纪律拒绝（context 失配等）。</summary>
    UnexpectedInput,

    /// <summary>cancel 到达但 contract 未声明 SafeStop obligation：fail closed（非终态）。</summary>
    CancelledWithoutSafeStopPath,

    /// <summary>Drive 在已 terminal 的 Run 上调用（幂等，零副作用）。</summary>
    AlreadyTerminal,

    /// <summary>Drive 在 legal activation 之前调用（fail closed）。</summary>
    NotActivated,
}

/// <summary>
/// Drive 结果（观察聚合，不新增 canonical state）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record RunDriveResult(
    RunDriveStatus Status,
    string? Reason,
    RuntimeOutcome? Outcome,
    int DeliveredEffects);

/// <summary>
/// RFS-001 — Uni Kernel internal run driver（ADR-0019 / baseline §24）最小
/// concrete realization。只拥有 lifecycle 编排（activation latch 已上移至
/// UniKernel——RFS-001 D20：Kernel 级 composition/lifecycle 协调态，本 driver
/// 不再自持 per-instance latch），不拥有任何 canonical domain truth 或
/// judgment authority；所有现实 Effect 仍 exclusively pass through Effect
/// Boundary（经 UniKernel 既有操作面）。
///
/// 串行验证屏障（不变量 43）：每次 dispatch 后必须取得 post-action accepted
/// Evidence（PostActionEffectFlow）并完成 reconciliation/verification，才允许
/// 下一次现实 Effect。本 driver 以可恢复 phase 状态机实现：StepVerify 未
/// 消费 post-action 证据前绝不回到 StepAct——该屏障现在可经多步场景
/// 证伪（前一步证据缺失时下一步 dispatch 不得发生）。
///
/// Drive 是可恢复（resumable）的：WaitingForInput 返回后，再次调用 Drive()
/// 从当前 phase 继续，不重做已完成的 phase（初始观察不重拉、agent 每 Run
/// 只 consult 一次）。
///
/// 本类型不是公共 Interface 冻结：字段/方法形状随 Phase 5/6 tracer 证据
/// 演进（D23）。
/// </summary>
public sealed class KernelRunDriver
{
    private const int MaxProposalSteps = 16;
    /// <summary>可恢复 Drive phase 状态机（RFS-001 D21/D22）。</summary>
    private enum DrivePhase
    {
        /// <summary>尚待消费初始外部观察（External）。</summary>
        NeedInitialObservation,

        /// <summary>尚待采纳 P25 decision（每 Run 只 consult 一次）。</summary>
        NeedDecision,

        /// <summary>待对当前 step 执行 act 链（dispatch）。</summary>
        StepAct,

        /// <summary>待消费上一 dispatch 的 post-action 证据（不变量 43 屏障）。</summary>
        StepVerify,

        /// <summary>terminal 编排（P16/P17/P18）。</summary>
        TerminalEvaluation,
    }

    private readonly UniKernel _kernel;
    private readonly AgentPlanPolicy _plan;

    /// <summary>PER-009：聚焦复查有界上限（防复读烧预算；超限诚实失败）。</summary>
    private const int FocusRetryCap = 3;

    /// <summary>
    /// PER-009 C-1/D14：冲突裁决新鲜度窗口占位（Δ ≤ 视觉耗时+余量）。
    /// 视觉耗时遥测接入后替换为推导值（D8 预算公式的消费侧）。
    /// </summary>
    private static readonly TimeSpan ConflictFreshnessWindow = TimeSpan.FromSeconds(3);

    private int _focusRetries;

    /// <summary>C-2：最近一次 dispatch 的下游确认时间（StepVerify 时序门执法）。</summary>
    private DateTimeOffset? _lastDispatchAt;
    private readonly RunDriverInputs _inputs;
    private readonly object _driverIdentity = new();
    private DrivePhase _phase = DrivePhase.NeedInitialObservation;
    private bool _consulted;
    private AgentDecision? _adoptedDecision;
    private string? _consultRejection;
    private int _stepIndex;

    public KernelRunDriver(UniKernel kernel, AgentPlanPolicy plan, RunDriverInputs inputs)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
    }

    /// <summary>
    /// Legal activation（P24）：一次性幂等 lifecycle command，完全委托
    /// UniKernel.ActivateGate（RFS-001 D20：Kernel 级 latch，同一 Kernel 上的
    /// 多个 driver 实例共享）。无 accepted Contract View → fail closed；
    /// 重复激活返回同一 Run 关联、零副作用（不创建第二 Run、不重放 Effect
    /// ——Run cardinality 由 RunModel 保证，Kernel 只持 latch）。
    /// </summary>
    public ActivationResult Activate()
    {
        var (accepted, alreadyActivated) = _kernel.ActivateGate(_driverIdentity);
        return accepted
            ? new ActivationResult(true, _kernel.RunId, alreadyActivated, Reason: null)
            : new ActivationResult(false, RunId: null, AlreadyActivated: false, Reason: "no-accepted-contract");
    }

    /// <summary>
    /// Self-drive（ADR-0019）：合法激活后运行至 terminal 或合法等待。不是
    /// per-cycle API——Host/测试/Simulation 只触发 Drive，phase 编排完全在
    /// 本 driver 内部；WaitingForInput 返回后可再次 Drive() 从断点恢复。
    /// 所有 owner 写入都经 UniKernel 既有操作面（P2/P8/P14/P16/P18 不变）。
    /// </summary>
    public RunDriveResult Drive()
    {
        if (!_kernel.IsActivated)
            return new RunDriveResult(RunDriveStatus.NotActivated, "drive-before-activation", null, 0);
        if (!_kernel.IsActivationOwner(_driverIdentity))
            return new RunDriveResult(
                RunDriveStatus.NotActivated, "activation-owned-by-another-driver", null,
                _kernel.EffectReceipts.Count);
        if (_kernel.IsRunTerminal)
            return new RunDriveResult(RunDriveStatus.AlreadyTerminal, "already-terminal", null, 0);

        var view = _kernel.RunView;
        if (view is null)
            return new RunDriveResult(RunDriveStatus.NotActivated, "no-accepted-contract", null, 0);

        while (true)
        {
            switch (_phase)
            {
                case DrivePhase.NeedInitialObservation:
                {
                    // P2/P3 真实路径；WaitingForInput / cancel / 纪律拒绝原样上抛
                    var initial = PullObservations(ObservationContext.External, "initial-observation");
                    if (initial.Stop is not null)
                        return initial.Stop;
                    _phase = DrivePhase.NeedDecision;
                    continue;
                }

                case DrivePhase.NeedDecision:
                {
                    // P25 语义 decision boundary（Phase 1 单边界）：每 Run 只
                    // consult 一次；恢复（resume）时不重新 consult。
                    if (!_consulted)
                    {
                        var consulted = ConsultAgent(view);
                        _consulted = true;
                        _adoptedDecision = consulted.Value;
                        _consultRejection = consulted.Rejection;
                    }
                    if (_consultRejection is not null)
                        return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, _consultRejection, null, 0);

                    switch (_adoptedDecision)
                    {
                        case AgentDecision.NoAction:
                            _phase = DrivePhase.TerminalEvaluation;
                            continue;
                        case AgentDecision.Act act:
                        {
                            var rejection = ValidateProposal(act.Proposal, view);
                            if (rejection is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, rejection, null, 0);
                            _stepIndex = 0;
                            _phase = DrivePhase.StepAct;
                            continue;
                        }
                        default:
                            // 防御：unknown decision kind 理论上已在 ConsultAgent
                            // 拦截（closed union），此处 fail closed 兜底。
                            return new RunDriveResult(
                                RunDriveStatus.AgentDecisionFailed, "unknown-decision-kind", null, 0);
                    }
                }

                case DrivePhase.StepAct:
                {
                    var steps = ((AgentDecision.Act)_adoptedDecision!).Proposal.Steps;
                    if (_stepIndex >= steps.Count)
                    {
                        _phase = DrivePhase.TerminalEvaluation;
                        continue;
                    }

                    // 采纳「仅本 step」的 TargetSpec（Control 仍独占 intent 签发）
                    var step = steps[_stepIndex];
                    _plan.Adopt(new[]
                    {
                        new TargetSpec(step.TargetRole, step.TargetDescriptor, step.EffectClass, step.DesiredState),
                    });

                    // Act 链（Control → Grounding → Binding → Assurance → Gate → Dispatch）
                    var root = _kernel.CurrentBelief?.Containers is { Count: 1 } containers
                        ? containers[0].Identity.ContainerId
                        : null;
                    if (root is null)
                        return new RunDriveResult(RunDriveStatus.GroundingFailed, "no-single-root-container", null, 0);

                    // PER-009 C-1（评审修复）：先裁决——权威域冲突经冻结
                    // ConflictResolver 销案（D13：confidence 盲、不升档、留档
                    // 不删）；余案才交 Control 聚焦（mechanism ④→⑤）。
                    // 销案 revision 携带原 occurrences，Act 可直接继续。
                    _ = _kernel.ResolveAuthorityConflicts(ConflictFreshnessWindow);

                    // PER-009 S6b：组合接线——每轮 act 决策前把 world 悬案推给
                    // policy（Kernel 内完成 ⇒ 双 Host 生而同构，§24.8）
                    _plan.ConflictedSubjects = _kernel.CurrentConflictedSubjects;

                    var slice = _kernel.DeriveSlice(root);
                    var intent = _kernel.SelectIntent(slice);
                    if (intent.Kind != ControlIntentKind.Act)
                    {
                        // PER-009 ⑤（mechanism 冻结图）：Observe + TargetSubject =
                        // 悬案聚焦信号 → 有界定向复查（Tier 1 通道，A 方案经
                        // 驱动面传导）。普通 Observe（desired-state 已满足 /
                        // 景观空，无 subject）保持原 fail-closed 语义不变。
                        if (intent.Kind == ControlIntentKind.Observe
                            && intent.TargetSubject is not null
                            && _focusRetries < FocusRetryCap)
                        {
                            _focusRetries++;
                            var focused = PullObservations(
                                ObservationContext.External, "focused-reobservation",
                                ObservationDepth.Focused,
                                new[] { intent.TargetSubject });
                            if (focused.Stop is not null)
                                return focused.Stop;
                            continue; // 重新 DeriveSlice → SelectIntent
                        }
                        return new RunDriveResult(
                            RunDriveStatus.GroundingFailed,
                            intent.Kind == ControlIntentKind.Observe && intent.TargetSubject is not null
                                ? "focused-reobserve-exhausted"
                                : "control-issued-non-act-intent",
                            null, 0);
                    }

                    var grounded = _kernel.ActViaCurrentGrounding(
                        intent, new TargetDescriptor(step.TargetRole, step.TargetDescriptor));
                    if (grounded.Act is null)
                        return new RunDriveResult(
                            RunDriveStatus.GroundingFailed, $"grounding:{grounded.View.Result}", null, 0);
                    if (grounded.Act.Receipt is null)
                        return new RunDriveResult(
                            RunDriveStatus.GateRejected,
                            grounded.Act.Binding?.RejectionReason?.ToString() ?? grounded.Act.Gate?.Reason ?? "gate-rejected",
                            null, 0);
                    _lastDispatchAt = grounded.Act.Receipt.DispatchedAt;
                    if (grounded.Act.Receipt.Outcome.IsUnconfirmedOutcome())
                        return new RunDriveResult(
                            RunDriveStatus.UnconfirmedDelivery,
                            $"unconfirmed:{grounded.Act.Receipt.Outcome}", null, 1);

                    _phase = DrivePhase.StepVerify;
                    continue;
                }

                case DrivePhase.StepVerify:
                {
                    // 串行验证屏障（不变量 43）：本 ordering 就是屏障本体——
                    // 下一次 dispatch（StepAct）只能在上一步 effect 的
                    // post-action 证据被消费并 reconcile 之后发生；屏障可经
                    // 多步场景证伪（StepVerify 未通过前绝不回到 StepAct）。
                    var post = PullObservations(ObservationContext.PostActionEffectFlow, "post-action-evidence");
                    if (post.Stop is not null)
                        return post.Stop;

                    var step = ((AgentDecision.Act)_adoptedDecision!).Proposal.Steps[_stepIndex];
                    var target = new TargetSpec(
                        step.TargetRole, step.TargetDescriptor, step.EffectClass, step.DesiredState);
                    var verification = _kernel.VerifyPostActionEffect(
                        target, post.ProcessedObservations, _lastDispatchAt);
                    if (!verification.IsVerified)
                        return new RunDriveResult(
                            RunDriveStatus.VerificationFailed,
                            verification.RejectionReason,
                            null,
                            _kernel.EffectReceipts.Count);
                    _stepIndex++;
                    _phase = DrivePhase.StepAct;
                    continue;
                }

                case DrivePhase.TerminalEvaluation:
                    // PER-009 C-1 补全：终局证明前同样先裁决——post 相双源分歧
                    // （如 host 观察态 vs XML 定案态）销案后 obligation 才可能满足；
                    // 权威域外的悬案保持冲突 → 终局如实未证（诚实失败）。
                    _ = _kernel.ResolveAuthorityConflicts(ConflictFreshnessWindow);
                    return EvaluateTerminalOnce();

                default:
                    return new RunDriveResult(RunDriveStatus.UnexpectedInput, "unknown-phase", null, 0);
            }
        }
    }

    /// <summary>P25 consultation 结果：Rejection 非 null = fail closed 原因。</summary>
    private readonly record struct ConsultOutcome(AgentDecision? Value, string? Rejection);

    /// <summary>一次外部输入拉取：Stop 非 null 表示等待/拒绝；否则携带 P2/P3 处理结果。</summary>
    private readonly record struct ObservationPull(
        RunDriveResult? Stop,
        IReadOnlyList<KernelResult> ProcessedObservations);

    /// <summary>
    /// decision id：run-correlated 且确定性——RunId 是 content-derived
    /// "run-&lt;sha256hex&gt;"，取末 12 字符 + 序号 "-1"（Phase 1 单边界
    /// 单次 consult）；同 Run 内确定，跨 Run 唯一。
    /// </summary>
    private string DecisionIdForRun()
    {
        var runId = _kernel.RunId;
        return $"decision-{(runId.Length > 12 ? runId[^12..] : runId)}-1";
    }

    /// <summary>P25 consultation：构建有界 context，调用外部 seam，做机械入口校验。</summary>
    private ConsultOutcome ConsultAgent(ExecutionContractView view)
    {
        var state = _kernel.RunState!;
        var claims = _kernel.CurrentBelief?.WorldState;
        var context = new AgentDecisionContext(
            DecisionId: DecisionIdForRun(),
            RunId: _kernel.RunId,
            ContractVersion: view.Version,
            Objective: view.Objective,
            AllowedEffects: view.AllowedEffects,
            CurrentWorldClaims: claims is null
                ? new Dictionary<string, string>()
                : claims.ToDictionary(kv => kv.Key, kv => kv.Value.Value),
            PendingObligations: state.ProofObligations.Obligations
                .Select(o => new AgentObligationView(
                    o.ObligationId, o.Kind.ToString(), o.Subject, o.RequiredValue, o.Mandatory))
                .ToList(),
            Phase: AgentDecisionPhase.InitialPlanning);

        var decision = _inputs.ConsultAgent(context);
        if (decision is null)
            return new ConsultOutcome(null, "no-response");

        string decisionId;
        switch (decision)
        {
            case AgentDecision.Act a:
                decisionId = a.Proposal.DecisionId;
                break;
            case AgentDecision.NoAction n:
                decisionId = n.Proposal.DecisionId;
                break;
            default:
                // 防御性 switch default：closed union 之外的派生类型 → fail closed
                return new ConsultOutcome(null, "unknown-decision-kind");
        }

        if (decisionId != context.DecisionId)
            return new ConsultOutcome(null, "correlation-mismatch");
        return new ConsultOutcome(decision, null);
    }

    /// <summary>
    /// P25 机械入口校验（proposal 级 + per-step 级；RFS-001 D23 评审 S3）。
    /// 返回 null = 通过；非 null = fail closed 原因。
    /// 注意：capability / risk / budget 级入口校验显式 deferred——尚无 owning
    /// model（Capability Plane / Grant 语义 = roadmap Phase 6；见 changes/
    /// RFS-001 D23 rationale）。不伪造这些检查。
    /// </summary>
    private static string? ValidateProposal(AgentActionProposal proposal, ExecutionContractView view)
    {
        if (proposal.Steps.Count == 0)
            return "empty-steps";
        if (proposal.Steps.Count > MaxProposalSteps)
            return "too-many-steps";
        foreach (var step in proposal.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.TargetRole))
                return "missing-target-role";
            if (string.IsNullOrWhiteSpace(step.EffectClass))
                return "missing-effect-class";
            if (!view.AllowedEffects.Contains(step.EffectClass))
                return "effect-class-not-allowed";
        }
        return null;
    }

    /// <summary>
    /// 拉取一轮期望 context 的观察并 Process（P2/P3）。返回 null = 成功消费；
    /// 非 null = 应作为 Drive 结果上抛（等待 / cancel / 消费纪律拒绝）。
    /// Cancel 在任意 observation pull 均可到达（既有 CancelPath；cancel claim
    /// Process 可能产生 belief revision，cancel 后照常进入 terminal 评估）。
    /// </summary>
    private ObservationPull PullObservations(
        ObservationContext expected, string phaseLabel,
        ObservationDepth depth = ObservationDepth.Normal,
        IReadOnlyList<string>? subjects = null)
    {
        var input = _inputs.NextInput(new ObservationDirective(expected, depth, subjects));
        switch (input)
        {
            case null:
                return new ObservationPull(
                    new RunDriveResult(RunDriveStatus.WaitingForInput, phaseLabel, null,
                        _kernel.EffectReceipts.Count),
                    Array.Empty<KernelResult>());

            case RunDriverInput.Cancel cancel:
                return new ObservationPull(CancelPath(cancel), Array.Empty<KernelResult>());

            case RunDriverInput.Unexpected unexpected:
                return new ObservationPull(
                    new RunDriveResult(RunDriveStatus.UnexpectedInput, unexpected.Reason, null,
                        _kernel.EffectReceipts.Count),
                    Array.Empty<KernelResult>());

            case RunDriverInput.Observation observation:
                if (observation.Proposals.Count == 0
                    || observation.Proposals.Any(p => p.Context != expected))
                    return new ObservationPull(
                        new RunDriveResult(
                            RunDriveStatus.UnexpectedInput, $"{phaseLabel}:context-mismatch", null,
                            _kernel.EffectReceipts.Count),
                        Array.Empty<KernelResult>());
                var processed = new List<KernelResult>(observation.Proposals.Count);
                foreach (var proposal in observation.Proposals)
                    processed.Add(_kernel.Process(proposal));
                return new ObservationPull(null, processed);

            default:
                return new ObservationPull(
                    new RunDriveResult(RunDriveStatus.UnexpectedInput, "unknown-input", null,
                        _kernel.EffectReceipts.Count),
                    Array.Empty<KernelResult>());
        }
    }

    /// <summary>
    /// Phase 1 cancel 路径（RFS-001 D14）：经 contract 声明的 mandatory SafeStop
    /// obligation 表达——cancel 事实作为外部观察经 P2 入证，Assurance 以既有
    /// 四分类形成 evidence-backed SafeStop proof。未声明该 obligation → fail
    /// closed（非终态）。正式 lifecycle command protocol 仍 Deferred ⑯。
    /// </summary>
    private RunDriveResult CancelPath(RunDriverInput.Cancel cancel)
    {
        var safeStop = _kernel.RunState?.ProofObligations.Obligations
            .FirstOrDefault(o => o.Mandatory && o.Kind == RunObligationKind.SafeStop);
        if (safeStop is null)
            return new RunDriveResult(
                RunDriveStatus.CancelledWithoutSafeStopPath, cancel.Reason, null, _kernel.EffectReceipts.Count);

        _kernel.Process(new ObservationProposal(
            new ObservationClaim(safeStop.Subject, safeStop.RequiredValue),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance(
                Producer: "kernel.lifecycle",
                CaptureTime: cancel.VirtualTime,
                Scope: "scope:lifecycle.cancel",
                TransformationLineage: new[] { $"cancel:{cancel.Reason}" })));
        return EvaluateTerminalOnce();
    }

    /// <summary>Terminal 编排（P16→P17→P18，全部经 UniKernel.EvaluateTerminal）。</summary>
    private RunDriveResult EvaluateTerminalOnce()
    {
        var evaluation = _kernel.EvaluateTerminal();
        if (evaluation.Outcome is not null)
            return new RunDriveResult(
                RunDriveStatus.Completed, "terminal-emitted", evaluation.Outcome, _kernel.EffectReceipts.Count);
        if (evaluation.Proof is null)
            return new RunDriveResult(
                RunDriveStatus.TerminalNotProven, "evidence-insufficient", null, _kernel.EffectReceipts.Count);
        return new RunDriveResult(
            RunDriveStatus.TerminalNotProven,
            evaluation.Transition.Reason ?? "terminal-transition-rejected", null, _kernel.EffectReceipts.Count);
    }
}
