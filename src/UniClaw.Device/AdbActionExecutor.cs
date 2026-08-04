using System.Globalization;
using System.Text.RegularExpressions;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

public sealed partial class AdbActionExecutor : IActionExecutor
{
    private readonly IAdbSession _session;
    private ScreenDimensions? _dimensions;

    public List<ActionRecord> History { get; } = new();

    public AdbActionExecutor(
        IAdbSession session,
        TimeSpan? timeout = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (timeout is TimeSpan t && t <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public AdbActionExecutor(
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

    public async Task<bool> TapAsync(
        double x,
        double y,
        CancellationToken ct = default)
    {
        var (px, py) = await NormalizeAsync(x, y, ct);
        var success = await RunShellAsync(
            $"input mouse -d 0 tap {px} {py}",
            ct);
        History.Add(new ActionRecord(
            "tap",
            DateTimeOffset.UtcNow,
            new()
            {
                ["x"] = x,
                ["y"] = y,
                ["px"] = px,
                ["py"] = py,
                ["command"] = "input mouse -d 0 tap",
            },
            success));
        return success;
    }

    public async Task<bool> SwipeAsync(
        double sx,
        double sy,
        double ex,
        double ey,
        int durationMs,
        CancellationToken ct = default)
    {
        if (durationMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMs));
        var (spx, spy) = await NormalizeAsync(sx, sy, ct);
        var (epx, epy) = await NormalizeAsync(ex, ey, ct);
        var success = await RunShellAsync(
            $"input swipe {spx} {spy} {epx} {epy} {durationMs}",
            ct);
        History.Add(new ActionRecord(
            "swipe",
            DateTimeOffset.UtcNow,
            new()
            {
                ["sx"] = sx,
                ["sy"] = sy,
                ["ex"] = ex,
                ["ey"] = ey,
                ["durationMs"] = durationMs,
            },
            success));
        return success;
    }

    public async Task<bool> PressBackAsync(CancellationToken ct = default)
    {
        var success = await RunShellAsync("input keyevent KEYCODE_BACK", ct);
        History.Add(new ActionRecord("back", DateTimeOffset.UtcNow, new(), success));
        return success;
    }

    public async Task<bool> InputTextAsync(
        string text,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = await _session.ExecuteShellAsync(
            $"input text {text.Replace(" ", "%s", StringComparison.Ordinal)}",
            ct);
        var success = result.Success;
        History.Add(new ActionRecord(
            "input_text",
            DateTimeOffset.UtcNow,
            new() { ["textLength"] = text.Length },
            success));
        return success;
    }

    public async Task<bool> LongPressAsync(
        double x,
        double y,
        int durationMs,
        CancellationToken ct = default)
    {
        if (durationMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMs));
        var (px, py) = await NormalizeAsync(x, y, ct);
        var success = await RunShellAsync(
            $"input swipe {px} {py} {px} {py} {durationMs}",
            ct);
        History.Add(new ActionRecord(
            "long_press",
            DateTimeOffset.UtcNow,
            new()
            {
                ["x"] = x,
                ["y"] = y,
                ["px"] = px,
                ["py"] = py,
                ["durationMs"] = durationMs,
            },
            success));
        return success;
    }

    public async Task<bool> LaunchPackageAsync(
        string packageName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(packageName)
            || !PackageNameRegex().IsMatch(packageName))
        {
            throw new ArgumentException(
                "Android package name contains unsupported characters.",
                nameof(packageName));
        }

        var success = await RunShellAsync(
            $"monkey -p {packageName} -c android.intent.category.LAUNCHER 1",
            ct);
        History.Add(new ActionRecord(
            "launch",
            DateTimeOffset.UtcNow,
            new() { ["package"] = packageName },
            success));
        return success;
    }

    public Task WaitAsync(int ms, CancellationToken ct = default)
    {
        if (ms < 0)
            throw new ArgumentOutOfRangeException(nameof(ms));
        return Task.Delay(ms, ct);
    }

    public List<ActionRecord> GetHistory() => [.. History];

    private async Task<(int Px, int Py)> NormalizeAsync(
        double nx,
        double ny,
        CancellationToken ct)
    {
        if (!double.IsFinite(nx) || nx is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(nx));
        if (!double.IsFinite(ny) || ny is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ny));
        var dimensions = await GetScreenDimensionsAsync(ct);
        return (
            (int)Math.Round(nx * dimensions.Width),
            (int)Math.Round(ny * dimensions.Height));
    }

    private async Task<ScreenDimensions> GetScreenDimensionsAsync(
        CancellationToken ct)
    {
        if (_dimensions is not null)
            return _dimensions;

        var result = await _session.ExecuteShellAsync("wm size", ct);
        if (!result.Success)
            throw new AdbCommandException("screen-size query", result);

        var match = ScreenSizeRegex().Match(result.StandardOutput);
        if (!match.Success)
        {
            throw new AdbCommandException(
                "screen-size query",
                new ShellResult(
                    false,
                    result.StandardOutput,
                    "Could not parse physical screen dimensions"));
        }

        _dimensions = new ScreenDimensions(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        return _dimensions;
    }

    private async Task<bool> RunShellAsync(
        string command,
        CancellationToken ct)
    {
        var result = await _session.ExecuteShellAsync(command, ct);
        return result.Success;
    }

    [GeneratedRegex(@"Physical size:\s*(\d+)x(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex ScreenSizeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageNameRegex();

    private sealed record class ScreenDimensions(int Width, int Height);
}
