using UniClaw.Kernel.Perception.Fusion;

namespace UniClaw.Kernel.Perception;

/// <summary>同一 capture 内 Fast YOLO/OCR 的最小 typed 传递。</summary>
public sealed record FastTextBasis(
    string CaptureId,
    string SessionCorrelation,
    string ObservationCycleId,
    IReadOnlyList<string> YoloDetections,
    IReadOnlyList<string> OcrTokens,
    DateTimeOffset CaptureTimestamp,
    bool ProviderAvailable = true,
    bool IsFresh = true)
{
    public IReadOnlyList<string> YoloDetections { get; } =
        (YoloDetections ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    public IReadOnlyList<string> OcrTokens { get; } =
        (OcrTokens ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    public bool HasDetection => YoloDetections.Count > 0 || OcrTokens.Count > 0;
    public bool IsValid => !string.IsNullOrWhiteSpace(CaptureId)
        && !string.IsNullOrWhiteSpace(SessionCorrelation)
        && !string.IsNullOrWhiteSpace(ObservationCycleId)
        && CaptureTimestamp != default;

    public bool AlignsWith(FusionCapture capture) => IsValid && capture is { IsValid: true }
        && string.Equals(CaptureId, capture.CaptureId, StringComparison.Ordinal)
        && string.Equals(SessionCorrelation, capture.SessionCorrelation, StringComparison.Ordinal)
        && string.Equals(ObservationCycleId, capture.ObservationCycleId, StringComparison.Ordinal)
        && CaptureTimestamp == capture.CaptureTimestamp;
}

public enum SlowTextGateStatus { Eligible, MissingFast, StaleFast, MisalignedFast, ProviderUnavailable }

public sealed record SlowTextGateResult(
    SlowTextGateStatus Status, FastTextBasis? Basis = null, string? Diagnostic = null)
{
    public bool Eligible => Status == SlowTextGateStatus.Eligible;
}

/// <summary>Text Slow 的前置门控；Visual Slow 不经过此门控。</summary>
public static class SlowTextGate
{
    public static SlowTextGateResult Evaluate(FusionCapture capture, FastTextBasis? basis)
    {
        if (basis is null || !basis.IsValid)
            return new(SlowTextGateStatus.MissingFast, Diagnostic: "fast YOLO/OCR basis missing");
        if (!basis.ProviderAvailable)
            return new(SlowTextGateStatus.ProviderUnavailable, basis, "fast provider unavailable");
        if (!basis.IsFresh)
            return new(SlowTextGateStatus.StaleFast, basis, "fast basis expired");
        if (!basis.AlignsWith(capture))
            return new(SlowTextGateStatus.MisalignedFast, basis, "fast basis capture/cycle/session misaligned");
        return new(SlowTextGateStatus.Eligible, basis);
    }
}
