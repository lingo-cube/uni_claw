namespace UniClaw.Core.Observation;

/// <summary>
/// Configuration for the <see cref="ObservationPipeline"/> (core-observation-pipeline D2).
/// UIA leg removed (delete-uia): the pipeline is pure AI passthrough plus
/// back-navigation analysis reuse. <see cref="EnablePopupDetection"/> is retained
/// for callers that still pass it; the popup heuristic itself was UIA-based and
/// is gone with the UIA parser.
/// </summary>
/// <param name="EnablePopupDetection">Retained for backward compatibility; the
/// UIA popup heuristic was removed with the UIA pipeline and no longer drives
/// any behavior.</param>
public sealed record class ObservationConfig(
    bool EnablePopupDetection = true)
{
    /// <summary>The default configuration.</summary>
    public static ObservationConfig Default { get; } = new();
}
