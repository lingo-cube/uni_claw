using UniClaw.Core.UniBrain;

namespace UniClaw.Device;

public sealed class AdbScreenCapture : IScreenCapture
{
    private readonly IAdbSession _session;

    public AdbScreenCapture(
        IAdbSession session,
        TimeSpan? timeout = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public AdbScreenCapture(
        string serial,
        string adbPath = "adb",
        TimeSpan? timeout = null)
        : this(
            new ProcessAdbSession(new AdbCommandRunnerOptions(
                serial,
                adbPath,
                timeout ?? TimeSpan.FromSeconds(20))),
            timeout)
    {
    }

    public async Task<byte[]> CaptureAsync(CancellationToken ct = default)
    {
        return await _session.CaptureScreenshotAsync(ct);
    }
}
