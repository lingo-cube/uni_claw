using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// C6 SC-P2-003 Recovery Verification Failure 正式场景测试（scenarios/SC-P2-003-recovery-verification-failure.md 契约）：
/// unrecoverable 变体（C3 数据）+ 恢复接线 harness（与 launcher-drift 相同 — 验证失败由变体数据驱动）。
/// 预期观测流（掩码于 seq4/seq5 一次性生效）：seq1 Startup 后（SettingsMain）→ seq2 observeInitial →
/// seq3 Step-1 post-action（NetworkSettings）→ seq4 Step-2 post-action（mask：Launcher + 不可解析元素 → drift，
/// Trap Expected=3/Observed=4）→ 恢复：Begin → LaunchApp(Settings)（Dispatched，但 seq5 mask 显示未恢复：
/// 仍 Launcher + 不可解析元素 — dispatch ≠ world success，裁决 10 / I-9）→ Recovery.Verify 判据失败 →
/// RunState=Failed（显式原因），无 Resume、无恢复后 Plan 步骤。
/// 预期动作链：[LaunchApp(Settings)（Startup）, Tap(0)（Step-1）, Tap(0)（Step-2，其 post-action 揭示 drift）,
/// LaunchApp(Settings)（恢复配方 Relaunch）] — 恢复 LaunchApp 之后零动作（SC-P2-003 证据 4）。
/// 断言键：SC-P2-003 证据 1-7（验证失败事件 / Failed 终态 / 无 Resume / 无恢复后动作 / 显式 Reason /
/// 无盲 Resume / 确定性重放）。
/// </summary>
public class RecoveryVerificationFailureTests
{
    private const string VerifyFailReason =
        "恢复验证失败：期望 [ForegroundApplication == Settings]，实际 Foreground=[Launcher], page=[Launcher]（seq=5）";

    // ── 证据 1-6：drift → Trap → 恢复动作 → 验证失败 → Run Failed（不得 Resume）────────────────────────

    [Fact]
    public async Task VerifyFailure_DriftTrap_RestoreDispatched_ButRunFailed_NoResume()
    {
        var harness = ScenarioHarness.Create("unrecoverable");

        var final = await harness.RunAsync();

        // ── 证据 2：RunState = Failed（非 Completed）────────────────────────────────────────────────────
        Assert.Equal(RunState.Failed, final);
        Assert.Equal(RunState.Failed, harness.Agent.State);
        var failEvent = Assert.Single(harness.Agent.Trace, e => e.RunState == RunState.Failed);
        Assert.Equal("Step-2", failEvent.StepId);         // 失败来源 = 挂起步骤
        Assert.Equal("NetworkSettings", failEvent.ContainerId);
        // ── 证据 5：Reason 显式，语义源自恢复验证失败（非 Plan 耗尽 / 步骤失败原因）────────────────────
        Assert.Equal(VerifyFailReason, harness.Agent.Reason);
        Assert.Equal(VerifyFailReason, failEvent.Reason);

        // ── 证据 1：Trace 含验证失败事件（RecoveryId 关联的 Verify → Failed，Reason 含期望与实际）────────
        var verifyFailEvent = Assert.Single(harness.Agent.Trace,
            e => e.Reason is not null && e.Reason.StartsWith("recovery verify: 恢复验证失败：", StringComparison.Ordinal));
        Assert.Equal("Recovery-1", verifyFailEvent.RecoveryId);
        Assert.Contains("期望 [ForegroundApplication == Settings]", verifyFailEvent.Reason);
        Assert.Contains("实际 Foreground=[Launcher], page=[Launcher]（seq=5）", verifyFailEvent.Reason);

        // ── drift → Trap（与 SC-P2-001 相同的检测路径）──────────────────────────────────────────────────
        var trap = harness.Agent.LastTrap ?? throw new InvalidOperationException("LastTrap 为 null：drift Run 未发射 Trap。");
        Assert.Equal(TrapKind.UnexpectedPage, trap.Kind);
        Assert.Equal(TrapScope.Agent, trap.Scope);
        Assert.Equal(3, trap.Expected);   // 容器绑定观测 seq（Step-1 post-action = NetworkSettings）
        Assert.Equal(4, trap.Observed);   // drift 观测 seq（Step-2 post-action 掩码）
        Assert.Equal(new DeviceAction.Tap(0), trap.LastAction);
        var trapEvent = Assert.Single(harness.Agent.Trace, e => e.TrapKind is not null);
        Assert.Equal("Step-2", trapEvent.StepId);
        Assert.Equal("NetworkSettings", trapEvent.ContainerId);

        // ── 证据 4：ActionHistory — 启动 → Step-1 → Step-2（drift 步骤，动作先于观测揭示 drift）→ 恢复 LaunchApp
        //    之后零动作（无 Tap(WiFi) / SetSwitch(ON) 等恢复后计划动作 — 证据 4 不变量）
        //    注：规范证据 4 的示意序列省略了 Step-2 的 Tap(WiFi)（恢复前的原计划动作 —
        //    §3 协议：动作先执行、post-action 观测揭示 drift）；断言的不变量 = 恢复动作后无任何后续动作
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication), // Startup LaunchApp
                new DeviceAction.Tap(0),                                       // Step-1 Tap(Network & Internet)
                new DeviceAction.Tap(0),                                       // Step-2 Tap(WiFi)（post-action 揭示 drift）
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication), // 恢复配方 Relaunch(Settings)（Dispatched）
            },
            harness.Environment.ActionHistory.ToArray());
        // 恢复 LaunchApp 之后零动作（证据 4 — 数组相等断言已隐含长度 4）

        // ── 证据 3/6：无 Resume —— 无恢复后续事件（无 observe 之后的 rebind/position-restore/resume）、
        //    无恢复后 Plan 步骤（无 Action-3）────────────────────────────────────────────────────────────
        var recoveryEvents = harness.Agent.Trace.Where(e => e.RecoveryId is not null).ToArray();
        Assert.Equal(3, recoveryEvents.Length); // 配方动作 + 恢复 observe + 验证失败（无 rebind / 无位置恢复 / 无 resume）
        Assert.All(recoveryEvents, e => Assert.Equal("Recovery-1", e.RecoveryId));
        Assert.Single(recoveryEvents, e => e.Action is DeviceAction.LaunchApp);  // 恢复动作已分发（证据：dispatch 发生）
        Assert.Single(recoveryEvents, e => e.Reason == "recovery observe (seq=5)"); // 证据 3（C6）：恢复后观测仍 Launcher —
                                                                                    // 由验证失败原因中的实际值证明
        Assert.DoesNotContain(harness.Agent.Trace, e => e.Reason is not null && e.Reason.Contains("recovery resume", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Agent.Trace, e => e.ActionId == "Action-3");
        Assert.DoesNotContain(harness.Agent.Trace, e => e.Reason == "recovery verify: VERIFIED");

        // ── 证据 1 补充：验证失败前两次证据评估（CP-06 seq2 初始评估 + Step-1 post-action）──
        Assert.Equal(2, harness.Evidence.Count);
        Assert.All(harness.Evidence, evidence => Assert.False(evidence.Satisfied));
        Assert.Equal(3, harness.Evidence[^1].SourceObservationSequence);

        // ── Trace 因果链（12 事件：Drift → Trap → Recovery(动作/observe/verify) → Failed）─────────────
        var trace = harness.Agent.Trace.ToArray();
        Assert.Equal(12, trace.Length);
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Failed },
            trace.Where(e => e.RunState is not null).Select(e => e.RunState));
        int trapIndex = Array.FindIndex(trace, e => e.TrapKind is not null);
        int recoveryStartIndex = Array.FindIndex(trace, e => e.RecoveryId is not null);
        int verifyFailIndex = Array.FindIndex(trace, e => e.Reason is not null && e.Reason.StartsWith("recovery verify: 恢复验证失败：", StringComparison.Ordinal));
        int failedIndex = Array.FindIndex(trace, e => e.RunState == RunState.Failed);
        Assert.True(trapIndex < recoveryStartIndex, "Trap 发射必须先于恢复会话。");
        Assert.True(recoveryStartIndex < verifyFailIndex, "恢复动作分发必须先于恢复验证（dispatch ≠ success — I-9）。");
        Assert.True(verifyFailIndex < failedIndex, "验证失败必须先于 Run 终止。");
        Assert.Equal(trace.Length - 1, failedIndex); // Failed 是终态事件（之后无任何事件 — 无 Resume）
        var actionChain = trace.Where(e => e.ActionId is not null).Select(e => e.ActionId).ToArray();
        Assert.Equal(new string?[] { "Action-1", "Action-2" }, actionChain); // 无恢复后 Plan 步骤（证据 3/4）
    }

    // ── 证据 7：确定性重放 — 同 runId 同输入 → 同验证失败 Trace ─────────────────────────────────────────

    [Fact]
    public async Task DeterministicReplay_TwoRuns_SameVerifyFailureTrace()
    {
        async Task<(ScenarioHarness Harness, RunState Final)> RunOnceAsync()
        {
            var harness = ScenarioHarness.Create("unrecoverable");
            var final = await harness.RunAsync();
            return (harness, final);
        }

        var (harnessA, finalA) = await RunOnceAsync();
        var (harnessB, finalB) = await RunOnceAsync();

        Assert.Equal(RunState.Failed, finalA);
        Assert.Equal(finalA, finalB);
        Assert.Equal(harnessA.Agent.Reason, harnessB.Agent.Reason); // 验证失败原因字节级一致
        Assert.Equal(harnessA.Agent.Trace.ToArray(), harnessB.Agent.Trace.ToArray());
        Assert.Equal(harnessA.Environment.ActionHistory.ToArray(), harnessB.Environment.ActionHistory.ToArray());
        Assert.Equal(harnessA.Evidence.ToArray(), harnessB.Evidence.ToArray());
        Assert.Equal(harnessA.Agent.LastTrap, harnessB.Agent.LastTrap);
    }
}
