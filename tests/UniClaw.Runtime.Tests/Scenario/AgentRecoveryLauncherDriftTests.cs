using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// C4 SC-P2-001 Agent Recovery 正式场景测试（scenarios/SC-P2-001-agent-recovery.md 契约）：
/// launcher-drift 变体（C1 数据）→ 全接线恢复 harness（C4：Startup 注入 RestoreRecipe / EntryStrategy 到
/// RecoveryAnchor；Recovery 组件真实接线 — 配方解析 / 位置恢复 / 验证判据）。
/// 预期观测流（mask 于 seq=4 一次性生效）：seq1 Startup 后（SettingsMain）→ seq2 observeInitial →
/// seq3 Step-1 post-action（NetworkSettings，容器绑定 seq3）→ seq4 Step-2 post-action（mask：Launcher 前台 +
/// 不可解析元素 → SemanticPage=null → Agent-scope drift）→ seq5 恢复后观测（SettingsMain）→
/// seq6 位置恢复观测（NetworkSettings，重绑挂起容器）→ seq7 续跑 Step-2 post-action（WiFiSettings）→
/// seq8 续跑 Step-3 post-action（WiFiSettingsOn）→ GoalEvidence Satisfied → Completed。
/// 预期动作链：[LaunchApp(Settings)（Startup）, Tap(0)（Step-1）, Tap(0)（Step-2，其 post-action 揭示 drift）,
/// LaunchApp(Settings)（恢复配方 Relaunch）, Tap(0)（位置恢复）, Tap(0)（续跑 Step-2 重执行）, SetSwitch(1,true)（Step-3）]。
/// 预期 Trace：19 事件（Idle→Initializing→Running→bind SettingsMain→Action-1→bind NetworkSettings→
/// Action-2→Trap→Recovery-1 ×7→Action-3→bind WiFiSettings→Action-4→Completed）。
/// 断言键：SC-P2-001 正式证据 1-7（Trap 载荷 / RecoveryId 会话 / ActionHistory / 不重启 / 证据完成 / 因果链 / 确定性重放）。
/// </summary>
public class AgentRecoveryLauncherDriftTests
{
    private const string Variant = "launcher-drift";

    // ── 证据 1-6：漂移 → Trap → 恢复（Begin/配方/observe/verify/rebind/位置恢复）→ 续跑 → 证据完成 ──────

    [Fact]
    public async Task HappyPath_DriftTrap_RecoveryVerify_Resume_Completed()
    {
        var harness = ScenarioHarness.Create(Variant);

        var final = await harness.RunAsync();

        // ── 证据 1/2：Run 进入 Running（Startup → Ready → bind SettingsMain → Step-1）───────────────
        Assert.Equal(RunState.Completed, final);
        Assert.Equal(RunState.Completed, harness.Agent.State);
        Assert.Equal(
            new RunState?[] { RunState.Idle, RunState.Initializing, RunState.Running, RunState.Completed },
            harness.Agent.Trace.Where(e => e.RunState is not null).Select(e => e.RunState));

        // ── 证据 3：Trap（Kind=UnexpectedPage, Scope=Agent; Expected=Step-1 post-action 序号 / Observed=当前序号）─
        var trap = harness.Agent.LastTrap ?? throw new InvalidOperationException("LastTrap 为 null：drift Run 未发射 Trap。");
        Assert.Equal(TrapKind.UnexpectedPage, trap.Kind);
        Assert.Equal(TrapScope.Agent, trap.Scope);
        Assert.Equal(3, trap.Expected);   // 容器绑定观测 seq（Step-1 post-action = NetworkSettings）
        Assert.Equal(4, trap.Observed);   // drift 观测 seq（Step-2 post-action 掩码）
        Assert.Equal("Agent.DetectDrift", trap.Source);
        Assert.Equal(new DeviceAction.Tap(0), trap.LastAction); // 最近已分发动作 = Step-2 的 Tap(WiFi)
        // 独立 Trap 事件（无 ActionId / RecoveryId — 与动作事件分离；StepId/ContainerId 关联上下文）
        var trapEvent = Assert.Single(harness.Agent.Trace, e => e.TrapKind is not null);
        Assert.Equal(TrapKind.UnexpectedPage, trapEvent.TrapKind);
        Assert.Equal(TrapScope.Agent, trapEvent.TrapScope);
        Assert.Equal("Step-2", trapEvent.StepId);
        Assert.Equal("NetworkSettings", trapEvent.ContainerId);
        Assert.Null(trapEvent.ActionId);
        Assert.Null(trapEvent.RecoveryId);

        // ── 证据 4：ActionHistory（7 动作：Startup 启动 → Step-1 → Step-2（其 post-action 揭示 drift）→
        //    恢复配方 Relaunch → 位置恢复 → 续跑 Step-2 重执行 → Step-3）────────────────────────────────
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication), // Startup LaunchApp
                new DeviceAction.Tap(0),                                       // Step-1 Tap(Network & Internet)
                new DeviceAction.Tap(0),                                       // Step-2 Tap(WiFi)（post-action 揭示 drift）
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication), // 恢复配方 Relaunch(Settings)
                new DeviceAction.Tap(0),                                       // 位置恢复 Tap(Network & Internet)
                new DeviceAction.Tap(0),                                       // 续跑 Step-2 Tap(WiFi) 重执行
                new DeviceAction.SetSwitch(1, true),                           // Step-3 SetSwitch(ON)
            },
            harness.Environment.ActionHistory.ToArray());

        // ── 证据 5：不重启 —— Step-1 全程只执行一次（位置恢复经组件重放，不重走 Traversal 协议）──────────
        var trace = harness.Agent.Trace.ToArray();
        Assert.Equal(19, trace.Length); // 完整因果链（见类注释）
        // StepId 由 Traversal 实例全局递增（不随恢复复位）：主循环 Step-1/2，续跑重执行获得 Step-3，
        // 最终步获得 Step-4。不重启的证据 = Step-1 仅出现一次（位置恢复经组件，未重走 Traversal）
        Assert.Equal(1, trace.Count(e => e.StepId == "Step-1")); // 主循环仅一次（无重启）
        Assert.Equal(2, trace.Count(e => e.StepId == "Step-2")); // 主循环 Action-2 + Trap 事件
        Assert.Equal(1, trace.Count(e => e.StepId == "Step-3")); // 续跑重执行的 Step-2（Action-3）
        Assert.Equal(1, trace.Count(e => e.StepId == "Step-4")); // 最终 Step-3（Action-4）

        // ── 证据 6：恢复会话（RecoveryId 事件 ×7，全部 Recovery-1）─────────────────────────────────────
        var recoveryEvents = trace.Where(e => e.RecoveryId is not null).ToArray();
        Assert.Equal(7, recoveryEvents.Length);
        Assert.All(recoveryEvents, e => Assert.Equal("Recovery-1", e.RecoveryId));
        // 低层恢复动作不伪装成 Plan 步骤（无 StepId / ActionId — 恢复走组件路径，非 Traversal 协议）
        Assert.All(recoveryEvents, e => Assert.Null(e.StepId));
        Assert.Single(recoveryEvents, e => e.Action is DeviceAction.LaunchApp); // 配方 Relaunch 动作
        var positionRestore = Assert.Single(recoveryEvents, e => e.Action is DeviceAction.Tap);
        Assert.Null(positionRestore.ActionId);
        Assert.Single(recoveryEvents, e => e.Reason == "recovery observe (seq=5)");  // 恢复后重新观测（§3）
        Assert.Single(recoveryEvents, e => e.Reason == "recovery verify: VERIFIED"); // I-9：判据检查通过
        Assert.Contains(recoveryEvents, e => e.ContainerId == "SettingsMain");       // 入口容器重绑
        Assert.Contains(recoveryEvents, e => e.ContainerId == "NetworkSettings");    // 挂起容器重绑
        Assert.Single(recoveryEvents, e => e.Reason == "recovery resume: plan index=1");

        // ── 证据 7a：Trace 因果链顺序 Drift → Trap → Recovery → Verify → Resume → Complete ──────────
        int action2Index = Array.FindIndex(trace, e => e.ActionId == "Action-2"); // drift 步骤动作
        int trapIndex = Array.FindIndex(trace, e => e.TrapKind is not null);
        int recoveryStartIndex = Array.FindIndex(trace, e => e.RecoveryId is not null);
        int verifyIndex = Array.FindIndex(trace, e => e.Reason == "recovery verify: VERIFIED");
        int resumeIndex = Array.FindIndex(trace, e => e.Reason is not null && e.Reason.StartsWith("recovery resume:", StringComparison.Ordinal));
        int completedIndex = Array.FindIndex(trace, e => e.RunState == RunState.Completed);
        Assert.True(action2Index < trapIndex, "动作事件必须先于 Trap 发射。");
        Assert.True(trapIndex < recoveryStartIndex, "Trap 发射必须先于恢复会话。");
        Assert.True(recoveryStartIndex < verifyIndex, "恢复动作分发必须先于恢复验证（dispatch ≠ success — I-9）。");
        Assert.True(verifyIndex < resumeIndex, "验证通过必须先于续跑。");
        Assert.True(resumeIndex < completedIndex, "续跑必须先于完成。");

        // ── 证据 7b：ActionId 因果链（Action-1..4 载荷顺序）─────────────────────────────────────────────
        var actionChain = trace.Where(e => e.ActionId is not null).Select(e => (Id: e.ActionId, Action: e.Action)).ToArray();
        Assert.Equal(4, actionChain.Length);
        Assert.Equal(("Action-1", (DeviceAction?)new DeviceAction.Tap(0)), actionChain[0]);          // Step-1
        Assert.Equal(("Action-2", (DeviceAction?)new DeviceAction.Tap(0)), actionChain[1]);          // Step-2（drift）
        Assert.Equal(("Action-3", (DeviceAction?)new DeviceAction.Tap(0)), actionChain[2]);          // 续跑 Step-2 重执行
        Assert.Equal(("Action-4", (DeviceAction?)new DeviceAction.SetSwitch(1, true)), actionChain[3]); // Step-3

        // ── 证据 7c：I-10 —— 恢复成功 ≠ 完成；完成由续跑 post-action 观测的证据评估驱动（seq=8）────────
        Assert.Equal(4, harness.Evidence.Count); // CP-06：seq2 初始评估 + seq3/seq7/seq8
        Assert.Equal(new long?[] { 2, 3, 7, 8 }, harness.Evidence.Select(e => e.SourceObservationSequence));
        Assert.False(harness.Evidence[0].Satisfied); // CP-06 seq2 初始评估（WiFi 未打开）
        Assert.False(harness.Evidence[1].Satisfied); // Step-1 post-action（NetworkSettings：开关未打开）
        Assert.False(harness.Evidence[2].Satisfied); // 续跑 Step-2 post-action（WiFiSettings：开关仍关 — 恢复≠完成）
        Assert.True(harness.Evidence[3].Satisfied);  // 续跑 Step-3 post-action（WiFiSettingsOn：开关开 → Completed）
        Assert.Equal("WiFi 开关已打开（观测 seq=8）。", harness.Agent.Reason);
        Assert.Equal(harness.Evidence[3].Reason, harness.Agent.Reason);
    }

    // ── 证据 7（SC-P2-001 断言 7）：确定性重放 —— 相同输入 → 相同 Trace（含恢复事件）/ ActionHistory / Evidence ─

    [Fact]
    public async Task DeterministicReplay_TwoRuns_SameTrace_ActionHistory_Evidence()
    {
        async Task<(ScenarioHarness Harness, RunState Final)> RunOnceAsync()
        {
            var harness = ScenarioHarness.Create(Variant);
            var final = await harness.RunAsync();
            return (harness, final);
        }

        var (harnessA, finalA) = await RunOnceAsync();
        var (harnessB, finalB) = await RunOnceAsync();

        Assert.Equal(RunState.Completed, finalA);
        Assert.Equal(finalA, finalB);
        Assert.Equal(harnessA.Agent.Trace.ToArray(), harnessB.Agent.Trace.ToArray());            // 含恢复事件
        Assert.Equal(harnessA.Environment.ActionHistory.ToArray(), harnessB.Environment.ActionHistory.ToArray());
        Assert.Equal(harnessA.Evidence.ToArray(), harnessB.Evidence.ToArray());
        Assert.Equal(harnessA.Agent.LastTrap, harnessB.Agent.LastTrap);
    }

    // ── 恢复规划数据（裁决 8）：Startup 注入 RecoveryAnchor；非恢复变体保持 null（Phase 1 向后兼容）──────

    [Fact]
    public async Task RecoveryAnchor_CarriesScenarioRestoreData()
    {
        var harness = ScenarioHarness.Create(Variant);

        await harness.RunAsync();

        var anchor = harness.Agent.RecoveryAnchor
            ?? throw new InvalidOperationException("RecoveryAnchor 为 null：Startup 未进入 Ready。");
        Assert.Equal("Settings", anchor.ApplicationIdentity);
        Assert.Equal("SettingsMain", anchor.ExpectedSemanticEntry);
        Assert.Equal("ForegroundApplication == Settings", anchor.VerificationCriteria);
        Assert.Equal("Relaunch(Settings)", anchor.RestoreRecipe);    // 恢复配方（恢复入口 = 启动锚点）
        Assert.Equal("Resolve(SettingsMain)", anchor.EntryStrategy); // 入口策略（恢复到入口语义页面）

        // 向后兼容：非恢复变体（Phase 1）锚点保持 3 字段（RestoreRecipe / EntryStrategy = null — 默认参数）
        var happy = ScenarioHarness.Create("happy");
        Assert.Equal(RunState.Completed, await happy.RunAsync());
        var happyAnchor = happy.Agent.RecoveryAnchor
            ?? throw new InvalidOperationException("happy RecoveryAnchor 为 null：Startup 未进入 Ready。");
        Assert.Null(happyAnchor.RestoreRecipe);
        Assert.Null(happyAnchor.EntryStrategy);
    }
}
