namespace UniClaw.Runtime.Adapters.Device;

/// <summary>
/// Read-only physical mechanism readiness check. It has no session lifecycle,
/// semantic authority, or dispatch capability.
/// </summary>
public sealed class AdbDevicePreflight
{
    private readonly AdbDeviceResolver _resolver;

    public AdbDevicePreflight(AdbDeviceResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<AdbDevicePreflightResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var resolution = await _resolver.ResolveAsync(cancellationToken);
        if (!resolution.IsResolved)
            return new(false, null, false, false, false, resolution.FailureReason);

        var serial = resolution.Serial!;
        var processResult = await _resolver.Runner.RunAsync(
            _resolver.AdbExecutable, ["-s", serial, "get-state"], TimeSpan.FromSeconds(5), cancellationToken);
        var channelReady = processResult.Started && !processResult.TimedOut && processResult.ExitCode == 0
            && System.Text.Encoding.UTF8.GetString(processResult.StandardOutput).Trim() == "device";
        if (!channelReady)
            return new(true, serial, false, false, false, processResult.FailureReason ?? processResult.StandardError ?? "ADB device channel is unavailable.");

        var dispatchProbe = await _resolver.Runner.RunAsync(
            _resolver.AdbExecutable, ["-s", serial, "shell", "true"], TimeSpan.FromSeconds(5), cancellationToken);
        var dispatchReady = dispatchProbe.Started && !dispatchProbe.TimedOut && dispatchProbe.ExitCode == 0;
        if (!dispatchReady)
        {
            return new(
                true,
                serial,
                true,
                false,
                false,
                dispatchProbe.FailureReason ?? dispatchProbe.StandardError ?? "ADB dispatch channel is unavailable.");
        }

        try
        {
            var screenshot = await new AdbScreenshotSource(
                _resolver.Runner, serial, _resolver.AdbExecutable).CaptureAsync(cancellationToken);
            screenshot.ScreenshotData.Dispose();
            return new(true, serial, true, true, true, null);
        }
        catch (Exception exception) when (exception is TimeoutException or InvalidOperationException)
        {
            return new(true, serial, true, false, true, exception.Message);
        }
    }
}

/// <summary>Read-only mechanism readiness facts; none of these assert semantic world state.</summary>
public sealed record AdbDevicePreflightResult(
    bool AdbExecutableAndDeviceSelectionReady,
    string? Serial,
    bool DeviceScopedChannelReady,
    bool ScreenshotMechanismReady,
    bool DispatchMechanismReady,
    string? FailureReason)
{
    public bool IsReady => AdbExecutableAndDeviceSelectionReady
        && DeviceScopedChannelReady
        && ScreenshotMechanismReady
        && DispatchMechanismReady;
}
