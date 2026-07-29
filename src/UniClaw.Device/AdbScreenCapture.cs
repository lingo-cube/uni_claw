using System.Collections.Immutable;
using UniClaw.Core.UniBrain;

namespace UniClaw.Device;

public sealed class AdbScreenCapture : IScreenCapture
{
    private readonly IAdbCommandRunner _runner;
    private readonly TimeSpan _timeout;

    public AdbScreenCapture(
        IAdbCommandRunner runner,
        TimeSpan? timeout = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public AdbScreenCapture(
        string serial,
        string adbPath = "adb",
        TimeSpan? timeout = null)
        : this(
            new AdbCommandRunner(new AdbCommandRunnerOptions(
                serial,
                adbPath,
                timeout ?? TimeSpan.FromSeconds(20))),
            timeout)
    {
    }

    public async Task<byte[]> CaptureAsync(CancellationToken ct = default)
    {
        var result = await _runner.RunAsync(
            new AdbCommandRequest(
                ImmutableArray.Create("exec-out", "screencap", "-p"),
                _timeout,
                CaptureBinaryOutput: true),
            ct);

        ThrowIfCancelled(result, ct);
        if (!result.Succeeded)
            throw new AdbCommandException("ADB screenshot capture", result);
        if (result.BinaryOutput.IsDefaultOrEmpty)
        {
            throw new AdbCommandException(
                "ADB screenshot capture",
                result with
                {
                    Failure = new AdbCommandFailure(
                        "invalid_output",
                        "ADB screenshot capture returned no bytes"),
                });
        }

        return result.BinaryOutput.ToArray();
    }

    private static void ThrowIfCancelled(
        AdbCommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(
                result.Failure.Message,
                cancellationToken);
    }
}
