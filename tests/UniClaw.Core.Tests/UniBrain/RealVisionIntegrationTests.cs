using UniClaw.ClaudeProvider;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// RealVisionIntegrationTests — 真实 AI 模型 + 预存截图的端到端测试。
/// 默认跳过（无截图资产时禁用）。放入截图到 Fixtures/Screenshots/ 后，
/// dotnet test --filter "Category=Integration" 自动运行。
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

    [Fact(Skip = "集成测试: 需要手动去掉 Skip 运行，dotnet test --filter Category=Integration")]
    public async Task AnalyzeScreenshot_WithClaudeVision_ReturnsPageAnalysis()
    {
        var screenshotPath = FindScreenshot();
        if (screenshotPath is null)
            return; // 无截图资产时静默跳过

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_AUTH_TOKEN")
                     ?? "sk-local-spike-test";
        var baseUrl = Environment.GetEnvironmentVariable("CLAUDE_BASE_URL")
                      ?? "http://localhost:4000";

        var config = new AnthropicProviderConfig(ApiKey: apiKey, Model: "sonnet", BaseUrl: baseUrl);
        using var http = new HttpClient();
        var provider = new AnthropicModelProvider(http, config);
        var capture = new FileScreenCapture(screenshotPath);
        var analyzer = new PageAnalyzer(
            provider, new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual), capture);

        var page = await analyzer.AnalyzeCurrentPageAsync();

        Assert.NotNull(page);
        Assert.NotNull(page.Level1Dir);

        // 保存分析结果到 Screenshots 目录
        var resultPath = Path.ChangeExtension(screenshotPath, ".analysis.json");
        var result = new
        {
            level1_dir = page.Level1Dir.ToString(),
            level2_dir = page.Level2Dir.ToString(),
            level1_menus = page.Level1Menus.Select(m => new { name = m.Name, x = m.Coordinate.X, y = m.Coordinate.Y, active = m.Active }),
            current_path = page.CurrentPath,
            items = page.Items.Select(i => new { name = i.Name, type = i.Type.ToString(), action = i.ExpectedAction.ToString(), x = i.Coordinate.X, y = i.Coordinate.Y }),
            is_popup = page.IsPopup,
            has_scroll = page.HasScroll,
            is_end_of_list = page.IsEndOfList,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(resultPath, json);
    }
}
