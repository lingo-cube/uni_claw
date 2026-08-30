using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B10 SC-P1-001 Normal WiFi Happy Path 正式场景测试（Golden Contract — specs/normal-wifi-scenario）。
/// 测试名以 SC-P1-001 Assertion ID 为键（断言 1-7）+ §34 组合 SHALL。
/// 纯断言：ScenarioHarness.Create("happy") → RunAsync() → harness 表面断言
/// （Agent.Trace / State / Reason / RecoveryAnchor + Environment.ActionHistory + Evidence）。
/// 预期动作顺序：LaunchApp → Tap(Network & Internet) → Tap(WiFi) → SetSwitch(开关, ON)；
/// 证据链：post-action 观测 seq 3/4/5，最终评估 Satisfied（I-10 — 证据来自 Observation，非 dispatch）。
/// </summary>
public class NormalWifiHappyPathTests
{
    // ── 断言 1：生命周期顺序（Trace RunState 转移 + 最终状态）──────────────────────────────────────────

    [Fact]
    public async Task Assertion1_LifecycleOrder_IdleInitializingRunningCompleted()
    {
        var harness = ScenarioHarness.Create("happy");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(RunState.Completed, harness.Agent.State);
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Completed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
    }

    // ── 断言 2：Startup 建立 RecoveryAnchor + Verify ForegroundApplication + LaunchApp ────────────────

    [Fact]
    public async Task Assertion2_Startup_RecoveryAnchorAndForegroundVerification()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        // RecoveryAnchor 已建立且三字段完整（§20 / 裁决 8）
        Assert.NotNull(harness.Agent.RecoveryAnchor);
        var anchor = harness.Agent.RecoveryAnchor!;
        Assert.Equal("Settings", anchor.ApplicationIdentity);
        Assert.Equal("SettingsMain", anchor.ExpectedSemanticEntry); // happy 变体启动后首屏
        Assert.False(string.IsNullOrWhiteSpace(anchor.VerificationCriteria));
        // Verify ForegroundApplication 通过的证据：LaunchApp 是唯一启动动作且 ApplicationId 正确
        var launch = Assert.IsType<DeviceAction.LaunchApp>(harness.Environment.ActionHistory[0]);
        Assert.Equal("Settings", launch.ApplicationId);
    }

    // ── 断言 3：每个动作后重新观测（§3）——post-action 观测序号单调递增 ────────────────────────────────

    [Fact]
    public async Task Assertion3_PostActionObservations_MonotonicIncreasingSequences()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        // CP-06：seq2 初始评估 1 次 + 3 步 → 3 次 post-action 证据评估（seq3 / seq4 / seq5）
        Assert.Equal(4, harness.Evidence.Count);
        Assert.Equal(new long?[] { 2, 3, 4, 5 }, harness.Evidence.Select(e => e.SourceObservationSequence));
        // 严格单调递增（断言 3：SequenceNumber 单调递增 — 裁决 6）
        Assert.True(
            harness.Evidence.Zip(harness.Evidence.Skip(1), (earlier, later) => later.SourceObservationSequence > earlier.SourceObservationSequence)
                .All(monotonic => monotonic));
    }

    // ── 断言 4：GoalEvidence —— 最终评估 Satisfied，证据引用最终 post-action 观测 ──────────────────────

    [Fact]
    public async Task Assertion4_GoalEvidence_SatisfiedOnFinalPostActionObservation()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var finalEvidence = harness.Evidence[^1];
        Assert.True(finalEvidence.Satisfied);
        Assert.Equal(5, finalEvidence.SourceObservationSequence); // 最终 post-action 观测 seq
        Assert.False(string.IsNullOrWhiteSpace(finalEvidence.Reason));
        Assert.Contains("开关", finalEvidence.Reason, StringComparison.Ordinal); // 原因提及开关状态
    }

    // ── 断言 5：Completed 事件在 dispatch 事件与 post-action 评估之后（dispatch ≠ completed）───────────

    [Fact]
    public async Task Assertion5_CompletedEvent_AfterLastDispatchAndEvaluation()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var trace = harness.Agent.Trace.ToArray();
        var lastActionIndex = Array.FindLastIndex(trace, e => e.Action is not null);
        var completedIndex = Array.FindIndex(trace, e => e.RunState == RunState.Completed);
        Assert.True(completedIndex > lastActionIndex, "Completed 事件必须位于最后一个动作分发事件之后（SC-P1-003 断言 5）。");
        Assert.Equal(RunState.Completed, harness.Agent.State);
        Assert.False(string.IsNullOrWhiteSpace(trace[completedIndex].Reason)); // 完成原因显式记录
    }

    // ── 断言 6：Trace 因果链（RunId / ContainerId / StepId / ActionId + 动作载荷）──────────────────────

    [Fact]
    public async Task Assertion6_TraceCausalChain_EventOrderAndActionPayloads()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var trace = harness.Agent.Trace.ToArray();
        // RunId 一致（因果链第一环）
        Assert.All(trace, e => Assert.Equal(ScenarioHarness.DefaultRunId, e.RunId));
        // §34 事件顺序签名（ContainerId, StepId, ActionId）：
        // bind Settings → Step-1 → navigate Network → Step-2 → navigate WiFi → Step-3 → Completed
        Assert.Equal(
            new (string? ContainerId, string? StepId, string? ActionId)[]
            {
                (null, null, null),              // Idle
                (null, null, null),              // Initializing
                (null, null, null),              // Running
                ("SettingsMain", null, null),    // bind 初始容器
                ("SettingsMain", "Step-1", "Action-1"),
                ("NetworkSettings", null, null), // navigate
                ("NetworkSettings", "Step-2", "Action-2"),
                ("WiFiSettings", null, null),    // navigate
                ("WiFiSettings", "Step-3", "Action-3"),
                (null, null, null),              // Completed
            },
            trace.Select(e => (e.ContainerId, e.StepId, e.ActionId)));
        // StepId / ActionId 按执行序配对（Step-1/2/3，Action-1/2/3）
        Assert.Equal(new[] { "Step-1", "Step-2", "Step-3" }, trace.Where(e => e.StepId is not null).Select(e => e.StepId));
        Assert.Equal(new[] { "Action-1", "Action-2", "Action-3" }, trace.Where(e => e.ActionId is not null).Select(e => e.ActionId));
        // 动作顺序：LaunchApp → Tap → Tap → SetSwitch(ON)（SC-P1-001 Expected action order）
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
                new DeviceAction.SetSwitch(1, true),
            },
            harness.Environment.ActionHistory);
        // Step-3 动作是 SetSwitch 且 TargetState == true（开关 ON — SC-P1-001 Completion evidence）
        var step3 = Assert.Single(trace.Where(e => e.StepId == "Step-3"));
        var setSwitch = Assert.IsType<DeviceAction.SetSwitch>(step3.Action);
        Assert.True(setSwitch.TargetState);
    }

    // ── 断言 7：确定性重放（同 runId → 完全相同事件序列）──────────────────────────────────────────────

    [Fact]
    public async Task Assertion7_DeterministicReplay_SameRunIdIdenticalTraceTuples()
    {
        async Task<DecisionRecord[]> RunAsync()
        {
            var harness = ScenarioHarness.Create("happy");
            await harness.RunAsync();
            return harness.Agent.Trace.ToArray();
        }

        // 全字段元组投影（推断类型）逐条相等，含动作载荷（SC-P1-001 断言 7：确定性、可重放）
        var traceA = (await RunAsync()).Select(e => (e.RunId, e.ContainerId, e.StepId, e.ActionId, e.Action, e.Reason, e.RunState)).ToArray();
        var traceB = (await RunAsync()).Select(e => (e.RunId, e.ContainerId, e.StepId, e.ActionId, e.Action, e.Reason, e.RunState)).ToArray();

        Assert.Equal(traceA, traceB);
    }

    // ── §34 完整生命周期 SHALL：断言 1 / 2 / 6 的组合证明 ──────────────────────────────────────────────

    [Fact]
    public async Task SHALL_34_FullLifecycleSequence_ProvenByAssertions1236()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        // §34 期望生命周期逐条对应：Idle → Initializing（Startup §19）→ Running
        // （bind → traverse → navigate ×2 → traverse）→ Completed（I-10 GoalEvidence）
        Assert.Equal(
            new (RunState? RunState, string? ContainerId, string? StepId, string? ActionId)[]
            {
                (RunState.Idle, null, null, null),
                (RunState.Initializing, null, null, null),
                (RunState.Running, null, null, null),
                (null, "SettingsMain", null, null),     // bind Settings Container
                (null, "SettingsMain", "Step-1", "Action-1"), // traverse Tap(Network & Internet)
                (null, "NetworkSettings", null, null),  // navigate
                (null, "NetworkSettings", "Step-2", "Action-2"), // traverse Tap(WiFi)
                (null, "WiFiSettings", null, null),     // navigate
                (null, "WiFiSettings", "Step-3", "Action-3"), // traverse SetSwitch(ON)
                (RunState.Completed, null, null, null), // Completed（仅 Satisfied GoalEvidence — I-10）
            },
            harness.Agent.Trace.Select(e => (e.RunState, e.ContainerId, e.StepId, e.ActionId)));
        // 组合证明的其余环节（断言 2：anchor 建立；断言 3：post-action 观测推进）
        Assert.NotNull(harness.Agent.RecoveryAnchor);
        Assert.Equal(new long?[] { 2, 3, 4, 5 }, harness.Evidence.Select(e => e.SourceObservationSequence)); // CP-06：seq2 初始评估在前
    }
}
