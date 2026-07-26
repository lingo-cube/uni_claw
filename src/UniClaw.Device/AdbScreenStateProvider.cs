using System.Diagnostics;
using System.Xml.Linq;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

/// <summary>
/// AdbScreenStateProvider — IScreenStateProvider 实现，通过 ADB uiautomator dump 获取设备滚动状态。
/// 解析 XML 布局查找可滚动视图 (ScrollView, ListView, RecyclerView)，
/// 提取 scrollY/maxScrollY 计算滚动进度。
/// </summary>
public sealed class AdbScreenStateProvider : IScreenStateProvider
{
    private readonly string _adbPath;
    private const string RemotePath = "/sdcard/ui_dump.xml";

    public AdbScreenStateProvider(string adbPath = "adb")
    {
        if (string.IsNullOrWhiteSpace(adbPath))
            throw new ArgumentException("adb path cannot be empty", nameof(adbPath));
        _adbPath = adbPath;
    }

    /// <inheritdoc />
    public bool HasScroll() => GetScrollState().HasScroll;

    /// <inheritdoc />
    public double GetScrollProgress() => GetScrollState().Progress;

    /// <inheritdoc />
    public bool IsEndOfList() => GetScrollState().IsEnd;

    /// <inheritdoc />
    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;

    // ── 内部 ────────────────────────────────────────────────────────

    private ScrollState GetScrollState()
    {
        try
        {
            return DumpAndParseAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return new ScrollState(false, 0.0, true);
        }
    }

    private async Task<ScrollState> DumpAndParseAsync()
    {
        // 1. uiautomator dump 到设备文件
        var dumpExit = await RunShellAsync($"uiautomator dump {RemotePath}");
        if (dumpExit != 0)
            return new ScrollState(false, 0.0, true);

        // 2. pull 到临时文件
        var tmpFile = Path.GetTempFileName();
        try
        {
            var pullExit = await RunAsync($"pull {RemotePath} \"{tmpFile}\"");
            if (pullExit != 0)
                return new ScrollState(false, 0.0, true);

            // 3. 解析 XML
            var doc = XDocument.Load(tmpFile);
            var root = doc.Root;
            if (root is null)
                return new ScrollState(false, 0.0, true);

            // 查找可滚动节点
            var scrollable = root.Descendants()
                .FirstOrDefault(e => (string?)e.Attribute("scrollable") == "true");
            if (scrollable is null)
                return new ScrollState(false, 0.0, true);

            var scrollY = (int?)scrollable.Attribute("scrollY") ?? 0;
            var maxScrollY = (int?)scrollable.Attribute("scrollYMax") ?? 0;

            if (maxScrollY <= 0)
                return new ScrollState(true, 0.0, true);

            var progress = Math.Clamp((double)scrollY / maxScrollY, 0.0, 1.0);
            var isEnd = scrollY >= maxScrollY;

            return new ScrollState(true, progress, isEnd);
        }
        finally
        {
            // 4. 清理
            try { File.Delete(tmpFile); } catch { }
            try { await RunShellAsync($"rm {RemotePath}"); } catch { }
        }
    }

    private async Task<int> RunShellAsync(string command)
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
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private async Task<int> RunAsync(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private sealed record ScrollState(bool HasScroll, double Progress, bool IsEnd);
}
