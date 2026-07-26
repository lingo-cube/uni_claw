using UniClaw.ClaudeProvider;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// RealVisionIntegrationTests — 真实 AI 模型 + 预存截图的端到端测试。
/// 验证 PageAnalyzer 全链路：FileScreenCapture → Claude/DeepSeek Vision → PageAnalysis。
/// 标记 Trait(Category=Integration)，默认 excluded（需要 API key + 截图资产）。
/// 运行方式: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public sealed class RealVisionIntegrationTests
{
    [Fact(Skip = "需要截图资产: 在 Fixtures/Screenshots/ 放入 .png 文件并设置 Claude/LLM API key")]
    public async Task AnalyzeScreenshot_WithClaudeVision_ReturnsPageAnalysis()
    {
        var screenshotPath = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Screenshots", "sample_screen.png");

        if (!File.Exists(screenshotPath))
            return; // 无资产时静默跳过

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
        Assert.NotEmpty(page.Items);
        Assert.NotNull(page.Level1Dir);
    }
}
