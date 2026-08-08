using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B2 Trap 发射单元测试：Agent-scope drift 时发射结构化 Trap(TrapKind.UnexpectedPage, TrapScope.Agent)
/// 并记录于 Trace（A1 Trap 模型 + A4 TraceEvent 字段的首个消费者）。
/// 用完整 RunAsync（生产路径：Startup → Running → Traversal 步骤 → drift → Trap → Fail，B2 过渡态；
/// B3 才把 Fail 替换为恢复流程）与「前台中途切换」的脚本化探测环境（直接构造 Observation — Phase 2C）。
/// 判定语义（B1）：前台离开恢复入口基线 且 !IsStillMine 且 SemanticPage==null → Agent-scope drift。
/// 观测流（脚本固定 4 次观测，seq 自增）：seq1 = Startup 观测，seq2 = observeInitial（容器绑定），
/// seq3 = 步骤 post-action 观测（前台切换 → drift），seq4 = 恢复后观测（前台仍未恢复 → 验证失败 — B3）。
/// B3 语义：drift 不再裸 Fail —— Trap 发射后进入恢复流程；本文件把恢复验证接线为「前台≠基线即失败」，
/// 故 drift Run 以「恢复验证失败」终结（Trap 载荷断言不受影响 — 发射点与载荷未变）。
/// Trap 载荷断言经 LastTrap 公共观察面读取（C4 seam；HG-1 不扩展 Trap 字段）。
/// </summary>
public class TrapEmissionTests
{
    private const string BaselineApplication = "Settings";
    private const string DriftForeground = "Launcher";
    private const string ProbeRunId = "probe-run";

    // ── 断言 1 + 8：drift Run 的 Trace 含 Trap 事件（Kind/Scope/StepId/ContainerId）；Run 以 Failed 终止 ──

    [Fact]
    public void DriftRun_TraceContainsTrapEvent_AndStillEndsInFailed()
    {
        var (agent, final, _) = RunDrift();

        // B3：发射 Trap 后进入恢复流程；本文件接线验证失败 → 显式 Failed（不再是 B2 过渡态的裸 Fail）
        Assert.Equal(RunState.Failed, final);
        Assert.Contains("恢复验证失败", agent.Reason);

        var trapEvent = Assert.Single(agent.Trace, e => e.TrapKind is not null);
        Assert.Equal(TrapKind.UnexpectedPage, trapEvent.TrapKind);
        Assert.Equal(TrapScope.Agent, trapEvent.TrapScope);
        Assert.Equal("Step-1", trapEvent.StepId);
        Assert.NotNull(trapEvent.ContainerId);
        Assert.Equal("ProbeEntry", trapEvent.ContainerId);
    }

    // ── 断言 2：Trap 事件与 Failed 事件分离，Trace 顺序为 action → Trap → Failed ──────────────────────

    [Fact]
    public void TrapEvent_IsSeparateFromFailedEvent_OrderedActionThenTrapThenFailed()
    {
        var (agent, _, _) = RunDrift();
        var events = agent.Trace.ToArray();

        var actionIndex = Array.FindIndex(events, e => e.ActionId == "Action-1");
        var trapIndex = Array.FindIndex(events, e => e.TrapKind is not null);
        var failedIndex = Array.FindIndex(events, e => e.RunState == RunState.Failed);

        Assert.True(actionIndex >= 0, "Trace 缺少 action 事件（drift 步骤的 dispatch 记录）。");
        Assert.True(trapIndex >= 0, "Trace 缺少 Trap 事件。");
        Assert.True(failedIndex >= 0, "Trace 缺少 Failed 事件。");
        Assert.True(actionIndex < trapIndex && trapIndex < failedIndex, "Trace 顺序必须为 action → Trap → Failed。");

        // 两个独立事件：Trap 事件不携带 RunState / Reason / RecoveryId，Failed 事件不携带 TrapKind
        Assert.Null(events[trapIndex].RunState);
        Assert.Null(events[trapIndex].Reason);
        Assert.Null(events[trapIndex].RecoveryId);
        Assert.Null(events[failedIndex].TrapKind);
        Assert.NotNull(events[failedIndex].Reason);
    }

    // ── 断言 3：Trap.Expected == 容器绑定观测（drift 前）的序号引用 ────────────────────────────────────

    [Fact]
    public void TrapPayload_Expected_EqualsPreDriftBoundObservationSequence()
    {
        var (agent, _, environment) = RunDrift();
        var trap = ReadLastTrap(agent);

        // 观测流：1=startup，2=observeInitial（容器绑定），3=post-action（drift），4=恢复后观测
        Assert.Equal(4, environment.SequenceCount);
        Assert.Equal(2, trap.Expected);             // Expected = 容器当前绑定观测 seq（drift 前；I-13 序号引用而非快照）
    }

    // ── 断言 4：Trap.Observed == drift 观测的序号引用 ───────────────────────────────────────────────────

    [Fact]
    public void TrapPayload_Observed_EqualsDriftObservationSequence()
    {
        var (agent, _, environment) = RunDrift();
        var trap = ReadLastTrap(agent);

        Assert.Equal(4, environment.SequenceCount);
        Assert.Equal(3, trap.Observed); // Observed = drift post-action 观测 seq
    }

    // ── 断言 5：Source == "Agent.DetectDrift"；Evidence 非空且携带两个序号引用 + 前台事实 ──────────────

    [Fact]
    public void TrapPayload_SourceAndEvidence_CarrySequenceRefsAndForegroundFacts()
    {
        var (agent, _, _) = RunDrift();
        var trap = ReadLastTrap(agent);

        Assert.Equal("Agent.DetectDrift", trap.Source);
        Assert.False(string.IsNullOrWhiteSpace(trap.Evidence));
        Assert.Contains("foreground=Launcher", trap.Evidence);      // drift 前台事实
        Assert.Contains("Settings", trap.Evidence);                 // 基线（RecoveryAnchor.ApplicationIdentity）事实
        Assert.Contains("expected=2", trap.Evidence);               // Expected 序号引用（I-13：非快照）
        Assert.Contains("observed=3", trap.Evidence);               // Observed 序号引用（I-13：非快照）
    }

    // ── 断言 6：Trap.LastAction == 该步 journal 的 DispatchedAction ─────────────────────────────────────

    [Fact]
    public void TrapPayload_LastAction_EqualsStepDispatchedAction()
    {
        var (agent, _, _) = RunDrift();
        var trap = ReadLastTrap(agent);

        var actionEvent = Assert.Single(agent.Trace, e => e.ActionId == "Action-1");
        Assert.NotNull(actionEvent.Action);
        Assert.Equal(actionEvent.Action, trap.LastAction); // 与 action 事件载荷同一动作（entry.DispatchedAction）
    }

    // ── 断言 7a：happy path（Completed）零 Trap 事件 ────────────────────────────────────────────────────

    [Fact]
    public void NonDrift_HappyPathCompleted_EmitsNoTrapEvent()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication, ProbeTarget()),
        ]);
        var (agent, final) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            new Goal(_ => new GoalEvidence(true, "probe: satisfied", null)),
            new Plan([new PlanStep("ProbeTarget", "Tap")]));

        Assert.Equal(RunState.Completed, final);
        Assert.DoesNotContain(agent.Trace, e => e.TrapKind is not null);
    }

    // ── 断言 7b：正常导航（新页面可解析 → Navigate）零 Trap 事件 ───────────────────────────────────────

    [Fact]
    public void NonDrift_Navigation_EmitsNoTrapEvent()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication, new ObservedElement("WiFi", null, 0)),
        ]);
        var (agent, _) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication
                ? (obs.Elements.Any(e => e.Text == "WiFi") ? "NetworkSettings" : "ProbeEntry")
                : null,
            obs => obs.ForegroundApplication == BaselineApplication && !obs.Elements.Any(e => e.Text == "WiFi"),
            new Goal(_ => new GoalEvidence(false, "probe: never satisfied", null)),
            new Plan([new PlanStep("ProbeTarget", "Tap")]));

        // 前台仍为基线 + 页面可解析 → 走 Navigate（ContainerId "NetworkSettings"）而非 drift
        Assert.Contains(agent.Trace, e => e.ContainerId == "NetworkSettings");
        Assert.Contains("Plan 步数耗尽", agent.Reason);
        Assert.DoesNotContain(agent.Trace, e => e.TrapKind is not null);
    }

    // ── 断言 7c：Unknown 页面但前台仍为基线 → 非 drift（Navigate 因 Unknown 失败）零 Trap 事件 ─────────

    [Fact]
    public void NonDrift_UnknownPage_SameForeground_EmitsNoTrapEvent()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication),
        ]);
        var (agent, _) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication
                ? (obs.Elements.Any() ? "ProbeEntry" : null)
                : null,
            obs => obs.Elements.Any(),
            new Goal(_ => new GoalEvidence(false, "probe: never satisfied", null)),
            new Plan([new PlanStep("ProbeTarget", "Tap")]));

        // drift 条件 1 不成立（前台 == 基线）→ 非 drift：Navigate 因 Unknown 失败，但零 Trap 事件
        Assert.Contains("Navigate 无法继续", agent.Reason);
        Assert.DoesNotContain(agent.Trace, e => e.TrapKind is not null);
    }

    // ── 断言 7d：步骤失败（Select 无匹配 → 直接 Fail）零 Trap 事件 ─────────────────────────────────────

    [Fact]
    public void NonDrift_StepFailure_EmitsNoTrapEvent()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication, ProbeTarget()),
        ]);
        var (agent, _) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            new Goal(_ => new GoalEvidence(false, "probe: never satisfied", null)),
            new Plan([new PlanStep("MissingTarget", "Tap")]));

        // 步骤失败 → Agent 直接 Fail（SC-P1-004），不经过 drift 判定 → 零 Trap 事件
        Assert.Contains("MissingTarget", agent.Reason);
        Assert.DoesNotContain(agent.Trace, e => e.TrapKind is not null);
    }

    // ── 断言 9：确定性 — 相同输入 → 相同 Trace（含 Trap 事件）与相同 Trap 载荷 ─────────────────────────

    [Fact]
    public void Deterministic_SameInputs_SameTraceAndTrapPayload()
    {
        var (agent1, _, _) = RunDrift();
        var (agent2, _, _) = RunDrift();

        Assert.Equal(agent1.Trace.ToArray(), agent2.Trace.ToArray());
        Assert.Equal(ReadLastTrap(agent1), ReadLastTrap(agent2));
    }

    // ── 探测基建 ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>drift 场景：脚本观测流（前台在第 3 次观测切换）→ 生产路径完整 Run，返回 Agent / 最终状态 / 环境。</summary>
    private static (RuntimeAgent Agent, RunState Final, ScriptedProbeEnvironment Environment) RunDrift()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget()),
            Template(BaselineApplication, ProbeTarget()),
            Template(DriftForeground),
        ]);
        var (agent, final) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            new Goal(_ => new GoalEvidence(false, "drift probe: never satisfied", null)),
            new Plan([new PlanStep("ProbeTarget", "Tap")]));
        return (agent, final, environment);
    }

    /// <summary>
    /// 生产路径 Run：Startup（目标 = 基线应用）→ 初始观测 → 容器（identity 规则 + B6 Traversal executor 方法组注入）
    /// → 按计划执行；每次 post-action 观测后 Agent 执行 Reconcile / drift 判定 / 证据评估（与真实接线一致）。
    /// </summary>
    private static (RuntimeAgent Agent, RunState Final) RunProbe(
        ScriptedProbeEnvironment environment,
        Func<Observation, string?> resolveSemanticPage,
        Func<Observation, bool> identityRule,
        Goal goal,
        Plan plan)
    {
        var startup = new RuntimeStartup(environment, BaselineApplication, resolveSemanticPage);
        var traversal = new RuntimeTraversal(environment);
        // B3：恢复验证接线为「前台 == 基线」——drift Run（前台 Launcher）恢复验证失败 → 显式 Failed；
        // 非 drift 场景永不进入恢复流程（惰性）
        var recovery = new RuntimeRecovery(
            environment,
            _ => [],
            (_, _) => null,
            (obs, _) => obs.ForegroundApplication == BaselineApplication);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            resolveSemanticPage,
            name => new RuntimeContainer(name, identityRule, traversal.ExecuteStep),
            recovery);
        var final = agent.RunAsync(goal, plan, ProbeRunId, CancellationToken.None).GetAwaiter().GetResult();
        return (agent, final);
    }

    /// <summary>读取 Trap 载荷（B3 — C4 观察面 LastTrap 属性；xunit v2 Assert.NotNull 返回 void — 用 ?? throw 承接）。</summary>
    private static Trap ReadLastTrap(RuntimeAgent agent)
        => agent.LastTrap
           ?? throw new InvalidOperationException("LastTrap 为 null：drift Run 未发射 Trap。");

    private static ObservedElement ProbeTarget() => new("ProbeTarget", null, 0);

    /// <summary>脚本模板：(前台, 元素集)；seq 由环境自增分配。</summary>
    private static (string Foreground, ImmutableArray<ObservedElement> Elements) Template(string foreground, params ObservedElement[] elements)
        => (foreground, [.. elements]);

    /// <summary>
    /// 脚本化探测环境（Phase 2C — 直接构造 Observation，不用 ScriptedEnvironment 的屏幕配置机制）：
    /// 按脚本顺序产出观测，脚本耗尽后重复最后一个模板；前台中途切换由此驱动 drift。
    /// </summary>
    private sealed class ScriptedProbeEnvironment : IEnvironment
    {
        private readonly IReadOnlyList<(string Foreground, ImmutableArray<ObservedElement> Elements)> _script;
        private int _index;
        private long _sequence;

        public ScriptedProbeEnvironment(IReadOnlyList<(string Foreground, ImmutableArray<ObservedElement> Elements)> script)
            => _script = script;

        /// <summary>已产出的观测数量（测试用：Trap.Expected / Observed 序号引用的事实来源）。</summary>
        public long SequenceCount => _sequence;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (foreground, elements) = _script[Math.Min(_index, _script.Count - 1)];
            _index++;
            _sequence++;
            return Task.FromResult(new Observation(elements, foreground, _sequence));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ActionResult(
                ActionResultOutcome.Dispatched, action.ToString(), "probe: dispatched"));
        }
    }
}
