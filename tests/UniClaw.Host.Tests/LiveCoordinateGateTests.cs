using UniClaw.Host;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>
/// CSC-001 Slice E：真机 negative case——grounded space（1080×1920）在
/// 设备 viewport 被改为 1080×2400 后 dispatch → coordinate-space-mismatch
/// → zero effect + RE-GROUND diagnostic（不猜、不投影）。ENVIRONMENT 门控
/// DSH_TEST_PERCEPTION_LIVE=1；finally 恢复 wm size。
/// </summary>
public sealed class LiveCoordinateGateTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") == "1"
        && Environment.GetEnvironmentVariable("DSH_TEST_NO_ADB") != "1";

    private static string Device =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_DEVICE") ?? "emulator-5554";

    private static string Shell(string args)
    {
        var info = new System.Diagnostics.ProcessStartInfo("adb", $"-s {Device} shell {args}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }

    private static void ApplyOverride(int width, int height)
    {
        // 设置必须验证生效（adb 偶发静默失败会假绿/假红——机械确认）
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Shell($"wm size {width}x{height}");
            var current = Shell("wm size");
            if (current.Contains($"{width}x{height}", StringComparison.Ordinal))
            {
                return;
            }
        }

        throw new InvalidOperationException($"wm size {width}x{height} 三次设置未生效：{Shell("wm size")}");
    }

    [Fact]
    public void StaleGroundedSpace_AgainstLiveDevice_ZeroEffect_ReGround()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用");
            return;
        }

        // 前置：设备处于本 AVD 标准实况 1080×1920（机械确认；本 emulator
        // 配置锁死 wm override 不可改——已实证，故 negative case 采用镜像
        // 方向：stale grounding 1080×2400 vs 设备实况，同一 Matches 执法点）
        Assert.Contains("1080x1920", Shell("wm size"), StringComparison.Ordinal);

        var driver = new AdbLiveEffectDriver(Device, adbExecutable: "adb");
        var request = new DispatchRequest(
            new DeliveryTarget(
                "occ-live-mismatch",
                new SpatialLocator(0.45, 0.40, 0.90, 0.45, AdbEffectDriver.SupportedFrame),
                Space: CoordinateSpace.DeviceViewport(1080, 2400, captureId: "cap-stale-grounding")),
            "tap", null, "rev-1");

        var result = driver.Deliver(request);

        output.WriteLine($"outcome={result.Outcome} reason={result.Reason} report={result.Report}");
        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("coordinate-space-mismatch", result.Reason, StringComparison.Ordinal);
        Assert.Contains("RE-GROUND", result.Reason, StringComparison.Ordinal);
        Assert.Contains("device-viewport:1080x2400", result.Reason, StringComparison.Ordinal); // grounded 侧进 diagnostic
        Assert.Contains("device-viewport:1080x1920", result.Reason, StringComparison.Ordinal); // 设备实况进 diagnostic
        Assert.DoesNotContain("input tap", result.Report, StringComparison.Ordinal); // zero effect
    }

    [Fact]
    public void MatchingGroundedSpace_OnDevice_ProjectsAtRealityDims()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用");
            return;
        }

        var driver = new AdbLiveEffectDriver(Device, adbExecutable: "adb");
        var request = new DispatchRequest(
            new DeliveryTarget(
                "occ-live-match",
                new SpatialLocator(0.05, 0.05, 0.06, 0.06, AdbEffectDriver.SupportedFrame), // 左上角安全区
                Space: CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-now")),
            "tap", null, "rev-1");

        var result = driver.Deliver(request);

        output.WriteLine($"outcome={result.Outcome} report={result.Report}");
        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        // 实测 1080×1920 基准：center=(0.055,0.055) → (59,105)——若曾用 2400
        // 魔数投影会是 (59,132)，E4 形态复现即在此断言暴露
        Assert.Contains("input tap 59 105", result.Report, StringComparison.Ordinal);
    }
}
