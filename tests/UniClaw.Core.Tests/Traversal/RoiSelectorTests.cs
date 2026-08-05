using System.Collections.Immutable;
using SkiaSharp;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// RoiSelector 密度评分测试 — 证明 yoloBboxes 输入激活密度权重 (0.6)，
/// 空 bbox → 退化纹理评分 (roi-scroll-detection PRD §3.5)。
/// 截图用合成纯白图：白底 texture≈0 / nonSolid≈0，分数完全由密度决定，
/// 消除其他信号干扰。
/// </summary>
public class RoiSelectorTests
{
    private static byte[] WhiteScreenshot(int w, int h)
    {
        using var bmp = new SKBitmap(
            new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bmp.Erase(SKColors.White);
        using var png = bmp.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    [Fact(DisplayName = "密度激活: bbox 在优选带 (40-65%) → 选中 ROI 覆盖 bbox 区域")]
    public void Select_WithDenseBboxes_ReturnsWindowOverlappingBbox()
    {
        const int w = 1080, h = 2400;
        // bbox (240,960,840,1360) 位于 40%-65% 带；白底 → 窗口分数 = 0.6×density
        var bboxes = new List<RoiRect> { new(240, 960, 840, 1360) };

        var roi = RoiSelector.Select(
            ImmutableArray<MenuItem>.Empty, bboxes, WhiteScreenshot(w, h), w, h);

        Assert.NotNull(roi);
        var overlapX = Math.Min(roi.Value.X2, 840) - Math.Max(roi.Value.X1, 240) + 1;
        var overlapY = Math.Min(roi.Value.Y2, 1360) - Math.Max(roi.Value.Y1, 960) + 1;
        Assert.True(overlapX > 0 && overlapY > 0,
            $"selected ROI {roi.Value} does not overlap bbox (240,960,840,1360)");
    }

    [Fact(DisplayName = "退化: 空 bbox + 纯白页 → null (纹理/非纯色均为 0, 分数 < 0.15)")]
    public void Select_EmptyBboxes_BlankPage_ReturnsNull()
    {
        const int w = 1080, h = 2400;

        Assert.Null(RoiSelector.Select(
            ImmutableArray<MenuItem>.Empty, [], WhiteScreenshot(w, h), w, h));
    }

    [Fact(DisplayName = "位置驱动: bbox 在候选带外底部 → 密度不足以越过阈值 → null")]
    public void Select_BboxesOutsideSweep_ReturnsNull()
    {
        const int w = 1080, h = 2400;
        // (864,2160,972,2304): y > 85% 扫描范围边缘, 与窗口 x 带 (54-917) 交集极小
        var bboxes = new List<RoiRect> { new(864, 2160, 972, 2304) };

        Assert.Null(RoiSelector.Select(
            ImmutableArray<MenuItem>.Empty, bboxes, WhiteScreenshot(w, h), w, h));
    }
}
