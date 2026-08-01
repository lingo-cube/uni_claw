using UniClaw.ClaudeProvider;
using UniClaw.Core.UniBrain;
using UniClaw.Core.Tests.Integration;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// 最小粒度视觉集成测试：单张预存图片 → PageAnalyzer → 与预期 golden 对比。
/// 默认跳过（IntegrationFact）。运行：
/// <code>
/// UNICLAW_INTEGRATION_SCOPES=vision-golden dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-golden"
/// </code>
///
/// 资产约定（见 Fixtures/Screenshots/README.md）：
/// - 截图放在 tests/UniClaw.Core.Tests/Fixtures/Screenshots/（PNG/JPG）。
/// - 预期文件与截图同名、后缀 .expected.json（如 a.jpg → a.expected.json）。
/// - 首次校准：UNICLAW_VISION_UPDATE_EXPECTED=1 运行一次，审阅生成的预期文件后再提交。
/// - 单图覆盖：UNICLAW_VISION_IMAGE=/path/to/screen.png。
/// </summary>
[Trait("Category", "Integration")]
public sealed class VisionGoldenIntegrationTests
{
    private static string? FindScreenshot()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Screenshots");
        if (!Directory.Exists(dir)) return null;
        return Directory.GetFiles(dir, "*.png")
            .Concat(Directory.GetFiles(dir, "*.jpg"))
            .Concat(Directory.GetFiles(dir, "*.jpeg"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    [Trait("IntegrationScope", IntegrationTestScopes.VisionGolden)]
    [IntegrationFact(IntegrationTestScopes.VisionGolden)]
    public async Task AnalyzeSingleImage_MatchesExpectedGolden()
    {
        var imagePath = Environment.GetEnvironmentVariable("UNICLAW_VISION_IMAGE")
                        ?? FindScreenshot();
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            throw new InvalidOperationException(
                "未找到截图资产。把图片放入 tests/UniClaw.Core.Tests/Fixtures/Screenshots/，"
                + "或设 UNICLAW_VISION_IMAGE=/path/to/screen.png 后再运行。");
        }

        var config = VisionTestSecrets.LoadSensenovaConfig();
        using var http = new HttpClient();
        var provider = new OpenAiCompatibleVisionProvider(
            http,
            new OpenAiCompatibleProviderConfig(config.ApiKey, config.Model, config.BaseUrl));
        var analyzer = new PageAnalyzer(
            provider,
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new FileScreenCapture(imagePath));

        var page = await analyzer.AnalyzeCurrentPageAsync();
        Assert.NotNull(page);
        Assert.NotEmpty(page.Items);

        // 实际结果落盘，便于人工 diff。
        var actualPath = Path.ChangeExtension(imagePath, ".actual.json");
        await File.WriteAllTextAsync(
            actualPath,
            VisionGoldenComparer.Serialize(VisionGoldenComparer.FromPageAnalysis(page)));

        var expectedPath = Path.ChangeExtension(imagePath, ".expected.json");
        if (string.Equals(
                Environment.GetEnvironmentVariable("UNICLAW_VISION_UPDATE_EXPECTED"),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            // 校准模式：把本次识别结果固化为 golden，供人工审阅后提交。
            await File.WriteAllTextAsync(
                expectedPath,
                VisionGoldenComparer.Serialize(VisionGoldenComparer.FromPageAnalysis(page)));
            return;
        }

        if (!File.Exists(expectedPath))
        {
            throw new InvalidOperationException(
                $"预期文件缺失：{expectedPath}。首次运行请设 "
                + "UNICLAW_VISION_UPDATE_EXPECTED=1 生成 golden，审阅后提交。");
        }

        VisionGoldenComparer.AssertMatches(
            VisionGoldenComparer.Load(expectedPath),
            page);
    }
}
