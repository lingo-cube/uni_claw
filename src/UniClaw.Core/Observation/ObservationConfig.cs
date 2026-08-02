namespace UniClaw.Core.Observation;

/// <summary>
/// Configuration for the <see cref="ObservationPipeline"/> (core-observation-pipeline D2).
/// UIA-first is kept (UIA hits &gt;90% on standard Settings pages in ~1s vs ~60s for
/// AI vision); the pipeline skips the UIA leg when <see cref="UIA_Enabled"/> is false
/// or the device capability is unavailable.
/// </summary>
/// <param name="UIA_MinItems">Minimum interactive items a UIAutomator dump must
/// yield for the UIA-only result to be trusted. Fewer items (popups, WebViews,
/// error screens) fall through to AI vision.</param>
/// <param name="EnablePopupDetection">When true, popup/dialog button labels in
/// the UIA hierarchy trigger the AI fallback. When false the popup heuristic
/// never forces an AI call.</param>
/// <param name="SkipUIAOnBackNavigation">When true, an analysis requested right
/// after a back navigation reuses the previously cached page analysis — no ADB
/// UIA dump and no AI call (the page returned to was already analyzed).</param>
/// <param name="UIA_Enabled">Master switch for the UIA leg. False routes every
/// observation straight to AI vision.</param>
public sealed record class ObservationConfig(
    int UIA_MinItems = 3,
    bool EnablePopupDetection = true,
    bool SkipUIAOnBackNavigation = true,
    bool UIA_Enabled = true)
{
    /// <summary>The default configuration (UIA-first, popup detection on, back reuse on).</summary>
    public static ObservationConfig Default { get; } = new();
}
