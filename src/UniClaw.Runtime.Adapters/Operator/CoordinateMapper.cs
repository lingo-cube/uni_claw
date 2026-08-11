using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Operator;

/// <summary>
/// Stateless coordinate translation for the physical Operator.
///
/// Converts normalized Runtime ElementBounds ([0,1]×[0,1], full-screenshot,
/// top-left origin) into device pixel coordinates.
///
/// Does NOT own semantic state, target selection, or capability authority.
/// </summary>
public static class CoordinateMapper
{
    /// <summary>
    /// Maps a normalized bounds center to device pixel coordinates.
    /// </summary>
    /// <returns>The interaction point (center of bounds) in pixel space, or null if invalid.</returns>
    public static (int X, int Y)? ToPixelCenter(
        ElementBounds bounds,
        int displayWidth,
        int displayHeight)
    {
        if (!bounds.IsValid)
            return null;
        if (displayWidth <= 0 || displayHeight <= 0)
            return null;

        int centerX = (int)(bounds.CenterX * displayWidth);
        int centerY = (int)(bounds.CenterY * displayHeight);

        // Clamp to valid pixel range
        centerX = Math.Clamp(centerX, 0, displayWidth - 1);
        centerY = Math.Clamp(centerY, 0, displayHeight - 1);

        return (centerX, centerY);
    }

    /// <summary>
    /// Maps a normalized bounds rectangle to device pixel rectangle.
    /// </summary>
    public static (int X1, int Y1, int X2, int Y2)? ToPixelRect(
        ElementBounds bounds,
        int displayWidth,
        int displayHeight)
    {
        if (!bounds.IsValid)
            return null;
        if (displayWidth <= 0 || displayHeight <= 0)
            return null;

        int x1 = Math.Clamp((int)(bounds.X1 * displayWidth), 0, displayWidth - 1);
        int y1 = Math.Clamp((int)(bounds.Y1 * displayHeight), 0, displayHeight - 1);
        int x2 = Math.Clamp((int)(bounds.X2 * displayWidth), 0, displayWidth - 1);
        int y2 = Math.Clamp((int)(bounds.Y2 * displayHeight), 0, displayHeight - 1);

        return (x1, y1, x2, y2);
    }
}
