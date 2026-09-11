using UniClaw.Kernel.Effects;

namespace UniClaw.Kernel.Perception;

/// <summary>采集结果：PNG RawArtifact（capture，内容寻址 id）+ 解码尺寸。</summary>
public sealed record CapturedScreenshot(RawArtifact Artifact, int Width, int Height);

/// <summary>
/// AdbScreenshotAcquisition — 感知 acquisition 的设备截屏 provider
/// （PER-005 ①；ADAPT legacy uni-agent AdbScreenshotSource，DIRECT 语义：
/// 新鲜设备截屏，不解释像素）。产出 PNG RawArtifact（P2 producer 侧的
/// raw perception 输入）；CaptureTime 由注入 clock 显式提供（host clock，
/// 只作 temporal provenance——ADR-0010：不是 freshness 权威）。异常面与
/// legacy 对齐：超时 TimeoutException；启动失败 / 非 0 退出 / 空输出 /
/// 坏图 InvalidOperationException——不重试、不降级，失败就是失败。
/// </summary>
public sealed class AdbScreenshotAcquisition
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    private readonly IAdbProcessRunner _runner;
    private readonly string _adbExecutable;
    private readonly string _serial;
    private readonly Func<DateTimeOffset> _clock;

    public AdbScreenshotAcquisition(
        string serial,
        string adbExecutable = "adb",
        Func<DateTimeOffset>? clock = null)
        : this(serial, adbExecutable, clock, runner: null)
    {
    }

    internal AdbScreenshotAcquisition(
        string serial,
        string adbExecutable,
        Func<DateTimeOffset>? clock,
        IAdbProcessRunner? runner)
    {
        _serial = string.IsNullOrWhiteSpace(serial)
            ? throw new ArgumentException("Resolved device serial is required.", nameof(serial))
            : serial;
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable)
            ? throw new ArgumentException("ADB executable is required.", nameof(adbExecutable))
            : adbExecutable;
        _clock = clock ?? (static () => DateTimeOffset.UtcNow);
        _runner = runner ?? new AdbProcessRunner();
    }

    public async Task<CapturedScreenshot> CaptureAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunCaptureAsync(
            _adbExecutable,
            ["-s", _serial, "exec-out", "screencap", "-p"],
            CaptureTimeout,
            cancellationToken);
        if (result.TimedOut)
            throw new TimeoutException("ADB screenshot capture timed out.");
        if (!result.Started || result.ExitCode != 0)
            throw new InvalidOperationException(
                "ADB screenshot capture failed: " + (result.FailureReason ?? result.StandardError));
        var png = result.StandardOutput;
        if (png.Length == 0)
            throw new InvalidOperationException("ADB screenshot capture returned empty output.");

        PngImage image;
        try
        {
            image = PngImage.Decode(png);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException("ADB screenshot capture did not return a valid image.", exception);
        }

        var artifact = RawArtifact.Capture(
            png,
            new ArtifactMetadata(image.Width, image.Height, Frame: "artifact", CaptureTime: _clock()));
        return new CapturedScreenshot(artifact, image.Width, image.Height);
    }
}
