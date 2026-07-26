using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// FileScreenCapture — IScreenCapture 实现，从文件读取截图。
/// 用于集成测试（预存截图 fixture 代替真机 ADB）。
/// </summary>
internal sealed class FileScreenCapture : IScreenCapture
{
    private readonly string _filePath;

    /// <param name="filePath">PNG 截图的文件路径</param>
    public FileScreenCapture(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public async Task<byte[]> CaptureAsync(CancellationToken ct = default)
    {
        return await File.ReadAllBytesAsync(_filePath, ct);
    }
}
