namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 归一化边界框，所有坐标在 [0, 1] 范围内（w/h &gt; 0）。
/// 像素转换不在 Domain 职责内（PRD §4.1），故无 BoundingBoxPixel / ToPixel。
/// </summary>
public sealed record class BoundingBox
{
    /// <summary>左上角 X 坐标 [0,1]</summary>
    public double X { get; init; }

    /// <summary>左上角 Y 坐标 [0,1]</summary>
    public double Y { get; init; }

    /// <summary>宽度 (0,1]</summary>
    public double Width { get; init; }

    /// <summary>高度 (0,1]</summary>
    public double Height { get; init; }

    /// <param name="X">左上角 X 坐标 [0,1]</param>
    /// <param name="Y">左上角 Y 坐标 [0,1]</param>
    /// <param name="Width">宽度 (0,1]</param>
    /// <param name="Height">高度 (0,1]</param>
    public BoundingBox(double X, double Y, double Width, double Height)
    {
        if (!InRange(X)) throw new DomainValidationException(nameof(X), X);
        if (!InRange(Y)) throw new DomainValidationException(nameof(Y), Y);
        if (!InRange(Width) || Width <= 0) throw new DomainValidationException(nameof(Width), Width);
        if (!InRange(Height) || Height <= 0) throw new DomainValidationException(nameof(Height), Height);

        this.X = X;
        this.Y = Y;
        this.Width = Width;
        this.Height = Height;
    }

    /// <summary>中心点 X 坐标</summary>
    public double CenterX => X + Width / 2;

    /// <summary>中心点 Y 坐标</summary>
    public double CenterY => Y + Height / 2;

    /// <summary>面积</summary>
    public double Area => Width * Height;

    /// <summary>获取中心点坐标</summary>
    public (double X, double Y) Center() => (CenterX, CenterY);

    /// <summary>检查是否包含另一个边界框</summary>
    public bool Contains(BoundingBox other) =>
        other.X >= X && other.Y >= Y &&
        other.X + other.Width <= X + Width &&
        other.Y + other.Height <= Y + Height;

    /// <summary>检查是否与另一个边界框重叠</summary>
    public bool Overlaps(BoundingBox other)
    {
        var notOverlaps = other.X > X + Width || other.X + other.Width < X ||
                          other.Y > Y + Height || other.Y + other.Height < Y;
        return !notOverlaps;
    }

    /// <summary>检查点是否在边界框内</summary>
    public bool ContainsPoint(double x, double y) =>
        x >= X && x <= X + Width &&
        y >= Y && y <= Y + Height;

    private static bool InRange(double v) => v >= 0.0 && v <= 1.0;
}
