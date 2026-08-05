using System.Collections.Immutable;
using System.Runtime.InteropServices;
using SkiaSharp;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Traversal;

/// <summary>
/// Selects the best ROI (region of interest) from a full-screen screenshot for
/// scroll-end detection. Scores candidate sliding windows with a composite metric
/// (YOLO bbox density, Laplacian-variance texture, non-solid ratio) minus a
/// dynamic-element penalty, per PRD §3.5 / roi-scroll-detection spec.
///
/// NOTE ON DATA SOURCE: <see cref="MenuItem"/> carries no boundsPx / yoloId —
/// those live in the LocalVisionProvider evidence layer. The caller therefore
/// passes the pixel-space boundsPx of yoloId != null items separately (the
/// yoloBboxes parameter); this signature deliberately diverges from
/// PRD §3.5's `Select(PageAnalysis, ...)` sketch (see decision in
/// openspec/changes/roi-scroll-detection/tasks.md).
/// </summary>
internal static class RoiSelector
{
    // ── candidate-range geometry (PRD §3.5.1) ────────────────────────────────
    /// <summary>Vertical sweep: window top edge starts at 30% of screen height.</summary>
    private const double CandidateTopRatio = 0.30;

    /// <summary>Vertical sweep: window top edge stops at 85% of screen height.</summary>
    private const double CandidateBottomRatio = 0.85;

    /// <summary>Preferred band: 40%-65% of screen height gets a score bonus.</summary>
    private const double PreferredBandTopRatio = 0.40;

    /// <summary>Preferred band: 40%-65% of screen height gets a score bonus.</summary>
    private const double PreferredBandBottomRatio = 0.65;

    /// <summary>ROI width: 80% of screen width (spec range 70%-90%).</summary>
    private const double WindowWidthRatio = 0.80;

    /// <summary>ROI height: 25% of screen height (spec range 20%-30%).</summary>
    private const double WindowHeightRatio = 0.25;

    /// <summary>Left/right edge margin excluded from the horizontal range (5%).</summary>
    private const double HorizontalMarginRatio = 0.05;

    /// <summary>Sliding step = 50% of window height (PRD §3.5.2).</summary>
    private const double WindowStepRatio = 0.50;

    // ── scoring weights (PRD §3.5.2) ─────────────────────────────────────────
    private const double DensityWeight = 0.6;
    private const double TextureWeight = 0.3;
    private const double NonSolidWeight = 0.1;
    private const double DegradedTextureWeight = 0.7;   // pure OCR scene: density 0
    private const double DegradedNonSolidWeight = 0.3;

    // ── calibration constants (PRD: calibrate on real device, like the blacklist) ──
    /// <summary>Normalises Laplacian variance over 8-bit grayscale into [0,1] (≈3% of the ~1M max variance).</summary>
    private const double TextureVarianceNormalizer = 30_000.0;

    /// <summary>Minimum combined score for a window to be selectable — blank page yields null (spec R3).</summary>
    private const double MinSelectionScore = 0.15;

    /// <summary>Local 3×3 std-dev above this counts a pixel as non-uniform (matches ScrollSwipeConfig.PixelNoiseThreshold default 15.0).</summary>
    private const double NonSolidNoiseThreshold = 15.0;

    /// <summary>Square of the noise threshold — comparison done on variance to skip sqrt per pixel.</summary>
    private const double NonSolidNoiseThresholdSquared = NonSolidNoiseThreshold * NonSolidNoiseThreshold;

    /// <summary>Per-element dynamic penalty, capped to avoid wiping out the whole score.</summary>
    private const double DynamicPenaltyPerElement = 0.3;

    /// <summary>Cap on the accumulated dynamic penalty.</summary>
    private const double DynamicPenaltyCap = 0.6;

    /// <summary>Score multiplier applied when the window centre lies outside the preferred 40%-65% band.</summary>
    private const double PreferredBandMultiplier = 0.9;

    /// <summary>Score multiplier (weight halved) when the window overlaps the status bar (y&lt;5%) or nav bar (y&gt;95%).</summary>
    private const double FixedRegionMultiplier = 0.5;

    /// <summary>Windows smaller than this (px) on either side are not scoreable.</summary>
    private const int MinWindowDimensionPx = 16;

    /// <summary>
    /// Dynamic-element type blacklist (PRD §3.5.2 mapping table): loading, banner,
    /// carousel, progressbar, video. Matched against <see cref="MenuItem.Type"/> via
    /// ToString().ToLowerInvariant(). NOTE: the current MenuItemType enum has no
    /// member for these YOLO labels, so the penalty is inert until the label
    /// calibration step (PRD §3.5.2) maps real model labels onto the enum.
    /// </summary>
    private static readonly HashSet<string> DynamicTypeBlacklist = new(StringComparer.Ordinal)
    {
        "loading", "banner", "carousel", "progressbar", "video",
    };

    /// <summary>
    /// Select the best ROI region by sliding-window scoring.
    /// Returns null when all windows score too low (blank page), the screenshot
    /// cannot be decoded, or its dimensions disagree with the declared capture size.
    /// </summary>
    /// <param name="items">Page items; only <see cref="MenuItem.Type"/> is consumed (dynamic-element blacklist matching).</param>
    /// <param name="yoloBboxes">Pixel-space boundsPx of items with yoloId != null, extracted by the caller from the raw evidence; empty list = pure OCR scene → degraded scoring.</param>
    /// <param name="screenshot">Raw full-screen screenshot bytes (PNG/JPEG).</param>
    /// <param name="screenWidth">Full-screen width in pixels.</param>
    /// <param name="screenHeight">Full-screen height in pixels.</param>
    /// <returns>Best-scoring ROI in pixel coordinates, or null when no window qualifies.</returns>
    public static RoiRect? Select(
        ImmutableArray<MenuItem> items,
        IReadOnlyList<RoiRect> yoloBboxes,
        byte[] screenshot,
        int screenWidth,
        int screenHeight)
    {
        if (screenshot is null || screenshot.Length == 0)
            return null;
        if (screenWidth <= 0 || screenHeight <= 0)
            return null;

        using var bitmap = Decode(screenshot, screenWidth, screenHeight);
        if (bitmap is null)
            return null;

        var pixelPtr = bitmap.GetPixels();
        if (pixelPtr == IntPtr.Zero)
            return null;
        var rgba = new byte[bitmap.ByteCount];
        Marshal.Copy(pixelPtr, rgba, 0, rgba.Length);
        var stride = bitmap.RowBytes;

        // Degraded mode: no YOLO boxes anywhere → density weight moves to texture
        // (PRD §3.5.3); density is skipped entirely in the composite.
        var boxes = yoloBboxes ?? Array.Empty<RoiRect>();
        bool degraded = boxes.Count == 0;
        double densityWeight = degraded ? 0.0 : DensityWeight;
        double textureWeight = degraded ? DegradedTextureWeight : TextureWeight;
        double nonSolidWeight = degraded ? DegradedNonSolidWeight : NonSolidWeight;

        var windowHeight = Math.Max(MinWindowDimensionPx, (int)(screenHeight * WindowHeightRatio));
        var windowWidth = Math.Max(MinWindowDimensionPx, (int)(screenWidth * WindowWidthRatio));

        // Horizontal range: exclude 5% margins each side; the 80%-wide window is
        // centred inside the valid [5%, 95%] band → X1 = 10%, X2 = 90%.
        var x1 = (int)(screenWidth * (HorizontalMarginRatio + (1.0 - 2.0 * HorizontalMarginRatio - WindowWidthRatio) / 2.0));
        var x2 = Math.Min(x1 + windowWidth - 1, screenWidth - 1);

        // Vertical sweep: top edge from 30% to 85% of screen height, step 50% of
        // window height. Edge windows are clamped, never discarded (spec scenario).
        var yStart = (int)(screenHeight * CandidateTopRatio);
        var yEnd = (int)(screenHeight * CandidateBottomRatio);
        var step = Math.Max(1, (int)(windowHeight * WindowStepRatio));

        double bestScore = double.NegativeInfinity;
        RoiRect? best = null;

        for (var y1 = yStart; y1 <= yEnd; y1 += step)
        {
            var y2 = Math.Min(y1 + windowHeight - 1, screenHeight - 1);
            if (y2 < y1 || x2 < x1)
                continue;

            var window = new RoiRect(x1, y1, x2, y2);
            var score = ScoreWindow(
                window, rgba, stride, screenWidth, screenHeight,
                items, boxes, densityWeight, textureWeight, nonSolidWeight);

            if (score > bestScore)
            {
                bestScore = score;
                best = window;
            }
        }

        // Blank / near-uniform page: nothing crossed the threshold (spec R3).
        return bestScore >= MinSelectionScore ? best : null;
    }

    /// <summary>
    /// Decode the screenshot and validate it against the declared capture
    /// dimensions. A mismatch means the buffer is stale or mismatched — no ROI can
    /// be derived in a consistent pixel space, so null is returned.
    /// </summary>
    private static SKBitmap? Decode(byte[] screenshot, int screenWidth, int screenHeight)
    {
        SKBitmap? bitmap;
        try
        {
            bitmap = SKBitmap.Decode(screenshot);
        }
        catch (Exception)
        {
            // Not a valid image (e.g. corrupt bytes) — same tolerance as ImageResizer.
            return null;
        }

        if (bitmap is null)
            return null;

        if (bitmap.Width != screenWidth || bitmap.Height != screenHeight)
        {
            bitmap.Dispose();
            return null;
        }

        if (bitmap.ColorType != SKColorType.Rgba8888)
        {
            // Normalise to RGBA so the raw buffer layout is known (offset ×4 per pixel).
            var converted = bitmap.Copy(SKColorType.Rgba8888);
            bitmap.Dispose();
            return converted;
        }

        return bitmap;
    }

    /// <summary>
    /// Composite score for one window: positive factors (density/texture/non-solid)
    /// with the active weights, position modifiers (preferred band, fixed regions),
    /// minus the dynamic-element penalty. FAB overlap excludes the window entirely.
    /// </summary>
    private static double ScoreWindow(
        RoiRect window,
        byte[] rgba,
        int stride,
        int screenWidth,
        int screenHeight,
        ImmutableArray<MenuItem> items,
        IReadOnlyList<RoiRect> yoloBboxes,
        double densityWeight,
        double textureWeight,
        double nonSolidWeight)
    {
        var winWidth = window.X2 - window.X1 + 1;
        var winHeight = window.Y2 - window.Y1 + 1;
        var winArea = (double)winWidth * winHeight;

        // ── YOLO bbox density: Σ(intersection(boundsPx, window)) / window area ──
        double density = 0.0;
        foreach (var box in yoloBboxes)
        {
            var ix = Math.Min(box.X2, window.X2) - Math.Max(box.X1, window.X1) + 1;
            var iy = Math.Min(box.Y2, window.Y2) - Math.Max(box.Y1, window.Y1) + 1;
            if (ix > 0 && iy > 0)
                density += ix * iy;
        }
        density = Math.Min(1.0, density / winArea); // overlapping boxes → clamp

        // ── extract window grayscale (luma, integer math) ──
        var gray = new byte[winWidth * winHeight];
        for (var y = 0; y < winHeight; y++)
        {
            var src = (window.Y1 + y) * stride + window.X1 * 4;
            var dst = y * winWidth;
            for (var x = 0; x < winWidth; x++)
            {
                var off = src + x * 4;
                gray[dst + x] = (byte)((rgba[off] * 299 + rgba[off + 1] * 587 + rgba[off + 2] * 114 + 500) / 1000);
            }
        }

        var texture = ComputeTextureScore(gray, winWidth, winHeight);
        var nonSolid = ComputeNonSolidRatio(gray, winWidth, winHeight);

        double score = densityWeight * density + textureWeight * texture + nonSolidWeight * nonSolid;

        // ── position preference: prefer the 40%-65% band (PRD §3.5.1) ──
        var centreY = (window.Y1 + window.Y2) / 2.0;
        if (centreY < screenHeight * PreferredBandTopRatio || centreY > screenHeight * PreferredBandBottomRatio)
            score *= PreferredBandMultiplier;

        // ── fixed regions: status bar (y < 5%) / nav bar (y > 95%) → weight halved ──
        if (window.Y1 < screenHeight * HorizontalMarginRatio || window.Y2 > screenHeight * (1.0 - HorizontalMarginRatio))
            score *= FixedRegionMultiplier;

        // ── floating_button / fab overlap → exclude the window entirely ──
        if (ContainsFabType(items, window, screenWidth, screenHeight))
            return double.NegativeInfinity;

        // ── dynamic-element penalty (loading/banner/carousel/progressbar/video) ──
        score -= ComputeDynamicPenalty(items, window, screenWidth, screenHeight);

        return score;
    }

    /// <summary>
    /// Texture complexity: variance of the 4-neighbour Laplacian response over the
    /// window grayscale, normalised to [0,1]. Linear gradients have near-constant
    /// Laplacian → low variance → low texture score (guards against gradient
    /// backgrounds winning, per spec R4).
    /// </summary>
    private static double ComputeTextureScore(byte[] gray, int width, int height)
    {
        if (width < 3 || height < 3)
            return 0.0;

        long sum = 0;
        long sumSq = 0;
        long count = 0;
        for (var y = 1; y < height - 1; y++)
        {
            var row = y * width;
            for (var x = 1; x < width - 1; x++)
            {
                var i = row + x;
                var lap = 4 * gray[i] - gray[i - width] - gray[i + width] - gray[i - 1] - gray[i + 1];
                sum += lap;
                sumSq += lap * lap;
                count++;
            }
        }

        if (count == 0)
            return 0.0;

        var mean = (double)sum / count;
        var variance = (double)sumSq / count - mean * mean;
        return Math.Min(1.0, Math.Max(0.0, variance) / TextureVarianceNormalizer);
    }

    /// <summary>
    /// Non-solid ratio: proportion of pixels whose local 3×3 std-dev exceeds the
    /// noise threshold (15.0, matching ScrollSwipeConfig.PixelNoiseThreshold).
    /// Integral images make each neighbourhood query O(1).
    /// </summary>
    private static double ComputeNonSolidRatio(byte[] gray, int width, int height)
    {
        if (width < 1 || height < 1)
            return 0.0;

        var iw = width + 1;
        var sum = new long[iw * (height + 1)];
        var sumSq = new long[iw * (height + 1)];

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var iy = (y + 1) * iw;
            var iyPrev = y * iw;
            for (var x = 0; x < width; x++)
            {
                var g = gray[row + x];
                var idx = iy + x + 1;
                sum[idx] = sum[iyPrev + x + 1] + sum[iy + x] - sum[iyPrev + x] + g;
                sumSq[idx] = sumSq[iyPrev + x + 1] + sumSq[iy + x] - sumSq[iyPrev + x] + (long)g * g;
            }
        }

        long nonSolid = 0;
        for (var y = 0; y < height; y++)
        {
            var y0 = Math.Max(0, y - 1);
            var y1 = Math.Min(height - 1, y + 1);
            for (var x = 0; x < width; x++)
            {
                var x0 = Math.Max(0, x - 1);
                var x1 = Math.Min(width - 1, x + 1);

                var rectSum = RectSum(sum, iw, x0, y0, x1, y1);
                var rectSumSq = RectSum(sumSq, iw, x0, y0, x1, y1);
                var n = (long)(x1 - x0 + 1) * (y1 - y0 + 1);

                var mean = (double)rectSum / n;
                var variance = (double)rectSumSq / n - mean * mean;
                if (variance > NonSolidNoiseThresholdSquared)
                    nonSolid++;
            }
        }

        return (double)nonSolid / ((long)width * height);
    }

    /// <summary>O(1) rectangle sum from an integral image (inclusive bounds).</summary>
    private static long RectSum(long[] integral, int iw, int x0, int y0, int x1, int y1)
        => integral[(y1 + 1) * iw + x1 + 1] - integral[y0 * iw + x1 + 1] - integral[(y1 + 1) * iw + x0] + integral[y0 * iw + x0];

    /// <summary>
    /// Dynamic penalty: each blacklist-type item located inside the window
    /// contributes 0.3 (capped at 0.6). Items carry no boundsPx, so coverage is
    /// approximated by the normalised centre coordinate; the branch is inert until
    /// MenuItemType can express the YOLO dynamic labels (see DynamicTypeBlacklist).
    /// </summary>
    private static double ComputeDynamicPenalty(ImmutableArray<MenuItem> items, RoiRect window, int screenWidth, int screenHeight)
    {
        if (items.IsDefault)
            return 0.0;

        int dynamicInWindow = 0;
        foreach (var item in items)
        {
            if (!IsDynamicType(item.Type))
                continue;

            var cx = (int)(item.Coordinate.X * screenWidth);
            var cy = (int)(item.Coordinate.Y * screenHeight);
            if (cx >= window.X1 && cx <= window.X2 && cy >= window.Y1 && cy <= window.Y2)
                dynamicInWindow++;
        }

        return Math.Min(DynamicPenaltyCap, dynamicInWindow * DynamicPenaltyPerElement);
    }

    /// <summary>
    /// True when a floating_button / fab item is inside the window — such windows
    /// are excluded entirely (PRD §3.5.2). Same centre-coordinate approximation and
    /// inert-with-current-enum caveat as the dynamic penalty.
    /// </summary>
    private static bool ContainsFabType(ImmutableArray<MenuItem> items, RoiRect window, int screenWidth, int screenHeight)
    {
        if (items.IsDefault)
            return false;

        foreach (var item in items)
        {
            if (!IsFabType(item.Type))
                continue;

            var cx = (int)(item.Coordinate.X * screenWidth);
            var cy = (int)(item.Coordinate.Y * screenHeight);
            if (cx >= window.X1 && cx <= window.X2 && cy >= window.Y1 && cy <= window.Y2)
                return true;
        }

        return false;
    }

    private static bool IsDynamicType(MenuItemType type)
        => DynamicTypeBlacklist.Contains(type.ToString().ToLowerInvariant());

    private static bool IsFabType(MenuItemType type)
    {
        var s = type.ToString().ToLowerInvariant();
        return s.Contains("floating") || s == "fab";
    }
}
