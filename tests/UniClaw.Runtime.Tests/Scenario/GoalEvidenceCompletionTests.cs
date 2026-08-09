using System;
using System.Linq;
using System.Threading;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B12 SC-P1-003 Goal Evidence Completion 正式场景测试（scenarios/catalog.md SC-P1-003 — I-10 / §43：
/// Plan exhausted ≠ Completed；Action Dispatched ≠ Completed；只有 Goal evaluator 从 post-action Observation
/// 产生 explicit Goal Evidence 才能 Completed）。
/// 测试名以 SC-P1-003 Assertion ID 为键（正向断言 1a / 1b / 2 / 3 + 负向断言 4 / 5）。
/// 记录式 evaluator（本文件私有 helper）：捕获每个 post-action Observation + 快照评估时 Agent.State，
/// 委托 ScenarioGoals.EvaluateWifiSwitchEvidence，写入 harness.Evidence；
/// 经 harness.Agent.RunAsync(goal, harness.Plan, harness.RunId) 直接注入（不调用 harness.RunAsync — B12 注入点）。
/// 正向变体 happy：SetSwitch(ON) 后开关 true → 证据 Satisfied → Completed；
/// 负向变体 switch-stuck：SetSwitch(ON) 返回 Dispatched 但世界不变（裁决 10 — dispatch ≠ world success）
/// → 诚实 evaluator 不满足 → Plan 耗尽 → Failed（显式原因；不是 Completed）。
/// </summary>
public class GoalEvidenceCompletionTests
{
    // ── 断言 1a：dispatch ≠ completed（时序：Completed 事件位于最后 dispatch 事件之后）──────────────────

    [Fact]
    public async Task Assertion1a_DispatchIsNotCompletion_CompletedEventAfterSetSwitchDispatch()
    {
        var harness = ScenarioHarness.Create("happy");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        // Completed 事件索引必须大于最后一个动作分发事件索引（SC-P1-003 断言 1 — dispatch ≠ completed）
        var lastActionIndex = Array.FindLastIndex(run.Trace, e => e.Action is not null);
        var completedIndex = Array.FindIndex(run.Trace, e => e.RunState == RunState.Completed);
        Assert.True(completedIndex > lastActionIndex, "Completed 事件必须位于最后一个动作分发事件之后（dispatch ≠ completed — SC-P1-003 断言 1）。");
        // 最后一个 Action 事件是 Step-3 的 SetSwitch 且载荷非空（dispatch 真实发生）
        var setSwitchEvent = run.Trace[lastActionIndex];
        Assert.NotNull(setSwitchEvent.Action);
        var setSwitch = Assert.IsType<DeviceAction.SetSwitch>(setSwitchEvent.Action);
        Assert.True(setSwitch.TargetState); // SetSwitch(ON) 是期望开关状态
    }

    // ── 断言 1b：Completed 在 post-action 评估之后（评估期间 Agent.State 全为 Running）──────────────────

    [Fact]
    public async Task Assertion1b_CompletedAfterPostActionEvaluation_EvaluatorSawRunningOnly()
    {
        var harness = ScenarioHarness.Create("happy");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        // 每次评估发生时 Run 都仍在 Running → Completed 判定发生在评估之后（post-action Observation 评估 → 之后才 Completed）
        Assert.Equal(4, run.StateSnapshots.Length); // CP-06：seq2 初始评估 1 次 + 3 个 post-action Observation → 共 4 次评估
        Assert.All(run.StateSnapshots, state => Assert.Equal(RunState.Running, state));
        Assert.Equal(RunState.Completed, run.FinalState);
    }

    // ── 断言 2：GoalEvidence.SourceObservationSequence == post-action Observation 序号（证据来自观察）───

    [Fact]
    public async Task Assertion2_EvidenceSequence_MatchesCapturedPostActionObservation()
    {
        var harness = ScenarioHarness.Create("happy");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        // 捕获的观测即 evaluator 实际收到的 Observation（CP-06：seq2 初始观测 + 每次 post-action 各一个）
        Assert.Equal(4, harness.Evidence.Count);
        Assert.Equal(run.Captured.Length, harness.Evidence.Count);
        // 每个证据引用的观测序号 == 该次评估实际收到的 Observation 序号（证据来自观察，不是 dispatch 结果）
        for (var i = 0; i < harness.Evidence.Count; i++)
        {
            Assert.Equal(run.Captured[i].SequenceNumber, harness.Evidence[i].SourceObservationSequence);
        }
        // 捕获观测序号严格单调递增（seq 2/3/4/5 — CP-06 初始观测 + post-action 观测推进，裁决 6）
        Assert.Equal(new long[] { 2, 3, 4, 5 }, run.Captured.Select(o => o.SequenceNumber));
        Assert.True(
            run.Captured.Zip(run.Captured.Skip(1), (earlier, later) => later.SequenceNumber > earlier.SequenceNumber)
                .All(monotonic => monotonic));
    }

    // ── 断言 3：完成原因记录于 Trace（GoalEvidence.Reason）────────────────────────────────────────────

    [Fact]
    public async Task Assertion3_CompletionReason_RecordedInTrace()
    {
        var harness = ScenarioHarness.Create("happy");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        // 恰好一个 Completed 事件；其 Reason == 最终 GoalEvidence 的 Reason（Agent 完成原因 = 证据原因）
        var completedEvent = Assert.Single(run.Trace.Where(e => e.RunState == RunState.Completed));
        Assert.False(string.IsNullOrWhiteSpace(completedEvent.Reason));
        Assert.Equal(harness.Evidence[^1].Reason, completedEvent.Reason);
    }

    // ── 断言 4（负向）：Plan 步数耗尽 + 证据不满足 → RunState 最终 == Failed（不是 Completed）──────────

    [Fact]
    public async Task Assertion4_Negative_PlanExhausted_UnSatisfied_FailedNotCompleted()
    {
        var harness = ScenarioHarness.Create("switch-stuck");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        Assert.Equal(RunState.Failed, run.FinalState);
        Assert.Equal(RunState.Failed, harness.Agent.State);
        // Trace 中不存在 Completed 事件（Plan 耗尽 ≠ Completed — I-10）
        Assert.DoesNotContain(run.Trace, e => e.RunState == RunState.Completed);
        // 最终证据：不满足，且引用最终 post-action Observation 序号（开关仍 false — 世界不变）
        Assert.False(harness.Evidence[^1].Satisfied);
        Assert.Equal(run.Captured[^1].SequenceNumber, harness.Evidence[^1].SourceObservationSequence);
    }

    // ── 断言 5（负向）：失败原因显式记录；无任何恢复动作（action history 仅计划动作）────────────────────

    [Fact]
    public async Task Assertion5_Negative_ExplicitFailureReason_NoRecoveryActions()
    {
        var harness = ScenarioHarness.Create("switch-stuck");

        var run = await RunWithRecordingEvaluatorAsync(harness);

        // 显式失败原因记录于 Trace（Plan 耗尽统一文案 — Agent.cs；不是静默 / 无原因）
        var failedIndex = Array.FindIndex(run.Trace, e => e.RunState == RunState.Failed);
        Assert.True(failedIndex >= 0, "Trace 必须包含 Failed 事件。");
        var failedEvent = run.Trace[failedIndex];
        Assert.NotNull(failedEvent.Reason);
        Assert.Contains("Plan 步数耗尽", failedEvent.Reason!, StringComparison.Ordinal);
        // action history == 4 个计划动作（LaunchApp + Tap + Tap + SetSwitch(ON)）：全部计划动作，
        // 无任何恢复 / 重试动作（SC-P1-003 断言 5 — 无额外动作）
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
                new DeviceAction.SetSwitch(1, true),
            },
            harness.Environment.ActionHistory);
    }

    /// <summary>
    /// 记录式 evaluator 执行路径（B12 核心 helper）：把每个 post-action Observation 与评估时的 Agent.State
    /// 快照捕获下来，证据评估委托给 ScenarioGoals.EvaluateWifiSwitchEvidence 并写入 harness.Evidence。
    /// 通过 harness.Agent.RunAsync 直接注入（Agent 每次 post-action Observation 后调用 evaluator — Agent.cs）。
    /// </summary>
    /// <param name="harness">已装配的 ScenarioHarness（其 Agent / Plan / RunId / Evidence 为执行与观察面）。</param>
    /// <returns>执行结果：最终 RunState + Trace 快照 + 捕获的 Observation / State 快照序列。</returns>
    private static async Task<RecordingRun> RunWithRecordingEvaluatorAsync(ScenarioHarness harness)
    {
        var captured = new List<Observation>();
        var stateSnapshots = new List<RunState>();
        var goal = new Goal(observation =>
        {
            captured.Add(observation);
            stateSnapshots.Add(harness.Agent.State);
            var evidence = ScenarioGoals.EvaluateWifiSwitchEvidence(observation);
            harness.Evidence.Add(evidence);
            return evidence;
        });

        var finalState = await harness.Agent.RunAsync(goal, harness.Plan, harness.RunId, CancellationToken.None);
        return new RecordingRun(
            finalState,
            harness.Agent.Trace.ToArray(),
            captured.ToArray(),
            stateSnapshots.ToArray());
    }

    /// <summary>记录式执行的结果载体：最终状态 + Trace 快照 + evaluator 捕获的 Observation / State 序列。</summary>
    /// <param name="FinalState">Agent.RunAsync 返回的最终 RunState。</param>
    /// <param name="Trace">run 结束后的 Trace 快照（Agent.Trace 是活后备列表 — 必须在 run 后快照）。</param>
    /// <param name="Captured">evaluator 收到的 post-action Observation 序列（按评估顺序）。</param>
    /// <param name="StateSnapshots">每次评估时快照的 Agent.State（证明评估发生在 Running 期间）。</param>
    private sealed record RecordingRun(
        RunState FinalState,
        TraceEvent[] Trace,
        Observation[] Captured,
        RunState[] StateSnapshots);

    // ── CP-06 断言 6（正向）：空 Plan + 初始 Observation 已满足 Goal → 无需 dispatch 即可 Completed ──

    [Fact]
    public async Task Assertion6_InitialGoalSatisfied_CompletesWithoutPlanStepDispatch()
    {
        var harness = ScenarioHarness.Create("initial-goal-satisfied");

        var finalState = await harness.Agent.RunAsync(harness.Goal, harness.Plan, harness.RunId, CancellationToken.None);

        Assert.Equal(RunState.Completed, finalState);
        Assert.Equal(RunState.Completed, harness.Agent.State);
        var completedEvent = Assert.Single(harness.Agent.Trace.Where(e => e.RunState == RunState.Completed));
        Assert.False(string.IsNullOrWhiteSpace(completedEvent.Reason));
        var evidence = Assert.Single(harness.Evidence);
        Assert.True(evidence.Satisfied);
        Assert.Equal(2L, evidence.SourceObservationSequence);
        Assert.Single(harness.Environment.ActionHistory);
        Assert.IsType<DeviceAction.LaunchApp>(harness.Environment.ActionHistory[0]);
    }

    // ── CP-06 断言 7（负向）：空 Plan + 初始 Observation 不满足 Goal → Failed（不谎报 Completed）─────

    [Fact]
    public async Task Assertion7_Negative_InitialGoalUnsatisfied_EmptyPlan_FailedNotCompleted()
    {
        var harness = ScenarioHarness.Create("happy");
        var emptyPlan = ScenarioPlans.Empty();

        var finalState = await harness.Agent.RunAsync(harness.Goal, emptyPlan, harness.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, finalState);
        Assert.Equal(RunState.Failed, harness.Agent.State);
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Completed);
        Assert.False(harness.Evidence[0].Satisfied);
        var failedEvent = Assert.Single(harness.Agent.Trace.Where(e => e.RunState == RunState.Failed));
        Assert.Contains("Plan 步数耗尽", failedEvent.Reason!, StringComparison.Ordinal);
    }

    // ── CP-06 断言 8（正向）：非空 Plan + 初始 Observation 已满足 Goal → 无需 dispatch 即可 Completed ──

    [Fact]
    public async Task Assertion8_NonEmptyPlan_InitialGoalSatisfied_CompletesWithZeroPlanStepDispatches()
    {
        var harness = ScenarioHarness.Create("initial-goal-satisfied");
        var plan = ScenarioPlans.WifiEnableSequence(); // 3 non-empty Plan steps: Tap, Tap, SetSwitch

        var finalState = await harness.Agent.RunAsync(harness.Goal, plan, harness.RunId, CancellationToken.None);

        Assert.Equal(RunState.Completed, finalState);
        Assert.Equal(RunState.Completed, harness.Agent.State);
        var completedEvent = Assert.Single(harness.Agent.Trace.Where(e => e.RunState == RunState.Completed));
        Assert.False(string.IsNullOrWhiteSpace(completedEvent.Reason));
        // 唯一一次评估 = 初始 post-Startup 观测（seq=2），Satisfied=true
        var evidence = Assert.Single(harness.Evidence);
        Assert.True(evidence.Satisfied);
        Assert.Equal(2L, evidence.SourceObservationSequence);
        Assert.Equal(completedEvent.Reason, evidence.Reason);
        // ZERO Plan-step dispatches：只有 LaunchApp（Startup），无任何 Plan step 被 dispatch
        Assert.Single(harness.Environment.ActionHistory);
        Assert.IsType<DeviceAction.LaunchApp>(harness.Environment.ActionHistory[0]);
        // 零 Action 事件（无 Step dispatch 即无 Action trace）
        Assert.DoesNotContain(harness.Agent.Trace, e => e.Action is not null);
    }

    // ── CP-06 断言 9（负向对照）：非空 Plan + 初始不满足 → 正常执行，不谎报提前完成 ──

    [Fact]
    public async Task Assertion9_Negative_NonEmptyPlan_InitialUnsatisfied_NormalExecutionNotPrematureComplete()
    {
        var harness = ScenarioHarness.Create("happy"); // WiFi OFF initially → Goal unsatisfied at seq=2
        var plan = ScenarioPlans.WifiEnableSequence();

        var finalState = await harness.Agent.RunAsync(harness.Goal, plan, harness.RunId, CancellationToken.None);

        Assert.Equal(RunState.Completed, finalState);
        // 初始评估不满足 → 正常执行路径（3 步 dispatch + 最终 post-action 满足）
        Assert.False(harness.Evidence[0].Satisfied); // seq=2 初始评估：WiFi OFF → 不满足
        Assert.True(harness.Evidence[^1].Satisfied); // 最终 post-action 评估：WiFi ON → 满足
        Assert.Equal(5L, harness.Evidence[^1].SourceObservationSequence); // seq5 = final post-action
        // 3 个 Plan steps 全部真实 dispatch（LaunchApp + Tap + Tap + SetSwitch = 4 actions）
        Assert.Equal(4, harness.Environment.ActionHistory.Count);
        Assert.Equal(3, harness.Agent.Trace.Count(e => e.Action is not null));
        // 初始 WorldBelief 推进正常（5 次观测：seq1 startup + seq2 observeInitial + 3 post-action）
        Assert.NotNull(harness.Agent.Belief);
        Assert.Equal(5, harness.Agent.Belief.SourceObservationSequence);
    }
}
