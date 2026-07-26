using System.Diagnostics;
using UniClaw.Core.UniBrain;

namespace UniClaw.Device;

/// <summary>
/// AdbScreenCapture — ADB-based screenshot capture implementing IScreenCapture.
/// Runs "adb exec-out screencap -p" and returns the raw PNG bytes.
/// </summary>
public sealed class AdbScreenCapture : IScreenCapture
{
    private readonly string _adbPath;

    /// <summary>
    /// 构造 AdbScreenCapture。
    /// </summary>
    /// <param name="adbPath">adb 可执行文件路径，默认 "adb"（需在 PATH 中）</param>
    public AdbScreenCapture(string adbPath = "adb")
    {
        if (string.IsNullOrWhiteSpace(adbPath))
            throw new ArgumentException("adb path cannot be empty", nameof(adbPath));
        _adbPath = adbPath;
    }

    /// <inheritdoc />
    public async Task<byte[]> CaptureAsync(CancellationToken ct = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = "exec-out screencap -p",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        // 读取 stdout（PNG 字节流）和 stderr（错误信息）并行
        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stderr = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ADB screencap failed (exit {process.ExitCode}): {stderr.Trim()}");

        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            throw new InvalidOperationException("ADB screencap returned 0 bytes.");

        return bytes;
    }
}
