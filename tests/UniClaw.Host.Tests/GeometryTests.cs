using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// TRAJECTORY-001 D5 — 几何工具箱单元测试：Rect 基本运算（Contains /
/// Overlaps / IntersectionArea / Center）与 GeometryOps（IsOccluded 中心点
/// 判据、ParseBounds uiAutomator bounds 解析）。D5 裁决边界：几何只做
/// 信号/诊断输入，不改 Coverage Ledger。
/// </summary>
public sealed class GeometryTests
{
    [Fact]
    public void Contains_PointInside_True()
    {
        var rect = new Rect(0, 0, 100, 100);
        Assert.True(rect.Contains((50, 50)));
    }

    [Fact]
    public void Contains_PointOutside_False()
    {
        var rect = new Rect(0, 0, 100, 100);
        Assert.False(rect.Contains((150, 50)));
        Assert.False(rect.Contains((50, -1)));
    }

    [Theory]
    [InlineData(0, 0)]   // 左上角（闭区间边界）
    [InlineData(100, 100)] // 右下角
    public void Contains_EdgePoint_True(long x, long y)
    {
        var rect = new Rect(0, 0, 100, 100);
        Assert.True(rect.Contains((x, y)));
    }

    [Fact]
    public void Overlaps_Intersecting_True()
    {
        Assert.True(new Rect(0, 0, 100, 100).Overlaps(new Rect(50, 50, 150, 150)));
    }

    [Fact]
    public void Overlaps_Disjoint_False()
    {
        Assert.False(new Rect(0, 0, 100, 100).Overlaps(new Rect(200, 200, 300, 300)));
    }

    [Fact]
    public void Overlaps_EdgeTouching_False()
    {
        // 仅边界相触（零面积交叠）不算遮挡。
        Assert.False(new Rect(0, 0, 100, 100).Overlaps(new Rect(100, 0, 200, 100)));
    }

    [Fact]
    public void Overlaps_Contained_True()
    {
        Assert.True(new Rect(0, 0, 100, 100).Overlaps(new Rect(10, 10, 20, 20)));
    }

    [Fact]
    public void IntersectionArea_PartialOverlap()
    {
        Assert.Equal(2500, new Rect(0, 0, 100, 100).IntersectionArea(new Rect(50, 50, 150, 150)));
    }

    [Fact]
    public void IntersectionArea_Disjoint_Zero()
    {
        Assert.Equal(0, new Rect(0, 0, 100, 100).IntersectionArea(new Rect(200, 200, 300, 300)));
    }

    [Fact]
    public void IntersectionArea_Contained_FullSmallerArea()
    {
        Assert.Equal(100, new Rect(0, 0, 100, 100).IntersectionArea(new Rect(10, 10, 20, 20)));
    }

    [Fact]
    public void Center_QuadrantRect_Midpoint()
    {
        Assert.Equal((150, 250), new Rect(100, 200, 200, 300).Center);
    }

    // ---- GeometryOps.IsOccluded：中心点在 overlay 内 = 被遮挡 ----

    [Fact]
    public void IsOccluded_TargetCenterInsideOverlay_True()
    {
        var target = new Rect(40, 40, 60, 60); // center (50,50)
        var overlay = new Rect(0, 0, 100, 100);
        Assert.True(GeometryOps.IsOccluded(target, overlay));
    }

    [Fact]
    public void IsOccluded_TargetCenterOutsideOverlay_False()
    {
        var target = new Rect(0, 0, 100, 100); // center (50,50)
        var overlay = new Rect(60, 0, 160, 100); // 只遮右半
        Assert.False(GeometryOps.IsOccluded(target, overlay));
    }

    [Fact]
    public void IsOccluded_EdgeTouchOverlayCenter_False()
    {
        // 中心恰好落在 overlay 边界上：闭区间 Contains 判遮挡（与 D5 中心点
        // 判据一致，不做半开区间特判）。
        var target = new Rect(0, 0, 100, 100); // center (50,50)
        var overlay = new Rect(50, 50, 90, 90);
        Assert.True(GeometryOps.IsOccluded(target, overlay));
    }

    // ---- GeometryOps.ParseBounds：uiAutomator "[x1,y1][x2,y2]" ----

    [Theory]
    [InlineData("[0,0][100,200]", 0, 0, 100, 200)]
    [InlineData("[33,44][55,66]", 33, 44, 55, 66)]
    public void ParseBounds_Valid_ReturnsRect(string raw, int x1, int y1, int x2, int y2)
    {
        Assert.Equal(new Rect(x1, y1, x2, y2), GeometryOps.ParseBounds(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not bounds")]
    [InlineData("[0,0][100]")]
    [InlineData("[0,0]")]
    [InlineData("[a,b][c,d]")]
    public void ParseBounds_Invalid_ReturnsNull(string? raw)
    {
        Assert.Null(GeometryOps.ParseBounds(raw));
    }
}
