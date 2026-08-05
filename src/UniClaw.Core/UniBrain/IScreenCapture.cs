namespace UniClaw.Core.UniBrain;

/// <summary>
/// IScreenCapture — 屏幕截图捕获抽象（Core 设备 I/O 接缝）。
/// §12-B 截图归属：截图捕获组合进 provider 侧，IPageAnalyzer.AnalyzeCurrentPageAsync 签名零改动。
/// Core 只持有抽象；真机实现（AdbScreenCapture）属 host，不进 Core。
/// 放 UniBrain namespace：唯一 Core 消费者为 PageAnalyzer (UniBrain)，UniBrain 自持其视觉输入接缝，
/// 不依赖 Traversal namespace（D-130 Locked: UniBrain 不引用 StateMachine/Traversal）。
/// </summary>
public interface IScreenCapture
{
    /// <summary>捕获当前屏幕，返回 PNG/JPEG 字节流。</summary>
    Task<byte[]> CaptureAsync(CancellationToken ct = default);

    /// <summary>
    /// Captures raw RGBA screen buffer (zero encode on device).
    /// Throw <see cref="NotSupportedException"/> if raw capture is unavailable;
    /// <see cref="PageAnalyzer"/> falls back to <see cref="CaptureAsync"/>.
    /// </summary>
    Task<RawScreenBuffer> CaptureRawAsync(CancellationToken ct = default);
}