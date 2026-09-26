using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Effects;

/// <summary>
/// CSC-001 Slice B：投影基准分辨率执法——wm size 解析（Override 优先）/
/// 优先级（live query > 显式配置）/ 解析不到 fail-closed（zero effect +
/// diagnostic，禁 silent fallback）。
/// </summary>
public class AdbViewportResolutionTests
{
    // ---- wm size 解析 ----

    [Theory]
    [InlineData("Physical size: 320x640\nOverride size: 1080x1920\n", 1080, 1920)] // Override 优先
    [InlineData("Physical size: 1080x1920\n", 1080, 1920)]
    [InlineData("Physical size: 1080x2400\nOverride size: 1080x1920\n", 1080, 1920)]
    [InlineData("", 0, 0)]
    [InlineData("garbage no size here", 0, 0)]
    [InlineData("Override size: 0x0\n", 0, 0)]
    public void TryParseWmSize_Cases(string output, int expectedWidth, int expectedHeight)
    {
        var parsed = AdbLiveEffectDriver.TryParseWmSize(output, out var width, out var height);

        Assert.Equal(expectedWidth > 0, parsed);
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    // ---- 分辨率优先级与 fail-closed（fake runner）----

    internal sealed class FakeRunner : IAdbProcessRunner
    {
        public System.Threading.Tasks.Task<AdbProcessResult> RunAsync(
            string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult(new AdbProcessResult(
                Started: true, TimedOut: false, ExitCode: 0, StandardError: "", FailureReason: null));

        public System.Threading.Tasks.Task<AdbCaptureResult> RunCaptureAsync(
            string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult(new AdbCaptureResult(
                Started: true, TimedOut: false, ExitCode: 0,
                StandardOutput: System.Text.Encoding.UTF8.GetBytes(WmOutput),
                StandardError: "", FailureReason: null));

        public string WmOutput { get; set; } = "Physical size: 320x640\nOverride size: 1080x1920\n";
    }

    private static DispatchRequest TapAt(double cx, double cy) => new(
        Target: new DeliveryTarget(
            OccurrenceReference: "occ-1",
            Spatial: new SpatialLocator(
                Math.Max(0, cx - 0.05), Math.Max(0, cy - 0.05),
                Math.Min(1, cx + 0.05), Math.Min(1, cy + 0.05),
                AdbEffectDriver.SupportedFrame),
            Space: UniClaw.Kernel.Perception.CoordinateSpace.DeviceViewport(1080, 1920)),
        EffectClass: "tap",
        Parameters: null,
        RevisionId: "rev-1");

    [Fact]
    public void LiveQuery_WinsOver_Configured()
    {
        var runner = new FakeRunner(); // wm 报 1080×1920
        var driver = new AdbLiveEffectDriver("emulator-5554", viewportWidth: 1080, viewportHeight: 2400,
            runner: runner); // 配置故意错误（PER-013 E4 形态）

        var result = driver.Deliver(TapAt(0.5, 0.5));

        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        Assert.Contains("input tap 540 960", result.Report, StringComparison.Ordinal); // ×1920（实测），不是 ×2400
    }

    [Fact]
    public void ConfigFallback_WhenQueryUnparseable()
    {
        var runner = new FakeRunner { WmOutput = "some adb error" };
        var driver = new AdbLiveEffectDriver("emulator-5554", viewportWidth: 1080, viewportHeight: 1920,
            runner: runner);

        var result = driver.Deliver(TapAt(0.5, 0.5));

        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        Assert.Contains("input tap 540 960", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void NoQueryNoConfig_FailClosed_ZeroEffect_WithDiagnostic()
    {
        var runner = new FakeRunner { WmOutput = "" };
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        var result = driver.Deliver(TapAt(0.5, 0.5));

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("coordinate-space-unresolved", result.Reason, StringComparison.Ordinal);
        Assert.Contains("不静默使用默认值", result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("input tap", result.Report, StringComparison.Ordinal); // zero effect
    }

    [Fact]
    public void LegacyMagicDefault_Removed_NoViewport_HasNoFallback()
    {
        // HostOptions 魔数 1080×2400 已删（nullable）——不配置 + 查询失败 =
        // fail-closed，绝不再出现 ×2400 投影
        var runner = new FakeRunner { WmOutput = "" };
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        Assert.Equal(DispatchOutcome.DeliveryFailed, driver.Deliver(TapAt(0.5, 0.5)).Outcome);
    }

    [Fact]
    public void IncompleteConfig_PairValidation()
    {
        Assert.Throws<ArgumentException>(() =>
            new AdbLiveEffectDriver("emulator-5554", viewportWidth: 1080, viewportHeight: null));
        Assert.Throws<ArgumentException>(() =>
            new AdbLiveEffectDriver("emulator-5554", viewportWidth: 0, viewportHeight: 1920));
    }

    [Fact]
    public void QueryResult_Cached_SecondDispatchNoSecondQuery()
    {
        var runner = new FakeRunner();
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        driver.Deliver(TapAt(0.5, 0.5));
        runner.WmOutput = "Physical size: 999x999\n"; // 若二次查询会改变结果
        var second = driver.Deliver(TapAt(0.5, 0.5));

        Assert.Contains("input tap 540 960", second.Report, StringComparison.Ordinal); // 仍是缓存值
    }
}
