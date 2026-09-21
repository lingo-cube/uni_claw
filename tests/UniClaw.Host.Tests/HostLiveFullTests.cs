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
                Effect = HostRunner.EffectProfile.AdbLive,
                DeviceId = deviceId,
                TargetState = target,
                Live = new LivePerception.LiveAssets(
                    deviceId,
                    "wifi-settings",
                    Path.Combine(repo, "platforms", "perception"),
                    Path.Combine(repo, ".perception", "venv", "bin", "python"),
                    Path.Combine(repo, ".perception", "cache")),
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
