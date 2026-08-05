using System.Buffers.Binary;
using System.Collections.Immutable;
using UniClaw.Core.UniBrain;

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

    public async Task<RawScreenBuffer> CaptureRawScreenBufferAsync(CancellationToken ct = default)
    {
        var result = await _runner.RunAsync(
            new AdbCommandRequest(
                ImmutableArray.Create("exec-out", "screencap"),
                CaptureBinaryOutput: true),
            ct);
        ThrowIfCancelled(result, ct);

        if (!result.Succeeded)
            throw new AdbCommandException(
                "ADB raw screencap",
                new ShellResult(false, result.StandardOutput, result.StandardError));

        if (result.BinaryOutput.IsDefaultOrEmpty)
        {
            throw new AdbCommandException(
                "ADB raw screencap",
                new ShellResult(false, string.Empty, "raw screencap capture returned no bytes"));
        }

        if (result.BinaryOutput.Length < 12)
        {
            throw new AdbCommandException(
                "ADB raw screencap",
                new ShellResult(false, string.Empty, "ADB raw screencap header too short"));
        }

        // Android screencap raw header: uint32 LE width | height | pixel_format
        var header = result.BinaryOutput.AsSpan();
        var width = BinaryPrimitives.ReadUInt32LittleEndian(header);
        var height = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
        var pixelFormat = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);

        if (pixelFormat != 1)
        {
            throw new AdbCommandException(
                "ADB raw screencap",
                new ShellResult(
                    false,
                    string.Empty,
                    $"Unsupported pixel format: {pixelFormat} (expected 1 = RGBA_8888)"));
        }

        var pixelCount = (int)(width * height * 4);
        return new RawScreenBuffer(
            Pixels: result.BinaryOutput.Slice(12, pixelCount).ToArray(),
            Width: (int)width,
            Height: (int)height,
            PixelFormat: (int)pixelFormat);
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
