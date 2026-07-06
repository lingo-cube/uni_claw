using Xunit;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// BoundingBox 单元测试 — PRD §5.1: 归一化 [0,1], w/h>0, 删 BoundingBoxPixel/ToPixel
/// </summary>
public class BoundingBoxTests
{
    [Fact(DisplayName = "BoundingBox中心点: 合法bbox → Center返回正确(x,y)")]
    public void Center_ShouldReturnCorrectCoordinates()
    {
        var bbox = new BoundingBox(X: 0.1, Y: 0.2, Width: 0.3, Height: 0.4);
        var (x, y) = bbox.Center();
        Assert.Equal(0.25, x);
        Assert.Equal(0.4, y);
    }

    [Fact(DisplayName = "BoundingBox面积: 合法bbox → Area返回width*height")]
    public void Area_ShouldReturnCorrectValue()
    {
        var bbox = new BoundingBox(X: 0.0, Y: 0.0, Width: 0.5, Height: 0.3);
        Assert.Equal(0.15, bbox.Area);
    }

    [Fact(DisplayName = "BoundingBox包含: 内嵌bbox → Contains返回true")]
    public void Contains_ShouldReturnTrue_WhenBoundingBoxIsInside()
    {
        var outer = new BoundingBox(X: 0.0, Y: 0.0, Width: 1.0, Height: 1.0);
        var inner = new BoundingBox(X: 0.2, Y: 0.2, Width: 0.1, Height: 0.1);
        Assert.True(outer.Contains(inner));
    }

    [Fact(DisplayName = "BoundingBox重叠: 两bbox有交集 → Overlaps返回true")]
    public void Overlaps_ShouldReturnTrue_WhenBoundingBoxesOverlap()
    {
        var a = new BoundingBox(X: 0.0, Y: 0.0, Width: 0.5, Height: 0.5);
        var b = new BoundingBox(X: 0.3, Y: 0.3, Width: 0.5, Height: 0.5);
        Assert.True(a.Overlaps(b));
    }

    [Fact(DisplayName = "BoundingBox包含点: 点在bbox内 → ContainsPoint返回true")]
    public void ContainsPoint_ShouldReturnTrue_WhenPointIsInside()
    {
        var bbox = new BoundingBox(X: 0.0, Y: 0.0, Width: 1.0, Height: 1.0);
        Assert.True(bbox.ContainsPoint(0.5, 0.5));
    }

    [Theory(DisplayName = "BoundingBox构造: 尺寸≤0 → 抛DomainValidationException+对应FieldName")]
    [InlineData(0.0, 0.0, 0.0, 0.3, "Width")]      // zero width
    [InlineData(0.0, 0.0, 0.3, 0.0, "Height")]      // zero height
    [InlineData(0.0, 0.0, -0.1, 0.3, "Width")]      // negative width
    [InlineData(0.0, 0.0, 0.3, -0.2, "Height")]     // negative height
    public void Construction_ShouldThrow_WhenDimensionNonPositive(
        double x, double y, double w, double h, string expectedField)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => new BoundingBox(X: x, Y: y, Width: w, Height: h));
        Assert.Equal(expectedField, ex.FieldName);
    }

    [Theory(DisplayName = "BoundingBox构造: 坐标越界[0,1] → 抛DomainValidationException+对应FieldName")]
    [InlineData(1.5, 0.0, 0.3, 0.3, "X")]           // x out of [0,1]
    [InlineData(0.0, -0.1, 0.3, 0.3, "Y")]          // y out of [0,1]
    [InlineData(0.0, 0.0, 1.5, 0.3, "Width")]       // width out of [0,1]
    [InlineData(0.0, 0.0, 0.3, 2.0, "Height")]      // height out of [0,1]
    public void Construction_ShouldThrow_WhenCoordinateOutOfRange(
        double x, double y, double w, double h, string expectedField)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => new BoundingBox(X: x, Y: y, Width: w, Height: h));
        Assert.Equal(expectedField, ex.FieldName);
    }

    [Fact(DisplayName = "BoundingBox构造异常详情: 越界值 → 异常含FieldName和IllegalValue")]
    public void Construction_ShouldExposeIllegalValueInException()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => new BoundingBox(X: 0.0, Y: 0.0, Width: -0.1, Height: 0.3));
        Assert.Equal("Width", ex.FieldName);
        Assert.Equal(-0.1, ex.IllegalValue);
    }
}
