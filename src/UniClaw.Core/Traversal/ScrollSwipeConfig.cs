namespace UniClaw.Core.Traversal;

/// <summary>
/// ScrollSwipeConfig — swipe coordinates (normalised 0-1 + duration ms) + ROI snapshot
/// capture / comparison / anti-deadloop thresholds.
/// Engine-level defaults; page-level overrides via IScreenStateProvider.GetScrollSwipeConfig().
/// All thresholds are calibrated on real devices — never hard-coded in consuming code.
/// </summary>
public sealed record class ScrollSwipeConfig(
    // ── gesture (existing, unchanged) ──
    double StartX = 0.5,
    double StartY = 0.7,
    double EndX = 0.5,
    double EndY = 0.3,
    int DurationMs = 300,

    // ── stable snapshot capture ──
    int StableSampleMaxRetries = 5,
    int StableSampleIntervalMs = 100,
    int StableSampleMaxTimeMs = 3000,     // absolute timeout (lazy-load etc.) — exceed → Unknown

    // ── snapshot dimensions ──
    int RoiSnapshotWidth = 0,   // 0 = auto-detect from aspect ratio
    int RoiSnapshotHeight = 0,

    // ── similarity thresholds ──
    int HashDistanceThreshold = 10,      // dHash Hamming distance, range 0-64
    double MadThreshold = 12.75,         // Mean Absolute Difference, range 0-255 (≈5%)
    double PixelNoiseThreshold = 15.0,   // pixel-change noise floor, range 0-255
    double ChangedPixelRatio = 0.1,      // changed-pixel proportion, range 0-1

    // ── anti-deadloop ──
    int MaxConsecutiveUnknown = 3,

    // ── second-swipe distance ratio ──
    double SecondSwipeDistanceRatio = 0.5,  // 2nd swipe distance = original × this value

    // ── existing field (unchanged semantics) ──
    int MaxEmptyScrollRetries = 1
);
