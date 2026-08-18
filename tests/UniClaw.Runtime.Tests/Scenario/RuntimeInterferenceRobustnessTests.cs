using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// 执行期干扰鲁棒性验证（2026-08-16，PROJECT_LEADER 场景→fixture 验证；零生产改动）。
/// 四类真实用户干扰场景的确定性机制边界变体，检验已毕业能力能否 handle / escalate / 显式终止：
///   1. UI 连续卡死（transport TimedOut）→ SC-P3-001 uncertain-action 语义（世界推进则完成；世界自环则确定性失败）
///   2. 退桌面反复打断（恢复后再次 drift）→ SC-P2-001 单次恢复尝试边界（不递归恢复，显式失败）
///   3. 未知弹窗（非 supported Popup）→ SC-P3-002 escalate 边界（不伪造处理/完成，Agent 显式失败）
///   4. H5/广告页伪装（身份歧义）→ Plan≠Reality / Grounding≠Identity authority 边界（不伪造完成）
/// 每变体验证：确定性终止、无盲重试、无无限循环、无伪造完成、显式原因、单次恢复边界。
/// </summary>
public class RuntimeInterferenceRobustnessTests
{
    // ── 场景 2a：UI 连续卡死（世界照常推进）——TimedOut 是 dispatch 不确定，不阻塞世界证据 ──────

    [Fact]
    public async Task RepeatTimeout_WorldAdvances_TimedOutDoesNotBlock_CompletesFromFreshEvidence()
    {
        var harness = ScenarioHarness.Create("repeat-timeout-advances");

        var final = await harness.RunAsync();

        // SC-P3-001：TimedOut 不阻止世界转场；fresh post-action Observation 推进 → GoalEvidence 完成
        Assert.Equal(RunState.Completed, final);
        // 每个 Plan 步骤恰好派发一次（无盲重试 — SC-P3-001：不自动重派发同一动作）
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication),
                new DeviceAction.Tap(0),          // Step-1 Tap(Network & Internet) — TimedOut
                new DeviceAction.Tap(0),          // Step-2 Tap(WiFi) — TimedOut
                new DeviceAction.SetSwitch(1, true), // Step-3 SetSwitch(ON) — TimedOut
            },
            harness.Environment.ActionHistory.ToArray());
        Assert.True(harness.Evidence.Last().Satisfied, "最终 post-action 观测（开关 true）必须产生 Satisfied GoalEvidence。");
    }

    // ── 场景 2b：UI 连续卡死（世界自环）——确定性终止：无盲重试、无无限循环、无伪造完成 ──────────

    [Fact]
    public async Task RepeatTimeout_WorldStuck_FailsDeterministically_NoBlindRetry_NoFabricatedCompletion()
    {
        var harness = ScenarioHarness.Create("repeat-timeout-stuck");

        var final = await harness.RunAsync();

        // 世界卡死：Step-1 TimedOut + 自环 → post-action 仍 SettingsMain → Step-2 Tap(WiFi) grounding 失败
        Assert.Equal(RunState.Failed, final);
        Assert.NotEqual(RunState.Completed, harness.Agent.State);
        // 每个已尝试动作恰好一次（无盲重试）；Step-2 因无候选未派发（无无限循环 — Plan 有界）
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication),
                new DeviceAction.Tap(0), // Step-1 仅一次（TimedOut 不触发重派发）
            },
            harness.Environment.ActionHistory.ToArray());
        // 显式失败原因（结构化，非静默 — §45）
        Assert.False(string.IsNullOrWhiteSpace(harness.Agent.Reason));
        // 无伪造完成事件
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Completed);
    }

    // ── 场景 3：退桌面反复打断——恢复成功后再次 drift → 单次恢复尝试边界，显式失败 ───────────────

    [Fact]
    public async Task DriftAgain_SecondDriftAfterRecovery_FailsWithoutSecondRecovery()
    {
        var harness = ScenarioHarness.Create("drift-again");

        var final = await harness.RunAsync();

        // 首次 drift → Trap；恢复成功（Recovery-1）后 resume 中再次 drift → 不递归恢复，显式失败
        Assert.Equal(RunState.Failed, final);
        Assert.NotEqual(RunState.Completed, harness.Agent.State);
        // 第一次 drift 的 Trap 已发射（UnexpectedPage / Agent scope）
        var trap = harness.Agent.LastTrap ?? throw new InvalidOperationException("LastTrap 为 null：首次 drift 未发射 Trap。");
        Assert.Equal(TrapKind.UnexpectedPage, trap.Kind);
        Assert.Equal(TrapScope.Agent, trap.Scope);
        // 恢复会话存在（Recovery-1）且只有一次（单次恢复尝试 — HG-2 边界：无恢复重试）
        var recoveryEvents = harness.Agent.Trace.Where(e => e.RecoveryId is not null).ToArray();
        Assert.NotEmpty(recoveryEvents);
        Assert.All(recoveryEvents, e => Assert.Equal("Recovery-1", e.RecoveryId));
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RecoveryId == "Recovery-2");
        // 显式原因：恢复后再次 Agent-scope drift（不递归恢复、不伪造续跑）
        Assert.Contains("恢复后再次 Agent-scope drift", harness.Agent.Reason);
    }

    // ── 场景 1：未知弹窗（非 supported Popup）——escalate 边界：不伪造处理/完成，Agent 显式失败 ──

    [Fact]
    public async Task UnknownOverlay_NotSupportedPopup_EscalatesToExplicitFailure_NoFabricatedHandling()
    {
        var harness = ScenarioHarness.Create("unknown-overlay");

        var final = await harness.RunAsync();

        // 覆盖层（前台 Settings、页面 Unknown、计划中无 Dismiss handling step）→ 不进入 SC-P3-002
        // local handling → Step-2 目标无法 grounding → Agent 显式失败
        Assert.Equal(RunState.Failed, final);
        Assert.NotEqual(RunState.Completed, harness.Agent.State);
        // 无 Dismiss / handling 动作被派发（未知弹窗不伪造处理）
        Assert.DoesNotContain(
            harness.Environment.ActionHistory,
            a => a is DeviceAction.Tap tap && tap.TargetElementIndex == 1); // 覆盖层 "知道了"（Index 1）未被点击
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication),
                new DeviceAction.Tap(0), // Step-1 仅一次；Step-2 无候选未派发
            },
            harness.Environment.ActionHistory.ToArray());
        // 显式失败原因
        Assert.False(string.IsNullOrWhiteSpace(harness.Agent.Reason));
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Completed);
    }

    // ── 场景 4：H5/广告页伪装（身份歧义）——不伪造完成；grounding 证据不足 → 显式失败 ────────────

    [Fact]
    public async Task SpoofedPage_IdentityAmbiguity_DoesNotFabricateCompletion()
    {
        var harness = ScenarioHarness.Create("spoofed-page");

        var final = await harness.RunAsync();

        // 广告页单元素 "WiFi" 被显式 identity 规则误判 NetworkSettings；Step-3 SetSwitch grounding
        // 选中唯一 "WiFi" 候选（Index 0），但该元素非开关承载（SwitchState=null）→ Environment 按
        // 物理能力语义 Rejected（SC-P1-005）→ 世界不变 → GoalEvidence 不满足 → 显式失败。
        // 验证：身份歧义 + 伪造元素不伪造完成（Grounding 是 evidence，不是 authority；dispatch ≠ world success）。
        Assert.Equal(RunState.Failed, final);
        Assert.NotEqual(RunState.Completed, harness.Agent.State);
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication),
                new DeviceAction.Tap(0),        // Step-1 进入广告页
                new DeviceAction.Tap(0),        // Step-2 Tap("WiFi" 文本) 无世界效果
                new DeviceAction.SetSwitch(0, true), // Step-3 派发到伪装元素（Index 0）→ Rejected
            },
            harness.Environment.ActionHistory.ToArray());
        // 显式失败原因；无伪造完成
        Assert.False(string.IsNullOrWhiteSpace(harness.Agent.Reason));
        Assert.DoesNotContain(harness.Agent.Trace, e => e.RunState == RunState.Completed);
    }
}
