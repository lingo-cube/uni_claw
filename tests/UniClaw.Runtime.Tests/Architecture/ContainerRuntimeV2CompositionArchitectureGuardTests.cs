using System.Reflection;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>Mechanical guards for the stateless V2 composition surface.</summary>
public sealed class ContainerRuntimeV2CompositionArchitectureGuardTests
{
    [Fact]
    public void FacadeIsStaticAndOwnsNoMutableState()
    {
        var facade = typeof(ContainerRuntimeV2);
        Assert.True(facade.IsAbstract && facade.IsSealed);
        Assert.Empty(facade.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        Assert.DoesNotContain(facade.GetMethods(BindingFlags.Public | BindingFlags.Static), method =>
            method.ReturnType == typeof(DeviceAction));
    }

    [Fact]
    public void CompositionContractsAreImmutableReadModels()
    {
        var contracts = new[]
        {
            typeof(ContainerRuntimeV2EvidenceContext),
            typeof(ContainerRuntimeV2LifecycleInput),
            typeof(ContainerRuntimeV2StartedResult),
            typeof(ContainerRuntimeV2ReadProjection),
            typeof(ContainerRuntimeV2SemanticTrustView),
            typeof(ContainerRuntimeV2LifecycleResult),
        };
        var mutableDefinitions = new[]
        {
            typeof(List<>), typeof(Dictionary<,>), typeof(IList<>),
            typeof(IDictionary<,>), typeof(ICollection<>),
        };

        foreach (var property in contracts.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            Assert.Null(property.SetMethod);
            Assert.False(property.PropertyType.IsArray);
            if (property.PropertyType.IsGenericType)
                Assert.DoesNotContain(property.PropertyType.GetGenericTypeDefinition(), mutableDefinitions);
        }
    }

    [Fact]
    public void AgentConsumerHasOnlyUnifiedProjectionAndCurrentStateSeam()
    {
        var method = typeof(RuntimeAgent).GetMethod("ConsumeContainerSemanticCorrection");
        Assert.NotNull(method);
        Assert.Equal(
            new[] { typeof(ContainerRuntimeV2ReadProjection), typeof(ContainerRuntimeV2State) },
            method!.GetParameters().Select(parameter => parameter.ParameterType));
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.ContainerReconciliation.cs"));
        Assert.Equal(1, source.Split("WithoutCompletedSibling", StringSplitOptions.None).Length - 1);
        Assert.Contains("ObservedActualCandidate", File.ReadAllText(RepoPath("src/UniClaw.Runtime/World/ContainerSemanticCorrection.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("ObservedVisitedCandidate", File.ReadAllText(RepoPath("src/UniClaw.Runtime/World/ContainerSemanticCorrection.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionExposesPendingOnlyAndAgentResultExposesZeroEffectFlags()
    {
        Assert.NotNull(typeof(ContainerRuntimeV2ReadProjection).GetProperty("PendingCorrectionRef"));
        Assert.Null(typeof(ContainerRuntimeV2ReadProjection).GetProperty("CorrectionConsumptionRef"));
        var resultType = typeof(RuntimeAgent).GetNestedType(
            "AgentSemanticCorrectionConsumptionResult", BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        foreach (var name in new[] { "HasAction", "HasRecovery", "HasGoalEvidenceMutation", "HasCompletion" })
        {
            var property = resultType!.GetProperty(name);
            Assert.NotNull(property);
            Assert.Null(property!.SetMethod);
        }
    }

    [Fact]
    public void ProductionFacadeDoesNotBecomeAnAuthorityCoordinator()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs"));
        foreach (var forbidden in new[]
        {
            "DeviceAction", "GoalEvidence", "RecoveryPlan", "RuntimeV2Coordinator",
            "_branchProgress", "MutableTrust", "ActionAuthorization",
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string RepoPath(string relative)
    {
        var directory = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(directory, ".git")))
        {
            directory = Directory.GetParent(directory)?.FullName
                ?? throw new DirectoryNotFoundException("Unable to locate repository root.");
        }

        return Path.Combine(directory, relative);
    }
}
