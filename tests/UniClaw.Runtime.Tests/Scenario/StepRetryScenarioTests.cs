using UniClaw.Runtime.Model;
using UniClaw.Runtime.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// C5 SC-P2-002 Step-scope Retry 正式场景测试（scenarios/SC-P2-002-step-retry.md 契约）：
/// flicker-target 变体（C2 数据）+ harness maxRetries=1（B4 Traversal Step-scope retry）。
/// 预期观测流（掩码于 seq3/seq4 一次性生效）：seq1 Startup 后（SettingsMain）→ seq2 observeInitial →
/// seq3 Step-1 post-action（掩码：仅 "Bluetooth" — flicker 瞬间）→ Step-2 Select 无 "WiFi" 候选 →
/// 重试 re-observe（seq4，掩码：Bluetooth + WiFi）→ re-resolve 命中（WiFi Index=1）→ Tap(1) →
/// seq5 WiFiSettings → Step-3 SetSwitch(1,true) → seq6 WiFiSettingsOn → GoalEvidence Satisfied → Completed。
/// 全程无 Agent 介入：无 Trap、无恢复动作（I-8 对偶 — 能本地处理不升级）。
/// 断言键：SC-P2-002 证据 1-7（journal 重试条目 / 不中断 / 无 Trap / 无 Recovery / 有界 / 确定性 / ActionHistory）。
/// </summary>
public class StepRetryScenarioTests
{
    // ── 证据 1-5 / 7：重试 → 继续 → 完成；journal 重试条目可见；无 Trap / 无 Recovery ────────────────

    [Fact]
    public async Task FlickerRetry_ReObserveResolve_NoUpgrade_RunCompletes()
    {
        var harness = ScenarioHarness.Create("flicker-target", maxRetries: 1);

        var final = await harness.RunAsync();

        // ── 证据 2：Run 未中断 — Completed（重试成功后续跑，无升级）───────────────────────────────────
        Assert.Equal(RunState.Completed, final);
        Assert.Equal(RunState.Completed, harness.Agent.State);
        Assert.Equal("WiFi 开关已打开（观测 seq=6）。", harness.Agent.Reason);
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Completed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));

        // ── 证据 3/4：Trace 无 Trap 事件、无 Recovery 事件（Step-scope 内解决，不升级 Agent scope）──────
        Assert.DoesNotContain(harness.Agent.Trace, e => e.TrapKind is not null);
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RecoveryId is not null);
        Assert.Null(harness.Agent.LastTrap);

        // ── 证据 1：journal 含重试条目（Step-2：首次失败(0) + 重试命中(1) + Succeeded(1)）──────────────
        var journal = harness.Traversal.Journal;
        Assert.Equal(5, journal.Count); // Step-1 + Step-2×3（重试条目）+ Step-3
        var step1Entry = journal[0];
        Assert.Equal("Step-1", step1Entry.StepId);
        Assert.Equal(0, step1Entry.RetryCount);
        Assert.IsType<TraversalStepResult.Succeeded>(step1Entry.Result);

        var step2Entries = journal.Where(e => e.StepId == "Step-2").ToArray();
        Assert.Equal(3, step2Entries.Length);
        Assert.Equal(0, step2Entries[0].RetryCount); // 首次 Select 失败
        Assert.IsType<TraversalStepResult.Failed>(step2Entries[0].Result);
        Assert.Equal("目标「WiFi」在当前观测中无匹配候选（Select 无结果）。",
            Assert.IsType<TraversalStepResult.Failed>(step2Entries[0].Result).Reason);
        Assert.Null(step2Entries[0].DispatchedAction); // 重试期间零派发
        Assert.Null(step2Entries[0].PostActionObservation);
        Assert.Equal(1, step2Entries[1].RetryCount);   // 重试命中标记（re-observe 观测快照）
        Assert.Equal("目标「WiFi」第 1 次重试 re-observe 命中，继续执行。",
            Assert.IsType<TraversalStepResult.Failed>(step2Entries[1].Result).Reason);
        var retryObs = step2Entries[1].PostActionObservation
            ?? throw new InvalidOperationException("重试命中条目缺少 re-observe 观测（证据 1：序号可追溯）。");
        Assert.Equal(4, retryObs.SequenceNumber);
        Assert.Equal(1, step2Entries[2].RetryCount);   // 最终在第 1 次重试上成功
        Assert.IsType<TraversalStepResult.Succeeded>(step2Entries[2].Result);
        Assert.Equal(5, (step2Entries[2].PostActionObservation ?? throw new InvalidOperationException("成功条目缺少动作后观测。")).SequenceNumber);

        // ── 证据 5：重试有界 — 全部条目 RetryCount ≤ maxRetries(1)（确定性上限）────────────────────────
        Assert.All(journal, e => Assert.True(e.RetryCount <= 1, $"RetryCount 超过上限：{e.RetryCount}"));
        var step3Entry = journal[^1];
        Assert.Equal("Step-3", step3Entry.StepId);
        Assert.Equal(0, step3Entry.RetryCount);
        Assert.IsType<TraversalStepResult.Succeeded>(step3Entry.Result);

        // ── 证据 7：ActionHistory — WiFi Tap 的 Index=1（re-resolve 后的 grounding 位置，重试对动作透明）──
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication), // Startup LaunchApp
                new DeviceAction.Tap(0),                                       // Step-1 Tap(Network & Internet)
                new DeviceAction.Tap(1),                                       // Step-2 Tap(WiFi) — Index=1（重试观测）
                new DeviceAction.SetSwitch(1, true),                           // Step-3 SetSwitch(ON)
            },
            harness.Environment.ActionHistory.ToArray());

        // ── Trace 因果链（10 事件，无 Trap / Recovery / 恢复动作）────────────────────────────────────────
        var trace = harness.Agent.Trace.ToArray();
        Assert.Equal(10, trace.Length);
        var actionChain = trace.Where(e => e.ActionId is not null).Select(e => (Id: e.ActionId, Action: e.Action)).ToArray();
        Assert.Equal(3, actionChain.Length);
        Assert.Equal(("Action-1", (DeviceAction?)new DeviceAction.Tap(0)), actionChain[0]);
        Assert.Equal(("Action-2", (DeviceAction?)new DeviceAction.Tap(1)), actionChain[1]);
        Assert.Equal(("Action-3", (DeviceAction?)new DeviceAction.SetSwitch(1, true)), actionChain[2]);
        Assert.Equal(1, trace.Count(e => e.StepId == "Step-2")); // 重试条目仅存在于 journal，不产生额外 Trace 步骤事件

        // ── I-10：完成仍由证据评估驱动（flicker 后的 post-action 观测 seq5/6）─────────────────────────────
        Assert.Equal(new long?[] { 3, 5, 6 }, harness.Evidence.Select(e => e.SourceObservationSequence));
        Assert.False(harness.Evidence[0].Satisfied);
        Assert.False(harness.Evidence[1].Satisfied);
        Assert.True(harness.Evidence[2].Satisfied);
        Assert.Equal(harness.Evidence[2].Reason, harness.Agent.Reason);
    }

    // ── 证据 6：确定性重放 — 同输入 + 同 ScriptedEnvironment → 同重试次数 + 同结果 ──────────────────────

    [Fact]
    public async Task DeterministicReplay_TwoRuns_SameTrace_Journal_ActionHistory()
    {
        async Task<(ScenarioHarness Harness, RunState Final)> RunOnceAsync()
        {
            var harness = ScenarioHarness.Create("flicker-target", maxRetries: 1);
            var final = await harness.RunAsync();
            return (harness, final);
        }

        var (harnessA, finalA) = await RunOnceAsync();
        var (harnessB, finalB) = await RunOnceAsync();

        Assert.Equal(RunState.Completed, finalA);
        Assert.Equal(finalA, finalB);
        Assert.Equal(harnessA.Agent.Trace.ToArray(), harnessB.Agent.Trace.ToArray());
        Assert.Equal(harnessA.Environment.ActionHistory.ToArray(), harnessB.Environment.ActionHistory.ToArray());
        Assert.Equal(harnessA.Evidence.ToArray(), harnessB.Evidence.ToArray());
        // 同重试次数（重放确定性 — 证据 6）：journal 重试条目序列一致
        Assert.Equal(
            harnessA.Traversal.Journal.Select(e => e.RetryCount).ToArray(),
            harnessB.Traversal.Journal.Select(e => e.RetryCount).ToArray());
        Assert.Equal(harnessA.Agent.LastTrap, harnessB.Agent.LastTrap);
    }
}
