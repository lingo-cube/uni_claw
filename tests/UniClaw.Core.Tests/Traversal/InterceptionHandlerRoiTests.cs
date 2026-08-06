using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// ROI 集成测试 — InterceptionHandler 直接消费 PageAnalysis.YoloBboxes (ImmutableArray&lt;RoiRect&gt;)。
/// BuildYoloBboxes 已删除 (e2e-dedup-vision-quality D5 迁移) — YOLO bbox 像素逆变换在
/// LocalVisionProvider Python→C# 边界完成，PageAnalyzer 仅做 List&lt;int&gt; → ImmutableArray&lt;RoiRect&gt; 重塑，
/// InterceptionHandler 直接使用 analysis.YoloBboxes，零转换。
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

    [Fact(DisplayName = "PageAnalysis.YoloBboxes 直接消费: ImmutableArray<RoiRect> 透传 (无二次变换)")]
    public void YoloBboxes_DirectConsumption_AsRoiRect()
    {
        // 模拟 LocalVisionProvider 已输出全屏 RoiRect (JSON 重塑后)
        var analysis = new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: ImmutableArray<MenuItem>.Empty,
            YoloBboxes: ImmutableArray.Create(
                new RoiRect(360, 600, 720, 780),
                new RoiRect(100, 200, 300, 400)));

        var result = analysis.YoloBboxes.ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(new RoiRect(360, 600, 720, 780), result[0]);
        Assert.Equal(new RoiRect(100, 200, 300, 400), result[1]);
    }

    [Fact(DisplayName = "PageAnalysis.YoloBboxes 空 → RoiSelector 退化 (AI provider 无检测数据)")]
    public void YoloBboxes_Empty_ReturnsEmpty()
    {
        var analysis = new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: ImmutableArray<MenuItem>.Empty,
            YoloBboxes: ImmutableArray<RoiRect>.Empty);

        Assert.Empty(analysis.YoloBboxes.ToList());
    }
}
