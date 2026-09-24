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

    /// <summary>RUN-004 裁决⑧层3：完成证明待人工终极裁定（合法等待）。</summary>
    AwaitingCompletionAdjudication,
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

        /// <summary>
        /// RUN-005 §5：Policy 展开轮（每轮恰一步；良基——每轮 dispatch
        ///（→ ApplicationsUsed++）或退出）。进入条件 = adoption（V6 通过 +
        /// lease 绑定）；退出 = policy-succeeded / PolicyInvalidated（typed
        /// reason）/ 步链既有转移。
        /// </summary>
        PolicyExpand,

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

    // ---- RUN-004：多轮协议字段（§2 F1'）----

    /// <summary>咨询轮次计数（DecisionId 序号）。</summary>
    private int _consultCounter;

    /// <summary>已派发步计数（预算执法）。</summary>
    private int _stepsDispatched;

    /// <summary>层2 归档：每步验证成功时追加（decisionN, stepIndex, receiptId）。</summary>
    private readonly List<(int DecisionN, int StepIndex, string ReceiptId)> _completedSteps = new();

    /// <summary>裁决⑧层3：等待人工终裁旗。</summary>
    private bool _awaitingRuling;

    /// <summary>
    /// 裁决⑧层3 settlement：completion adjudication 被 rejected 后的持久终局
    /// 状态（RUN-004 终局收口）。语义：API 返回 TerminalNotProven
    /// "completion-rejected" 与状态机终局必须一致——后续 Drive() 不得重新
    /// 采纳同一 completion proposal、不得重新发起同一裁决（防
    /// NoAction→驳回→再裁决环）。这不是「清空临时字段的偶然效果」，
    /// 而是显式登记的状态机语义。
    /// </summary>
    private bool _completionAdjudicationRejected;

    /// <summary>上一个回答（校验期的对照引用；当前回答校验通过并采纳后才回填）。</summary>
    private AgentDecision? _lastAnswer;

    /// <summary>当前 Defer 链已执行的等待轮数（每轮 = 1 拉取 + 1 再咨询，M1）。</summary>
    private int _deferRoundsUsed;

    /// <summary>Defer 配额耗尽：下一轮咨询以 DeferRoundsExhausted 相位发出（T6 终问）。</summary>
    private bool _deferExhaustedPending;

    /// <summary>上一次咨询的失败上下文（E2/E3 捕获点原文，M2 不净化）。</summary>
    private string? _pendingFailureReason;

    /// <summary>失败来源相位（E2=StepRejected / E3=VerificationFailed；2026-09-23：
    /// 由控制流在捕获点显式记录，PhaseForCurrent 直读——不再从世界状态反推）。</summary>
    private AgentDecisionPhase? _pendingFailurePhase;

    private int? _pendingFailedStepIndex;

    /// <summary>层3 完成自证缓存（docket 呈递用）。</summary>
    private CompletionEvidence? _lastCompletionEvidence;
    private IReadOnlyList<(string Anchor, bool Verified)>? _lastAnchorResults;

    // ---- RUN-005 Slice A：Policy 协议面（adoption/展开运行时归 Slice B）----

    /// <summary>
    /// V6f operand：已通过 V6 的 PolicyId → 咨询序号（同 run 内唯一性；
    /// 同序号 = 同次咨询幂等重验，不算重复——见 PolicyValidation.IsDuplicatePolicyId）。
    /// 【v0.3.1 约束】validated/reserved ≠ adopted：本表只在 **实际 adoption**
    /// 时记录（NeedDecision Policy case），validation 通过而未采纳的 id 不占用。
    /// </summary>
    private readonly Dictionary<string, int> _adoptedPolicyIds = new(StringComparer.Ordinal);

    // ---- RUN-005 Slice B：Policy 展开运行时（§5/§8；全部 driver private
    //      ephemeral——不持久化、非 recovery state、非 authority）----

    /// <summary>ephemeral PolicyState（§8 + v0.3.1：含 adopted exact lease）。
    /// null = 无活跃 policy。</summary>
    private PolicyExecutionState? _policy;

    /// <summary>当前展开轮物化的单步（模板派生——目标来自模板，Kernel 零猜测）。</summary>
    private AgentActionStep? _policyStep;

    /// <summary>per-guard 游标（§8：与 Proposal.Guards 同序；null = 该 guard 尚无可比样本）。</summary>
    private PolicyGuardCursor?[] _guardCursors = Array.Empty<PolicyGuardCursor?>();

    /// <summary>policy 级结局捕获（§8：活到下次咨询、消费即清；Progress.PolicyState 数据源）。</summary>
    private PendingPolicyOutcome? _pendingPolicyOutcome;

    private readonly RunDriverInputs _inputs;
    private readonly object _driverIdentity = new();
    private DrivePhase _phase = DrivePhase.NeedInitialObservation;
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

        // 层3 settlement 执法：completion adjudication rejected 后的 Drive
        // 直接以同一终局语义应答（幂等重报），不重入 NeedDecision、不重新
        // 采纳 completion proposal、不重新发起裁决。
        if (_completionAdjudicationRejected)
            return new RunDriveResult(
                RunDriveStatus.TerminalNotProven, "completion-rejected",
                null, _kernel.EffectReceipts.Count);

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
                    // RUN-004 多轮协议：每个边界恰一次咨询（幂等续跑由
                    // _adoptedDecision 非空判定）；预算执法前置（§2.1）。
                    if (_adoptedDecision is null)
                    {
                        var budget = CurrentBudget(view);
                        if (budget.RoundsRemaining <= 0)
                            return new RunDriveResult(
                                RunDriveStatus.AgentDecisionFailed,
                                "consult-budget-exhausted", null, _kernel.EffectReceipts.Count);

                        var consulted = ConsultAgentV2(view);
                        _adoptedDecision = consulted.Value;
                        _consultRejection = consulted.Rejection;
                        // 注意：不再立即回填 _lastAnswer——校验通过并采纳后才成为
                        // 下一轮的「上一轮回答」（2026-09-23 语义修正：防止当前回答
                        // 被当成上一轮，导致首个 Defer 被判嵌套）。
                    }
                    if (_consultRejection is not null)
                    {
                        // RUN-004：null-response 已转 TerminalEvaluation → continue
                        if (_phase == DrivePhase.TerminalEvaluation)
                            continue;
                        return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, _consultRejection, null, 0);
                    }

                    switch (_adoptedDecision)
                    {
                        case AgentDecision.NoAction no:
                        {
                            // V4 hollow-completion（SR-074）
                            var v4 = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (v4 is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, v4, null, 0);

                            // 采纳：当前回答成为下一轮对照；Defer 链终结
                            _lastAnswer = _adoptedDecision;
                            _deferRoundsUsed = 0;
                            _deferExhaustedPending = false;

                            // 裁决⑧：有 Completion → 尝试折抵
                            if (no.Proposal.Completion is { } evidence)
                            {
                                var anchorResults = VerifyCompletionAnchors(evidence);
                                if (anchorResults.All(r => r.Verified))
                                {
                                    // 层2 全锚住：completion claim 入证 → TerminalEvaluation
                                    DischargeCompletion(evidence, "kernel.completion-verifier");
                                    _phase = DrivePhase.TerminalEvaluation;
                                    _adoptedDecision = null; // 允许终局后再入（不复用旧回答）
                                    continue;
                                }
                                // 层3：锚不住 → 人工终裁
                                _lastCompletionEvidence = evidence;
                                _lastAnchorResults = anchorResults;
                                _awaitingRuling = true;
                                return new RunDriveResult(
                                    RunDriveStatus.AwaitingCompletionAdjudication,
                                    "completion-anchors-unverified", null, _kernel.EffectReceipts.Count);
                            }
                            _phase = DrivePhase.TerminalEvaluation;
                            _adoptedDecision = null;
                            continue;
                        }
                        case AgentDecision.Defer defer:
                        {
                            // V5 defer-unbounded（静态界）
                            var v5 = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (v5 is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, v5, null, 0);

                            // T6 终问（配额已尽）：仍 Defer → 确定性终局（不再拉取/咨询）
                            if (_deferExhaustedPending)
                            {
                                _adoptedDecision = null;
                                return new RunDriveResult(
                                    RunDriveStatus.TerminalNotProven,
                                    "defer-exhausted", null, _kernel.EffectReceipts.Count);
                            }

                            // 新链起点：上一轮不是 Defer → 轮次计数重新开始
                            if (_lastAnswer is not AgentDecision.Defer)
                                _deferRoundsUsed = 0;

                            // M1 配额语义（2026-09-23 定形）：MaxRounds = 等待轮次数
                            //（每轮 = 1 拉取 + 1 再咨询；首个 Defer 是请求本身，不入配额）。
                            if (_deferRoundsUsed >= defer.Spec.MaxRounds)
                            {
                                // 配额已尽仍收到 Defer：进入 exhaustion 终问（不再等待）
                                _deferExhaustedPending = true;
                                _adoptedDecision = null;
                                continue;
                            }

                            _deferRoundsUsed++;
                            _lastAnswer = _adoptedDecision;
                            _adoptedDecision = null; // 清空以触发再咨询
                            var observed = PullObservations(ObservationContext.External, "defer-observation");
                            if (observed.Stop is not null)
                                return observed.Stop;
                            continue; // 回到 NeedDecision 咨询
                        }
                        case AgentDecision.Act act:
                        {
                            var rejection = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (rejection is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, rejection, null, 0);
                            _lastAnswer = _adoptedDecision;
                            _deferRoundsUsed = 0;
                            _deferExhaustedPending = false;
                            _stepIndex = 0;
                            _phase = DrivePhase.StepAct;
                            continue;
                        }
                        case AgentDecision.Policy policy:
                        {
                            // RUN-005 Slice A：V6 机械校验（fail closed，零新
                            // Effect，映射回既有 AgentDecisionFailed 语义）。
                            var rejection = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (rejection is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, rejection, null, 0);

                            // RUN-005 Slice B：adoption（V6 通过即采纳，原子）。
                            // v0.3.1 约束 ①：绑定并保存 exact PolicyLeaseRef 进
                            // ephemeral PolicyState（后续每轮 current==adopted
                            // exact 等值校验，非「存在某 lease」）。
                            // v0.3.1 约束 ②：PolicyId 只在 **实际采纳** 时占用
                            //（validated/reserved ≠ adopted——V6f operand = 采纳集）。
                            var lease = PolicyLease.TryDerive(_kernel.CurrentBelief);
                            _policy = new PolicyExecutionState(
                                policy.Proposal.PolicyId,
                                lease.Lease!,
                                policy.Proposal,
                                ApplicationsUsed: 0,
                                LastTerminationStatus: null);
                            _guardCursors = new PolicyGuardCursor?[
                                policy.Proposal.Guards?.Count ?? 0];
                            _adoptedPolicyIds[policy.Proposal.PolicyId] = _consultCounter;
                            _lastAnswer = _adoptedDecision;
                            _deferRoundsUsed = 0;
                            _deferExhaustedPending = false;
                            _phase = DrivePhase.PolicyExpand;
                            continue;
                        }
                        default:
                            return new RunDriveResult(
                                RunDriveStatus.AgentDecisionFailed, "unknown-decision-kind", null, 0);
                    }
                }

                case DrivePhase.StepAct:
                {
                    // RUN-005 §5 步骤 7：Policy 展开轮的 steps = 模板物化单步
                    //（CurrentSteps 统一取步——Act 链逻辑逐字节不变）
                    var steps = CurrentSteps();
                    if (_stepIndex >= steps.Count)
                    {
                        // E1（RUN-004）：提案耗尽 → 回 NeedDecision（StepVerified）
                        _pendingFailureReason = null;
                        _pendingFailurePhase = null;
                        _pendingFailedStepIndex = null;
                        _adoptedDecision = null; // 清空触发再咨询
                        _phase = DrivePhase.NeedDecision;
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
                    {
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.StepRejected, "no-single-root-container");
                        if (CurrentBudget(view).RoundsRemaining > 0)
                        {
                            _pendingFailureReason = "no-single-root-container";
                            _pendingFailurePhase = AgentDecisionPhase.StepRejected;
                            _pendingFailedStepIndex = _stepIndex;
                            _adoptedDecision = null;
                            _phase = DrivePhase.NeedDecision;
                            continue;
                        }
                        return new RunDriveResult(RunDriveStatus.GroundingFailed, "no-single-root-container", null, 0);
                    }

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
                        // E4（RUN-004）：plain Observe（无 subject、无冲突）=
                        // 目标已满足/无可行动
                        if (intent.Kind == ControlIntentKind.Observe
                            && intent.TargetSubject is null
                            && _kernel.CurrentConflictedSubjects.Count == 0)
                        {
                            // RUN-005 §5 E4 映射：policy 展开中 = 重评
                            // Termination on fresh belief——Satisfied →
                            // policy-succeeded；否则 control-non-act
                            //（F7(b)：消灭静默楔死）
                            if (_policy is not null)
                            {
                                var e4View = PolicyEvaluationView.FromBelief(_kernel.CurrentBelief!);
                                var e4Termination = PolicyEvaluation.EvaluateConjunction(
                                    _policy.Proposal.Termination, e4View);
                                _policy = _policy with { LastTerminationStatus = e4Termination };
                                if (e4Termination == PolicyTruth.Satisfied)
                                {
                                    ExitPolicy(AgentDecisionPhase.StepVerified, "policy-succeeded", e4Termination);
                                }
                                else
                                {
                                    ExitPolicy(
                                        AgentDecisionPhase.PolicyInvalidated,
                                        $"policy:{PolicyInvalidationTokens.Token(PolicyInvalidationReason.ControlNonAct)}",
                                        e4Termination);
                                }
                                continue;
                            }
                            _phase = DrivePhase.TerminalEvaluation;
                            continue;
                        }
                        // E4b：Observe + TargetSubject（聚焦复查信号）→ 保持
                        // PER-009 聚焦环路（有界 ≤3）
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
                    {
                        var reason = $"grounding:{grounded.View.Result}";
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.StepRejected, reason);
                        if (CurrentBudget(view).RoundsRemaining > 0)
                        {
                            _pendingFailureReason = reason;
                            _pendingFailurePhase = AgentDecisionPhase.StepRejected;
                            _pendingFailedStepIndex = _stepIndex;
                            _adoptedDecision = null;
                            _phase = DrivePhase.NeedDecision;
                            continue;
                        }
                        return new RunDriveResult(RunDriveStatus.GroundingFailed, reason, null, 0);
                    }
                    if (grounded.Act.Receipt is null)
                    {
                        var reason = grounded.Act.Binding?.RejectionReason?.ToString()
                            ?? grounded.Act.Gate?.Reason ?? "gate-rejected";
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.StepRejected, reason);
                        if (CurrentBudget(view).RoundsRemaining > 0)
                        {
                            _pendingFailureReason = reason;
                            _pendingFailurePhase = AgentDecisionPhase.StepRejected;
                            _pendingFailedStepIndex = _stepIndex;
                            _adoptedDecision = null;
                            _phase = DrivePhase.NeedDecision;
                            continue;
                        }
                        return new RunDriveResult(RunDriveStatus.GateRejected, reason, null, 0);
                    }
                    _lastDispatchAt = grounded.Act.Receipt.DispatchedAt;
                    _stepsDispatched++;
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

                    var step = CurrentSteps()[_stepIndex];
                    var target = new TargetSpec(
                        step.TargetRole, step.TargetDescriptor, step.EffectClass, step.DesiredState);
                    var verification = _kernel.VerifyPostActionEffect(
                        target, post.ProcessedObservations, _lastDispatchAt);
                    if (!verification.IsVerified)
                    {
                        var reason = verification.RejectionReason ?? "verification-failed";
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.VerificationFailed, reason);
                        if (CurrentBudget(view).RoundsRemaining > 0)
                        {
                            _pendingFailureReason = reason;
                            _pendingFailurePhase = AgentDecisionPhase.VerificationFailed;
                            _pendingFailedStepIndex = _stepIndex;
                            _adoptedDecision = null;
                            _phase = DrivePhase.NeedDecision;
                            continue;
                        }
                        return new RunDriveResult(
                            RunDriveStatus.VerificationFailed,
                            reason,
                            null,
                            _kernel.EffectReceipts.Count);
                    }
                    // 层2 归档：每步验证成功时追加
                    _completedSteps.Add((_consultCounter, _stepIndex,
                        _kernel.EffectReceipts.LastOrDefault()?.ReceiptId ?? ""));
                    _stepIndex++;
                    if (_policy is not null)
                    {
                        // RUN-005 §5：verified → ApplicationsUsed++ / GuardCursor
                        // 更新 → 回 PolicyExpand（良基：本轮 application 消耗）
                        _policy = _policy with { ApplicationsUsed = _policy.ApplicationsUsed + 1 };
                        UpdateGuardCursors();
                        _phase = DrivePhase.PolicyExpand;
                    }
                    else
                    {
                        _phase = DrivePhase.StepAct;
                    }
                    continue;
                }

                case DrivePhase.PolicyExpand:
                {
                    // RUN-005 §5：每轮恰一步；良基不变量——每次重入必 dispatch
                    //（→ verified 后 ApplicationsUsed++）或退出，无「既不
                    // dispatch 又不退出」的轮（单约束即完备有界，F1(o)）。
                    var state = _policy!;

                    // 1. fresh observation（External）→ reconcile → fresh belief
                    //（WaitingForInput 可恢复：phase 保持，续跑重入本步）
                    var observed = PullObservations(ObservationContext.External, "policy-observation");
                    if (observed.Stop is not null)
                        return observed.Stop;

                    // 2. lease check（v0.3.1 约束）：current active lease ==
                    //    adopted lease（exact 等值）；零/多根/身份漂移一律
                    //    LeaseInvalidated → 零新 Effect 回 decision boundary
                    var currentLease = PolicyLease.TryDerive(_kernel.CurrentBelief);
                    if (currentLease.Lease is null
                        || currentLease.Lease.RootContainerId != state.Lease.RootContainerId)
                    {
                        ExitPolicy(
                            AgentDecisionPhase.PolicyInvalidated,
                            $"policy:{PolicyInvalidationTokens.Token(PolicyInvalidationReason.LeaseInvalidated)}",
                            state.LastTerminationStatus ?? PolicyTruth.Unknown);
                        continue;
                    }

                    // 每轮即席派生的最小求值视图（§4.1：fresh-derived/ephemeral）
                    var evaluationView = PolicyEvaluationView.FromBelief(_kernel.CurrentBelief!);

                    // 3. Termination（合取）：Satisfied → 成功出口；Unknown →
                    //    termination-unprovable（fail closed）；Violated → 继续
                    var termination = PolicyEvaluation.EvaluateConjunction(
                        state.Proposal.Termination, evaluationView);
                    _policy = state = state with { LastTerminationStatus = termination };
                    if (termination == PolicyTruth.Satisfied)
                    {
                        ExitPolicy(AgentDecisionPhase.StepVerified, "policy-succeeded", termination);
                        continue;
                    }
                    if (termination == PolicyTruth.Unknown)
                    {
                        ExitPolicy(
                            AgentDecisionPhase.PolicyInvalidated,
                            $"policy:{PolicyInvalidationTokens.Token(PolicyInvalidationReason.TerminationUnprovable)}",
                            termination);
                        continue;
                    }

                    // 4. Guards（逐个 tri-state；无 Fallback 分支——Unknown 同样
                    //    回 decision boundary）
                    if (state.Proposal.Guards is { } guards)
                    {
                        PolicyInvalidationReason? guardFailure = null;
                        for (var i = 0; i < guards.Count && guardFailure is null; i++)
                        {
                            var truth = PolicyEvaluation.Evaluate(guards[i], evaluationView, _guardCursors[i]);
                            if (truth == PolicyTruth.Violated)
                                guardFailure = PolicyInvalidationReason.GuardViolated;
                            else if (truth == PolicyTruth.Unknown)
                                guardFailure = PolicyInvalidationReason.GuardUnknown;
                        }
                        if (guardFailure is { } guardReason)
                        {
                            ExitPolicy(
                                AgentDecisionPhase.PolicyInvalidated,
                                $"policy:{PolicyInvalidationTokens.Token(guardReason)}",
                                termination);
                            continue;
                        }
                    }

                    // 5. bounds：ApplicationsUsed ≥ MaxApplications → 耗尽
                    if (state.ApplicationsUsed >= state.Proposal.MaxApplications)
                    {
                        ExitPolicy(
                            AgentDecisionPhase.PolicyInvalidated,
                            $"policy:{PolicyInvalidationTokens.Token(PolicyInvalidationReason.BoundsExhausted)}",
                            termination);
                        continue;
                    }

                    // 6. Match（合取）：Satisfied → 继续；Violated/Unknown →
                    //    no-match / match-unknown
                    var match = PolicyEvaluation.EvaluateConjunction(state.Proposal.Match, evaluationView);
                    if (match != PolicyTruth.Satisfied)
                    {
                        ExitPolicy(
                            AgentDecisionPhase.PolicyInvalidated,
                            $"policy:{PolicyInvalidationTokens.Token(match == PolicyTruth.Violated
                                ? PolicyInvalidationReason.NoMatch
                                : PolicyInvalidationReason.MatchUnknown)}",
                            termination);
                        continue;
                    }

                    // 7. 物化单步（目标来自模板——Kernel 零猜测），复用
                    //    StepAct→StepVerify 全链（零新执行器）
                    var template = state.Proposal.ActionTemplate;
                    _policyStep = new AgentActionStep(
                        template.TargetRole, template.TargetDescriptor,
                        template.EffectClass, template.DesiredState);
                    _stepIndex = 0;
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

    /// <summary>当前预算快照（§2.1）。</summary>
    private ConsultationBudget CurrentBudget(ExecutionContractView view) => new(
        RoundsRemaining: view.MaxConsultations - _consultCounter,
        StepsRemaining: view.MaxTotalSteps - _stepsDispatched);

    /// <summary>多轮咨询（V2）：构建 v2 上下文 + 递增 DecisionId + 锚点核验。</summary>
    private ConsultOutcome ConsultAgentV2(ExecutionContractView view)
    {
        _consultCounter++;
        var decisionId = DecisionIdForRun(_consultCounter);

        var state = _kernel.RunState!;
        var claims = _kernel.CurrentBelief?.WorldState;
        var claimSummaries = claims is null
            ? new Dictionary<string, ClaimSummary>()
            : claims.ToDictionary(
                kv => kv.Key,
                kv => new ClaimSummary(
                    kv.Value.Value,
                    DispositionOf(kv.Key, kv.Value),
                    _kernel.CurrentConflictedSubjects.Contains(kv.Key)));

        var context = new AgentDecisionContext(
            DecisionId: decisionId,
            RunId: _kernel.RunId,
            ContractVersion: view.Version,
            Objective: view.Objective,
            AllowedEffects: view.AllowedEffects,
            CurrentWorldClaims: claimSummaries,
            PendingObligations: state.ProofObligations.Obligations
                .Select(o => new AgentObligationView(
                    o.ObligationId, o.Kind.ToString(), o.Subject, o.RequiredValue, o.Mandatory))
                .ToList(),
            Phase: PhaseForCurrent(),
            FailureReason: _pendingFailureReason,
            FailedStepIndex: _pendingFailedStepIndex,
            Screen: DeriveScreenSummary(),
            Elements: DeriveElementSummaries(),
            Progress: new ConsultationProgress(
                _consultCounter, _stepsDispatched, _completedSteps.Count,
                PolicyState: _pendingPolicyOutcome?.Summary),
            BudgetRemaining: CurrentBudget(view));

        var decision = _inputs.ConsultAgent(context);
        if (decision is null)
        {
            // RUN-004：非初始边界的 no-response = agent 无法重规划 → 终局评估
            if (context.Phase != AgentDecisionPhase.InitialPlanning)
            {
                _phase = DrivePhase.TerminalEvaluation;
                _adoptedDecision = null;
            }
            return new ConsultOutcome(null, "no-response");
        }

        string answeredId;
        switch (decision)
        {
            case AgentDecision.Act a: answeredId = a.Proposal.DecisionId; break;
            case AgentDecision.NoAction n: answeredId = n.Proposal.DecisionId; break;
            // RUN-004 终局收口：Defer 同样回带 DecisionId（D2 防串话适用于
            // 每一次 consultation response，不限于最终可执行 decision）
            case AgentDecision.Defer d: answeredId = d.DecisionId; break;
            // RUN-005 V6e：Policy 同律回带 DecisionId（防串话；Defer 先例——
            // correlation 字段在 union 成员上，与 PolicyId 正交）
            case AgentDecision.Policy p: answeredId = p.DecisionId; break;
            default: return new ConsultOutcome(null, "unknown-decision-kind");
        }
        if (answeredId != decisionId)
            return new ConsultOutcome(null, "correlation-mismatch");

        // 清除失败上下文（已消费）；RUN-005：policy 结局同点消费即清（§8——
        // 活到下次咨询为止，本咨询 context 已携带其投影）
        _pendingFailureReason = null;
        _pendingFailurePhase = null;
        _pendingFailedStepIndex = null;
        _pendingPolicyOutcome = null;
        return new ConsultOutcome(decision, null);
    }

    private AgentDecisionPhase PhaseForCurrent()
    {
        if (_deferExhaustedPending)
            return AgentDecisionPhase.DeferRoundsExhausted;
        // 失败来源由控制流显式记录（E2/E3 捕获点），不再从世界状态反推
        if (_pendingFailurePhase is { } phase)
            return phase;
        // RUN-005：policy 级结局显式捕获（F7(o)/F6(b)——不依赖 _completedSteps
        // 推导：0-application 即时满足/E4 路径下 _completedSteps 可为空）
        if (_pendingPolicyOutcome is
            {
                Phase: AgentDecisionPhase.StepVerified or AgentDecisionPhase.PolicyInvalidated
            } outcome)
            return outcome.Phase;
        return _completedSteps.Count > 0
            ? AgentDecisionPhase.StepVerified
            : AgentDecisionPhase.InitialPlanning;
    }

    /// <summary>
    /// Disposition 三态（spec §5 / F4 值域三分）：conflicted（权威域悬案）＞
    /// revised（痕迹链非空——该 subject 的值曾被后续证据修正）＞ established。
    /// </summary>
    private string DispositionOf(string subject, WorldClaim claim)
    {
        if (_kernel.CurrentConflictedSubjects.Contains(subject))
            return "conflicted";
        return claim.SupersededEvidenceIds is not null ? "revised" : "established";
    }

    private ScreenSummary? DeriveScreenSummary()
    {
        var current = _kernel.CurrentBelief;
        if (current?.Containers is not { Count: 1 } containers)
            return null;
        var containerId = containers[0].Identity.ContainerId;
        var signature = current.WorldState.TryGetValue(
            World.WorldModel.SignatureSubjectPrefix + containerId, out var sig)
            ? sig.Value : null;
        return new ScreenSummary(containerId, signature);
    }

    private IReadOnlyList<ElementSummary> DeriveElementSummaries()
    {
        var current = _kernel.CurrentBelief;
        if (current?.Occurrences is not { } occurrences)
            return Array.Empty<ElementSummary>();
        return occurrences
            .Select(o => new ElementSummary(
                o.Role, null, null, null, null, null,
                ElementEpistemic.Partial)) // v1：occurrence 投影，视觉单源
            .ToList();
    }

    /// <summary>
    /// decision id：run-correlated 且确定性——RunId 是 content-derived
    /// "run-&lt;sha256hex&gt;"，取末 12 字符 + 咨询序号 "-{n}"（多轮协议：
    /// 每次咨询严格递增，D2）。同 Run 内确定，跨 Run 唯一。
    /// </summary>
    private string DecisionIdForRun(int n)
    {
        var runId = _kernel.RunId;
        return $"decision-{(runId.Length > 12 ? runId[^12..] : runId)}-{n}";
    }

    /// <summary>决策校验（V3–V6；Act case 内部沿用既有五检查；Policy case = RUN-005 V6 形态/预算/唯一性/lease 绑定）。</summary>
    private string? ValidateDecision(
        AgentDecision decision,
        ExecutionContractView view,
        ConsultationBudget remaining,
        AgentDecision? lastAnswer)
    {
        switch (decision)
        {
            case AgentDecision.Act act:
            {
                var existing = ValidateProposal(act.Proposal, view);
                if (existing is not null) return existing;
                // V3 budget-exceeded（SR-049）
                if (act.Proposal.Steps.Count > remaining.StepsRemaining)
                    return "budget-exceeded:steps";
                break;
            }
            case AgentDecision.NoAction no:
            {
                // V4 hollow-completion（SR-074）
                var hasMandatory = _kernel.RunState?.ProofObligations.Obligations
                    .Any(o => o.Mandatory) == true;
                if (hasMandatory && no.Proposal.Completion is null
                    && _consultCounter <= 1
                    && _stepsDispatched == 0
                    && _completedSteps.Count == 0
                    && _kernel.RunState?.ProofObligations.Obligations
                        .Any(o => o.Mandatory
                            && _kernel.CurrentBelief?.WorldState.TryGetValue(o.Subject, out var claim) == true
                            && claim.Value == o.RequiredValue) != true)
                    return "hollow-completion";
                break;
            }
            case AgentDecision.Defer defer:
            {
                // V5 defer-unbounded（SR-068）：静态界（配额链已由 M1 计数替代——
                // 配额内重复 Defer 合法、耗尽进入 DeferRoundsExhausted 终问；
                // 「嵌套拒绝」语义 2026-09-23 修订移除，见 spec §4 注记）
                if (defer.Spec.MaxRounds > 4)
                    return "defer-unbounded:max-rounds";
                break;
            }
            case AgentDecision.Policy policy:
            {
                // V6（RUN-005 §9）：V6a-d 形态 + V6c bounds（纯函数面）
                var shape = PolicyValidation.ValidateProposal(
                    policy.Proposal, view, remaining.StepsRemaining);
                if (shape is not null)
                    return shape;
                // V6f：PolicyId 同 run 内唯一（同次咨询幂等重验除外）
                if (PolicyValidation.IsDuplicatePolicyId(
                        _adoptedPolicyIds, policy.Proposal.PolicyId, _consultCounter))
                    return "policy:duplicate-policy-id";
                // V6g（semantic lease）：adoption 必须可绑定当前 active
                // execution lease——无 lease / identity 不合法 → reject
                //（per-expand 的 lease validity 校验归 Slice B）
                var lease = PolicyLease.TryDerive(_kernel.CurrentBelief);
                if (lease.Rejection is not null)
                    return $"policy:{lease.Rejection}";
                break;
            }
        }
        return null;
    }

    /// <summary>层2 锚点核验（裁决⑧：核验后信）。</summary>
    private IReadOnlyList<(string Anchor, bool Verified)> VerifyCompletionAnchors(
        CompletionEvidence evidence)
    {
        var results = new List<(string, bool)>();
        foreach (var anchor in evidence.Checklist)
        {
            var verified = anchor switch
            {
                // step 锚按 (DecisionN, StepIndex) 命中——ReceiptId 不参与
                // 匹配（锚点格式无 receipt 段；修复前整元组 Contains 恒
                // false——step 锚从未可核验，层2 验收测试暴露）
                var a when a.StartsWith("step:", StringComparison.Ordinal) =>
                    ParseStepAnchor(a) is { } step
                    && _completedSteps.Any(c =>
                        c.DecisionN == step.DecisionN && c.StepIndex == step.StepIndex),
                var a when a.StartsWith("dispatch:", StringComparison.Ordinal) =>
                    _kernel.EffectReceipts.Any(r => r.ReceiptId == a["dispatch:".Length..]),
                var a when a.StartsWith("obs:", StringComparison.Ordinal) =>
                    _kernel.CurrentBelief?.EvidenceBasis.Contains(a["obs:".Length..]) == true,
                _ => false,
            };
            results.Add((anchor, verified));
        }
        return results;
    }

    private (int DecisionN, int StepIndex, string ReceiptId)? ParseStepAnchor(string anchor)
    {
        // "step:{decisionN}.{stepIndex}"
        var body = anchor["step:".Length..];
        var dot = body.IndexOf('.');
        if (dot < 0) return null;
        if (!int.TryParse(body[..dot], out var n) || !int.TryParse(body[(dot + 1)..], out var idx))
            return null;
        return (n, idx, ""); // ReceiptId 不参与匹配
    }

    /// <summary>
    /// 层2/3 折抵：completion claim 经 Process 入证（裁决⑧ G2 闭合）。
    /// 折抵目标 = <b>未世界满足</b>的 mandatory 义务（spec §3：subject =
    /// 未世界满足义务之 Subject——已满足的不重复入证，判定同源见
    /// UniKernel.UnsatisfiedMandatoryObligations）。
    /// </summary>
    private void DischargeCompletion(CompletionEvidence evidence, string producer)
    {
        foreach (var obligation in _kernel.UnsatisfiedMandatoryObligations())
        {
            // 入证 context = PostActionEffectFlow：折抵断言的是「已经发生的
            // effect 流」（锚点 = 步归档/dispatch receipt/post-action 观察），
            // 且 MaterialEffect 判定门（ING-006 D7）要求 backing evidence 为
            // 该 context——义务是否满足仍由层1 JudgeOutcome 按证据类判定，
            // anchor 只是把断言送进裁决的门槛（收口裁决⑥：不把「有真实
            // anchor」误写成「义务已满足」）。
            _kernel.Process(new Evidence.ObservationProposal(
                new Evidence.ObservationClaim(obligation.Subject, obligation.RequiredValue),
                Evidence.IngressKind.Observation,
                Evidence.ObservationContext.PostActionEffectFlow,
                new Evidence.Provenance(producer, DateTimeOffset.UtcNow,
                    $"scope:{obligation.Subject}",
                    new[] { "completion-evidence", evidence.Basis })));
        }
    }

    /// <summary>层3：docket 呈递面（公开只读，零新类型——元组）。</summary>
    public (CompletionEvidence Evidence, IReadOnlyList<(string Anchor, bool Verified)> Results)?
        PendingCompletionDossier =>
        _awaitingRuling && _lastCompletionEvidence is { } evidence && _lastAnchorResults is { } results
            ? (evidence, results)
            : null;

    /// <summary>层3：人工终裁恢复（rejected = 持久 settlement，防环）。</summary>
    public RunDriveResult ResumeWithAdjudication(bool approved, string? note)
    {
        if (!_awaitingRuling)
            throw new InvalidOperationException("不在裁决等待态");
        _awaitingRuling = false;
        if (approved && _lastCompletionEvidence is { } evidence)
        {
            DischargeCompletion(evidence, "kernel.completion-adjudicator");
            _phase = DrivePhase.TerminalEvaluation;
            _adoptedDecision = null;
        }
        else
        {
            // 持久 settlement：显式状态机语义（非清字段的偶然效果）——
            // 后续 Drive() 幂等重报 completion-rejected，不重入裁决。
            _completionAdjudicationRejected = true;
            _adoptedDecision = null;
            return new RunDriveResult(
                RunDriveStatus.TerminalNotProven,
                $"completion-rejected{(note is null ? "" : $":{note}")}",
                null, _kernel.EffectReceipts.Count);
        }
        return Drive(); // 按裁决终局
    }

    // ---- RUN-005 Slice B：Policy 展开运行时 helpers（§5/§7/§8）-----------------

    /// <summary>
    /// 当前执行 steps：Policy 展开轮 = 模板物化的单步（目标来自模板——
    /// Kernel 零猜测）；否则 = 采纳的 Act proposal steps。StepAct/StepVerify
    /// 主链经此统一取步，Act 路径行为不变。
    /// </summary>
    private IReadOnlyList<AgentActionStep> CurrentSteps() =>
        _policyStep is { } policyStep
            ? new[] { policyStep }
            : ((AgentDecision.Act)_adoptedDecision!).Proposal.Steps;

    /// <summary>
    /// §8 GuardCursor 更新（verified application 后）：第一份样本只初始化
    /// cursor（warm-up）；同值 → ConsecutiveUnchangedCount++；异值 → 重置。
    /// 只有可比样本（subject 在场且无冲突）参与——证据不足不产样本、不产
    /// 假「未变」计数。
    /// </summary>
    private void UpdateGuardCursors()
    {
        if (_policy?.Proposal.Guards is not { } guards || _guardCursors.Length == 0)
            return;
        var view = PolicyEvaluationView.FromBelief(_kernel.CurrentBelief!);
        for (var i = 0; i < guards.Count && i < _guardCursors.Length; i++)
        {
            if (guards[i] is not PolicyGuard.ObservationUnchanged guard)
                continue; // closed vocabulary——非成员类型不会出现（V6a 已拒）
            if (!view.Claims.TryGetValue(guard.Subject, out var fact) || fact.InConflict)
                continue;
            var sample = fact.Value;
            var cursor = _guardCursors[i];
            _guardCursors[i] = cursor is null || cursor.InWarmUp
                ? new PolicyGuardCursor(guard.Subject, sample, ConsecutiveUnchangedCount: 0)
                : cursor.LastObservedValue == sample
                    ? cursor with { ConsecutiveUnchangedCount = cursor.ConsecutiveUnchangedCount + 1 }
                    : cursor with { LastObservedValue = sample, ConsecutiveUnchangedCount = 0 };
        }
    }

    /// <summary>
    /// §7 policy 级出口（成功 / invalidation 共用）：捕获
    /// <see cref="_pendingPolicyOutcome"/>（活到下次咨询、消费即清；
    /// Progress.PolicyState 数据源）→ 清空 policy 执行态 → 回 NeedDecision。
    /// 成功出口 phase = 既有 <see cref="AgentDecisionPhase.StepVerified"/>；
    /// invalidation 出口 phase = <see cref="AgentDecisionPhase.PolicyInvalidated"/>
    ///（typed cause，reason 原文经 _pendingFailure* 同构捕获，M2 不净化）。
    /// 预算门（F10(b)）：invalidation 时 RoundsRemaining ≤ 0 → 由 NeedDecision
    /// 既有 consult-budget-exhausted 检查如实终局（同语义零重复执法）。
    /// </summary>
    private void ExitPolicy(AgentDecisionPhase phase, string reason, PolicyTruth terminationStatus)
    {
        var state = _policy!;
        _pendingPolicyOutcome = new PendingPolicyOutcome(
            phase, reason,
            new PolicyProgressState(state.PolicyId, state.ApplicationsUsed, terminationStatus));
        if (phase == AgentDecisionPhase.PolicyInvalidated)
        {
            _pendingFailureReason = reason;
            _pendingFailurePhase = AgentDecisionPhase.PolicyInvalidated;
            _pendingFailedStepIndex = null;
        }
        _policy = null;
        _policyStep = null;
        _guardCursors = Array.Empty<PolicyGuardCursor?>();
        _adoptedDecision = null;
        _phase = DrivePhase.NeedDecision;
    }

    /// <summary>
    /// §7 步链失败（grounding/gate/verification/no-root）时的 policy 作废：
    /// 既有 StepRejected/VerificationFailed 转移原样保留，本方法只清空
    /// policy 执行态并把 policy 摘要并入 _pendingPolicyOutcome（「既有载荷 +
    /// policy 摘要」）。无活跃 policy 时零副作用。
    /// </summary>
    private void VoidActivePolicyForStepFailure(AgentDecisionPhase phase, string reason)
    {
        if (_policy is not { } state)
            return;
        _pendingPolicyOutcome = new PendingPolicyOutcome(
            phase, reason,
            new PolicyProgressState(
                state.PolicyId, state.ApplicationsUsed,
                state.LastTerminationStatus ?? PolicyTruth.Unknown));
        _policy = null;
        _policyStep = null;
        _guardCursors = Array.Empty<PolicyGuardCursor?>();
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
