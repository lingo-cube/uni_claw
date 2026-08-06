namespace UniClaw.Core.Domain.Models.Content;

/// <summary>
/// Pixel-coordinate ROI rectangle, cached per-Container lifetime.
/// 全屏像素空间矩形 (x1 ≤ x2, y1 ≤ y2)。供 PageAnalysis.YoloBboxes 承载
/// vision-provider 输出的检测框 (local-vision 已在 provider 边界完成
/// crop/resize 逆变换, e2e-dedup-vision-quality D5 迁移后为全屏像素角点)。
/// </summary>
public readonly record struct RoiRect(
    int X1, int Y1, int X2, int Y2
);
