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

    // ── C1 launcher-drift 变体：一次性观测掩码（seq=4 注入 Launcher 前台 + 不可解析元素 → 语义页面 null）──

    [Fact]
    public async Task LauncherDriftVariant_LaunchApp_ReachesSettingsMain()
    {
        var env = ScriptedEnvironmentVariants.LauncherDrift();

        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var after = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal("Settings", after.ForegroundApplication);
        Assert.Equal("Network & Internet", Assert.Single(after.Elements).Text);
    }

    [Fact]
    public async Task LauncherDriftVariant_Tap_ReachesNetworkSettings()
    {
        var env = ScriptedEnvironmentVariants.LauncherDrift();
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);

        var network = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal("WiFi", Assert.Single(network.Elements).Text);
    }

    [Fact]
    public async Task LauncherDriftVariant_Step2PostAction_LauncherForeground_UnresolvableElements()
    {
        var env = ScriptedEnvironmentVariants.LauncherDrift();
        var seq = await CollectLauncherDriftSequenceAsync(env);

        // seq4 = Step-2 post-action：掩码观测 — Launcher 前台 + 不可解析元素（Phone/Messages）
        var drift = seq[3];
        Assert.Equal(4, drift.SequenceNumber);
        Assert.Equal("Launcher", drift.ForegroundApplication);
        Assert.Equal(2, drift.Elements.Length);
        Assert.Equal("Phone", drift.Elements[0].Text);
        Assert.Equal("Messages", drift.Elements[1].Text);
        Assert.Null(drift.Elements[0].SwitchState);
        Assert.Null(drift.Elements[1].SwitchState);
    }

    [Fact]
    public async Task LauncherDriftVariant_DriftObservation_ResolvesToNullPage()
    {
        var env = ScriptedEnvironmentVariants.LauncherDrift();
        var seq = await CollectLauncherDriftSequenceAsync(env);

        // drift 前置条件：不可解析元素 → SemanticPage=null（空元素 + "Launcher" 会解析为 "Launcher"，压掉 drift）
        Assert.Null(ScenarioIdentity.ResolveSemanticPage(seq[3]));
        // 对照：掩码前后观测均正常解析为语义页面
        Assert.Equal("SettingsMain", ScenarioIdentity.ResolveSemanticPage(seq[1]));
        Assert.Equal("NetworkSettings", ScenarioIdentity.ResolveSemanticPage(seq[2]));
        Assert.Equal("SettingsMain", ScenarioIdentity.ResolveSemanticPage(seq[4]));
    }

    [Fact]
    public async Task LauncherDriftVariant_Relaunch_RestoresSettingsMain()
    {
        var env = ScriptedEnvironmentVariants.LauncherDrift();
        var seq = await CollectLauncherDriftSequenceAsync(env);

        // mask 一次性：Relaunch 后恢复到 SettingsMain（不重复 Launcher/Phone/Messages）
        Assert.Equal(5, seq[4].SequenceNumber);
        Assert.Equal("Settings", seq[4].ForegroundApplication);
        Assert.Equal("Network & Internet", Assert.Single(seq[4].Elements).Text);
    }

    [Fact]
    public async Task LauncherDriftVariant_NoPhantomActions_InActionHistory()
    {
        var env = ScriptedEnvironmentVariants.LauncherDrift();
        await CollectLauncherDriftSequenceAsync(env);

        // 掩码观测不是动作：ActionHistory 只含实际执行动作（无幻影条目）
        Assert.Equal(4, env.ActionHistory.Count);
        Assert.Equal(new DeviceAction.LaunchApp("Settings"), env.ActionHistory[0]);
        Assert.Equal(new DeviceAction.Tap(0), env.ActionHistory[1]);
        Assert.Equal(new DeviceAction.Tap(0), env.ActionHistory[2]);
        Assert.Equal(new DeviceAction.LaunchApp("Settings"), env.ActionHistory[3]);
    }

    [Fact]
    public async Task LauncherDriftVariant_SequenceNumbers_Monotonic()
    {
        var env = ScriptedEnvironmentVariants.LauncherDrift();
        var seq = await CollectLauncherDriftSequenceAsync(env);

        // 掩码观测同样占用序号：1..5 单调（确定性 — 裁决 6）
        Assert.Equal(5, seq.Length);
        for (var i = 0; i < seq.Length; i++)
            Assert.Equal(i + 1, seq[i].SequenceNumber);
    }

    [Fact]
    public async Task LauncherDriftVariant_DeterministicReplay()
    {
        var first = await CollectLauncherDriftSequenceAsync(ScriptedEnvironmentVariants.LauncherDrift());
        var second = await CollectLauncherDriftSequenceAsync(ScriptedEnvironmentVariants.LauncherDrift());

        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
            AssertSameObservation(first[i], second[i]);
    }

    // ── C2 flicker-target 变体：观测侧 flicker — seq3 只见 Bluetooth，seq4 见 Bluetooth + WiFi（重试命中面）──

    [Fact]
    public async Task FlickerTargetVariant_FirstNetworkSettingsObserve_OnlyBluetooth()
    {
        var env = ScriptedEnvironmentVariants.FlickerTarget();
        var seq = await CollectFlickerSequenceAsync(env);

        // seq3 = Step-1 post-action（Network Settings 首次观测，掩码：无 WiFi — flicker 瞬间）
        var first = seq[2];
        Assert.Equal(3, first.SequenceNumber);
        Assert.Equal("Settings", first.ForegroundApplication);
        var element = Assert.Single(first.Elements);
        Assert.Equal("Bluetooth", element.Text);
        Assert.Null(element.SwitchState);
        Assert.DoesNotContain(first.Elements, e => e.Text == "WiFi");
    }

    [Fact]
    public async Task FlickerTargetVariant_SecondObserve_BluetoothAndWiFi()
    {
        var env = ScriptedEnvironmentVariants.FlickerTarget();
        var seq = await CollectFlickerSequenceAsync(env);

        // seq4 = 重试 re-observe（掩码：WiFi 出现 — 世界抖动恢复）
        var second = seq[3];
        Assert.Equal(4, second.SequenceNumber);
        Assert.Equal("Settings", second.ForegroundApplication);
        Assert.Equal(2, second.Elements.Length);
        Assert.Equal("Bluetooth", second.Elements[0].Text);
        Assert.Equal(0, second.Elements[0].Index);
        Assert.Equal("WiFi", second.Elements[1].Text);
        Assert.Equal(1, second.Elements[1].Index); // 重试 re-resolve 的 grounding 位置
        Assert.Null(second.Elements[1].SwitchState); // WiFi 是列表项，非开关
    }

    [Fact]
    public async Task FlickerTargetVariant_ResolveSemanticPage_BothObservations()
    {
        var env = ScriptedEnvironmentVariants.FlickerTarget();
        var seq = await CollectFlickerSequenceAsync(env);

        // 首次观测（单元素 "Bluetooth"）→ 单元素规则命中 → "NetworkSettings"（容器保持绑定）
        Assert.Equal("NetworkSettings", ScenarioIdentity.ResolveSemanticPage(seq[2]));
        // 恢复观测（2 元素 [Bluetooth, WiFi]）→ 2 元素规则以 elements[1].SwitchState 判别：
        // WiFi SwitchState=null（列表项非开关）→ 无匹配页面 = null。
        // 注：SC-P2-002 的 retry 在 Traversal Step-scope（仅 Select re-resolve），不咨询页面身份 —
        //     null 解析不影响重试命中；Step-scope retry 后也不触发 Agent-scope drift（前台仍为 Settings）。
        Assert.Null(ScenarioIdentity.ResolveSemanticPage(seq[3]));
    }

    [Fact]
    public async Task FlickerTargetVariant_TapWifi_IndexOne_SucceedsOnSecondObserve()
    {
        var env = ScriptedEnvironmentVariants.FlickerTarget();
        await CollectFlickerSequenceAsync(env);

        // 第二次观测（seq4）中 WiFi 位于 Index=1 — 重试 re-resolve 的 grounding 结果 → Tap(1) 派发成功
        var tap = await env.ExecuteAsync(new DeviceAction.Tap(1), CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Dispatched, tap.Outcome);
        Assert.Equal(3, env.ActionHistory.Count); // LaunchApp, Tap(0)[Network], Tap(1)[WiFi]
        Assert.Equal(new DeviceAction.Tap(1), env.ActionHistory[2]);
    }

    [Fact]
    public async Task FlickerTargetVariant_DeterministicReplay()
    {
        var first = await CollectFlickerSequenceAsync(ScriptedEnvironmentVariants.FlickerTarget());
        var second = await CollectFlickerSequenceAsync(ScriptedEnvironmentVariants.FlickerTarget());

        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
            AssertSameObservation(first[i], second[i]);
    }

    // ── C3 unrecoverable 变体：Relaunch Dispatched 但观测仍为 Launcher（恢复无效 → 验证失败面）─────────

    [Fact]
    public async Task UnrecoverableVariant_DriftAtSeq4_LauncherForeground_UnresolvableElements()
    {
        var env = ScriptedEnvironmentVariants.Unrecoverable();
        var seq = await CollectUnrecoverableSequenceAsync(env);

        // seq4 = Step-2 post-action：与 launcher-drift 相同的 drift 掩码（Launcher 前台 + 不可解析元素）
        var drift = seq[3];
        Assert.Equal(4, drift.SequenceNumber);
        Assert.Equal("Launcher", drift.ForegroundApplication);
        Assert.Equal(2, drift.Elements.Length);
        Assert.Equal("Phone", drift.Elements[0].Text);
        Assert.Equal("Messages", drift.Elements[1].Text);
        Assert.Null(ScenarioIdentity.ResolveSemanticPage(drift)); // drift 前置条件：SemanticPage=null
    }

    [Fact]
    public async Task UnrecoverableVariant_RelaunchObserve_StillLauncher_NotRestored()
    {
        var env = ScriptedEnvironmentVariants.Unrecoverable();
        var seq = await CollectUnrecoverableSequenceAsync(env);

        // Relaunch 动作照常 Dispatched（fake 切换屏幕状态）…
        Assert.Equal(4, env.ActionHistory.Count);
        Assert.Equal(new DeviceAction.LaunchApp("Settings"), env.ActionHistory[3]);
        // …但 seq5 恢复观测掩码显示仍未恢复（恢复动作无效 — 世界不配合，裁决 10 / I-9）
        var postRelaunch = seq[4];
        Assert.Equal(5, postRelaunch.SequenceNumber);
        Assert.Equal("Launcher", postRelaunch.ForegroundApplication); // 不是 "Settings"
        Assert.Equal("Phone", postRelaunch.Elements[0].Text);
        Assert.Equal("Messages", postRelaunch.Elements[1].Text);
        Assert.Null(ScenarioIdentity.ResolveSemanticPage(postRelaunch));
    }

    [Fact]
    public async Task UnrecoverableVariant_DeterministicReplay()
    {
        var first = await CollectUnrecoverableSequenceAsync(ScriptedEnvironmentVariants.Unrecoverable());
        var second = await CollectUnrecoverableSequenceAsync(ScriptedEnvironmentVariants.Unrecoverable());

        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
            AssertSameObservation(first[i], second[i]);
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

    /// <summary>C3 标准流程：startup 观测（seq1 Launcher）→ LaunchApp → 初始观测（seq2 SettingsMain）
    /// → Tap(Network) → 步骤观测（seq3 NetworkSettings）→ Tap(WiFi) → Step-2 post-action（seq4 = drift 掩码）
    /// → Relaunch → 恢复观测（seq5 = 未恢复掩码：仍 Launcher）。</summary>
    private static async Task<ImmutableArray<Observation>> CollectUnrecoverableSequenceAsync(ScriptedEnvironment env)
    {
        var builder = ImmutableArray.CreateBuilder<Observation>();
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq1 Launcher（启动前）
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq2 SettingsMain
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);                       // Network & Internet
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq3 NetworkSettings
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);                       // WiFi
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq4 drift 掩码
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);        // Relaunch（Dispatched）
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq5 未恢复掩码
        return builder.ToImmutable();
    }

    /// <summary>C2 标准流程：startup 观测（seq1 Launcher）→ LaunchApp → 初始观测（seq2 SettingsMain）
    /// → Tap(Network & Internet) → Step-1 post-action（seq3 = flicker 首次观测：仅 Bluetooth）
    /// → 重试 re-observe（seq4 = Bluetooth + WiFi）。</summary>
    private static async Task<ImmutableArray<Observation>> CollectFlickerSequenceAsync(ScriptedEnvironment env)
    {
        var builder = ImmutableArray.CreateBuilder<Observation>();
        builder.Add(await env.ObserveAsync(CancellationToken.None));              // seq1 Launcher（启动前）
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));              // seq2 SettingsMain
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);  // Network & Internet
        builder.Add(await env.ObserveAsync(CancellationToken.None));              // seq3 flicker 首次观测（掩码）
        builder.Add(await env.ObserveAsync(CancellationToken.None));              // seq4 重试 re-observe（掩码）
        return builder.ToImmutable();
    }

    /// <summary>C1 标准流程：startup 观测（seq1）→ LaunchApp → 初始观测（seq2）→ Tap(Network) → 步骤观测（seq3）
    /// → Tap(WiFi) → Step-2 post-action 观测（seq4 = drift 掩码）→ Relaunch → 恢复观测（seq5）。</summary>
    private static async Task<ImmutableArray<Observation>> CollectLauncherDriftSequenceAsync(ScriptedEnvironment env)
    {
        var builder = ImmutableArray.CreateBuilder<Observation>();
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq1 Launcher（启动前）
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq2 SettingsMain
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);                       // Network & Internet
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq3 NetworkSettings
        await env.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);                       // WiFi
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq4 drift 掩码
        await env.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        builder.Add(await env.ObserveAsync(CancellationToken.None));                                   // seq5 SettingsMain 恢复
        return builder.ToImmutable();
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
