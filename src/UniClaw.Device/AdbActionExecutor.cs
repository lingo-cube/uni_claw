using System.Diagnostics;
using System.Text.RegularExpressions;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

/// <summary>
/// AdbActionExecutor — IActionExecutor 实现，通过 ADB shell input 命令执行设备操作。
/// 坐标归一化 0-1 → 像素转换（自动获取设备屏幕尺寸）。
/// </summary>
public sealed partial class AdbActionExecutor : IActionExecutor
{
    private readonly string _adbPath;
    private ScreenDimensions? _dimensions;

    /// <summary>操作历史记录</summary>
    public List<ActionRecord> History { get; } = new();

    public AdbActionExecutor(string adbPath = "adb")
    {
        if (string.IsNullOrWhiteSpace(adbPath))
            throw new ArgumentException("adb path cannot be empty", nameof(adbPath));
        _adbPath = adbPath;
    }

    /// <inheritdoc />
    public async Task<bool> TapAsync(double x, double y, CancellationToken ct = default)
    {
        var (px, py) = await NormalizeAsync(x, y, ct);
        var exitCode = await RunShellAsync($"input tap {px} {py}", ct);
        var success = exitCode == 0;
        History.Add(new ActionRecord("tap", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["px"] = px, ["py"] = py }, success));
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int durationMs, CancellationToken ct = default)
    {
        var (spx, spy) = await NormalizeAsync(sx, sy, ct);
        var (epx, epy) = await NormalizeAsync(ex, ey, ct);
        var exitCode = await RunShellAsync($"input swipe {spx} {spy} {epx} {epy} {durationMs}", ct);
        var success = exitCode == 0;
        History.Add(new ActionRecord("swipe", DateTimeOffset.UtcNow,
            new() { ["sx"] = sx, ["sy"] = sy, ["ex"] = ex, ["ey"] = ey, ["durationMs"] = durationMs }, success));
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> PressBackAsync(CancellationToken ct = default)
    {
        var exitCode = await RunShellAsync("input keyevent KEYCODE_BACK", ct);
        var success = exitCode == 0;
        History.Add(new ActionRecord("back", DateTimeOffset.UtcNow, new(), success));
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> InputTextAsync(string text, CancellationToken ct = default)
    {
        // ADB shell input text 需要转义空格和特殊字符
        var escaped = EscapeText(text);
        var exitCode = await RunShellAsync($"input text {escaped}", ct);
        var success = exitCode == 0;
        History.Add(new ActionRecord("input_text", DateTimeOffset.UtcNow,
            new() { ["text"] = text }, success));
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken ct = default)
    {
        var (px, py) = await NormalizeAsync(x, y, ct);
        // ADB 没有独立的长按命令，用 swipe 从同坐标到同坐标模拟
        var exitCode = await RunShellAsync($"input swipe {px} {py} {px} {py} {durationMs}", ct);
        var success = exitCode == 0;
        History.Add(new ActionRecord("long_press", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["px"] = px, ["py"] = py, ["durationMs"] = durationMs }, success));
        return success;
    }

    /// <inheritdoc />
    public Task WaitAsync(int ms, CancellationToken ct = default) => Task.Delay(ms, ct);

    /// <inheritdoc />
    public List<ActionRecord> GetHistory() => History;

    // ── 内部 ────────────────────────────────────────────────────────

    /// <summary>归一化坐标 → 像素坐标（延迟获取屏幕尺寸，缓存）</summary>
    private async Task<(int Px, int Py)> NormalizeAsync(double nx, double ny, CancellationToken ct)
    {
        var dims = await GetScreenDimensionsAsync(ct);
        return (
            (int)(nx * dims.Width),
            (int)(ny * dims.Height)
        );
    }

    /// <summary>获取设备屏幕尺寸（首次运行 ADB 查询，缓存后续复用）</summary>
    private async Task<ScreenDimensions> GetScreenDimensionsAsync(CancellationToken ct)
    {
        if (_dimensions is not null)
            return _dimensions;

        var output = await RunShellWithOutputAsync("wm size", ct);
        var match = ScreenSizeRegex().Match(output);
        if (!match.Success)
            throw new InvalidOperationException(
                $"Could not parse screen dimensions from ADB output: {output}");

        _dimensions = new ScreenDimensions(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value));
        return _dimensions;
    }

    /// <summary>运行 ADB shell 命令，返回 exit code</summary>
    private async Task<int> RunShellAsync(string command, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = $"shell {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    /// <summary>运行 ADB shell 命令，返回 stdout 文本</summary>
    private async Task<string> RunShellWithOutputAsync(string command, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = $"shell {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output;
    }

    /// <summary>转义 input text 特殊字符 — 空格和引号用 %s 替代（ADB shell 兼容）</summary>
    private static string EscapeText(string text)
    {
        // ADB shell input text 用 %s 代表空格，其他特殊字符同上
        return text
            .Replace("'", "'\"'\"'")   // 单引号逃逸
            .Replace(" ", "%s");
    }

    [GeneratedRegex(@"Physical size:\s*(\d+)x(\d+)")]
    private static partial Regex ScreenSizeRegex();

    private sealed record ScreenDimensions(int Width, int Height);
}
