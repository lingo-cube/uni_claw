using System.Reflection;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// Q21-Q23 architecture guard: test realizations must not remain reachable from
/// the Product Agent.Dsh source closure. This is intentionally source-level so
/// DI/config/reflection cannot reintroduce them through a production reference.
/// </summary>
public sealed class DecisionChannelClosureTests
{
    [Fact]
    public void Product_AgentDsh_SourceClosure_ContainsNo_TestTransportConcreteTypes()
    {
        var assemblyPath = typeof(UniClaw.Agent.Dsh.DshAgentAdapter).Assembly.Location;
        var projectDirectory = FindProjectDirectory(assemblyPath);
        var sourceFiles = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var source = string.Join("\n", sourceFiles.Select(File.ReadAllText));

        Assert.DoesNotContain("JsonRpcStdioTransport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FakeDshTransport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayDshTransport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StdioDecisionChannel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FakeDecisionChannel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayDecisionChannel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", source, StringComparison.OrdinalIgnoreCase);

        var exportedNames = typeof(UniClaw.Agent.Dsh.DshAgentAdapter).Assembly.GetExportedTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("IDshTransport", exportedNames);
        Assert.DoesNotContain("JsonRpcStdioTransport", exportedNames);
        Assert.DoesNotContain("FakeDshTransport", exportedNames);
        Assert.DoesNotContain("ReplayDshTransport", exportedNames);

        var projectFile = Path.Combine(projectDirectory, "UniClaw.Agent.Dsh.csproj");
        var project = File.ReadAllText(projectFile);
        Assert.DoesNotContain("tests/", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Harness", project, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindProjectDirectory(string assemblyPath)
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(assemblyPath)!);
             directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "UniClaw.Agent.Dsh");
            if (File.Exists(Path.Combine(candidate, "UniClaw.Agent.Dsh.csproj")))
                return candidate;
        }

        throw new InvalidOperationException("UniClaw.Agent.Dsh project directory not found");
    }
}
