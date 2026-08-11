using SkiaSharp;
using UniClaw.Runtime.Capabilities.Perception.Vision;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Perception.Vision;

/// <summary>
/// Frame-scoped image-based switch state reader using SkiaSharp.
///
/// Crops the toggle region from an immutable perception frame and classifies
/// the visual ON/OFF state using deterministic geometry analysis.
///
/// Bound to one PerceptionFrame at construction. Does NOT own Runtime state,
/// semantic belief, capability selection, or goal completion authority.
///
/// Classification approach (deterministic, no ML):
///   1. Crop the switch region from the full screenshot.
///   2. Analyze luminance distribution to detect knob position.
///   3. Knob right → ON; knob left → OFF; ambiguous → UNKNOWN.
/// </summary>
public sealed class ImageSwitchStateProvider : ISwitchStateReader
{
    private readonly SKBitmap _frameBitmap;
    private readonly int _fullWidth;
    private readonly int _fullHeight;

    public PerceptionFrame Frame { get; }

    /// <summary>
    /// Creates a reader bound to one immutable screenshot frame.
    /// </summary>
    /// <param name="frameBitmap">Immutable screenshot bitmap (BGRA or similar).</param>
    /// <param name="fullWidth">Full screenshot width in pixels.</param>
    /// <param name="fullHeight">Full screenshot height in pixels.</param>
    public ImageSwitchStateProvider(SKBitmap frameBitmap, int fullWidth, int fullHeight)
    {
        ArgumentNullException.ThrowIfNull(frameBitmap);
        if (fullWidth <= 0 || fullHeight <= 0)
            throw new ArgumentException("Frame dimensions must be positive.");

        _frameBitmap = frameBitmap;
        _fullWidth = fullWidth;
        _fullHeight = fullHeight;
        Frame = new PerceptionFrame();
    }

    /// <inheritdoc />
    public ValueTask<bool?> ReadAsync(
        ElementBounds switchBounds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!switchBounds.IsValid)
            return ValueTask.FromResult<bool?>(null);

        try
        {
            // Convert normalized [0,1] bounds to pixel coordinates
            var cropRect = NormalizedToPixelRect(switchBounds);
            if (cropRect.Width < 8 || cropRect.Height < 8)
                return ValueTask.FromResult<bool?>(null); // too small to classify

            // Crop the switch region
            using var crop = new SKBitmap(cropRect.Width, cropRect.Height);
            if (!_frameBitmap.ExtractSubset(crop, cropRect))
                return ValueTask.FromResult<bool?>(null);

            // Classify: analyze knob position within the track
            var result = ClassifySwitchRegion(crop);
            return ValueTask.FromResult(result);
        }
        catch
        {
            // Any processing failure → fail closed
            return ValueTask.FromResult<bool?>(null);
        }
    }

    private SKRectI NormalizedToPixelRect(ElementBounds bounds)
    {
        int x1 = (int)(bounds.X1 * _fullWidth);
        int y1 = (int)(bounds.Y1 * _fullHeight);
        int x2 = (int)(bounds.X2 * _fullWidth);
        int y2 = (int)(bounds.Y2 * _fullHeight);
        return new SKRectI(x1, y1, x2, y2);
    }

    /// <summary>
    /// Classifies a cropped switch region as ON, OFF, or UNKNOWN.
    ///
    /// Approach: divide the crop into left and right halves.
    /// The knob (dark circle against lighter track) position determines state.
    /// More dark pixels on the right → ON; on the left → OFF.
    /// </summary>
    private static bool? ClassifySwitchRegion(SKBitmap crop)
    {
        int width = crop.Width;
        int height = crop.Height;
        if (width < 4 || height < 4)
            return null;

        int midX = width / 2;

        // Count dark pixels (knob) in left vs right halves
        int leftDark = 0, rightDark = 0;
        int leftTotal = 0, rightTotal = 0;

        // Sample a horizontal band in the middle third of the crop
        int bandTop = height / 3;
        int bandBottom = 2 * height / 3;

        for (int y = bandTop; y < bandBottom; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = crop.GetPixel(x, y);
                // Luminance: simple average of R, G, B
                int luminance = (pixel.Red + pixel.Green + pixel.Blue) / 3;

                if (x < midX)
                {
                    leftTotal++;
                    if (luminance < 100) leftDark++;
                }
                else
                {
                    rightTotal++;
                    if (luminance < 100) rightDark++;
                }
            }
        }

        if (leftTotal == 0 || rightTotal == 0)
            return null;

        float leftRatio = (float)leftDark / leftTotal;
        float rightRatio = (float)rightDark / rightTotal;

        // Significant asymmetry → classified state
        float difference = rightRatio - leftRatio;
        const float minDifference = 0.15f;

        if (difference > minDifference)
            return true;  // knob right → ON
        if (difference < -minDifference)
            return false; // knob left → OFF

        // Ambiguous — no clear asymmetry
        return null;
    }
}
