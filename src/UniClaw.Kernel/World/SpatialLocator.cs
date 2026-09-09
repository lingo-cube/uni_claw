namespace UniClaw.Kernel.World;

/// <summary>
/// SpatialLocator — frame-bound 归一化空间锚（DSE-002 / P-UW-16 / UWM-009
/// §24「frame-bound spatial」语义位的首个 realization）。归一化 [0,1]×[0,1]
/// 相对 SpatialFrameId 声明的坐标系（v0.1 词汇 = "device-viewport"：单设备
/// 全屏；词汇开放不锁枚举——机械手/多屏未来只加词不加结构）。
/// 规则 A（协议构造级）：任何空间值没有 SpatialFrame 就是无效载荷——
/// 构造拒绝；bounds 有效性（X1≤X2 / Y1≤Y2 / [0,1] 域）同点执法。
/// 这是 DeliveryTarget 的 executable locator 素材；Reference identity
/// （occurrence id）永不参与执行（DSE-002 invariant）。
/// </summary>
public sealed record SpatialLocator
{
    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }
    public string SpatialFrameId { get; }

    /// <summary>投影用中心点（legacy CoordinateMapper.ToPixelCenter 数学）。</summary>
    public double CenterX => (X1 + X2) / 2;
    public double CenterY => (Y1 + Y2) / 2;

    public SpatialLocator(double x1, double y1, double x2, double y2, string spatialFrameId)
    {
        if (string.IsNullOrWhiteSpace(spatialFrameId))
            throw new ArgumentException("spatial 值没有 SpatialFrame = 无效协议载荷（P-UW-16）", nameof(spatialFrameId));
        if (x1 < 0 || y1 < 0 || x2 > 1 || y2 > 1 || x1 > x2 || y1 > y2)
            throw new ArgumentException($"bounds 无效（期望归一化 [0,1]×[0,1] 且 X1≤X2/Y1≤Y2）：({x1},{y1},{x2},{y2})", nameof(x1));
        X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
        SpatialFrameId = spatialFrameId;
    }
}
