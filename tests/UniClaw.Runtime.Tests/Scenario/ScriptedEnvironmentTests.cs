using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B3 ScriptedEnvironment 机制测试（specs/environment SHALL + scenario-catalog Initial World）：
/// 确定性可重放 / SequenceNumber 单调 / action history 含 Rejected / SetSwitch 非开关 → Rejected /
/// 同文本 Index 稳定 / 5 个数据变体冒烟。
/// </summary>
public class ScriptedEnvironmentTests
{
    [Fact]
    public async Task ObserveAsync_SequenceNumbersAreMonotonicStartingAtOne()
    {
        var env = ScriptedEnvironmentVariants.Happy();

        var s1 = await env.ObserveAsync(CancellationToken.None);
        var s2 = await env.ObserveAsync(CancellationToken.None);
        var s3 = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal(1, s1.SequenceNumber);
        Assert.Equal(2, s2.SequenceNumber);
        Assert.Equal(3, s3.SequenceNumber);
    }

    [Fact]
    public async Task SameInputSequence_ProducesIdenticalObservationSequences()
    {
        var first = await CollectObservationSequenceAsync(ScriptedEnvironmentVariants.Happy());
        var second = await CollectObservationSequenceAsync(ScriptedEnvironmentVariants.Happy());

        // 注：不能对整个 Observation 序列用 Assert.Equal —— Observation.Elements 是 ImmutableArray，
        // 其结构相等是底层数组引用相等（.NET 已知陷阱）；确定性断言须落到标量证据。
        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
            AssertSameObservation(first[i], second[i]);
    }

    [Fact]
    public async Task ActionHistory_RecordsAllExecutedActionsIncludingRejected()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);

        // 标题元素（Index 0，SwitchState=null）→ Rejected；仍须记录进 history
        var rejected = await env.ExecuteAsync(new DeviceAction.SetSwitch(0, true), CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Rejected, rejected.Outcome);
        Assert.Equal(4, env.ActionHistory.Count);
        Assert.Equal(new DeviceAction.LaunchApp("Settings"), env.ActionHistory[0]);
        Assert.Equal(new DeviceAction.Tap(0), env.ActionHistory[1]);
        Assert.Equal(new DeviceAction.Tap(0), env.ActionHistory[2]);
        Assert.Equal(new DeviceAction.SetSwitch(0, true), env.ActionHistory[3]);
    }

    [Fact]
    public async Task SetSwitch_OnNonSwitchElement_IsRejected_AndWorldUnchanged()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var before = await env.ObserveAsync(CancellationToken.None);

        var result = await env.ExecuteAsync(new DeviceAction.SetSwitch(0, true), CancellationToken.None);
        var after = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Info);
        Assert.Equal(before.Elements, after.Elements);
    }

    [Fact]
    public async Task SameTextVariant_WifiElementsHaveStableDistinctIndices()
    {
        var env = ScriptedEnvironmentVariants.SameText();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);

        var first = await env.ObserveAsync(CancellationToken.None);
        var second = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal(2, first.Elements.Length);
        Assert.Equal("WiFi", first.Elements[0].Text);
        Assert.Null(first.Elements[0].SwitchState);
        Assert.Equal(0, first.Elements[0].Index);
        Assert.Equal("WiFi", first.Elements[1].Text);
        Assert.False(first.Elements[1].SwitchState);
        Assert.Equal(1, first.Elements[1].Index);

        // Index 是观测内稳定序位（非坐标）：重复观测保持不变
        Assert.Equal(first.Elements[0].Index, second.Elements[0].Index);
        Assert.Equal(first.Elements[1].Index, second.Elements[1].Index);
    }

    [Fact]
    public async Task HappyVariant_LaunchThenNavigate_ReachesWifiSwitchOn()
    {
        var env = ScriptedEnvironmentVariants.Happy();

        var pre = await env.ObserveAsync(CancellationToken.None);
        Assert.Equal("Launcher", pre.ForegroundApplication);
        Assert.Empty(pre.Elements);

        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var s1 = await env.ObserveAsync(CancellationToken.None);
        Assert.Equal("Settings", s1.ForegroundApplication);
        var mainElement = Assert.Single(s1.Elements);
        Assert.Equal("Network & Internet", mainElement.Text);
        Assert.Null(mainElement.SwitchState);
        Assert.Equal(0, mainElement.Index);

        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var s2 = await env.ObserveAsync(CancellationToken.None);
        Assert.Equal("WiFi", Assert.Single(s2.Elements).Text);

        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var s3 = await env.ObserveAsync(CancellationToken.None);
        Assert.Equal(2, s3.Elements.Length);
        Assert.Null(s3.Elements[0].SwitchState);
        Assert.False(s3.Elements[1].SwitchState);

        var dispatch = await env.ExecuteAsync(new DeviceAction.SetSwitch(1, true), CancellationToken.None);
        Assert.Equal(ActionResultOutcome.Dispatched, dispatch.Outcome);
        var s4 = await env.ObserveAsync(CancellationToken.None);
        Assert.True(s4.Elements[1].SwitchState);
        Assert.Null(s4.Elements[0].SwitchState);
    }

    [Fact]
    public async Task StartupForegroundFailVariant_LaunchKeepsLauncherForeground()
    {
        var env = ScriptedEnvironmentVariants.StartupForegroundFail();

        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var after = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal("Launcher", after.ForegroundApplication);
        Assert.NotEqual("Settings", after.ForegroundApplication);
    }

    [Fact]
    public async Task SwitchStuckVariant_SetSwitchOn_DispatchedButWorldUnchanged()
    {
        var env = ScriptedEnvironmentVariants.SwitchStuck();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);

        var dispatch = await env.ExecuteAsync(new DeviceAction.SetSwitch(1, true), CancellationToken.None);
        var after = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Dispatched, dispatch.Outcome);
        Assert.False(after.Elements[1].SwitchState);
    }

    [Fact]
    public async Task MissingTargetVariant_NetworkScreenHasOnlyBluetooth()
    {
        var env = ScriptedEnvironmentVariants.MissingTarget();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);

        var network = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal("Bluetooth", Assert.Single(network.Elements).Text);
        Assert.DoesNotContain(network.Elements, e => e.Text == "WiFi");
    }

    [Fact]
    public async Task SameTextVariant_SetSwitchOnTitleRejected_OnSwitchSetsTrue()
    {
        var env = ScriptedEnvironmentVariants.SameText();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);

        var onTitle = await env.ExecuteAsync(new DeviceAction.SetSwitch(0, true), CancellationToken.None);
        Assert.Equal(ActionResultOutcome.Rejected, onTitle.Outcome);

        var onSwitch = await env.ExecuteAsync(new DeviceAction.SetSwitch(1, true), CancellationToken.None);
        Assert.Equal(ActionResultOutcome.Dispatched, onSwitch.Outcome);

        var after = await env.ObserveAsync(CancellationToken.None);
        Assert.Null(after.Elements[0].SwitchState);
        Assert.True(after.Elements[1].SwitchState);
    }

    [Fact]
    public async Task Tap_WithOutOfRangeIndex_IsRejected_AndWorldUnchanged()
    {
        var env = ScriptedEnvironmentVariants.Happy();

        var result = await env.ExecuteAsync(new DeviceAction.Tap(99), CancellationToken.None);
        var after = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Rejected, result.Outcome);
        Assert.Equal("Launcher", after.ForegroundApplication);
    }

    [Fact]
    public async Task Action_WithoutTargetElementIndex_IsRejected()
    {
        var env = ScriptedEnvironmentVariants.Happy();

        var result = await env.ExecuteAsync(new DeviceAction.Tap(null), CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Rejected, result.Outcome);
        Assert.Single(env.ActionHistory);
    }

    [Fact]
    public async Task Tap_OnElementWithoutTransition_IsDispatched_ButWorldUnchanged()
    {
        var env = ScriptedEnvironmentVariants.Happy();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var before = await env.ObserveAsync(CancellationToken.None);

        // 标题元素（Index 0）无 Tap 转场：dispatch 成功但世界不变（dispatch outcome ≠ world success — 裁决 10）
        var result = await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var after = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Dispatched, result.Outcome);
        Assert.Equal(before.Elements, after.Elements);
    }

    /// <summary>逐字段断言两个观测等价（规避 ImmutableArray 引用相等陷阱；ObservedElement 是纯标量 record，值相等可靠）。</summary>
    private static void AssertSameObservation(Observation expected, Observation actual)
    {
        Assert.Equal(expected.ForegroundApplication, actual.ForegroundApplication);
        Assert.Equal(expected.SequenceNumber, actual.SequenceNumber);
        Assert.Equal(expected.Elements.Length, actual.Elements.Length);
        for (var i = 0; i < expected.Elements.Length; i++)
            Assert.Equal(expected.Elements[i], actual.Elements[i]);
    }

    private static async Task<ImmutableArray<Observation>> CollectObservationSequenceAsync(ScriptedEnvironment env)
    {
        var builder = ImmutableArray.CreateBuilder<Observation>();
        builder.Add(await env.ObserveAsync(CancellationToken.None));
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));
        await env.ExecuteAsync(new DeviceAction.SetSwitch(1, true), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));
        return builder.ToImmutable();
    }
}
