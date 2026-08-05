using System.Runtime.InteropServices;
using SkiaSharp;

namespace UniClaw.Core.Traversal;

/// <summary>
/// Standardises an ROI region of a full screenshot into an <see cref="RoiSnapshot"/>:
///
/// decode → crop ROI (clamped) → grayscale (0.299R + 0.587G + 0.114B) →
/// fixed-size resize → light 3×3 gaussian blur → GrayPixels (0-255),
/// plus a separate internal 9×8 resize → 64-bit dHash for perceptual comparison.
///
/// Failure contract: returns <c>null</c> (never throws) when the input cannot be
/// decoded or the ROI clamps to an empty region — the caller decides how to treat
/// an unavailable snapshot.  All SkiaSharp bitmaps are disposed before returning.
/// </summary>
internal static class RoiSnapshotGenerator
{
    /// <summary>Target snapshot size for landscape ROIs (width &gt; height).</summary>
    private const int LandscapeWidth = 256;
    private const int LandscapeHeight = 128;

    /// <summary>Target snapshot size for portrait ROIs (height &gt;= width).</summary>
    private const int PortraitWidth = 128;
    private const int PortraitHeight = 256;

    /// <summary>dHash source grid: 8 rows × 9 pixels → 8 bits/row → 64 bits.</summary>
    private const int HashWidth = 9;
    private const int HashHeight = 8;

    /// <summary>
    /// Generates a standardised snapshot of the ROI inside <paramref name="screenshot"/>.
    /// </summary>
    /// <param name="screenshot">Full screenshot bytes (PNG/JPEG).</param>
    /// <param name="roi">Pixel-coordinate region to standardise.</param>
    /// <param name="snapshotWidth">Explicit snapshot width; 0 = auto-detect by orientation.</param>
    /// <param name="snapshotHeight">Explicit snapshot height; 0 = auto-detect by orientation.</param>
    /// <param name="frameSeq">Source frame sequence, passed through to the snapshot.</param>
    /// <returns>
    /// The standardised snapshot, or <c>null</c> if the bytes cannot be decoded or
    /// the ROI contains no pixels after clamping to the bitmap bounds.
    /// </returns>
    public static RoiSnapshot? Generate(
        byte[] screenshot,
        RoiRect roi,
        int snapshotWidth = 0,
        int snapshotHeight = 0,
        long frameSeq = 0)
    {
        if (screenshot is null || screenshot.Length == 0)
            return null;

        // ── decode ──────────────────────────────────────────────
        using var decoded = SafeDecode(screenshot);
        if (decoded is null)
            return null;

        // ── crop ROI, clamped to bitmap bounds ──────────────────
        var srcX = Math.Max(0, roi.X1);
        var srcY = Math.Max(0, roi.Y1);
        var srcX2 = Math.Min(decoded.Width, roi.X2);
        var srcY2 = Math.Min(decoded.Height, roi.Y2);
        if (srcX2 <= srcX || srcY2 <= srcY)
            return null; // ROI is fully outside the bitmap
        var cropW = srcX2 - srcX;
        var cropH = srcY2 - srcY;

        using var cropped = new SKBitmap(cropW, cropH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(cropped))
        {
            var srcRect = new SKRect(srcX, srcY, srcX2, srcY2);
            var dstRect = new SKRect(0, 0, cropW, cropH);
            canvas.DrawBitmap(decoded, srcRect, dstRect, paint: null);
        }

        // ── grayscale: gray = 0.299R + 0.587G + 0.114B ─────────
        var grayFull = ToGrayscale(cropped);

        // ── fixed-size resize (explicit dims, or auto by orientation) ──
        var targetW = snapshotWidth > 0 && snapshotHeight > 0
            ? snapshotWidth
            : cropW > cropH ? LandscapeWidth : PortraitWidth;
        var targetH = snapshotWidth > 0 && snapshotHeight > 0
            ? snapshotHeight
            : cropW > cropH ? LandscapeHeight : PortraitHeight;

        using var grayBmp = WrapPixels(grayFull, cropW, cropH);
        using var resized = grayBmp.Resize(
            new SKImageInfo(targetW, targetH, SKColorType.Gray8, SKAlphaType.Opaque),
            SKSamplingOptions.Default);
        if (resized is null)
            return null;

        // ── light gaussian blur + GrayPixels (0-255) ────────────
        var grayPixels = Blur3x3(ReadPixels(resized), targetW, targetH);

        // ── dHash: internal 9×8 resize → 64-bit comparison hash ─
        using var hashBmp = WrapPixels(grayPixels, targetW, targetH);
        using var hashResized = hashBmp.Resize(
            new SKImageInfo(HashWidth, HashHeight, SKColorType.Gray8, SKAlphaType.Opaque),
            SKSamplingOptions.Default);
        if (hashResized is null)
            return null;

        var hash = ComputeDHash(ReadPixels(hashResized));

        return new RoiSnapshot(hash, grayPixels, targetW, targetH, frameSeq);
    }

    /// <summary>Decodes image bytes; returns null on invalid/corrupt input.</summary>
    private static SKBitmap? SafeDecode(byte[] screenshot)
    {
        try
        {
            return SKBitmap.Decode(screenshot);
        }
        catch (Exception)
        {
            // Not a valid image (e.g. truncated capture) — no snapshot.
            return null;
        }
    }

    /// <summary>
    /// Converts an Rgba8888 bitmap to single-channel grayscale bytes (0-255)
    /// using the Rec. 601 luma weights.
    /// </summary>
    private static byte[] ToGrayscale(SKBitmap bitmap)
    {
        var pixels = new byte[bitmap.Width * bitmap.Height];
        var src = bitmap.GetPixels();
        var rowBytes = bitmap.RowBytes;
        var row = new byte[rowBytes];
        for (var y = 0; y < bitmap.Height; y++)
        {
            Marshal.Copy(src + y * rowBytes, row, 0, rowBytes);
            for (var x = 0; x < bitmap.Width; x++)
            {
                var i = x * 4;
                var r = row[i];      // Rgba8888 memory order: R, G, B, A
                var g = row[i + 1];
                var b = row[i + 2];
                pixels[y * bitmap.Width + x] = (byte)Math.Round(r * 0.299 + g * 0.587 + b * 0.114);
            }
        }
        return pixels;
    }

    /// <summary>
    /// Copies the bitmap's grayscale contents into a contiguous byte array,
    /// honouring RowBytes so padded rows are read correctly.
    /// </summary>
    private static byte[] ReadPixels(SKBitmap bitmap)
    {
        var pixels = new byte[bitmap.Width * bitmap.Height];
        var src = bitmap.GetPixels();
        var rowBytes = bitmap.RowBytes;
        for (var y = 0; y < bitmap.Height; y++)
            Marshal.Copy(src + y * rowBytes, pixels, y * bitmap.Width, bitmap.Width);
        return pixels;
    }

    /// <summary>
    /// Wraps a contiguous byte array (rowBytes == width) as a non-allocating
    /// Gray8 bitmap. The caller must keep <paramref name="pixels"/> alive for
    /// the bitmap's lifetime.
    /// </summary>
    private static SKBitmap WrapPixels(byte[] pixels, int width, int height)
    {
        var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque),
            rowBytes: width);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.SetPixels(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
        return bitmap;
    }

    /// <summary>
    /// Light 3×3 gaussian-ish blur (kernel 1/2/1 · 2/4/2 · 1/2/1, sigma ≈ 0.85),
    /// normalised to 1/16; edges replicate the nearest pixel.  Applied on the
    /// grayscale plane — equivalent to blurring RGB then converting, since both
    /// operations are linear.
    /// </summary>
    private static byte[] Blur3x3(byte[] src, int width, int height)
    {
        var dst = new byte[src.Length];
        for (var y = 0; y < height; y++)
        {
            var y0 = Math.Max(0, y - 1);
            var y1 = Math.Min(height - 1, y + 1);
            for (var x = 0; x < width; x++)
            {
                var x0 = Math.Max(0, x - 1);
                var x1 = Math.Min(width - 1, x + 1);
                var acc =
                    src[y0 * width + x0] + 2 * src[y0 * width + x] + src[y0 * width + x1] +
                    2 * src[y * width + x0] + 4 * src[y * width + x] + 2 * src[y * width + x1] +
                    src[y1 * width + x0] + 2 * src[y1 * width + x] + src[y1 * width + x1];
                dst[y * width + x] = (byte)(acc / 16);
            }
        }
        return dst;
    }

    /// <summary>
    /// 64-bit dHash: per row, bit k (MSB-first) = 1 when pixel[k] &gt; pixel[k+1].
    /// Row 0 lands in bits 56-63, row 7 in bits 0-7.
    /// </summary>
    private static ulong ComputeDHash(byte[] hashPixels)
    {
        ulong hash = 0;
        for (var y = 0; y < HashHeight; y++)
        {
            var row = y * HashWidth;
            ulong rowBits = 0;
            for (var x = 0; x < HashWidth - 1; x++)
                rowBits = (rowBits << 1) | (hashPixels[row + x] > hashPixels[row + x + 1] ? 1UL : 0UL);
            hash = (hash << 8) | rowBits;
        }
        return hash;
    }
}
