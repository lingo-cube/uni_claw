using Xunit;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// BoundingBox 单元测试
/// </summary>
public class BoundingBoxTests
{
    [Fact]
    public void Center_ShouldReturnCorrectCoordinates()
    {
        // Arrange
        var bbox = new BoundingBox(X: 0.1, Y: 0.2, Width: 0.3, Height: 0.4);

        // Act
        var (x, y) = bbox.Center();

        // Assert
        Assert.Equal(0.25, x);
        Assert.Equal(0.4, y);
    }

    [Fact]
    public void Area_ShouldReturnCorrectValue()
    {
        // Arrange
        var bbox = new BoundingBox(X: 0.0, Y: 0.0, Width: 0.5, Height: 0.3);

        // Act
        var area = bbox.Area;

        // Assert
        Assert.Equal(0.15, area);
    }

    [Fact]
    public void Contains_ShouldReturnTrue_WhenBoundingBoxIsInside()
    {
        // Arrange
        var outer = new BoundingBox(X: 0.0, Y: 0.0, Width: 1.0, Height: 1.0);
        var inner = new BoundingBox(X: 0.2, Y: 0.2, Width: 0.1, Height: 0.1);

        // Act
        var result = outer.Contains(inner);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenBoundingBoxIsOutside()
    {
        // Arrange
        var outer = new BoundingBox(X: 0.0, Y: 0.0, Width: 0.5, Height: 0.5);
        var inner = new BoundingBox(X: 0.4, Y: 0.4, Width: 0.2, Height: 0.2);

        // Act
        var result = outer.Contains(inner);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Overlaps_ShouldReturnTrue_WhenBoundingBoxesOverlap()
    {
        // Arrange
        var a = new BoundingBox(X: 0.0, Y: 0.0, Width: 0.5, Height: 0.5);
        var b = new BoundingBox(X: 0.3, Y: 0.3, Width: 0.5, Height: 0.5);

        // Act
        var result = a.Overlaps(b);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Overlaps_ShouldReturnFalse_WhenBoundingBoxesDoNotOverlap()
    {
        // Arrange
        var a = new BoundingBox(X: 0.0, Y: 0.0, Width: 0.2, Height: 0.2);
        var b = new BoundingBox(X: 0.3, Y: 0.3, Width: 0.2, Height: 0.2);

        // Act
        var result = a.Overlaps(b);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsPoint_ShouldReturnTrue_WhenPointIsInside()
    {
        // Arrange
        var bbox = new BoundingBox(X: 0.0, Y: 0.0, Width: 1.0, Height: 1.0);

        // Act
        var result = bbox.ContainsPoint(0.5, 0.5);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsPoint_ShouldReturnFalse_WhenPointIsOutside()
    {
        // Arrange
        var bbox = new BoundingBox(X: 0.0, Y: 0.0, Width: 0.5, Height: 0.5);

        // Act
        var result = bbox.ContainsPoint(0.6, 0.6);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DefaultConstructor_ShouldCreateZeroBoundingBox()
    {
        // Act
        var bbox = new BoundingBox();

        // Assert
        Assert.Equal(0.0, bbox.X);
        Assert.Equal(0.0, bbox.Y);
        Assert.Equal(0.0, bbox.Width);
        Assert.Equal(0.0, bbox.Height);
    }
}
