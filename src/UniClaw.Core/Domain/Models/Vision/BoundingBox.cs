namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 归一化边界框，所有坐标在 [0, 1] 范围内
/// </summary>
/// <param name="X">左上角X坐标 (0-1)</param>
/// <param name="Y">左上角Y坐标 (0-1)</param>
/// <param name="Width">宽度 (0-1)</param>
/// <param name="Height">高度 (0-1)</param>
public readonly record struct BoundingBox(
    double X,
    double Y,
    double Width,
    double Height)
{
    /// <summary>
    /// 中心点X坐标
    /// </summary>
    public readonly double CenterX => X + Width / 2;

    /// <summary>
    /// 中心点Y坐标
    /// </summary>
    public readonly double CenterY => Y + Height / 2;

    /// <summary>
    /// 面积
    /// </summary>
    public readonly double Area => Width * Height;

    /// <summary>
    /// 默认构造函数（零值边界框）
    /// </summary>
    public BoundingBox() : this(0, 0, 0, 0) { }

    /// <summary>
    /// 获取中心点坐标
    /// </summary>
    public readonly (double X, double Y) Center() => (CenterX, CenterY);

    /// <summary>
    /// 检查是否包含另一个边界框
    /// </summary>
    public readonly bool Contains(BoundingBox other) =>
        other.X >= X && other.Y >= Y &&
        other.X + other.Width <= X + Width &&
        other.Y + other.Height <= Y + Height;

    /// <summary>
    /// 检查是否与另一个边界框重叠
    /// </summary>
    public readonly bool Overlaps(BoundingBox other)
    {
        var notOverlaps = other.X > X + Width || other.X + other.Width < X ||
                          other.Y > Y + Height || other.Y + other.Height < Y;
        return !notOverlaps;
    }

    /// <summary>
    /// 检查点是否在边界框内
    /// </summary>
    public readonly bool ContainsPoint(double x, double y) =>
        x >= X && x <= X + Width &&
        y >= Y && y <= Y + Height;

    /// <summary>
    /// 转换为像素坐标
    /// </summary>
    public readonly BoundingBoxPixel ToPixel(int screenWidth, int screenHeight) =>
        new(
            X: (int)(X * screenWidth),
            Y: (int)(Y * screenHeight),
            Width: (int)(Width * screenWidth),
            Height: (int)(Height * screenHeight)
        );
}

/// <summary>
/// 像素坐标边界框
/// </summary>
public readonly record struct BoundingBoxPixel(
    int X,
    int Y,
    int Width,
    int Height)
{
    /// <summary>
    /// 中心点像素坐标
    /// </summary>
    public readonly (int X, int Y) Center => (X + Width / 2, Y + Height / 2);
}
