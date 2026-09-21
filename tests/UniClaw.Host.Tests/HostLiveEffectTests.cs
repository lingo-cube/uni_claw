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

    // 标定对（来源：2026-09-20 实屏真推理 `--analyze`（emulator-5554 Wi-Fi 设置
    // 根页，-S 强停后）；录制锚的 y=0.407 与当日实屏漂移 ~5% ——标定须随布局重标）
    private const double SwitchX1 = 0.8347, SwitchY1 = 0.3563, SwitchX2 = 0.9583, SwitchY2 = 0.3919;

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

        // 前置：强停 Settings 后开 Wi-Fi 设置根页（避免旧会话停在子页——
        // 实证：残留 Share Wi-Fi 子页时全页零 switch），读初始开关态
        await AdbAsync("-s emulator-5554 shell am start -S -a android.settings.WIFI_SETTINGS");
        await Task.Delay(TimeSpan.FromSeconds(3));
        var before = await WifiState();

        var root = Path.Combine(Path.GetTempPath(), "uniclaw-host-live-" + Guid.NewGuid().ToString("N"));
        try
        {
            // 半真组合：真 ADB 投递 + 仿真帧（标定 bounds；初始帧注入真实观察态
            // ——否则「目标已满足」会合法触发 Observe 零操作；目标态 = 翻转）
            var flipped = before == "0" ? "on" : "off";
            var result = HostRunner.RunOnce(
                root,
                new HostRunner.HostOptions(
                    HostRunner.EffectProfile.AdbLive, "emulator-5554", 1080, 2400)
                {
                    Bounds = new HostRunner.Calibration(SwitchX1, SwitchY1, SwitchX2, SwitchY2),
                    InitialState = before == "0" ? "off" : "on",
                    TargetState = flipped,
                });

            output.WriteLine($"status={result.Status} outcome={result.OutcomeClassification} delivered={result.DeliveredEffects}");

            // 独立验证源（不信任 sim 帧）：adb 全局设置翻转——系统设置传播
            // 有延迟，轮询至翻转（≤5s）
            string? after = null;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                after = await WifiState();
                if (after != before)
                    break;
            }
            Assert.NotEqual(before, after);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
