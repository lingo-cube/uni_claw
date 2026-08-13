using System.Diagnostics;
using System.ComponentModel;
using System.Text;

namespace UniClaw.Runtime.Adapters.Device;

/// <summary>
/// Bounded, argument-safe process mechanism used only by the concrete ADB adapters.
/// It has no semantic interpretation or retry authority.
/// </summary>
internal interface IAdbProcessRunner
{
    Task<AdbProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed record AdbProcessResult(
    bool Started,
    bool TimedOut,
    int? ExitCode,
    byte[] StandardOutput,
    string StandardError,
    string? FailureReason);

internal sealed class AdbProcessRunner : IAdbProcessRunner
{
    // 64 MiB accepts a high-resolution lossless PNG while still bounding a
    // malformed or hostile process response. Screenshot acquisition is the
    // only expected binary stdout consumer of this runner.
    private const int MaximumCapturedOutputBytes = 64 * 1024 * 1024;

    public async Task<AdbProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return new(false, false, null, [], string.Empty, "ADB process did not start.");
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new(false, false, null, [], string.Empty, exception.Message);
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        var stdoutTask = ReadBoundedAsync(process.StandardOutput.BaseStream, CancellationToken.None);
        var stderrTask = ReadBoundedTextAsync(process.StandardError, CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdoutTask, stderrTask);
            return new(true, true, null, [], string.Empty, "ADB process timed out.");
        }
        catch
        {
            TryKill(process);
            await Task.WhenAll(stdoutTask, stderrTask);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new(true, false, process.ExitCode, stdout, stderr, null);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaximumCapturedOutputBytes)
                throw new InvalidOperationException("ADB process stdout exceeded the capture bound.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private static async Task<string> ReadBoundedTextAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var value = await reader.ReadToEndAsync(cancellationToken);
        return value.Length <= MaximumCapturedOutputBytes
            ? value
            : value[..MaximumCapturedOutputBytes];
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process has already exited; no cleanup remains.
        }
    }
}
