using SkiaSharp;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// Lightweight screenshot preprocessor: optional top / bottom crop (status bar,
/// navigation chrome), proportional downscale to <paramref name="maxWidth"/> px,
/// and JPEG encode.
///
/// Crop parameters are expressed as ratios of image height (0.0–1.0).
/// Defaults calibrated against Android Settings on a 1080×1920 emulator:
///   top    120 px / 1920 ≈ 0.0625  (status bar + search chrome)
///   bottom 120 px / 1920 ≈ 0.0625  (empty spacing below list)
///
/// Override via environment variables:
///   UNICLAW_IMAGE_MAX_WIDTH      — max width px (default 720)
///   UNICLAW_IMAGE_CROP_TOP       — top crop ratio (default 0.0625)
///   UNICLAW_IMAGE_CROP_BOTTOM    — bottom crop ratio (default 0.0625)
///   UNICLAW_IMAGE_JPEG_QUALITY   — JPEG quality 1–100 (default 85)
///
/// SkiaSharp is used so no native binary dependency is required beyond the
/// NuGet package.
/// </summary>
public static class ImageResizer
{
    /// <summary>Default max-width px for the vision pipeline (matching production calibration).</summary>
    public const int DefaultMaxWidth = 720;
    /// <summary>Default top crop ratio (6.25% ≈ 120 px on 1920-height).</summary>
    public const double DefaultCropTopRatio = 0.0625;
    /// <summary>Default bottom crop ratio (6.25% ≈ 120 px on 1920-height).</summary>
    public const double DefaultCropBottomRatio = 0.0625;
    /// <summary>Default JPEG quality (85 = visually indistinguishable from lossless for UI screenshots).</summary>
    public const int DefaultJpegQuality = 85;

    /// <summary>
    /// Crop and resize <paramref name="raw"/> (raw RGBA from adb screencap without -p),
    /// then JPEG-encode. Skips SKBitmap.Decode — raw pixels go directly via SetPixels,
    /// saving one device-side PNG encode + one host-side PNG decode per frame.
    /// </summary>
    public static byte[] ProcessRaw(
        RawScreenBuffer raw,
        int maxWidth = DefaultMaxWidth,
        double cropTopRatio = DefaultCropTopRatio,
        double cropBottomRatio = DefaultCropBottomRatio,
        int jpegQuality = DefaultJpegQuality)
    {
        if (raw.Pixels is null || raw.Pixels.Length == 0)
            return Array.Empty<byte>();

        // Zero-decode: raw RGBA bytes → SKBitmap via SetPixels
        using var source = new SKBitmap(raw.Width, raw.Height,
            SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(raw.Pixels,
            System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            source.SetPixels(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }

        // ── crop ──────────────────────────────────────────────
        var topPx = cropTopRatio > 0
            ? (int)(source.Height * cropTopRatio)
            : 0;
        var bottomPx = cropBottomRatio > 0
            ? (int)(source.Height * cropBottomRatio)
            : 0;
        var cropHeight = source.Height - topPx - bottomPx;

        SKBitmap processed;
        if (cropHeight <= 0 || (topPx == 0 && bottomPx == 0))
        {
            processed = source;
        }
        else
        {
            processed = new SKBitmap(source.Width, cropHeight,
                SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var canvas = new SKCanvas(processed);
            var srcRect = new SKRect(0, topPx, source.Width, source.Height - bottomPx);
            var dstRect = new SKRect(0, 0, source.Width, cropHeight);
            canvas.DrawBitmap(source, srcRect, dstRect, paint: null);
        }

        // ── resize + JPEG encode ──────────────────────────────
        var result = ResizeAndEncode(processed, maxWidth, jpegQuality);
        if (processed != source)
            processed.Dispose();
        return result;
    }

    /// <summary>
    /// Crop and resize <paramref name="imageBytes"/>.
    /// </summary>
    /// <param name="imageBytes">Raw screenshot bytes (PNG/JPEG).</param>
    /// <param name="maxWidth">Max output width px.</param>
    /// <param name="cropTopRatio">Fraction of image height to crop from the top (0=none).</param>
    /// <param name="cropBottomRatio">Fraction of image height to crop from the bottom (0=none).</param>
    /// <param name="jpegQuality">JPEG encode quality 1–100.</param>
    public static byte[] ResizeToMaxWidth(
        byte[] imageBytes,
        int maxWidth = DefaultMaxWidth,
        double cropTopRatio = DefaultCropTopRatio,
        double cropBottomRatio = DefaultCropBottomRatio,
        int jpegQuality = DefaultJpegQuality)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return Array.Empty<byte>();

        SKBitmap? source;
        try
        {
            source = SKBitmap.Decode(imageBytes);
        }
        catch (Exception)
        {
            // Not a valid image (e.g. test fake bytes) — return as-is.
            return imageBytes;
        }

        if (source is null)
            return imageBytes;

        // ── compute crop bounds ───────────────────────────────
        var topPx = cropTopRatio > 0
            ? (int)(source.Height * cropTopRatio)
            : 0;
        var bottomPx = cropBottomRatio > 0
            ? (int)(source.Height * cropBottomRatio)
            : 0;
        var cropHeight = source.Height - topPx - bottomPx;

        if (cropHeight <= 0)
            return ResizeAndEncode(source, maxWidth, jpegQuality);

        if (topPx == 0 && bottomPx == 0)
            return ResizeAndEncode(source, maxWidth, jpegQuality);

        // ── crop ──────────────────────────────────────────────
        using var cropped = new SKBitmap(source.Width, cropHeight);
        using var canvas = new SKCanvas(cropped);
        var srcRect = new SKRect(0, topPx, source.Width, source.Height - bottomPx);
        var dstRect = new SKRect(0, 0, source.Width, cropHeight);
        canvas.DrawBitmap(source, srcRect, dstRect, paint: null);
        return ResizeAndEncode(cropped, maxWidth, jpegQuality);
    }

    private static byte[] ResizeAndEncode(SKBitmap bitmap, int maxWidth, int jpegQuality)
    {
        if (bitmap.Width <= maxWidth)
        {
            // Already within width limit — return original bytes as-is.
            // (The caller still has the original imageBytes; we encode as JPEG
            // only when resizing happened.)
            return bitmap.Encode(SKEncodedImageFormat.Jpeg, jpegQuality).ToArray();
        }

        var ratio = (double)maxWidth / bitmap.Width;
        var newHeight = (int)(bitmap.Height * ratio);
        using var resized = bitmap.Resize(
            new SKImageInfo(maxWidth, newHeight),
            SKSamplingOptions.Default);
        return resized.Encode(SKEncodedImageFormat.Jpeg, jpegQuality).ToArray();
    }
}
