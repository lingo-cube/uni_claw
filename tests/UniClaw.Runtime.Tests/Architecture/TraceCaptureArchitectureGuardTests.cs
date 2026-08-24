using System.Text.RegularExpressions;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>
/// TC-01 mechanical guards for the reusable trace-capture/scenario-catalog
/// harness boundary. These checks inspect repository sources.
/// </summary>
public sealed class TraceCaptureArchitectureGuardTests
{
    private const string RuntimeProject = "src/UniClaw.Runtime/UniClaw.Runtime.csproj";
    private const string HarnessProject = "src/UniClaw.Runtime.Harness/UniClaw.Runtime.Harness.csproj";
    private const string RuntimeSource = "src/UniClaw.Runtime";
    private const string HarnessReplaySource = "src/UniClaw.Runtime.Harness/Replay";
    private const string TestSource = "tests/UniClaw.Runtime.Tests";

    private static readonly string[] ForbiddenHarnessReferences =
    {
        "UniClaw.Runtime.Adapters",
        "UniClaw.Runtime.PhysicalHost",
        "UniClaw.Runtime.DriverHost",
    };

    private static readonly string[] RuntimeHarnessDependencyTokens =
    {
        "UniClaw.Runtime.Harness",
        "UniClaw.Runtime.Adapters",
        "CapturingEnvironment",
        "TraceCaptureSession",
        "ITraceCaptureStore",
        "FileTraceCaptureStore",
        "ScenarioCatalog",
    };

    [Fact]
    public void RuntimeProject_HasNoProjectReferenceOrHarnessDependency()
    {
        var projectPath = RepoPath(RuntimeProject);
        Assert.True(File.Exists(projectPath), $"Missing required project: {RuntimeProject}");

        var project = File.ReadAllText(projectPath);
        Assert.DoesNotContain("<ProjectReference", project, StringComparison.Ordinal);

        foreach (var file in SourceFiles(RuntimeSource))
        {
            var source = File.ReadAllText(file);
            foreach (var token in RuntimeHarnessDependencyTokens)
            {
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void HarnessProject_ReferencesOnlyRuntime()
    {
        var projectPath = RepoPath(HarnessProject);
        Assert.True(File.Exists(projectPath), $"Missing required project: {HarnessProject}");

        var project = File.ReadAllText(projectPath);
        var references = Regex.Matches(project, @"<ProjectReference\b[^>]*Include=""([^""]+)""", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Single(references);
        Assert.Contains("UniClaw.Runtime.csproj", references[0], StringComparison.Ordinal);
        foreach (var forbidden in ForbiddenHarnessReferences)
        {
            Assert.DoesNotContain(forbidden, project, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReplayContracts_LiveInHarness_WithoutTestAssemblyDuplicates()
    {
        var harnessAssetContracts = RepoPath(Path.Combine(HarnessReplaySource, "AssetContracts.cs"));
        var harnessReplayEnvironment = RepoPath(Path.Combine(HarnessReplaySource, "ReplayEnvironment.cs"));
        Assert.True(File.Exists(harnessAssetContracts), $"Missing required source: {harnessAssetContracts}");
        Assert.True(File.Exists(harnessReplayEnvironment), $"Missing required source: {harnessReplayEnvironment}");

        Assert.Empty(Directory.EnumerateFiles(RepoPath(TestSource), "AssetContracts.cs", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(RepoPath(TestSource), "ReplayEnvironment.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public void AgentContainerTraversal_HaveNoCaptureCatalogOrStoreDependencies()
    {
        foreach (var component in new[] { "Agent", "Container", "Traversal" })
        {
            var componentPath = RepoPath(Path.Combine(RuntimeSource, component));
            Assert.True(Directory.Exists(componentPath), $"Missing required component directory: {componentPath}");

            foreach (var file in SourceFiles(componentPath))
            {
                var source = File.ReadAllText(file);
                Assert.DoesNotContain("UniClaw.Runtime.Harness", source, StringComparison.Ordinal);
                Assert.DoesNotContain("UniClaw.Runtime.Adapters", source, StringComparison.Ordinal);
                Assert.DoesNotMatch(@"\b(?:TraceCaptureSession|ITraceCaptureStore|FileTraceCaptureStore|CapturingEnvironment|ScenarioCatalog)\b", source);
            }
        }
    }

    private static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(RepoPath(directory), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string RepoPath(string relativePath)
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        return Path.GetFullPath(relativePath);
    }
}
