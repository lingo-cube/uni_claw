using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// CSC-001 Gate A：CoordinateSpace contract 执法——1080×1920 / 1080×2400 /
/// landscape / rotation change / capture ≠ configured default 五场景；
/// 构造期 fail-closed；id 确定性；Matches = dims+rotation 语义兼容。
/// </summary>
public class CoordinateSpaceTests
{
    // ---- 五场景 ----

    [Fact]
    public void Portrait_1080x1920_ValidDeviceViewport()
    {
        var space = CoordinateSpace.DeviceViewport(1080, 1920);

        Assert.Equal("device-viewport:1080x1920@0", space.CoordinateSpaceId);
        Assert.Equal(ScreenRotation.None, space.Rotation);
        Assert.True(space.Matches(CoordinateSpace.DeviceViewport(1080, 1920)));
    }

    [Fact]
    public void Configured_1080x2400_DoesNotMatch_Captured_1080x1920()
    {
        // PER-013 E4 根因形态：实测 1080×1920 vs 配置默认 1080×2400 ——
        // 两空间不兼容 → 不得互相投影（Slice C/D 执法点消费本判别）
        var captured = CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-1");
        var configuredDefault = CoordinateSpace.DeviceViewport(1080, 2400);

        Assert.False(captured.Matches(configuredDefault));
        Assert.False(configuredDefault.Matches(captured));
        Assert.NotEqual(captured, configuredDefault);
    }

    [Fact]
    public void Landscape_1920x1080_IsDistinctSpace()
    {
        var portrait = CoordinateSpace.DeviceViewport(1080, 1920);
        var landscape = CoordinateSpace.DeviceViewport(1920, 1080);

        Assert.Equal("device-viewport:1920x1080@0", landscape.CoordinateSpaceId);
        Assert.False(portrait.Matches(landscape));
    }

    [Fact]
    public void RotationChange_IsSpaceChange_RegroundRequired()
    {
        // 同一物理设备旋转：portrait@0 → landscape@90 —— 空间变化，
        // 旧 grounding 归一化坐标不可在旋转后空间直接投影
        var before = CoordinateSpace.DeviceViewport(1080, 1920, ScreenRotation.None);
        var after = CoordinateSpace.DeviceViewport(1920, 1080, ScreenRotation.Rot90);

        Assert.False(before.Matches(after));
        Assert.Equal(ScreenRotation.Rot90, after.Rotation);

        // 方向不同即使 dims 相同（正方形屏等边缘）也不兼容
        var square0 = CoordinateSpace.DeviceViewport(1080, 1080, ScreenRotation.None);
        var square90 = CoordinateSpace.DeviceViewport(1080, 1080, ScreenRotation.Rot90);
        Assert.False(square0.Matches(square90));
    }

    [Fact]
    public void CaptureSize_DiffersFrom_ConfiguredDefault_Detectable()
    {
        // capture 携带 provenance（CaptureId）；语义兼容只看 dims+rotation：
        // 同尺寸不同 capture → Matches（坐标投影可互换），但 record 不相等
        var captureA = CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-a");
        var captureB = CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-b");
        var wrongDefault = CoordinateSpace.DeviceViewport(1080, 2400, captureId: "configured");

        Assert.True(captureA.Matches(captureB));
        Assert.NotEqual(captureA, captureB); // provenance 级不同
        Assert.False(captureA.Matches(wrongDefault));
    }

    // ---- 构造期 fail-closed ----

    [Theory]
    [InlineData(0, 1920)]
    [InlineData(1080, 0)]
    [InlineData(-1, 1920)]
    [InlineData(1080, -5)]
    public void NonPositiveDimensions_Rejected(int width, int height)
    {
        Assert.Throws<ArgumentException>(() =>
            CoordinateSpace.DeviceViewport(width, height));
    }

    [Fact]
    public void EmptySpaceId_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoordinateSpace(" ", 1080, 1920, ScreenRotation.None, captureId: null));
    }

    [Fact]
    public void IllegalRotationCast_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CoordinateSpace(
                "device-viewport:1080x1920@45", 1080, 1920,
                (ScreenRotation)45, captureId: null));
    }

    // ---- id 确定性 ----

    [Fact]
    public void DeviceViewportId_IsDeterministic_RendersDimsAndRotation()
    {
        Assert.Equal(
            CoordinateSpace.DeviceViewport(1080, 1920).CoordinateSpaceId,
            CoordinateSpace.DeviceViewport(1080, 1920).CoordinateSpaceId);
        Assert.Equal(
            "device-viewport:1080x1920@90",
            CoordinateSpace.DeviceViewport(1080, 1920, ScreenRotation.Rot90).CoordinateSpaceId);
        Assert.Equal(
            "device-viewport:1920x1080@270",
            CoordinateSpace.DeviceViewport(1920, 1080, ScreenRotation.Rot270).CoordinateSpaceId);
    }
}
