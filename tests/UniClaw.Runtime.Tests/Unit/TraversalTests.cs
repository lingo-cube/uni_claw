using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
// 注：命名空间 UniClaw.Runtime.Traversal 与类 Traversal 同名；UniClaw.Runtime.Container 与类 Container 同名——
// 本测试位于 UniClaw.Runtime 之下，裸名会先绑定到命名空间（CS0118），故用类型别名引用类。
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B6 Traversal 机制测试（specs/container-traversal SHALL）：单步协议 Select→Check→Execute→Observe→Verify→Branch /
/// journal / grounding 消歧（SC-P1-005）/ 失败结构化表达（SC-P1-004）/ 确定性；协议 token 由 Traversal 定义。
/// </summary>
public class TraversalTests
{
    [Fact]
    public async Task Happy_TapStep_Succeeds_WithCorrectTargetIndex_AndJournal()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var settingsMain = await env.ObserveAsync(CancellationToken.None); // seq 1
        var traversal = new RuntimeTraversal(env);

        var result = traversal.ExecuteStep(new PlanStep("Network & Internet", "Tap"), settingsMain, settingsMain.Elements);

        Assert.IsType<TraversalStepResult.Succeeded>(result);
        Assert.Equal(new DeviceAction.Tap(0), env.ActionHistory[^1]); // Tap 携带正确 TargetElementIndex
        Assert.Equal(2, env.ActionHistory.Count); // LaunchApp(前置) + Tap
        var entry = Assert.Single(traversal.Journal);
        Assert.False(string.IsNullOrWhiteSpace(entry.StepId));
        Assert.Equal(0, entry.SelectedElementIndex);
        Assert.Equal(new DeviceAction.Tap(0), entry.DispatchedAction);
        Assert.Equal(2, entry.PostActionObservation!.SequenceNumber); // 动作后重新观察（§3）：1 → 2
        Assert.IsType<TraversalStepResult.Succeeded>(entry.Result);
    }

    [Fact]
    public async Task MissingTarget_CheckFails_NoActionDispatched()
    {
        var env = ScriptedEnvironmentVariants.MissingTarget();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var network = await env.ObserveAsync(CancellationToken.None);
        Assert.Equal("Bluetooth", Assert.Single(network.Elements).Text);
        var traversal = new RuntimeTraversal(env);

        var result = traversal.ExecuteStep(new PlanStep("WiFi", "Tap"), network, network.Elements);

        var failed = Assert.IsType<TraversalStepResult.Failed>(result);
        Assert.False(string.IsNullOrWhiteSpace(failed.Reason)); // 非空原因（SC-P1-004）
        Assert.Equal(2, env.ActionHistory.Count); // 零动作分发（history 仅 Launch + 上一步 Tap）
        var entry = Assert.Single(traversal.Journal);
        Assert.Null(entry.SelectedElementIndex);
        Assert.Null(entry.DispatchedAction);
        Assert.Null(entry.PostActionObservation);
        Assert.IsType<TraversalStepResult.Failed>(entry.Result);
    }

    [Fact]
    public async Task SameText_SetSwitch_SelectsStateBearingElement()
    {
        var env = ScriptedEnvironmentVariants.Happy(); // same-text 世界数据与 happy 相同（catalog）
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var wifi = await env.ObserveAsync(CancellationToken.None);
        Assert.Equal(2, wifi.Elements.Length); // 标题 + 开关两个 "WiFi"
        var traversal = new RuntimeTraversal(env);

        var result = traversal.ExecuteStep(new PlanStep("WiFi", "SetSwitch true"), wifi, wifi.Elements);

        Assert.IsType<TraversalStepResult.Succeeded>(result);
        var setSwitch = Assert.IsType<DeviceAction.SetSwitch>(env.ActionHistory[^1]);
        Assert.Equal(1, setSwitch.TargetElementIndex); // 开关元素 Index（≠ 标题 Index 0）— SC-P1-005
        Assert.True(setSwitch.TargetState);
        var entry = Assert.Single(traversal.Journal);
        Assert.Equal(1, entry.SelectedElementIndex);
        Assert.True(entry.PostActionObservation!.Elements[1].SwitchState); // 开关 true
        Assert.Null(entry.PostActionObservation.Elements[0].SwitchState);  // 标题仍 null
    }

    [Fact]
    public async Task SwitchStuck_SetSwitchStep_Succeeds_WorldUnchanged()
    {
        var env = ScriptedEnvironmentVariants.SwitchStuck();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var wifi = await env.ObserveAsync(CancellationToken.None);
        var traversal = new RuntimeTraversal(env);

        var result = traversal.ExecuteStep(new PlanStep("WiFi", "SetSwitch true"), wifi, wifi.Elements);

        // dispatch ≠ world success（裁决 10）：步骤 Succeed；Run 失败判定在 Agent/evaluator（SC-P1-003 负向）
        Assert.IsType<TraversalStepResult.Succeeded>(result);
        var post = Assert.Single(traversal.Journal).PostActionObservation!;
        Assert.False(post.Elements[1].SwitchState); // 世界未变（物理卡住）
    }

    [Fact]
    public async Task SetSwitchOnNonSwitchCandidate_EnvironmentRejects_StepFailsWithReason()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var network = await env.ObserveAsync(CancellationToken.None); // "WiFi"（SwitchState=null，导航项）
        var traversal = new RuntimeTraversal(env);

        // 无 state-bearing 候选时 SetSwitch 落到唯一匹配 → 环境按物理能力拒绝（SC-P1-005 错误路径）
        var result = traversal.ExecuteStep(new PlanStep("WiFi", "SetSwitch true"), network, network.Elements);

        var failed = Assert.IsType<TraversalStepResult.Failed>(result);
        Assert.False(string.IsNullOrWhiteSpace(failed.Reason));
        Assert.Equal(3, env.ActionHistory.Count); // LaunchApp + Tap(前置) + SetSwitch(尝试分发、被拒)
        Assert.Equal(new DeviceAction.SetSwitch(0, true), env.ActionHistory[^1]);
        Assert.IsType<TraversalStepResult.Failed>(Assert.Single(traversal.Journal).Result);
    }

    [Fact]
    public async Task UnknownActionToken_StepFails_NoActionDispatched()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var settingsMain = await env.ObserveAsync(CancellationToken.None);
        var traversal = new RuntimeTraversal(env);

        var result = traversal.ExecuteStep(new PlanStep("Network & Internet", "Drag"), settingsMain, settingsMain.Elements);

        Assert.IsType<TraversalStepResult.Failed>(result);
        Assert.Single(env.ActionHistory); // 仅 LaunchApp——协议解析失败不产生动作
    }

    [Fact]
    public async Task MultiStep_StepIdsUniqueInOrder_PostObservationFeedsNextStep()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var settingsMain = await env.ObserveAsync(CancellationToken.None); // seq 1
        var traversal = new RuntimeTraversal(env);

        var r1 = traversal.ExecuteStep(new PlanStep("Network & Internet", "Tap"), settingsMain, settingsMain.Elements);
        var s2 = traversal.Journal[0].PostActionObservation!; // journal 携带 post-action Observation（B7 组合模式）
        var r2 = traversal.ExecuteStep(new PlanStep("WiFi", "Tap"), s2, s2.Elements);

        Assert.IsType<TraversalStepResult.Succeeded>(r1);
        Assert.IsType<TraversalStepResult.Succeeded>(r2);
        Assert.Equal("WiFi", Assert.Single(s2.Elements).Text); // 上一步后世界已到 Network Settings
        Assert.Equal(2, traversal.Journal.Count);
        Assert.Equal("Step-1", traversal.Journal[0].StepId);
        Assert.Equal("Step-2", traversal.Journal[1].StepId);
        Assert.NotEqual(traversal.Journal[0].StepId, traversal.Journal[1].StepId); // 步标识唯一
        Assert.Equal(3, traversal.Journal[1].PostActionObservation!.SequenceNumber); // 单调推进 1→2→3
    }

    [Fact]
    public async Task ExecuteStep_IsDeterministicAcrossInstances()
    {
        async Task<(TraversalStepResult Result, ImmutableArray<TraversalJournalEntry> Journal)> RunAsync()
        {
            var env = ScriptedEnvironmentVariants.Happy();
            await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
            var s1 = await env.ObserveAsync(CancellationToken.None);
            var traversal = new RuntimeTraversal(env);
            var result = traversal.ExecuteStep(new PlanStep("Network & Internet", "Tap"), s1, s1.Elements);
            return (result, traversal.Journal.ToImmutableArray());
        }

        var (resultA, journalA) = await RunAsync();
        var (resultB, journalB) = await RunAsync();

        Assert.Equal(resultA, resultB);
        Assert.Equal(journalA.Select(e => e.StepId), journalB.Select(e => e.StepId));
        Assert.Equal(journalA.Select(e => e.SelectedElementIndex), journalB.Select(e => e.SelectedElementIndex));
        Assert.Equal(journalA.Select(e => e.PostActionObservation!.SequenceNumber), journalB.Select(e => e.PostActionObservation!.SequenceNumber));
    }

    [Fact]
    public void ExecuteStep_MatchesContainerExecutorDelegateShape()
    {
        var traversal = new RuntimeTraversal(new ScriptedEnvironment("Launcher", null, []));
        // 编译期证明：方法组可转换为 B5 Container 注入的 executor 形状（同步 delegate）
        Func<PlanStep, Observation, ImmutableArray<ObservedElement>, TraversalStepResult> asExecutorDelegate = traversal.ExecuteStep;
        Assert.NotNull(asExecutorDelegate);
    }

    [Fact]
    public async Task ContainerExecutorComposition_FullStep_RelaysSucceeded()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var settingsMain = await env.ObserveAsync(CancellationToken.None);

        var traversal = new RuntimeTraversal(env);
        var container = new RuntimeContainer("SettingsMain", o => o.ForegroundApplication == "Settings", traversal.ExecuteStep);
        container.Bind(settingsMain);

        var result = container.ExecuteStep(new PlanStep("Network & Internet", "Tap"));

        Assert.IsType<TraversalStepResult.Succeeded>(result);
        Assert.True(container.IsLocalComplete);
        Assert.Equal(new DeviceAction.Tap(0), env.ActionHistory[^1]); // LaunchApp(前置) + Tap
        Assert.Single(traversal.Journal);
    }

    [Fact]
    public void PublicSurface_NoCoordinateOrHierarchyModels()
    {
        var banned = new[] { "X", "Y", "Rect", "Bounds", "Width", "Height", "Parent", "Children" };
        var memberNames = typeof(RuntimeTraversal)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)
            .Concat(typeof(TraversalJournalEntry).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name));

        Assert.DoesNotContain(memberNames, name => banned.Contains(name, StringComparer.Ordinal));
    }
}
