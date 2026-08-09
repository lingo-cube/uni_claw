using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
// 注：命名空间 UniClaw.Runtime.Agent / .Startup / .Container / .Traversal 与同名类——
// 本测试位于 UniClaw.Runtime 之下，裸名会先绑定到命名空间（CS0118），故用类型别名引用类。
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B7 Agent 机制测试（run-lifecycle SHALL / SC-P1-001 / 002 / 003 / 004 / 005）：
/// RunState 全生命周期（含 Initializing→Failed — SC-P1-002；Running→Failed — SC-P1-003 负向 / SC-P1-004）、
/// WorldBelief 代持与推进、Plan 驱动（bind / traverse / navigate 循环）、
/// 每次 post-action Observation 后 evidence evaluator 评估（I-10）、
/// 最终 failure authority（无恢复动作）、Trace 因果链（4 态转移 / StepId / 动作载荷）、
/// 确定性重放（同 runId → 相同 Trace — SC-P1-001 断言 7）。
/// 组合 wiring 使用 B9 共享基建（ScenarioHarness / ScenarioGoals / ScenarioPlans / ScenarioIdentity —
/// 裁决 7：5 个 Scenario 共享同一 Runtime slice）；本文件只保留变体特定的机制断言
/// （belief 推进的记录式 resolve、源码扫描等）。
/// </summary>
public class AgentTests
{
    // ── SC-P1-001 happy：Completed + Trace 4 态 + 动作顺序 + GoalEvidence ──────────────────────────────

    [Fact]
    public async Task Happy_Completed_TraceFourStates_ActionHistoryAndGoalEvidence()
    {
        var harness = ScenarioHarness.Create("happy");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(RunState.Completed, harness.Agent.State);
        Assert.False(string.IsNullOrWhiteSpace(harness.Agent.Reason));
        // Trace 生命周期 4 态（SC-P1-001 断言 1）：Idle → Initializing → Running → Completed
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Completed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
        // 动作顺序（SC-P1-001 Expected action order）：LaunchApp → Tap → Tap → SetSwitch
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
                new DeviceAction.SetSwitch(1, true),
            },
            harness.Environment.ActionHistory);
        // GoalEvidence：最终评估 Satisfied，证据引用最终 post-action Observation 序号（SC-P1-001 断言 4）
        Assert.Equal(3, harness.Evidence.Count); // seq3 / seq4 / seq5 各评估一次
        Assert.True(harness.Evidence[^1].Satisfied);
        Assert.Equal(5, harness.Evidence[^1].SourceObservationSequence);
        // 完成事件在 dispatch 事件与评估之后（SC-P1-003 断言 5）；完成原因记录于 Trace（断言 3）
        Assert.Equal(RunState.Completed, harness.Agent.Trace[^1].RunState);
        Assert.Equal(harness.Evidence[^1].Reason, harness.Agent.Trace[^1].Reason);
        // Trace 因果链完整（SC-P1-001 断言 6）：每个事件都携带 RunId
        Assert.All(harness.Agent.Trace, e => Assert.Equal(ScenarioHarness.DefaultRunId, e.RunId));
    }

    // ── SC-P1-002 startup-fg-fail：Failed、从未 Running、NotReady 原因、无 Container/Step 事件 ─────────

    [Fact]
    public async Task StartupForegroundFail_Failed_NoRunning_NotReadyReason_NoContainerOrStepEvents()
    {
        var harness = ScenarioHarness.Create("startup-fg-fail");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(RunState.Failed, harness.Agent.State);
        Assert.Null(harness.Agent.RecoveryAnchor); // SC-P1-002 断言 3：anchor 未建立
        // 从未进入 Running（SC-P1-002 断言 1）：Trace 无 Running 转移
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Failed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
        // NotReady 显式原因记录于 Trace（SC-P1-002 断言 2）
        Assert.Contains("ForegroundApplication 验证失败", harness.Agent.Reason, StringComparison.Ordinal);
        Assert.Equal(harness.Agent.Reason, harness.Agent.Trace[^1].Reason);
        // 无 Container / Step / Action 事件（SC-P1-002 断言 6）
        Assert.DoesNotContain(harness.Agent.Trace, e => e.ContainerId is not null || e.StepId is not null || e.Action is not null);
        // 无恢复动作（SC-P1-002 断言 5）：action history 仅 LaunchApp
        Assert.Equal(new DeviceAction[] { new DeviceAction.LaunchApp("Settings") }, harness.Environment.ActionHistory);
    }

    // ── SC-P1-004 missing-target：Failed + StepId + 显式原因 + 无恢复动作 ───────────────────────────────

    [Fact]
    public async Task MissingTarget_Failed_StepIdAndReason_NoRecoveryActions()
    {
        var harness = ScenarioHarness.Create("missing-target");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(RunState.Failed, harness.Agent.State);
        var failedEvent = harness.Agent.Trace[^1];
        Assert.Equal(RunState.Failed, failedEvent.RunState);
        Assert.Equal("Step-2", failedEvent.StepId); // SC-P1-004 断言 1：StepId 关联 Failed 结果
        Assert.Equal("NetworkSettings", failedEvent.ContainerId);
        Assert.False(string.IsNullOrWhiteSpace(failedEvent.Reason)); // 非空原因（断言 1 / SC-P1-004）
        Assert.Contains("无匹配候选", failedEvent.Reason, StringComparison.Ordinal);
        Assert.Equal(harness.Agent.Reason, failedEvent.Reason);
        // 无恢复动作（SC-P1-004 断言 3）：LaunchApp → Tap(Network & Internet)，之后无任何动作
        Assert.Equal(
            new DeviceAction[] { new DeviceAction.LaunchApp("Settings"), new DeviceAction.Tap(0) },
            harness.Environment.ActionHistory);
    }

    // ── SC-P1-003 负向 switch-stuck：Failed（不是 Completed）+ 显式原因 + 完整历史 + 无恢复 ─────────────

    [Fact]
    public async Task SwitchStuck_FailedNotCompleted_ExplicitReason_FullHistory_NoRecovery()
    {
        var harness = ScenarioHarness.Create("switch-stuck");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Failed, state); // 不是 Completed（SC-P1-003 断言 4）
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Completed);
        Assert.False(harness.Evidence[^1].Satisfied); // 诚实 evaluator：开关仍 false
        Assert.Contains("未满足", harness.Agent.Reason, StringComparison.Ordinal); // 显式原因（SC-P1-003 断言 5）
        Assert.Equal(harness.Agent.Reason, harness.Agent.Trace[^1].Reason);
        // 完整动作历史 + 无额外恢复动作（断言 5 / 裁决 10：dispatch Dispatched 但世界不变）
        Assert.Equal(4, harness.Environment.ActionHistory.Count);
        Assert.Equal(new DeviceAction.SetSwitch(1, true), harness.Environment.ActionHistory[^1]);
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Failed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));
    }

    // ── SC-P1-005 same-text：Trace 中 SetSwitch 动作携带 grounding 解析后的开关 Index ──────────────────

    [Fact]
    public async Task SameText_TraceStepActionCarriesGroundingResolvedSwitchIndex()
    {
        var harness = ScenarioHarness.Create("same-text");

        var state = await harness.RunAsync();

        Assert.Equal(RunState.Completed, state);
        // SC-P1-005 断言 1：SetSwitch 动作 TargetElementIndex == 开关元素 Index（≠ 标题 Index 0）
        var step3 = Assert.Single(harness.Agent.Trace.Where(e => e.StepId == "Step-3"));
        Assert.Equal(new DeviceAction.SetSwitch(1, true), step3.Action);
        Assert.Equal("WiFiSettings", step3.ContainerId);
        Assert.Equal(5, harness.Agent.Belief!.SourceObservationSequence); // 最终证据来自 post-action Observation
    }

    // ── 确定性重放（SC-P1-001 断言 7）：同 runId → 完全相同 Trace ─────────────────────────────────────

    [Fact]
    public async Task DeterministicReplay_SameRunId_ProducesIdenticalTraceAndHistory()
    {
        async Task<(RunState State, TraceEvent[] Trace, IReadOnlyList<DeviceAction> History)> RunOnceAsync()
        {
            var harness = ScenarioHarness.Create("happy");
            var state = await harness.RunAsync();
            return (state, harness.Agent.Trace.ToArray(), harness.Environment.ActionHistory);
        }

        var (stateA, traceA, historyA) = await RunOnceAsync();
        var (stateB, traceB, historyB) = await RunOnceAsync();

        Assert.Equal(RunState.Completed, stateA);
        Assert.Equal(stateA, stateB);
        Assert.Equal(traceA, traceB); // TraceEvent 记录相等（标量 + DeviceAction 载荷）
        Assert.Equal(historyA, historyB);
    }

    // ── SC-P3-002 Task 2.1：Container-scope bounded handling + continuity / escalation ────────────────

    [Fact]
    public async Task PopupLocalHandling_Continuous_PreservesSameContainerProgress_AndCompletesFromGoalEvidence()
    {
        var (agent, environment, containers, evidence) = await RunPopupLocalHandlingAsync("continuous");

        Assert.Equal(RunState.Completed, agent.State);
        var container = Assert.Single(containers);
        Assert.Equal(new[] { "WiFi", "Dismiss" }, container.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(4, container.CurrentObservation!.SequenceNumber);
        Assert.True(container.IsStillMine(container.CurrentObservation));
        Assert.Null(agent.LastTrap);
        Assert.Equal(2, evidence.Count);
        Assert.False(evidence[0].Satisfied);
        Assert.True(evidence[1].Satisfied);
        Assert.Equal(4, evidence[1].SourceObservationSequence);
        Assert.Single(agent.Trace.Where(entry => entry.StepId == "Step-2" && entry.Action is DeviceAction.Tap));
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            environment.ActionHistory);
    }

    [Fact]
    public async Task PopupLocalHandling_DismissRejected_EscalatesContainerEvidence_WithoutBlindRepeatOrCompletion()
    {
        var (agent, environment, containers, evidence) = await RunPopupLocalHandlingAsync("rejected");

        Assert.Equal(RunState.Failed, agent.State);
        Assert.DoesNotContain(agent.Trace, entry => entry.RunState == RunState.Completed);
        var container = Assert.Single(containers);
        Assert.Equal(new[] { "WiFi", "Dismiss" }, container.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(3, container.CurrentObservation!.SequenceNumber);
        var trap = agent.LastTrap ?? throw new InvalidOperationException("local handling failure 未升级结构化 evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(3, trap.Expected);
        Assert.Null(trap.Observed);
        Assert.Equal(new DeviceAction.Tap(0), trap.LastAction);
        Assert.Single(agent.Trace.Where(entry => entry.TrapScope == TrapScope.Container));
        Assert.Single(evidence);
        Assert.False(evidence[0].Satisfied);
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            environment.ActionHistory);
    }

    [Fact]
    public async Task PopupLocalHandling_PageChanged_EscalatesThenAgentRebinds_WhileOriginalProgressRemains()
    {
        var (agent, environment, containers, evidence) = await RunPopupLocalHandlingAsync("page-changed");

        Assert.Equal(RunState.Failed, agent.State);
        Assert.Equal(2, containers.Count);
        var original = containers[0];
        var rebound = containers[1];
        Assert.Equal("NetworkSettings", original.SemanticPageName);
        Assert.Equal(new[] { "WiFi", "Dismiss" }, original.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal("SettingsMain", rebound.SemanticPageName);
        Assert.Empty(rebound.ExecutedSteps);
        var trap = agent.LastTrap ?? throw new InvalidOperationException("continuity failure 未升级结构化 evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(3, trap.Expected);
        Assert.Equal(4, trap.Observed);
        Assert.Single(agent.Trace.Where(entry => entry.StepId == "Step-2" && entry.Action is DeviceAction.Tap));
        Assert.Equal("SettingsMain", agent.Belief!.SemanticPage);
        Assert.Equal(2, evidence.Count);
        Assert.All(evidence, item => Assert.False(item.Satisfied));
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            environment.ActionHistory);
    }

    // ── SC-P3-003 Task 2.1：targetless viewport continuity + Container-scope escalation ─────────────

    [Fact]
    public async Task ViewportMovement_Continuous_AdvancesSameContainerAndPreservesExistingProgress()
    {
        var (agent, environment, traversal, containers, evidence) = await RunViewportMovementAsync("continuous");

        Assert.Equal(RunState.Completed, agent.State);
        var container = Assert.Single(containers);
        Assert.Equal("ScrollableList", container.SemanticPageName);
        Assert.Equal(new[] { "A", "Viewport" }, container.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(4, container.CurrentObservation!.SequenceNumber);
        Assert.Equal(new[] { "D", "E", "F" }, container.CurrentObservation.Elements.Select(element => element.Text));
        Assert.Null(agent.LastTrap);
        Assert.Equal(new[] { false, true }, evidence.Select(item => item.Satisfied));
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.ScrollForward(),
            },
            environment.ActionHistory);
        var viewportEntry = traversal.Journal[^1];
        Assert.Null(viewportEntry.SelectedElementIndex);
        Assert.Equal(new DeviceAction.ScrollForward(), viewportEntry.DispatchedAction);
        Assert.Equal(4, viewportEntry.PostActionObservation!.SequenceNumber);
    }

    [Fact]
    public async Task ViewportMovement_Rejected_EmitsContainerEvidenceWithoutObserveOrRedispatch()
    {
        var (agent, environment, traversal, containers, evidence) = await RunViewportMovementAsync("rejected");

        Assert.Equal(RunState.Failed, agent.State);
        var container = Assert.Single(containers);
        Assert.Equal(new[] { "A", "Viewport" }, container.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(2, container.CurrentObservation!.SequenceNumber);
        var trap = agent.LastTrap ?? throw new InvalidOperationException("viewport rejection 未升级 Container-scope evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Null(trap.Observed);
        Assert.Equal(new DeviceAction.ScrollForward(), trap.LastAction);
        Assert.Single(environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Equal(3, environment.ObservationHistory.Count);
        Assert.Single(evidence);
        Assert.False(evidence[0].Satisfied);
        Assert.Null(traversal.Journal[^1].PostActionObservation);
    }

    [Fact]
    public async Task ViewportMovement_StaleEvidence_EmitsContainerEvidenceWithoutContinuityOrRedispatch()
    {
        var (agent, environment, traversal, containers, evidence) = await RunViewportMovementAsync("stale");

        Assert.Equal(RunState.Failed, agent.State);
        var container = Assert.Single(containers);
        Assert.Equal(2, container.CurrentObservation!.SequenceNumber);
        Assert.Equal(new[] { "A", "Viewport" }, container.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(TrapScope.Container, agent.LastTrap!.Scope);
        Assert.Equal(2, agent.LastTrap.Expected);
        Assert.Equal(2, agent.LastTrap.Observed);
        Assert.Single(environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Equal(2, traversal.Journal[^1].PostActionObservation!.SequenceNumber);
        Assert.Single(evidence);
        Assert.False(evidence[0].Satisfied);
    }

    [Fact]
    public async Task ViewportMovement_PageChanged_EscalatesThenAgentRebinds_AndPreservesOriginalProgress()
    {
        var (agent, environment, _, containers, evidence) = await RunViewportMovementAsync("page-changed");

        Assert.Equal(RunState.Failed, agent.State);
        Assert.Equal(2, containers.Count);
        var original = containers[0];
        var rebound = containers[1];
        Assert.Equal("ScrollableList", original.SemanticPageName);
        Assert.Equal(new[] { "A", "Viewport" }, original.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(2, original.CurrentObservation!.SequenceNumber);
        Assert.Equal("OtherPage", rebound.SemanticPageName);
        Assert.Empty(rebound.ExecutedSteps);
        var trap = agent.LastTrap ?? throw new InvalidOperationException("viewport identity conflict 未升级 Container-scope evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(2, trap.Expected);
        Assert.Equal(4, trap.Observed);
        Assert.Equal("OtherPage", agent.Belief!.SemanticPage);
        Assert.Single(environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.All(evidence, item => Assert.False(item.Satisfied));
    }

    // ── Mechanism：belief 推进（WorldBelief 由 Observation 生成并沿 Run 推进）─────────────────────────

    [Fact]
    public async Task Mechanism_BeliefAdvancesThroughInjectedResolution()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        var recordedPages = new List<string?>();
        string? RecordingResolve(Observation observation)
        {
            var page = ScenarioIdentity.ResolveSemanticPage(observation);
            recordedPages.Add(page);
            return page;
        }

        // 变体特定逻辑：记录式 resolve（belief 推进链断言）——harness 固定用非记录式解析，故此处手动接线
        var startup = new RuntimeStartup(env, ScenarioHarness.TargetApplication, RecordingResolve);
        var traversal = new RuntimeTraversal(env);
        // B3：本场景不触发 drift → 恢复组件惰性接线
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup, traversal, ct => env.ObserveAsync(ct), RecordingResolve, ScenarioIdentity.ContainerFactory(traversal), recovery);

        var state = await agent.RunAsync(
            ScenarioGoals.EnableWifi([]), ScenarioPlans.WifiEnableSequence(), ScenarioHarness.DefaultRunId, CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        // belief 推进链：Startup 语义解析 → 初始 Reconcile → 每步 post-action Reconcile
        Assert.Equal(
            new string?[] { "SettingsMain", "SettingsMain", "NetworkSettings", "WiFiSettings", "WiFiSettingsOn" },
            recordedPages);
        Assert.Equal("WiFiSettingsOn", agent.Belief!.SemanticPage);
        Assert.Equal(1f, agent.Belief.Confidence);
        Assert.Equal(5, agent.Belief.SourceObservationSequence); // 证据引用支撑观测序列（裁决 2）
    }

    // ── Mechanism：RunState 唯一 owner 是 Agent（I-2）────────────────────────────────────────────────

    [Fact]
    public void Mechanism_OnlyAgentAndModelTouchRunState()
    {
        var runtimeDir = TestRepositoryPaths.RepoPath("src", "UniClaw.Runtime");
        var offenders = Directory.EnumerateFiles(runtimeDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Agent{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}Model{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => File.ReadAllText(p).Contains("RunState.", StringComparison.Ordinal))
            .ToList();
        Assert.True(
            offenders.Count == 0,
            "RunState 成员访问出现在非 Agent/Model 生产文件（I-2：RunState 唯一 owner 是 Agent）: " + string.Join(", ", offenders));
    }

    // ── Mechanism：生产 Agent 源码零场景字符串（裁决 11）──────────────────────────────────────────────

    [Fact]
    public void Mechanism_NoScenarioStringsInAgentSource()
    {
        var agentSource = TestRepositoryPaths.RepoPath("src", "UniClaw.Runtime", "Agent", "Agent.cs");
        Assert.True(File.Exists(agentSource), $"Agent 源码缺失: {agentSource}");
        var content = File.ReadAllText(agentSource);
        foreach (var banned in new[] { "WiFi", "Network & Internet", "Bluetooth", "SettingsMain", "Launcher" })
        {
            Assert.False(
                content.Contains(banned, StringComparison.Ordinal),
                $"Agent 源码包含场景字符串「{banned}」（裁决 11：生产 Runtime 不硬编码场景字符串）。");
        }
    }

    private static async Task<(
        RuntimeAgent Agent,
        ScriptedEnvironment Environment,
        List<RuntimeContainer> Containers,
        List<GoalEvidence> Evidence)> RunPopupLocalHandlingAsync(string branch)
    {
        var (dismissTarget, dispatchOutcome) = branch switch
        {
            "continuous" => ("NetworkSettings", ActionResultOutcome.Dispatched),
            "rejected" => ("Popup", ActionResultOutcome.Rejected),
            "page-changed" => ("SettingsMain", ActionResultOutcome.Dispatched),
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "未知 Task 2.1 Popup 分支。"),
        };
        var environment = new ScriptedEnvironment(
            "NetworkSettings",
            launchNextScreenName: null,
            [
                new ScreenConfig(
                    "NetworkSettings",
                    "Settings",
                    [new ElementConfig("WiFi", null, null)]),
                new ScreenConfig(
                    "Popup",
                    "Settings",
                    [
                        new ElementConfig(
                            "Dismiss",
                            null,
                            new TransitionConfig(
                                ScreenTransitionAction.Tap,
                                dismissTarget,
                                DispatchOutcome: dispatchOutcome)),
                    ]),
                new ScreenConfig(
                    "SettingsMain",
                    "Settings",
                    [new ElementConfig("Network & Internet", null, null)]),
            ],
            observeScreenTransitions: new Dictionary<long, string> { [3] = "Popup" });
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ScenarioIdentity.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer CreateContainer(string pageName)
        {
            var container = new RuntimeContainer(pageName, ScenarioIdentity.IdentityRule(pageName), traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var evidence = new List<GoalEvidence>();
        var goal = new Goal(observation =>
        {
            var satisfied = branch == "continuous"
                && observation.SequenceNumber >= 4
                && ScenarioIdentity.ResolveSemanticPage(observation) == "NetworkSettings";
            var item = new GoalEvidence(
                satisfied,
                satisfied ? "fresh world evidence satisfies goal" : "goal evidence remains unsatisfied",
                observation.SequenceNumber);
            evidence.Add(item);
            return item;
        });
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ScenarioIdentity.ResolveSemanticPage,
            CreateContainer,
            recovery);

        await agent.RunAsync(
            goal,
            new Plan([new PlanStep("WiFi", "Tap"), new PlanStep("Dismiss", "Tap")]),
            "sc-p3-002-task-2-1",
            CancellationToken.None);
        return (agent, environment, containers, evidence);
    }

    private static async Task<(
        RuntimeAgent Agent,
        ScriptedEnvironment Environment,
        RuntimeTraversal Traversal,
        List<RuntimeContainer> Containers,
        List<GoalEvidence> Evidence)> RunViewportMovementAsync(string branch)
    {
        var environment = branch switch
        {
            "continuous" => ScriptedEnvironmentVariants.ViewportContinuous(),
            "rejected" => ScriptedEnvironmentVariants.ViewportRejected(),
            "stale" => ScriptedEnvironmentVariants.ViewportRuntimeStale(),
            "page-changed" => ScriptedEnvironmentVariants.ViewportPageChanged(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "未知 Task 2.1 viewport 分支。"),
        };
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ResolveViewportPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer CreateContainer(string pageName)
        {
            var container = new RuntimeContainer(
                pageName,
                observation => string.Equals(ResolveViewportPage(observation), pageName, StringComparison.Ordinal),
                traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var evidence = new List<GoalEvidence>();
        var goal = new Goal(observation =>
        {
            var satisfied = branch == "continuous"
                && observation.SequenceNumber >= 4
                && ResolveViewportPage(observation) == "ScrollableList"
                && observation.Elements.Any(element => element.Text == "D");
            var item = new GoalEvidence(
                satisfied,
                satisfied ? "fresh viewport evidence satisfies goal" : "goal evidence remains unsatisfied",
                observation.SequenceNumber);
            evidence.Add(item);
            return item;
        });
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolveViewportPage,
            CreateContainer,
            recovery);

        await agent.RunAsync(
            goal,
            new Plan([new PlanStep("A", "Tap"), new PlanStep("Viewport", "ScrollForward")]),
            "sc-p3-003-task-2-1",
            CancellationToken.None);
        return (agent, environment, traversal, containers, evidence);
    }

    private static string? ResolveViewportPage(Observation observation)
        => observation.Elements.Any(element => element.Text is "A" or "B" or "C" or "D" or "E" or "F")
            ? "ScrollableList"
            : observation.Elements.Any(element => element.Text == "Other semantic page")
                ? "OtherPage"
                : null;
}
