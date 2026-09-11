using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UniClaw.Kernel.Tests")]

namespace UniClaw.Kernel.Effects;

/// <summary>
/// ADB 进程协议层（ADB-001；legacy uni-agent AdbProcessRunner 平移——DIRECT
/// IDEA，真机实证语义）。只做有界进程执行，零语义解释、零 retry authority。
/// timeout 语义（审计 F1 物理事实）：超时 kill 的是 adb **client** 进程树；
/// adb server 可能已完成向设备转发——设备侧效果未知。该事实由
/// AdbLiveEffectDriver 映射为 UnknownOutcome（唯一合法后继 re-observe）。
/// </summary>
internal interface IAdbProcessRunner
{
    Task<AdbProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>二进制 stdout 捕获变体（PER-005 观察侧：screencap PNG 载荷）。
    /// 语义与 RunAsync 完全一致（有界 / 超时 kill / 三通道），仅 stdout 以
    /// 字节返回而非丢弃。</summary>
    Task<AdbCaptureResult> RunCaptureAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>进程结果三通道：启动 / 超时 / 退出码 + stdio（diagnostic 透传用）。</summary>
internal sealed record AdbProcessResult(
    bool Started,
    bool TimedOut,
    int? ExitCode,
    string StandardError,
    string? FailureReason);

/// <summary>RunCaptureAsync 结果：stdout 为二进制载荷（同一 64 MiB bound）。</summary>
internal sealed record AdbCaptureResult(
    bool Started,
    bool TimedOut,
    int? ExitCode,
    byte[] StandardOutput,
    string StandardError,
    string? FailureReason);

internal sealed class AdbProcessRunner : IAdbProcessRunner
{
    // 64 MiB：截图是唯一预期二进制 stdout 消费者（未来观察侧复用）；
    // bound 防 malformed/hostile 输出。
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
                return new(false, false, null, string.Empty, "ADB process did not start.");
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new(false, false, null, string.Empty, exception.Message);
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        _ = ReadBoundedAsync(process.StandardOutput.BaseStream);
        var stderrTask = ReadBoundedTextAsync(process.StandardError);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await stderrTask;
            // TimedOut=true：client 被 kill，设备侧效果未知（→ UnknownOutcome）
            return new(true, true, null, await stderrTask, "ADB process timed out.");
        }
        catch
        {
            TryKill(process);
            await stderrTask;
            throw;
        }

        var stderr = await stderrTask;
        return new(true, false, process.ExitCode, stderr, null);
    }

    public async Task<AdbCaptureResult> RunCaptureAsync(
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
                return new(false, false, null, Array.Empty<byte>(), string.Empty, "ADB process did not start.");
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new(false, false, null, Array.Empty<byte>(), string.Empty, exception.Message);
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        var stdoutTask = ReadBoundedAsync(process.StandardOutput.BaseStream);
        var stderrTask = ReadBoundedTextAsync(process.StandardError);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await stdoutTask.ConfigureAwait(false);
            var stderrOnTimeout = await stderrTask;
            return new(true, true, null, Array.Empty<byte>(), stderrOnTimeout, "ADB process timed out.");
        }
        catch
        {
            TryKill(process);
            try { await stdoutTask.ConfigureAwait(false); } catch (InvalidOperationException) { /* bound 溢出同 RunAsync 丢弃语义 */ }
            await stderrTask;
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new(true, false, process.ExitCode, stdout, stderr, null);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            if (output.Length + read > MaximumCapturedOutputBytes)
                throw new InvalidOperationException("ADB process stdout exceeded the capture bound.");
            await output.WriteAsync(buffer.AsMemory(0, read));
        }
        return output.ToArray();
    }

    private static async Task<string> ReadBoundedTextAsync(StreamReader reader)
    {
        var value = await reader.ReadToEndAsync();
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
            // 进程已退出，无清理可做。
        }
    }
}
