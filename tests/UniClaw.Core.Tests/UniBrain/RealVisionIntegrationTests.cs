using System.Text.Json;
using UniClaw.ClaudeProvider;
using UniClaw.Core.UniBrain;
using UniClaw.Core.Tests.Integration;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// RealVisionIntegrationTests — 真实 AI 视觉模型 + 预存截图的端到端测试。
/// 默认跳过（IntegrationFact）；仅当
/// <c>UNICLAW_INTEGRATION_SCOPES=vision-smoke</c> 时按需运行。
/// 用 ClaudeProvider 的 OpenAiCompatibleVisionProvider 直连 sensenova-6.7-flash-lite
/// （OpenAI 协议 /v1/chat/completions + image_url），与 Host 生产组装一致，
/// 不走 litellm gateway。
/// </summary>
[Trait("Category", "Integration")]
public sealed class RealVisionIntegrationTests
{
    private static string? FindScreenshot()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Screenshots");
        if (!Directory.Exists(dir)) return null;
        return Directory.GetFiles(dir, "*.png")
            .Concat(Directory.GetFiles(dir, "*.jpg"))
            .Concat(Directory.GetFiles(dir, "*.jpeg"))
            .FirstOrDefault();
    }

    [Trait("IntegrationScope", IntegrationTestScopes.VisionSmoke)]
    [IntegrationFact(IntegrationTestScopes.VisionSmoke)]
    public async Task AnalyzeScreenshot_WithSensenovaVision_ReturnsPageAnalysis()
    {
        var screenshotPath = FindScreenshot();
        if (screenshotPath is null)
            return; // 无截图资产时静默跳

        var config = VisionTestSecrets.LoadSensenovaConfig();
        using var http = new HttpClient();
        var provider = new OpenAiCompatibleVisionProvider(
            http,
            new OpenAiCompatibleProviderConfig(
                config.ApiKey,
                config.Model,
                config.BaseUrl));
        var capture = new FileScreenCapture(screenshotPath);
        var analyzer = new PageAnalyzer(
            provider, new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual), capture);

        var page = await analyzer.AnalyzeCurrentPageAsync();

        Assert.NotNull(page);
        // sensenova 对设置界面应能识别出交互元素，验证 items 非空
        Assert.NotEmpty(page.Items);

        // 保存分析结果到 Screenshots 目录
        var resultPath = Path.ChangeExtension(screenshotPath, ".analysis.json");
        var result = new
        {
            provider = provider.ProviderId,
            model = config.Model,
            level1_dir = page.Level1Dir.ToString(),
            level2_dir = page.Level2Dir.ToString(),
            level1_menus = page.Level1Menus.Select(m => new { name = m.Name, x = m.Coordinate.X, y = m.Coordinate.Y, active = m.Active }),
            current_path = page.CurrentPath,
            items = page.Items.Select(i => new { name = i.Name, type = i.Type.ToString(), action = i.ExpectedAction.ToString(), x = i.Coordinate.X, y = i.Coordinate.Y, parent = i.Parent }),
            is_popup = page.IsPopup,
            has_scroll = page.HasScroll,
            is_end_of_list = page.IsEndOfList,
        };
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(resultPath, json);
    }
}
