using System.Text.RegularExpressions;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

public sealed class ActiveContainerContextArchitectureGuardTests
{
    private static readonly Regex ActiveContextField = new(
        @"\b(?:private|internal|public)\s+(?:readonly\s+)?ActiveContainerContext\??\s+(_activeContainerContext)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void AgentOwnsExactlyOneMutableActiveContextSlotAndNoOldField()
    {
        var agentSource = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.cs"));
        Assert.Single(ActiveContextField.Matches(agentSource));
        Assert.DoesNotContain("RuntimeContainer? _activeContainer;", agentSource, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenWorldDoesNotDeclareParentsStackOrMutableAncestrySet()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs"));
        Assert.DoesNotContain("Stack<", source, StringComparison.Ordinal);
        Assert.DoesNotContain("var parents", source, StringComparison.Ordinal);
        Assert.DoesNotContain("var ancestry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImmutableHashSet<string>? ancestry", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextSourceContainsOnlyExecutionAndActivePathFields()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/ActiveContainerContext.cs"));
        Assert.Contains("ActiveExecutionContainer", source, StringComparison.Ordinal);
        Assert.Contains("ActiveAncestorPath", source, StringComparison.Ordinal);
        var type = typeof(UniClaw.Runtime.Agent.ActiveContainerContext);
        var instanceMemberNames = type
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.Name)
            .Concat(type
                .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Select(property => property.Name))
            .ToArray();
        foreach (var forbidden in new[]
                 {
                     "belief", "observed", "world", "visited", "progress", "completeness",
                     "recovery", "transition", "goal", "strategy", "boundary", "history"
                 })
        {
            Assert.DoesNotContain(
                instanceMemberNames,
                name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AllRunEntrypointsExplicitlyStartKnownEmptyContext()
    {
        foreach (var file in new[] { "Agent.cs", "Agent.OpenWorld.cs", "Agent.PlanRun.cs", "Agent.SemanticRun.cs" })
        {
            var source = File.ReadAllText(RepoPath($"src/UniClaw.Runtime/Agent/{file}"));
            if (file == "Agent.cs")
                continue;
            Assert.Contains("StartRunActiveExecutionContext(", source, StringComparison.Ordinal);
        }
    }

    private static string RepoPath(string relative)
    {
        var directory = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(directory, ".git")))
        {
            var parent = Directory.GetParent(directory)?.FullName
                ?? throw new DirectoryNotFoundException("Unable to locate repository root.");
            directory = parent;
        }

        return Path.Combine(directory, relative);
    }
}
