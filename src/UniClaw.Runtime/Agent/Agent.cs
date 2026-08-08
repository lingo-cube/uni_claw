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

        // ── Running 循环：bind / traverse / navigate（§5；B3 — 索引循环：drift 恢复需挂起 Plan 索引）──────
        for (int i = 0; i < plan.Steps.Length; i++)
        {
            var step = plan.Steps[i];
            var result = _activeContainer.ExecuteStep(step);
            var entry = LastJournalEntry();
            if (result is TraversalStepResult.Failed failed)
            {
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

            // B1 — Agent-scope drift（HG-3：无 DriftStatus 字段；仅用既有表面，纯函数）：
            // 前台离开恢复入口基线 + 容器不再属于本页 + 语义页面 Unknown → 世界信念丢失
            // B2 — 结构化 Trap(Scope=Agent) 发射（A1 模型首个消费者）+ 独立 Trap 事件记录
            //      （I-13：Trap 只携带观测序号引用；Expected = 容器绑定观测 / Observed = drift 观测）
            // B3 — 发射 Trap 后进入 RecoveryAnchor 驱动的恢复流程（HG-4 Option B：机制在 Recovery 组件，
            //      决策在 Agent — 挂起索引 / 恢复验证 / 位置恢复 / 续跑）；不再以裸 Fail 终止
            if (IsAgentScopeDrift(postObservation, _activeContainer, _belief))
            {
                EmitDriftTrap(runId, entry, postObservation, _activeContainer);
                return await RecoverFromDriftAsync(runId, goal, plan, i, _activeContainer, postObservation, entry.StepId, cancellationToken);
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

            // 未满足：IsStillMine? → 是 → 下一步；否 → Navigate（容器切换判定 authority 在 Agent — I-3）
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

        // ── Plan 耗尽且无 Satisfied 证据：Failed（显式原因；不是 Completed — SC-P1-003 负向）─────────────
        return Fail(runId, $"Plan 步数耗尽但 Goal 证据未满足：最后一次证据评估（seq={_belief?.SourceObservationSequence}）Satisfied=false。");
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

        // 5. VERIFIED：Reconcile + 重绑入口容器（RecoveryAnchor.ExpectedSemanticEntry — §20）
        _belief = Reconcile.FromObservation(recoveryObs, _resolveSemanticPage);
        _activeContainer = CreateContainer(_recoveryAnchor!.ExpectedSemanticEntry);
        _activeContainer.Bind(recoveryObs);
        _trace.Add(new TraceEvent(runId)
        {
            RecoveryId = $"Recovery-{_recoveryCounter}",
            ContainerId = _activeContainer.SemanticPageName,
        });

        // 6. Position-restore：重放 Plan[0..suspendedIndex)（动作解析 + 分发经组件，不重走 Traversal 协议）
        for (int j = 0; j < suspendedIndex; j++)
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

    /// <summary>经容器工厂创建容器（工厂返回 null = 调用侧协议违约 — §45）。</summary>
    private RuntimeContainer CreateContainer(string semanticPageName)
        => _containerFactory(semanticPageName)
           ?? throw new InvalidOperationException("containerFactory 返回 null：必须返回有效的 Container。");

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
}
