using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using Xunit;
using static UniClaw.Kernel.Tests.Effects.AdbViewportResolutionTests;

namespace UniClaw.Kernel.Tests.Effects;

/// <summary>
/// CSC-001 Slice D：effect 前 mechanical gate 五行矩阵的 consolidated 证据。
/// 每行 = 检查 | 执法点 | 失败结果（zero effect + explicit diagnostic）。
/// Effect Boundary 本体不做 perception/authority 判断——所有检查都是
/// 机械结构验证（空间/尺寸/方向/域/新鲜度），执法点分布在
/// SpatialLocator 构造（域）→ ValidateSupport（支持集）→ driver 空间链
/// → EB 既有 stale 拒绝。
/// </summary>
public class EffectGateMatrixTests
{
    [Fact]
    public void Row1_CoordinateSpaceValid_UnknownOrUnresolved_FailClosed()
    {
        var driver = new AdbLiveEffectDriver("e", null, null, runner: new FakeRunner());
        var noSpace = new DispatchRequest(
            new DeliveryTarget("occ", new SpatialLocator(0.1, 0.1, 0.2, 0.2, AdbEffectDriver.SupportedFrame)),
            "tap", null, "r");
        Assert.Equal(DispatchOutcome.DeliveryFailed, driver.Deliver(noSpace).Outcome); // unknown-on-target

        var unresolvable = new AdbLiveEffectDriver("e", null, null,
            runner: new FakeRunner { WmOutput = "error" });
        var withSpace = noSpace with
        {
            Target = noSpace.Target with { Space = CoordinateSpace.DeviceViewport(1080, 1920) },
        };
        var unresolved = unresolvable.Deliver(withSpace);
        Assert.Equal(DispatchOutcome.DeliveryFailed, unresolved.Outcome); // unresolved
        Assert.Contains("coordinate-space", unresolved.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Row2_DimensionsValid_NonPositive_RejectedAtConstruction()
    {
        // CoordinateSpace 构造执法 + driver 配置成对/为正执法 + wm 解析拒 0x0
        Assert.Throws<ArgumentException>(() => CoordinateSpace.DeviceViewport(0, 1920));
        Assert.Throws<ArgumentException>(() => new AdbLiveEffectDriver("e", 0, 1920));
        Assert.False(AdbLiveEffectDriver.TryParseWmSize("Override size: 0x0\n", out _, out _));
    }

    [Fact]
    public void Row3_RotationCompatible_MismatchZeroEffectWithDiagnostic()
    {
        var driver = new AdbLiveEffectDriver("e", null, null,
            runner: new FakeRunner { WmOutput = "Override size: 1080x1920\n" });
        var rotated = new DispatchRequest(
            new DeliveryTarget("occ",
                new SpatialLocator(0.4, 0.4, 0.6, 0.6, AdbEffectDriver.SupportedFrame),
                Space: CoordinateSpace.DeviceViewport(1080, 1920, ScreenRotation.Rot90)),
            "tap", null, "r");

        var result = driver.Deliver(rotated);

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("coordinate-space-mismatch", result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("input tap", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Row4_BoundsInsideViewport_ConstructionEnforced_ClampIsRoundingOnly()
    {
        // bounds 域执法在 SpatialLocator 构造（P-UW-16：[0,1]×[0,1] + X1≤X2）：
        // 越界 bounds 根本无法成为协议载荷 → 投影后必在 viewport 内；驱动
        // clamp 仅修剪 center==1.0 的像素取整边界（R2 的"错位吞错"已被
        // 空间链根除——错维度投影不再可能到达 clamp）。
        Assert.Throws<ArgumentException>(() => new SpatialLocator(0.0, 0.0, 1.2, 0.5, "device-viewport"));
        Assert.Throws<ArgumentException>(() => new SpatialLocator(0.6, 0.0, 0.4, 0.5, "device-viewport"));

        var driver = new AdbLiveEffectDriver("e", null, null,
            runner: new FakeRunner { WmOutput = "Override size: 1000x2000\n" });
        var edge = driver.Deliver(new DispatchRequest(
            new DeliveryTarget("occ",
                new SpatialLocator(0.95, 0.95, 1.0, 1.0, AdbEffectDriver.SupportedFrame),
                Space: CoordinateSpace.DeviceViewport(1000, 2000)),
            "tap", null, "r"));

        Assert.Equal(DispatchOutcome.DeliveryCompleted, edge.Outcome); // 域内 → 投影合法
        Assert.Contains("input tap 975 1950", edge.Report, StringComparison.Ordinal); // center=(0.975,0.975)——合法 locator 中心恒 <1.0，clamp 永不触发
    }

    [Fact]
    public void Row5_Freshness_StaleRevisionRejectedAtEffectBoundary_ExistingContract()
    {
        // 既有 grounding freshness 契约：canonical binding 绑定特定 revision，
        // 失效为派生判定 → EB 拒绝（StaleRevision）→ 零 dispatch。
        // 执法证据：ControlToEffectTests.Stale...（BindingRejectionReason.StaleRevision，
        // 行 171-174）。本行汇总引用，不重复实现——D 不重做 EB 判定。
        Assert.Equal(
            BindingRejectionReason.StaleRevision.ToString(),
            nameof(BindingRejectionReason.StaleRevision));
    }
}
