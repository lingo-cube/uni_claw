using Xunit;
using UniClaw.Runtime.Model;
using UniClaw.Semantic.Infrastructure.Fast;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>
/// Characterization guards for the external semantic-capability boundary.
/// The neutrality guard intentionally remains red until embedded scenario
/// knowledge is migrated.
/// </summary>
public sealed class ExternalSemanticCapabilityBoundaryGuardTests
{
    private const string RuntimeSource = "src/UniClaw.Runtime";

    private static readonly string[] KnownExternalCapabilityRoots =
    {
        "src/UniClaw.SemanticCapability",
        "src/UniClaw.Semantic.Settings",
        "src/UniClaw.Runtime.SemanticCapability",
        "src/UniClaw.Runtime.Semantic",
    };

    private static readonly string[] ScenarioTokens =
    {
        "Settings", "DeveloperOptions", "PreferenceRow", "NavigateUp", "Navigate up",
        "collapsing_toolbar", "wifi settings", "scenario corpus",
    };

    [Fact]
    public void ExternalSemanticCapabilityProjects_AreOptionalAndDependencyDirectionIsInward()
    {
        foreach (var root in ExternalCapabilityRoots())
        {
            var fullRoot = RepoPath(root);
            if (!Directory.Exists(fullRoot))
                continue;

            foreach (var project in Directory.GetFiles(fullRoot, "*.csproj", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(project);
                Assert.DoesNotContain("UniClaw.Runtime.Agent", content, StringComparison.Ordinal);
                Assert.DoesNotContain("UniClaw.Runtime.Traversal", content, StringComparison.Ordinal);
                Assert.DoesNotContain("UniClaw.Runtime.Recovery", content, StringComparison.Ordinal);
                Assert.DoesNotContain("UniClaw.Runtime.DriverHost", content, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Runtime_DoesNotRequireAnExternalSemanticCapabilityAtBuildOrStartup()
    {
        var project = File.ReadAllText(RepoPath("src/UniClaw.Runtime/UniClaw.Runtime.csproj"));
        Assert.DoesNotContain("SemanticCapability", project, StringComparison.OrdinalIgnoreCase);

        foreach (var file in RuntimeSourceFiles())
        {
            var content = CodeOnly(File.ReadAllText(file));
            Assert.DoesNotContain("UniClaw.SemanticCapability", content, StringComparison.Ordinal);
            Assert.DoesNotContain("RequireSemanticCapability", content, StringComparison.Ordinal);
            Assert.DoesNotContain("SemanticCapabilityRequired", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SemanticInfrastructureImplementations_AreNotEmbeddedInRuntimeAssembly()
    {
        var runtimeAssembly = typeof(Observation).Assembly;
        var infrastructureAssembly = typeof(FastSemanticContainerIdentityProvider).Assembly;

        Assert.Null(runtimeAssembly.GetType(
            "UniClaw.Runtime.Capabilities.Perception.Semantic.Fast.FastSemanticContainerIdentityProvider"));
        Assert.Null(runtimeAssembly.GetType(
            "UniClaw.Runtime.Capabilities.Perception.Semantic.Corpus.SemanticCorpus"));
        Assert.Same(infrastructureAssembly,
            typeof(FastSemanticContainerIdentityProvider).Assembly);
        Assert.NotNull(infrastructureAssembly.GetType(
            "UniClaw.Semantic.Infrastructure.Fast.FastSemanticContainerIdentityProvider"));
        Assert.NotNull(infrastructureAssembly.GetType(
            "UniClaw.Semantic.Infrastructure.Corpus.SemanticCorpus"));
    }

    [Fact]
    public void ExternalSemanticCapabilityAuthorityBearingTypes_ContainNoExecutionSurface()
    {
        foreach (var file in ExternalSourceFiles())
        {
            var name = Path.GetFileName(file);
            if (!name.Contains("V2", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("CapabilityBinding", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("SemanticEvidence", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = CodeOnly(File.ReadAllText(file));
            Assert.DoesNotMatch(
                @"(?im)^\s*(?:public|protected|internal)\s+[^;\r\n]*(?:DeviceAction|RunState|GoalEvidence|Traversal|StateMachine|Recovery|RunOpenWorld|StartRun|StartStrategyRun)\b",
                content);
            Assert.DoesNotMatch(
                @"(?im)^\s*(?:public|protected|internal)\s+(?:event\s+)?(?:Action|Func|Delegate)\b",
                content);
        }
    }

    [Fact]
    public void RuntimeProductionSource_IsScenarioNeutral()
    {
        var violations = RuntimeSourceFiles()
            .SelectMany(file => ScenarioTokens
                .Where(token => CodeOnly(File.ReadAllText(file)).Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => Relative(file) + " contains scenario token '" + token + "'"))
            .ToArray();

        Assert.True(violations.Length == 0,
            "Runtime production source contains scenario knowledge. Move interpretation to an external "
            + "semantic capability binding.\n" + string.Join("\n", violations));

        var adapterViolations = SourceFiles(RepoPath("src/UniClaw.Runtime.Adapters"))
            .SelectMany(file => new[] { "toolbar", "page title", "preference row", "Navigate up" }
                .Where(token => CodeOnly(File.ReadAllText(file)).Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => Relative(file) + " contains semantic-role pattern '" + token + "'"))
            .ToArray();
        Assert.True(adapterViolations.Length == 0,
            "Adapters must retain acquisition and primitive parsing only; semantic roles belong to external bindings.\n"
            + string.Join("\n", adapterViolations));
    }

    [Fact]
    public void StrategyAndPreTerminalSources_DoNotAcquireSemanticOrExecutionAuthority()
    {
        foreach (var relative in new[]
                 {
                     "src/UniClaw.Runtime/Planning/StrategyContract.cs",
                     "src/UniClaw.Runtime/Planning/StrategyExecutionReasoningSession.cs",
                     "src/UniClaw.Runtime/Agent/Agent.PreTerminalCycle.cs",
                 })
        {
            var content = CodeOnly(File.ReadAllText(RepoPath(relative)));
            foreach (var token in new[]
                     { "DeviceAction", "StartRun", "GoalEvidence",
                       "UniClaw.Semantic.Settings", "UniClaw.SemanticCapability" })
                Assert.DoesNotContain(token, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AdbAdapter_IsOptionalAndCannotBeRuntimeRequirement()
    {
        foreach (var project in Directory.GetFiles(RepoPath("src"), "*.csproj", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(project);
            if (!content.Contains("Adb", StringComparison.OrdinalIgnoreCase)
                && !content.Contains("ADB", StringComparison.Ordinal))
                continue;

            Assert.DoesNotContain("UniClaw.Runtime.csproj", content, StringComparison.Ordinal);
        }

        foreach (var file in RuntimeSourceFiles())
        {
            var content = CodeOnly(File.ReadAllText(file));
            Assert.DoesNotContain("AdbUiHierarchySource", content, StringComparison.Ordinal);
            Assert.DoesNotContain("ADB_REQUIRED", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> RuntimeSourceFiles()
        => SourceFiles(RepoPath(RuntimeSource));

    private static IEnumerable<string> ExternalSourceFiles()
        => ExternalCapabilityRoots().SelectMany(root => Directory.Exists(RepoPath(root))
            ? SourceFiles(RepoPath(root))
            : Enumerable.Empty<string>());

    private static IEnumerable<string> ExternalCapabilityRoots()
    {
        foreach (var root in KnownExternalCapabilityRoots)
            yield return root;

        var src = RepoPath("src");
        if (!Directory.Exists(src))
            yield break;

        foreach (var project in Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(project);
            if (name.Contains("Semantic", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Capability", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Evaluation", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Settings", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.GetRelativePath(RepoRoot(), Path.GetDirectoryName(project)!);
            }
        }
    }

    private static IEnumerable<string> SourceFiles(string root)
        => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string RepoPath(string relative)
        => Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Relative(string path)
        => Path.GetRelativePath(RepoRoot(), path);

    // Guards must inspect executable source shape, not XML documentation or comments.
    private static string CodeOnly(string source)
        => System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(source, @"/\*.*?\*/", string.Empty,
                System.Text.RegularExpressions.RegexOptions.Singleline),
            @"//[^\r\n]*", string.Empty);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            // 仓库根 = 同时含 AGENTS.md 与 src/UniClaw.Runtime.sln（子级区域地图只满足 AGENTS.md）。
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "src", "UniClaw.Runtime.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
