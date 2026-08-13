using SkiaSharp;

namespace UniClaw.Runtime.Adapters.Device;

/// <summary>Fresh device-scoped PNG capture through ADB. It does not interpret pixels.</summary>
public sealed class AdbScreenshotSource : IScreenshotSource
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);
    private readonly IAdbProcessRunner _runner;
    private readonly string _adbExecutable;
    private readonly string _serial;

    public AdbScreenshotSource(string serial, string adbExecutable = "adb")
        : this(new AdbProcessRunner(), serial, adbExecutable) { }

    internal AdbScreenshotSource(IAdbProcessRunner runner, string serial, string adbExecutable)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _serial = string.IsNullOrWhiteSpace(serial) ? throw new ArgumentException("Resolved device serial is required.", nameof(serial)) : serial;
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable) ? throw new ArgumentException("ADB executable is required.", nameof(adbExecutable)) : adbExecutable;
    }

    public async Task<ScreenshotCapture> CaptureAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(_adbExecutable, ["-s", _serial, "exec-out", "screencap", "-p"], CaptureTimeout, cancellationToken);
        if (result.TimedOut)
            throw new TimeoutException("ADB screenshot capture timed out.");
        if (!result.Started || result.ExitCode != 0)
            throw new InvalidOperationException("ADB screenshot capture failed: " + (result.FailureReason ?? result.StandardError));
        if (result.StandardOutput.Length == 0)
            throw new InvalidOperationException("ADB screenshot capture returned empty output.");

        SKBitmap? bitmap;
        try
        {
            bitmap = SKBitmap.Decode(result.StandardOutput);
        }
        catch (ArgumentNullException)
        {
            throw new InvalidOperationException("ADB screenshot capture did not return a valid image.");
        }
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            bitmap?.Dispose();
            throw new InvalidOperationException("ADB screenshot capture did not return a valid image.");
        }
        return new(bitmap, bitmap.Width, bitmap.Height);
    }
}
