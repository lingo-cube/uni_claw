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
    /// Approach: the track fills the crop and is the luminance majority; the knob
    /// is the strong luminance outlier (either darker or lighter than the track).
    /// Divide the crop into left/right halves and count outlier pixels per half —
    /// knob right → ON; knob left → OFF; no asymmetric outlier mass → UNKNOWN.
    ///
    /// Theme-agnostic by construction (5.1 emulator calibration):
    ///   - Dark knob on light track (legacy): knob darker than track.
    ///   - Android 15 Settings (white knob, gray/teal track): knob lighter than
    ///     the gray/teal track. A fixed "darkness" threshold cannot cover both —
    ///     the ON teal track (lum ≈ 104) is darker than the OFF knob (lum ≈ 121) —
    ///     so the outlier-vs-median formulation is used instead. Deterministic,
    ///     no ML, no learned weights.
    /// </summary>
    private static bool? ClassifySwitchRegion(SKBitmap crop)
    {
        int width = crop.Width;
        int height = crop.Height;
        if (width < 4 || height < 4)
            return null;

        int midX = width / 2;

        // Sample a horizontal band in the middle third of the crop
        int bandTop = height / 3;
        int bandBottom = 2 * height / 3;

        // 1. Collect band luminances → baseline = median (the track is the majority).
        var luminances = new List<int>(width * (bandBottom - bandTop));
        for (int y = bandTop; y < bandBottom; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = crop.GetPixel(x, y);
                luminances.Add((pixel.Red + pixel.Green + pixel.Blue) / 3);
            }
        }

        if (luminances.Count == 0)
            return null;
        luminances.Sort();
        int baseline = luminances[luminances.Count / 2];

        // 2. Count knob-outlier pixels (strong deviation from the track baseline)
        //    per half. Threshold: track±knob luminance gap observed in calibration
        //    is ≥ 100 (OFF knob Δ≈107, ON knob Δ≈150); 60 keeps margin while
        //    ignoring track shading noise.
        const int outlierDelta = 60;
        int leftOutlier = 0, rightOutlier = 0;
        int leftTotal = 0, rightTotal = 0;

        for (int y = bandTop; y < bandBottom; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = crop.GetPixel(x, y);
                int luminance = (pixel.Red + pixel.Green + pixel.Blue) / 3;
                bool isOutlier = Math.Abs(luminance - baseline) >= outlierDelta;

                if (x < midX)
                {
                    leftTotal++;
                    if (isOutlier) leftOutlier++;
                }
                else
                {
                    rightTotal++;
                    if (isOutlier) rightOutlier++;
                }
            }
        }

        if (leftTotal == 0 || rightTotal == 0)
            return null;

        float leftRatio = (float)leftOutlier / leftTotal;
        float rightRatio = (float)rightOutlier / rightTotal;

        // Significant asymmetry → classified state (knob side)
        float difference = rightRatio - leftRatio;
        const float minDifference = 0.15f;

        if (difference > minDifference)
            return true;  // knob right → ON
        if (difference < -minDifference)
            return false; // knob left → OFF

        // Ambiguous — no clear knob asymmetry
        return null;
    }
}
