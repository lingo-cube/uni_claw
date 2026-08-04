using System.Collections.Immutable;

namespace UniClaw.Device;

public sealed class ProcessAdbSession : IAdbSession
{
    private readonly AdbCommandRunner _runner;

    public string Serial => _runner.Serial;

    public ProcessAdbSession(AdbCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public ProcessAdbSession(AdbCommandRunnerOptions options)
        : this(new AdbCommandRunner(options))
    {
    }

    public async Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default)
    {
        var result = await _runner.RunAsync(
            new AdbCommandRequest(
                ImmutableArray.Create("exec-out", "screencap", "-p"),
                CaptureBinaryOutput: true),
            ct);
        ThrowIfCancelled(result, ct);

        if (!result.Succeeded)
            throw new AdbCommandException(
                "ADB screenshot capture",
                new ShellResult(false, result.StandardOutput, result.StandardError));

        if (result.BinaryOutput.IsDefaultOrEmpty)
        {
            throw new AdbCommandException(
                "ADB screenshot capture",
                new ShellResult(false, string.Empty, "screenshot capture returned no bytes"));
        }

        return result.BinaryOutput.ToArray();
    }

    public async Task<ShellResult> ExecuteShellAsync(
        string command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Shell command is required.", nameof(command));

        var result = await _runner.RunAsync(
            AdbCommandRequest.Create(["shell", command]),
            ct);
        ThrowIfCancelled(result, ct);

        return new ShellResult(
            result.Succeeded,
            result.StandardOutput,
            result.StandardError);
    }

    public async Task<string> DumpUiHierarchyAsync(CancellationToken ct = default)
    {
        const string remotePath = "/sdcard/uniclaw-window-dump.xml";

        var dumpResult = await _runner.RunAsync(
            AdbCommandRequest.Create(["shell", "uiautomator", "dump", remotePath]),
            ct);
        ThrowIfCancelled(dumpResult, ct);

        if (!dumpResult.Succeeded)
        {
            throw new AdbCommandException(
                "UI dump",
                new ShellResult(
                    false,
                    dumpResult.StandardOutput,
                    dumpResult.StandardError));
        }

        var catResult = await _runner.RunAsync(
            new AdbCommandRequest(
                ImmutableArray.Create("exec-out", "cat", remotePath),
                CaptureBinaryOutput: true),
            ct);
        ThrowIfCancelled(catResult, ct);

        if (!catResult.Succeeded)
        {
            throw new AdbCommandException(
                "UI dump read",
                new ShellResult(
                    false,
                    catResult.StandardOutput,
                    catResult.StandardError));
        }

        if (catResult.BinaryOutput.IsDefaultOrEmpty)
        {
            throw new AdbCommandException(
                "UI dump read",
                new ShellResult(false, string.Empty, "UI dump returned no bytes"));
        }

        return System.Text.Encoding.UTF8.GetString(
            catResult.BinaryOutput.ToArray());
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static void ThrowIfCancelled(
        AdbCommandResult result,
        CancellationToken ct)
    {
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(result.Failure.Message, ct);
    }
}
