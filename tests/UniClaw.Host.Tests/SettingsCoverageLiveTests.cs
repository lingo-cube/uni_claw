using System.Diagnostics;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>
/// AGT-010 真机终考（ENVIRONMENT 门控 DSH_TEST_PERCEPTION_LIVE=1，需模拟器）：
/// 在真实 emulator 上复跑 plans §3 主路径——neg-c 场景（饥饿滚动候选冲击
/// 列表底部）+ AGT-010 修复（多信号 RouteKey）。历史基线：e1/d1/d3/d4 在
/// step 20 确定性失败于「Security & privacy」进入验证（页面标题与根页同为
/// "Settings"，标题指纹无法区分，digest 55291AC…）。
/// 本测试断言修复后该进入**真实可验证**；终局诚实（完成或有界停止）。
/// 咨询侧 = 本地指令跟随 double（directive → 单步 Act；模型智能不在本
/// 验收面内——被验证的是 director/kernel/feed/effect 真实链）。
/// </summary>
[Collection("LiveDevice")]
public sealed class SettingsCoverageLiveTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") == "1";

    private static bool NoAdb =>
        Environment.GetEnvironmentVariable("DSH_TEST_NO_ADB") == "1";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    [Fact]
    public void NegC_Rerun_CollisionPageEntryVerifies_ScrollBottomReached()
    {
        if (!Enabled || NoAdb)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用或 DSH_TEST_NO_ADB=1");
            return;
        }

        var repo = RepoRoot();
        var selected = LiveDeviceSelector.Resolve();
        Assert.True(selected.IsUsable, $"{selected.Status}: {selected.Detail}");
        var deviceId = selected.Serial!;
        output.WriteLine($"device = {deviceId} (api {selected.ApiLevel})");

        // 前置：干净 Settings 根页（-S 强停；残留子页会污染首屏候选）
        Adb(deviceId, "shell am start -S -a android.settings.SETTINGS");
        Thread.Sleep(TimeSpan.FromSeconds(3));
        var size = Adb(deviceId, "shell wm size").Output;
        var dims = System.Text.RegularExpressions.Regex.Match(size, @"(\d+)x(\d+)");
        Assert.True(dims.Success, size);
        var width = int.Parse(dims.Groups[1].Value);
        var height = int.Parse(dims.Groups[2].Value);
        output.WriteLine($"viewport = {width}x{height}");

        // neg-c 同源配置（饥饿滚动候选冲击底部），RootRoute 迁移为 rk1 语义
        var configPath = Path.Combine(Path.GetTempPath(), $"settings-coverage-live-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(configPath, $"""
            configVersion: "1"
            session:
              taskTitle: 遍历设置菜单覆盖测试
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: 60
              maxConsultRounds: 60
              maxScrolls: 8
              maxConsecutiveFailures: 3
              maxDirectiveRetries: 1
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 99
              secondLevelPages: 2
              backNavigation: true
              repeatedEntries: 1
            targetPages:
              - Network & internet
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            rootRoute: android.settings|rk1:Settings|src=homepage_title|up=0
            scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
            backDescriptor: Navigate up
            """);
        var config = SettingsCoverageConfig.Load(configPath);

        var runRoot = Path.Combine(repo, "evidence", "agt-010", "live-rerun");
        var result = SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
            DeviceId: deviceId,
            Live: new LivePerception.LiveAssets(
                deviceId, "settings-live", repo, "python3"),
            Config: config,
            UnderlyingConsult: DirectiveFollower,
            ViewportWidth: width,
            ViewportHeight: height,
            DshSessionIdAccessor: () => "agt-010-live-rerun"));

        output.WriteLine($"status={result.Status} reason={result.Reason} report={result.Report.Status} "
            + $"steps={result.Steps.Count} runDir={result.RunDir}");

        // 断言 1（核心，AGT-010）：撞名页进入真实可验证——标题同为 "Settings"
        // 的 Security & privacy 页 RouteAfter 为 src=title|up=1 的 rk1 key（≠根页），
        // 步骤 Verified（e1 基线：确定性 post-action-target-unique 失败）
        var colliding = result.Steps.Where(s => s.TargetDescriptor == "Security & privacy").ToList();
        Assert.True(colliding.Count >= 1,
            "未出现 Security & privacy 步骤（滚动未到底或候选未发现）：" + result.Report.FirstDivergence);
        Assert.Contains(colliding, s => s.Verified
            && s.RouteAfter == "android.settings|rk1:Settings|src=title|up=1");

        // 断言 2：没有任何步骤因撞名在 target-unique 上失败
        Assert.DoesNotContain(result.Steps, s =>
            s.TargetDescriptor == "Security & privacy"
            && s.FailureReason?.Contains("target-unique") == true);

        // 断言 3：滚动确实推进（≥1 次 verified swipe = 底部区域可达）
        Assert.Contains(result.Steps, s => s.EffectClass == "swipe-up" && s.Verified);

        // 断言 4：终局诚实（完成或 BoundedStop；不伪装）
        Assert.True(result.Report.Status is "CoverageComplete" or "BoundedStop",
            result.Report.Status);
        if (result.Report.Status == "BoundedStop")
            Assert.NotEmpty(result.Report.UncoveredItems);

        // 记录：弹窗清障是否发生（AGT-009 真实触发面——依赖设备状态）
        var obstacles = result.Consults.Where(c => c.DirectiveKind == "obstacle").ToList();
        output.WriteLine($"obstacle consults = {obstacles.Count}");
        foreach (var step in result.Steps)
            output.WriteLine($"  step {step.Index}: {step.TargetDescriptor} | {step.EffectClass} "
                + $"| verified={step.Verified} | routeAfter={step.RouteAfter}"
                + (step.FailureReason is null ? "" : $" | fail={step.FailureReason}"));
    }

    /// <summary>指令跟随 double：directive → 恰一单步 Act（模型智能不在验收面）。</summary>
    private static AgentDecision? DirectiveFollower(AgentDecisionContext context)
    {
        var marker = "COVERAGE DIRECTIVE (authoritative for this turn): ";
        var start = context.Objective.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null; // 无 directive 的咨询（终局）由 director 自答，不会走到这里
        var directive = context.Objective[(start + marker.Length)..].Split('\n', 2)[0].Trim();
        var targetStart = directive.IndexOf('<');
        var targetEnd = directive.IndexOf('>');
        var target = targetStart >= 0 && targetEnd > targetStart
            ? directive[(targetStart + 1)..targetEnd] : null;
        if (directive.StartsWith("Scroll", StringComparison.Ordinal))
            return new AgentDecision.Act(new AgentActionProposal(
                context.DecisionId,
                new[] { new AgentActionStep("scrollable",
                    "com.android.settings:id/main_content_scrollable_container", "swipe-up", null) },
                "live-rerun-follower"));
        if (target is null)
            return null;
        return new AgentDecision.Act(new AgentActionProposal(
            context.DecisionId,
            new[] { new AgentActionStep("ui.element", target, "tap", null) },
            "live-rerun-follower"));
    }

    private static (string Output, int Exit) Adb(string deviceId, string arguments)
    {
        var info = new ProcessStartInfo("adb", $"-s {deviceId} {arguments}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info)!;
        var outputText = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (outputText, process.ExitCode);
    }
}
