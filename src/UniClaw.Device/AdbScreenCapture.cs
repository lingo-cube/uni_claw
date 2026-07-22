namespace UniClaw.Device;

/// <summary>
/// AdbScreenCapture — ADB-based screenshot capture.
/// Stub: throws NotImplementedException. Real implementation requires adb + Android SDK.
/// </summary>
public sealed class AdbScreenCapture
{
    /// <summary>
    /// Capture a screenshot from the connected Android device.
    /// </summary>
    /// <returns>PNG image bytes</returns>
    public Task<byte[]> CaptureAsync(CancellationToken ct = default)
        => throw new NotImplementedException("ADB screen capture not yet implemented.");
}
