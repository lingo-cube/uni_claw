using UniClaw.Core.UniBrain;

namespace UniClaw.Device;

public interface IAdbSession : IAsyncDisposable
{
    string Serial { get; }

    /// <summary>捕获当前屏幕截图，返回 PNG 字节流。</summary>
    Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Captures raw framebuffer via adb exec-out screencap (no -p).
    /// Returns parsed header dimensions + raw RGBA pixel payload.
    /// </summary>
    Task<RawScreenBuffer> CaptureRawScreenBufferAsync(CancellationToken ct = default);

    /// <summary>执行 shell 命令，返回结构化结果。</summary>
    Task<ShellResult> ExecuteShellAsync(
        string command,
        CancellationToken ct = default);
}
