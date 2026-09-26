using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-002 G1：原 UniClaw.Host.Tests 服务回放端到端迁入（ENVIRONMENT 门控
/// DSH_TEST_PERCEPTION_SERVICE=1：启动真 Python 视觉服务，重但无需真机）。
/// 录制截图 → 真推理 → 共享提取器 → 完整核心环路（经 DevLoopRunner）。
/// 验收：Completed/Completion。
/// </summary>
public sealed class DevServiceReplayTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_SERVICE") == "1";

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
    public void ServiceReplay_RecordedScreensRealInference_FullLoopToCompletion()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_SERVICE 未启用（需 .perception venv）");
            return;
        }

        var repo = RepoRoot();
        var capture = Path.Combine(repo, "platforms", "perception", "evaluation", "assets", "captures", "wifi-slice2-calibration");
        var assets = new ServicePerception.ServiceReplayAssets(
            Path.Combine(capture, "frames", "wifi-off-emulator-5554.png"),
            Path.Combine(capture, "frames", "wifi-on-emulator-5554.png"),
            ScreenId: "wifi-settings",
            ProviderRoot: Path.Combine(repo, "platforms", "perception"),
            PythonExecutable: Path.Combine(repo, ".perception", "venv", "bin", "python"),
            CacheRoot: Path.Combine(repo, ".perception", "cache"));

        var root = Path.Combine(Path.GetTempPath(), "uniclaw-dev-svc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = DevLoopRunner.RunOnce(
                root,
                clock => (new ServicePerception.ServiceReplayFrameFeed(clock, assets, "on").Next, null),
                targetState: "on",
                runName: "svc");

            output.WriteLine($"status={result.Status} outcome={result.OutcomeClassification} delivered={result.DeliveredEffects}");
            Assert.Equal(RunDriveStatus.Completed, result.Status);
            Assert.Equal("Completion", result.OutcomeClassification);
            Assert.Single(result.ReceiptOutcomes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

/// <summary>
/// SIM-002 G1：原 UniClaw.Host.Tests 半真档（仿真帧源 + 真 ADB 投递）迁入。
/// ENVIRONMENT 门控 DSH_TEST_PERCEPTION_LIVE=1（模拟器生命周期外部管理；
/// RUN-002 先例）。帧源携带**标定坐标**（常量 + 独立验证源 adb settings
/// get global wifi_on——不伪造）；标定注：bounds 对应 emulator-5554 /
/// 1080x2400 / API 35 Wi-Fi 设置页开关；布局变化须重新标定。
/// </summary>
public sealed class DevHalfRealEffectTests(ITestOutputHelper output)
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
        var info = new System.Diagnostics.ProcessStartInfo("adb", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = System.Diagnostics.Process.Start(info)
            ?? throw new InvalidOperationException("adb 启动失败（fail-closed）");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None);
        Assert.Equal(0, process.ExitCode);
        return stdout.Trim();
    }

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
        var before = await AdbAsync("-s emulator-5554 shell settings get global wifi_on");

        var root = Path.Combine(Path.GetTempPath(), "uniclaw-dev-halfreal-" + Guid.NewGuid().ToString("N"));
        try
        {
            // 半真组合：真 ADB 投递 + 仿真帧（标定 bounds；初始帧注入真实观察态
            // ——否则「目标已满足」会合法触发 Observe 零操作；目标态 = 翻转）
            var flipped = before == "0" ? "on" : "off";
            var result = DevLoopRunner.RunOnce(
                root,
                clock => (new V0Runtime.FrameFeed(
                    clock,
                    new V0Runtime.Calibration(SwitchX1, SwitchY1, SwitchX2, SwitchY2),
                    targetState: flipped,
                    initialState: before == "0" ? "off" : "on").Next, null),
                targetState: flipped,
                makeDriver: clock => new AdbLiveEffectDriver(
                    "emulator-5554", 1080, 1920, adbExecutable: "adb", clock: () => clock.Now),
                runName: "halfreal");

            output.WriteLine($"status={result.Status}({result.Reason}) outcome={result.OutcomeClassification} delivered={result.DeliveredEffects}");
            Assert.Equal(RunDriveStatus.Completed, result.Status);
            Assert.Equal("Completion", result.OutcomeClassification);

            // 现实验证：adb 独立复核开关态确已翻转
            string? after = null;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                after = await AdbAsync("-s emulator-5554 shell settings get global wifi_on");
                if ((after == "1" ? "on" : "off") != (before == "0" ? "off" : "on"))
                    break;
            }
            Assert.Equal(flipped, after == "1" ? "on" : "off");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
