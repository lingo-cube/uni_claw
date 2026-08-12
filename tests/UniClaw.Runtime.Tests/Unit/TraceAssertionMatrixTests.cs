using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B8 TraceEvent 断言矩阵（scenarios/catalog.md 各 Scenario Assertions + 按事件类别的字段存在性规则）：
/// 测试名以 Scenario Assertion ID 为键（SC-P1-001_A1 / A5_A6 … SC-P1-005_A1）。
/// 验证 B7 记录的 Trace 完整性：RunId / ContainerId / StepId / ActionId / Action（载荷）/ Reason / RunState 因果链
/// （SC-P1-001 断言 6 — ActionId 环节由 B8 补齐）、按事件类别的字段存在性、只追加 + 确定性重放。
/// 组合 wiring 使用 B9 共享 harness（ScenarioHarness — 裁决 7：5 个 Scenario 共享同一 Runtime slice）。
/// </summary>
public class TraceAssertionMatrixTests
{
    // ── SC-P1-001_A1：生命周期转移顺序 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SC_P1_001_A1_LifecycleTransitionOrder_IdleInitializingRunningCompleted()
    {
        var harness = ScenarioHarness.Create("happy");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Completed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
    }

    // ── SC-P1-001_A5_A6：Completed 在最后一个动作之后 + 因果链字段完整 ─────────────────────────────────

    [Fact]
    public async Task SC_P1_001_A5_A6_CompletedAfterLastAction_CausalChainFieldsComplete()
    {
        var harness = ScenarioHarness.Create("happy");
        var trace = harness.Agent.Trace;

        await harness.RunAsync();

        // A5：Completed 事件在最后一个动作（Step-3 分发）事件之后
        var completedIndex = Array.FindIndex(trace.ToArray(), e => e.RunState == RunState.Completed);
        var step3Index = Array.FindIndex(trace.ToArray(), e => e.StepId == "Step-3");
        Assert.True(completedIndex > step3Index, "Completed 事件必须位于最后一个动作分发事件之后（SC-P1-003 断言 5）。");

        // A6：因果链完整 — 每步事件携带 RunId / ContainerId / StepId / ActionId + 动作载荷
        var stepEvents = trace.Where(e => e.StepId is not null).ToArray();
        Assert.Equal(3, stepEvents.Length);
        Assert.All(stepEvents, e =>
        {
            Assert.Equal(ScenarioHarness.DefaultRunId, e.RunId);
            Assert.NotNull(e.ContainerId);
            Assert.NotNull(e.StepId);
            Assert.NotNull(e.ActionId); // B8：ActionId 环节已补齐
            Assert.NotNull(e.Action);   // 动作载荷
        });
        // 完成原因记录（Reason 环节）
        Assert.False(string.IsNullOrWhiteSpace(trace[completedIndex].Reason));
    }

    // ── SC-P1-002_A1_A2_A6：startup 失败 — 无 Running、NotReady 原因、无 Container/Step 事件 ──────────

    [Fact]
    public async Task SC_P1_002_A1_A2_A6_NoRunning_NotReadyReason_NoContainerOrStepEvents()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Failed, state);
        // A1：从未进入 Running（Trace 无 Running 转移事件）
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Running);
        // A2：Failed 携带 NotReady 显式原因
        var failed = harness.Agent.Trace[^1];
        Assert.Equal(RunState.Failed, failed.RunState);
        Assert.Contains("ForegroundApplication 验证失败", failed.Reason, StringComparison.Ordinal);
        // A6：无 Container / Step / Action / ActionId 事件
        Assert.DoesNotContain(harness.Agent.Trace,
            e => e.ContainerId is not null || e.StepId is not null || e.ActionId is not null || e.Action is not null);
        // 无恢复动作（A5）
        Assert.Equal(new DeviceAction[] { new DeviceAction.LaunchApp("Settings") }, harness.Environment.ActionHistory);
    }

    // ── SC-P1-003_A1_A3：Completed 在 dispatch 与评估之后；Reason == GoalEvidence.Reason ──────────────

    [Fact]
    public async Task SC_P1_003_A1_A3_CompletedAfterDispatchAndEvaluation_ReasonMatchesEvidence()
    {
        var harness = ScenarioHarness.Create("happy");
        var trace = harness.Agent.Trace;

        await harness.RunAsync();

        // A1：dispatch（Step-3 动作事件）之后才 Completed
        var step3Index = Array.FindIndex(trace.ToArray(), e => e.StepId == "Step-3");
        var completedIndex = Array.FindIndex(trace.ToArray(), e => e.RunState == RunState.Completed);
        Assert.True(completedIndex > step3Index);
        // A2：证据来自 post-action Observation（不是 dispatch 结果 — I-10）
        Assert.True(harness.Evidence[^1].Satisfied);
        Assert.Equal(5, harness.Evidence[^1].SourceObservationSequence);
        // A3：完成原因记录于 Trace == GoalEvidence.Reason
        Assert.Equal(harness.Evidence[^1].Reason, trace[completedIndex].Reason);
    }

    // ── SC-P1-003_A4_A5 负向：switch-stuck — Failed（非 Completed）、显式原因、无恢复 ───────────────────

    [Fact]
    public async Task SC_P1_003_A4_A5_Negative_FailedNotCompleted_ExplicitReason_NoRecovery()
    {
        var harness = ScenarioHarness.Create("switch-stuck");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Failed, state);
        // A4：最终 Failed（不是 Completed）
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Completed);
        Assert.False(harness.Evidence[^1].Satisfied);
        // A5：显式原因记录于 Trace
        Assert.Equal(RunState.Failed, harness.Agent.Trace[^1].RunState);
        Assert.Contains("未满足", harness.Agent.Trace[^1].Reason, StringComparison.Ordinal);
        // A5：无额外恢复动作（完整 4 动作后停止）
        Assert.Equal(4, harness.Environment.ActionHistory.Count);
        Assert.Equal(new DeviceAction.SetSwitch(1, true), harness.Environment.ActionHistory[^1]);
    }

    // ── SC-P1-004_A1_A2：步骤失败 — Failed 携带 StepId + 非空原因 ─────────────────────────────────────

    [Fact]
    public async Task SC_P1_004_A1_A2_FailedWithStepIdAndNonEmptyReason()
    {
        var harness = ScenarioHarness.Create("missing-target");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Failed, state);
        var failed = harness.Agent.Trace[^1];
        Assert.Equal(RunState.Failed, failed.RunState);
        // A1：StepId 关联 Failed 结果（SC-P1-004：Agent 是最终 failure authority，原因结构化上报）
        Assert.Equal("Step-2", failed.StepId);
        Assert.Equal("NetworkSettings", failed.ContainerId);
        // A2：非空原因
        Assert.False(string.IsNullOrWhiteSpace(failed.Reason));
        Assert.Contains("无匹配候选", failed.Reason, StringComparison.Ordinal);
        // 无恢复动作（A3）
        Assert.Equal(2, harness.Environment.ActionHistory.Count);
    }

    // ── SC-P1-005_A1：动作载荷 — SetSwitch.TargetElementIndex == 开关元素 Index ────────────────────────

    [Fact]
    public async Task SC_P1_005_A1_ActionPayload_TargetsSwitchElement()
    {
        var harness = ScenarioHarness.Create("same-text");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Completed, state);
        var step3 = Assert.Single(harness.Agent.Trace.Where(e => e.StepId == "Step-3"));
        var setSwitch = Assert.IsType<DeviceAction.SetSwitch>(step3.Action);
        Assert.Equal(1, setSwitch.TargetElementIndex); // 开关元素 Index（≠ 标题 Index 0 — SC-P1-005 断言 1）
        Assert.NotNull(step3.ActionId);
    }

    // ── 字段存在性规则（按事件类别）────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FieldRules_AllEvents_CarryRunIdEqualToInjectedRunId()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        Assert.NotEmpty(harness.Agent.Trace);
        Assert.All(harness.Agent.Trace, e => Assert.Equal(ScenarioHarness.DefaultRunId, e.RunId));
    }

    [Fact]
    public async Task FieldRules_TransitionEvents_OnlyLifecycleTransitionsInOrder()
    {
        var happy = ScenarioHarness.Create("happy");
        var failing = ScenarioHarness.Create("startup-fg-fail");
        await happy.RunAsync();
        await failing.RunAsync();

        // happy：Idle → Initializing → Running → Completed；且终结态事件唯一
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Completed },
            happy.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
        Assert.Single(happy.Agent.Trace.Where(e => e.RunState is RunState.Completed or RunState.Failed));
        // startup 失败：Idle → Initializing → Failed；且终结态事件唯一
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Failed },
            failing.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
        Assert.Single(failing.Agent.Trace.Where(e => e.RunState is RunState.Completed or RunState.Failed));
    }

    [Fact]
    public async Task FieldRules_StepEvents_CarryCompleteCausalChain_ActionIdsSequentialUnique()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var stepEvents = harness.Agent.Trace.Where(e => e.StepId is not null).ToArray();
        Assert.Equal(3, stepEvents.Length);
        // Step 事件：ContainerId + StepId + ActionId + Action 全部非空
        Assert.All(stepEvents, e =>
        {
            Assert.NotNull(e.ContainerId);
            Assert.NotNull(e.StepId);
            Assert.NotNull(e.ActionId);
            Assert.NotNull(e.Action);
        });
        // ActionId 顺序递增且唯一（Action-1 / Action-2 / Action-3）
        Assert.Equal(new[] { "Action-1", "Action-2", "Action-3" }, stepEvents.Select(e => e.ActionId));
        // 无孤儿动作/标识：任何 Action 或 ActionId 都必须落在 Step 事件上
        Assert.All(harness.Agent.Trace.Where(e => e.Action is not null || e.ActionId is not null),
            e => Assert.NotNull(e.StepId));
    }

    [Fact]
    public async Task FieldRules_ContainerEvents_CarryContainerIdOnly()
    {
        var harness = ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var containerEvents = harness.Agent.Trace.Where(e => e.ContainerId is not null && e.StepId is null).ToArray();
        Assert.Equal(3, containerEvents.Length); // 初始 bind + 2 次 navigate
        Assert.All(containerEvents, e =>
        {
            Assert.NotNull(e.ContainerId);
            Assert.Null(e.StepId);
            Assert.Null(e.ActionId);
            Assert.Null(e.Action);
            Assert.Null(e.Reason);
            Assert.Null(e.RunState);
        });
    }

    [Fact]
    public async Task FieldRules_FailedEvents_CarryExplicitReason_ShapedPerSource()
    {
        // startup 源失败（SC-P1-002）：Failed 事件无 ContainerId / StepId
        var startup = ScenarioHarness.Create("startup-fg-fail");
        await startup.RunAsync();
        var startupFailed = startup.Agent.Trace[^1];
        Assert.Equal(RunState.Failed, startupFailed.RunState);
        Assert.False(string.IsNullOrWhiteSpace(startupFailed.Reason));
        Assert.Null(startupFailed.ContainerId);
        Assert.Null(startupFailed.StepId);

        // 步骤源失败（SC-P1-004）：Failed 事件携带 StepId（escalate 半句的结构化结果 — 裁决 4）
        var stepFailed = ScenarioHarness.Create("missing-target");
        await stepFailed.RunAsync();
        var stepFailedEvent = stepFailed.Agent.Trace[^1];
        Assert.Equal(RunState.Failed, stepFailedEvent.RunState);
        Assert.False(string.IsNullOrWhiteSpace(stepFailedEvent.Reason));
        Assert.NotNull(stepFailedEvent.StepId);
        Assert.NotNull(stepFailedEvent.ContainerId);

        // 证据不满足源失败（SC-P1-003 负向）：Failed 事件显式原因，发生在最后一个动作事件之后
        var unsatisfied = ScenarioHarness.Create("switch-stuck");
        await unsatisfied.RunAsync();
        var unsatisfiedEvent = unsatisfied.Agent.Trace[^1];
        Assert.Equal(RunState.Failed, unsatisfiedEvent.RunState);
        Assert.Contains("未满足", unsatisfiedEvent.Reason, StringComparison.Ordinal);
        Assert.True(
            unsatisfied.Agent.Trace.Count - 1 > Array.FindIndex(unsatisfied.Agent.Trace.ToArray(), e => e.StepId == "Step-3"));
    }

    [Fact]
    public async Task FieldRules_Trace_AppendOnlyStableAndDeterministic()
    {
        var run1 = ScenarioHarness.Create("happy");
        var run2 = ScenarioHarness.Create("happy");
        await run1.RunAsync();
        await run2.RunAsync();

        // 追加式只读暴露（I-2 / 裁决 5）：Agent 持有 List<TraceEvent>，外部只见 IReadOnlyList
        Assert.IsAssignableFrom<IReadOnlyList<TraceEvent>>(run1.Agent.Trace);
        // 确定性重放（SC-P1-001 断言 7）：同 runId → 完全相同事件序列
        Assert.Equal(run1.Agent.Trace.ToArray(), run2.Agent.Trace.ToArray());
        Assert.Equal(run1.Environment.ActionHistory, run2.Environment.ActionHistory);
    }

    // ── 架构断言：无独立 Observability 组件（裁决 5）────────────────────────────────────────────────────

    [Fact]
    public void Architecture_ObservabilityDirectory_HasNoComponentCode()
    {
        var dir = TestRepositoryPaths.RepoPath("src", "UniClaw.Runtime", "Observability");
        Assert.True(Directory.Exists(dir), $"Observability/ 目录缺失: {dir}");
        var csFiles = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).ToList();
        // RuntimeObservability.cs is the approved ActivitySource emission seam
        // (openspec/changes/runtime-observability-trace-foundation, TC 1.1).
        // All other runtime observability types remain forbidden.
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "RuntimeObservability.cs" };
        var unexpected = csFiles.Where(f => !allowed.Contains(Path.GetFileName(f))).ToList();
        Assert.True(
            unexpected.Count == 0,
            $"Observability/ 含未批准的 .cs 文件: {string.Join(", ", unexpected)}");
    }
}
