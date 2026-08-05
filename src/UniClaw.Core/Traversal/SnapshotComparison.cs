namespace UniClaw.Core.Traversal;

/// <summary>
/// Composite comparison result between two <see cref="RoiSnapshot"/>s.
/// IsSame is true only when ALL three metrics pass their respective thresholds.
/// </summary>
public sealed record SnapshotComparison(
    int HashDistance,               // 0-64 Hamming
    double MeanAbsoluteDifference,  // 0-255 grayscale
    double ChangedPixelRatio,       // 0-1 proportion
    bool IsSame
);
