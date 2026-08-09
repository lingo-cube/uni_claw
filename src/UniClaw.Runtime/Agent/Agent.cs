using System.Collections.Immutable;
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
public sealed class Agent
{
    private readonly RuntimeStartup _startup;
    private readonly RuntimeTraversal _traversal;
    private readonly Func<CancellationToken, Task<Observation>> _observeInitial;
    private readonly Func<Observation, string?> _resolveSemanticPage;
    private readonly Func<string, RuntimeContainer> _containerFactory;
    private readonly RuntimeRecovery _recovery;
    private readonly List<TraceEvent> _trace = [];
    private int _actionCounter;
    private int _recoveryCounter;
    private RunState _state = RunState.Idle;
    private WorldBelief? _belief;
    private RuntimeContainer? _activeContainer;
    private string? _reason;
    private RecoveryAnchor? _recoveryAnchor;
    private Trap? _lastTrap;

    /// <summary>SC-P3-CAND-004: immutable cross-Container progress snapshots; sole mutable owner is Agent.</summary>
    private ImmutableDictionary<string, BranchProgressEvidence> _branchProgress =
        ImmutableDictionary<string, BranchProgressEvidence>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>挂起步骤索引（B3 — HG-4 决策记录：恢复前被挂起的 Plan 索引；null = 未发生恢复）。</summary>
    private int? _suspendedStepIndex;

    /// <summary>挂起容器（B3 — HG-4 决策记录：drift 时的活动容器；null = 未发生恢复）。</summary>
    private RuntimeContainer? _suspendedContainer;

    /// <summary>构造 Agent。</summary>
    /// <param name="startup">§19 Startup 程序（Initializing 阶段调用；Ready / NotReady 报告 — run-lifecycle SHALL）。</param>
    /// <param name="traversal">B6 Traversal 实例（读取 Journal[^1]：post-action Observation / 动作载荷 / StepId）。</param>
    /// <param name="observeInitial">初始观测源：Ready 后获取当前观测（StartupResult 不携带 Observation 的补偿注入）。</param>
    /// <param name="resolveSemanticPage">语义解析规则：Observation → 语义页面名（WorldBelief 生成；null = Unknown — §10）。</param>
    /// <param name="containerFactory">容器工厂：语义页面名 → 已装配的 Container（identity 规则 + step executor 由调用侧配置）。</param>
    /// <param name="recovery">恢复机制组件（B3 — HG-4 Option B：机制归组件；决策仍在本 Agent）。</param>
    /// <exception cref="ArgumentNullException">任一参数为 null。</exception>
    public Agent(
        RuntimeStartup startup,
        RuntimeTraversal traversal,
        Func<CancellationToken, Task<Observation>> observeInitial,
        Func<Observation, string?> resolveSemanticPage,
        Func<string, RuntimeContainer> containerFactory,
        RuntimeRecovery recovery)
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
    }

    /// <summary>Run 全局生命周期（I-2：唯一 owner 是 Agent；初始 Idle — §18）。</summary>
    public RunState State => _state;

    /// <summary>当前世界信念（WorldBelief 代持 — §11；null = 尚未 Reconcile）。</summary>
    public WorldBelief? Belief => _belief;

    /// <summary>追加式 Trace 因果链（只读快照；唯一可变 owner 是 Agent — 裁决 5 / I-2）。</summary>
    public IReadOnlyList<TraceEvent> Trace => _trace;

    /// <summary>最终显式原因（完成 = GoalEvidence.Reason；失败 = 显式失败原因；终止前为 null — §45）。</summary>
    public string? Reason => _reason;

    /// <summary>Startup Ready 时建立的 RecoveryAnchor（§20）；NotReady 时保持 null（SC-P1-002 断言 3）。</summary>
    public RecoveryAnchor? RecoveryAnchor => _recoveryAnchor;

    /// <summary>最近一次发射的 Trap（B2 — C4 观察面：Trap 载荷的可观测入口；null = 本 Run 未发射）。</summary>
    public Trap? LastTrap => _lastTrap;

    /// <summary>Immutable cross-Container progress snapshot keyed by parent semantic identity.</summary>
    public IReadOnlyDictionary<string, BranchProgressEvidence> BranchProgress => _branchProgress;

    /// <summary>
    /// 执行一次 Run（Phase 1：一个 Agent 实例恰好对应一次 Run — I-2）：
    /// Idle → Initializing（Startup）→ Ready? → Running（bind / traverse / navigate 循环）→ Completed | Failed。
    /// 每次 post-action Observation 后：Reconcile → evidence evaluator 评估（SC-P1-003）；
    /// Satisfied → Completed（I-10——Plan 耗尽、dispatch 结果均不构成完成判定）；
    /// Plan 耗尽且无 Satisfied → Failed（显式原因，不是 Completed）；
    /// Traversal 步骤 Failed → Running → Failed（StepId + 显式原因，无恢复动作 — SC-P1-004）；
    /// Agent-scope drift（世界信念丢失，B1/B2）→ Trap 发射 + RecoveryAnchor 驱动恢复（B3）；
    /// 恢复验证失败 / 恢复后再次 drift → Failed（显式原因；单次恢复尝试 — HG-2）。
    /// 生命周期转移全部以 TraceEvent 记录（RunState? / Reason? / Action? / ActionId / StepId / ContainerId）。
    /// </summary>
    /// <param name="goal">Goal（evidence evaluator 由调用侧注入 — 裁决 3）。</param>
    /// <param name="plan">执行计划（步数据由调用侧注入 — 裁决 11）。</param>
    /// <param name="runId">Run 标识（TraceEvent.RunId；确定性重放 — SC-P1-001 断言 7）。</param>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>最终 RunState（Completed | Failed）。</returns>
    /// <exception cref="ArgumentNullException">goal 或 plan 为 null。</exception>
    /// <exception cref="ArgumentException">runId 为空或空白。</exception>
    /// <exception cref="InvalidOperationException">实例已执行过 Run；或 executor / containerFactory 违反协议。</exception>
    public async Task<RunState> RunAsync(Goal goal, Plan plan, string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (_state != RunState.Idle)
            throw new InvalidOperationException("Agent 已执行过 Run（Phase 1：一个实例恰好对应一次 Run；请新建实例）。");

        // ── Idle → Initializing（生命周期转移记录 — SC-P1-001 断言 1）───────────────────────────────────
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Idle });
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Initializing });
        _state = RunState.Initializing;

        // ── Initializing：Startup（§19；Ready 之前不得进入 Running — SC-P1-001 / SC-P1-002）──────────────
        var startupResult = await _startup.StartAsync(cancellationToken);
        if (startupResult is StartupResult.NotReady notReady)
        {
            return Fail(runId, notReady.Reason);
        }
        var ready = (StartupResult.Ready)startupResult;
        _recoveryAnchor = ready.Anchor;

        // ── Running：observeInitial → Reconcile → 建立初始容器（bind — §5）──────────────────────────────
        _trace.Add(new TraceEvent(runId) { RunState = RunState.Running });
        _state = RunState.Running;
        var initialObservation = await _observeInitial(cancellationToken);
        _belief = Reconcile.FromObservation(initialObservation, _resolveSemanticPage);
        _activeContainer = CreateContainer(ready.Anchor.ExpectedSemanticEntry);
        _activeContainer.Bind(initialObservation);
        _trace.Add(new TraceEvent(runId) { ContainerId = _activeContainer.SemanticPageName });
        InitializeBranchProgress(initialObservation, plan);

        // SC-P3-CAND-008 is an opt-in bounded protocol. It repeatedly consumes one complete
        // inventory receipt from the current Container evidence, independently authorizes at most
        // one required branch, executes one existing Tap, and reconciles a fresh child Container
        // before another decision. All depth/protocol bookkeeping is Run-local derived data; no
        // route/frontier/depth field or second semantic authority is introduced.
        if (goal.BranchInventoryEvaluator is not null)
        {
            // SC-P3-CAND-009 opt-in: the singular discovered-branch effect carrier scopes this Run to
            // the bounded main-loop revalidation flow instead of the CAND-008 discovery loop. The
            // required P inventory is still accepted only through the frozen CAND-008 acceptance gate
            // (bounded current same-Container evidence + validated source Observation sequences); the
            // carrier itself establishes no membership. Carrier absent keeps the frozen CAND-008 route.
            if (goal.DiscoveredBranchEffectCriterion is null)
            {
                return RunBoundedCrossPageDiscovery(
                    goal,
                    plan,
                    runId);
            }

            var currentContainer = _activeContainer
                ?? throw new InvalidOperationException("bounded revalidation 缺少 active Container。");
            var current = currentContainer.CurrentObservation
                ?? throw new InvalidOperationException("bounded revalidation Container 缺少当前 Observation。");
            var accepted = currentContainer.ViewportExplorationObservations;
            var inventory = goal.BranchInventoryEvaluator(accepted, 0)
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 返回 null：必须返回 BranchInventoryEvidence。");
            var inventoryOutcome = inventory.RequiredBranchEvidence switch
            {
                null => "unresolved",
                { Count: 0 } => "leaf",
                _ => "complete",
            };
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = currentContainer.SemanticPageName,
                Reason = $"branch inventory {inventoryOutcome}: depth=0, source-seq={current.SequenceNumber}; {inventory.Reason}",
            });
            if (!TryAcceptBranchInventory(currentContainer, current, inventory, out _, out var inventoryFailure))
            {
                return Fail(runId, inventoryFailure!);
            }
        }

        // SC-P3-CAND-006：仅当 Goal 显式提供 bounded criterion 时，Agent 对同一 fresh
        // initial Observation 的 candidates 做一次稳定顺序分类。false/null 只留下无 Action 的
        // Trace evidence；first true 才可作为一个 transient Tap step 进入既有 Container/Traversal。
        // 该步骤不修改 immutable Plan，也不把 authorization 解释为 required work 或 completion。
        if (!TryBuildBoundedCandidateExecutionPlan(goal, initialObservation, plan, runId, out var executionPlan))
        {
            return Fail(
                runId,
                $"Bounded candidate authorization 未产生可执行候选（seq={initialObservation.SequenceNumber}）；零 candidate dispatch。");
        }

        ViewportExplorationEvidence? viewportExplorationDecision = null;
        RuntimeContainer? viewportExplorationContainer = null;
        long? viewportExplorationSourceSequence = null;

        // ── Running 循环：bind / traverse / navigate（§5；B3 — 索引循环：drift 恢复需挂起 Plan 索引）──────
        for (int i = 0; i < executionPlan.Steps.Length; i++)
        {
            var step = executionPlan.Steps[i];
            var stepObservation = _activeContainer.CurrentObservation
                ?? throw new InvalidOperationException("active Container 缺少当前观测（协议违约：Bind 必须先于执行）。");
            var isViewportPlanStep = IsScrollForwardAction(step.ActionDescription);
            if (isViewportPlanStep && goal.ViewportExplorationEvaluator is not null)
            {
                var retainedEvidence = _activeContainer.ViewportExplorationObservations;
                var currentEvidenceSequence = retainedEvidence.IsDefaultOrEmpty
                    ? (long?)null
                    : retainedEvidence[^1].SequenceNumber;
                if (!ReferenceEquals(viewportExplorationContainer, _activeContainer)
                    || viewportExplorationSourceSequence != currentEvidenceSequence)
                {
                    viewportExplorationDecision = EvaluateViewportExploration(
                        goal,
                        _activeContainer,
                        runId,
                        stepId: null);
                    viewportExplorationContainer = _activeContainer;
                    viewportExplorationSourceSequence = currentEvidenceSequence;
                }

                if (viewportExplorationDecision!.ContinueExploration is null)
                {
                    return Fail(
                        runId,
                        $"Viewport exploration unresolved：{viewportExplorationDecision.Reason}；不 dispatch 下一次 viewport action。");
                }
                if (viewportExplorationDecision.ContinueExploration is false)
                {
                    var initialExhaustionGoalEvidence = goal.EvidenceEvaluator(stepObservation);
                    if (initialExhaustionGoalEvidence.Satisfied)
                        return Complete(runId, initialExhaustionGoalEvidence);
                    return Fail(
                        runId,
                        $"Viewport exploration positively exhausted，但 GoalEvidence 未满足：{viewportExplorationDecision.Reason}");
                }
            }
            var isLocalHandlingStep = _activeContainer.CanHandleLocalObstruction(
                stepObservation,
                _belief?.SemanticPage,
                _recoveryAnchor.ApplicationIdentity,
                step);
            var wasLocallyCompleteBeforeStep = _activeContainer.IsLocalComplete;
            var result = _activeContainer.ExecuteStep(step);
            var entry = LastJournalEntry();
            var isViewportStep = entry.DispatchedAction is DeviceAction.ScrollForward;
            if (result is TraversalStepResult.Failed failed)
            {
                if (isLocalHandlingStep)
                {
                    EmitContainerEscalation(
                        runId,
                        entry,
                        _activeContainer,
                        entry.PostActionObservation,
                        $"Container-scope local handling 未能完成连续性证明：{failed.Reason}");
                }
                if (isViewportStep)
                {
                    EmitViewportEscalation(
                        runId,
                        entry,
                        _activeContainer,
                        entry.PostActionObservation,
                        $"Viewport action 未能取得可接受的 fresh continuity evidence：{failed.Reason}");
                }
                // SC-P1-004：Agent 是最终 failure authority——StepId + 显式原因，无恢复动作
                return Fail(runId, failed.Reason, entry.StepId);
            }

            // Succeeded：读 Journal[^1]（StepId / 动作载荷 / post-action Observation — B6 组合模式）
            // ActionId：本 Run 内按分发顺序递增的唯一动作标识（SC-P1-001 断言 6 因果链的 ActionId 环节 — B8）
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = _activeContainer.SemanticPageName,
                StepId = entry.StepId,
                ActionId = $"Action-{++_actionCounter}",
                Action = entry.DispatchedAction,
            });
            var postObservation = entry.PostActionObservation
                ?? throw new InvalidOperationException("step executor 返回 Succeeded 但未提供 post-action Observation（协议违约 — §3）。");

            // 每次 post-action Observation 后：Reconcile → evidence evaluator（SC-P1-003）
            _belief = Reconcile.FromObservation(postObservation, _resolveSemanticPage);

            // SC-P3-003：viewport snapshot 变化与 dispatch outcome 都不证明 semantic navigation/continuity。
            // Container 只在 fresh + compatible foreground + IsStillMine + same reconciled page 时推进其
            // CurrentObservation；不 Bind，因而保留 local progress。失败先产生 Container-scope evidence，
            // 再由 Agent 独占 higher-scope response authority。
            var viewportContinuityFailed = false;
            if (isViewportStep
                && !_activeContainer.TryVerifyViewportContinuity(
                    postObservation,
                    _belief.SemanticPage,
                    _recoveryAnchor.ApplicationIdentity))
            {
                viewportContinuityFailed = true;
                EmitViewportEscalation(
                    runId,
                    entry,
                    _activeContainer,
                    postObservation,
                    $"Viewport continuity 未获证明：foreground={postObservation.ForegroundApplication ?? "<null>"}, "
                    + $"semanticPage={_belief.SemanticPage ?? "Unknown"}, seq={postObservation.SequenceNumber}。");
            }

            ViewportExplorationEvidence? postViewportExplorationDecision = null;
            if (isViewportStep
                && !viewportContinuityFailed
                && goal.ViewportExplorationEvaluator is not null)
            {
                postViewportExplorationDecision = EvaluateViewportExploration(
                    goal,
                    _activeContainer,
                    runId,
                    entry.StepId);
                viewportExplorationDecision = postViewportExplorationDecision;
                viewportExplorationContainer = _activeContainer;
                viewportExplorationSourceSequence = _activeContainer.ViewportExplorationObservations[^1].SequenceNumber;
                if (postViewportExplorationDecision.ContinueExploration is null)
                {
                    return Fail(
                        runId,
                        $"Viewport exploration unresolved：{postViewportExplorationDecision.Reason}；不 dispatch 下一次 viewport action。",
                        entry.StepId);
                }
            }

            // B1 — Agent-scope drift（HG-3：无 DriftStatus 字段；仅用既有表面，纯函数）：
            // 前台离开恢复入口基线 + 容器不再属于本页 + 语义页面 Unknown → 世界信念丢失
            // B2 — 结构化 Trap(Scope=Agent) 发射（A1 模型首个消费者）+ 独立 Trap 事件记录
            //      （I-13：Trap 只携带观测序号引用；Expected = 容器绑定观测 / Observed = drift 观测）
            // B3 — 发射 Trap 后进入 RecoveryAnchor 驱动的恢复流程（HG-4 Option B：机制在 Recovery 组件，
            //      决策在 Agent — 挂起索引 / 恢复验证 / 位置恢复 / 续跑）；不再以裸 Fail 终止
            if (!isViewportStep && IsAgentScopeDrift(postObservation, _activeContainer, _belief))
            {
                EmitDriftTrap(runId, entry, postObservation, _activeContainer);
                return await RecoverFromDriftAsync(runId, goal, executionPlan, i, _activeContainer, postObservation, entry.StepId, cancellationToken);
            }

            // SC-P3-002：批准的 bounded local handling 之后，dispatch / Succeeded 都不证明连续性。
            // Container 仅在 fresh sequence + compatible foreground + existing identity rule + reconciled page
            // 共同成立时接受同一 Container；不调用 Bind，因此已有 local progress 保留。
            var localContinuityFailed = false;
            if (isLocalHandlingStep
                && !_activeContainer.TryVerifyLocalContinuity(
                    postObservation,
                    _belief.SemanticPage,
                    _recoveryAnchor.ApplicationIdentity))
            {
                localContinuityFailed = true;
                EmitContainerEscalation(
                    runId,
                    entry,
                    _activeContainer,
                    postObservation,
                    $"Container continuity 未获证明：foreground={postObservation.ForegroundApplication ?? "<null>"}, "
                    + $"semanticPage={_belief.SemanticPage ?? "Unknown"}, seq={postObservation.SequenceNumber}。");
            }

            if (!isViewportStep && !isLocalHandlingStep)
            {
                RecordBranchCompletionBeforeReturn(
                    executionPlan,
                    i,
                    _activeContainer,
                    wasLocallyCompleteBeforeStep,
                    postObservation,
                    _belief.SemanticPage);
            }

            var evidence = goal.EvidenceEvaluator(postObservation);
            if (evidence.Satisfied)
            {
                // I-10：仅 Satisfied 的 GoalEvidence 触发 Completed（dispatch 结果不构成完成判定 — 裁决 10）
                _trace.Add(new TraceEvent(runId) { RunState = RunState.Completed, Reason = evidence.Reason });
                _state = RunState.Completed;
                _reason = evidence.Reason;
                return RunState.Completed;
            }

            if (postViewportExplorationDecision?.ContinueExploration is false)
            {
                return Fail(
                    runId,
                    $"Viewport exploration positively exhausted，但 GoalEvidence 未满足：{postViewportExplorationDecision.Reason}",
                    entry.StepId);
            }
            if (postViewportExplorationDecision?.ContinueExploration is true
                && !HasRemainingViewportStep(executionPlan, i))
            {
                return Fail(
                    runId,
                    $"Viewport exploration bound reached while fresh evidence still requires continuation：{postViewportExplorationDecision.Reason}；semantic exhaustion 未获证明。",
                    entry.StepId);
            }

            // viewport local proof 失败后不重新 dispatch，也不让 snapshot difference 隐式成为 navigation。
            // 若 semantic page 可解析，Agent 可显式 rebind；Unknown 则 Agent 以结构化 Container evidence
            // 为依据结束本 Run。原 Container 的 progress 保持不变。
            if (viewportContinuityFailed)
            {
                var higherScopePage = _belief.SemanticPage;
                if (higherScopePage is null)
                {
                    return Fail(
                        runId,
                        $"Viewport movement 后无法证明 Container 连续性：观测（seq={postObservation.SequenceNumber}）语义页面 Unknown。",
                        entry.StepId);
                }
                _activeContainer = CreateContainer(higherScopePage);
                _activeContainer.Bind(postObservation);
                _trace.Add(new TraceEvent(runId) { ContainerId = _activeContainer.SemanticPageName });
                continue;
            }

            // local proof 已失败时不再次把同一证据分类为可处理 obstruction，避免 blind repeat。
            // Agent 仅使用既有 higher-scope outcome：已解析页面则 rebind；Unknown 则显式失败。
            if (localContinuityFailed)
            {
                var higherScopePage = _belief.SemanticPage;
                if (higherScopePage is null)
                {
                    return Fail(
                        runId,
                        $"局部 obstruction 处理后无法证明 Container 连续性：观测（seq={postObservation.SequenceNumber}）语义页面 Unknown。",
                        entry.StepId);
                }
                _activeContainer = CreateContainer(higherScopePage);
                _activeContainer.Bind(postObservation);
                _trace.Add(new TraceEvent(runId) { ContainerId = _activeContainer.SemanticPageName });
                continue;
            }

            // 未满足：IsStillMine? → 是 → 下一步；否 → Navigate（容器切换判定 authority 在 Agent — I-3）
            if (!_activeContainer.IsStillMine(postObservation))
            {
                // 同一前台 + Unknown + identity 不接受，只构成 Container-scope obstruction hypothesis。
                // 仅当计划中的下一步可由当前候选 grounding 时，Container 接受 fresh obstruction Observation
                // 供一次 bounded handling；不 Bind、不清空 progress、不调用 Recovery。
                if (_activeContainer.IsLocalObstructionHypothesis(
                        postObservation,
                        _belief.SemanticPage,
                        _recoveryAnchor.ApplicationIdentity))
                {
                    if (i + 1 < executionPlan.Steps.Length
                        && _activeContainer.TryAcceptLocalObstruction(
                            postObservation,
                            _belief.SemanticPage,
                            _recoveryAnchor.ApplicationIdentity,
                            executionPlan.Steps[i + 1]))
                    {
                        continue;
                    }
                }

                var newPage = _belief.SemanticPage;
                if (newPage is null)
                {
                    return Fail(runId, $"Navigate 无法继续：观测（seq={postObservation.SequenceNumber}）无法解析新语义页面（Unknown — §10）。");
                }
                _activeContainer = CreateContainer(newPage);
                _activeContainer.Bind(postObservation);
                _trace.Add(new TraceEvent(runId) { ContainerId = _activeContainer.SemanticPageName });
            }
        }

        // ── Plan 耗尽且无 Satisfied 证据：Failed（显式原因；不是 Completed — SC-P1-003 负向）─────────────
        return Fail(runId, $"Plan 步数耗尽但 Goal 证据未满足：最后一次证据评估（seq={_belief?.SourceObservationSequence}）Satisfied=false。");
    }

    /// <summary>
    /// SC-P3-CAND-008 bounded forward discovery. Agent remains the sole inventory/depth/selection
    /// authority. Container supplies accepted page-local evidence, Traversal executes one nominated
    /// local step, and Environment supplies dispatch/Observation evidence only.
    /// </summary>
    private RunState RunBoundedCrossPageDiscovery(Goal goal, Plan plan, string runId)
    {
        var semanticDepth = 0;
        var nextViewportStep = 0;
        var viewportSteps = plan.Steps
            .Where(step => IsScrollForwardAction(step.ActionDescription))
            .ToImmutableArray();

        while (true)
        {
            var container = _activeContainer
                ?? throw new InvalidOperationException("bounded discovery 缺少 active Container。");
            var current = container.CurrentObservation
                ?? throw new InvalidOperationException("bounded discovery Container 缺少当前 Observation。");
            var accepted = container.ViewportExplorationObservations;
            var evaluator = goal.BranchInventoryEvaluator
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 缺失：调用方必须先检查 optional criterion。");
            var inventory = evaluator(accepted, semanticDepth)
                ?? throw new InvalidOperationException("BranchInventoryEvaluator 返回 null：必须返回 BranchInventoryEvidence。");

            var inventoryOutcome = inventory.RequiredBranchEvidence switch
            {
                null => "unresolved",
                { Count: 0 } => "leaf",
                _ => "complete",
            };
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = container.SemanticPageName,
                Reason = $"branch inventory {inventoryOutcome}: depth={semanticDepth}, "
                    + $"source-seq={current.SequenceNumber}; {inventory.Reason}",
            });

            if (inventory.RequiredBranchEvidence is null)
            {
                if (nextViewportStep >= viewportSteps.Length
                    || goal.ViewportExplorationEvaluator is null)
                {
                    return Fail(
                        runId,
                        $"Required branch inventory unresolved at depth={semanticDepth}：{inventory.Reason}；零 discovered-branch dispatch。");
                }

                var viewportDecision = EvaluateViewportExploration(goal, container, runId, stepId: null);
                if (viewportDecision.ContinueExploration is not true)
                {
                    var outcome = viewportDecision.ContinueExploration is false ? "exhausted" : "unresolved";
                    return Fail(
                        runId,
                        $"Branch inventory unresolved and viewport exploration {outcome}：{viewportDecision.Reason}；不 dispatch discovered branch。");
                }

                var viewportStep = viewportSteps[nextViewportStep++];
                var viewportResult = container.ExecuteStep(viewportStep);
                var viewportEntry = LastJournalEntry();
                if (viewportResult is TraversalStepResult.Failed viewportFailed)
                    return Fail(runId, viewportFailed.Reason, viewportEntry.StepId);

                RecordDispatchedStep(runId, container, viewportEntry);
                var viewportObservation = viewportEntry.PostActionObservation
                    ?? throw new InvalidOperationException("viewport step Succeeded 但缺少 fresh Observation。");
                _belief = Reconcile.FromObservation(viewportObservation, _resolveSemanticPage);
                if (!container.TryVerifyViewportContinuity(
                        viewportObservation,
                        _belief.SemanticPage,
                        _recoveryAnchor!.ApplicationIdentity))
                {
                    EmitViewportEscalation(
                        runId,
                        viewportEntry,
                        container,
                        viewportObservation,
                        "Bounded discovery viewport evidence cannot prove same-Container continuity.");
                    return Fail(
                        runId,
                        $"Viewport movement 后无法证明同一 Container continuity（seq={viewportObservation.SequenceNumber}）。",
                        viewportEntry.StepId);
                }

                // Same-Container accepted evidence extends the criterion input but does not change
                // semanticDepth. Re-evaluate inventory from the refreshed evidence next iteration.
                continue;
            }

            if (!TryAcceptBranchInventory(container, current, inventory, out var progress, out var invalidReason))
                return Fail(runId, invalidReason!);

            if (inventory.RequiredBranchEvidence.Count == 0)
            {
                var leafGoalEvidence = goal.EvidenceEvaluator(current);
                if (leafGoalEvidence.Satisfied)
                    return Complete(runId, leafGoalEvidence);
                return Fail(
                    runId,
                    $"Bounded leaf positively proven but GoalEvidence remains unsatisfied：{leafGoalEvidence.Reason}");
            }

            var pendingBranches = progress!.ApprovedSiblingEvidence
                .Where(entry => !progress.CompletedSiblingEvidence.ContainsKey(entry.Key))
                .OrderBy(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .ToArray();
            if (pendingBranches.Length == 0)
            {
                var exhaustedGoalEvidence = goal.EvidenceEvaluator(current);
                if (exhaustedGoalEvidence.Satisfied)
                    return Complete(runId, exhaustedGoalEvidence);
                return Fail(
                    runId,
                    "Required branch inventory contains no unresolved work, but independent GoalEvidence remains unsatisfied；不 redispatch proven branch。");
            }

            var authorizationEvaluator = goal.CandidateAuthorizationEvaluator;
            if (authorizationEvaluator is null)
            {
                return Fail(
                    runId,
                    "Required branch inventory exists but bounded candidate authorization is unresolved because no criterion was supplied；零 dispatch。");
            }

            ObservedElement? selected = null;
            foreach (var (branchIdentity, sourceSequence) in pendingBranches)
            {
                var sourceObservation = accepted.First(observation => observation.SequenceNumber == sourceSequence);
                var sourceCandidate = sourceObservation.Elements.First(element =>
                    string.Equals(element.Text, branchIdentity, StringComparison.Ordinal));
                var authorization = authorizationEvaluator(sourceObservation, sourceCandidate)
                    ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence。");
                var outcome = authorization.Authorized switch
                {
                    true => "authorized",
                    false => "rejected",
                    null => "unresolved",
                };
                _trace.Add(new TraceEvent(runId)
                {
                    ContainerId = container.SemanticPageName,
                    Reason = $"required branch authorization {outcome}: text={branchIdentity}, "
                        + $"source-seq={sourceSequence}; {authorization.Reason}",
                });
                if (authorization.Authorized is true)
                {
                    selected = current.Elements.FirstOrDefault(element =>
                        string.Equals(element.Text, branchIdentity, StringComparison.Ordinal));
                    if (selected is null)
                    {
                        return Fail(
                            runId,
                            $"Required authorized branch '{branchIdentity}' is absent from the current fresh Observation；零 dispatch。");
                    }
                    break;
                }
            }

            if (selected is null)
            {
                return Fail(
                    runId,
                    $"No required branch is independently authorized at depth={semanticDepth}；零 discovered-branch dispatch。");
            }

            var selectedStep = new PlanStep(selected.Text, "Tap");
            var parentPage = container.SemanticPageName;
            var result = container.ExecuteStep(selectedStep);
            var entry = LastJournalEntry();
            if (result is TraversalStepResult.Failed failed)
                return Fail(runId, failed.Reason, entry.StepId);

            RecordDispatchedStep(runId, container, entry);
            var postObservation = entry.PostActionObservation
                ?? throw new InvalidOperationException("bounded branch Tap Succeeded 但缺少 fresh Observation。");
            _belief = Reconcile.FromObservation(postObservation, _resolveSemanticPage);
            var childPage = _belief.SemanticPage;
            if (childPage is null
                || string.Equals(childPage, parentPage, StringComparison.Ordinal)
                || container.IsStillMine(postObservation))
            {
                return Fail(
                    runId,
                    $"Required branch '{selected.Text}' dispatch did not prove a fresh child Container transition；不 blind redispatch。",
                    entry.StepId);
            }

            _activeContainer = CreateContainer(childPage);
            _activeContainer.Bind(postObservation);
            semanticDepth = checked(semanticDepth + 1);
            _trace.Add(new TraceEvent(runId) { ContainerId = childPage });
        }
    }

    private bool TryAcceptBranchInventory(
        RuntimeContainer container,
        Observation current,
        BranchInventoryEvidence inventory,
        out BranchProgressEvidence? progress,
        out string? failure)
    {
        progress = null;
        failure = null;
        var required = inventory.RequiredBranchEvidence;
        if (required is null)
        {
            failure = "Unresolved inventory cannot be accepted.";
            return false;
        }

        var accepted = container.ViewportExplorationObservations;
        if (accepted.IsDefaultOrEmpty
            || accepted[^1].SequenceNumber != current.SequenceNumber
            || !ReferenceEquals(current, container.CurrentObservation)
            || !string.Equals(_belief?.SemanticPage, container.SemanticPageName, StringComparison.Ordinal))
        {
            failure = "Inventory source is not the current accepted semantic Container evidence.";
            return false;
        }

        foreach (var (identity, sequence) in required)
        {
            var source = accepted.FirstOrDefault(observation => observation.SequenceNumber == sequence);
            if (source is null
                || !source.Elements.Any(element => string.Equals(element.Text, identity, StringComparison.Ordinal)))
            {
                failure = $"Inventory branch '{identity}' does not reference accepted source evidence seq={sequence}.";
                return false;
            }
        }

        var completed = ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal);
        if (_branchProgress.TryGetValue(container.SemanticPageName, out var prior))
        {
            completed = prior.CompletedSiblingEvidence
                .Where(entry => required.ContainsKey(entry.Key))
                .ToImmutableDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }

        progress = new BranchProgressEvidence(container.SemanticPageName, required, completed);
        _branchProgress = _branchProgress.SetItem(container.SemanticPageName, progress);
        return true;
    }

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
    /// SC-P3-CAND-006 one-Observation bounded classification. Agent alone consumes the Goal criterion;
    /// lower scopes receive only the first authorized candidate as an existing Tap protocol step.
    /// Rejected/unresolved candidates remain pre-dispatch Trace evidence and never enter Traversal.
    /// </summary>
    private bool TryBuildBoundedCandidateExecutionPlan(
        Goal goal,
        Observation observation,
        Plan plan,
        string runId,
        out Plan executionPlan)
    {
        executionPlan = plan;
        var evaluator = goal.CandidateAuthorizationEvaluator;
        if (evaluator is null)
            return true;

        ObservedElement? firstAuthorized = null;
        foreach (var candidate in observation.Elements)
        {
            var authorization = evaluator(observation, candidate)
                ?? throw new InvalidOperationException("CandidateAuthorizationEvaluator 返回 null evidence：必须返回三值 Authorized 与非空 Reason。");
            if (authorization.Authorized is true)
            {
                firstAuthorized ??= candidate;
                continue;
            }

            var outcome = authorization.Authorized is false ? "rejected" : "unresolved";
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = _activeContainer?.SemanticPageName,
                Reason = $"bounded candidate {outcome}: text={candidate.Text}, index={candidate.Index}, "
                    + $"source-seq={observation.SequenceNumber}; {authorization.Reason}",
            });
        }

        if (firstAuthorized is null)
            return false;

        executionPlan = new Plan(plan.Steps.Insert(
            0,
            new PlanStep(firstAuthorized.Text, "Tap")));
        return true;
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

    private static bool HasRemainingViewportStep(Plan plan, int completedStepIndex)
        => plan.Steps
            .Skip(completedStepIndex + 1)
            .Any(step => IsScrollForwardAction(step.ActionDescription));

    private static bool IsScrollForwardAction(string actionDescription)
        => string.Equals(actionDescription, "ScrollForward", StringComparison.Ordinal);

    /// <summary>
    /// 记录 Container 构造的现有 Trap vocabulary evidence；Agent 仅拥有观察面与 higher-scope response，
    /// 不把 Container-scope proof failure 解释为 Goal success 或自动 Recovery。
    /// </summary>
    private void EmitContainerEscalation(
        string runId,
        TraversalJournalEntry entry,
        RuntimeContainer container,
        Observation? observed,
        string evidence)
    {
        _lastTrap = container.CreateLocalObstructionEscalation(observed, entry.DispatchedAction, evidence);
        _trace.Add(new TraceEvent(runId)
        {
            StepId = entry.StepId,
            ContainerId = container.SemanticPageName,
            TrapKind = _lastTrap.Kind,
            TrapScope = _lastTrap.Scope,
        });
    }

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

    /// <summary>
    /// 发射 Agent-scope drift Trap（B2 — HG-1 恰好 7 字段不变；I-13：只携带观测序号引用，不携带世界快照）
    /// 并记录独立 Trap 事件（与生命周期事件分离：无 RunState / Reason / RecoveryId）；
    /// 载荷保存至 _lastTrap（LastTrap 属性 — C4 观察面）。
    /// </summary>
    /// <param name="runId">Run 标识。</param>
    /// <param name="entry">drift 步骤的 journal 条目（StepId / LastAction 数据源）。</param>
    /// <param name="postObservation">drift post-action Observation（Observed 序号引用）。</param>
    /// <param name="container">drift 时的活动容器（Expected 序号引用与 ContainerId 数据源；调用侧保证非 null）。</param>
    private void EmitDriftTrap(string runId, TraversalJournalEntry entry, Observation postObservation, RuntimeContainer container)
    {
        _lastTrap = new Trap(
            TrapKind.UnexpectedPage,
            TrapScope.Agent,
            container.CurrentObservation?.SequenceNumber,
            postObservation.SequenceNumber,
            "Agent.DetectDrift",
            $"Agent-scope drift: foreground={postObservation.ForegroundApplication} != {_recoveryAnchor?.ApplicationIdentity}, page unresolvable, seq expected={container.CurrentObservation?.SequenceNumber} observed={postObservation.SequenceNumber}",
            entry.DispatchedAction);
        _trace.Add(new TraceEvent(runId)
        {
            StepId = entry.StepId,
            ContainerId = container.SemanticPageName,
            TrapKind = _lastTrap.Kind,
            TrapScope = _lastTrap.Scope,
        });
    }

    /// <summary>
    /// Agent-scope drift 恢复流程（B3 — HG-4 Option B：机制在 Recovery 组件，决策在 Agent）：
    /// 1) Begin（消费 RecoveryAnchor.RestoreRecipe → 配方动作列表，解析在组件）→
    /// 2) 依次分发配方动作（经组件 → IEnvironment；dispatch 结果不构成恢复成功证据 — 裁决 10）→
    /// 3) 恢复后重新观测（§3）→ 4) 按 VerificationCriteria 验证（判据检查在组件；未通过 = 显式 Failed）→
    /// 5) 通过则 Reconcile + 重绑入口容器（ExpectedSemanticEntry — §20）→
    /// 6) 位置恢复：重放 Plan[0..suspendedIndex)（动作解析 + 分发经组件，不重走 Traversal 协议）；
    ///    语义页面回到挂起容器页面即重绑挂起容器并停止重放（挂起容器是 drift 时局部状态 owner — I-2）→
    /// 7) 续跑：从挂起索引重放剩余步骤（含挂起步骤自身——其动作虽已分发，恢复后必须重新执行；
    ///    与主循环相同步逻辑含证据评估；恢复后再次 drift → 发射 Trap + 显式失败，不递归恢复）。
    /// 单次恢复尝试（B3 边界）：无重试 / 无恢复策略（HG-2）。
    /// </summary>
    /// <param name="runId">Run 标识。</param>
    /// <param name="goal">Goal（续跑步骤的证据评估器数据源 — 裁决 3）。</param>
    /// <param name="plan">执行计划（挂起索引的剩余步骤数据源 — 裁决 11）。</param>
    /// <param name="suspendedIndex">挂起步骤索引（drift 步骤在 Plan 中的位置）。</param>
    /// <param name="suspendedContainer">drift 时的活动容器（挂起容器）。</param>
    /// <param name="driftObservation">drift 观测（载荷已固化于 Trap.Evidence — I-13 序号引用；本流程不再消费）。</param>
    /// <param name="suspendedStepId">挂起步骤的 StepId（验证失败显式原因关联）。</param>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>最终 RunState（Completed | Failed）。</returns>
    private async Task<RunState> RecoverFromDriftAsync(
        string runId,
        Goal goal,
        Plan plan,
        int suspendedIndex,
        RuntimeContainer suspendedContainer,
        Observation driftObservation,
        string? suspendedStepId,
        CancellationToken cancellationToken)
    {
        _suspendedStepIndex = suspendedIndex;
        _suspendedContainer = suspendedContainer;

        // 1. Begin recovery：消费 RecoveryAnchor.RestoreRecipe（配方解析在组件 — HG-4）
        _recovery.Begin(_recoveryAnchor!);

        // 2. Execute recipe actions（Relaunch 等；RestoreRecipe 由调用侧注入 — 裁决 8/11；
        //    B3 Startup 尚未填充配方 → 惰性执行）
        while (_recovery.HasRemainingActions)
        {
            var action = await _recovery.ExecuteNextAsync(cancellationToken);
            _trace.Add(new TraceEvent(runId)
            {
                RecoveryId = $"Recovery-{++_recoveryCounter}",
                Action = action,
                ContainerId = _activeContainer?.SemanticPageName,
            });
        }

        // 3. Post-recovery observe（§3：动作后必须重新观察；恢复结果只能经观测确认）
        var recoveryObs = await _recovery.ObserveAsync(cancellationToken);
        _trace.Add(new TraceEvent(runId)
        {
            RecoveryId = $"Recovery-{_recoveryCounter}",
            Reason = $"recovery observe (seq={recoveryObs.SequenceNumber})",
        });

        // 4. Verify（判据在 RecoveryAnchor.VerificationCriteria；检查机制在组件 — HG-4）
        var result = _recovery.Verify(recoveryObs, _recoveryAnchor!.VerificationCriteria);
        _trace.Add(new TraceEvent(runId)
        {
            RecoveryId = $"Recovery-{_recoveryCounter}",
            Reason = $"recovery verify: {(result is RecoveryResult.Verified ? "VERIFIED" : ((RecoveryResult.Failed)result).Reason)}",
        });
        if (result is RecoveryResult.Failed failed)
        {
            // B5：原因由组件构建（SC-P2-003：RecoveryResult.Failed(Reason: "恢复验证失败：期望 X，实际 Y（seq=N）")）→ 原样转交
            return Fail(runId, failed.Reason, suspendedStepId);
        }

        // 5. VERIFIED：Reconcile。SC-P3-CAND-005 先由 Agent 使用 fresh recovered-world evidence
        //    解释 retained branch progress；RecoveryResult.Verified / parent identity 本身不证明 branch effect。
        _belief = Reconcile.FromObservation(recoveryObs, _resolveSemanticPage);
        var resumeFromRecoveredParent = TryRevalidateRecoveredBranchProgress(
            goal.DiscoveredBranchEffectCriterion,
            plan,
            suspendedContainer,
            recoveryObs,
            out var progressValidityFailure);
        if (progressValidityFailure is not null)
        {
            return Fail(runId, progressValidityFailure, suspendedStepId);
        }

        if (resumeFromRecoveredParent)
        {
            // Verified Recovery 已直接回到挂起 parent，且 retained completion 已由 fresh criterion
            // revalidate；重绑同一 owner 后直接续跑，不 replay 已完成 A prefix。
            _activeContainer = suspendedContainer;
            _activeContainer.Bind(recoveryObs);
            _trace.Add(new TraceEvent(runId)
            {
                RecoveryId = $"Recovery-{_recoveryCounter}",
                ContainerId = _activeContainer.SemanticPageName,
                Reason = $"recovered parent branch progress revalidated (seq={recoveryObs.SequenceNumber})",
            });
        }
        else
        {
            // Existing SC-P2 path：没有 applicable retained branch progress 时保持原 position-restore 行为。
            _activeContainer = CreateContainer(_recoveryAnchor!.ExpectedSemanticEntry);
            _activeContainer.Bind(recoveryObs);
            _trace.Add(new TraceEvent(runId)
            {
                RecoveryId = $"Recovery-{_recoveryCounter}",
                ContainerId = _activeContainer.SemanticPageName,
            });
        }

        // 6. Position-restore：重放 Plan[0..suspendedIndex)（动作解析 + 分发经组件，不重走 Traversal 协议）
        for (int j = 0; !resumeFromRecoveredParent && j < suspendedIndex; j++)
        {
            var step = plan.Steps[j];
            var action = _recovery.ResolveRecoveryAction(step, _activeContainer.CurrentObservation!);
            if (action is null)
            {
                return Fail(runId, $"位置恢复: 无法解析 Step-{j + 1} 的动作", suspendedStepId);
            }
            await _recovery.ExecuteActionAsync(action, cancellationToken);
            _trace.Add(new TraceEvent(runId) { RecoveryId = $"Recovery-{_recoveryCounter}", Action = action });
            var obs = await _recovery.ObserveAsync(cancellationToken);
            _belief = Reconcile.FromObservation(obs, _resolveSemanticPage);

            // 页面回到挂起容器页面 → 重绑挂起容器，停止重放
            if (_belief.SemanticPage == suspendedContainer.SemanticPageName)
            {
                _activeContainer = suspendedContainer;
                _activeContainer.Bind(obs);
                _trace.Add(new TraceEvent(runId) { RecoveryId = $"Recovery-{_recoveryCounter}", ContainerId = _activeContainer.SemanticPageName });
                break;
            }
            if (_belief.SemanticPage is not null)
            {
                _activeContainer = CreateContainer(_belief.SemanticPage);
                _activeContainer.Bind(obs);
                _trace.Add(new TraceEvent(runId) { RecoveryId = $"Recovery-{_recoveryCounter}", ContainerId = _activeContainer.SemanticPageName });
            }
        }

        // 7. Resume：续跑剩余步骤（含挂起步骤自身——其动作虽已分发，恢复后必须重新执行）
        _trace.Add(new TraceEvent(runId)
        {
            RecoveryId = $"Recovery-{_recoveryCounter}",
            Reason = $"recovery resume: plan index={suspendedIndex}",
        });
        for (int i = suspendedIndex; i < plan.Steps.Length; i++)
        {
            var step = plan.Steps[i];
            var wasLocallyCompleteBeforeStep = _activeContainer.IsLocalComplete;
            var stepResult = _activeContainer.ExecuteStep(step);
            var entry = LastJournalEntry();
            if (stepResult is TraversalStepResult.Failed stepFailed)
            {
                return Fail(runId, stepFailed.Reason, entry.StepId);
            }
            _trace.Add(new TraceEvent(runId)
            {
                ContainerId = _activeContainer.SemanticPageName,
                StepId = entry.StepId,
                ActionId = $"Action-{++_actionCounter}",
                Action = entry.DispatchedAction,
            });
            var postObservation = entry.PostActionObservation
                ?? throw new InvalidOperationException("step executor 返回 Succeeded 但未提供 post-action Observation（协议违约 — §3）。");
            _belief = Reconcile.FromObservation(postObservation, _resolveSemanticPage);

            // 恢复后再次 drift：单次恢复尝试（不递归）— 发射 Trap + 显式失败（挂起上下文写入原因 — HG-4 决策记录）
            if (IsAgentScopeDrift(postObservation, _activeContainer, _belief))
            {
                EmitDriftTrap(runId, entry, postObservation, _activeContainer);
                return Fail(runId, $"恢复后再次 Agent-scope drift（挂起于 plan index={_suspendedStepIndex}，容器={_suspendedContainer?.SemanticPageName}）", entry.StepId);
            }

            RecordBranchCompletionBeforeReturn(
                plan,
                i,
                _activeContainer,
                wasLocallyCompleteBeforeStep,
                postObservation,
                _belief.SemanticPage);

            var evidence = goal.EvidenceEvaluator(postObservation);
            if (evidence.Satisfied)
            {
                _trace.Add(new TraceEvent(runId) { RunState = RunState.Completed, Reason = evidence.Reason });
                _state = RunState.Completed;
                _reason = evidence.Reason;
                return RunState.Completed;
            }
            if (!_activeContainer.IsStillMine(postObservation))
            {
                var newPage = _belief.SemanticPage;
                if (newPage is null)
                {
                    return Fail(runId, $"Navigate 无法继续：观测（seq={postObservation.SequenceNumber}）无法解析新语义页面（Unknown — §10）。");
                }
                _activeContainer = CreateContainer(newPage);
                _activeContainer.Bind(postObservation);
                _trace.Add(new TraceEvent(runId) { ContainerId = _activeContainer.SemanticPageName });
            }
        }

        return Fail(runId, $"Plan 步数耗尽但 Goal 证据未满足：最后一次证据评估（seq={_belief?.SourceObservationSequence}）Satisfied=false。");
    }

    /// <summary>
    /// SC-P3-CAND-005 bounded recovered-parent protocol extended by SC-P3-CAND-009. Existing
    /// completion sequences at/before Trap.Observed remain historical until their matching branch
    /// effect criterion evaluates the strict-fresh post-verified-Recovery Observation. A PlanStep
    /// carrying a BranchEffectEvidenceEvaluator keeps frozen CAND-005 precedence; a discovered
    /// non-Plan branch may use the Goal-held singular carrier only when the carrier identity is
    /// exactly the completed branch identity present in the approved inventory under the same
    /// suspended parent. The three-way outcome is consumed by Agent control flow and is never
    /// stored as a validity state.
    /// </summary>
    /// <returns>true when retained branch progress made this bounded protocol applicable; false keeps the frozen SC-P2 path.</returns>
    private bool TryRevalidateRecoveredBranchProgress(
        BranchEffectCriterion? discoveredBranchEffectCriterion,
        Plan plan,
        RuntimeContainer suspendedContainer,
        Observation recoveryObservation,
        out string? failure)
    {
        failure = null;
        var driftBoundary = _lastTrap?.Observed;
        if (driftBoundary is null
            || !_branchProgress.TryGetValue(suspendedContainer.SemanticPageName, out var progress))
        {
            return false;
        }

        var retainedCompletions = progress.CompletedSiblingEvidence
            .Where(item => item.Value <= driftBoundary.Value)
            .ToArray();
        if (retainedCompletions.Length == 0)
            return false;

        if (recoveryObservation.SequenceNumber <= driftBoundary.Value
            || !string.Equals(
                _belief?.SemanticPage,
                suspendedContainer.SemanticPageName,
                StringComparison.Ordinal)
            || !suspendedContainer.IsStillMine(recoveryObservation))
        {
            failure =
                $"Recovery 后 retained branch progress 无法验证：fresh recovered parent continuity 未获证明"
                + $"（boundary={driftBoundary.Value}, observed={recoveryObservation.SequenceNumber}, "
                + $"expectedParent={suspendedContainer.SemanticPageName}, actualParent={_belief?.SemanticPage ?? "Unknown"}）。";
            return true;
        }

        var completed = progress.CompletedSiblingEvidence;
        foreach (var (branchIdentity, _) in retainedCompletions)
        {
            // SC-P3-CAND-005 precedence: a PlanStep carrying a durable effect criterion evaluates the
            // fresh Observation unchanged. Transient CAND-006 Tap steps carry no criterion and are
            // not criterion carriers. SC-P3-CAND-009 fallback: a discovered non-Plan branch uses the
            // Goal-held singular carrier only on exact identity match within the approved inventory
            // of this same suspended parent; missing/mismatched identity stays unresolved.
            bool? outcome = null;
            var branchStep = plan.Steps.FirstOrDefault(step =>
                string.Equals(step.TargetDescription, branchIdentity, StringComparison.Ordinal)
                && step.BranchEffectEvidenceEvaluator is not null);
            if (branchStep is not null)
            {
                outcome = branchStep.BranchEffectEvidenceEvaluator?.Invoke(recoveryObservation);
            }
            else if (discoveredBranchEffectCriterion is { } carrier
                && string.Equals(carrier.BranchIdentity, branchIdentity, StringComparison.Ordinal)
                && progress.ApprovedSiblingEvidence.ContainsKey(carrier.BranchIdentity))
            {
                outcome = carrier.Evaluator(recoveryObservation);
            }

            if (outcome is true)
            {
                completed = completed.SetItem(branchIdentity, recoveryObservation.SequenceNumber);
                continue;
            }

            if (outcome is false)
            {
                completed = completed.Remove(branchIdentity);
                failure ??=
                    $"Recovery 后 branch effect contradicted：{branchIdentity} 的 fresh evidence 明确不满足"
                    + $"（seq={recoveryObservation.SequenceNumber}）；不贡献 completion，且不 blind redispatch。";
                continue;
            }

            failure ??=
                $"Recovery 后 branch effect unresolved：{branchIdentity} 缺少可判定的 fresh evidence"
                + $"（seq={recoveryObservation.SequenceNumber}）；retained history 不贡献 completion，且不 blind redispatch。";
        }

        _branchProgress = _branchProgress.SetItem(
            progress.ParentSemanticPage,
            new BranchProgressEvidence(
                progress.ParentSemanticPage,
                progress.ApprovedSiblingEvidence,
                completed));
        return true;
    }

    /// <summary>经容器工厂创建容器（工厂返回 null = 调用侧协议违约 — §45）。</summary>
    private RuntimeContainer CreateContainer(string semanticPageName)
        => _containerFactory(semanticPageName)
           ?? throw new InvalidOperationException("containerFactory 返回 null：必须返回有效的 Container。");

    /// <summary>
    /// SC-P3-CAND-004 bounded inventory: the initial parent Observation must freshly expose at least
    /// two distinct approved Tap targets. Plan limits the approved boundary but never proves presence.
    /// </summary>
    private void InitializeBranchProgress(Observation parentObservation, Plan plan)
    {
        var parentPage = _belief?.SemanticPage;
        if (parentPage is null)
            return;
        var approvedTargets = parentObservation.Elements
            .Select(element => element.Text)
            .Where(text => plan.Steps.Any(step =>
                string.Equals(step.TargetDescription, text, StringComparison.Ordinal)
                && string.Equals(step.ActionDescription, "Tap", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (approvedTargets.Length < 2)
            return;

        var inventory = approvedTargets.ToImmutableDictionary(
            identity => identity,
            _ => parentObservation.SequenceNumber,
            StringComparer.Ordinal);
        _branchProgress = _branchProgress.SetItem(
            parentPage,
            new BranchProgressEvidence(
                parentPage,
                inventory,
                ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal)));
    }

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

    /// <summary>
    /// Agent-scope drift 判定（B1 — HG-3：不新增 DriftStatus 字段；纯函数，不新增状态）。
    /// 三个条件同时成立才判定：
    /// (1) 前台应用 ≠ RecoveryAnchor.ApplicationIdentity（恢复入口基线），
    /// (2) 容器判定当前观测不再属于本语义页面（!IsStillMine — I-3），
    /// (3) 语义页面 Unknown（SemanticPage == null — §10）。
    /// 不误伤正常导航：导航前提是语义页面可解析（条件 3 不成立）；
    /// 仅前台切换而页面可解析、或仍属本页，均不算 drift。
    /// </summary>
    /// <param name="observation">post-action Observation（§3）。</param>
    /// <param name="container">当前活动容器。</param>
    /// <param name="belief">本次 Reconcile 后的世界信念。</param>
    /// <returns>true = Agent-scope 世界信念丢失（由 Agent 判定显式失败）。</returns>
    private bool IsAgentScopeDrift(Observation observation, RuntimeContainer container, WorldBelief belief)
    {
        var baseline = _recoveryAnchor?.ApplicationIdentity;
        return !string.Equals(observation.ForegroundApplication, baseline, StringComparison.Ordinal)
            && !container.IsStillMine(observation)
            && belief.SemanticPage is null;
    }

    /// <summary>读取刚执行步骤的 journal 尾条目（executor 未记录 = 协议违约 — §45）。</summary>
    private TraversalJournalEntry LastJournalEntry()
    {
        if (_traversal.Journal.Count == 0)
            throw new InvalidOperationException("Traversal journal 为空：step executor 未记录执行条目（协议违约 — §45）。");
        return _traversal.Journal[^1];
    }

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
