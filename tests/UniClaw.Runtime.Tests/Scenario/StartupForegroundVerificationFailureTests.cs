using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B11 SC-P1-002 Startup Foreground Verification 失败 正式场景测试
/// （scenarios/catalog.md SC-P1-002 — 可信 Runtime 尚未建立时不得进入正式执行 §19）。
/// 测试名以 SC-P1-002 Assertion ID 为键（断言 1-6）。
/// 纯断言：ScenarioHarness.Create("startup-fg-fail") → RunAsync() → harness 表面断言。
/// 预期：LaunchApp 后前台仍为 "Launcher"（≠ 目标应用）→ Startup 报告 NotReady(显式原因)
/// → Agent 判定 Failed：从未进入 Running、RecoveryAnchor 未建立、无恢复动作（action history 仅 [LaunchApp]）。
/// </summary>
public class StartupForegroundVerificationFailureTests
{
    // ── 断言 1：RunState 从未进入 Running（Trace 无 Running 转移事件）──────────────────────────────────

    [Fact]
    public async Task Assertion1_NeverEntersRunning_TransitionsEndAtFailed()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        await harness.RunAsync();

        // 全部 RunState 转移事件：Idle → Initializing → Failed（无 Running — 断言 1）
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Failed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
        // 显式断言：Trace 中不存在 Running 转移事件
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Running);
    }

    // ── 断言 2：StartupResult == NotReady(显式原因)，原因记录于 Trace ──────────────────────────────────

    [Fact]
    public async Task Assertion2_NotReady_ExplicitForegroundVerificationReasonInTrace()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        await harness.RunAsync();

        // Agent.Reason 非空且与前台验证失败相关（Startup.cs NotReady 文案：观测到「Launcher」，期望「Settings」）
        Assert.NotNull(harness.Agent.Reason);
        var reason = harness.Agent.Reason!;
        Assert.Contains("ForegroundApplication 验证失败", reason, StringComparison.Ordinal);
        Assert.Contains("Settings", reason, StringComparison.Ordinal); // 期望目标应用
        Assert.Contains("Launcher", reason, StringComparison.Ordinal); // 实际观测到的前台
        // 原因已记录于 Trace：最终 Failed 事件的 Reason 与 Agent.Reason 一致（NotReady → Run 失败原因传播）
        var finalEvent = harness.Agent.Trace[^1];
        Assert.Equal(reason, finalEvent.Reason);
    }

    // ── 断言 3：RecoveryAnchor 未建立（Agent 无 anchor）───────────────────────────────────────────────

    [Fact]
    public async Task Assertion3_RecoveryAnchor_NotEstablished()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        await harness.RunAsync();

        // Startup 未 Ready → §20 anchor 从未建立
        Assert.Null(harness.Agent.RecoveryAnchor);
    }

    // ── 断言 4：RunState 最终 == Failed（Agent 是 Run 终止 authority）─────────────────────────────────

    [Fact]
    public async Task Assertion4_FinalState_FailedWithExplicitReason()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(RunState.Failed, harness.Agent.State);
        // 最终 Trace 事件是 Failed 且带显式原因（DecisionRecord.Reason 非空）
        var finalEvent = harness.Agent.Trace[^1];
        Assert.Equal(RunState.Failed, finalEvent.RunState);
        Assert.False(string.IsNullOrWhiteSpace(finalEvent.Reason));
    }

    // ── 断言 5：action history 仅含 [LaunchApp]（无恢复动作）──────────────────────────────────────────

    [Fact]
    public async Task Assertion5_ActionHistory_OnlyLaunchApp_NoRecoveryActions()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        await harness.RunAsync();

        // 恰好一个动作（count == 1 即证明无 PressBack / relaunch / retry 等恢复执行）
        var launch = Assert.IsType<DeviceAction.LaunchApp>(Assert.Single(harness.Environment.ActionHistory));
        Assert.Equal(ScenarioHarness.TargetApplication, launch.ApplicationId);
    }

    // ── 断言 6：无 Container 绑定、无 Traversal 执行（Trace 无 Container / Step / Action 事件）─────────

    [Fact]
    public async Task Assertion6_NoContainerBind_NoTraversalExecution_NoEvidence()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        await harness.RunAsync();

        // Trace 中不存在任何携带 ContainerId / StepId / ActionId / 动作载荷的事件
        Assert.DoesNotContain(harness.Agent.Trace, e => e.ContainerId is not null);
        Assert.DoesNotContain(harness.Agent.Trace, e => e.StepId is not null);
        Assert.DoesNotContain(harness.Agent.Trace, e => e.ActionId is not null);
        Assert.DoesNotContain(harness.Agent.Trace, e => e.Action is not null);
        // evidence evaluator 从未到达（Running 未进入 → 无 post-action Observation 评估）
        Assert.Empty(harness.Evidence);
    }
}
