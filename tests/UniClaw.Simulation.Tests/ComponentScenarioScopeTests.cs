using System.Text.Json;
using UniClaw.Simulation.Tests.ComponentReplay;
using Xunit;

namespace UniClaw.Simulation.Tests;

public sealed class ComponentScenarioScopeTests
{
    [Fact]
    public void SharedAnnotationCases_MatchExpectedValidity()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory!.FullName, "tests/UniClaw.Simulation.Tests/Fixtures/ComponentReplay/annotation-cases.json");
        using var matrix = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var row in matrix.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = row.GetProperty("id").GetString();
            var expected = row.GetProperty("valid").GetBoolean();
            var errors = ComponentScenarioScope.Validate(row.GetProperty("data"));
            Assert.True(expected == (errors.Count == 0), $"{id}: expected valid={expected}; {string.Join("; ", errors)}");
        }
    }
}
