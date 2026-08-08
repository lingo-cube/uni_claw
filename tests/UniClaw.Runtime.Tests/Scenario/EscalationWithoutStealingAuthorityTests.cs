using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B13 SC-P1-004 Escalation Without Stealing Authority 正式场景测试
/// （scenarios/catalog.md SC-P1-004 — I-8 的 escalate 半句；recovery 半句属 Phase 2，裁决 4；
/// 不引入 Trap / TrapKind / TrapScope / RecoveryRequest / Recovery Runtime）。
/// 测试名以 SC-P1-004 Assertion ID 为键（断言 1-4）。
/// 纯断言：ScenarioHarness.Create("missing-target") → RunAsync() → harness 表面断言。
/// 预期：Step-2 目标 "WiFi" 在 Network Settings（仅 "Bluetooth"）无候选 → Traversal 返回
/// TraversalStepResult.Failed(非空原因)（结构化结果、零动作分发 — Traversal.cs Check 阶段）→
/// Container 只读转交 → Agent（最终 failure authority）判定 RunState → Failed（StepId + 显式原因；
/// 无恢复动作 — Expected action order: LaunchApp → Tap(Network & Internet)）。
/// </summary>
public class EscalationWithoutStealingAuthorityTests
{
    // ── 断言 1：步骤失败的结构化结果记录于 Trace（StepId + 非空原因；失败步零动作分发）──────────────

    [Fact]
    public async Task Assertion1_StepFailure_StructuredResultInTrace_WithStepIdAndReason()
    {
        var harness = ScenarioHarness.Create("missing-target");

        await harness.RunAsync();

        var trace = harness.Agent.Trace.ToArray();
        // 唯一 Failed 事件携带失败步 Step-2 与非空原因（结构化结果 — §45 / TraversalStepResult.Failed）
        var failedEvent = Assert.Single(trace.Where(e => e.RunState == RunState.Failed));
        Assert.Equal("Step-2", failedEvent.StepId);
        Assert.NotNull(failedEvent.Reason);
        Assert.Contains("无匹配候选", failedEvent.Reason!, StringComparison.Ordinal); // Select 失败原因（Traversal.cs）
        // Step-2 在 dispatch 之前失败：Trace 中无任何 Step-2 动作载荷事件（Select 无候选 → 零动作分发）
        Assert.DoesNotContain(trace, e => e.StepId == "Step-2" && e.Action is not null);
        // 唯一带动作载荷的事件是 Step-1 的 Tap（动作只在成功步分发）
        var actionEvent = Assert.Single(trace.Where(e => e.Action is not null));
        Assert.Equal("Step-1", actionEvent.StepId);
    }

    // ── 断言 2：最终 Failed + 显式原因（语义：目标无候选）；无 Completed 事件 ─────────────────────────

    [Fact]
    public async Task Assertion2_FinalFailed_ExplicitReason_NoCompleted()
    {
        var harness = ScenarioHarness.Create("missing-target");

        await harness.RunAsync();

        Assert.Equal(RunState.Failed, harness.Agent.State);
        Assert.NotNull(harness.Agent.Reason);
        // 显式原因语义：目标 "WiFi" 在当前观测（Network Settings）无匹配候选
        Assert.Contains("WiFi", harness.Agent.Reason!, StringComparison.Ordinal);
        Assert.Contains("无匹配候选", harness.Agent.Reason!, StringComparison.Ordinal);
        // 失败路径绝不产出 Completed（无 Satisfied 证据；Plan 提前终止 — I-10）
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Completed);
    }

    // ── 断言 3：无恢复动作（action history 仅计划动作）───────────────────────────────────────────────

    [Fact]
    public async Task Assertion3_NoRecovery_ActionHistoryOnlyPlannedLaunchAndTap()
    {
        var harness = ScenarioHarness.Create("missing-target");

        await harness.RunAsync();

        // 恰好 2 个动作：LaunchApp + Step-1 Tap(Network & Internet)（SC-P1-004 Expected action order）；
        // Step-2 未分发（Select 失败）；无 PressBack / 重新 Launch / 重试（断言 3 — 无恢复动作）
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
            },
            harness.Environment.ActionHistory);
    }

    // ── 断言 4：Agent 是唯一 Run 终止 authority（行为面组合证明 — 断言 2 + 3 合并）───────────────────

    [Fact]
    public async Task Assertion4_AgentIsSoleTerminationAuthority_EscalationStopsAtAgent()
    {
        var harness = ScenarioHarness.Create("missing-target");

        await harness.RunAsync();

        var trace = harness.Agent.Trace.ToArray();
        // (a) 终止 authority：唯一 Failed 转移事件，且是 Trace 最后一个事件
        //     （Agent 判定终止后无任何后续转移 / 动作 — 终止决定没有再往下游走）
        var failedIndex = Array.FindIndex(trace, e => e.RunState == RunState.Failed);
        Assert.True(failedIndex >= 0, "Trace 必须包含 Failed 转移事件。");
        Assert.Equal(trace.Length - 1, failedIndex);
        // (b) 不 steal：失败前无恢复执行（断言 3 — ActionHistory 仅计划动作），
        //     失败步 StepId 来自 Traversal journal（escalate 表面 — 结构化结果），
        //     RunState=Failed 判定（Run 去向）唯一由 Agent 发出（I-8：lower scope 可 escalate，不得 steal）
        var failedEvent = trace[failedIndex];
        Assert.Equal("Step-2", failedEvent.StepId);
        Assert.NotNull(failedEvent.Reason);
        // (c) evaluator 在失败步后未被调用（Agent 在 Failed 上短路返回 — 只评估过 Step-1 后的一次 post-action 观测）
        Assert.Single(harness.Evidence);
    }
}
