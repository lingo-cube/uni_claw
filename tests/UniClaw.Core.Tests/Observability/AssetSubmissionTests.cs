using System.Text.Json;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// AssetSubmission Append flag serialization / deserialization tests.
/// </summary>
public sealed class AssetSubmissionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Append_DefaultsToFalse()
    {
        var submission = new AssetSubmission("test.cat", [1, 2, 3], "path.txt");

        Assert.False(submission.Append);
    }

    [Fact]
    public void Append_True_Roundtrips()
    {
        var submission = new AssetSubmission("test.cat", [1, 2, 3], "path.txt", append: true);

        Assert.True(submission.Append);
    }

    [Fact]
    public void Serialization_IncludesAppendField()
    {
        var submission = new AssetSubmission("asset.analysis_snapshot", [10, 20], "analysis.jsonl", append: true);
        var json = JsonSerializer.Serialize(submission, JsonOptions);

        Assert.Contains("\"append\":true", json);
    }

    [Fact]
    public void Serialization_DefaultAppendFalseStillSerializes()
    {
        // False is serialized explicitly — consumers don't guess.
        var submission = new AssetSubmission("asset.screenshot", [1], "before.png");
        var json = JsonSerializer.Serialize(submission, JsonOptions);

        Assert.Contains("\"append\":false", json);
    }
}
