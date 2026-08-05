namespace UniClaw.Core.Traversal;

/// <summary>
/// Standardised ROI snapshot: dHash (64-bit Hamming) + normalised greyscale matrix (0-255)
/// for pixel-level comparison.  Does NOT hold raw JPEG/PNG bytes.
/// </summary>
public sealed record RoiSnapshot(
    ulong PerceptualHash,
    byte[] GrayPixels,
    int Width,
    int Height,
    long FrameSeq
);
