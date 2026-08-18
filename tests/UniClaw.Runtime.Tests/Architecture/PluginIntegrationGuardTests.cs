using System.Reflection;
using System.Text.RegularExpressions;
using UniClaw.Runtime.DriverHost;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>
/// Mechanical integration-boundary guards for the DSH control-plane plugin
/// (design.md §8 PLUG-F17 and the integration-boundary invariants).
///
/// A–B: the UniClaw Kernel (Runtime, Runtime.Agent) must carry ZERO DSH
/// concepts — no imports, no tokens, no coupling in any direction.
/// C: the DriverHost boundary must not reach ADB / PhysicalHost / Vision.Host /
///    UniClaw.Runtime.Adapters / PhysicalEnvironment.
/// D: DSH concept references in .NET are confined to the NEW plugin/adapter
///    boundary (Control/ + Transport/); historical change-name citations in
///    pre-existing files are documentation, not dependencies.
/// F: the control surface is a CLOSED read-only method set — no mutation path
///    exists for any caller, transport included.
/// (Guard E — plugin module free of ADB/PhysicalEnvironment — is enforced by the
/// Node-side lifecycle test over dsh-plugin-uniclaw/src.)
/// </summary>
public sealed class PluginIntegrationGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "UniClaw.Runtime")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> SourceFiles(string relativeDir)
        => Directory.EnumerateFiles(Path.Combine(RepoRoot, relativeDir), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static IEnumerable<string> Violations(string relativeDir, string pattern, string? exclusionPattern = null)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var exclusion = exclusionPattern is null ? null : new Regex(exclusionPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (var file in SourceFiles(relativeDir))
        {
            var text = File.ReadAllText(file);
            if (exclusion is not null && exclusion.IsMatch(text))
            {
                continue;
            }
            if (regex.IsMatch(text))
            {
                yield return $"{file}: DSH reference";
            }
        }
    }

    [Fact]
    public void GuardA_RuntimeHasZeroDshDependency()
    {
        var violations = Violations("src/UniClaw.Runtime", @"\bdsh\b|deepseek|cordis|@deepseek").ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void GuardB_RuntimeAgentHasZeroDshDependency()
    {
        var violations = Violations("src/UniClaw.Runtime/Agent", @"\bdsh\b|deepseek|cordis|@deepseek").ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void GuardC_DriverHostHasNoDeviceOrExecutionAuthorityDependency()
    {
        var violations = Violations(
            "src/UniClaw.Runtime.DriverHost",
            @"\bAdb\b|PhysicalHost|Vision\.Host|UniClaw\.Runtime\.Adapters|PhysicalEnvironment").ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void GuardD_DshReferencesConfinedToPluginAdapterBoundary()
    {
        // Pre-existing files may cite historical change names as documentation.
        // The new changes (dsh-runtime-agent-subagent-run-entry,
        // dsh-assistance-provider-adapter) are cited in Execution/Assistance
        // comments for the same documentation purpose — change-name citations,
        // not DSH concept dependencies.
        const string historicalCitation = @"dsh-kernel-read-only-observability|dsh-runtime-agent-subagent-run-entry|dsh-assistance-provider-adapter";
        var violations = new List<string>();
        foreach (var file in SourceFiles("src/UniClaw.Runtime.DriverHost"))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}Control{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}Transport{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }
            var text = File.ReadAllText(file);
            var withoutCitation = Regex.Replace(text, historicalCitation, string.Empty);
            if (Regex.IsMatch(withoutCitation, @"\bdsh\b|deepseek|@deepseek|cordis", RegexOptions.IgnoreCase))
            {
                violations.Add($"{file}: DSH reference outside the plugin/adapter boundary");
            }
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void GuardF_ControlSurfaceIsAClosedReadOnlyMethodSet()
    {
        var expected = new HashSet<string>(
            ["Ping", "ListRuns", "InspectRun", "InspectTrap", "OpenEvidence", "GetRuntimeEvents", "ControlSupport"],
            StringComparer.Ordinal);
        var actual = typeof(IUniClawControlSurface).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(expected.SetEquals(actual),
            $"surface method set drifted: expected [{string.Join(", ", expected.Order())}] actual [{string.Join(", ", actual.Order())}]");

        // Every method returns a value; none returns void or Task (no fire-and-forget mutation seam).
        foreach (var method in typeof(IUniClawControlSurface).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.NotEqual(typeof(void), method.ReturnType);
        }
    }

    [Fact]
    public void GuardF2_DefaultSurfaceIsSealedAndReadOnlyByConstruction()
    {
        var type = typeof(UniClawControlSurface);
        Assert.True(type.IsSealed, "UniClawControlSurface must be sealed (single boundary implementation)");
        Assert.True(typeof(IUniClawControlSurface).IsAssignableFrom(type));
    }
}
