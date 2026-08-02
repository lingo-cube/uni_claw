using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

public sealed class PromptTemplateRegistryTests
{
    [Fact]
    public void AnalyzeVisualLite_HasOwnCapability_AndIsRegisteredSeparately()
    {
        var library = new PromptLibrary(
            PromptTemplateRegistry.AnalyzeVisual,
            PromptTemplateRegistry.AnalyzeVisualLite);

        Assert.NotNull(library.GetTemplate(ModelCapabilities.AnalyzeVisual));
        Assert.NotNull(library.GetTemplate(ModelCapabilities.AnalyzeVisualLite));
    }

    [Fact]
    public void AnalyzeVisualLite_RequestsOnlyChangeCheckAnswer()
    {
        var template = PromptTemplateRegistry.AnalyzeVisualLite;

        Assert.Equal(ModelCapabilities.AnalyzeVisualLite, template.Capability);
        // Full-analysis vocabulary must NOT leak into the lite prompt.
        Assert.DoesNotContain("level1_menus", template.SystemPrompt);
        Assert.DoesNotContain("items", template.SystemPrompt);
        Assert.Contains("changed", template.SystemPrompt);
        Assert.Contains("page_identity", template.SystemPrompt);
        Assert.Contains("item_count", template.SystemPrompt);
    }

    [Fact]
    public void AnalyzeVisualLite_ReducesResponseBudget()
    {
        Assert.Equal(1024, PromptTemplateRegistry.AnalyzeVisualLite.MaxTokens);
    }

    [Fact]
    public void AnalyzeVisualLite_ResolvesBeforeContextVariable()
    {
        var template = PromptTemplateRegistry.AnalyzeVisualLite;

        var resolved = template.Resolve(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["before"] = "{\"current_path\":[\"Settings\"]}",
            });

        Assert.Contains("{\"current_path\":[\"Settings\"]}", resolved.User);
    }
}
