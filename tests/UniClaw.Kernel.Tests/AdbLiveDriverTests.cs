using System.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// ADB-001 验收（L1–L3）—— AdbLiveEffectDriver 三态真实物理映射。
/// L1/L2 纯确定性（fake runner）；L3 ENVIRONMENT-lite：真实 adb 二进制 ×
/// 假 serial——进程孵化/错误通道/exit≠0→DeliveryFailed 全链真实（设备缺席
/// 恰好验证失败路径；真机点击另立验收）。dry-run driver 回归（L4）由
/// DeliveryTargetAdbTests 既有断言承载。
/// </summary>
public sealed class AdbLiveDriverTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);
    private static readonly SpatialLocator RealSpatial = new(
        48 / 1080.0, 140 / 1920.0, 1032 / 1080.0, 188 / 1920.0,
        AdbEffectDriver.SupportedFrame);

    private static DispatchRequest TapRequest => new(
        new DeliveryTarget("occ-ref", RealSpatial), "tap", null, "rev-1");

    private sealed class FakeRunner(Func<IReadOnlyList<string>, AdbProcessResult> behavior)
        : IAdbProcessRunner
    {
        public IReadOnlyList<string>? LastArgs { get; private set; }

        public Task<AdbProcessResult> RunAsync(
            string executable, IReadOnlyList<string> arguments,
            TimeSpan timeout, CancellationToken cancellationToken)
        {
            LastArgs = arguments;
            return Task.FromResult(behavior(arguments));
        }

        // PER-005 新增接口成员（观察侧 stdout 捕获）；本测试族不消费，stub 满足契约。
        public Task<AdbCaptureResult> RunCaptureAsync(
            string executable, IReadOnlyList<string> arguments,
            TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(new AdbCaptureResult(true, false, 0, Array.Empty<byte>(), string.Empty, null));
    }

    private static AdbProcessResult Ok(int exitCode = 0, string stderr = "") =>
        new(true, false, exitCode, stderr, null);

    // ---- L1 三态真实映射 -----------------------------------------------

    [Fact]
    public void L1a_TimeoutMapsToUnknownOutcome_NeverBlindRedispatch()
    {
        var runner = new FakeRunner(_ => new AdbProcessResult(
            true, true, null, "", "ADB process timed out."));
        var driver = new AdbLiveEffectDriver("emulator-5554", 1080, 1920, runner, clock: () => FixedTime);

        var result = driver.Deliver(TapRequest);

        Assert.Equal(DispatchOutcome.UnknownOutcome, result.Outcome);
        Assert.StartsWith("timeout-killed", result.Reason);
        Assert.Contains("设备侧效果未知", result.Reason);   // F1 物理事实进 reason
    }

    [Fact]
    public void L1b_NonZeroExitMapsToDeliveryFailed_WithStderrDiagnostic()
    {
        var runner = new FakeRunner(_ => Ok(1, "error: device 'emulator-5554' not found"));
        var driver = new AdbLiveEffectDriver("emulator-5554", 1080, 1920, runner, clock: () => FixedTime);

        var result = driver.Deliver(TapRequest);

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.StartsWith("transport", result.Reason);
        Assert.Contains("device 'emulator-5554' not found", result.Reason); // diagnostic 透传
    }

    [Fact]
    public void L1c_NotStartedMapsToDeliveryFailed()
    {
        var runner = new FakeRunner(_ => new AdbProcessResult(
            false, false, null, "", "adb executable unavailable"));
        var driver = new AdbLiveEffectDriver("emulator-5554", 1080, 1920, runner, clock: () => FixedTime);

        var result = driver.Deliver(TapRequest);

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("adb executable unavailable", result.Reason);
    }

    [Fact]
    public void L1d_ZeroExitMapsToDeliveryCompleted_WithExactCommand()
    {
        var runner = new FakeRunner(_ => Ok());
        var driver = new AdbLiveEffectDriver("emulator-5554", 1080, 1920, runner, adbExecutable: "adb", clock: () => FixedTime);

        var result = driver.Deliver(TapRequest);

        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        Assert.Equal("adb -s emulator-5554 shell input tap 540 164", result.Report);
        // runner 收到的精确参数（-s serial 前置）
        Assert.Equal(new[] { "-s", "emulator-5554", "shell", "input", "tap", "540", "164" }, runner.LastArgs);
    }

    // ---- L2 共享构造：支持集失败族与 dry-run 一致 ------------------------

    [Fact]
    public void L2a_SharedSupportSet_MissingLocatorFailsClosed()
    {
        var driver = new AdbLiveEffectDriver("s", 1080, 1920,
            new FakeRunner(_ => throw new UnreachableException("不应执行进程")), clock: () => FixedTime);
        var result = driver.Deliver(new DispatchRequest(
            new DeliveryTarget("occ-ref"), "tap", null, "rev-1"));   // 无 spatial

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.StartsWith("no-executable-locator", result.Reason);
    }

    [Fact]
    public void L2b_SharedSupportSet_UnsupportedEffectFailsClosed()
    {
        var driver = new AdbLiveEffectDriver("s", 1080, 1920,
            new FakeRunner(_ => throw new UnreachableException("不应执行进程")), clock: () => FixedTime);
        var result = driver.Deliver(new DispatchRequest(
            new DeliveryTarget("occ-ref", RealSpatial), "swipe", null, "rev-1"));

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.StartsWith("unsupported-effect", result.Reason);
    }

    // ---- L3 ENVIRONMENT-lite：真实 adb 二进制 × 假 serial ----------------

    [Fact]
    public void L3_EnvLite_RealAdbBinaryFakeSerial_FailsClosedThroughRealProcess()
    {
        // 真实进程/错误通道/exit≠0 全链验证（无需设备——缺席恰好验证失败路径）。
        // adb 二进制不在场时显式跳过（环境不可用 ≠ 语义失败）。
        if (Environment.GetEnvironmentVariable("DSH_TEST_NO_ADB") == "1")
        {
            return; // 显式跳过开关
        }
        try
        {
            var probe = new ProcessStartInfo("adb", "version") { RedirectStandardOutput = true };
            using var p = Process.Start(probe);
            p?.WaitForExit(5000);
            if (p is null || p.ExitCode != 0)
                return; // adb 不可用：跳过 EnvLite（DETERMINISTIC 层已全覆盖语义）
        }
        catch
        {
            return; // 无 adb：跳过
        }

        var driver = new AdbLiveEffectDriver("dsh-fake-serial-0000", 1080, 1920,
            clock: () => FixedTime);
        var result = driver.Deliver(TapRequest);

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);   // 真实 exit≠0
        Assert.StartsWith("transport", result.Reason);
        Assert.Contains("dsh-fake-serial-0000", result.Reason);          // 真实 adb 诊断
    }
}
