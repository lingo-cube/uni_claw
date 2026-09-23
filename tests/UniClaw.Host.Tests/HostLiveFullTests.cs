using UniClaw.Host;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>
/// 路线一第 3 步 — 全真闭环（ENVIRONMENT 门控 DSH_TEST_PERCEPTION_LIVE=1，
/// 需模拟器）：实屏截图 → 真推理 → 在线检测（bounds 来自当前屏幕）→
/// 真 ADB tap → **实屏复查 + 系统设置独立读态** → 验证由现实裁决。
/// 这是「能用起来」的终点验收：整链无仿真帧。
/// </summary>
public sealed class HostLiveFullTests(ITestOutputHelper output)
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
    public void FullLiveRun_RealPerceptionRealTapRealReobservation_CompletesAndFlips()
    {
        if (!Enabled || NoAdb)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用或 DSH_TEST_NO_ADB=1");
            return;
        }

        var repo = RepoRoot();
        var deviceId = "emulator-5554";

        // 前置：干净 Wi-Fi 设置根页（-S 强停；残留子页会零 switch——实证）
        var info = new System.Diagnostics.ProcessStartInfo(
            "adb", $"-s {deviceId} shell am start -S -a android.settings.WIFI_SETTINGS")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        using var start = System.Diagnostics.Process.Start(info)!;
        start.WaitForExit();
        Thread.Sleep(TimeSpan.FromSeconds(3));

        var before = LivePerception.LiveFrameFeed.ReadWifiState(deviceId);
        var target = before == "on" ? "off" : "on";
        output.WriteLine($"initial wifi = {before}, target = {target}");

        var root = Path.Combine(Path.GetTempPath(), "uniclaw-host-full-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = HostRunner.RunOnce(root, new HostRunner.HostOptions
            {
                DeviceId = deviceId,
                TargetState = target,
                Live = new LivePerception.LiveAssets(
                    deviceId,
                    "wifi-settings",
                    Path.Combine(repo, "platforms", "perception"),
                    Path.Combine(repo, ".perception", "venv", "bin", "python"),
                    Path.Combine(repo, ".perception", "cache")),
                // SIM-002 G1：咨询 double 由测试注入（产品 Host 无内置仿真咨询）
                ConsultAgent = context => SingleStepConsult.Consult(context, target),
            });

            output.WriteLine($"status={result.Status}({result.Reason}) outcome={result.OutcomeClassification} delivered={result.DeliveredEffects}");

            // 验证由现实裁决：Completed = 真复查看到了目标态；adb 再独立复核
            Assert.Equal(RunDriveStatus.Completed, result.Status);
            Assert.Equal("Completion", result.OutcomeClassification);

            string? after = null;
            for (var i = 0; i < 10; i++)
            {
                Thread.Sleep(500);
                after = LivePerception.LiveFrameFeed.ReadWifiState(deviceId);
                if (after != before)
                    break;
            }
            Assert.Equal(target, after); // 全真闭环：现实确实翻转到目标态
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

/// <summary>
/// SIM-002 G1：测试侧确定性咨询 double（原仿真档单步策略迁入测试——
/// 产品 Host 咨询缝 fail-closed 后由本 double 显式注入）。第一次 Act
/// （单步 tap 至目标态），StepVerified / VerificationFailed 后 NoAction
/// （目标应已达成——由 TerminalEvaluation 如实判定）。
/// </summary>
internal static class SingleStepConsult
{
    internal static AgentDecision Consult(AgentDecisionContext context, string targetState)
    {
        if (context.Phase == AgentDecisionPhase.StepVerified
            || context.Phase == AgentDecisionPhase.VerificationFailed)
        {
            return new AgentDecision.NoAction(new AgentNoActionProposal(
                context.DecisionId, "goal-should-be-met-after-first-step"));
        }
        return new AgentDecision.Act(new AgentActionProposal(
            context.DecisionId,
            new[] { new AgentActionStep("switch", TargetDescriptor: null, EffectClass: "tap", DesiredState: targetState) },
            Justification: "v0-single-step-goal"));
    }
}
