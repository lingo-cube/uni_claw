namespace UniClaw.Core.Traversal;

/// <summary>
/// Device capability probe for UIAutomator (core-observation-pipeline D6).
/// Implemented by providers that can disable UIA-first analysis when the
/// device's UIAutomator is unavailable (car head units, WebView-only devices,
/// or after the first dump failure of the session).
/// </summary>
public interface IUiAutomatorAvailability
{
    /// <summary>
    /// True while UIAutomator dumps are believed to work on this device.
    /// Once false, stays false for the remainder of the session.
    /// </summary>
    bool IsUiAutomatorAvailable { get; }
}
