using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Traversal;

/// <summary>
/// Captures a stable ROI snapshot by sampling until consecutive identical frames
/// are observed, or the retry / absolute-time budgets are exhausted.
///
/// Stability model: two consecutive frames whose <see cref="SnapshotComparer.Compare"/>
/// result reports <see cref="SnapshotComparison.IsSame"/> == true count as one
/// "stable pair". Pre-scroll (S0) requires 1 stable pair (2 consecutive identical
/// frames); post-scroll (S1/S2) requires 2 stable pairs (3 consecutive identical
/// frames) because the scroll settles progressively and the first settled frame
/// may still be mid-animation.
/// </summary>
internal sealed class StableFrameCapturer
{
    private readonly IScreenCapture _capture;
    private readonly ScrollSwipeConfig _config;

    public StableFrameCapturer(IScreenCapture capture, ScrollSwipeConfig config)
    {
        _capture = capture;
        _config = config;
    }

    /// <summary>
    /// Captures the stable pre-scroll snapshot (S0).
    /// </summary>
    /// <param name="roi">ROI region to sample.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The most recent stable snapshot, or <c>null</c> when the screen never
    /// settles, the capture cannot be decoded, or the absolute-time budget is exceeded.
    /// </returns>
    public async Task<RoiSnapshot?> CaptureBeforeScrollAsync(RoiRect roi, CancellationToken ct = default)
        => await CaptureStableAsync(roi, requiredConsecutivePairs: 1, ct);

    /// <summary>
    /// Captures the stable post-scroll snapshot (S1/S2).
    /// </summary>
    /// <param name="roi">ROI region to sample.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The most recent stable snapshot, or <c>null</c> when the screen never
    /// settles, the capture cannot be decoded, or the absolute-time budget is exceeded.
    /// </returns>
    public async Task<RoiSnapshot?> CaptureAfterScrollAsync(RoiRect roi, CancellationToken ct = default)
        => await CaptureStableAsync(roi, requiredConsecutivePairs: 2, ct);

    /// <summary>
    /// Samples the ROI until <paramref name="requiredConsecutivePairs"/> consecutive
    /// identical frame pairs are observed. <see cref="ScrollSwipeConfig.StableSampleMaxRetries"/>
    /// is the TOTAL number of capture iterations (not retries after a first failure),
    /// and <see cref="ScrollSwipeConfig.StableSampleMaxTimeMs"/> is an absolute timeout
    /// checked at the start of every iteration. A null snapshot from the generator
    /// (undecodable capture / empty ROI) aborts the sampling immediately as "unknown".
    /// </summary>
    private async Task<RoiSnapshot?> CaptureStableAsync(
        RoiRect roi, int requiredConsecutivePairs, CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;
        var consecutivePairs = 0;
        RoiSnapshot? lastSnapshot = null;
        var frameSeq = 0L;

        for (var iteration = 0;
             iteration < _config.StableSampleMaxRetries && consecutivePairs < requiredConsecutivePairs;
             iteration++)
        {
            // Absolute timeout (lazy-load etc.) — exceeded → Unknown.
            if ((DateTimeOffset.UtcNow - startTime).TotalMilliseconds > _config.StableSampleMaxTimeMs)
                return null;

            ct.ThrowIfCancellationRequested();

            var t0 = DateTimeOffset.UtcNow;
            var screenshotBytes = await _capture.CaptureAsync(ct);
            var snapshot = RoiSnapshotGenerator.Generate(
                screenshotBytes, roi, _config.RoiSnapshotWidth, _config.RoiSnapshotHeight, frameSeq++);

            // Cannot generate a snapshot from this frame → Unknown.
            if (snapshot is null)
                return null;

            if (lastSnapshot is not null)
            {
                var comparison = SnapshotComparer.Compare(lastSnapshot, snapshot, _config);
                consecutivePairs = comparison.IsSame ? consecutivePairs + 1 : 0;
            }

            lastSnapshot = snapshot;

            // Dynamic delay: hold the inter-sample cadence at StableSampleIntervalMs
            // regardless of how long the capture itself took (never negative).
            var elapsed = (DateTimeOffset.UtcNow - t0).TotalMilliseconds;
            var wait = Math.Max(0, _config.StableSampleIntervalMs - (int)elapsed);
            await Task.Delay(wait, ct);
        }

        return consecutivePairs >= requiredConsecutivePairs ? lastSnapshot : null;
    }
}
