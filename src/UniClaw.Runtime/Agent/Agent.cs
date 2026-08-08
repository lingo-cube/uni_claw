using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
// 注：命名空间 UniClaw.Runtime.Startup / .Container / .Traversal 与同名类——
// 本类位于 UniClaw.Runtime.Agent，裸名会先绑定到命名空间（CS0118），故用类型别名引用类。
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Agent;

/// <summary>
/// Run 级控制器（宪章 §5；design.md §5；run-lifecycle SHALL）：RunState 全局生命周期唯一 owner（I-2）、
/// WorldBelief 代持（World/Reconcile 只更新不裁决）、Active Container（Phase 1 单容器栈，深度 1）、
/// Plan 驱动（bind / traverse / navigate 循环）、证据评估（每次 post-action Observation 后调用注入的
/// evidence evaluator — SC-P1-003）、最终 failure authority（Run 终止只能由 Agent 发出 — SC-P1-004）、
/// TraceEvent 列表持有（只追加不改写 — 裁决 5）。
/// 不硬编码场景字符串（裁决 3 / 11）：Goal / Plan / 语义解析规则 / container identity 规则 /
/// 容器工厂全部由调用侧注入。无 FSM（I-7）；无恢复动作（裁决 4 — Phase 2 引入）。
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
    private readonly List<TraceEvent> _trace = [];
    private int _actionCounter;
    private RunState _state = RunState.Idle;
    private WorldBelief? _belief;
    private RuntimeContainer? _activeContainer;
    private string? _reason;
    private RecoveryAnchor? _recoveryAnchor;

    /// <summary>构造 Agent。</summary>
    /// <param name="startup">§19 Startup 程序（Initializing 阶段调用；Ready / NotReady 报告 — run-lifecycle SHALL）。</param>
    /// <param name="traversal">B6 Traversal 实例（读取 Journal[^1]：post-action Observation / 动作载荷 / StepId）。</param>
    /// <param name="observeInitial">初始观测源：Ready 后获取当前观测（StartupResult 不携带 Observation 的补偿注入）。</param>
    /// <param name="resolveSemanticPage">语义解析规则：Observation → 语义页面名（WorldBelief 生成；null = Unknown — §10）。</param>
    /// <param name="containerFactory">容器工厂：语义页面名 → 已装配的 Container（identity 规则 + step executor 由调用侧配置）。</param>
    /// <exception cref="ArgumentNullException">任一参数为 null。</exception>
    public Agent(
        RuntimeStartup startup,
        RuntimeTraversal traversal,
        Func<CancellationToken, Task<Observation>> observeInitial,
        Func<Observation, string?> resolveSemanticPage,
        Func<string, RuntimeContainer> containerFactory)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(traversal);
        ArgumentNullException.ThrowIfNull(observeInitial);
        ArgumentNullException.ThrowIfNull(resolveSemanticPage);
        ArgumentNullException.ThrowIfNull(containerFactory);
        _startup = startup;
        _traversal = traversal;
        _observeInitial = observeInitial;
        _resolveSemanticPage = resolveSemanticPage;
        _containerFactory = containerFactory;
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

    /// <summary>
    /// 执行一次 Run（Phase 1：一个 Agent 实例恰好对应一次 Run — I-2）：
    /// Idle → Initializing（Startup）→ Ready? → Running（bind / traverse / navigate 循环）→ Completed | Failed。
    /// 每次 post-action Observation 后：Reconcile → evidence evaluator 评估（SC-P1-003）；
    /// Satisfied → Completed（I-10——Plan 耗尽、dispatch 结果均不构成完成判定）；
    /// Plan 耗尽且无 Satisfied → Failed（显式原因，不是 Completed）；
    /// Traversal 步骤 Failed → Running → Failed（StepId + 显式原因，无恢复动作 — SC-P1-004）。
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

        // ── Running 循环：bind / traverse / navigate（§5）────────────────────────────────────────────────
        foreach (var step in plan.Steps)
        {
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

    /// <summary>经容器工厂创建容器（工厂返回 null = 调用侧协议违约 — §45）。</summary>
    private RuntimeContainer CreateContainer(string semanticPageName)
        => _containerFactory(semanticPageName)
           ?? throw new InvalidOperationException("containerFactory 返回 null：必须返回有效的 Container。");

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
