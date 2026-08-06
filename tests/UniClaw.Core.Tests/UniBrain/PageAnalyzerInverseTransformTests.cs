using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using UniClaw.Core.Tests.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// PageAnalyzer 坐标逆变换测试 (e2e-dedup-vision-quality D4/D5) — raw 路径下,
/// 模型返回的 crop 空间坐标经 ToCoordinate 逆变换回全屏归一化空间; YoloBboxes
/// 从 C# 发送图像素角点映射为全屏像素角点。变换参数与 ImageResizer 调用同源
/// (env 覆盖 / 默认值)。fallback 路径无原始尺寸 → 跳过逆变换 (PageAnalyzerTests
/// happy path 已覆盖 as-is 行为)。
/// 8×8 屏幕 + MAX_WIDTH=4 + CROP_TOP=0.25 → Sx=2, CropTopPx=2, ScaleY=0.5:
///   y_full = y * 0.5 + 0.25;  bbox_full = bbox * 2 + (0, 2, 0, 2)。
/// </summary>
[Collection(nameof(EnvSensitiveTestsCollection))]
public sealed class PageAnalyzerInverseTransformTests : IDisposable
{
    private const string MaxWidthVar = "UNICLAW_IMAGE_MAX_WIDTH";
    private const string CropTopVar = "UNICLAW_IMAGE_CROP_TOP";
    private const string CropBottomVar = "UNICLAW_IMAGE_CROP_BOTTOM";
    private const string RawBufferVar = "UNICLAW_RAW_SCREEN_BUFFER";

    private readonly (string Name, string? Value)[] _saved =
    [
        (MaxWidthVar, Environment.GetEnvironmentVariable(MaxWidthVar)),
        (CropTopVar, Environment.GetEnvironmentVariable(CropTopVar)),
        (CropBottomVar, Environment.GetEnvironmentVariable(CropBottomVar)),
        (RawBufferVar, Environment.GetEnvironmentVariable(RawBufferVar)),
    ];

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
            Environment.SetEnvironmentVariable(name, value);
    }

    /// <summary>Raw 路径 fake — CaptureRawAsync 返回 8×8 RGBA 缓冲区, 旧路径不可用。</summary>
    private sealed class RawScreenCapture : IScreenCapture
    {
        public Task<RawScreenBuffer> CaptureRawAsync(CancellationToken ct = default)
            => Task.FromResult(new RawScreenBuffer(new byte[8 * 8 * 4], 8, 8, 1));

        public Task<byte[]> CaptureAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Old path not supported in raw fake");
    }

    /// <summary>固定 JSON 响应的 vision fake。</summary>
    private sealed class FixedJsonProvider : IModelProvider
    {
        private readonly string _content;
        public FixedJsonProvider(string content) => _content = content;
        public string ProviderId => "fixed-json";

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => Task.FromResult(new ModelResponse(_content, ProviderId, "vision", 50, 200, 15.0));

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private static string Json() =>
        "{\"level1_dir\":\"left\",\"level1_menus\":[],\"level2_dir\":\"top\",\"level2_menus\":[],"
        + "\"current_path\":[],"
        + "\"items\":[{\"name\":\"X\",\"type\":\"menu_item\",\"coordinate\":{\"x\":0.5,\"y\":0.4},\"parent\":null}],"
        + "\"yolo_bboxes\":[1,0,3,3],"
        + "\"is_popup\":false,\"popup_info\":null,"
        + "\"close_button\":{\"x\":0.5,\"y\":0.4},\"back_button\":null,"
        + "\"has_scroll\":false,\"is_end_of_list\":false}";

    private static PageAnalysis AnalyzeRaw()
    {
        // env: MAX_WIDTH=4 (等比缩放 sx = 8/4 = 2), CROP_TOP=0.25 (ScaleY=0.5, CropTopPx=2),
        // CROP_BOTTOM 显式清空 → 默认; RAW_SCREEN_BUFFER 非 "0" → raw 路径。
        Environment.SetEnvironmentVariable(MaxWidthVar, "4");
        Environment.SetEnvironmentVariable(CropTopVar, "0.25");
        Environment.SetEnvironmentVariable(CropBottomVar, null);
        Environment.SetEnvironmentVariable(RawBufferVar, null);

        var provider = new FixedJsonProvider(Json());
        var capture = new RawScreenCapture();
        var analyzer = new PageAnalyzer(provider, new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual), capture);
        var result = analyzer.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
        Assert.NotNull(result);
        return result;
    }

    [Fact(DisplayName = "raw 路径: item 坐标 y=0.4 → 逆变换 0.4*0.5+0.25 = 0.45 (x 不变)")]
    public void AnalyzeRaw_ItemCoordinate_InverseTransformedToFullScreen()
    {
        var page = AnalyzeRaw();

        var item = Assert.Single(page.Items, i => i.Name == "X");
        Assert.Equal(0.5, item.Coordinate.X);
        Assert.Equal(0.45, item.Coordinate.Y, 6);
    }

    [Fact(DisplayName = "raw 路径: close_button 坐标与 item 同公式逆变换")]
    public void AnalyzeRaw_PopupCloseCoordinate_InverseTransformed()
    {
        var page = AnalyzeRaw();

        Assert.NotNull(page.CloseButton);
        Assert.Equal(0.5, page.CloseButton.X);
        Assert.Equal(0.45, page.CloseButton.Y, 6);
    }

    [Fact(DisplayName = "raw 路径: YoloBboxes JSON 扁平列表重塑为 ImmutableArray<RoiRect> (像素已由 provider 变换)")]
    public void AnalyzeRaw_YoloBboxes_ReshapedToRoiRect()
    {
        var page = AnalyzeRaw();

        // JSON yolo_bboxes: [1,0,3,3] → 重塑为单个 RoiRect(1,0,3,3)
        // (像素值已由 local-vision provider 在 Python→C# 边界完成逆变换, PageAnalyzer 仅重塑)
        var bboxes = page.YoloBboxes;
        Assert.Single(bboxes);
        Assert.Equal(new RoiRect(1, 0, 3, 3), bboxes[0]);
    }
}
