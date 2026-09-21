using System.Diagnostics;
using UniClaw.Host;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>
/// 路线一第 1 步：半真档（仿真帧源 + 真 ADB 投递）。ENVIRONMENT 门控
/// （RUN-002 先例：DSH_TEST_PERCEPTION_LIVE=1 启用；模拟器生命周期外部
/// 管理）。帧源携带**标定坐标**（uni-agent LiveCalibration 同款思路：
/// 常量 + 独立验证源 adb settings get global wifi_on——不伪造）。
/// 标定注：bounds 对应 emulator-5554 / 1080x2400 / API 35 Wi-Fi 设置页
/// 开关；布局变化须重新标定（常量即标定对）。
/// </summary>
public sealed class HostLiveEffectTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") == "1";

    private static bool NoAdb =>
        Environment.GetEnvironmentVariable("DSH_TEST_NO_ADB") == "1";

    // 标定对（emulator-5554 Wi-Fi 页开关，归一化 bounds + AdbEffectDriver 支持坐标系）
    private const double SwitchX1 = 0.86, SwitchY1 = 0.30, SwitchX2 = 0.97, SwitchY2 = 0.36;

    private static async Task<string> AdbAsync(string arguments)
    {
        var info = new ProcessStartInfo("adb", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("adb 启动失败（fail-closed）");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None);
        Assert.Equal(0, process.ExitCode);
        return stdout.Trim();
    }

    private static Task<string> WifiState() =>
        AdbAsync("shell settings get global wifi_on");

    [Fact]
    public async Task HalfRealRun_SimFramesWithRealAdbTap_FlipsWifiSwitch()
    {
        if (!Enabled || NoAdb)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用或 DSH_TEST_NO_ADB=1");
            return;
        }

        var devices = await AdbAsync("devices");
        Assert.Contains("emulator-5554", devices, StringComparison.Ordinal);

        // 前置：打开 Wi-Fi 设置页（RUN-002 同款），读初始开关态
        await AdbAsync("-s emulator-5554 shell am start -a android.settings.WIFI_SETTINGS");
        await Task.Delay(TimeSpan.FromSeconds(2));
        var before = await WifiState();

        var root = Path.Combine(Path.GetTempPath(), "uniclaw-host-live-" + Guid.NewGuid().ToString("N"));
        try
        {
            // 半真组合：真 ADB 投递 + 仿真帧（标定 bounds；目标态 = 初始态翻转）
            var flipped = before == "0" ? "on" : "off";
            var result = HostRunner.RunOnce(
                root,
                new HostRunner.HostOptions(
                    HostRunner.EffectProfile.AdbLive, "emulator-5554", 1080, 2400)
                {
                    Bounds = new HostRunner.Calibration(SwitchX1, SwitchY1, SwitchX2, SwitchY2),
                    TargetState = flipped,
                });

            output.WriteLine($"status={result.Status} outcome={result.OutcomeClassification} delivered={result.DeliveredEffects}");

            // 独立验证源（不信任 sim 帧）：adb 全局设置翻转
            var after = await WifiState();
            Assert.NotEqual(before, after);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
