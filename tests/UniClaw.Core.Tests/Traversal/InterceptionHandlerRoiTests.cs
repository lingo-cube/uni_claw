using System.Collections.Immutable;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// BuildYoloBboxes 反变换测试 — PageAnalysis.YoloBboxes (C# 发送图空间,
/// 与 items 坐标同源) → 全屏截图空间 RoiRect。变换参数与
/// PageAnalyzer.ImageResizer 调用同源: env 覆盖 / 默认值
/// (UNICLAW_IMAGE_MAX_WIDTH=720, UNICLAW_IMAGE_CROP_TOP=0.0625)。
/// </summary>
[CollectionDefinition(nameof(EnvSensitiveTestsCollection), DisableParallelization = true)]
public sealed class EnvSensitiveTestsCollection;

[Collection(nameof(EnvSensitiveTestsCollection))]
public class InterceptionHandlerRoiTests : IDisposable
{
    private const string MaxWidthVar = "UNICLAW_IMAGE_MAX_WIDTH";
    private const string CropTopVar = "UNICLAW_IMAGE_CROP_TOP";

    private readonly string? _savedMaxWidth =
        Environment.GetEnvironmentVariable(MaxWidthVar);
    private readonly string? _savedCropTop =
        Environment.GetEnvironmentVariable(CropTopVar);

    public void Dispose()
    {
        Restore(MaxWidthVar, _savedMaxWidth);
        Restore(CropTopVar, _savedCropTop);
    }

    private static void Restore(string name, string? value)
        => Environment.SetEnvironmentVariable(name, value);

    [Fact(DisplayName = "默认参数: 1080×2400 全屏, 720×1400 发送图 → sx=1.5, cropTopPx=150")]
    public void BuildYoloBboxes_DefaultParams_MapsToFullscreen()
    {
        Environment.SetEnvironmentVariable(MaxWidthVar, null);
        Environment.SetEnvironmentVariable(CropTopVar, null);

        var result = InterceptionHandler.BuildYoloBboxes(
            ImmutableArray.Create(240, 300, 480, 420), 1080, 2400);

        Assert.Equal([new RoiRect(360, 600, 720, 780)], result);
    }

    [Fact(DisplayName = "宽 ≤ maxWidth: sx=1, 仅加 cropTopPx 偏移")]
    public void BuildYoloBboxes_ScreenBelowMaxWidth_OnlyCropOffset()
    {
        Environment.SetEnvironmentVariable(MaxWidthVar, "720");
        Environment.SetEnvironmentVariable(CropTopVar, null);

        // 400×800 屏幕 → 发送图 400×700; cropTopPx = round(0.0625×800) = 50
        var result = InterceptionHandler.BuildYoloBboxes(
            ImmutableArray.Create(10, 20, 30, 40), 400, 800);

        Assert.Equal([new RoiRect(10, 70, 30, 90)], result);
    }

    [Fact(DisplayName = "env 覆盖: MAX_WIDTH=1080 (不缩放) + CROP_TOP=0.1 → sx=1, cropTopPx=240")]
    public void BuildYoloBboxes_EnvOverrides_Apply()
    {
        Environment.SetEnvironmentVariable(MaxWidthVar, "1080");
        Environment.SetEnvironmentVariable(CropTopVar, "0.1");

        var result = InterceptionHandler.BuildYoloBboxes(
            ImmutableArray.Create(240, 300, 480, 420), 1080, 2400);

        Assert.Equal([new RoiRect(240, 540, 480, 660)], result);
    }

    [Fact(DisplayName = "空/非法输入 → 空列表 (AI provider 无检测数据 → 密度退化)")]
    public void BuildYoloBboxes_EmptyOrMalformed_ReturnsEmpty()
    {
        Assert.Empty(InterceptionHandler.BuildYoloBboxes(ImmutableArray<int>.Empty, 1080, 2400));
        Assert.Empty(InterceptionHandler.BuildYoloBboxes(ImmutableArray.Create(1, 2, 3), 1080, 2400));
    }
}
