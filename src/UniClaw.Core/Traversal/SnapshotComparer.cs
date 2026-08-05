using System.Numerics;

namespace UniClaw.Core.Traversal;

/// <summary>
/// Pure-function comparer for <see cref="RoiSnapshot"/>s, used on every scroll
/// to detect whether the screen has stabilised / changed (anti-deadloop).
/// No side effects, no IO — safe to call from hot paths.
/// </summary>
internal static class SnapshotComparer
{
    /// <summary>
    /// Compare two ROI snapshots using three composite metrics.
    /// IsSame is true ONLY when ALL three pass their respective thresholds (AND semantics):
    /// 1. HashDistance          (Hamming distance, 0-64)     <= HashDistanceThreshold
    /// 2. MeanAbsoluteDifference (0-255 grayscale)           <= MadThreshold
    /// 3. ChangedPixelRatio     (0-1 proportion)             <= ChangedPixelRatio
    /// If GrayPixels are null or have different lengths, the comparison is treated as
    /// "cannot verify" → IsSame = false (MAD/ratio report worst-case values 255.0 / 1.0).
    /// </summary>
    public static SnapshotComparison Compare(
        RoiSnapshot a,
        RoiSnapshot b,
        ScrollSwipeConfig config)
    {
        // ── Metric 1: perceptual hash distance (dHash Hamming, 0-64) ──
        // No pixel data required, always computable.
        ulong xor = a.PerceptualHash ^ b.PerceptualHash;
        int hashDistance = BitOperations.PopCount(xor);

        // ── Metric 2 & 3: pixel-level metrics ──
        // Both GrayPixels arrays must exist and be the same length; otherwise the
        // snapshots are not comparable → treat as different (IsSame = false).
        byte[] aPixels = a.GrayPixels;
        byte[] bPixels = b.GrayPixels;
        if (aPixels is null || bPixels is null || aPixels.Length != bPixels.Length)
        {
            return new SnapshotComparison(
                HashDistance: hashDistance,
                MeanAbsoluteDifference: 255.0, // worst case: max grayscale difference
                ChangedPixelRatio: 1.0,        // worst case: every pixel changed
                IsSame: false);
        }

        // Single pass over both arrays: accumulate absolute difference sum and
        // count pixels that exceed the noise floor.  Simple loop (not LINQ) —
        // this runs on every scroll, per-frame.
        int length = aPixels.Length;
        long diffSum = 0;
        int changedCount = 0;
        for (int i = 0; i < length; i++)
        {
            int diff = Math.Abs(aPixels[i] - bPixels[i]);
            diffSum += diff;
            if (diff > config.PixelNoiseThreshold)
            {
                changedCount++;
            }
        }

        double meanAbsoluteDifference = diffSum / (double)length;
        double changedPixelRatio = changedCount / (double)length;

        // ── Composite verdict: ALL three metrics must pass ──
        bool isSame = hashDistance <= config.HashDistanceThreshold
                      && meanAbsoluteDifference <= config.MadThreshold
                      && changedPixelRatio <= config.ChangedPixelRatio;

        return new SnapshotComparison(
            HashDistance: hashDistance,
            MeanAbsoluteDifference: meanAbsoluteDifference,
            ChangedPixelRatio: changedPixelRatio,
            IsSame: isSame);
    }
}
