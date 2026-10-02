using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Effects;

/// <summary>
/// AGT-005 — swipe effect 的物理翻译（dry-run 与 live 同源）：
/// 命令由目标容器自身 bounds 派生（上下各留 15% 边距，300ms 时长），
/// 方向由词汇携带；非支持集 effect 仍 fail closed。
/// </summary>
public sealed class AdbSwipeEffectTests
{
    private static DispatchRequest Request(string effect, double x1, double y1, double x2, double y2) => new(
        new DeliveryTarget(
            "occ-scroll-1",
            new SpatialLocator(x1, y1, x2, y2, AdbEffectDriver.SupportedFrame),
            Space: CoordinateSpace.DeviceViewport(1080, 1920)),
        effect,
        Parameters: null,
        RevisionId: "rev-1");

    [Fact]
    public void SwipeUp_BuildsSwipeCommandFromTargetBounds()
    {
        var driver = new AdbEffectDriver(1080, 1920);
        // 归一化 bounds (0,0.07)-(1,0.95) → pixel top=134 bottom=1824；
        // 行程 1690，15% 边距 = 253；startY=1824-253=1571，endY=134+253=387。
        var result = driver.Deliver(Request("swipe-up", 0, 0.07, 1, 0.95));

        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        Assert.Equal("adb shell input swipe 540 1571 540 387 300", result.Report);
    }

    [Fact]
    public void SwipeDown_ReversesDirection()
    {
        var driver = new AdbEffectDriver(1080, 1920);
        var result = driver.Deliver(Request("swipe-down", 0, 0.07, 1, 0.95));

        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        Assert.Equal("adb shell input swipe 540 387 540 1571 300", result.Report);
    }

    [Fact]
    public void Swipe_SmallContainer_KeepsSufficientTravel()
    {
        var driver = new AdbEffectDriver(1080, 1920);
        var result = driver.Deliver(Request("swipe-up", 0.4, 0.4, 0.6, 0.6));

        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        // bounds 768..1152，margin 57 → 1095 → 825，行程 270px（足够非 fling）。
        Assert.Equal("adb shell input swipe 540 1095 540 825 300", result.Report);
    }

    [Theory]
    [InlineData("pinch")]
    [InlineData("fling-up")]
    [InlineData("scroll")]
    public void UnsupportedEffect_FailsClosed(string effect)
    {
        var driver = new AdbEffectDriver(1080, 1920);
        var result = driver.Deliver(Request(effect, 0, 0.1, 1, 0.9));

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("unsupported-effect", result.Reason);
    }
}
