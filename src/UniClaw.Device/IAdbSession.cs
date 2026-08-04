namespace UniClaw.Device;

public interface IAdbSession : IAsyncDisposable
{
    string Serial { get; }

    /// <summary>捕获当前屏幕截图，返回 PNG 字节流。</summary>
    Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default);

    /// <summary>执行 shell 命令，返回结构化结果。</summary>
    Task<ShellResult> ExecuteShellAsync(
        string command,
        CancellationToken ct = default);

    /// <summary>
    /// 拉取当前 UI 层级 XML。
    /// 内部合并 uiautomator dump + cat 为一次调用，调用方不关心文件路径。
    /// </summary>
    Task<string> DumpUiHierarchyAsync(CancellationToken ct = default);
}
