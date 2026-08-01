using System.Text.Json;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

public sealed class SchemasContractTests
{
    [Fact(DisplayName = "AnalyzeVisual schema: every coordinate axis is constrained to [0,1]")]
    public void AnalyzeVisual_AllCoordinateAxesAreNormalized()
    {
        using var document = JsonDocument.Parse(Schemas.AnalyzeVisual);
        var coordinateSchemas = FindCoordinateSchemas(document.RootElement).ToArray();

        Assert.Equal(6, coordinateSchemas.Length);
        foreach (var coordinateSchema in coordinateSchemas)
        {
            var properties = coordinateSchema.GetProperty("properties");
            AssertNormalizedAxis(properties.GetProperty("x"));
            AssertNormalizedAxis(properties.GetProperty("y"));
        }
    }

    [Fact(DisplayName = "AnalyzeVisual prompt: visible elements and normalized coordinates are explicit")]
    public void AnalyzeVisual_PromptStatesVisibilityAndCoordinateContract()
    {
        var prompt = PromptTemplateRegistry.AnalyzeVisual.SystemPrompt;

        Assert.Contains("only currently visible interactive elements", prompt, StringComparison.Ordinal);
        Assert.Contains("center lies outside the screenshot", prompt, StringComparison.Ordinal);
        Assert.Contains("closed interval [0,1]", prompt, StringComparison.Ordinal);
        Assert.Contains("never a pixel value", prompt, StringComparison.Ordinal);
    }

    private static IEnumerable<JsonElement> FindCoordinateSchemas(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("properties", out var properties) &&
                properties.ValueKind == JsonValueKind.Object &&
                properties.TryGetProperty("x", out _) &&
                properties.TryGetProperty("y", out _))
            {
                yield return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var coordinate in FindCoordinateSchemas(property.Value))
                {
                    yield return coordinate;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var coordinate in FindCoordinateSchemas(item))
                {
                    yield return coordinate;
                }
            }
        }
    }

    private static void AssertNormalizedAxis(JsonElement axisSchema)
    {
        Assert.Equal("number", axisSchema.GetProperty("type").GetString());
        Assert.Equal(0, axisSchema.GetProperty("minimum").GetInt32());
        Assert.Equal(1, axisSchema.GetProperty("maximum").GetInt32());
    }
}
