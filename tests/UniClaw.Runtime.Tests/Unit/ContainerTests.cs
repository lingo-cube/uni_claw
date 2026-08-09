using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
// 注：命名空间 UniClaw.Runtime.Container 与类 Container 同名——本测试位于 UniClaw.Runtime 之下，
// 裸名 Container 会先绑定到命名空间（CS0118），故用类型别名引用类。
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B5 Container 机制测试（specs/container-traversal SHALL）：still-mine / candidates 只读快照 /
/// 局部完成判定 / 步骤失败原样转交（I-8 / SC-P1-004）/ 确定性；identity 规则与 executor 全部注入。
/// </summary>
public class ContainerTests
{
    [Fact]
    public async Task IsStillMine_UsesInjectedRule_TrueOnOwnScreen_FalseOnOtherScreen()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var settingsMain = await env.ObserveAsync(CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var networkSettings = await env.ObserveAsync(CancellationToken.None);

        // 注入规则：前台为 Settings 且含 "Network & Internet"（测试字符串允许；生产不硬编码 — 裁决 11）
        var container = new RuntimeContainer(
            "SettingsMain",
            o => o.ForegroundApplication == "Settings" && o.Elements.Any(e => e.Text == "Network & Internet"),
            (_, _, _) => new TraversalStepResult.Succeeded());

        Assert.True(container.IsStillMine(settingsMain));
        Assert.False(container.IsStillMine(networkSettings));
    }

    [Fact]
    public async Task Candidates_AreCurrentObservationElements_ReadOnlySnapshot_StableIndex()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var wifiSettings = await env.ObserveAsync(CancellationToken.None);

        var container = new RuntimeContainer("WiFiSettings", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(wifiSettings);

        var candidates = container.Candidates;
        Assert.Equal(candidates, wifiSettings.Elements); // 候选 = 当前观测的全部元素（无过滤/消歧 — 裁决 3）
        Assert.Equal(2, candidates.Length);
        Assert.Equal("WiFi", candidates[0].Text);
        Assert.Equal(0, candidates[0].Index);
        Assert.Equal("WiFi", candidates[1].Text);
        Assert.Equal(1, candidates[1].Index);

        // 只读快照：重复读取内容与序位不变（Index 是观测内稳定序位，非坐标）
        Assert.Equal(candidates[0].Index, container.Candidates[0].Index);
        Assert.Equal(candidates[1].Index, container.Candidates[1].Index);
    }

    [Fact]
    public void IsLocalComplete_FalseBeforeExecution_TrueAfterSucceededStep()
    {
        var container = new RuntimeContainer("Page", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(new Observation([], "Settings", 1));

        Assert.False(container.IsLocalComplete); // 未执行 → false

        var result = container.ExecuteStep(new PlanStep("target", "action"));

        Assert.IsType<TraversalStepResult.Succeeded>(result);
        Assert.True(container.IsLocalComplete);
    }

    [Fact]
    public void ExecuteStep_FailedResult_RelayedAsIs_NoRecovery_NoRunState_NotLocalComplete()
    {
        TraversalStepResult? produced = null;
        var container = new RuntimeContainer(
            "NetworkSettings",
            _ => true,
            (step, observation, candidates) =>
            {
                // 注入 executor 模拟无法推进（SC-P1-004：目标在当前观测无候选）——B6 Traversal 是生产实现者
                produced = new TraversalStepResult.Failed($"目标「{step.TargetDescription}」在当前观测中无匹配。");
                return produced;
            });
        container.Bind(new Observation([new ObservedElement("Bluetooth", null, 0)], "Settings", 1));

        var result = container.ExecuteStep(new PlanStep("WiFi", "Tap"));

        // 原样转交：同一 Failed 实例（引用相等）——不重写 / 不吞没 / 不包装（I-8 / SC-P1-004）
        Assert.Same(produced, result);
        var failed = Assert.IsType<TraversalStepResult.Failed>(result);
        Assert.False(string.IsNullOrWhiteSpace(failed.Reason));
        Assert.False(container.IsLocalComplete); // 失败不构成局部完成
    }

    [Fact]
    public void ExecuteStep_SucceededResult_RelayedAndRecordedInExecutedSteps()
    {
        var succeeded = new TraversalStepResult.Succeeded();
        var container = new RuntimeContainer("Page", _ => true, (_, _, _) => succeeded);
        container.Bind(new Observation([], "Settings", 1));

        var result = container.ExecuteStep(new PlanStep("target", "action"));

        Assert.Same(succeeded, result);
        Assert.Single(container.ExecutedSteps);
        Assert.Equal(new PlanStep("target", "action"), container.ExecutedSteps[0]);
        Assert.True(container.IsLocalComplete);
    }

    [Fact]
    public void Bind_SetsObservation_AndResetsLocalState()
    {
        var container = new RuntimeContainer("Page", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());
        var first = new Observation([new ObservedElement("A", null, 0)], "Settings", 1);
        container.Bind(first);
        container.ExecuteStep(new PlanStep("target", "action"));
        Assert.True(container.IsLocalComplete);

        var second = new Observation([new ObservedElement("B", null, 0)], "Settings", 2);
        container.Bind(second);

        Assert.Same(second, container.CurrentObservation);
        Assert.Equal(new[] { second }, container.ViewportExplorationObservations);
        Assert.Empty(container.ExecutedSteps);
        Assert.False(container.IsLocalComplete);
    }

    [Fact]
    public void Unbound_CurrentObservationNull_CandidatesEmpty()
    {
        var container = new RuntimeContainer("Page", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());

        Assert.Null(container.CurrentObservation);
        Assert.Empty(container.Candidates);
    }

    [Fact]
    public void ExecuteStep_BeforeBind_ThrowsInvalidOperation()
    {
        var container = new RuntimeContainer("Page", _ => true, (_, _, _) => new TraversalStepResult.Succeeded());

        Assert.Throws<InvalidOperationException>(() => container.ExecuteStep(new PlanStep("t", "a")));
    }

    [Fact]
    public void ExecuteStep_IsDeterministicForSameInputs()
    {
        static TraversalStepResult Executor(PlanStep step, Observation observation, ImmutableArray<ObservedElement> candidates)
            => candidates.Any(e => e.Text == step.TargetDescription)
                ? new TraversalStepResult.Succeeded()
                : new TraversalStepResult.Failed($"未找到目标「{step.TargetDescription}」。");

        var observation = new Observation([new ObservedElement("Network & Internet", null, 0)], "Settings", 1);
        var step = new PlanStep("Network & Internet", "Tap");

        var a = new RuntimeContainer("SettingsMain", o => o.SequenceNumber == 1, Executor);
        a.Bind(observation);
        var b = new RuntimeContainer("SettingsMain", o => o.SequenceNumber == 1, Executor);
        b.Bind(observation);

        var resultA = a.ExecuteStep(step);
        var resultB = b.ExecuteStep(step);

        Assert.Equal(resultA, resultB);
        Assert.Equal(a.IsLocalComplete, b.IsLocalComplete);
        Assert.Equal(a.ExecutedSteps, b.ExecutedSteps);
    }

    [Fact]
    public void LocalObstruction_AcceptsOnlyFreshGroundedEvidence_WithoutResettingProgress()
    {
        var baseline = new Observation([new ObservedElement("WiFi", null, 0)], "Settings", 1);
        var obstruction = new Observation([new ObservedElement("Dismiss", null, 0)], "Settings", 2);
        var progress = new PlanStep("WiFi", "Tap");
        var dismiss = new PlanStep("Dismiss", "Tap");
        var container = new RuntimeContainer(
            "NetworkSettings",
            observation => observation.Elements.Any(element => element.Text == "WiFi"),
            (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(baseline);
        container.ExecuteStep(progress);
        var before = container.ExecutedSteps;

        Assert.True(container.IsLocalObstructionHypothesis(obstruction, null, "Settings"));
        Assert.False(container.TryAcceptLocalObstruction(obstruction, null, "Settings", new PlanStep("Other", "Tap")));
        Assert.True(container.TryAcceptLocalObstruction(obstruction, null, "Settings", dismiss));

        Assert.Same(obstruction, container.CurrentObservation);
        Assert.Equal(before, container.ExecutedSteps);
        Assert.True(container.IsLocalComplete);
    }

    [Fact]
    public void LocalContinuity_RequiresFreshForegroundIdentityAndReconciledPage_AndMutatesOnlyOnProof()
    {
        var obstruction = new Observation([new ObservedElement("Dismiss", null, 0)], "Settings", 2);
        var continuous = new Observation([new ObservedElement("WiFi", null, 0)], "Settings", 3);
        var container = new RuntimeContainer(
            "NetworkSettings",
            observation => observation.Elements.Any(element => element.Text == "WiFi"),
            (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(obstruction);
        container.ExecuteStep(new PlanStep("Dismiss", "Tap"));
        var progress = container.ExecutedSteps;

        Assert.False(container.TryVerifyLocalContinuity(obstruction, "NetworkSettings", "Settings"));
        Assert.False(container.TryVerifyLocalContinuity(continuous with { ForegroundApplication = "Launcher" }, "NetworkSettings", "Settings"));
        Assert.False(container.TryVerifyLocalContinuity(continuous, null, "Settings"));
        Assert.False(container.TryVerifyLocalContinuity(continuous, "SettingsMain", "Settings"));
        Assert.Same(obstruction, container.CurrentObservation);
        Assert.Equal(progress, container.ExecutedSteps);

        Assert.True(container.TryVerifyLocalContinuity(continuous, "NetworkSettings", "Settings"));
        Assert.Same(continuous, container.CurrentObservation);
        Assert.Equal(progress, container.ExecutedSteps);
    }

    [Fact]
    public void LocalObstructionEscalation_UsesExistingContainerMismatchTrapVocabulary()
    {
        var obstruction = new Observation([new ObservedElement("Dismiss", null, 0)], "Settings", 2);
        var observed = new Observation([new ObservedElement("Network & Internet", null, 0)], "Settings", 3);
        var action = new DeviceAction.Tap(0);
        var container = new RuntimeContainer("NetworkSettings", _ => false, (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(obstruction);

        var trap = container.CreateLocalObstructionEscalation(observed, action, "continuity unproven");

        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(2, trap.Expected);
        Assert.Equal(3, trap.Observed);
        Assert.Equal("Container.VerifyLocalContinuity", trap.Source);
        Assert.Equal("continuity unproven", trap.Evidence);
        Assert.Equal(action, trap.LastAction);
    }

    [Fact]
    public void ViewportContinuity_AdvancesObservationWithoutBind_AndPreservesProgress()
    {
        var before = new Observation(
            [new ObservedElement("A", null, 0), new ObservedElement("B", null, 1)],
            "Settings",
            3);
        var after = new Observation(
            [new ObservedElement("D", null, 0), new ObservedElement("E", null, 1)],
            "Settings",
            4);
        var container = new RuntimeContainer(
            "ScrollableList",
            observation => observation.Elements.Any(element => element.Text is "A" or "B" or "D" or "E"),
            (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(before);
        container.ExecuteStep(new PlanStep("A", "Tap"));
        var progress = container.ExecutedSteps;

        Assert.True(container.TryVerifyViewportContinuity(after, "ScrollableList", "Settings"));

        Assert.Same(after, container.CurrentObservation);
        Assert.Equal(new[] { before, after }, container.ViewportExplorationObservations);
        Assert.Equal(progress, container.ExecutedSteps);
        Assert.True(container.IsLocalComplete);
    }

    [Fact]
    public void ViewportContinuity_RejectsStaleForegroundIdentityAndSemanticConflict_WithoutMutation()
    {
        var before = new Observation([new ObservedElement("A", null, 0)], "Settings", 3);
        var candidate = new Observation([new ObservedElement("D", null, 0)], "Settings", 4);
        var container = new RuntimeContainer(
            "ScrollableList",
            observation => observation.Elements.Any(element => element.Text is "A" or "D"),
            (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(before);
        container.ExecuteStep(new PlanStep("A", "Tap"));
        var progress = container.ExecutedSteps;

        Assert.False(container.TryVerifyViewportContinuity(before, "ScrollableList", "Settings"));
        Assert.False(container.TryVerifyViewportContinuity(candidate with { ForegroundApplication = "Launcher" }, "ScrollableList", "Settings"));
        Assert.False(container.TryVerifyViewportContinuity(
            candidate with { Elements = [new ObservedElement("Other", null, 0)] },
            "ScrollableList",
            "Settings"));
        Assert.False(container.TryVerifyViewportContinuity(candidate, "OtherPage", "Settings"));

        Assert.Same(before, container.CurrentObservation);
        Assert.Equal(new[] { before }, container.ViewportExplorationObservations);
        Assert.Equal(progress, container.ExecutedSteps);
    }

    [Fact]
    public void ViewportContinuityEscalation_UsesExistingContainerScopeTrapVocabulary()
    {
        var before = new Observation([new ObservedElement("A", null, 0)], "Settings", 3);
        var observed = new Observation([new ObservedElement("Other", null, 0)], "Settings", 4);
        var action = new DeviceAction.ScrollForward();
        var container = new RuntimeContainer("ScrollableList", _ => false, (_, _, _) => new TraversalStepResult.Succeeded());
        container.Bind(before);

        var trap = container.CreateViewportContinuityEscalation(observed, action, "viewport continuity unproven");

        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(3, trap.Expected);
        Assert.Equal(4, trap.Observed);
        Assert.Equal("Container.VerifyViewportContinuity", trap.Source);
        Assert.Equal("viewport continuity unproven", trap.Evidence);
        Assert.Equal(action, trap.LastAction);
    }
}
