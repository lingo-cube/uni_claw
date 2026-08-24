using System.Reflection;
using System.Text.RegularExpressions;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>Mechanical guards for the optional pre-terminal seam and its authority boundary.</summary>
public sealed class PreTerminalCycleArchitectureGuardTests
{
    [Fact]
    public void CheckpointCallIsAtInventoryBoundaryOnly()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs"));
        Assert.Equal(1, Count(source, "TryEvaluatePreTerminalCheckpointAsync("));
        var inventory = source.IndexOf("TryAcceptBranchInventory", StringComparison.Ordinal);
        var checkpoint = source.IndexOf("TryEvaluatePreTerminalCheckpointAsync(", StringComparison.Ordinal);
        var authorization = source.IndexOf("CandidateAuthorizationEvaluator", checkpoint, StringComparison.Ordinal);
        Assert.True(inventory >= 0 && checkpoint > inventory);
        Assert.True(authorization < 0 || authorization > checkpoint);
    }

    [Fact]
    public void ForbiddenOperationalPhasesContainNoCheckpointCall()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs"));
        foreach (var phase in new[]
        {
            "SettlePostActionObservationAsync",
            "SettlePostScrollEvidenceQualityAsync",
            "ExploreCurrentContainerViewportsAsync",
            "RecoverFromDriftAsync",
        })
        {
            var phaseSource = phase == "RecoverFromDriftAsync"
                ? File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.Recovery.cs"))
                : source;
            var match = Regex.Match(phaseSource,
                $@"\n    private[^\n]*{Regex.Escape(phase)}[^\n]*\n(?<body>.*?)(?=\n    private|\z)",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            Assert.True(match.Success, $"phase source missing: {phase}");
            var body = match.Groups["body"].Value;
            Assert.DoesNotContain("TryEvaluatePreTerminalCheckpointAsync", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PassiveSurfacesHaveNoAuthorityMembers()
    {
        var proposalMembers = typeof(PreTerminalContinuationProposal).GetMembers()
            .Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
        var evaluatorMethods = typeof(IPreTerminalReasoningEvaluator).GetMethods()
            .Select(method => method.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var forbidden in new[] { "DeviceAction", "RunState", "GoalEvidence", "Recovery", "Fsm", "StartRun", "Complete", "Fail" })
            Assert.DoesNotContain(forbidden, proposalMembers);
        Assert.DoesNotContain("AuthorizeAction", evaluatorMethods);
        Assert.DoesNotContain("RunOpenWorldAsync", evaluatorMethods);
        Assert.DoesNotContain("Complete", evaluatorMethods);
        Assert.DoesNotContain("Fail", evaluatorMethods);
    }

    [Fact]
    public void PassiveSurfaceTypesContainNoAuthorityDependencies()
    {
        var forbidden = new[]
        {
            "DeviceAction", "RunState", "GoalEvidence", "Traversal", "Recovery",
            "StateMachine", "Fsm", "Lifecycle", "Agent", "MultiRun",
        };
        foreach (var surface in new[]
                 {
                     typeof(PreTerminalReasoningSnapshot),
                     typeof(PreTerminalContinuationProposal),
                     typeof(IPreTerminalReasoningEvaluator),
                 })
        {
            foreach (var type in SurfaceTypes(surface))
                Assert.DoesNotContain(forbidden, token =>
                    type.FullName?.Contains(token, StringComparison.OrdinalIgnoreCase) is true);
        }

        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.PreTerminalCycle.cs"));
        foreach (var forbiddenToken in new[] { "DeviceAction", "Traversal", "AuthorizeAction", "Complete", "RunOpenWorldAsync", "StartRun" })
            Assert.DoesNotContain(forbiddenToken, source, StringComparison.Ordinal);
    }

    private static IEnumerable<Type> SurfaceTypes(Type surface)
    {
        yield return surface;
        foreach (var property in surface.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return property.PropertyType;
            foreach (var nested in Unwrap(property.PropertyType)) yield return nested;
        }
        foreach (var field in surface.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return field.FieldType;
            foreach (var nested in Unwrap(field.FieldType)) yield return nested;
        }
        foreach (var method in surface.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return method.ReturnType;
            foreach (var nested in Unwrap(method.ReturnType)) yield return nested;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
                foreach (var nested in Unwrap(parameter.ParameterType)) yield return nested;
            }
        }
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (!type.IsGenericType) yield break;
        foreach (var argument in type.GetGenericArguments())
        {
            yield return argument;
            foreach (var nested in Unwrap(argument)) yield return nested;
        }
    }

    private static int Count(string value, string token)
        => Enumerable.Range(0, value.Length)
            .Count(index => index + token.Length <= value.Length
                && value.AsSpan(index, token.Length).SequenceEqual(token));

    private static string RepoPath(string relative)
    {
        // 仓库根 = 同时含 AGENTS.md 与 src/UniClaw.Runtime.sln 的目录
        // （子级区域地图只满足 AGENTS.md，不满足 sln 标记）。
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !(File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "src", "UniClaw.Runtime.sln"))))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), relative);
    }
}
