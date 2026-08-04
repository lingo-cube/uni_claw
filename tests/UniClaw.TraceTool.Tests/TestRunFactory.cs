using System.Text.Json.Nodes;

namespace UniClaw.TraceTool.Tests;

/// <summary>
/// Test-run factories: the success snapshot predates span recording (0 spans), so the
/// CLI's EmptyTrace gate (exit 3) rejects it. CLI diff tests therefore need a second
/// span-bearing run that still differs from the failure fixture — a copy of the failure
/// run with altered result.json metrics.
/// </summary>
internal static class TestRunFactory
{
    /// <summary>Copy of the failure fixture with durationMs/stepsConsumed changed.</summary>
    public static async Task<string> CreateModifiedFailureRunAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"diff-run-{Guid.NewGuid():N}");
        CopyDirectory(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "failure"),
            tempDir);
        var resultPath = Path.Combine(tempDir, "result.json");
        var node = JsonNode.Parse(await File.ReadAllTextAsync(resultPath))!;
        node["durationMs"] = 99999;
        node["stepsConsumed"] = 99;
        await File.WriteAllTextAsync(resultPath, node.ToJsonString());
        return tempDir;
    }

    public static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
