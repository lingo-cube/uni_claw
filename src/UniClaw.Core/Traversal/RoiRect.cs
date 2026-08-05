namespace UniClaw.Core.Traversal;

/// <summary>
/// Pixel-coordinate ROI rectangle, cached per-Container lifetime.
/// </summary>
public readonly record struct RoiRect(
    int X1, int Y1, int X2, int Y2
);
