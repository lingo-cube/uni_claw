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
/// B3 恢复流程单元测试（HG-4 Option B：机制在 Recovery 组件 / 决策在 Agent）：
/// Agent-scope drift（B1 判定）→ Trap 发射（B2）→ RecoveryAnchor 驱动恢复 → 续跑 的完整闭环。
/// 生产路径全接线：Startup → observeInitial → Container（identity 规则 + B6 Traversal executor 方法组）→
/// Recovery 组件（配方解析 / 位置恢复动作解析 / 验证判据检查均注入 — 裁决 8/11 不硬编码场景字符串）。
/// 观测流由脚本化探测环境驱动（Phase 2C — 直接构造 Observation；前台中途切换驱动 drift；
/// 恢复观测 / 位置恢复观测 / 续跑观测按脚本推进；动作历史可断言组件分发路径）。
/// B3 边界：单次恢复尝试（无重试 — HG-2）；RestoreRecipe 在 Startup 填充前为 null → 配方执行惰性
/// （配方机制由组件级测试直接验证）；位置恢复动作（组件 → IEnvironment，不重走 Traversal 协议）
/// 是 Agent 集成路径上的组件分发证据。
/// </summary>
public class AgentRecoveryTests
{
    private const string BaselineApplication = "Settings";
    private const string DriftForeground = "Launcher";
    private const string ProbeRunId = "recovery-probe";

    // ── 1. 端到端：Launcher drift → Trap → 恢复（observe/verify/rebind）→ 续跑 → Completed ────────────

    [Fact]
    public void EndToEnd_Drift_Trap_Recovery_Resume_Completed()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget()),   // seq1 startup
            Template(BaselineApplication, ProbeTarget()),   // seq2 observeInitial（入口容器绑定）
            Template(DriftForeground),                      // seq3 步骤 post-action → drift
            Template(BaselineApplication, ProbeTarget()),   // seq4 恢复后观测（验证通过）
            Template(BaselineApplication),                  // seq5 续跑步骤 post-action → ProbeTarget 已清除 → Completed
        ]);
        var (agent, _, final) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            // 诚实 Goal（CP-06 语义门修复 — SEMANTIC_CORRECTION_WITHIN_EXISTING_CP06）：
            // 完成条件 = 「ProbeTarget 已被清出世界」（恢复后续跑步骤产生真实世界效果）。
            // 初始观测（seq2）含 ProbeTarget → 不满足 → Run 必须走 drift/Trap/恢复/续跑路径；
            // 不能用「前台 == 基线」作 Goal — 那在 seq2 即满足，会提前零 dispatch 完成（探针而非完成条件）。
            new Goal(obs => new GoalEvidence(
                !obs.Elements.Any(e => e.Text == "ProbeTarget"),
                "probe: recovered", obs.SequenceNumber)),
            new Plan([new PlanStep("ProbeTarget", "Tap")]));

        Assert.Equal(RunState.Completed, final);
        Assert.Equal(RunState.Completed, agent.Trace[^1].RunState);

        // Trap 已发射（B1/B2 载荷语义保留：Expected=绑定观测 seq2 / Observed=drift 观测 seq3）
        var trapEvent = Assert.Single(agent.Trace, e => e.TrapKind is not null);
        Assert.Equal(TrapKind.UnexpectedPage, trapEvent.TrapKind);
        Assert.Equal(TrapScope.Agent, trapEvent.TrapScope);
        Assert.Equal("Step-1", trapEvent.StepId);
        var trap = agent.LastTrap ?? throw new InvalidOperationException("LastTrap 为 null：drift Run 未发射 Trap。");
        Assert.Equal(2, trap.Expected);
        Assert.Equal(3, trap.Observed);

        // 恢复轨迹：observe → verify VERIFIED → resume；挂起步骤在续跑中重新执行（Action-2）
        Assert.Single(agent.Trace, e => e.Reason is not null && e.Reason == "recovery observe (seq=4)");
        Assert.Single(agent.Trace, e => e.Reason == "recovery verify: VERIFIED");
        Assert.Single(agent.Trace, e => e.Reason == "recovery resume: plan index=0");
        var resumeAction = Assert.Single(agent.Trace, e => e.ActionId == "Action-2");
        Assert.Equal(new DeviceAction.Tap(0), resumeAction.Action);
    }

    // ── 2. 挂起语义：续跑从挂起索引开始（含挂起步骤自身），不从头重放 ──────────────────────────────────

    [Fact]
    public void SuspendSemantics_ResumeReexecutesFromSuspendedIndex()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget(), Wifi()),   // seq1 startup
            Template(BaselineApplication, ProbeTarget(), Wifi()),   // seq2 observeInitial
            Template(BaselineApplication, ProbeTarget(), Wifi()),   // seq3 step0 post-action
            Template(DriftForeground),                              // seq4 step1 post-action → drift（挂起 index=1）
            Template(BaselineApplication, ProbeTarget(), Wifi()),   // seq5 恢复后观测
            Template(BaselineApplication, ProbeTarget(), Wifi()),   // seq6 位置恢复观测（页面已回到挂起容器）
            Template(BaselineApplication, Wifi()),                  // seq7 续跑 step1 post-action → Completed
        ]);
        var (agent, traversal, final) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            new Goal(obs =>
            {
                var satisfied = obs.ForegroundApplication == BaselineApplication
                    && obs.Elements.Any(e => e.Text == "WiFi")
                    && !obs.Elements.Any(e => e.Text == "ProbeTarget");
                return new GoalEvidence(satisfied, "probe: wifi done", obs.SequenceNumber);
            }),
            new Plan([new PlanStep("ProbeTarget", "Tap"), new PlanStep("WiFi", "Tap")]));

        Assert.Equal(RunState.Completed, final);
        // 主循环：Action-1（step0）、Action-2（step1，drift）
        Assert.Single(agent.Trace, e => e.ActionId == "Action-1");
        Assert.Single(agent.Trace, e => e.ActionId == "Action-2");
        // 位置恢复：step0 经组件重放（RecoveryId 事件携带 Tap(0)）
        Assert.Single(agent.Trace, e => e.RecoveryId is not null && e.Action is DeviceAction.Tap tap0 && tap0.TargetElementIndex == 0);
        // 续跑从挂起索引 1 开始：重新执行 step1 → Action-3（而非从 step0 重来）
        Assert.Single(agent.Trace, e => e.Reason == "recovery resume: plan index=1");
        var resumeAction = Assert.Single(agent.Trace, e => e.ActionId == "Action-3");
        Assert.Equal(new DeviceAction.Tap(1), resumeAction.Action);
        // step0 主循环路径只执行一次（位置恢复走组件，不产生额外 Traversal journal 条目）
        Assert.Equal(3, traversal.Journal.Count); // step0 / step1 / 续跑 step1
    }

    // ── 3. 恢复动作经组件 + IEnvironment 分发（不经 Traversal 协议）────────────────────────────────────

    [Fact]
    public void RecoveryActions_DispatchViaComponentAndEnvironment_NotTraversal()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(DriftForeground),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, Wifi()),
        ]);
        var (agent, traversal, _) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            new Goal(obs =>
            {
                var satisfied = obs.ForegroundApplication == BaselineApplication
                    && obs.Elements.Any(e => e.Text == "WiFi")
                    && !obs.Elements.Any(e => e.Text == "ProbeTarget");
                return new GoalEvidence(satisfied, "probe: wifi done", obs.SequenceNumber);
            }),
            new Plan([new PlanStep("ProbeTarget", "Tap"), new PlanStep("WiFi", "Tap")]));

        // 动作顺序：LaunchApp → step0 → step1 → 位置恢复（组件）→ 续跑 step1
        Assert.Equal(5, environment.ExecutedActions.Count);
        Assert.Equal(new DeviceAction.Tap(0), environment.ExecutedActions[3]); // 位置恢复动作：组件 → IEnvironment
        Assert.Equal(new DeviceAction.Tap(1), environment.ExecutedActions[4]); // 续跑动作：Traversal → IEnvironment

        // 未走 Traversal 协议：journal 只有 3 条（位置恢复不产生第 4 条）
        Assert.Equal(3, traversal.Journal.Count);
        // 位置恢复动作带 RecoveryId（组件路径），不产生 ActionId
        var positionRestoreEvent = Assert.Single(agent.Trace, e => e.RecoveryId is not null && e.Action is not null);
        Assert.Null(positionRestoreEvent.ActionId);
    }

    // ── 4. Trace 事件：RecoveryId 会话 + observe / verify / rebind / position-restore / resume ─────────

    [Fact]
    public void TraceEvents_RecoverySession_ObserveVerifyRebindPositionRestoreResume()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(DriftForeground),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, ProbeTarget(), Wifi()),
            Template(BaselineApplication, Wifi()),
        ]);
        var (agent, _, _) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            new Goal(obs =>
            {
                var satisfied = obs.ForegroundApplication == BaselineApplication
                    && obs.Elements.Any(e => e.Text == "WiFi")
                    && !obs.Elements.Any(e => e.Text == "ProbeTarget");
                return new GoalEvidence(satisfied, "probe: wifi done", obs.SequenceNumber);
            }),
            new Plan([new PlanStep("ProbeTarget", "Tap"), new PlanStep("WiFi", "Tap")]));

        var recoveryEvents = agent.Trace.Where(e => e.RecoveryId is not null).ToArray();
        Assert.True(recoveryEvents.Length >= 6, "恢复会话应产生 observe/verify/rebind/position-restore/resume 等事件。");
        // 会话标识一致（无配方动作 → 会话 id 为 Recovery-0）
        Assert.All(recoveryEvents, e => Assert.Equal("Recovery-0", e.RecoveryId));
        // 各阶段事件存在
        Assert.Single(recoveryEvents, e => e.Reason is not null && e.Reason.StartsWith("recovery observe (seq=", StringComparison.Ordinal));
        Assert.Single(recoveryEvents, e => e.Reason == "recovery verify: VERIFIED");
        Assert.Contains(recoveryEvents, e => e.ContainerId == "ProbeEntry"); // 入口容器重绑 + 挂起容器重绑
        Assert.Single(recoveryEvents, e => e.Action is DeviceAction.Tap);    // 位置恢复动作
        Assert.Single(recoveryEvents, e => e.Reason == "recovery resume: plan index=1");
    }

    // ── 5. 验证失败：不可恢复 → Failed + 显式原因（不进入 rebind/resume）───────────────────────────────

    [Fact]
    public void VerifyFailure_Unrecoverable_FailedWithExplicitReason()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication, ProbeTarget()),   // seq1 startup
            Template(BaselineApplication, ProbeTarget()),   // seq2 observeInitial
            Template(DriftForeground),                      // seq3 步骤 post-action → drift
            Template(DriftForeground),                      // seq4 恢复后观测（恢复未生效 → 验证失败）
        ]);
        var (agent, _, final) = RunProbe(
            environment,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            obs => obs.ForegroundApplication == BaselineApplication,
            new Goal(_ => new GoalEvidence(false, "probe: never satisfied", null)),
            new Plan([new PlanStep("ProbeTarget", "Tap")]));

        Assert.Equal(RunState.Failed, final);
        // B5：失败原因显式携带期望判据与实际观测事实（SC-P2-003 Evidence 1/5）
        Assert.Contains("恢复验证失败：期望 [ForegroundApplication == Settings]", agent.Reason);
        Assert.Contains($"实际 Foreground=[{DriftForeground}], page=[{DriftForeground}]（seq=4）", agent.Reason);
        var verifyFailEvent = Assert.Single(agent.Trace,
            e => e.Reason is not null && e.Reason.Contains("recovery verify: 恢复验证失败：", StringComparison.Ordinal));
        Assert.Contains("期望 [ForegroundApplication == Settings]", verifyFailEvent.Reason);
        Assert.Contains($"Foreground=[{DriftForeground}], page=[{DriftForeground}]（seq=4）", verifyFailEvent.Reason);
        // 验证失败 → 不进入 rebind/resume：无恢复后续事件、无 Action-2
        Assert.DoesNotContain(agent.Trace, e => e.Reason is not null && e.Reason.Contains("recovery resume", StringComparison.Ordinal));
        Assert.DoesNotContain(agent.Trace, e => e.ActionId == "Action-2");
    }

    // ── 6. 确定性重放：相同输入 → 相同 Trace（含恢复事件）与相同 Trap ──────────────────────────────────

    [Fact]
    public void Deterministic_SameInputs_SameTraceAndTrap()
    {
        (RuntimeAgent Agent, RunState Final) RunOnce()
        {
            var environment = new ScriptedProbeEnvironment(
            [
                Template(BaselineApplication, ProbeTarget()),
                Template(BaselineApplication, ProbeTarget()),
                Template(DriftForeground),
                Template(BaselineApplication, ProbeTarget()),
                Template(BaselineApplication),
            ]);
            var (agent, _, final) = RunProbe(
                environment,
                obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
                obs => obs.ForegroundApplication == BaselineApplication,
                // 诚实 Goal（与测试 #1 同一修复 — CP-06 语义门）：完成条件 = ProbeTarget 清出世界；
                // 初始观测含 ProbeTarget → 不满足 → drift/Trap/恢复/续跑路径被执行（非探针空转）。
                new Goal(obs => new GoalEvidence(
                    !obs.Elements.Any(e => e.Text == "ProbeTarget"),
                    "probe: recovered", obs.SequenceNumber)),
                new Plan([new PlanStep("ProbeTarget", "Tap")]));
            return (agent, final);
        }

        var (agentA, stateA) = RunOnce();
        var (agentB, stateB) = RunOnce();

        Assert.Equal(RunState.Completed, stateA);
        Assert.Equal(stateA, stateB);
        Assert.Equal(agentA.Trace.ToArray(), agentB.Trace.ToArray()); // 含恢复事件
        Assert.Equal(agentA.LastTrap, agentB.LastTrap);
    }

    // ── 7. Recovery 组件机制：配方消费 → 经 IEnvironment 分发 → 验证判据检查（HG-4 机制归组件）─────────

    [Fact]
    public async Task RecoveryComponent_ConsumesRecipe_DispatchesViaEnvironment_VerifiesByCriteria()
    {
        var environment = new ScriptedProbeEnvironment(
        [
            Template(BaselineApplication),
        ]);
        var recovery = new RuntimeRecovery(
            environment,
            recipe => recipe.StartsWith("relaunch:", StringComparison.Ordinal)
                ? [new DeviceAction.LaunchApp(recipe["relaunch:".Length..])]
                : [],
            (_, _) => null,
            (obs, criteria) => obs.ForegroundApplication == BaselineApplication && criteria == "ForegroundApplication == Settings");
        var anchor = new RecoveryAnchor(
            BaselineApplication, "ProbeEntry", "ForegroundApplication == Settings", RestoreRecipe: "relaunch:Settings");

        recovery.Begin(anchor);
        Assert.True(recovery.HasRemainingActions);
        var action = await recovery.ExecuteNextAsync(CancellationToken.None);
        Assert.Equal(new DeviceAction.LaunchApp("Settings"), action);
        Assert.False(recovery.HasRemainingActions);
        Assert.Equal(new DeviceAction.LaunchApp("Settings"), environment.ExecutedActions[0]); // 经 IEnvironment 分发

        var obs = await recovery.ObserveAsync(CancellationToken.None);
        Assert.Equal(1, obs.SequenceNumber);
        Assert.IsType<RecoveryResult.Verified>(recovery.Verify(obs, "ForegroundApplication == Settings"));
        var failed = Assert.IsType<RecoveryResult.Failed>(recovery.Verify(obs, "ForegroundApplication == Launcher"));
        // B5：失败原因同时包含期望判据与实际观测事实（VerificationCriteria 语义消费 — 非透传）
        Assert.Contains("期望 [ForegroundApplication == Launcher]", failed.Reason);
        Assert.Contains($"实际 Foreground=[{BaselineApplication}], page=[{BaselineApplication}]（seq=1）", failed.Reason);
    }

    // ── 探测基建 ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 生产路径 Run：Startup（目标 = 基线应用）→ 初始观测 → 容器（identity 规则 + B6 Traversal executor）→
    /// Recovery 组件（空配方 / 位置恢复动作解析 / 前台验证判据）→ 按计划执行。
    /// drift 触发后 Agent 进入恢复流程（B3）；恢复验证接线为「前台 == 基线」。
    /// </summary>
    private static (RuntimeAgent Agent, RuntimeTraversal Traversal, RunState Final) RunProbe(
        ScriptedProbeEnvironment environment,
        Func<Observation, string?> resolveSemanticPage,
        Func<Observation, bool> identityRule,
        Goal goal,
        Plan plan)
    {
        var startup = new RuntimeStartup(environment, BaselineApplication, resolveSemanticPage);
        var traversal = new RuntimeTraversal(environment);
        var recovery = CreateRecovery(environment);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            resolveSemanticPage,
            name => new RuntimeContainer(name, identityRule, traversal.ExecuteStep),
            recovery);
        var final = agent.RunAsync(goal, plan, ProbeRunId, CancellationToken.None).GetAwaiter().GetResult();
        return (agent, traversal, final);
    }

    /// <summary>
    /// 组件机制接线（测试侧注入 — 裁决 8/11）：配方 "relaunch:X" → LaunchApp(X)；
    /// 位置恢复动作 = PlanStep 目标文本在当前观测中的 Index → Tap；验证判据 = 前台 == 基线应用。
    /// </summary>
    private static RuntimeRecovery CreateRecovery(IEnvironment environment)
        => new(
            environment,
            recipe => recipe.StartsWith("relaunch:", StringComparison.Ordinal)
                ? [new DeviceAction.LaunchApp(recipe["relaunch:".Length..])]
                : [],
            (step, obs) =>
            {
                int index = -1;
                for (int k = 0; k < obs.Elements.Length; k++)
                {
                    if (obs.Elements[k].Text == step.TargetDescription)
                    {
                        index = k;
                        break;
                    }
                }
                return index >= 0 ? new DeviceAction.Tap(index) : null;
            },
            (obs, _) => obs.ForegroundApplication == BaselineApplication);

    private static ObservedElement ProbeTarget() => new("ProbeTarget", null, 0);

    private static ObservedElement Wifi() => new("WiFi", null, 1);

    /// <summary>脚本模板：(前台, 元素集)；seq 由环境自增分配。</summary>
    private static (string Foreground, ImmutableArray<ObservedElement> Elements) Template(string foreground, params ObservedElement[] elements)
        => (foreground, [.. elements]);

    /// <summary>
    /// 脚本化探测环境（Phase 2C — 直接构造 Observation）：按脚本顺序产出观测，脚本耗尽后重复最后一个模板；
    /// 记录已分发动作（ExecutedActions — 组件分发路径的观察面）；前台中途切换由此驱动 drift。
    /// </summary>
    private sealed class ScriptedProbeEnvironment : IEnvironment
    {
        private readonly IReadOnlyList<(string Foreground, ImmutableArray<ObservedElement> Elements)> _script;
        private readonly List<DeviceAction> _executedActions = [];
        private int _index;
        private long _sequence;

        public ScriptedProbeEnvironment(IReadOnlyList<(string Foreground, ImmutableArray<ObservedElement> Elements)> script)
            => _script = script;

        /// <summary>已产出的观测数量（测试用：Trap.Expected / Observed 序号引用的事实来源）。</summary>
        public long SequenceCount => _sequence;

        /// <summary>已分发动作序列（测试用：恢复动作经组件 → IEnvironment 的观察面）。</summary>
        public IReadOnlyList<DeviceAction> ExecutedActions => _executedActions;

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
            _executedActions.Add(action);
            return Task.FromResult(new ActionResult(
                ActionResultOutcome.Dispatched, action.ToString(), "probe: dispatched"));
        }
    }
}
