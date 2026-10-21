using UniClaw.Kernel.Control;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.World;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

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

    /// <summary>Typed read-only traversal handler is still running; DriveAsync can resume it.</summary>
    WaitingForHandler,
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

        /// <summary>
        /// AGT-009 §11：advisory Plan 展开轮（每轮恰消费一项；良基——每轮
        /// 消费一项 / 物化单步进 StepAct 链 / 有界转移退出）。ActItem 物化
        /// 为单步复用 StepAct→StepVerify 全链（零新执行器）；ObserveItem
        /// 拉取一轮外部观察（零 Effect）；ControlItem 只做 Reobserve /
        /// Replan / Stop 的有界转移。
        /// </summary>
        PlanExpand,

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
    private ObservationRecoveryControl? _observationRecovery;
    private string? _observationPullSlot;
    private readonly List<KernelResult> _observationRecoveryEvidence = new();
    private readonly HashSet<string> _observationRecoveryFailures = new(StringComparer.Ordinal);

    /// <summary>C-2：最近一次 dispatch 的下游确认时间（StepVerify 时序门执法）。</summary>
    private DateTimeOffset? _lastDispatchAt;

    // ---- RUN-004：多轮协议字段（§2 F1'）----

    /// <summary>咨询轮次计数（DecisionId 序号）。</summary>
    private int _consultCounter;

    /// <summary>已派发步计数（预算执法）。</summary>
    private int _stepsDispatched;

    /// <summary>层2 归档：每步验证成功时追加（decisionN, stepIndex, receiptId）。</summary>
    private readonly List<(int DecisionN, int StepIndex, string ReceiptId)> _completedSteps = new();
    private readonly HashSet<(int DecisionN, int StepIndex)> _linearStepAnchors = new();
    private WorldBeliefRevision? _preDispatchBelief;

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
    private RunDriveResult? _traversalCancelDisposition;

    /// <summary>上一个回答（校验期的对照引用；当前回答校验通过并采纳后才回填）。</summary>
    private AgentDecision? _lastAnswer;

    /// <summary>当前 Defer 链已执行的等待轮数（每轮 = 1 拉取 + 1 再咨询，M1）。</summary>
    private int _deferRoundsUsed;

    /// <summary>Defer 配额耗尽：下一轮咨询以 DeferRoundsExhausted 相位发出（T6 终问）。</summary>
    private bool _deferExhaustedPending;

    /// <summary>上一次咨询的失败上下文（E2/E3 捕获点原文，M2 不净化）。</summary>
    private string? _pendingFailureReason;

    // ---- PROTOCOL-FIX-003（ADR-0050）：预算续授轻记账（driver ephemeral——
    //      不持久化、不进 canonical Run State（不变量 36 精神）、非 authority；
    //      拒续/触顶 → 既有 budget-exhausted 诚实终局）----

    /// <summary>已授出的续授步数（追加到契约初始额度之上的部分）。</summary>
    private int _grantedRenewalSteps;

    /// <summary>已授出的续授轮次。</summary>
    private int _grantedRenewalRounds;

    /// <summary>当前咨询的续授窗口开关（每次咨询进入决策边界时重算）。</summary>
    private bool _renewalWindowOpen;

    /// <summary>续授留痕账本（授/拒均记；只读审计投影经 RenewalsGranted）。</summary>
    private readonly List<BudgetRenewalRecord> _renewalLedger = new();
    private int _budgetReviewOrdinal;
    private string? _activeBudgetReviewId;
    private int _budgetReviewAttempt;
    private int _stepsAtLastGrant;
    private int _roundsAtLastGrant;
    private int _stepsVerifiedAtLastGrant;
    private bool _budgetReviewStrengthened;
    private AgentProgressAssessment? _lastReviewAssessment;
    private readonly List<BudgetReviewHistoryEntry> _budgetReviewHistory = new();
    private string? _lastHandledBudgetReviewTrigger;
    private int _lastOverviewStepsConsumed;
    private int _lastOverviewRoundsConsumed;
    private int _lastOverviewStepsVerified;
    private HashSet<string> _lastOverviewSourceRefs = new(StringComparer.Ordinal);
    private HashSet<string> _lastOverviewKnownItems = new(StringComparer.Ordinal);
    private HashSet<string> _lastOverviewUnknowns = new(StringComparer.Ordinal);
    private ExecutionOverviewProjection? _segmentStartProjection;
    private string? _segmentStartExpectation;
    private readonly HashSet<string> _lastGrantedProgressFactKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _fundedItemProgress = new(StringComparer.Ordinal);
    private ExecutionOverviewProjection? _consultationProjectionSnapshot;

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

    // ---- AGT-009：advisory Plan 展开运行时（全部 driver ephemeral——
    //      不持久化、非 recovery state、非 authority）----

    /// <summary>advisory 计划项数上限（V7 执法；与 MaxProposalSteps 同界）。</summary>
    private const int MaxPlanItems = 16;

    /// <summary>已采纳 Plan 项（null = 无活跃 plan）。</summary>
    private IReadOnlyList<PlanItem>? _planItems;

    /// <summary>当前 Plan 项游标（_completedSteps 归档索引 = 项序）。</summary>
    private int _planItemIndex;

    /// <summary>当前 ActItem 物化的单步（目标来自计划——Kernel 零猜测）。</summary>
    private AgentActionStep? _planStep;
    private AgentPlanProposal? _activePlanProposal;
    private PlanExecutionStatus _planStatus = PlanExecutionStatus.Completed;
    private string? _planInterruptionReason;
    private bool _planPaused;
    private PlanItem.ForEachItem? _activeForEach;
    private int _forEachIteration;
    private int _forEachBodyIndex;
    private string? _activeCandidateKey;
    private PlanCandidate? _activeCandidate;
    private string? _activeCandidateRevision;
    private IReadOnlyList<string> _activeGuardEvidenceRefs = Array.Empty<string>();
    private string? _forEachScopeKey;
    private readonly HashSet<string> _seenCandidateKeys = new(StringComparer.Ordinal);
    private TraversalControl _traversal => _kernel.TraversalControl;
    private PlanItem.TraverseItem? _activeTraversal;
    private TraversalControl.Work? _traversalWork;
    private string? _traversalVerifiedContainerId;
    private int _traversalHandlerIndex;
    private PlanLanguageHandlerCall? _pendingPlanLanguageCall;
    private PendingPlanLanguageCall? _pendingPlanLanguage;
    private string? _traversalHandlerPhase;
    private string? _traversalRoot;
    private readonly List<PlanAttempt> _planAttempts = new();
    private readonly List<PlanConditionEvaluation> _planConditionEvaluations = new();
    private readonly List<PlanBlockedEvidence> _blockedCandidates = new();
    private int _planWorkUsed;
    private int _driveInProgress;
    private int _planAttemptOrdinal;
    private int? _activeAttemptIndex;
    private readonly Dictionary<string, int> _latestPlanVersionById = new(StringComparer.Ordinal);

    // Control frames are execution cursors, never world/task state or another driver.
    private sealed record PlanBlockFrame(
        IReadOnlyList<PlanItem> Items, int ItemIndex,
        PlanItem.ForEachItem? Loop, int Iteration, int BodyIndex,
        PlanCandidate? Candidate, string? CandidateRevision, string? ScopeKey,
        string[] SeenKeys, PlanCandidate? InheritedCandidate,
        string Kind, int Repetitions)
    {
        public int BlockIteration { get; set; }
    }
    private readonly Stack<PlanBlockFrame> _planBlocks = new();
    private PlanCandidate? _inheritedPlanCandidate;
    private PlanCandidate? CurrentPlanCandidate => _activeCandidate ?? _inheritedPlanCandidate;

    private sealed record PendingPlanLanguageCall(
        PlanLanguageHandler Binding,
        PlanHandlerInput Input,
        TraversalControl.Work Work,
        string Phase,
        string Revision,
        string CallId,
        TimeSpan Timeout,
        Stopwatch Timer,
        long StartedTimestamp);

    private readonly RunDriverInputs _inputs;
    private readonly IConsultationJournal _consultationJournal;
    private readonly object _driverIdentity = new();
    private DrivePhase _phase = DrivePhase.NeedInitialObservation;
    private AgentDecision? _adoptedDecision;
    private string? _consultRejection;
    private int _stepIndex;
    private string? _boundarySignature;
    private int _boundarySequence;
    private InterruptionSignal? _pendingInterruption;

    public KernelRunDriver(UniKernel kernel, AgentPlanPolicy plan, RunDriverInputs inputs)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        _consultationJournal = inputs.ConsultationJournal ?? new InMemoryConsultationJournal();
    }

    /// <summary>Current read-only Plan execution and attempt evidence.</summary>
    public PlanExecutionView? PlanExecution => CurrentPlanExecutionView();

    /// <summary>Current read-only traversal execution record.</summary>
    public TraversalExecutionView? TraversalExecution => _kernel.TraversalExecution;

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
        if (Interlocked.CompareExchange(ref _driveInProgress, 1, 0) != 0)
            return DriveInProgressResult();
        try { return DriveCore(); }
        finally { Volatile.Write(ref _driveInProgress, 0); }
    }

    private RunDriveResult DriveCore()
    {
        if (!_kernel.IsActivated)
            return new RunDriveResult(RunDriveStatus.NotActivated, "drive-before-activation", null, 0);
        if (!_kernel.IsActivationOwner(_driverIdentity))
            return new RunDriveResult(
                RunDriveStatus.NotActivated, "activation-owned-by-another-driver", null,
                _kernel.EffectReceipts.Count);
        if (_kernel.IsRunTerminal)
            return new RunDriveResult(RunDriveStatus.AlreadyTerminal, "already-terminal", null, 0);
        if (_traversalCancelDisposition is { } cancelledTraversal)
            return cancelledTraversal;

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
        // A rejected consultation is a durable fail-closed disposition. A
        // later Drive must report it, not mint a new correlation automatically.
        if (_consultRejection is not null && _adoptedDecision is null)
            return new RunDriveResult(
                RunDriveStatus.AgentDecisionFailed, _consultRejection, null,
                _kernel.EffectReceipts.Count);

        while (true)
        {
            if (_pendingPlanLanguageCall is not null)
            {
                if (TryFinishPendingPlanLanguageCall()) continue;
                return new RunDriveResult(RunDriveStatus.WaitingForHandler,
                    "traversal:waiting-for-handler", null, _kernel.EffectReceipts.Count);
            }

            // Cancellation has its own lifecycle feed and is sampled at every
            // safe driver boundary. StepVerify owns an in-flight delivery
            // receipt, so defer the poll until its verification facts settle.
            if (_phase != DrivePhase.StepVerify && _inputs.PollCancellation is { } pollCancellation)
            {
                RunDriverInput.Cancel? cancel;
                try
                {
                    cancel = pollCancellation();
                }
                catch (Exception exception)
                {
                    return new RunDriveResult(
                        RunDriveStatus.UnexpectedInput,
                        $"cancellation-poll-failed:{exception.GetType().Name}",
                        null,
                        _kernel.EffectReceipts.Count);
                }

                if (cancel is not null)
                    return CancelPath(cancel);
            }

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
                        {
                            AbandonActiveTraversal("traversal:consult-budget-exhausted");
                            return new RunDriveResult(
                                RunDriveStatus.AgentDecisionFailed,
                                "consult-budget-exhausted", null, _kernel.EffectReceipts.Count);
                        }

                        // D2：本次咨询的续授窗口（进入决策边界时重算；续授后
                        // 剩余额度回升 → 下次咨询窗口自然关闭）。
                        _renewalWindowOpen = IsRenewalWindowOpen(budget);
                        _consultationProjectionSnapshot = _inputs.ProjectExecutionOverview?.Invoke();
                        _segmentStartExpectation ??= view.Objective;
                        if (_segmentStartProjection is null && !_renewalLedger.Any(record => record.Granted))
                            _segmentStartProjection = _consultationProjectionSnapshot;
                        var reviewTrigger = BudgetReviewTriggerSignature(_consultationProjectionSnapshot);
                        if (_plan.GoalDirectedContinuationEnabled && _renewalWindowOpen
                            && budget.RoundsRemaining >= 2
                            && _activeBudgetReviewId is null
                            && !string.Equals(reviewTrigger, _lastHandledBudgetReviewTrigger, StringComparison.Ordinal))
                        {
                            _activeBudgetReviewId = $"{_kernel.RunId}:budget-review-{++_budgetReviewOrdinal}";
                            _budgetReviewAttempt = 1;
                            _budgetReviewStrengthened = _renewalLedger.Any(r => !r.Granted
                                && r.RejectionReason is not "renewal:explicitly-declined")
                                || (_segmentStartProjection is not null
                                    && !HasNewProgressFacts(_renewalLedger.Any(record => record.Granted)
                                            ? _lastGrantedProgressFactKeys : _segmentStartProjection.ProgressFactKeys,
                                        _consultationProjectionSnapshot?.ProgressFactKeys));
                            if (_planStatus == PlanExecutionStatus.Active && _planItems is not null)
                                PausePlan("budget-review");
                        }
                        var consulted = ConsultAgentV2(view);
                        _adoptedDecision = consulted.Value;
                        _consultRejection = consulted.Rejection;
                        if (_activeBudgetReviewId is not null && _consultRejection == "no-response")
                        {
                            if (_budgetReviewAttempt == 1 && CurrentBudget(view).RoundsRemaining > 0)
                            {
                                _budgetReviewAttempt = 2;
                                _adoptedDecision = null;
                                _consultRejection = null;
                                continue;
                            }
                            _renewalLedger.Add(new BudgetRenewalRecord(
                                DecisionIdForRun(_consultCounter), 0, 0, null, false,
                                "renewal:review-incomplete", _activeBudgetReviewId, ReviewComplete: false));
                            _budgetReviewHistory.Add(new BudgetReviewHistoryEntry(
                                _activeBudgetReviewId, null, null, _consultationProjectionSnapshot?.PreviousSegmentActual,
                                Math.Max(0, _stepsDispatched - _stepsAtLastGrant),
                                Math.Max(0, _consultCounter - _roundsAtLastGrant), 0, 0, Complete: false,
                                ProjectionRevision: _consultationProjectionSnapshot?.ProjectionRevision,
                                ProgressFactKeys: _consultationProjectionSnapshot?.ProgressFactKeys));
                            _lastHandledBudgetReviewTrigger = reviewTrigger;
                            _activeBudgetReviewId = null;
                            _adoptedDecision = null;
                            _consultRejection = null;
                            continue;
                        }
                        if (_activeBudgetReviewId is not null && _consultRejection is null)
                        {
                            var response = ReviewResponseFor(_adoptedDecision);
                            if (response is null || response.ReviewId != _activeBudgetReviewId
                                || response.Assessment is null
                                || response.Assessment.OverviewId != _activeBudgetReviewId)
                            {
                                if (_budgetReviewAttempt == 1 && CurrentBudget(view).RoundsRemaining > 0)
                                {
                                    _budgetReviewAttempt = 2;
                                    _adoptedDecision = null;
                                    continue;
                                }
                                _renewalLedger.Add(new BudgetRenewalRecord(
                                    DecisionIdForRun(_consultCounter), 0, 0, null, false,
                                    "renewal:review-incomplete", _activeBudgetReviewId, ReviewComplete: false));
                                _budgetReviewHistory.Add(new BudgetReviewHistoryEntry(
                                    _activeBudgetReviewId, null, null,
                                    _consultationProjectionSnapshot?.PreviousSegmentActual,
                                    Math.Max(0, _stepsDispatched - _stepsAtLastGrant),
                                    Math.Max(0, _consultCounter - _roundsAtLastGrant), 0, 0, Complete: false,
                                    ProjectionRevision: _consultationProjectionSnapshot?.ProjectionRevision));
                                _lastHandledBudgetReviewTrigger = reviewTrigger;
                                _activeBudgetReviewId = null;
                                _adoptedDecision = null;
                                continue;
                            }
                            if (_adoptedDecision is AgentDecision.ContinuePlan
                                && response.Outcome is not (BudgetReviewOutcome.ContinueWithoutRenewal
                                    or BudgetReviewOutcome.RequestRenewal))
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed,
                                    "plan:review-outcome-conflicts-with-resume", null, _kernel.EffectReceipts.Count);
                            // Preserve the expectation that governed the spent
                            // segment before a grant installs the next expectation.
                            var reviewedSegmentExpectation = _segmentStartExpectation ?? view.Objective;
                            var reviewedProjection = _consultationProjectionSnapshot;
                            var reviewedSegmentActual = FormatSegmentActual(reviewedProjection,
                                new HashSet<string>(reviewedProjection?.KnownItems ?? Array.Empty<string>(), StringComparer.Ordinal),
                                new HashSet<string>(reviewedProjection?.Unknowns ?? Array.Empty<string>(), StringComparer.Ordinal));
                            if (response.Outcome == BudgetReviewOutcome.RequestRenewal)
                            {
                                var reviewProjection = _consultationProjectionSnapshot;
                                if (response.Renewal is null)
                                    _renewalLedger.Add(new BudgetRenewalRecord(
                                        DecisionIdForRun(_consultCounter), 0, 0, null, false,
                                        "renewal:request-missing", _activeBudgetReviewId));
                                else if (LegacyRenewalFor(_adoptedDecision) is not null)
                                    _renewalLedger.Add(new BudgetRenewalRecord(
                                        DecisionIdForRun(_consultCounter), response.Renewal.AdditionalSteps,
                                        response.Renewal.AdditionalRounds, response.Renewal.Justification,
                                        false, "renewal:conflicting-legacy-and-review-request", _activeBudgetReviewId));
                                else if (string.IsNullOrWhiteSpace(response.Assessment?.NextDirection)
                                    || string.IsNullOrWhiteSpace(response.Assessment.NextSegmentExpectation)
                                    || response.Assessment.ExpectedObservableEvidence.Count == 0
                                    || response.Assessment.EvidenceRefs.Count == 0
                                    || (string.IsNullOrWhiteSpace(response.Assessment.Rationale)
                                        && string.IsNullOrWhiteSpace(response.Renewal.Justification)))
                                    _renewalLedger.Add(new BudgetRenewalRecord(
                                        DecisionIdForRun(_consultCounter), response.Renewal.AdditionalSteps,
                                        response.Renewal.AdditionalRounds, response.Renewal.Justification,
                                        false, "renewal:next-segment-evidence-required", _activeBudgetReviewId));
                                else if (reviewProjection?.ActionableItems is not null
                                    && !IsCurrentActionableItem(response.Assessment.NextItemKey, reviewProjection))
                                    _renewalLedger.Add(new BudgetRenewalRecord(
                                        DecisionIdForRun(_consultCounter), response.Renewal.AdditionalSteps,
                                        response.Renewal.AdditionalRounds, response.Renewal.Justification,
                                        false, "renewal:next-item-not-actionable", _activeBudgetReviewId));
                                else if (_renewalLedger.Any(record => record.Granted)
                                    && (reviewProjection?.ActionableItems is not null
                                        ? !HasUnfundedItemBasis(response.Assessment.NextItemKey!, reviewProjection)
                                        : !HasNewProgressFacts(_lastGrantedProgressFactKeys,
                                            reviewProjection?.ProgressFactKeys)))
                                    _renewalLedger.Add(new BudgetRenewalRecord(
                                        DecisionIdForRun(_consultCounter), response.Renewal.AdditionalSteps,
                                        response.Renewal.AdditionalRounds, response.Renewal.Justification,
                                        false, "renewal:repeat-route-without-new-evidence", _activeBudgetReviewId));
                                else
                                {
                                    ProcessRenewal(response.Renewal, _activeBudgetReviewId);
                                    if (_renewalLedger.LastOrDefault(r => r.ReviewId == _activeBudgetReviewId)?.Granted == true)
                                    {
                                        _segmentStartProjection = reviewProjection;
                                        _segmentStartExpectation = response.Assessment?.NextSegmentExpectation;
                                        // Preserve all facts already used for grants; an old
                                        // state reappearing cannot become new progress again.
                                        _lastGrantedProgressFactKeys.UnionWith(
                                            reviewProjection?.ProgressFactKeys ?? Array.Empty<string>());
                                        if (response.Assessment.NextItemKey is { } itemKey
                                            && reviewProjection?.ActionableItems?.TryGetValue(itemKey, out var item) == true)
                                        {
                                            if (!_fundedItemProgress.TryGetValue(itemKey, out var fundedFacts))
                                                _fundedItemProgress[itemKey] = fundedFacts = new(StringComparer.Ordinal);
                                            fundedFacts.UnionWith(item.ProgressFactKeys);
                                        }
                                    }
                                }
                            }
                            else if (response.Renewal is not null)
                            {
                                _renewalLedger.Add(new BudgetRenewalRecord(
                                    DecisionIdForRun(_consultCounter), response.Renewal.AdditionalSteps,
                                    response.Renewal.AdditionalRounds, response.Renewal.Justification,
                                    false, "renewal:outcome-does-not-request-renewal", _activeBudgetReviewId));
                            }
                            else if (response.Outcome == BudgetReviewOutcome.ContinueWithoutRenewal)
                                _renewalLedger.Add(new BudgetRenewalRecord(
                                    DecisionIdForRun(_consultCounter), 0, 0, null, false,
                                    "renewal:explicitly-declined", _activeBudgetReviewId));
                            if (_adoptedDecision is AgentDecision.ContinuePlan
                                && response.Outcome == BudgetReviewOutcome.RequestRenewal
                                && _renewalLedger.LastOrDefault(r => r.ReviewId == _activeBudgetReviewId)?.Granted != true)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed,
                                    "plan:resume-renewal-not-granted", null, _kernel.EffectReceipts.Count);
                            _lastReviewAssessment = response.Assessment;
                            var projection = _consultationProjectionSnapshot;
                            _budgetReviewHistory.Add(new BudgetReviewHistoryEntry(
                                _activeBudgetReviewId, response.Outcome,
                                reviewedSegmentExpectation,
                                reviewedSegmentActual,
                                Math.Max(0, _stepsDispatched - _stepsAtLastGrant),
                                Math.Max(0, _consultCounter - _roundsAtLastGrant),
                                _renewalLedger.LastOrDefault(r => r.ReviewId == _activeBudgetReviewId && r.Granted)?.AdditionalSteps ?? 0,
                                _renewalLedger.LastOrDefault(r => r.ReviewId == _activeBudgetReviewId && r.Granted)?.AdditionalRounds ?? 0,
                                Complete: true,
                                NextDirection: response.Assessment?.NextDirection,
                                EvidenceRefs: projection?.SourceFactRefs,
                                ProjectionRevision: projection?.ProjectionRevision,
                                ProgressFactKeys: projection?.ProgressFactKeys,
                                NextItemKey: response.Assessment?.NextItemKey));
                            if (_renewalLedger.LastOrDefault(r => r.ReviewId == _activeBudgetReviewId)?.Granted == true)
                            {
                                _stepsAtLastGrant = _stepsDispatched;
                                _roundsAtLastGrant = _consultCounter;
                                _stepsVerifiedAtLastGrant = _completedSteps.Count;
                            }
                            _lastHandledBudgetReviewTrigger = reviewTrigger;
                            _activeBudgetReviewId = null;
                            _budgetReviewAttempt = 0;
                        }
                        // 注意：不再立即回填 _lastAnswer——校验通过并采纳后才成为
                        // 下一轮的「上一轮回答」（2026-09-23 语义修正：防止当前回答
                        // 被当成上一轮，导致首个 Defer 被判嵌套）。
                    }
                    if (_consultRejection is not null)
                    {
                        if (_consultRejection == "no-response"
                            && _activeTraversal is not null && _planPaused)
                        {
                            _consultRejection = null;
                            return new RunDriveResult(RunDriveStatus.WaitingForInput,
                                "traversal:awaiting-decision", null, _kernel.EffectReceipts.Count);
                        }
                        // P25 no-response is durably journaled as a rejected
                        // consultation. After an already-started run, retain
                        // the historical terminal-evaluation surface so the
                        // caller receives an honest non-proof outcome rather
                        // than a second consultation or a new Effect.
                        if (_consultRejection == "no-response"
                            && _pendingFailurePhase is not null)
                        {
                            _phase = DrivePhase.TerminalEvaluation;
                            _adoptedDecision = null;
                            _consultRejection = null;
                            continue;
                        }
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

                            // PROTOCOL-FIX-003 D3：NoAction 可携带续授提案
                            if (!_plan.GoalDirectedContinuationEnabled) ProcessRenewal(no.Proposal.Renewal);

                            // 采纳：当前回答成为下一轮对照；Defer 链终结
                            _lastAnswer = _adoptedDecision;
                            AbandonPausedPlan("plan:replaced-by-no-action");
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
                        case AgentDecision.Blocked blocked:
                        {
                            // AGT-019 M1：Agent 诚实申报卡死——零新 Effect
                            // 的确定性终局（defer-exhausted 同族；合法回答，
                            // 非 AgentDecisionFailed）。原因随 reason 留痕。
                            _lastAnswer = _adoptedDecision;
                            AbandonPausedPlan("plan:replaced-by-blocked");
                            _deferRoundsUsed = 0;
                            _deferExhaustedPending = false;
                            _adoptedDecision = null;
                            return new RunDriveResult(
                                RunDriveStatus.TerminalNotProven,
                                $"agent-blocked:{blocked.Reason}",
                                null,
                                _kernel.EffectReceipts.Count);
                        }
                        case AgentDecision.Skip skip:
                        {
                            var rejection = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (rejection is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, rejection, null, 0);
                            if (_inputs.MarkTargetCoverage is null)
                                return new RunDriveResult(
                                    RunDriveStatus.AgentDecisionFailed,
                                    "skip-owner-unavailable", null, _kernel.EffectReceipts.Count);
                            try
                            {
                                _inputs.MarkTargetCoverage(new TargetCoverageMarkRequest(
                                    skip.Target, skip.DecisionId, skip.Reason, skip.EvidenceRefs));
                            }
                            catch (Exception ex)
                            {
                                return new RunDriveResult(
                                    RunDriveStatus.AgentDecisionFailed,
                                    "skip-owner-failed:" + ex.GetType().Name, null,
                                    _kernel.EffectReceipts.Count);
                            }
                            _lastAnswer = _adoptedDecision;
                            AbandonPausedPlan("plan:replaced-by-skip");
                            _deferRoundsUsed = 0;
                            _deferExhaustedPending = false;
                            _adoptedDecision = null;
                            _pendingInterruption = null;
                            _phase = DrivePhase.NeedDecision;
                            continue;
                        }
                        case AgentDecision.Defer defer:
                        {
                            // V5 defer-unbounded（静态界）
                            var v5 = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (v5 is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, v5, null, 0);

                            // PROTOCOL-FIX-003 D3：Defer 可携带续授提案（规则侧
                            // 封顶；拒续仅留痕、按无续授继续）。
                            var renewalGranted = !_plan.GoalDirectedContinuationEnabled && ProcessRenewal(defer.Renewal);

                            // T6 终问（配额已尽）：仍 Defer → 确定性终局（不再
                            // 拉取/咨询）；但终问携带**已获授**续授时闭合死锁角
                            //（ADR-0050）：轮次续上 → Defer 链重新有界继续。
                            if (_deferExhaustedPending)
                            {
                                if (renewalGranted)
                                {
                                    _deferExhaustedPending = false;
                                    _deferRoundsUsed = 0;
                                    _lastAnswer = _adoptedDecision;
                                    _adoptedDecision = null;
                                    var granted = PullObservations(
                                        ObservationContext.External, "defer-observation");
                                    if (granted.Stop is not null)
                                        return granted.Stop;
                                    continue;
                                }
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
                            // PROTOCOL-FIX-003 D3：Act 可携带续授提案（规则侧封顶）
                            if (!_plan.GoalDirectedContinuationEnabled) ProcessRenewal(act.Proposal.Renewal);
                            _lastAnswer = _adoptedDecision;
                            AbandonPausedPlan("plan:replaced-by-act");
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
                            AbandonPausedPlan("plan:replaced-by-policy");
                            _guardCursors = new PolicyGuardCursor?[
                                policy.Proposal.Guards?.Count ?? 0];
                            _adoptedPolicyIds[policy.Proposal.PolicyId] = _consultCounter;
                            _lastAnswer = _adoptedDecision;
                            _deferRoundsUsed = 0;
                            _deferExhaustedPending = false;
                            _phase = DrivePhase.PolicyExpand;
                            continue;
                        }
                        case AgentDecision.Plan plan:
                        {
                            // AGT-009：V7 机械校验（fail closed，零新 Effect，
                            // 映射回既有 AgentDecisionFailed 语义）。
                            var rejection = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (rejection is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, rejection, null, 0);

                            // 采纳（V7 通过即采纳，原子）：计划项进入 ephemeral
                            // 游标；PlanExpand 逐项物化——每次最多一个 Act。
                            AbandonPausedPlan("plan:replaced-by-new-version");
                            _planBlocks.Clear();
                            _inheritedPlanCandidate = null;
                            _planItems = plan.Proposal.Items;
                            if (plan.Proposal.PlanId is { } planId)
                                _latestPlanVersionById[planId] = plan.Proposal.Version;
                            _activePlanProposal = plan.Proposal;
                            _planStatus = PlanExecutionStatus.Active;
                            _planPaused = false;
                            _planInterruptionReason = null;
                            _planWorkUsed = 0;
                            _planItemIndex = 0;
                            ResetForEach();
                            _lastAnswer = _adoptedDecision;
                            _deferRoundsUsed = 0;
                            _deferExhaustedPending = false;
                            _phase = DrivePhase.PlanExpand;
                            continue;
                        }
                        case AgentDecision.ContinuePlan resume:
                        {
                            var rejection = ValidateDecision(_adoptedDecision!, view, CurrentBudget(view), _lastAnswer);
                            if (rejection is not null)
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed, rejection, null, 0);
                            if (CurrentBudget(view).StepsRemaining <= 0)
                            {
                                AbandonPausedPlan("plan:resume-without-step-budget");
                                _adoptedDecision = null;
                                return new RunDriveResult(RunDriveStatus.AgentDecisionFailed,
                                    "plan:resume-without-step-budget", null, _kernel.EffectReceipts.Count);
                            }
                            _lastAnswer = _adoptedDecision;
                            _planPaused = false;
                            _planStatus = PlanExecutionStatus.Active;
                            _planInterruptionReason = null;
                            _traversal.Resume();
                            RecordTraversalState();
                            _adoptedDecision = null;
                            _phase = DrivePhase.PlanExpand;
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
                        //（plan 模式步列表恒为物化单步，此分支不可达——防御清空）
                        _pendingFailureReason = null;
                        _pendingFailurePhase = null;
                        _pendingFailedStepIndex = null;
                        _adoptedDecision = null; // 清空触发再咨询
                        _planItems = null;
                        _planItemIndex = 0;
                        _planStep = null;
                        _phase = DrivePhase.NeedDecision;
                        continue;
                    }

                    // 采纳「仅本 step」的 TargetSpec（Control 仍独占 intent 签发）
                    var step = steps[_stepIndex];
                    _activeGuardEvidenceRefs = Array.Empty<string>();
                    if (_inputs.GuardStep is { } stepGuard)
                    {
                        var guard = stepGuard(step, BuildGuardContext(view));
                        _activeGuardEvidenceRefs = guard.EvidenceRefs ?? Array.Empty<string>();
                        if (guard.Verdict == PlanStepGuardVerdict.AlreadySatisfied)
                        {
                            if (_activeTraversal is not null && _traversalWork is not null)
                            {
                                RecordPlanAttempt(step, PlanAttemptStatus.AlreadySatisfied, null,
                                    guard.EvidenceRefs ?? Array.Empty<string>(), guard.Reason);
                                _traversal.Gap(_traversalWork.ContainerId,
                                    _traversalWork.Candidate?.Target.Key,
                                    "navigation-already-satisfied-without-page-transition",
                                    guard.EvidenceRefs ?? Array.Empty<string>());
                                _planStep = null;
                                PausePlan("traversal:navigation-already-satisfied-requires-revision");
                                continue;
                            }
                            if (_planItems is not null)
                            {
                                RecordPlanAttempt(step, PlanAttemptStatus.AlreadySatisfied, null,
                                    guard.EvidenceRefs ?? Array.Empty<string>(), guard.Reason);
                                AdvancePlanItem();
                                _phase = DrivePhase.PlanExpand;
                                continue;
                            }
                            if (_policy is not null)
                            {
                                _policy = _policy with { ApplicationsUsed = _policy.ApplicationsUsed + 1 };
                                _phase = DrivePhase.PolicyExpand;
                                continue;
                            }
                            _stepIndex++;
                            continue;
                        }
                        if (guard.Verdict is PlanStepGuardVerdict.Reject or PlanStepGuardVerdict.Unknown)
                        {
                            if (_planItems is not null)
                            {
                                RecordPlanAttempt(step,
                                    guard.Verdict == PlanStepGuardVerdict.Unknown ? PlanAttemptStatus.UnknownOutcome : PlanAttemptStatus.Rejected,
                                    null, guard.EvidenceRefs ?? Array.Empty<string>(), guard.Reason);
                            }
                            VoidActivePolicyForStepFailure(AgentDecisionPhase.StepRejected, guard.Reason);
                            VoidActivePlanForStepFailure();
                            _pendingFailureReason = guard.Reason;
                            _pendingFailurePhase = AgentDecisionPhase.StepRejected;
                            _pendingFailedStepIndex = _planItemIndex;
                            _adoptedDecision = null;
                            _phase = DrivePhase.NeedDecision;
                            continue;
                        }
                    }
                    if (_planItems is not null)
                    {
                        BeginPlanAttempt(step);
                    }
                    _plan.Adopt(new[]
                    {
                        // PER-014 R3：DesiredState → typed CheckedState（Policy
                        // 模板 string 词汇经 FromPresentation 严格解析；未知词
                        // = null = 无期望终态检查，不猜）。
                        new TargetSpec(step.TargetRole, step.TargetDescriptor, step.EffectClass,
                            CheckedSemantics.FromPresentation(step.DesiredState)),
                    });

                    // Act 链（Control → Grounding → Binding → Assurance → Gate → Dispatch）
                    var root = CurrentInteractionContainerId(_kernel.CurrentBelief);
                    if (root is null)
                    {
                        UpdateActivePlanAttempt(PlanAttemptStatus.Rejected, null,
                            Array.Empty<string>(), "no-single-root-container");
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.StepRejected, "no-single-root-container");
                        VoidActivePlanForStepFailure();
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
                            // AGT-009：plan 展开中 = 当前 ActItem 目标已满足
                            //（零 dispatch）→ 消费本项继续计划（E4 语义）
                            if (_planItems is not null)
                            {
                                UpdateActivePlanAttempt(PlanAttemptStatus.AlreadySatisfied, null,
                                    Array.Empty<string>(), "control:no-action-required");
                                if (_activeTraversal is not null && _traversalWork is { Return: false } work
                                    && work.Operation.Kind == TraversalOperationKind.Navigate)
                                {
                                    _traversal.Gap(work.ContainerId, work.Candidate?.Target.Key,
                                        "navigation-already-satisfied-without-page-transition",
                                        _activeGuardEvidenceRefs);
                                    PausePlan("traversal:navigation-already-satisfied-requires-revision");
                                    continue;
                                }
                                AdvancePlanItem();
                                _phase = DrivePhase.PlanExpand;
                                if (_planItems is not null && (_planItemIndex < _planItems.Count || _planBlocks.Count > 0)
                                    && _plan.GoalDirectedContinuationEnabled
                                    && CurrentBudget(view).RoundsRemaining >= 2
                                    && IsRenewalWindowOpen(CurrentBudget(view)))
                                {
                                    PausePlan("plan:budget-checkpoint");
                                    continue;
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
                        if (intent.Kind == ControlIntentKind.Observe && intent.TargetSubject is not null)
                            UpdateActivePlanAttempt(PlanAttemptStatus.Rejected, null,
                                Array.Empty<string>(), "focused-reobserve-exhausted");
                        return new RunDriveResult(
                            RunDriveStatus.GroundingFailed,
                            intent.Kind == ControlIntentKind.Observe && intent.TargetSubject is not null
                                ? "focused-reobserve-exhausted"
                                : "control-issued-non-act-intent",
                            null, 0);
                    }

                    _preDispatchBelief = _kernel.CurrentBelief;
                    var grounded = _kernel.ActViaCurrentGrounding(
                        intent, new TargetDescriptor(step.TargetRole, step.TargetDescriptor));
                    if (grounded.Act is null)
                    {
                        var reason = $"grounding:{grounded.View.Result}";
                        UpdateActivePlanAttempt(PlanAttemptStatus.Rejected, null, Array.Empty<string>(), reason);
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.StepRejected, reason);
                        VoidActivePlanForStepFailure();
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
                        UpdateActivePlanAttempt(PlanAttemptStatus.Rejected, null, Array.Empty<string>(), reason);
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.StepRejected, reason);
                        VoidActivePlanForStepFailure();
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
                    {
                        UpdateActivePlanAttempt(PlanAttemptStatus.Unconfirmed,
                            grounded.Act.Receipt.ReceiptId, Array.Empty<string>(), $"unconfirmed:{grounded.Act.Receipt.Outcome}");
                        _phase = DrivePhase.StepVerify;
                        return new RunDriveResult(
                            RunDriveStatus.UnconfirmedDelivery,
                            $"unconfirmed:{grounded.Act.Receipt.Outcome}", null, 1);
                    }
                    UpdateActivePlanAttempt(PlanAttemptStatus.Started,
                        grounded.Act.Receipt.ReceiptId, Array.Empty<string>(), null);

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
                    {
                        // Preserve every available verification fact first. If
                        // the post-action feed is empty, lifecycle cancellation
                        // may still stop an in-flight wait without treating the
                        // receipt as verified.
                        if (post.Stop.Status == RunDriveStatus.WaitingForInput
                            && _inputs.PollCancellation is { } pollDuringVerificationWait)
                        {
                            RunDriverInput.Cancel? cancel;
                            try { cancel = pollDuringVerificationWait(); }
                            catch (Exception exception)
                            {
                                return new RunDriveResult(RunDriveStatus.UnexpectedInput,
                                    $"cancellation-poll-failed:{exception.GetType().Name}", null,
                                    _kernel.EffectReceipts.Count);
                            }
                            if (cancel is not null) return CancelPath(cancel);
                        }
                        return post.Stop;
                    }

                    var step = CurrentSteps()[_stepIndex];
                    var target = new TargetSpec(
                        step.TargetRole, step.TargetDescriptor, step.EffectClass,
                        CheckedSemantics.FromPresentation(step.DesiredState));
                    var verification = _kernel.VerifyPostActionEffect(
                        target, post.ProcessedObservations, _lastDispatchAt, _preDispatchBelief);
                    if (!verification.IsVerified)
                    {
                        var reason = verification.RejectionReason ?? "verification-failed";
                        UpdateActivePlanAttempt(PlanAttemptStatus.Rejected,
                            _kernel.EffectReceipts.LastOrDefault()?.ReceiptId,
                            post.ProcessedObservations.Select(o => o.Admission.EvidenceId)
                                .Where(id => id is not null).Cast<string>().ToArray(), reason);
                        VoidActivePolicyForStepFailure(AgentDecisionPhase.VerificationFailed, reason);
                        VoidActivePlanForStepFailure();
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
                    // 层2 归档：每步验证成功时追加（plan 模式 stepIndex =
                    // 计划项序——多 ActItem 锚点 step:N.{item} 保持唯一可核验）
                    _completedSteps.Add((_consultCounter,
                        _planItems is not null ? _planItemIndex : _stepIndex,
                        _kernel.EffectReceipts.LastOrDefault()?.ReceiptId ?? ""));
                    if (_activeForEach is null)
                        _linearStepAnchors.Add((_consultCounter,
                            _planItems is not null ? _planItemIndex : _stepIndex));
                    UpdateActivePlanAttempt(PlanAttemptStatus.Verified,
                        _kernel.EffectReceipts.LastOrDefault()?.ReceiptId,
                        post.ProcessedObservations.Select(o => o.Admission.EvidenceId)
                            .Where(id => id is not null).Cast<string>().ToArray(), null);
                    _preDispatchBelief = null;
                    _stepIndex++;
                    if (_policy is not null)
                    {
                        // RUN-005 §5：verified → ApplicationsUsed++ / GuardCursor
                        // 更新 → 回 PolicyExpand（良基：本轮 application 消耗）
                        _policy = _policy with { ApplicationsUsed = _policy.ApplicationsUsed + 1 };
                        UpdateGuardCursors();
                        _phase = DrivePhase.PolicyExpand;
                    }
                    else if (_activeTraversal is not null && _traversalWork is not null)
                    {
                        _traversalVerifiedContainerId = CurrentInteractionContainerId(_kernel.CurrentBelief);
                        _traversalHandlerPhase = "after";
                        _traversalHandlerIndex = 0;
                        _phase = DrivePhase.PlanExpand;
                    }
                    else if (_planItems is not null)
                    {
                        // AGT-009：本 ActItem verified（不变量 43 屏障已过）→
                        // 消费该项，回 PlanExpand 取下一项
                        AdvancePlanItem();
                        _phase = DrivePhase.PlanExpand;
                        if (_planItems is not null && (_planItemIndex < _planItems.Count || _planBlocks.Count > 0)
                            && _plan.GoalDirectedContinuationEnabled
                            && CurrentBudget(view).RoundsRemaining >= 2
                            && IsRenewalWindowOpen(CurrentBudget(view)))
                        {
                            PausePlan("plan:budget-checkpoint");
                            continue;
                        }
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

                case DrivePhase.PlanExpand:
                {
                    // AGT-009 §11：advisory 计划逐项消费（良基：每轮恰消费
                    // 一项 / 物化单步 / 有界转移退出；ActItem 复用
                    // StepAct→StepVerify 全链，零新执行器）。
                    var items = _planItems!;

                    if (_planItemIndex >= items.Count)
                    {
                        if (CompletePlanBlock()) continue;
                        // 计划耗尽 → 决策边界。注意：耗尽不构成 completion
                        // proof——ObserveItem 与耗尽都不能单独宣告任务完成，
                        // 终局仍由 NoAction+Completion / TerminalEvaluation 判定。
                        ExitPlan(AgentDecisionPhase.StepVerified, "plan-exhausted");
                        continue;
                    }

                    switch (items[_planItemIndex])
                    {
                        case PlanItem.TraverseItem traverse:
                            if (_activeTraversal is null)
                            {
                                var root = CurrentInteractionContainerId(_kernel.CurrentBelief);
                                if (root is null)
                                {
                                    ExitPlan(AgentDecisionPhase.StepRejected, "traversal:no-current-container");
                                    continue;
                                }
                                _activeTraversal = traverse;
                                _traversalVerifiedContainerId = null;
                                _kernel.BeginTraversalRecords();
                                _traversalRoot = root;
                                _traversal.ConfigureRules(traverse.Rules);
                                _traversal.Start(_activePlanProposal!.PlanId, _activePlanProposal.Version, root);
                RecordTraversalState();
                            }
                            if (AdvanceTraversal(view)) continue;
                            continue;
                        case PlanItem.ActItem act:
                        {
                            if (act.TargetRole == "$candidate" || act.TargetDescriptor == "$candidate")
                            {
                                if (CurrentPlanCandidate is not { } bound)
                                {
                                    ExitPlan(AgentDecisionPhase.StepRejected, "plan:unbound-candidate");
                                    continue;
                                }
                                act = act with
                                {
                                    TargetRole = act.TargetRole == "$candidate" ? bound.TargetRole : act.TargetRole,
                                    TargetDescriptor = act.TargetDescriptor == "$candidate" ? bound.TargetDescriptor : act.TargetDescriptor,
                                };
                            }
                            // 冲突失效（冻结决策 11）：ActItem 物化前世界存在
                            // 与其语义目标相交的权威域悬案 → 废弃剩余计划回决策
                            // 边界（下次咨询携带原因；预算由 NeedDecision 既有
                            // 门执法）。相交判定与 AgentPlanPolicy 聚焦复查
                            // 同律（"role[:desc]" 相等或前缀）——非相交悬案由
                            // 既有冲突裁决/聚焦机制处理，不一刀切废弃。
                            // Observe/Control 不受此门限制——它们正是冲突的
                            // 补证/转移路径。
                            if (PlanTargetConflicted(_kernel.CurrentConflictedSubjects, act))
                            {
                                ExitPlan(AgentDecisionPhase.StepRejected, "plan:world-conflict");
                                continue;
                            }
                            // 步预算逐项复检（V3 同律；采纳时已总检）
                            if (CurrentBudget(view).StepsRemaining <= 0)
                            {
                                PausePlan("plan:steps-exhausted");
                                continue;
                            }
                            if (_activePlanProposal is not null && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
                            {
                                PausePlan("plan:local-work-exhausted");
                                continue;
                            }
                            _planWorkUsed++;
                            // 物化单步（目标来自计划——Kernel 零猜测）
                            _planStep = new AgentActionStep(
                                act.TargetRole, act.TargetDescriptor,
                                act.EffectClass, act.DesiredState);
                            _stepIndex = 0;
                            _phase = DrivePhase.StepAct;
                            continue;
                        }
                        case PlanItem.IfItem branch:
                        {
                            if (!SpendPlanControlWork()) continue;
                            PlanConditionResult? result;
                            try { result = _inputs.EvaluatePlanCondition?.Invoke(branch.Condition, CurrentPlanCandidate); }
                            catch (Exception ex)
                            {
                                ExitPlan(AgentDecisionPhase.StepRejected, $"plan:condition-failed:{ex.GetType().Name}");
                                continue;
                            }
                            if (result is null || string.IsNullOrWhiteSpace(result.ScopeKey)
                                || string.IsNullOrWhiteSpace(result.Revision) || result.EvidenceRefs is null
                                || result.Truth is not (PolicyTruth.Satisfied or PolicyTruth.Violated or PolicyTruth.Unknown)
                                || (result.Truth != PolicyTruth.Unknown && result.EvidenceRefs.Count == 0))
                            {
                                ExitPlan(AgentDecisionPhase.StepRejected, "plan:condition-unavailable");
                                continue;
                            }
                            _planConditionEvaluations.Add(new PlanConditionEvaluation(
                                $"{_kernel.RunId}:plan-condition-{_planConditionEvaluations.Count + 1}",
                                branch.Condition, result, CurrentPlanPosition()));
                            if (result.Truth == PolicyTruth.Unknown)
                            {
                                ExitPlan(AgentDecisionPhase.StepRejected, $"plan:condition-unknown:{branch.Condition}:{result.Reason}");
                                continue;
                            }
                            var selected = result.Truth == PolicyTruth.Satisfied ? branch.Then : branch.Else;
                            if (selected.Count == 0) AdvancePlanItem();
                            else EnterPlanBlock(selected, result.Truth == PolicyTruth.Satisfied ? "then" : "else");
                            continue;
                        }
                        case PlanItem.RepeatItem repeat:
                            if (!SpendPlanControlWork()) continue;
                            EnterPlanBlock(repeat.Body, "repeat", repeat.MaxIterations);
                            continue;
                        case PlanItem.ForEachItem loop:
                        {
                            _activeForEach ??= loop;
                            if (!ReferenceEquals(_activeForEach, loop) && _activeForEach != loop)
                            {
                                ExitPlan(AgentDecisionPhase.StepRejected, "plan:foreach-state-mismatch");
                                continue;
                            }
                            if (_forEachIteration >= loop.MaxIterations)
                            {
                                _planItemIndex++;
                                ResetForEach();
                                continue;
                            }
                            if (_activeCandidate is null)
                            {
                                if (_activePlanProposal is not null
                                    && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
                                {
                                    PausePlan("plan:local-work-exhausted");
                                    continue;
                                }
                                _planWorkUsed++;
                                PlanCandidateSet? set;
                                try { set = _inputs.ProjectPlanCandidates?.Invoke(loop.Source); }
                                catch (Exception ex)
                                {
                                    ExitPlan(AgentDecisionPhase.StepRejected, $"plan:source-failed:{ex.GetType().Name}");
                                    continue;
                                }
                                if (set is null || string.IsNullOrWhiteSpace(set.ScopeKey)
                                    || string.IsNullOrWhiteSpace(set.Revision) || set.Candidates is null)
                                {
                                    ExitPlan(AgentDecisionPhase.StepRejected, "plan:source-unavailable");
                                    continue;
                                }
                                if (_forEachScopeKey is null) _forEachScopeKey = set.ScopeKey;
                                else if (!string.Equals(_forEachScopeKey, set.ScopeKey, StringComparison.Ordinal))
                                {
                                    ExitPlan(AgentDecisionPhase.StepRejected, "plan:source-scope-changed");
                                    continue;
                                }
                                if (set.Candidates.Any(c => c is null || string.IsNullOrWhiteSpace(c.Key)
                                    || string.IsNullOrWhiteSpace(c.TargetRole) || string.IsNullOrWhiteSpace(c.TargetDescriptor)
                                    || c.EvidenceRefs is null || c.EvidenceRefs.Count == 0)
                                    || set.Candidates.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != set.Candidates.Count)
                                {
                                    ExitPlan(AgentDecisionPhase.StepRejected, "plan:invalid-candidate-set");
                                    continue;
                                }
                                var candidate = set.Candidates.FirstOrDefault(c => !_seenCandidateKeys.Contains(c.Key));
                                if (candidate is null)
                                {
                                    _planItemIndex++;
                                    ResetForEach();
                                    continue;
                                }
                                if (candidate.Disposition == PlanCandidateDisposition.Unknown)
                                {
                                    _planInterruptionReason = $"plan:candidate-unknown:{candidate.Key}";
                                    ExitPlan(AgentDecisionPhase.StepRejected, _planInterruptionReason);
                                    continue;
                                }
                                if (candidate.Disposition == PlanCandidateDisposition.Blocked)
                                {
                                    if (!loop.ContinueOnBlocked || string.IsNullOrWhiteSpace(candidate.Reason)
                                        || candidate.EvidenceRefs.Count == 0)
                                    {
                                        _planInterruptionReason = $"plan:candidate-blocked:{candidate.Key}";
                                        ExitPlan(AgentDecisionPhase.StepRejected, _planInterruptionReason);
                                        continue;
                                    }
                                    _blockedCandidates.Add(new PlanBlockedEvidence(
                                        candidate.Key, loop.Source, candidate.Reason, candidate.EvidenceRefs,
                                        CurrentPlanPosition()));
                                    _seenCandidateKeys.Add(candidate.Key);
                                    _forEachIteration++;
                                    if (_forEachIteration >= loop.MaxIterations)
                                    {
                                        _planItemIndex++;
                                        ResetForEach();
                                    }
                                    continue;
                                }
                                if (candidate.Disposition != PlanCandidateDisposition.Ready)
                                {
                                    ExitPlan(AgentDecisionPhase.StepRejected, "plan:unknown-candidate-disposition");
                                    continue;
                                }
                                _activeCandidate = candidate;
                                _activeCandidateKey = candidate.Key;
                                _activeCandidateRevision = set.Revision;
                                _forEachBodyIndex = 0;
                            }
                            if (_forEachBodyIndex >= loop.Body.Count)
                            {
                                AdvancePlanItem();
                                continue;
                            }
                            switch (loop.Body[_forEachBodyIndex])
                            {
                                case PlanItem.ActItem bodyAct:
                                {
                                    var candidate = _activeCandidate!;
                                    var role = bodyAct.TargetRole == "$candidate" ? candidate.TargetRole : bodyAct.TargetRole;
                                    var descriptor = bodyAct.TargetDescriptor == "$candidate" ? candidate.TargetDescriptor : bodyAct.TargetDescriptor;
                                    var expanded = bodyAct with { TargetRole = role, TargetDescriptor = descriptor };
                                    if (PlanTargetConflicted(_kernel.CurrentConflictedSubjects, expanded))
                                    {
                                        ExitPlan(AgentDecisionPhase.StepRejected, "plan:world-conflict");
                                        continue;
                                    }
                                    if (CurrentBudget(view).StepsRemaining <= 0)
                                    {
                                        PausePlan("plan:steps-exhausted");
                                        continue;
                                    }
                                    if (_activePlanProposal is not null && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
                                    {
                                        PausePlan("plan:local-work-exhausted");
                                        continue;
                                    }
                                    _planWorkUsed++;
                                    _planStep = new AgentActionStep(role, descriptor, bodyAct.EffectClass, bodyAct.DesiredState);
                                    _stepIndex = 0;
                                    _phase = DrivePhase.StepAct;
                                    continue;
                                }
                                case PlanItem.ObserveItem observe:
                                {
                                    if (_activePlanProposal is not null && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
                                    {
                                        PausePlan("plan:local-work-exhausted");
                                        continue;
                                    }
                                    var observed = PullObservations(ObservationContext.External, "plan-observe",
                                        subjects: observe.Subject is null ? null : new[] { observe.Subject });
                                    if (observed.Stop is not null) return observed.Stop;
                                    _planWorkUsed++;
                                    AdvancePlanItem();
                                    continue;
                                }
                                default:
                                    if (!SpendPlanControlWork()) continue;
                                    EnterPlanBlock(new[] { loop.Body[_forEachBodyIndex] }, "candidate-body");
                                    continue;
                            }
                        }
                        case PlanItem.CheckpointItem checkpoint:
                        {
                            if (_activePlanProposal is not null
                                && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
                            {
                                PausePlan("plan:local-work-exhausted");
                                continue;
                            }
                            _planWorkUsed++;
                            AdvancePlanItem();
                            PausePlan(checkpoint.Reason ?? "plan:checkpoint");
                            continue;
                        }
                        case PlanItem.ObserveItem observe:
                        {
                            // 记录/补证（零 Effect）：拉取一轮外部观察经 P2 入证
                            //（WaitingForInput 可恢复：phase 与游标保持，续跑重拉）
                            if (_activePlanProposal is not null && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
                            {
                                PausePlan("plan:local-work-exhausted");
                                continue;
                            }
                            var observed = PullObservations(
                                ObservationContext.External, "plan-observe",
                                subjects: observe.Subject is null ? null : new[] { observe.Subject });
                            if (observed.Stop is not null)
                                return observed.Stop;
                            _planWorkUsed++;
                            _planItemIndex++;
                            continue;
                        }
                        case PlanItem.ControlItem control:
                        {
                            if (_activePlanProposal is not null
                                && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
                            {
                                PausePlan("plan:local-work-exhausted");
                                continue;
                            }
                            _planWorkUsed++;
                            switch (control.Kind)
                            {
                                case PlanControlKind.Reobserve:
                                    // 有界重观察：废弃剩余计划 + 拉取一轮外部
                                    // 观察，回决策边界（下次咨询消耗全局预算；
                                    // Defer 同构——WaitingForInput 后续跑咨询）
                                    ExitPlan(AgentDecisionPhase.StepVerified, "plan:reobserve");
                                    var reobserved = PullObservations(
                                        ObservationContext.External, "plan-reobserve");
                                    if (reobserved.Stop is not null)
                                        return reobserved.Stop;
                                    continue;
                                case PlanControlKind.Replan:
                                    // 重新规划：废弃剩余计划，回决策边界（全局
                                    // 咨询预算共享、不重置——NeedDecision 既有执法）
                                    ExitPlan(AgentDecisionPhase.StepVerified, "plan:replan");
                                    continue;
                                case PlanControlKind.Stop:
                                    // 安全终止：废弃剩余计划，零新 Effect 进终局评估
                                    AbandonActiveTraversal("plan:stopped");
                                    _planItems = null;
                                    _planItemIndex = 0;
                                    _planStep = null;
                                    _adoptedDecision = null;
                                    _phase = DrivePhase.TerminalEvaluation;
                                    continue;
                                default:
                                    // 枚举闭合之外的 Kind（防御面）→ fail closed
                                    ExitPlan(AgentDecisionPhase.StepRejected, "plan:unknown-control");
                                    continue;
                            }
                        }
                        default:
                            // closed union 之外的派生 PlanItem → fail closed（零 Effect）
                            ExitPlan(AgentDecisionPhase.StepRejected, "plan:unknown-item");
                            continue;
                    }
                }

                case DrivePhase.TerminalEvaluation:
                    if (_inputs.CheckTaskEvidenceBarrier?.Invoke() is { } missingTaskEvidence)
                        return new RunDriveResult(RunDriveStatus.TerminalNotProven,
                            $"task-evidence-unproven:{missingTaskEvidence}", null, _kernel.EffectReceipts.Count);
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

    /// <summary>Drive through typed asynchronous handler completion, timeout,
    /// or cancellation; external input waits remain resumable results.</summary>
    public async Task<RunDriveResult> DriveAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _driveInProgress, 1, 0) != 0)
            return DriveInProgressResult();
        try { return await DriveAsyncCore(cancellationToken).ConfigureAwait(false); }
        finally { Volatile.Write(ref _driveInProgress, 0); }
    }

    private async Task<RunDriveResult> DriveAsyncCore(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested && _kernel.IsActivated
                && _kernel.IsActivationOwner(_driverIdentity) && !_kernel.IsRunTerminal)
            {
                if (_pendingPlanLanguage is { } active && _pendingPlanLanguageCall is { } activeCall)
                {
                    activeCall.Cancel();
                    FinishPlanLanguageCall(active, PlanHandlerCallStatus.Cancelled, null, "handler-cancelled");
                }
                _traversalCancelDisposition = CancelPath(new RunDriverInput.Cancel(
                    "drive-cancelled", DateTimeOffset.UtcNow));
                return _traversalCancelDisposition;
            }

            var result = DriveCore();
            if (result.Status != RunDriveStatus.WaitingForHandler) return result;
            var call = _pendingPlanLanguageCall;
            var pending = _pendingPlanLanguage;
            if (call is null || pending is null) return DriveCore();

            if (cancellationToken.IsCancellationRequested) continue;
            RunDriverInput.Cancel? runCancel = null;
            try { runCancel = _inputs.PollCancellation?.Invoke(); }
            catch (Exception ex)
            {
                call.Cancel();
                FinishPlanLanguageCall(pending, PlanHandlerCallStatus.Cancelled, null,
                    $"cancellation-poll-failed:{ex.GetType().Name}");
                return new RunDriveResult(RunDriveStatus.UnexpectedInput,
                    $"cancellation-poll-failed:{ex.GetType().Name}", null, _kernel.EffectReceipts.Count);
            }
            if (runCancel is not null)
            {
                call.Cancel();
                FinishPlanLanguageCall(pending, PlanHandlerCallStatus.Cancelled, null, $"run-cancelled:{runCancel.Reason}");
                _traversalCancelDisposition = CancelPath(runCancel);
                return _traversalCancelDisposition;
            }

            var remaining = pending.Timeout - pending.Timer.Elapsed;
            if (remaining <= TimeSpan.Zero) continue;
            await Task.WhenAny(call.Completion, Task.Delay(
                remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50),
                cancellationToken)).ConfigureAwait(false);
        }
    }

    private RunDriveResult DriveInProgressResult() => new(
        RunDriveStatus.UnexpectedInput, "drive-in-progress", null, _kernel.EffectReceipts.Count);

    /// <summary>P25 consultation 结果：Rejection 非 null = fail closed 原因。</summary>
    private readonly record struct ConsultOutcome(AgentDecision? Value, string? Rejection);

    /// <summary>一次外部输入拉取：Stop 非 null 表示等待/拒绝；否则携带 P2/P3 处理结果。</summary>
    private readonly record struct ObservationPull(
        RunDriveResult? Stop,
        IReadOnlyList<KernelResult> ProcessedObservations);

    /// <summary>
    /// 当前预算快照（§2.1）。PROTOCOL-FIX-003：契约预算 = 初始额度，已授续授
    /// 追加其上（两层顶——续授不可突破 policy HardCap，由 ValidateRenewal 封顶）。
    /// </summary>
    private ConsultationBudget CurrentBudget(ExecutionContractView view) => new(
        RoundsRemaining: view.MaxConsultations + _grantedRenewalRounds - _consultCounter,
        StepsRemaining: view.MaxTotalSteps + _grantedRenewalSteps - _stepsDispatched);

    /// <summary>续授通道是否配置（任一维度有 HardCap 即开通道；全 null = 关闭，缺省路径）。</summary>
    private bool RenewalConfigured =>
        _plan.GoalDirectedContinuationEnabled
        || _plan.HardCapTotalSteps is not null || _plan.HardCapConsultations is not null;

    /// <summary>
    /// D2 触发（规则侧）：进决策边界时检查 剩余 ≤ 阈值（阈值与硬顶同源于
    /// policy；步数/轮次各自独立、只有配置了硬顶的维度参与）。
    /// </summary>
    private bool IsRenewalWindowOpen(ConsultationBudget budget) =>
        RenewalConfigured
        && ((_plan.GoalDirectedContinuationEnabled
                && ((budget.StepsRemaining <= EffectiveRenewalThresholdSteps())
                    || budget.RoundsRemaining <= EffectiveRenewalThresholdRounds()))
            || (_plan.HardCapTotalSteps is not null
                && budget.StepsRemaining <= _plan.RenewalThresholdSteps)
            || (_plan.HardCapConsultations is not null
                && budget.RoundsRemaining <= _plan.RenewalThresholdRounds));

    /// <summary>续授窗口投影（上下文携带；未配置续授 → null，缺省路径不变）。</summary>
    private RenewalWindow? RenewalWindowViewFor() =>
        _plan.GoalDirectedContinuationEnabled || !RenewalConfigured
            ? null
            : new RenewalWindow(
                Open: _renewalWindowOpen,
                HardCapSteps: _plan.GoalDirectedContinuationEnabled
                    ? _plan.AuthorizedTotalSteps ?? 0 : _plan.HardCapTotalSteps ?? 0,
                HardCapRounds: _plan.GoalDirectedContinuationEnabled
                    ? _plan.AuthorizedTotalConsultRounds ?? 0 : _plan.HardCapConsultations ?? 0,
                GrantedSteps: _grantedRenewalSteps,
                GrantedRounds: _grantedRenewalRounds,
                ThresholdSteps: EffectiveRenewalThresholdSteps(),
                ThresholdRounds: EffectiveRenewalThresholdRounds());

    private int EffectiveRenewalThresholdSteps() => _plan.RenewalThresholdSteps > 0
        ? _plan.RenewalThresholdSteps
        : (_plan.GoalDirectedContinuationEnabled && _kernel.RunView is { } view
            ? Math.Max(1, (int)Math.Ceiling(view.MaxTotalSteps * 0.2)) : 0);

    private int EffectiveRenewalThresholdRounds() => _plan.RenewalThresholdRounds > 0
        ? (_plan.GoalDirectedContinuationEnabled ? Math.Max(2, _plan.RenewalThresholdRounds) : _plan.RenewalThresholdRounds)
        : (_plan.GoalDirectedContinuationEnabled && _kernel.RunView is { } view
            ? Math.Max(2, (int)Math.Ceiling(view.MaxConsultations * 0.2)) + 1 : 0);

    /// <summary>
    /// D3 校验（规则侧）：窗口开启 + 已授 + 提案 ≤ HardCap（各维独立；null
    /// 硬顶维度不可续）。返回 null = 可授；非 null = 拒绝原因（M2 词汇，kebab）。
    /// 拒绝不构成决定失败——留痕后按无续授继续（拒续 → 既有 budget-exhausted
    /// 诚实终局，D4）。
    /// </summary>
    private string? ValidateRenewal(BudgetRenewalRequest renewal)
    {
        if (!RenewalConfigured)
            return "renewal:no-policy-hardcap";
        if (!_renewalWindowOpen)
            return "renewal:window-closed";
        if (renewal.AdditionalSteps < 0 || renewal.AdditionalRounds < 0
            || (renewal.AdditionalSteps == 0 && renewal.AdditionalRounds == 0))
            return "renewal:invalid-request";
        var authorizedSteps = _plan.AuthorizedTotalSteps;
        var authorizedRounds = _plan.AuthorizedTotalConsultRounds;
        if (renewal.AdditionalSteps > 0
            && (_plan.GoalDirectedContinuationEnabled
                ? (authorizedSteps is not null && _kernel.RunView is { } sv
                    && (long)sv.MaxTotalSteps + _grantedRenewalSteps + renewal.AdditionalSteps > authorizedSteps)
                : (_plan.HardCapTotalSteps is null
                    || (long)_grantedRenewalSteps + renewal.AdditionalSteps > _plan.HardCapTotalSteps)))
            return "renewal:hardcap-exceeded:steps";
        if (renewal.AdditionalRounds > 0
            && (_plan.GoalDirectedContinuationEnabled
                ? (authorizedRounds is not null && _kernel.RunView is { } rv
                    && (long)rv.MaxConsultations + _grantedRenewalRounds + renewal.AdditionalRounds > authorizedRounds)
                : (_plan.HardCapConsultations is null
                    || (long)_grantedRenewalRounds + renewal.AdditionalRounds > _plan.HardCapConsultations)))
            return "renewal:hardcap-exceeded:rounds";
        if ((long)_grantedRenewalSteps + renewal.AdditionalSteps > int.MaxValue
            || (long)_grantedRenewalRounds + renewal.AdditionalRounds > int.MaxValue
            || (_kernel.RunView is { } budgetView
                && ((long)budgetView.MaxTotalSteps + _grantedRenewalSteps + renewal.AdditionalSteps > int.MaxValue
                    || (long)budgetView.MaxConsultations + _grantedRenewalRounds + renewal.AdditionalRounds > int.MaxValue)))
            return "renewal:counter-overflow";
        return null;
    }

    /// <summary>
    /// D3/D4：处理决定携带的续授提案——可授则记入轻记账（已授额度生效）并返
    /// 回 true，拒绝则仅留痕、按无续授继续（返回 false）。全程 P25 留痕
    ///（决策 id、量、依据、结果）。
    /// </summary>
    private bool ProcessRenewal(BudgetRenewalRequest? renewal, string? reviewId = null)
    {
        if (renewal is null)
            return false;
        var rejection = _plan.GoalDirectedContinuationEnabled
            && (reviewId is null || !string.Equals(reviewId, _activeBudgetReviewId, StringComparison.Ordinal))
                ? "renewal:explicit-review-required"
                : ValidateRenewal(renewal);
        if (rejection is null)
        {
            _grantedRenewalSteps += renewal.AdditionalSteps;
            _grantedRenewalRounds += renewal.AdditionalRounds;
        }
        _renewalLedger.Add(new BudgetRenewalRecord(
            DecisionIdForRun(_consultCounter),
            renewal.AdditionalSteps, renewal.AdditionalRounds,
            renewal.Justification,
            Granted: rejection is null,
            RejectionReason: rejection,
            ReviewId: reviewId));
        return rejection is null;
    }

    /// <summary>续授审计投影（只读；A4 锚定面）。</summary>
    public IReadOnlyList<BudgetRenewalRecord> RenewalsGranted => _renewalLedger.ToArray();

    private ExecutionOverview BuildExecutionOverview(ExecutionContractView view)
    {
        var budget = CurrentBudget(view);
        var projection = _consultationProjectionSnapshot;
        var currentRefs = new HashSet<string>(projection?.SourceFactRefs ?? Array.Empty<string>(), StringComparer.Ordinal);
        var currentKnown = new HashSet<string>(projection?.KnownItems ?? Array.Empty<string>(), StringComparer.Ordinal);
        var currentUnknown = new HashSet<string>(projection?.Unknowns ?? Array.Empty<string>(), StringComparer.Ordinal);
        var overview = new ExecutionOverview(
            OverviewId: _activeBudgetReviewId ?? $"{_kernel.RunId}:overview-{_consultCounter}",
            RunId: _kernel.RunId,
            ContractVersion: view.Version,
            Objective: view.Objective,
            InitialSteps: view.MaxTotalSteps,
            InitialRounds: view.MaxConsultations,
            GrantedSteps: _grantedRenewalSteps,
            GrantedRounds: _grantedRenewalRounds,
            StepsConsumed: _stepsDispatched,
            RoundsConsumed: _consultCounter,
            StepsRemaining: budget.StepsRemaining,
            RoundsRemaining: budget.RoundsRemaining,
            SegmentStepsConsumed: Math.Max(0, _stepsDispatched - _stepsAtLastGrant),
            SegmentRoundsConsumed: Math.Max(0, _consultCounter - _roundsAtLastGrant),
            Projection: projection,
            PreviousSegmentStepsConsumed: _budgetReviewHistory.LastOrDefault()?.StepsConsumed ?? 0,
            PreviousSegmentRoundsConsumed: _budgetReviewHistory.LastOrDefault()?.RoundsConsumed ?? 0,
            PreviousSegmentExpectation: _budgetReviewHistory.LastOrDefault()?.Expectation,
            PreviousSegmentActual: _budgetReviewHistory.LastOrDefault()?.Actual,
            CurrentSegmentExpectation: _segmentStartExpectation ?? _lastReviewAssessment?.NextSegmentExpectation,
            CurrentSegmentActual: FormatSegmentActual(projection, currentKnown, currentUnknown),
            RelevantHistory: _budgetReviewStrengthened ? _budgetReviewHistory.ToArray() : null,
            PreviousOverviewStepsConsumed: _lastOverviewStepsConsumed,
            PreviousOverviewRoundsConsumed: _lastOverviewRoundsConsumed,
            StepsConsumedSincePreviousOverview: Math.Max(0, _stepsDispatched - _lastOverviewStepsConsumed),
            RoundsConsumedSincePreviousOverview: Math.Max(0, _consultCounter - _lastOverviewRoundsConsumed),
            NewlyVerifiedStepsSincePreviousOverview: Math.Max(0, _completedSteps.Count - _lastOverviewStepsVerified),
            NewSourceFactRefsSincePreviousOverview: currentRefs.Except(_lastOverviewSourceRefs, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            NewlyKnownItemsSincePreviousOverview: currentKnown.Except(_lastOverviewKnownItems, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            NewlyUnknownItemsSincePreviousOverview: currentUnknown.Except(_lastOverviewUnknowns, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            SegmentStepsVerified: Math.Max(0, _completedSteps.Count - _stepsVerifiedAtLastGrant),
            PlanExecution: _activePlanProposal is null ? null : new PlanExecutionOverview(
                CurrentPlanPosition(), _planStatus, _planInterruptionReason,
                _planAttempts.Count, _blockedCandidates.Count, _activePlanProposal.StageExpectation));
        _lastOverviewStepsConsumed = _stepsDispatched;
        _lastOverviewRoundsConsumed = _consultCounter;
        _lastOverviewStepsVerified = _completedSteps.Count;
        _lastOverviewSourceRefs = currentRefs;
        _lastOverviewKnownItems = currentKnown;
        _lastOverviewUnknowns = currentUnknown;
        return overview;
    }

    private string? FormatSegmentActual(
        ExecutionOverviewProjection? projection,
        HashSet<string> currentKnown,
        HashSet<string> currentUnknown)
    {
        if (_segmentStartProjection is null)
            return $"verified={Math.Max(0, _completedSteps.Count - _stepsVerifiedAtLastGrant)};new-progress-facts=unknown;owner-actual={projection?.PreviousSegmentActual ?? "unknown"}";
        var start = _segmentStartProjection;
        var newKnown = currentKnown.Except(start.KnownItems, StringComparer.Ordinal).Count();
        var newUnknown = currentUnknown.Except(start.Unknowns, StringComparer.Ordinal).Count();
        var currentProgressKeys = new HashSet<string>(projection?.ProgressFactKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        var baselineProgressKeys = _renewalLedger.Any(record => record.Granted)
            ? _lastGrantedProgressFactKeys
            : new HashSet<string>(start.ProgressFactKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        var newProgressFacts = currentProgressKeys.Except(baselineProgressKeys, StringComparer.Ordinal).Count();
        return $"verified={Math.Max(0, _completedSteps.Count - _stepsVerifiedAtLastGrant)};new-known={newKnown};new-unknown={newUnknown};new-progress-facts={newProgressFacts};owner-actual={projection?.PreviousSegmentActual ?? "unknown"}";
    }

    private BudgetReview? BuildBudgetReview(ExecutionContractView view)
    {
        if (!_plan.GoalDirectedContinuationEnabled || _activeBudgetReviewId is null)
            return null;
        var suggestedSteps = Math.Max(1, (int)Math.Ceiling(view.MaxTotalSteps * 0.2));
        var suggestedRounds = Math.Max(1, (int)Math.Ceiling(view.MaxConsultations * 0.2));
        return new BudgetReview(
            _activeBudgetReviewId,
            Required: true,
            Attempt: _budgetReviewAttempt,
            Strengthened: _budgetReviewStrengthened,
            SuggestedAdditionalSteps: suggestedSteps,
            SuggestedAdditionalRounds: suggestedRounds,
            InitialSteps: view.MaxTotalSteps,
            InitialRounds: view.MaxConsultations,
            GrantedSteps: _grantedRenewalSteps,
            GrantedRounds: _grantedRenewalRounds,
            StepsConsumed: _stepsDispatched,
            RoundsConsumed: _consultCounter,
            StepsRemaining: CurrentBudget(view).StepsRemaining,
            RoundsRemaining: CurrentBudget(view).RoundsRemaining);
    }

    private PlanExecutionView? CurrentPlanExecutionView() => _activePlanProposal is null
        ? null
        : new PlanExecutionView(CurrentPlanPosition(), _planStatus,
            _activePlanProposal.StageExpectation, _planInterruptionReason,
            _planAttempts.ToArray(), _blockedCandidates.ToArray(), _planConditionEvaluations.ToArray(),
            _kernel.TraversalExecution is { } traversal
                && traversal.PlanId == _activePlanProposal.PlanId
                && traversal.Version == _activePlanProposal.Version ? traversal : null);

    private string? ValidateTraversalBindings(PlanItem.TraverseItem item)
    {
        if (item.Rules is null || item.Rules.Count == 0 || item.Rules.Count > 16
            || item.MaxDepth < 0 || item.MaxDepth > 16 || item.ReturnStep is null)
            return "traversal:invalid-plan";
        if (_inputs.PlanHandlerTimeout <= TimeSpan.Zero || _inputs.PlanHandlerTimeout > TimeSpan.FromSeconds(30))
            return "traversal:invalid-handler-timeout";
        foreach (var operation in item.Rules.Select(rule => rule.Operation).Append(
                     new TraversalOperation("$return", TraversalOperationKind.Navigate, item.ReturnStep,
                         Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>())))
        {
            if (operation is null || string.IsNullOrWhiteSpace(operation.OperationId)
                || !Enum.IsDefined(operation.Kind)
                || operation.Before is null || operation.AfterVerified is null
                || (operation.Kind == TraversalOperationKind.ReadOnly && operation.Step is not null)
                || (operation.Kind == TraversalOperationKind.Navigate && operation.Step is null))
                return "traversal:invalid-operation";
            if (operation.Before.Count > 8 || operation.AfterVerified.Count > 8)
                return "traversal:too-many-handlers";
            foreach (var handler in operation.Before.Concat(operation.AfterVerified))
                if (handler is null || string.IsNullOrWhiteSpace(handler.HandlerId)
                    || string.IsNullOrWhiteSpace(handler.CapabilityId)
                    || string.IsNullOrWhiteSpace(handler.ProtocolVersion)
                    || string.IsNullOrWhiteSpace(handler.ExpectedLanguage)
                    || !string.Equals(handler.CapabilityId, "runtime.language-inspector", StringComparison.Ordinal)
                    || !string.Equals(handler.ProtocolVersion, "1.0", StringComparison.Ordinal))
                    return "traversal:required-handler-unavailable";
        }
        var operations = item.Rules.Select(rule => rule.Operation).Append(new TraversalOperation(
            "$return", TraversalOperationKind.Navigate, item.ReturnStep,
            Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>())).ToArray();
        if (item.Rules.Any(rule => rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
            || item.Rules.Select(rule => rule.RuleId).Distinct(StringComparer.Ordinal).Count() != item.Rules.Count
            || operations.Select(operation => operation.OperationId).Distinct(StringComparer.Ordinal).Count() != operations.Length
            || operations.SelectMany(operation => operation.Before.Concat(operation.AfterVerified))
                .Select(handler => handler.HandlerId).Distinct(StringComparer.Ordinal).Count()
                != operations.SelectMany(operation => operation.Before.Concat(operation.AfterVerified)).Count())
            return "traversal:duplicate-identity";
        try
        {
            foreach (var handler in operations.SelectMany(operation => operation.Before.Concat(operation.AfterVerified)))
                if (handler.Required && (_inputs.CanBindPlanLanguage?.Invoke(handler) != true
                    || (_inputs.InspectPlanLanguage is null && _inputs.InspectPlanLanguageAsync is null)))
                    return "traversal:required-handler-unavailable";
        }
        catch (Exception) { return "traversal:handler-binding-validation-failed"; }
        return null;
    }

    private bool AdvanceTraversal(ExecutionContractView view)
    {
        var item = _activeTraversal!;
        if (_traversal.Paused)
        {
            PausePlan(_traversal.View().PauseReason ?? "traversal:paused");
            return true;
        }
        if (_traversalWork is { } expectedWork)
        {
            var actualContainer = CurrentInteractionContainerId(_kernel.CurrentBelief);
            var expectedContainer = _traversalHandlerPhase == "after"
                ? _traversalVerifiedContainerId
                    ?? (expectedWork.Return ? expectedWork.ExpectedReturnContainerId : expectedWork.ContainerId)
                : expectedWork.ContainerId;
            if (expectedContainer is null || actualContainer != expectedContainer)
            {
                var reason = _traversalHandlerPhase == "after"
                    ? "traversal:after-container-changed"
                    : "traversal:work-container-not-current";
                _traversal.Gap(expectedWork.ContainerId, expectedWork.Candidate?.Target.Key,
                    reason, expectedWork.Candidate?.Target.EvidenceRefs ?? Array.Empty<string>());
                PausePlan(reason);
                return true;
            }
        }
        if (_traversalWork is not null && _traversalHandlerPhase == "after")
        {
            if (!RunTraversalHandlers(_traversalWork.Operation.AfterVerified, "AfterVerified", _traversalWork))
            {
                if (_pendingPlanLanguageCall is not null) return true;
                PausePlan(_traversal.View().PauseReason ?? "traversal:handler-paused");
                return true;
            }
            if (_traversalWork.Return)
            {
                var current = _kernel.CurrentBelief;
                var currentId = current?.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var claim) == true
                    ? claim.Value : null;
                if (!_traversal.Returned(_traversalWork, currentId))
                {
                    PausePlan(_traversal.View().PauseReason ?? "traversal:return-failed");
                    return true;
                }
                _traversalWork = null;
                _traversalHandlerPhase = null;
                _traversalVerifiedContainerId = null;
            }
            else if (_traversalWork.Operation.Kind == TraversalOperationKind.ReadOnly)
            {
                _traversal.ReadOnlyCompleted(_traversalWork);
                _traversalWork = null;
                _traversalHandlerPhase = null;
                _traversalVerifiedContainerId = null;
            }
            else
            {
                var current = _kernel.CurrentBelief;
                if (current?.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var claim) != true
                    || CurrentInteractionContainerId(current) != claim.Value)
                {
                    _traversal.Gap(_traversalWork.ContainerId, _traversalWork.Candidate?.Target.Key,
                        "navigation-target-page-unknown", Array.Empty<string>());
                    _traversal.Pause("traversal:navigation-target-page-unknown");
                    PausePlan("traversal:navigation-target-page-unknown");
                    return true;
                }
                _traversal.Navigated(_traversalWork, claim.Value,
                    new TraversalOperation("$return", TraversalOperationKind.Navigate, item.ReturnStep,
                        Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>()));
                _traversalWork = null;
                _traversalHandlerPhase = null;
                _traversalVerifiedContainerId = null;
            }
        }
        if (_traversalWork is null)
        {
            if (_activePlanProposal is not null && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
            {
                _traversal.Pause("traversal:local-work-exhausted");
                PausePlan("traversal:local-work-exhausted");
                return true;
            }
            var current = _kernel.CurrentBelief;
            var currentId = CurrentInteractionContainerId(current);
            if (current is null || currentId is null)
            {
                _traversal.Fail("traversal:current-container-unknown");
                PausePlan("traversal:current-container-unknown");
                return true;
            }
            TraversalObservation? observation;
            try { observation = _inputs.ProjectTraversal?.Invoke(item.Source); }
            catch (Exception ex)
            {
                _traversal.Fail($"traversal:projection-failed:{ex.GetType().Name}");
                PausePlan(_traversal.View().PauseReason!);
                return true;
            }
            if (observation is null || !TraversalObservationIsCurrent(
                    observation, current, currentId, _kernel.NavigationScope))
            {
                _traversal.Fail("traversal:stale-or-unassociated-projection");
                PausePlan("traversal:stale-or-unassociated-projection");
                return true;
            }
            _planWorkUsed++;
            var work = _traversal.Select(observation, item.MaxDepth);
            if (work is null)
            {
                if (_traversal.Finished)
                {
                    RecordTraversalState();
                    _planItemIndex++;
                    _activeTraversal = null;
                    _traversalWork = null;
                }
                else if (_traversal.Paused) PausePlan(_traversal.View().PauseReason ?? "traversal:paused");
                RecordTraversalState();
                return true;
            }
            _traversalWork = work;
            RecordTraversalOperation(work, current);
            _traversalHandlerPhase = "before";
            _traversalHandlerIndex = 0;
        }
        var selected = _traversalWork!;
        var before = selected.Operation.Before;
        if (!RunTraversalHandlers(before, "Before", selected))
        {
            if (_pendingPlanLanguageCall is not null) return true;
            PausePlan(_traversal.View().PauseReason ?? "traversal:handler-paused");
            return true;
        }
        if (selected.Operation.Kind == TraversalOperationKind.ReadOnly)
        {
            _traversalVerifiedContainerId = selected.ContainerId;
            _traversalHandlerPhase = "after";
            _traversalHandlerIndex = 0;
            return true;
        }
        var step = selected.Return ? item.ReturnStep : selected.Operation.Step!;
        if (!selected.Return && selected.Candidate is { } candidate)
            step = step with
            {
                TargetRole = step.TargetRole == "$candidate" ? candidate.Target.TargetRole : step.TargetRole,
                TargetDescriptor = step.TargetDescriptor == "$candidate" ? candidate.Target.TargetDescriptor : step.TargetDescriptor,
            };
        _planWorkUsed++;
        _planStep = step;
        _stepIndex = 0;
        _phase = DrivePhase.StepAct;
        RecordTraversalState();
        return true;
    }

    private bool RunTraversalHandlers(IReadOnlyList<PlanLanguageHandler> handlers, string phase, TraversalControl.Work work)
    {
        while (_traversalHandlerIndex < handlers.Count)
        {
            var binding = handlers[_traversalHandlerIndex];
            if (binding.Required && (_kernel.TraversalExecution?.Handlers ?? Array.Empty<PlanHandlerRecord>()).Any(record =>
                    record.OperationId == work.InvocationId && record.HandlerId == binding.HandlerId
                    && record.Phase == phase && record.Status is PlanHandlerCallStatus.TimedOut or PlanHandlerCallStatus.Cancelled))
            {
                _traversal.Pause("traversal:handler-timedout-requires-revision");
                return false;
            }
            if (_activePlanProposal is not null && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
            {
                _traversal.Pause("traversal:local-work-exhausted");
                return false;
            }
            _planWorkUsed++;
            var revision = _kernel.CurrentBelief?.RevisionId ?? string.Empty;
            var containerId = phase == "AfterVerified"
                ? _traversalVerifiedContainerId ?? work.ContainerId
                : work.ContainerId;
            var handlerTarget = work.Candidate?.Target ?? new PlanCandidate(
                work.ContainerId, "container", work.ContainerId, PlanCandidateDisposition.Ready,
                _kernel.CurrentBelief?.EvidenceBasis.ToArray() ?? Array.Empty<string>());
            var input = new PlanHandlerInput(_kernel.RunId!, work.InvocationId, phase,
                revision, containerId, handlerTarget, TargetContainerId: work.ContainerId);
            bool canBind;
            try { canBind = _inputs.CanBindPlanLanguage?.Invoke(binding) == true; }
            catch (Exception ex)
            {
                RecordHandlerFailure(work, binding, phase, revision, PlanHandlerCallStatus.Failed,
                    $"binding-check-failed:{ex.GetType().Name}");
                if (binding.Required || binding.RequirePass) return false;
                _traversalHandlerIndex++;
                continue;
            }
            if (!canBind)
            {
                RecordHandlerFailure(work, binding, phase, revision, PlanHandlerCallStatus.Unavailable, "binding-unavailable");
                if (binding.Required || binding.RequirePass) return false;
                _traversalHandlerIndex++;
                continue;
            }
            if (_inputs.InspectPlanLanguageAsync is null && _inputs.InspectPlanLanguage is null)
            {
                RecordHandlerFailure(work, binding, phase, revision, PlanHandlerCallStatus.Unavailable, "handler-adapter-unavailable");
                if (binding.Required || binding.RequirePass) return false;
                _traversalHandlerIndex++;
                continue;
            }

            var callId = Guid.NewGuid().ToString("N");
            var timeout = _inputs.PlanHandlerTimeout > TimeSpan.Zero ? _inputs.PlanHandlerTimeout : TimeSpan.Zero;
            var callCancellation = new CancellationTokenSource();
            var call = new PlanLanguageHandlerCall(callId, callCancellation);
            var pending = new PendingPlanLanguageCall(binding, input, work, phase, revision,
                callId, timeout, Stopwatch.StartNew(), Stopwatch.GetTimestamp());
            _pendingPlanLanguageCall = call;
            _pendingPlanLanguage = pending;
            _kernel.RecordTraversalHandler(new PlanHandlerRecord(work.InvocationId, binding.HandlerId,
                phase, revision, PlanHandlerCallStatus.Running, null, null, callId));
            RecordTraversalState();
            try
            {
                Task<Finding?> completion;
                if (_inputs.InspectPlanLanguageAsync is { } inspectAsync)
                    completion = inspectAsync(binding, input, call.Token).AsTask();
                else
                {
                    var inspect = _inputs.InspectPlanLanguage!;
                    completion = Task.Run(() => inspect(binding, input));
                }
                call.Observe(completion);
            }
            catch (Exception ex)
            {
                call.Observe(Task.FromException<Finding?>(ex));
            }
            return false;
        }
        _traversalHandlerIndex = 0;
        if (phase == "Before") _traversalHandlerPhase = "operation";
        return true;
    }

    private (PlanHandlerCallStatus Status, string? Reason) ClassifyPlanLanguageFinding(
        PendingPlanLanguageCall pending, Finding? finding)
    {
        if (finding is null) return (PlanHandlerCallStatus.Failed, "handler-returned-no-finding");
        if (finding.Correlation.RunId != _kernel.RunId
            || finding.Correlation.OperationId != pending.Work.InvocationId
            || finding.Correlation.DomainCorrelation != pending.Revision
            || finding.SourceOwner != pending.Binding.CapabilityId
            || finding.RuleVersion != pending.Binding.ProtocolVersion)
            return (PlanHandlerCallStatus.Failed, "finding-association-mismatch");
        if (finding.Status is CapabilityStatus.Completed or CapabilityStatus.Unknown)
            return (PlanHandlerCallStatus.Completed, null);
        return (finding.Status == CapabilityStatus.TimedOut
            ? PlanHandlerCallStatus.TimedOut
            : finding.Status == CapabilityStatus.Unavailable
                ? PlanHandlerCallStatus.Unavailable
                : PlanHandlerCallStatus.Failed, $"finding-status:{finding.Status}");
    }

    private bool TryFinishPendingPlanLanguageCall()
    {
        var call = _pendingPlanLanguageCall;
        var pending = _pendingPlanLanguage;
        if (call is null || pending is null) return true;
        var completionAt = call.CompletedTimestamp;
        var completedBeforeDeadline = completionAt is { } timestamp
            && Stopwatch.GetElapsedTime(pending.StartedTimestamp, timestamp) <= pending.Timeout;
        if (!completedBeforeDeadline && pending.Timer.Elapsed >= pending.Timeout)
        {
            call.Cancel();
            FinishPlanLanguageCall(pending, PlanHandlerCallStatus.TimedOut, null, "handler-timeout");
            return true;
        }
        if (!call.Completion.IsCompleted) return false;
        if (call.Failure is { } failure)
        {
            FinishPlanLanguageCall(pending, PlanHandlerCallStatus.Failed, null, failure.GetType().Name);
        }
        else
        {
            try
            {
                var finding = call.Completion.GetAwaiter().GetResult();
                var (status, reason) = ClassifyPlanLanguageFinding(pending, finding);
                FinishPlanLanguageCall(pending, status, finding, reason);
            }
            catch (Exception ex)
            {
                FinishPlanLanguageCall(pending, PlanHandlerCallStatus.Failed, null, ex.GetType().Name);
            }
        }
        return true;
    }

    private void FinishPlanLanguageCall(PendingPlanLanguageCall pending, PlanHandlerCallStatus status,
        Finding? finding, string? reason)
    {
        var call = _pendingPlanLanguageCall;
        if (call is null || call.CallId != pending.CallId || call.IsClosed) return;
        if (call.CancellationFailure is { } cancelFailure)
            reason = string.IsNullOrEmpty(reason) ? $"handler-cancel-request-failed:{cancelFailure}"
                : $"{reason};handler-cancel-request-failed:{cancelFailure}";
        call.Close();
        _kernel.RecordTraversalHandler(new PlanHandlerRecord(pending.Work.InvocationId, pending.Binding.HandlerId,
            pending.Phase, pending.Revision, status, finding, reason, pending.CallId));
        _pendingPlanLanguageCall = null;
        _pendingPlanLanguage = null;
        call.Dispose();
        if (status != PlanHandlerCallStatus.Completed && pending.Binding.Required)
            _traversal.Pause($"traversal:handler-{status.ToString().ToLowerInvariant()}");
        else if (pending.Binding.RequirePass && (finding is null || finding.Disposition != FindingDisposition.Pass
            || finding.Status != CapabilityStatus.Completed))
            _traversal.Pause("traversal:handler-require-pass");
        else
            _traversalHandlerIndex++;
        RecordTraversalState();
    }

    private void RecordHandlerFailure(TraversalControl.Work work, PlanLanguageHandler binding, string phase,
        string revision, PlanHandlerCallStatus status, string reason)
    {
        _kernel.RecordTraversalHandler(new PlanHandlerRecord(work.InvocationId, binding.HandlerId,
            phase, revision, status, null, reason));
        RecordTraversalState();
        if (binding.Required) _traversal.Pause($"traversal:handler-{status.ToString().ToLowerInvariant()}");
        else if (binding.RequirePass) _traversal.Pause("traversal:handler-require-pass");
    }

    private void RecordTraversalState()
    {
        foreach (var progress in _traversal.DrainProgressEvents())
            _kernel.RecordTraversalProgress(progress);
        var view = _traversal.View();
        _kernel.RecordTraversal(view with
        {
            WorkUsed = _planWorkUsed,
            KnownRemainingWork = view.Status == PlanExecutionStatus.Completed
                ? 0
                : !_traversal.HasObservedCurrentFrontier
                    ? null
                    : _traversal.KnownPendingWork + (_traversalWork is null ? 0 : 1),
        });
    }

    private void RecordTraversalOperation(TraversalControl.Work work, WorldBeliefRevision current)
    {
        var target = work.Candidate?.Target;
        var occurrence = target is null ? null : current.Occurrences?.SingleOrDefault(item =>
            item.OwningContainerId == work.ContainerId && item.Role == target.TargetRole
            && item.SemanticDescriptor == target.TargetDescriptor
            && target.EvidenceRefs.Any(reference => item.EvidenceBasis.Contains(reference)));
        var evidence = target?.EvidenceRefs ?? current.Containers?
            .SingleOrDefault(container => container.Identity.ContainerId == work.ContainerId)?.EvidenceBasis
            ?? Array.Empty<string>();
        _kernel.RecordTraversalOperation(new TraversalOperationRecord(
            work.InvocationId, work.Operation.OperationId, work.ContainerId, target?.Key,
            occurrence?.State, current.RevisionId, evidence.ToArray(), Completed: false));
        RecordTraversalState();
    }

    private static bool TraversalObservationIsCurrent(TraversalObservation observation, WorldBeliefRevision current,
        string currentId, string expectedScope)
    {
        var graph = observation.Graph;
        if (graph is null || graph.SourceRevisionId != current.RevisionId || graph.Scope != expectedScope
            || graph.CurrentContainerId != currentId
            || !current.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var claim)
            || claim.Value != currentId || graph.Nodes.Count(node => node.Identity.ContainerId == currentId) != 1
            || observation.Candidates is null || observation.DiscoveryEvidenceRefs is null
            || observation.DiscoveryComplete && observation.DiscoveryEvidenceRefs.Count == 0
            || observation.DiscoveryEvidenceRefs.Any(reference => !current.EvidenceBasis.Contains(reference))
            || graph.Nodes.Any(node => current.Containers?.Contains(node) != true)
            || graph.Edges.Any(edge => current.Relations?.Contains(edge) != true)
            || observation.Candidates.Select(candidate => candidate?.Target?.Key)
                .Distinct(StringComparer.Ordinal).Count() != observation.Candidates.Count)
            return false;
        foreach (var candidate in observation.Candidates)
        {
            if (candidate?.Target is not { } target || target.EvidenceRefs is null || target.EvidenceRefs.Count == 0
                || target.EvidenceRefs.Any(reference => !current.EvidenceBasis.Contains(reference))
                || candidate.Conditions is null
                || !Enum.IsDefined(target.Disposition)
                || candidate.Conditions.Values.Any(value => !Enum.IsDefined(value))) return false;
            var matches = current.Occurrences?.Where(o => o.OwningContainerId == currentId
                && o.Role == target.TargetRole && o.SemanticDescriptor == target.TargetDescriptor
                && target.EvidenceRefs.Any(reference => o.EvidenceBasis.Contains(reference))).ToArray();
            if (matches is not { Length: 1 }) return false;
        }
        return true;
    }

    private PlanExecutionView? AgentPlanExecutionView()
    {
        var view = CurrentPlanExecutionView();
        return view is null
            ? null
            : view with
            {
                ConditionEvaluations = view.ConditionEvaluations?.Select(evaluation => evaluation with
                {
                    Result = evaluation.Result with { EvidenceRefs = new[] { evaluation.EvaluationId } },
                }).ToArray(),
                Attempts = view.Attempts.Select(attempt => attempt with
                {
                    EvidenceRefs = new[] { $"plan-attempt:{attempt.AttemptId}" },
                }).ToArray(),
            };
    }

    private PlanExecutionPosition CurrentPlanPosition() => new(
        _activePlanProposal?.PlanId ?? string.Empty,
        _activePlanProposal?.Version ?? 0,
        _planItemIndex,
        _forEachIteration,
        _forEachBodyIndex,
        CurrentPlanCandidate?.Key,
        _planWorkUsed,
        _planBlocks.Count == 0 ? null : string.Join("/", _planBlocks.Reverse().Select(frame =>
            $"{frame.ItemIndex}.{frame.Iteration}.{frame.BodyIndex}:{frame.Kind}:{frame.BlockIteration}")));

    private void BeginPlanAttempt(AgentActionStep step)
    {
        if (_activeAttemptIndex is { } activeIndex
            && activeIndex >= 0 && activeIndex < _planAttempts.Count
            && _planAttempts[activeIndex] is { Status: PlanAttemptStatus.Started } active
            && active.Position == CurrentPlanPosition()
            && active.Step == step)
            return;

        _planAttemptOrdinal++;
        _planAttempts.Add(new PlanAttempt(
            $"{_kernel.RunId}:plan-attempt-{_planAttemptOrdinal}",
            CurrentPlanPosition(), DecisionIdForRun(_consultCounter), step,
            PlanAttemptStatus.Started, null,
            MergeEvidenceRefs(CurrentCandidateEvidenceRefs(), _activeGuardEvidenceRefs), null));
        _activeAttemptIndex = _planAttempts.Count - 1;
    }

    private void RecordPlanAttempt(
        AgentActionStep step,
        PlanAttemptStatus status,
        string? receiptId,
        IReadOnlyList<string> evidenceRefs,
        string? reason)
    {
        if (_activeAttemptIndex is { } activeIndex
            && activeIndex >= 0 && activeIndex < _planAttempts.Count
            && _planAttempts[activeIndex] is { Status: PlanAttemptStatus.Started or PlanAttemptStatus.Unconfirmed } active
            && active.Position == CurrentPlanPosition()
            && active.Step == step)
        {
            UpdateActivePlanAttempt(status, receiptId, evidenceRefs, reason);
            return;
        }

        _planAttemptOrdinal++;
        _planAttempts.Add(new PlanAttempt(
            $"{_kernel.RunId}:plan-attempt-{_planAttemptOrdinal}",
            CurrentPlanPosition(), DecisionIdForRun(_consultCounter), step,
            status, receiptId, MergeEvidenceRefs(CurrentCandidateEvidenceRefs(), evidenceRefs), reason));
    }

    private void UpdateActivePlanAttempt(
        PlanAttemptStatus status,
        string? receiptId,
        IReadOnlyList<string> evidenceRefs,
        string? reason)
    {
        if (_activeAttemptIndex is not { } index || index < 0 || index >= _planAttempts.Count)
            return;
        var attempt = _planAttempts[index];
        _planAttempts[index] = attempt with
        {
            Status = status,
            ReceiptId = receiptId ?? attempt.ReceiptId,
            EvidenceRefs = MergeEvidenceRefs(attempt.EvidenceRefs, evidenceRefs),
            Reason = reason,
        };
        if (status is not (PlanAttemptStatus.Started or PlanAttemptStatus.Unconfirmed))
            _activeAttemptIndex = null;
    }

    private IReadOnlyList<string> CurrentCandidateEvidenceRefs()
    {
        var refs = new List<string>();
        if (CurrentPlanCandidate is { } current) refs.AddRange(current.EvidenceRefs);
        foreach (var frame in _planBlocks)
            if (frame.Candidate is { } parent) refs.AddRange(parent.EvidenceRefs);
        if (_activeForEach is not null && _activeCandidateRevision is { } revision)
            refs.Add($"plan-source:{_activeForEach.Source}:revision:{revision}");
        return refs.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> MergeEvidenceRefs(
        IReadOnlyList<string> existing,
        IReadOnlyList<string> added) => existing.Concat(added).Distinct(StringComparer.Ordinal).ToArray();

    private bool SpendPlanControlWork()
    {
        if (_activePlanProposal is not null && _planWorkUsed >= _activePlanProposal.MaxLocalOperations)
        {
            PausePlan("plan:local-work-exhausted");
            return false;
        }
        _planWorkUsed++;
        return true;
    }

    private void EnterPlanBlock(IReadOnlyList<PlanItem> items, string kind, int repetitions = 1)
    {
        var binding = CurrentPlanCandidate;
        _planBlocks.Push(new PlanBlockFrame(_planItems!, _planItemIndex,
            _activeForEach, _forEachIteration, _forEachBodyIndex,
            _activeCandidate, _activeCandidateRevision, _forEachScopeKey,
            _seenCandidateKeys.ToArray(), _inheritedPlanCandidate, kind, repetitions));
        _planItems = items;
        _planItemIndex = 0;
        ResetForEach();
        _inheritedPlanCandidate = binding;
    }

    private bool CompletePlanBlock()
    {
        if (_planBlocks.Count == 0) return false;
        var frame = _planBlocks.Peek();
        if (frame.BlockIteration + 1 < frame.Repetitions)
        {
            if (!SpendPlanControlWork()) return true;
            frame.BlockIteration++;
            _planItemIndex = 0;
            ResetForEach();
            _inheritedPlanCandidate = frame.Candidate ?? frame.InheritedCandidate;
            return true;
        }
        _planBlocks.Pop();
        _planItems = frame.Items;
        _planItemIndex = frame.ItemIndex;
        _activeForEach = frame.Loop;
        _forEachIteration = frame.Iteration;
        _forEachBodyIndex = frame.BodyIndex;
        _activeCandidate = frame.Candidate;
        _activeCandidateKey = frame.Candidate?.Key;
        _activeCandidateRevision = frame.CandidateRevision;
        _forEachScopeKey = frame.ScopeKey;
        _seenCandidateKeys.Clear();
        foreach (var key in frame.SeenKeys) _seenCandidateKeys.Add(key);
        _inheritedPlanCandidate = frame.InheritedCandidate;
        AdvancePlanItem();
        return true;
    }

    private void AdvancePlanItem()
    {
        if (_activeForEach is not null)
        {
            _forEachBodyIndex++;
            if (_forEachBodyIndex >= _activeForEach.Body.Count)
            {
                if (_activeCandidateKey is { } key) _seenCandidateKeys.Add(key);
                _activeCandidateKey = null;
                _activeCandidate = null;
                _activeCandidateRevision = null;
                _forEachBodyIndex = 0;
                _forEachIteration++;
                if (_forEachIteration >= _activeForEach.MaxIterations)
                {
                    _planItemIndex++;
                    ResetForEach();
                }
            }
        }
        else
        {
            _planItemIndex++;
        }
    }

    private void ResetForEach()
    {
        _activeForEach = null;
        _forEachIteration = 0;
        _forEachBodyIndex = 0;
        _activeCandidateKey = null;
        _activeCandidate = null;
        _activeCandidateRevision = null;
        _forEachScopeKey = null;
        _seenCandidateKeys.Clear();
    }

    private void PausePlan(string reason)
    {
        _planPaused = true;
        _planStatus = PlanExecutionStatus.Paused;
        _planInterruptionReason = reason;
        if (_activeTraversal is not null)
        {
            _traversal.Pause(reason);
            RecordTraversalState();
            var facts = new TraversalRecoveryFacts(CurrentPlanPosition(),
                _kernel.TraversalExecution ?? _traversal.View() with { WorkUsed = _planWorkUsed },
                _planAttempts.ToArray(), _stepsDispatched, _consultCounter);
            _kernel.RecordTraversalRecovery(facts);
            _inputs.ObserveTraversalRecovery?.Invoke(facts);
        }
        _adoptedDecision = null;
        _phase = DrivePhase.NeedDecision;
    }

    private void AbandonPausedPlan(string reason)
    {
        if (!_planPaused && _activeTraversal is null) return;
        AbandonActiveTraversal(reason);
        _planPaused = false;
        _planStatus = PlanExecutionStatus.Abandoned;
        _planInterruptionReason = reason;
        _planItems = null;
        _planStep = null;
        _activeTraversal = null;
        _traversalWork = null;
        ResetForEach();
    }

    private void AbandonActiveTraversal(string reason)
    {
        if (_activeTraversal is null) return;
        if (!_traversal.Finished)
        {
            _traversal.Abandon(reason);
            RecordTraversalState();
        }
        _activeTraversal = null;
        _traversalWork = null;
    }

    private AgentDecisionContext BuildGuardContext(ExecutionContractView view)
    {
        var currentContainer = CurrentInteractionContainerId(_kernel.CurrentBelief);
        var elements = DeriveElementSummaries(currentContainer);
        var state = _kernel.RunState;
        var claims = _kernel.CurrentBelief?.WorldState;
        var summaries = claims is null
            ? new Dictionary<string, ClaimSummary>()
            : claims.Where(kv => !kv.Key.StartsWith("ui.node.", StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key,
                    kv => new ClaimSummary(kv.Value.Value, DispositionOf(kv.Key, kv.Value),
                        _kernel.CurrentConflictedSubjects.Contains(kv.Key)), StringComparer.Ordinal);
        return new AgentDecisionContext(
            DecisionId: DecisionIdForRun(_consultCounter), RunId: _kernel.RunId,
            ContractVersion: view.Version, Objective: view.Objective,
            AllowedEffects: view.AllowedEffects,
            CurrentWorldClaims: summaries,
            PendingObligations: state?.ProofObligations.Obligations.Select(o =>
                new AgentObligationView(o.ObligationId, o.Kind.ToString(), o.Subject, o.RequiredValue, o.Mandatory)).ToArray()
                ?? Array.Empty<AgentObligationView>(),
            Phase: PhaseForCurrent(), Screen: DeriveScreenSummary(), Elements: elements,
            Progress: new ConsultationProgress(_consultCounter, _stepsDispatched, _completedSteps.Count),
            BudgetRemaining: CurrentBudget(view), PlanExecution: AgentPlanExecutionView(),
            AvailablePlanSources: _inputs.AvailablePlanSources,
            AvailablePlanConditions: _inputs.AvailablePlanConditions);
    }

    private static BudgetReviewResponse? ReviewResponseFor(AgentDecision? decision) => decision switch
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

    private static BudgetRenewalRequest? LegacyRenewalFor(AgentDecision? decision) => decision switch
    {
        AgentDecision.Act x => x.Proposal.Renewal,
        AgentDecision.NoAction x => x.Proposal.Renewal,
        AgentDecision.Defer x => x.Renewal,
        _ => null,
    };

    private string BudgetReviewTriggerSignature(ExecutionOverviewProjection? projection)
    {
        // Consultation logging and repeated observations move source watermarks
        // without creating a new reason to ask an already-answered review again.
        var keys = string.Join(",", (projection?.ProgressFactKeys ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal));
        var budget = CurrentBudget(_kernel.RunView!);
        return $"grant={_grantedRenewalSteps}/{_grantedRenewalRounds}|steps-low={budget.StepsRemaining <= EffectiveRenewalThresholdSteps()}|rounds-low={budget.RoundsRemaining <= EffectiveRenewalThresholdRounds()}|{keys}";
    }

    private static bool IsCurrentActionableItem(string? key, ExecutionOverviewProjection? projection) =>
        key is not null && projection?.ActionableItems is { } items
        && items.TryGetValue(key, out var item) && item.SourceFactRefs.Count > 0
        && item.SourceFactRefs.All(reference => projection.SourceFactRefs.Contains(reference, StringComparer.Ordinal));

    private bool HasUnfundedItemBasis(string key, ExecutionOverviewProjection projection) =>
        !_fundedItemProgress.TryGetValue(key, out var fundedFacts)
        || HasNewProgressFacts(fundedFacts, projection.ActionableItems![key].ProgressFactKeys);

    private static bool HasNewProgressFacts(
        IEnumerable<string>? baselineKeys,
        IEnumerable<string>? currentKeys)
    {
        var baseline = new HashSet<string>(baselineKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        return (currentKeys ?? Array.Empty<string>()).Any(key => !baseline.Contains(key));
    }

    /// <summary>
    /// PER-014 R3：任一 mandatory obligation 已满足——走 typed 权威判定路径
    ///（UniKernel.UnsatisfiedMandatoryObligations → RuntimeAssurance.
    /// EvaluateObligations → R1 SemanticCheckedResolver 缝），与终局折抵
    /// 同源同判，不再直读 legacy state subjects。Run/belief 未建立 → 如实
    /// false（fail-closed，不猜）。
    /// </summary>
    private bool AnyMandatoryObligationSatisfied()
    {
        var state = _kernel.RunState;
        if (state is null || _kernel.RunView is null || _kernel.CurrentBelief is null)
            return false;
        var mandatory = state.ProofObligations.Obligations
            .Where(o => o.Mandatory)
            .ToList();
        if (mandatory.Count == 0)
            return false;
        var unsatisfied = _kernel.UnsatisfiedMandatoryObligations();
        return mandatory.Any(o => unsatisfied.All(u => u.ObligationId != o.ObligationId));
    }

    /// <summary>多轮咨询（V2）：构建 v2 上下文 + 递增 DecisionId + 锚点核验。</summary>
    private ConsultOutcome ConsultAgentV2(ExecutionContractView view)
    {
        var viewRef = view.AcceptedContractViewRef ?? view.ContractRef ?? view.Version;
        var boundarySignature = string.Join("|", PhaseForCurrent(), _stepsDispatched,
            _completedSteps.Count, _pendingFailurePhase?.ToString() ?? "", _pendingFailedStepIndex?.ToString() ?? "",
            _pendingInterruption?.SignalKey ?? "", _pendingInterruption?.EventSeq.ToString() ?? "");
        if (!string.Equals(_boundarySignature, boundarySignature, StringComparison.Ordinal))
        {
            _boundarySignature = boundarySignature;
            _boundarySequence++;
        }
        var boundaryId = $"{_kernel.RunId}:boundary-{_boundarySequence}";

        // A pending record survives a process crash. Refuse the whole run until
        // an explicit recovery path exists; never issue a second consultation.
        if (_consultationJournal.HasPending(_kernel.RunId, viewRef))
            return new ConsultOutcome(null, "consultation-pending");

        // Reconstruct the ordinal from durable records so restart cannot reset
        // the HardCap counter and mint an unbounded sequence.
        _consultCounter = Math.Max(_consultCounter,
            _consultationJournal.ConsumedCount(_kernel.RunId, viewRef));
        _consultCounter++;
        var decisionId = DecisionIdForRun(_consultCounter);

        var state = _kernel.RunState!;
        var claims = _kernel.CurrentBelief?.WorldState;
        // Typed hierarchy fields are evidence for the WorldModel's semantic
        // occurrence projection; they are not Agent-facing world claims.  A
        // full ui.node.* dump overwhelms a real model and duplicates the
        // Elements projection. Preserve the existing claim surface for
        // product subjects while suppressing only raw hierarchy fields.
        var claimSummaries = claims is null
            ? new Dictionary<string, ClaimSummary>()
            : claims
                .Where(kv => !kv.Key.StartsWith("ui.node.", StringComparison.Ordinal))
                .ToDictionary(
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
            BudgetRemaining: CurrentBudget(view),
            RenewalWindow: RenewalWindowViewFor(),
            ProductSessionId: view.ProductSessionId,
            ContractViewRef: viewRef,
            DecisionBoundaryId: boundaryId,
            ExecutionOverview: BuildExecutionOverview(view),
            BudgetReview: BuildBudgetReview(view),
                            PlanExecution: AgentPlanExecutionView(),
            AvailablePlanSources: _consultCounter == 1 ? _inputs.AvailablePlanSources : null,
            AvailablePlanConditions: _consultCounter == 1 ? _inputs.AvailablePlanConditions : null);

        if (_pendingInterruption is { } interruption)
        {
            context = context with
            {
                FailureReason = interruption.Reason ?? context.FailureReason,
                Heartbeat = interruption.Heartbeat ?? context.Heartbeat,
                ContextReferenceIndex = interruption.ContextReferenceIndex ?? context.ContextReferenceIndex,
                CasePacket = interruption.CasePacket ?? context.CasePacket,
            };
        }

        var key = new ConsultationKey(_kernel.RunId, viewRef, boundaryId, decisionId);
        var fingerprint = ConsultationFingerprint(context);
        ConsultationJournalEntry reserved;
        try
        {
            reserved = _consultationJournal.Reserve(key, fingerprint);
        }
        catch (ConsultationJournalConflictException)
        {
            return new ConsultOutcome(null, "consultation-payload-conflict");
        }
        if (reserved.Status == ConsultationJournalStatus.Completed)
            return new ConsultOutcome(reserved.Decision, null);
        if (reserved.Status == ConsultationJournalStatus.Rejected)
            return new ConsultOutcome(null, reserved.RejectionReason ?? "consultation-rejected");
        if (reserved.Status != ConsultationJournalStatus.Pending)
            return new ConsultOutcome(null, "consultation-journal-invalid-state");

        AgentDecision? decision;
        try
        {
            decision = _inputs.ConsultAgent(context);
        }
        catch (Exception ex)
        {
            _consultationJournal.Reject(key, fingerprint, "consultation-exception:" + ex.GetType().Name);
            return new ConsultOutcome(null, "consultation-exception");
        }
        if (decision is null)
        {
            _consultationJournal.Reject(key, fingerprint, "no-response");
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
            // AGT-009：Plan 同律回带 DecisionId（D2 防串话适用于每一次
            // consultation response；Policy 先例）
            case AgentDecision.Plan plan: answeredId = plan.DecisionId; break;
            case AgentDecision.ContinuePlan resume: answeredId = resume.DecisionId; break;
            // AGT-019 M1：Blocked 同律回带 DecisionId（D2 防串话；Plan 先例）
            case AgentDecision.Blocked blocked: answeredId = blocked.DecisionId; break;
            case AgentDecision.Skip skip: answeredId = skip.DecisionId; break;
            default:
                _consultationJournal.Reject(key, fingerprint, "unknown-decision-kind");
                return new ConsultOutcome(null, "unknown-decision-kind");
        }
        if (answeredId != decisionId)
        {
            _consultationJournal.Reject(key, fingerprint, "correlation-mismatch");
            return new ConsultOutcome(null, "correlation-mismatch");
        }

        try
        {
            _consultationJournal.Complete(key, fingerprint, decision);
        }
        catch (ConsultationJournalConflictException)
        {
            return new ConsultOutcome(null, "consultation-payload-conflict");
        }

        // 清除失败上下文（已消费）；RUN-005：policy 结局同点消费即清（§8——
        // 活到下次咨询为止，本咨询 context 已携带其投影）
        _pendingFailureReason = null;
        _pendingFailurePhase = null;
        _pendingFailedStepIndex = null;
        _pendingPolicyOutcome = null;
        _pendingInterruption = null;
        return new ConsultOutcome(decision, null);
    }

    private static string ConsultationFingerprint(AgentDecisionContext context)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            context.RunId,
            context.ProductSessionId,
            context.ContractViewRef,
            context.DecisionBoundaryId,
            context.DecisionId,
            context.ContractVersion,
            context.Objective,
            AllowedEffects = context.AllowedEffects.OrderBy(x => x, StringComparer.Ordinal),
            Claims = context.CurrentWorldClaims.OrderBy(x => x.Key, StringComparer.Ordinal),
            context.PendingObligations,
            context.Phase,
            context.FailureReason,
            context.FailedStepIndex,
            context.Progress,
            context.BudgetRemaining,
            context.Heartbeat,
            context.ContextReferenceIndex,
            context.CasePacket,
            context.ExecutionOverview,
            context.BudgetReview,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
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
        if (current?.Containers is not { } containers)
            return null;
        var containerId = CurrentInteractionContainerId(current);
        if (containerId is null) return null;
        var signature = current.WorldState.TryGetValue(
            World.WorldModel.SignatureSubjectPrefix + containerId, out var sig)
            ? sig.Value : null;
        return new ScreenSummary(containerId, signature);
    }

    private string? CurrentInteractionContainerId(WorldBeliefRevision? belief)
    {
        if (belief?.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var current) == true
            && !_kernel.CurrentConflictedSubjects.Contains(WorldModel.CurrentContainerSubject)
            && belief.Containers?.Count(container => container.Identity.ContainerId == current.Value) == 1)
            return current.Value;
        return belief?.Containers is { Count: 1 } containers ? containers[0].Identity.ContainerId : null;
    }

    private IReadOnlyList<ElementSummary> DeriveElementSummaries(string? containerId = null)
    {
        var current = _kernel.CurrentBelief;
        if (current?.Occurrences is not { } occurrences)
            return Array.Empty<ElementSummary>();
        var effectiveContainerId = containerId ?? CurrentInteractionContainerId(current);
        if (effectiveContainerId is null)
            return Array.Empty<ElementSummary>();
        var hasSingleContainer = current.Containers is { Count: 1 };
        return occurrences.Where(o => o.OwningContainerId == effectiveContainerId
                || (hasSingleContainer && o.OwningContainerId is null))
            .Select(o => new ElementSummary(
                o.Role,
                o.SemanticDescriptor,
                o.Locator is { } locator
                    ? $"{locator.X1:0.####},{locator.Y1:0.####},{locator.X2:0.####},{locator.Y2:0.####}"
                    : null,
                Clickable: o.Role is "ui.element" or "switch",
                Checkable: o.Role == "switch",
                Enabled: true,
                // AGT-019（owner 订正后补齐）：诚实填值——NativeLocator
                // 在场（XML 连接，双源一致）= Observed；视觉单源 = Partial
                // （枚举本义"XML 缺席或未连接——诚实降级"，此前硬编码
                // Observed 属退化填值）。
                o.Native is not null ? ElementEpistemic.Observed : ElementEpistemic.Partial,
                State: o.Role == "switch"
                    ? _kernel.ResolveCheckedForConsultation(new TargetDescriptor(
                        o.Role, o.SemanticDescriptor, o.OwningContainerId)) is { State: FieldState.Observed } checkedValue
                        ? checkedValue.Value.ToString().ToLowerInvariant()
                        : null
                    : o.State))
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
                // V4 hollow-completion（SR-074）。PER-014 R3：满足判定改走
                // typed 权威路径（UniKernel.UnsatisfiedMandatoryObligations →
                // EvaluateObligations → R1 缝），不再直读 legacy state subjects。
                var hasMandatory = _kernel.RunState?.ProofObligations.Obligations
                    .Any(o => o.Mandatory) == true;
                if (hasMandatory && no.Proposal.Completion is null
                    && _consultCounter <= 1
                    && _stepsDispatched == 0
                    && _completedSteps.Count == 0
                    && !AnyMandatoryObligationSatisfied())
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
                // AGT-019 M2（B2）：参数化观察的诚实执法——档位封闭
                //（enum 类型形状）；Purpose 提供则必须有内容且有界。
                if (defer.Spec.Purpose is { } purpose
                    && (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 256))
                    return "defer-purpose-invalid";
                break;
            }
            case AgentDecision.Blocked blocked:
            {
                // AGT-019 M1：reason 是诚实信号的本体——空原因拒绝
                //（honest-signaling 执法；非形状约束）。
                if (string.IsNullOrWhiteSpace(blocked.Reason))
                    return "blocked-empty-reason";
                break;
            }
            case AgentDecision.Skip skip:
            {
                if (string.IsNullOrWhiteSpace(skip.Target))
                    return "skip-target-empty";
                if (string.IsNullOrWhiteSpace(skip.Reason))
                    return "skip-reason-empty";
                if (skip.EvidenceRefs is null || skip.EvidenceRefs.Count == 0
                    || skip.EvidenceRefs.Any(string.IsNullOrWhiteSpace))
                    return "skip-evidence-required";
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
            case AgentDecision.Plan plan:
            {
                // AGT-009 V7：形态/未知项/预算（纯函数面；malformed /
                // unknown / 超预算计划零 Effect）
                var shape = ValidatePlanProposal(plan.Proposal, view);
                if (shape is not null)
                    return shape;
                var containsComposableItems = plan.Proposal.Items.Any(item => item is PlanItem.ForEachItem or PlanItem.CheckpointItem or PlanItem.IfItem or PlanItem.RepeatItem);
                var actItemCount = plan.Proposal.Items.OfType<PlanItem.ActItem>().Count();
                if (!containsComposableItems && actItemCount > remaining.StepsRemaining)
                    return "budget-exceeded:steps";
                if (plan.Proposal.PlanId is { } planId
                    && _latestPlanVersionById.TryGetValue(planId, out var priorVersion)
                    && plan.Proposal.Version <= priorVersion)
                    return "plan:version-must-increase";
                break;
            }
            case AgentDecision.ContinuePlan resume:
                if (!_planPaused || _activePlanProposal is null || _planItems is null)
                    return "plan:not-paused";
                if (_planInterruptionReason is "plan:local-work-exhausted"
                    or "traversal:local-work-exhausted")
                    return "plan:local-work-exhausted-requires-revision";
                if (_planInterruptionReason is "traversal:navigation-already-satisfied-requires-revision"
                    or "traversal:navigation-cycle")
                    return "plan:traversal-revision-required";
                if (resume.Position != CurrentPlanPosition())
                    return "plan:resume-position-mismatch";
                if (!string.Equals(resume.Position.PlanId, _activePlanProposal.PlanId, StringComparison.Ordinal)
                    || resume.Position.Version != _activePlanProposal.Version)
                    return "plan:resume-identity-mismatch";
                break;
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
                    && _linearStepAnchors.Contains((step.DecisionN, step.StepIndex)),
                var a when a.StartsWith("plan-attempt:", StringComparison.Ordinal) =>
                    _planAttempts.Any(attempt => attempt.AttemptId == a["plan-attempt:".Length..]
                        && attempt.Status == PlanAttemptStatus.Verified),
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
                    new[] { "completion-evidence", evidence.Basis }),
                Origin: Evidence.ObservationOrigin.PostActionSelf)); // 诊断自报（ADR-0048）
        }
    }

    /// <summary>层3：docket 呈递面（公开只读，零新类型——元组）。</summary>
    public (CompletionEvidence Evidence, IReadOnlyList<(string Anchor, bool Verified)> Results)?
        PendingCompletionDossier =>
        _awaitingRuling && _lastCompletionEvidence is { } evidence && _lastAnchorResults is { } results
            ? (evidence, results)
            : null;

    /// <summary>只读证据投影：已通过 post-action 屏障的 step 锚。</summary>
    public IReadOnlyList<(int DecisionN, int StepIndex, string ReceiptId)> CompletedSteps =>
        _completedSteps.ToArray();

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
    /// 当前执行 steps：Policy 展开轮 = 模板物化的单步；Plan 展开轮 =
    /// ActItem 物化的单步（目标均来自上游建议——Kernel 零猜测）；否则 =
    /// 采纳的 Act proposal steps。StepAct/StepVerify 主链经此统一取步，
    /// Act 路径行为不变。
    /// </summary>
    private IReadOnlyList<AgentActionStep> CurrentSteps() =>
        _policyStep is { } policyStep
            ? new[] { policyStep }
            : _planStep is { } planStep
                ? new[] { planStep }
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
    /// AGT-009：ActItem 目标与权威域悬案的相交判定——与
    /// AgentPlanPolicy.FirstConflictIntersectingTarget 同律（"role[:desc]"
    /// 相等或以其为前缀），保证 plan 失效门与 Control 聚焦复查对「同一
    /// 悬案是否触及同一目标」的判定一致。
    /// </summary>
    private static bool PlanTargetConflicted(
        IReadOnlyList<string> conflictedSubjects, PlanItem.ActItem act)
    {
        var target = act.TargetDescriptor is null
            ? act.TargetRole
            : $"{act.TargetRole}:{act.TargetDescriptor}";
        foreach (var conflicted in conflictedSubjects)
        {
            if (conflicted == target
                || conflicted.StartsWith(target + ".", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// AGT-009 §11：步链失败（grounding/gate/verification/no-root）时的
    /// plan 作废——镜像 VoidActivePolicyForStepFailure：只清空 plan 执行态
    ///（尚未执行的剩余计划项全部废弃，冻结决策 11），既有
    /// StepRejected/VerificationFailed 转移与 pendingFailure 捕获不变。
    /// 无活跃 plan 时零副作用。
    /// </summary>
    private void VoidActivePlanForStepFailure()
    {
        if (_activeTraversal is not null && _traversalWork is { } work)
            _traversal.Gap(work.ContainerId, work.Candidate?.Target.Key,
                work.Return ? "return-step-failed" : "navigation-step-failed",
                _kernel.CurrentBelief?.EvidenceBasis.ToArray() ?? Array.Empty<string>());
        AbandonActiveTraversal("traversal:step-chain-failed");
        _planItems = null;
        _planStep = null;
        _planPaused = false;
        _planStatus = PlanExecutionStatus.Abandoned;
    }

    /// <summary>
    /// AGT-009 §11：plan 级出口（耗尽/转移/失效共用）——清空 plan 执行态，
    /// 回 NeedDecision。StepRejected 语义的失效把原因经 _pendingFailure*
    /// 捕获（E2/E3 同构），下次咨询携带；正常出口（耗尽/Reobserve/Replan）
    /// 不设失败上下文。预算门与 ExitPolicy 同律（F10(b) 同构）：失效时
    /// RoundsRemaining ≤ 0 → 由 NeedDecision 既有 consult-budget-exhausted
    /// 检查如实终局（同语义零重复执法）。
    /// </summary>
    private void ExitPlan(AgentDecisionPhase phase, string reason)
    {
        if (reason != "plan-exhausted") AbandonActiveTraversal($"traversal:{reason}");
        if (phase == AgentDecisionPhase.StepRejected)
        {
            _pendingFailureReason = reason;
            _pendingFailurePhase = AgentDecisionPhase.StepRejected;
            _pendingFailedStepIndex = _planItemIndex;
        }
        _planItems = null;
        _planStatus = reason == "plan-exhausted" ? PlanExecutionStatus.Completed : PlanExecutionStatus.Abandoned;
        _planPaused = false;
        _planInterruptionReason = reason == "plan-exhausted" ? null : reason;
        _planStep = null;
        _adoptedDecision = null;
        _phase = DrivePhase.NeedDecision;
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
            if (step.DesiredState is not null
                && CheckedSemantics.FromPresentation(step.DesiredState) is null)
                return "unsupported-desired-state";
        }
        return null;
    }

    /// <summary>
    /// AGT-009 V7：advisory Plan 入口机械校验（proposal 级 + per-item 级）。
    /// 返回 null = 通过；非 null = fail closed 原因。malformed / 未知
    /// PlanItem（closed union 外派生类型）/ 超步数一律零 Effect。
    /// </summary>
    private string? ValidatePlanProposal(AgentPlanProposal proposal, ExecutionContractView view)
    {
        if (proposal.Items.Count == 0) return "plan:empty";
        if (proposal.Items.Count > MaxPlanItems) return "plan:too-many-items";
        if (proposal.Version < 1 || proposal.MaxLocalOperations < 1) return "plan:invalid-version-or-work-limit";
        if (proposal.PlanId is not null && string.IsNullOrWhiteSpace(proposal.PlanId)) return "plan:invalid-id";
        if (proposal.Items.Any(item => item is PlanItem.ForEachItem or PlanItem.CheckpointItem or PlanItem.IfItem or PlanItem.RepeatItem or PlanItem.TraverseItem)
            && string.IsNullOrWhiteSpace(proposal.PlanId)) return "plan:composable-plan-requires-identity";
        var count = 0;
        var ancestors = new HashSet<PlanItem>(ReferenceEqualityComparer.Instance);
        return ValidateBlock(proposal.Items, 0, bound: false);

        string? ValidateBlock(IReadOnlyList<PlanItem>? items, int depth, bool bound)
        {
            if (items is null || items.Count > MaxPlanItems || depth > 8) return "plan:invalid-block";
            foreach (var item in items)
            {
                if (item is null || ++count > 128 || !ancestors.Add(item)) return "plan:invalid-control-structure";
                string? error = null;
                switch (item)
                {
                    case PlanItem.ActItem act:
                        if (string.IsNullOrWhiteSpace(act.TargetRole)) error = "plan:missing-target-role";
                        else if (!bound && (act.TargetRole == "$candidate" || act.TargetDescriptor == "$candidate")) error = "plan:unbound-candidate";
                        else if (string.IsNullOrWhiteSpace(act.EffectClass)) error = "plan:missing-effect-class";
                        else if (!view.AllowedEffects.Contains(act.EffectClass)) error = "plan:effect-class-not-allowed";
                        else if (act.DesiredState is not null && CheckedSemantics.FromPresentation(act.DesiredState) is null) error = "plan:unsupported-desired-state";
                        break;
                    case PlanItem.ForEachItem loop:
                        if (string.IsNullOrWhiteSpace(loop.Source) || loop.MaxIterations < 1 || loop.Body is null || loop.Body.Count == 0)
                            error = "plan:invalid-foreach";
                        else if (_inputs.ProjectPlanCandidates is null || (_inputs.AvailablePlanSources is not null && !_inputs.AvailablePlanSources.ContainsKey(loop.Source)))
                            error = "plan:unknown-candidate-source";
                        else error = ValidateBlock(loop.Body, depth + 1, true);
                        break;
                    case PlanItem.TraverseItem traversal:
                        if (string.IsNullOrWhiteSpace(traversal.Source) || traversal.Rules is null
                            || traversal.Rules.Count == 0 || traversal.MaxDepth < 0 || traversal.ReturnStep is null)
                            error = "plan:invalid-traversal";
                        else if (_inputs.ProjectTraversal is null
                            || _inputs.AvailablePlanSources is null
                            || !_inputs.AvailablePlanSources.ContainsKey(traversal.Source))
                            error = "plan:unknown-traversal-source";
                        else if (traversal.Rules.Any(rule => rule is null || string.IsNullOrWhiteSpace(rule.RuleId)
                            || string.IsNullOrWhiteSpace(rule.Condition) || rule.Operation is null
                            || string.IsNullOrWhiteSpace(rule.Operation.OperationId)
                            || rule.Operation.Before is null || rule.Operation.AfterVerified is null
                            || rule.Operation.Kind == TraversalOperationKind.ReadOnly && rule.Operation.Step is not null
                            || rule.Operation.Kind == TraversalOperationKind.Navigate && rule.Operation.Step is null)
                            || traversal.Rules.Select(rule => rule.RuleId).Distinct(StringComparer.Ordinal).Count() != traversal.Rules.Count)
                            error = "plan:invalid-traversal-rule";
                        else if (traversal.Rules.SelectMany(rule => rule.Operation.Kind == TraversalOperationKind.Navigate
                                ? new[] { rule.Operation.Step! } : Array.Empty<AgentActionStep>())
                            .Append(traversal.ReturnStep)
                            .Any(step => string.IsNullOrWhiteSpace(step.TargetRole)
                                || string.IsNullOrWhiteSpace(step.EffectClass)
                                || !view.AllowedEffects.Contains(step.EffectClass)
                                || step.DesiredState is not null && CheckedSemantics.FromPresentation(step.DesiredState) is null))
                            error = "plan:invalid-traversal-step";
                        else error = ValidateTraversalBindings(traversal);
                        break;
                    case PlanItem.IfItem branch:
                        if (string.IsNullOrWhiteSpace(branch.Condition) || _inputs.EvaluatePlanCondition is null
                            || (_inputs.AvailablePlanConditions is not null && !_inputs.AvailablePlanConditions.ContainsKey(branch.Condition)))
                            error = "plan:unknown-condition";
                        else error = ValidateBlock(branch.Then, depth + 1, bound) ?? ValidateBlock(branch.Else, depth + 1, bound);
                        break;
                    case PlanItem.RepeatItem repeat:
                        if (repeat.MaxIterations < 1 || repeat.Body is null || repeat.Body.Count == 0) error = "plan:invalid-repeat";
                        else error = ValidateBlock(repeat.Body, depth + 1, bound);
                        break;
                    case PlanItem.CheckpointItem:
                    case PlanItem.ObserveItem:
                        break;
                    case PlanItem.ControlItem control:
                        if (control.Kind is not (PlanControlKind.Reobserve or PlanControlKind.Replan or PlanControlKind.Stop)) error = "plan:unknown-control";
                        break;
                    default: error = "plan:unknown-item"; break;
                }
                ancestors.Remove(item);
                if (error is not null) return error;
            }
            return null;
        }
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
        _observationRecovery ??= new ObservationRecoveryControl(
            _inputs.ObservationRecoveryTimeout, _inputs.MaxObservationRecoveryRequests);
        // The slot is the original execution position, never the capture id.
        // Changing scene/capture cannot renew its deadline or its run subquota.
        var slot = $"{_phase}:{_consultCounter}:{_stepIndex}:{_planItemIndex}:{_lastDispatchAt:O}";
        if (_observationPullSlot != slot)
        {
            _observationPullSlot = slot;
            _observationRecoveryEvidence.Clear();
        }
        var processed = _observationRecoveryEvidence;
        if (_observationRecovery.IsExpired(slot))
            return new ObservationPull(RecordObservationRecoveryFailure(slot,
                "page-identity-observation-expired", expected, processed), processed.ToArray());
        while (true)
        {
        var need = _kernel.CurrentBelief?.ObservationNeed;
        TimeSpan? remaining = null;
        if (need is not null)
        {
            if (_inputs.PollCancellation?.Invoke() is { } cancelled)
                return new ObservationPull(CancelPath(cancelled), processed.ToArray());
            if (!_observationRecovery.TrySchedule(slot, out var available))
                return new ObservationPull(RecordObservationRecoveryFailure(slot,
                    "page-identity-observation-exhausted", expected, processed), processed.ToArray());
            remaining = available;
        }
        var input = _inputs.NextInput(new ObservationDirective(expected,
            need is null ? depth : ObservationDepth.Slow,
            need is null ? subjects : new[] { WorldModel.CurrentContainerSubject }, need, remaining));
        if (need is not null && input is RunDriverInput.Observation && !_observationRecovery.CanAccept(slot))
            return new ObservationPull(RecordObservationRecoveryFailure(slot,
                "page-identity-observation-timeout", expected, processed), processed.ToArray());
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

            case RunDriverInput.Interruption interruption:
                if (string.IsNullOrWhiteSpace(interruption.Signal.SignalKey)
                    || string.IsNullOrWhiteSpace(interruption.Signal.Kind)
                    || interruption.Signal.EventSeq < 0)
                    return new ObservationPull(
                        new RunDriveResult(RunDriveStatus.UnexpectedInput,
                            "invalid-interruption-signal", null, _kernel.EffectReceipts.Count),
                        Array.Empty<KernelResult>());
                _pendingInterruption = interruption.Signal;
                // A post-action interruption cannot bypass the verification
                // barrier. Keep StepVerify resumable and let the next input
                // provide the required observation before consulting.
                if (expected == ObservationContext.PostActionEffectFlow)
                    return new ObservationPull(
                        new RunDriveResult(RunDriveStatus.WaitingForInput,
                            "interruption-awaits-post-action-evidence", null,
                            _kernel.EffectReceipts.Count),
                        Array.Empty<KernelResult>());
                return new ObservationPull(null, Array.Empty<KernelResult>());

            case RunDriverInput.Observation observation:
                if (observation.Proposals.Count == 0
                    || observation.Proposals.Any(p => p.Context != expected))
                    return new ObservationPull(
                        new RunDriveResult(
                            RunDriveStatus.UnexpectedInput, $"{phaseLabel}:context-mismatch", null,
                            _kernel.EffectReceipts.Count),
                        Array.Empty<KernelResult>());
                foreach (var proposal in observation.Proposals)
                    processed.Add(_kernel.Process(proposal));
                if (_kernel.CurrentBelief?.ObservationNeed is not null) continue;
                _observationRecovery.Complete(slot);
                var completed = processed.ToArray();
                processed.Clear();
                _observationPullSlot = null;
                return new ObservationPull(null, completed);

            default:
                return new ObservationPull(
                    new RunDriveResult(RunDriveStatus.UnexpectedInput, "unknown-input", null,
                        _kernel.EffectReceipts.Count),
                    Array.Empty<KernelResult>());
        }
        }
    }

    private RunDriveResult RecordObservationRecoveryFailure(string slot, string reason,
        ObservationContext context, IReadOnlyList<KernelResult> observations)
    {
        if (_observationRecoveryFailures.Add(slot) && context == ObservationContext.PostActionEffectFlow)
        {
            var evidence = observations.Select(o => o.Admission.EvidenceId).OfType<string>().ToArray();
            UpdateActivePlanAttempt(PlanAttemptStatus.Rejected,
                _kernel.EffectReceipts.LastOrDefault()?.ReceiptId, evidence, reason);
            if (_activeTraversal is not null && _traversalWork is { } work)
            {
                _traversal.Gap(work.ContainerId, work.Candidate?.Target.Key, reason, evidence);
                _traversal.Pause(reason);
                RecordTraversalState();
                var facts = new TraversalRecoveryFacts(CurrentPlanPosition(), _kernel.TraversalExecution!,
                    _planAttempts.ToArray(), _stepsDispatched, _consultCounter);
                _kernel.RecordTraversalRecovery(facts);
                _inputs.ObserveTraversalRecovery?.Invoke(facts);
            }
        }
        // No Run failure or automatic Back. The original verification position
        // stays closed to late authorization; existing Recovery receives facts.
        return new RunDriveResult(context == ObservationContext.PostActionEffectFlow
            ? RunDriveStatus.VerificationFailed : RunDriveStatus.GroundingFailed,
            reason, null, _kernel.EffectReceipts.Count);
    }

    /// <summary>
    /// Phase 1 cancel 路径（RFS-001 D14）：经 contract 声明的 mandatory SafeStop
    /// obligation 表达——cancel 事实作为外部观察经 P2 入证，Assurance 以既有
    /// 四分类形成 evidence-backed SafeStop proof。未声明该 obligation → fail
    /// closed（非终态）。正式 lifecycle command protocol 仍 Deferred ⑯。
    /// </summary>
    private RunDriveResult CancelPath(RunDriverInput.Cancel cancel)
    {
        var traversalWasActive = _activeTraversal is not null;
        if (traversalWasActive)
        {
            if (_phase == DrivePhase.StepVerify && _traversalWork is { } inFlight)
                _traversal.Gap(inFlight.ContainerId, inFlight.Candidate?.Target.Key,
                    "verification-interrupted-by-cancel", _activeGuardEvidenceRefs);
            AbandonPausedPlan($"traversal:cancelled:{cancel.Reason}");
        }

        var safeStop = _kernel.RunState?.ProofObligations.Obligations
            .FirstOrDefault(o => o.Mandatory && o.Kind == RunObligationKind.SafeStop);
        if (safeStop is null)
        {
            var stopped = new RunDriveResult(
                RunDriveStatus.CancelledWithoutSafeStopPath, cancel.Reason, null, _kernel.EffectReceipts.Count);
            if (traversalWasActive) _traversalCancelDisposition = stopped;
            return stopped;
        }

        if (!traversalWasActive)
            AbandonPausedPlan($"traversal:cancelled:{cancel.Reason}");

        _kernel.Process(new ObservationProposal(
            new ObservationClaim(safeStop.Subject, safeStop.RequiredValue),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance(
                Producer: "kernel.lifecycle",
                CaptureTime: cancel.VirtualTime,
                Scope: "scope:lifecycle.cancel",
                TransformationLineage: new[] { $"cancel:{cancel.Reason}" })));
        var result = EvaluateTerminalOnce();
        if (traversalWasActive && result.Status != RunDriveStatus.Completed)
            _traversalCancelDisposition = result;
        return result;
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
