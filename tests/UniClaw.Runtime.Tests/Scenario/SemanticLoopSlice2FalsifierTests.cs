using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Vision;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// Slice 2 falsifier proofs (tasks 6.1–6.8): the Agent→Reality semantic loop must
/// NEVER treat dispatch receipt, stale frames, or provider failures as goal success.
///
/// All scenarios use the 5.1 calibration switch geometry
/// (RealitySeededSettingsFixture.RecordedWifiSwitchBounds: "Wi‑Fi" row
/// (0.06,0.42)-(0.164,0.441), toggle (0.832,0.407)-(0.96,0.452)).
/// </summary>
public sealed class SemanticLoopSlice2FalsifierTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ElementBounds WifiRowBounds = new(0.06f, 0.42f, 0.164f, 0.441f);
    private static readonly ElementBounds RecordedToggleBounds = RealitySeededSettingsFixture.RecordedWifiSwitchBounds;

    private static readonly SemanticGoalInput Goal = new("WifiConnectivity", "Enabled", true);

    private sealed class RecoveryProbe
    {
        public bool Entered;
    }

    private static (RuntimeAgent Agent, ScriptedEnvironment Environment, RuntimeTraversal Traversal, RecoveryProbe Probe) Build(
        bool? initialSwitchState,
        bool changeToOn = false,
        bool rejectedTransition = false,
        string? togglePerceptionType = null,
        IReadOnlyDictionary<long, long>? sequenceOverrides = null)
    {
        var probe = new RecoveryProbe();

        var offTransition = changeToOn
            ? new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true,
                DispatchOutcome: rejectedTransition ? ActionResultOutcome.Rejected : ActionResultOutcome.Dispatched)
            : null;

        var settingsScreen = new ScreenConfig("Settings", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, WifiRowBounds, "menuItem"),
            new ElementConfig("", initialSwitchState, offTransition, RecordedToggleBounds, togglePerceptionType ?? "toggle"),
        ]);
        var onScreen = new ScreenConfig("On", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, WifiRowBounds, "menuItem"),
            new ElementConfig("", true, null, RecordedToggleBounds, "toggle"),
        ]);
        var lostScreen = new ScreenConfig("Lost", "settings", []);

        var env = new ScriptedEnvironment(
            "Settings", "Settings",
            [settingsScreen, onScreen, lostScreen],
            observeSequenceOverrides: sequenceOverrides);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(semanticEnv,
            _ => { probe.Entered = true; return []; },
            (_, _) => null,
            (_, _) => true);
        var container = new RuntimeContainer("Settings", o => o.ForegroundApplication == "settings", traversal.ExecuteStep);

        var criteria = new ElementBindingCriteria([Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));

        var agent = new RuntimeAgent(startup, traversal, t => semanticEnv.ObserveAsync(t), _ => "Settings", _ => container, recovery, pages, criteria);
        return (agent, env, traversal, probe);
    }

    // ── 6.1 端到端闭环 + 6.8 trace 因果链 ────────────────────────────────

    [Fact]
    public async Task S2E1_FullLoop_RecordedBounds_FreshGoalEvidence_TraceChain()
    {
        var (agent, env, traversal, _) = Build(initialSwitchState: false, changeToOn: true);
        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "s2e1");
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.True(satisfied.Evidence.Satisfied);
        Assert.Equal(RunState.Completed, agent.State);

        // 恰好一次物理分发：OFF → ON
        var dispatch = Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.True(dispatch.TargetState);
        Assert.Equal(RecordedToggleBounds, dispatch.TargetBounds);

        // 分发收据 ≠ 成功证据：fresh Observation 序列推进（startup seq1 < initial seq2 < post seq3）
        Assert.Equal(new long[] { 1, 2, 3 }, env.ObservationHistory.Select(o => o.SequenceNumber).ToArray());
        var journal = traversal.Journal;
        var entry = Assert.Single(journal);
        Assert.IsType<TraversalStepResult.Succeeded>(entry.Result);
        Assert.NotNull(entry.PostActionObservation);
        var freshObs = entry.PostActionObservation!;
        Assert.Equal(3L, freshObs.SequenceNumber);

        // 感知证据：fresh Observation 提取 ON
        var toggle = Assert.Single(freshObs.Elements.Where(e => e.PerceptionType == "toggle"));
        Assert.True(toggle.SwitchState);
        Assert.Equal(RecordedToggleBounds, toggle.Bounds);

        // 6.8: GoalEvidence.SourceObservationSequence 指向 fresh 观测（journal 同源）
        Assert.Equal(freshObs.SequenceNumber, satisfied.Evidence.SourceObservationSequence);
        Assert.Contains(agent.Trace, t => t.Reason == "semantic capability selected: SetEnabled");
    }

    // ── 6.2 Falsifier F3：dispatch 成功但世界未变 → 非 SATISFIED ─────────

    [Fact]
    public async Task S2F3_DispatchOk_WorldUnchanged_NotSatisfied()
    {
        var (agent, env, traversal, _) = Build(initialSwitchState: false); // 无转场 → SetSwitch Dispatched 但世界不变
        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "s2f3", maxIterations: 2);
        Assert.IsType<SemanticRunResult.BudgetExhausted>(result);
        Assert.Equal(RunState.Failed, agent.State);

        // 分发收据 OK（Traversal Succeeded + fresh 观测序列推进）…
        Assert.Equal(2, traversal.Journal.Count);
        Assert.All(traversal.Journal, e => Assert.IsType<TraversalStepResult.Succeeded>(e.Result));
        // …但世界未变：每次 fresh 观测感知仍是 OFF
        Assert.Equal(2, env.ActionHistory.OfType<DeviceAction.SetSwitch>().Count());
        Assert.All(traversal.Journal, e => Assert.False(e.PostActionObservation!.Elements.Single(x => x.PerceptionType == "toggle").SwitchState));
        // 收据 ≠ 世界变化 → 绝不 SATISFIED
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── 6.3 Falsifier F4：陈旧截图不可验证成功（fail closed）────────────

    [Fact]
    public async Task S2F4_StalePostDispatchObservation_FailsClosed()
    {
        // 第 3 次 Observe（post-dispatch）返回序列 2 == pre-dispatch 序列 2 → 陈旧
        var (agent, env, _, _) = Build(initialSwitchState: false, changeToOn: true,
            sequenceOverrides: new Dictionary<long, long> { [3] = 2 });
        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "s2f4");
        var failed = Assert.IsType<SemanticRunResult.ExecutionFailed>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Contains("post-observation sequence did not advance", failed.Reason);

        // 动作确实分发过一次，但验证路径拒绝陈旧观测 → 非 SATISFIED
        Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    [Fact]
    public void S2F4b_SwitchStateValidation_StaleFrame_FailsClosedNull()
    {
        var frameA = new PerceptionFrame();
        var frameB = new PerceptionFrame();
        var reader = new FakeFrameScopedReader(frameA, true);

        // 同帧 → 通过；异帧 → fail closed (null)；UNKNOWN → 原样 null
        Assert.True(SwitchStateValidation.ValidateFrameMatch(reader, frameA, true));
        Assert.Null(SwitchStateValidation.ValidateFrameMatch(reader, frameB, true));
        Assert.Null(SwitchStateValidation.ValidateFrameMatch(reader, frameA, null));
    }

    private sealed class FakeFrameScopedReader(PerceptionFrame frame, bool? value) : ISwitchStateReader
    {
        public PerceptionFrame Frame { get; } = frame;
        public ValueTask<bool?> ReadAsync(ElementBounds switchBounds, CancellationToken cancellationToken = default)
            => new(value);
    }

    // ── 6.4 Falsifier F5：失败动作不误触发恢复 ──────────────────────────

    [Fact]
    public async Task S2F5_RejectedDispatch_NoAutoRecovery_NotSatisfied()
    {
        var (agent, env, _, probe) = Build(initialSwitchState: false, changeToOn: true, rejectedTransition: true);
        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "s2f5");
        var failed = Assert.IsType<SemanticRunResult.ExecutionFailed>(result);
        Assert.Equal(RunState.Failed, agent.State);

        // 单次 dispatch 失败 → TraversalStepResult.Failed(结构化原因) → Agent 决策（SC-P1-004 escalate 不偷权）
        Assert.Contains("Semantic action rejected", failed.Reason);
        Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        // 恢复未因单次失败自动触发
        Assert.False(probe.Entered);
        // 失败动作绝不产生语义成功
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── 6.5 Falsifier F6：provider 失败不产生语义成功 ───────────────────

    [Fact]
    public async Task S2F6a_PerceptionUnknown_StateEvidenceRequired_ZeroDispatch()
    {
        // 感知 fail-closed：分类器无法判定 → SwitchState null → UNKNOWN
        var (agent, env, _, _) = Build(initialSwitchState: null);
        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "s2f6a");
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    [Fact]
    public async Task S2F6b_NoToggleCandidate_Unknown_ZeroDispatch()
    {
        // 感知未产出 toggle 类型候选（物理管线归一化失败场景）→ 无候选 → UNKNOWN
        var (agent, env, _, _) = Build(initialSwitchState: false, togglePerceptionType: "switch");
        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "s2f6b");
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── 6.6 幂等 / Unknown 变体：零物理分发 ─────────────────────────────

    [Fact]
    public async Task S2E6_IdempotentAlreadyOn_ZeroDispatch_DecisionOnly()
    {
        var (agent, env, _, _) = Build(initialSwitchState: true); // 世界已满足 → 幂等
        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "s2e6");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        // 决策即终止：未进入 capability 执行阶段
        Assert.DoesNotContain(agent.Trace, t => t.Reason == "semantic capability selected: SetEnabled");
    }
}
