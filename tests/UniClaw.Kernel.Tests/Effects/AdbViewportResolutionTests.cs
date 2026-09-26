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
        {
            WmCalls++;
            return System.Threading.Tasks.Task.FromResult(new AdbCaptureResult(
                Started: true, TimedOut: false, ExitCode: 0,
                StandardOutput: System.Text.Encoding.UTF8.GetBytes(WmOutput),
                StandardError: "", FailureReason: null));
        }

        public string WmOutput { get; set; } = "Physical size: 320x640\nOverride size: 1080x1920\n";
        public int WmCalls { get; private set; }
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
    public void V1_Driver_SessionCache_MultipleDispatchesSingleWmQuery()
    {
        // CSC-002 V1（driver 视角）：session 内多 dispatch 只查一次 wm size
        var runner = new FakeRunner();
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        for (var i = 0; i < 5; i++)
        {
            var result = driver.Deliver(TapAt(0.5, 0.5));
            Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        }

        Assert.Equal(1, runner.WmCalls);
    }

    [Fact]
    public void V3_Driver_CaptureSignalInvalidates_OldGroundingRejected()
    {
        // CSC-002 V3：设备变化经 capture 信号暴露——旧 grounding 被拒，
        // re-ground 后恢复；无 capture 变化时 session cache 复用（CSC-001
        // 每-dispatch-查询语义已被 CSC-002 owner 指令取代）。
        var runner = new FakeRunner(); // Override 1080×1920
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        // dispatch 1：1920 grounding 与设备一致 → 投影 ×1920
        var first = driver.Deliver(TapAt(0.5, 0.5));
        Assert.Equal(DispatchOutcome.DeliveryCompleted, first.Outcome);
        Assert.Contains("input tap 540 960", first.Report, StringComparison.Ordinal);
        Assert.Equal(1, runner.WmCalls);

        // 设备 viewport 变化 + 新 capture 观察到 2400 → 信号触发失效 → 重实测
        runner.WmOutput = "Physical size: 320x640\nOverride size: 1080x2400\n";
        var stale = driver.Deliver(new DispatchRequest(
            new DeliveryTarget("occ-2",
                new SpatialLocator(0.45, 0.45, 0.55, 0.55, AdbEffectDriver.SupportedFrame),
                Space: CoordinateSpace.DeviceViewport(1080, 2400, captureId: "cap-new")),
            "tap", null, "rev-2"));
        Assert.Equal(DispatchOutcome.DeliveryCompleted, stale.Outcome);
        Assert.Contains("input tap 540 1200", stale.Report, StringComparison.Ordinal); // 新空间投影
        Assert.Equal(2, runner.WmCalls); // 失效 → 恰一次重查

        // 旧 grounding（1920）再 dispatch：capture 信号与 cache(2400) 冲突
        // → 失效 → fresh 实测（#3，仍 2400）→ 旧 grounding mismatch → 零 effect
        // （owner Slice D 原文：invalidate → fresh device resolution → old
        // grounding invalid → RE-GROUND——冲突 capture 每次触发重实测是语义
        // 本体，非缺陷）
        var old = driver.Deliver(TapAt(0.5, 0.5));
        Assert.Equal(DispatchOutcome.DeliveryFailed, old.Outcome);
        Assert.Contains("coordinate-space-mismatch", old.Reason, StringComparison.Ordinal);
        Assert.Equal(3, runner.WmCalls);
    }
}
