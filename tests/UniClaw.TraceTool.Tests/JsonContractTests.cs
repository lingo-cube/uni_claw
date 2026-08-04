using System.Text.Json;
using UniClaw.TraceTool.Commands;
using Xunit;

namespace UniClaw.TraceTool.Tests;

/// <summary>
/// Task 7.3 — JSON contract tests: stdout carries a single JSON document wrapped with
/// schemaVersion ("1"), stderr carries logs, evidence is bounded, and the diff exit
/// code signals behavioral differences (trace-analyzer-cli spec §json-contract).
/// </summary>
[Collection("TraceCliConsole")]
public sealed class JsonContractTests
{
    private static string FailureDir => TraceRunFixture.FixturePath("failure");

    private static JsonElement ParseDocument(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement Data(JsonElement root)
    {
        Assert.True(root.TryGetProperty("data", out var data), "missing data wrapper");
        return data;
    }

    [Fact]
    public async Task Diagnose_JsonOutput_ContainsSchemaVersion()
    {
        var result = await CliTestHelper.RunAsync(
            "diagnose", "--run", FailureDir, "--format", "json");

        Assert.Equal(TraceExitCodes.Success, result.ExitCode);
        var root = ParseDocument(result.Out);
        Assert.Equal("1", root.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public async Task Diagnose_JsonOutput_ContainsRequiredFields()
    {
        var result = await CliTestHelper.RunAsync(
            "diagnose", "--run", FailureDir, "--format", "json");

        var data = Data(ParseDocument(result.Out));
        Assert.Equal("20260803T131333575Z-27768db6a5fe48d", data.GetProperty("runId").GetString());
        Assert.Equal("failure", data.GetProperty("status").GetString());
        Assert.True(data.TryGetProperty("run", out _));
        Assert.True(data.TryGetProperty("verdict", out var verdict));
        Assert.True(verdict.TryGetProperty("cause", out var cause));
        Assert.Equal("target_page_identity_not_verified", cause.GetString());
        Assert.True(data.TryGetProperty("evidence", out _));
        Assert.True(data.TryGetProperty("suggestions", out _));
        Assert.True(data.TryGetProperty("artifactPaths", out var artifactPaths));
        Assert.True(artifactPaths.TryGetProperty("tracePath", out _));
    }

    [Fact]
    public async Task Diagnose_JsonOutput_EvidenceBounded()
    {
        var result = await CliTestHelper.RunAsync(
            "diagnose", "--run", FailureDir, "--format", "json");

        var evidence = Data(ParseDocument(result.Out)).GetProperty("evidence");
        Assert.True(evidence.ValueKind == JsonValueKind.Array);
        Assert.InRange(evidence.GetArrayLength(), 0, 5);
    }

    [Fact]
    public async Task List_JsonOutput_IsValidJson()
    {
        var result = await CliTestHelper.RunAsync(
            "list",
            "--dir", Path.GetDirectoryName(FailureDir)!,
            "--format", "json");

        Assert.Equal(TraceExitCodes.Success, result.ExitCode);
        var data = Data(ParseDocument(result.Out));
        Assert.True(data.TryGetProperty("runs", out var runs));
        Assert.True(runs.ValueKind == JsonValueKind.Array);
        Assert.NotEmpty(runs.EnumerateArray());
    }

    [Fact]
    public async Task Diff_DetectsDifference_ExitsWithCodeOne()
    {
        // Success snapshot has no spans (CLI EmptyTrace gate, exit 3) — use the
        // failure fixture plus a metric-modified copy of it.
        var modified = await TestRunFactory.CreateModifiedFailureRunAsync();
        try
        {
            var result = await CliTestHelper.RunAsync(
                "diff", "--run-a", FailureDir, "--run-b", modified, "--format", "json");

            Assert.Equal(TraceExitCodes.DiffDetected, result.ExitCode);
            var data = Data(ParseDocument(result.Out));
            Assert.True(data.GetProperty("hasDifferences").GetBoolean());
            Assert.True(data.TryGetProperty("metricDiffs", out var metricDiffs));
            Assert.NotEmpty(metricDiffs.EnumerateArray());
        }
        finally
        {
            if (Directory.Exists(modified))
                Directory.Delete(modified, recursive: true);
        }
    }
}
