using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Brain;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
// 注：命名空间 UniClaw.Runtime.Startup / .Container / .Traversal 与同名类——
// 本类位于 UniClaw.Runtime.Agent，裸名会先绑定到命名空间（CS0118），故用类型别名引用类。
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;

namespace UniClaw.Runtime.Agent;

/// <summary>
/// Run 级控制器（宪章 §5；design.md §5；run-lifecycle SHALL）：RunState 全局生命周期唯一 owner（I-2）、
/// WorldBelief 代持（World/Reconcile 只更新不裁决）、Active Container（Phase 1 单容器栈，深度 1）、
/// Plan 驱动（bind / traverse / navigate 循环）、证据评估（每次 post-action Observation 后调用注入的
/// evidence evaluator — SC-P1-003）、最终 failure authority（Run 终止只能由 Agent 发出 — SC-P1-004）、
/// TraceEvent 列表持有（只追加不改写 — 裁决 5）。
/// 不硬编码场景字符串（裁决 3 / 11）：Goal / Plan / 语义解析规则 / container identity 规则 /
/// 容器工厂全部由调用侧注入。无 FSM（I-7）。
/// 恢复（B3 — HG-4 Option B）：机制在 Recovery 组件（配方解析 / 动作分发 / 验证检查），
/// 决策在 Agent（何时恢复、挂起索引、恢复验证、位置恢复、续跑）；单次恢复尝试，无重试策略（HG-2）。
/// 依赖方向：Agent → Startup / Container / Traversal / World / Model（I-1）；
/// 不引用 IEnvironment：初始观测来自注入的 observeInitial，动作后观测来自 Traversal journal（B6 组合模式）。
/// </summary>
public sealed partial class Agent
{
    private readonly RuntimeStartup _startup;
    private readonly RuntimeTraversal _traversal;
    private readonly Func<CancellationToken, Task<Observation>> _observeInitial;
    private readonly Func<Observation, string?> _resolveSemanticPage;
    private readonly Func<string, RuntimeContainer> _containerFactory;
    private readonly RuntimeRecovery _recovery;
    private readonly PageAnalysisCriteria? _pageAnalysisCriteria;
    private readonly ElementBindingCriteria? _elementBindingCriteria;
    private readonly IAssistanceProvider? _assistanceProvider;
    private readonly List<TraceEvent> _trace = [];
    private int _actionCounter;
    private int _recoveryCounter;
    private RunState _state = RunState.Idle;
    private WorldBelief? _belief;
    private RuntimeContainer? _activeContainer;
    private string? _reason;
    private RecoveryAnchor? _recoveryAnchor;
    private Trap? _lastTrap;

    /// <summary>Assistance 咨询计数器（L1 CONSULT — 有界纪律；run 级累计，单 Run 实例无需 reset）。</summary>
    private int _assistanceConsults;

    /// <summary>Assistance 咨询 RequestId 序号。</summary>
    private int _assistanceRequestCounter;

    /// <summary>每次裁决的咨询上限（确定性小常数；耗尽后回到既有 fail-closed 语义，绝不无限循环）。</summary>
    private const int MaxAssistanceConsults = 3;

    /// <summary>SC-P3-CAND-004: immutable cross-Container progress snapshots; sole mutable owner is Agent.</summary>
    private ImmutableDictionary<string, BranchProgressEvidence> _branchProgress =
        ImmutableDictionary<string, BranchProgressEvidence>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>挂起步骤索引（B3 — HG-4 决策记录：恢复前被挂起的 Plan 索引；null = 未发生恢复）。</summary>
    private int? _suspendedStepIndex;

    /// <summary>挂起容器（B3 — HG-4 决策记录：drift 时的活动容器；null = 未发生恢复）。</summary>
    private RuntimeContainer? _suspendedContainer;

    /// <summary>
    /// LATENCY_DRIVEN_BOUNDED_EXECUTION_POLICY: post-scroll deferred reconciliation state.
    /// When true, the Agent has one or more ScrollForward actions whose post-scroll observations
    /// have NOT yet been fully semantically reconciled. Exploration-safe actions (ScrollForward)
    /// are allowed; semantic actions (SetSwitch, Tap, completion) are forbidden until checkpoint.
    /// </summary>
    private bool _postScrollContinuityUnverified;

    /// <summary>
    /// Number of consecutive deferred ScrollForward actions since the last semantic checkpoint.
    /// Reset to zero when semantic reconciliation is performed.
    /// </summary>
    private int _deferredScrollCount;

    /// <summary>
    /// Maximum number of deferred ScrollForward actions before a mandatory semantic checkpoint.
    /// This is a SAFETY / LATENCY BUDGET — NOT scenario knowledge about specific scroll counts.
    /// </summary>
    private const int MaxDeferredScrolls = 5;

    /// <summary>构造 Agent。</summary>
    /// <param name="startup">§19 Startup 程序（Initializing 阶段调用；Ready / NotReady 报告 — run-lifecycle SHALL）。</param>
    /// <param name="traversal">B6 Traversal 实例（读取 Journal[^1]：post-action Observation / 动作载荷 / StepId）。</param>
    /// <param name="observeInitial">初始观测源：Ready 后获取当前观测（StartupResult 不携带 Observation 的补偿注入）。</param>
    /// <param name="resolveSemanticPage">语义解析规则：Observation → 语义页面名（WorldBelief 生成；null = Unknown — §10）。</param>
    /// <param name="containerFactory">容器工厂：语义页面名 → 已装配的 Container（identity 规则 + step executor 由调用侧配置）。</param>
    /// <param name="recovery">恢复机制组件（B3 — HG-4 Option B：机制归组件；决策仍在本 Agent）。</param>
    /// <param name="pageAnalysisCriteria">可选 PageAnalysis 识别知识（观察→多源语义证据）；null = 不启用 PageAnalysis 路径（向后兼容）。</param>
    /// <param name="elementBindingCriteria">可选 BindingAnalysis 绑定识别知识；null = 不启用对象绑定路径（向后兼容）。</param>
    /// <param name="assistanceProvider">可选 L1 CONSULT 外部信息提供者（External Contract Plane 3）；
    /// null = 现状 fail-closed 行为（零回归）。建议制：advice 是候选信息，Agent 保留最终裁决（I-3）。</param>
    /// <exception cref="ArgumentNullException">任一必需参数为 null。</exception>
    public Agent(
        RuntimeStartup startup,
        RuntimeTraversal traversal,
        Func<CancellationToken, Task<Observation>> observeInitial,
        Func<Observation, string?> resolveSemanticPage,
        Func<string, RuntimeContainer> containerFactory,
        RuntimeRecovery recovery,
        PageAnalysisCriteria? pageAnalysisCriteria = null,
        ElementBindingCriteria? elementBindingCriteria = null,
        IAssistanceProvider? assistanceProvider = null)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(traversal);
        ArgumentNullException.ThrowIfNull(observeInitial);
        ArgumentNullException.ThrowIfNull(resolveSemanticPage);
        ArgumentNullException.ThrowIfNull(containerFactory);
        ArgumentNullException.ThrowIfNull(recovery);
        _startup = startup;
        _traversal = traversal;
        _observeInitial = observeInitial;
        _resolveSemanticPage = resolveSemanticPage;
        _containerFactory = containerFactory;
        _recovery = recovery;
        _pageAnalysisCriteria = pageAnalysisCriteria;
        _elementBindingCriteria = elementBindingCriteria;
        _assistanceProvider = assistanceProvider;
    }

    /// <summary>Run 全局生命周期（I-2：唯一 owner 是 Agent；初始 Idle — §18）。</summary>
    public RunState State => _state;

    /// <summary>当前世界信念（WorldBelief 代持 — §11；null = 尚未 Reconcile）。</summary>
    public WorldBelief? Belief => _belief;

    /// <summary>追加式 Trace 因果链（只读快照；唯一可变 owner 是 Agent — 裁决 5 / I-2）。</summary>
    public IReadOnlyList<TraceEvent> Trace => _trace;

    /// <summary>最终显式原因（完成 = GoalEvidence.Reason；失败 = 显式失败原因；终止前为 null — §45）。</summary>
    public string? Reason => _reason;

    /// <summary>
    /// SIBLING/SUBTREE LEDGER — immutable snapshot of the per-Container
    /// completion progress (RequiredChildren = authorized obligations,
    /// CompletedChildren, SubtreeComplete evaluation). Test/evidence
    /// observability only; the Agent remains the sole ledger owner.
    /// </summary>
    public IReadOnlyDictionary<string, BranchProgressEvidence> ProgressSnapshot => _branchProgress;

    /// <summary>Startup Ready 时建立的 RecoveryAnchor（§20）；NotReady 时保持 null（SC-P1-002 断言 3）。</summary>
    public RecoveryAnchor? RecoveryAnchor => _recoveryAnchor;

    /// <summary>最近一次发射的 Trap（B2 — C4 观察面：Trap 载荷的可观测入口；null = 本 Run 未发射）。</summary>
    public Trap? LastTrap => _lastTrap;

    /// <summary>Immutable cross-Container progress snapshot keyed by parent semantic identity.</summary>
    public IReadOnlyDictionary<string, BranchProgressEvidence> BranchProgress => _branchProgress;

    /// <summary>
    /// 逐跳导航证据（宿主独立佐证源）：本 Run 每次被接受的跨容器转场所用的 fresh 观测，按接受顺序。
    /// 真实 UI 转场有动画窗口，Agent 可能以有界重观测接受晚于 journal PostActionObservation 的帧
    /// （journal 记录 Traversal 的首帧 post-action 观测；本列表记录 Agent 语义验证实际接受的观测）。
    /// 观测局部、只读；不是新 authority（决策仍在 Agent；宿主用自有 resolver 独立重建页面名）。
    /// </summary>
    public IReadOnlyList<Observation> NavigationEvidence => _navigationEvidence;

    private readonly List<Observation> _navigationEvidence = [];



    private void RecordDispatchedStep(
        string runId,
        RuntimeContainer container,
        TraversalJournalEntry entry)
    {
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = container.SemanticPageName,
            StepId = entry.StepId,
            ActionId = $"Action-{++_actionCounter}",
            Action = entry.DispatchedAction,
        });
    }


    /// <summary>
    /// SC-P3-CAND-007 bounded same-Container evidence interpretation. Agent is the only decision
    /// authority; Container supplies an immutable retained-evidence snapshot and keeps sole state ownership.
    /// </summary>
    private ViewportExplorationEvidence EvaluateViewportExploration(
        Goal goal,
        RuntimeContainer container,
        string runId,
        string? stepId)
    {
        var evaluator = goal.ViewportExplorationEvaluator
            ?? throw new InvalidOperationException("ViewportExplorationEvaluator 缺失：调用方必须先检查 optional criterion。");
        var retainedEvidence = container.ViewportExplorationObservations;
        if (retainedEvidence.IsDefaultOrEmpty)
            throw new InvalidOperationException("Container 缺少 bounded viewport exploration evidence：Bind 必须先于判定。");
        var result = evaluator(retainedEvidence)
            ?? throw new InvalidOperationException("ViewportExplorationEvaluator 返回 null：必须返回三值 evidence 与非空 Reason。");
        var outcome = result.ContinueExploration switch
        {
            true => "continue",
            false => "exhausted",
            null => "unresolved",
        };
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = container.SemanticPageName,
            StepId = stepId,
            Reason = $"viewport exploration {outcome}: source-seq={retainedEvidence[^1].SequenceNumber}; {result.Reason}",
        });
        return result;
    }

    /// <summary>
    /// SC-P3-CAND-007 bounded same-Container evidence interpretation for the semantic loop,
    /// where the caller injects the evaluator at the RunSemanticGoalAsync call boundary (the
    /// semantic goal input <see cref="SemanticGoalInput"/> deliberately does NOT carry runtime
    /// exploration knowledge). Agent remains the only decision authority; the evaluator only
    /// interprets retained viewport evidence.
    /// </summary>
    private ViewportExplorationEvidence EvaluateViewportExploration(
        Func<ImmutableArray<Observation>, ViewportExplorationEvidence> evaluator,
        RuntimeContainer container,
        string runId,
        string? stepId)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        var retainedEvidence = container.ViewportExplorationObservations;
        if (retainedEvidence.IsDefaultOrEmpty)
            throw new InvalidOperationException("Container 缺少 bounded viewport exploration evidence：Bind 必须先于判定。");
        var result = evaluator(retainedEvidence)
            ?? throw new InvalidOperationException("ViewportExplorationEvaluator 返回 null：必须返回三值 evidence 与非空 Reason。");
        var outcome = result.ContinueExploration switch
        {
            true => "continue",
            false => "exhausted",
            null => "unresolved",
        };
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = container.SemanticPageName,
            StepId = stepId,
            Reason = $"viewport exploration {outcome}: source-seq={retainedEvidence[^1].SequenceNumber}; {result.Reason}",
        });
        return result;
    }

    private static bool IsScrollForwardAction(string actionDescription)
        => string.Equals(actionDescription, "ScrollForward", StringComparison.Ordinal);


    private void EmitViewportEscalation(
        string runId,
        TraversalJournalEntry entry,
        RuntimeContainer container,
        Observation? observed,
        string evidence)
    {
        _lastTrap = container.CreateViewportContinuityEscalation(observed, entry.DispatchedAction, evidence);
        _trace.Add(new TraceEvent(runId)
        {
            StepId = entry.StepId,
            ContainerId = container.SemanticPageName,
            TrapKind = _lastTrap.Kind,
            TrapScope = _lastTrap.Scope,
        });
    }


    /// <summary>经容器工厂创建容器（工厂返回 null = 调用侧协议违约 — §45）。</summary>
    private RuntimeContainer CreateContainer(string semanticPageName)
        => _containerFactory(semanticPageName)
           ?? throw new InvalidOperationException("containerFactory 返回 null：必须返回有效的 Container。");


    /// <summary>
    /// Record one child only when it was locally complete before the return step, the immediately
    /// preceding local action produced child-page evidence, and the return reconciles freshly to the
    /// parent that owns the approved inventory. Return/revisit alone never creates completion.
    /// </summary>
    private void RecordBranchCompletionBeforeReturn(
        Plan plan,
        int stepIndex,
        RuntimeContainer childContainer,
        bool wasLocallyCompleteBeforeStep,
        Observation postReturnObservation,
        string? reconciledParentPage)
    {
        if (!wasLocallyCompleteBeforeStep
            || reconciledParentPage is null
            || string.Equals(reconciledParentPage, childContainer.SemanticPageName, StringComparison.Ordinal)
            || !_branchProgress.TryGetValue(reconciledParentPage, out var parentProgress)
            || _traversal.Journal.Count < 2)
        {
            return;
        }

        var childEvidence = _traversal.Journal[^2].PostActionObservation;
        if (childEvidence is null
            || childEvidence.SequenceNumber >= postReturnObservation.SequenceNumber
            || !string.Equals(
                _resolveSemanticPage(childEvidence),
                childContainer.SemanticPageName,
                StringComparison.Ordinal))
        {
            return;
        }

        string? enteredBranch = null;
        for (var index = stepIndex - 1; index >= 0; index--)
        {
            var target = plan.Steps[index].TargetDescription;
            if (parentProgress.ApprovedSiblingEvidence.ContainsKey(target))
            {
                enteredBranch = target;
                break;
            }
        }
        if (enteredBranch is null)
            return;

        _branchProgress = _branchProgress.SetItem(
            reconciledParentPage,
            parentProgress.WithCompletedSibling(enteredBranch, childEvidence.SequenceNumber));
    }


    /// <summary>读取刚执行步骤的 journal 尾条目（executor 未记录 = 协议违约 — §45）。</summary>
    private TraversalJournalEntry LastJournalEntry()
    {
        if (_traversal.Journal.Count == 0)
            throw new InvalidOperationException("Traversal journal 为空：step executor 未记录执行条目（协议违约 — §45）。");
        return _traversal.Journal[^1];
    }

    // ── Phase 4: Semantic Action Authorization ─────────────────────────────

    /// <summary>
    /// Validates and authorizes a SemanticAction against domain knowledge.
    /// Agent is the sole semantic decision authority (I-3).
    ///
    /// Checks:
    ///   1. SemanticAction is well-formed
    ///   2. Capability applies to the object's category
    ///   3. State dimension matches capability's declared dimension
    ///
    /// Returns null if authorized; returns Invalid result if validation fails.
    /// </summary>
    public static SemanticActionResult? AuthorizeAction(
        SemanticAction action,
        SemanticObject obj,
        Capability capability)
        => ActionAuthorizer.Validate(action, obj, capability);

    /// <summary>终结 Run 为 Failed（Run 终止 authority 唯一在 Agent — I-2 / SC-P1-004）；记录显式原因与失败来源。</summary>
    private RunState Fail(string runId, string reason, string? stepId = null)
    {
        _trace.Add(new TraceEvent(runId)
        {
            ContainerId = _activeContainer?.SemanticPageName,
            StepId = stepId,
            RunState = RunState.Failed,
            Reason = reason,
        });
        _state = RunState.Failed;
        _reason = reason;
        return RunState.Failed;
    }

    /// <summary>Only satisfied GoalEvidence may complete the Run (I-10).</summary>
    private RunState Complete(string runId, GoalEvidence evidence)
    {
        if (!evidence.Satisfied)
            throw new ArgumentException("Only satisfied GoalEvidence may complete the Run.", nameof(evidence));
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Completed, Reason = evidence.Reason });
        _state = RunState.Completed;
        _reason = evidence.Reason;
        return RunState.Completed;
    }

}
