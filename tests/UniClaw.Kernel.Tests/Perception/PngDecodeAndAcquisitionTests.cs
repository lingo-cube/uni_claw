using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-005 ①（DETERMINISTIC）——PNG 解码（corpus 真机帧）+ AdbScreenshot
/// Acquisition 的三通道失败面（超时 / 非 0 退出 / 空输出 / 坏图）与
/// artifact 组装（内容寻址 id / 注入 clock / 帧元数据）。真实 adb 路径由
/// PerceptionLiveEnvironmentTests（A5）覆盖。
/// </summary>
public sealed class PngDecodeAndAcquisitionTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");

    private static byte[] CorpusPng() =>
        File.ReadAllBytes(Path.Combine(CorpusRoot, "legacy-direct", "golden-run-v1", "case-a-before.png"));

    // ---- PNG 解码 -----------------------------------------------------------

    [Fact]
    public void Decode_CorpusRealDevicePng_1080x1920Rgba()
    {
        var image = PngImage.Decode(CorpusPng());
        Assert.Equal(1080, image.Width);
        Assert.Equal(1920, image.Height);
        Assert.Equal(1080 * 1920 * 4, image.Rgba.Length);
    }

    [Fact]
    public void Decode_MalformedInput_FailClosedThrows()
    {
        Assert.Throws<InvalidOperationException>(() => PngImage.Decode(new byte[64]));
        Assert.Throws<InvalidOperationException>(() => PngImage.Decode("not a png at all"u8.ToArray()));
        var truncated = CorpusPng();
        Assert.Throws<InvalidOperationException>(() => PngImage.Decode(truncated[..1000]));
    }

    [Fact]
    public void Decode_UnsupportedColorType_FailClosedThrows()
    {
        // 伪造 colorType=3（palette）IHDR —— 不支持域必须拒绝而非猜测
        var png = CorpusPng().ToArray();
        png[25] = 3;
        Assert.Throws<InvalidOperationException>(() => PngImage.Decode(png));
    }

    // ---- Acquisition（进程 double） -----------------------------------------

    private sealed class ScriptedRunner(Func<IReadOnlyList<string>, AdbCaptureResult> behavior)
        : IAdbProcessRunner
    {
        public Task<AdbProcessResult> RunAsync(
            string executable, IReadOnlyList<string> arguments,
            TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(new AdbProcessResult(true, false, 0, string.Empty, null));

        public Task<AdbCaptureResult> RunCaptureAsync(
            string executable, IReadOnlyList<string> arguments,
            TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(behavior(arguments));
    }

    private static AdbScreenshotAcquisition AcquisitionWith(
        Func<IReadOnlyList<string>, AdbCaptureResult> behavior) =>
        new("emulator-5554", "adb", clock: () => FixedTime, runner: new ScriptedRunner(behavior));

    [Fact]
    public async Task Capture_ValidPng_ProducesContentAddressedArtifact()
    {
        var png = CorpusPng();
        var acquisition = AcquisitionWith(_ =>
            new AdbCaptureResult(true, false, 0, png, string.Empty, null));

        var capture = await acquisition.CaptureAsync(CancellationToken.None);

        Assert.Equal(1080, capture.Width);
        Assert.Equal(1920, capture.Height);
        Assert.Equal(png, capture.Artifact.Payload);
        Assert.Equal(FixedTime, capture.Artifact.Metadata.CaptureTime);
        Assert.Equal("artifact", capture.Artifact.Metadata.Frame);
        Assert.Equal(1080, capture.Artifact.Metadata.Width);
        // 内容寻址确定性：同 PNG → 同 ArtifactId
        var again = await acquisition.CaptureAsync(CancellationToken.None);
        Assert.Equal(capture.Artifact.ArtifactId, again.Artifact.ArtifactId);
        Assert.StartsWith("art-", capture.Artifact.ArtifactId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Capture_SendsScreencapCommandWithSerial()
    {
        IReadOnlyList<string>? seen = null;
        var acquisition = AcquisitionWith(args =>
        {
            seen = args;
            return new AdbCaptureResult(true, false, 0, CorpusPng(), string.Empty, null);
        });
        await acquisition.CaptureAsync(CancellationToken.None);
        Assert.Equal(["-s", "emulator-5554", "exec-out", "screencap", "-p"], seen);
    }

    [Fact]
    public async Task Capture_Timeout_ThrowsTimeoutException()
    {
        var acquisition = AcquisitionWith(_ =>
            new AdbCaptureResult(true, true, null, Array.Empty<byte>(), "timed out", "ADB process timed out."));
        await Assert.ThrowsAsync<TimeoutException>(() => acquisition.CaptureAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Capture_NonZeroExit_ThrowsWithStderr()
    {
        var acquisition = AcquisitionWith(_ =>
            new AdbCaptureResult(true, false, 1, Array.Empty<byte>(), "device not found", null));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => acquisition.CaptureAsync(CancellationToken.None));
        Assert.Contains("device not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Capture_EmptyOutput_ThrowsInvalidOperation()
    {
        var acquisition = AcquisitionWith(_ =>
            new AdbCaptureResult(true, false, 0, Array.Empty<byte>(), string.Empty, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => acquisition.CaptureAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Capture_GarbageBytes_ThrowsInvalidImage()
    {
        var acquisition = AcquisitionWith(_ =>
            new AdbCaptureResult(true, false, 0, "garbage-not-png"u8.ToArray(), string.Empty, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => acquisition.CaptureAsync(CancellationToken.None));
    }

    [Fact]
    public void Ctor_RequiresSerialAndExecutable_FailClosed()
    {
        Assert.Throws<ArgumentException>(() => new AdbScreenshotAcquisition(" "));
        Assert.Throws<ArgumentException>(() => new AdbScreenshotAcquisition("serial", " "));
    }
}
