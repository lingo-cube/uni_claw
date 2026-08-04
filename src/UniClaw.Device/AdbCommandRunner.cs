using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace UniClaw.Device;

public sealed record class AdbCommandRunnerOptions(
    string Serial,
    string AdbPath,
    TimeSpan DefaultTimeout)
{
    public AdbCommandRunnerOptions(string serial)
        : this(serial, "adb", TimeSpan.FromSeconds(30))
    {
    }
}

public sealed record class AdbCommandRequest(
    ImmutableArray<string> Arguments,
    TimeSpan? Timeout = null,
    bool CaptureBinaryOutput = false,
    ImmutableHashSet<int>? SensitiveArgumentIndexes = null)
{
    public static AdbCommandRequest Create(
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        bool captureBinaryOutput = false,
        IEnumerable<int>? sensitiveArgumentIndexes = null) =>
        new(
            arguments.ToImmutableArray(),
            timeout,
            captureBinaryOutput,
            sensitiveArgumentIndexes?.ToImmutableHashSet());
}

public sealed record class AdbCommandFailure(
    string Kind,
    string Message,
    string? ExceptionType = null);

public sealed record class AdbCommandResult(
    string Serial,
    ImmutableArray<string> Arguments,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    ImmutableArray<byte> BinaryOutput,
    TimeSpan Duration,
    AdbCommandFailure? Failure)
{
    public bool Succeeded => Failure is null && ExitCode == 0;
}

public sealed class AdbCommandException : Exception
{
    public ShellResult Result { get; }

    public AdbCommandException(string operation, ShellResult result)
        : base(BuildMessage(operation, result))
    {
        Result = result;
    }

    private static string BuildMessage(string operation, ShellResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? "no error output"
            : result.StandardError.Trim();
        return $"{operation} failed: {detail}";
    }
}

[Obsolete("Use IAdbSession via ProcessAdbSession or AdvancedSharpAdbSession instead.")]
public sealed class AdbCommandRunner
{
    private readonly AdbCommandRunnerOptions _options;

    public string Serial => _options.Serial;

    public AdbCommandRunner(AdbCommandRunnerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Serial))
            throw new ArgumentException("ADB serial is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.AdbPath))
            throw new ArgumentException("ADB path is required.", nameof(options));
        if (options.DefaultTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "ADB timeout must be positive.");
        _options = options with
        {
            Serial = options.Serial.Trim(),
            AdbPath = options.AdbPath.Trim(),
        };
    }

    public async Task<AdbCommandResult> RunAsync(
        AdbCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Arguments.IsDefaultOrEmpty)
            throw new ArgumentException("At least one ADB argument is required.", nameof(request));
        if (request.Timeout is { } timeout && timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(request), "ADB timeout must be positive.");

        var redactedArguments = RedactArguments(request);
        var stopwatch = Stopwatch.StartNew();
        using var process = CreateProcess(request);

        try
        {
            if (!process.Start())
            {
                return Failed(
                    redactedArguments,
                    stopwatch.Elapsed,
                    "start_failure",
                    "ADB process did not start");
            }
        }
        catch (Exception ex)
        {
            return Failed(
                redactedArguments,
                stopwatch.Elapsed,
                "start_failure",
                "ADB process could not be started",
                ex);
        }

        using var timeoutCts = new CancellationTokenSource(
            request.Timeout ?? _options.DefaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        Task<string>? stdoutTextTask = null;
        MemoryStream? stdoutBytes = null;
        Task? stdoutBinaryTask = null;
        if (request.CaptureBinaryOutput)
        {
            stdoutBytes = new MemoryStream();
            stdoutBinaryTask = process.StandardOutput.BaseStream.CopyToAsync(
                stdoutBytes,
                CancellationToken.None);
        }
        else
        {
            stdoutTextTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        }

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            if (stdoutBinaryTask is not null)
                await stdoutBinaryTask;
            var stdout = stdoutTextTask is null ? string.Empty : await stdoutTextTask;
            var stderr = await stderrTask;
            stopwatch.Stop();

            var failure = process.ExitCode == 0
                ? null
                : new AdbCommandFailure(
                    "non_zero_exit",
                    $"ADB exited with code {process.ExitCode}");
            return new AdbCommandResult(
                Serial,
                redactedArguments,
                process.ExitCode,
                stdout,
                stderr,
                stdoutBytes?.ToArray().ToImmutableArray() ?? ImmutableArray<byte>.Empty,
                stopwatch.Elapsed,
                failure);
        }
        catch (OperationCanceledException ex)
        {
            TryKill(process);
            await DrainAsync(stdoutTextTask, stdoutBinaryTask, stderrTask);
            stopwatch.Stop();
            var callerCancelled = cancellationToken.IsCancellationRequested;
            return Failed(
                redactedArguments,
                stopwatch.Elapsed,
                callerCancelled ? "cancelled" : "timeout",
                callerCancelled
                    ? "ADB command was cancelled"
                    : $"ADB command timed out after {request.Timeout ?? _options.DefaultTimeout}",
                ex);
        }
        finally
        {
            stdoutBytes?.Dispose();
        }
    }

    private Process CreateProcess(AdbCommandRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.AdbPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = request.CaptureBinaryOutput ? null : Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-s");
        startInfo.ArgumentList.Add(Serial);
        foreach (var argument in request.Arguments)
        {
            if (argument is null)
                throw new ArgumentException("ADB arguments cannot contain null.", nameof(request));
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }

    private static ImmutableArray<string> RedactArguments(AdbCommandRequest request)
    {
        var sensitive = request.SensitiveArgumentIndexes
                        ?? ImmutableHashSet<int>.Empty;
        return request.Arguments
            .Select((argument, index) => sensitive.Contains(index) ? "[REDACTED]" : argument)
            .ToImmutableArray();
    }

    private AdbCommandResult Failed(
        ImmutableArray<string> arguments,
        TimeSpan duration,
        string kind,
        string message,
        Exception? exception = null) =>
        new(
            Serial,
            arguments,
            null,
            string.Empty,
            string.Empty,
            ImmutableArray<byte>.Empty,
            duration,
            new AdbCommandFailure(kind, message, exception?.GetType().Name));

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task DrainAsync(
        Task<string>? stdoutText,
        Task? stdoutBinary,
        Task<string> stderr)
    {
        try
        {
            if (stdoutText is not null)
                await stdoutText;
            if (stdoutBinary is not null)
                await stdoutBinary;
            await stderr;
        }
        catch
        {
        }
    }
}
