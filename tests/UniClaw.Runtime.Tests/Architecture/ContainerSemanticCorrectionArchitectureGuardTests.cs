using System.Reflection;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>Mechanical guards for the read-only correction boundary.</summary>
public sealed class ContainerSemanticCorrectionArchitectureGuardTests
{
    [Fact]
    public void PublicCorrectionContractsHaveNoPublicSettersOrMutableCollections()
    {
        var contracts = new[]
        {
            typeof(ContainerObligationContext),
            typeof(ContainerSemanticCorrectionFact),
            typeof(ContainerObligationContextRef),
            typeof(ContainerObligationReevaluationInput),
            typeof(ContainerPathConfirmation),
            typeof(ContainerExecutionPath),
            typeof(ContainerCheckpointProposal),
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
    public void ProjectorIsStaticAndHasNoMutableStateOrAsyncSurface()
    {
        var type = typeof(ContainerSemanticCorrectionProjector);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            Assert.DoesNotContain("Task", method.ReturnType.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(method.GetParameters(), parameter =>
                parameter.ParameterType == typeof(CancellationToken)
                || typeof(Delegate).IsAssignableFrom(parameter.ParameterType));
        }
    }

    [Fact]
    public void CorrectionSurfaceDoesNotReferenceAuthorityTypesOrActions()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/World/ContainerSemanticCorrection.cs"));
        foreach (var forbidden in new[]
        {
            "DeviceAction", "BranchProgressEvidence", "GoalEvidence", "ActiveContainerContext",
            "DriverHost", "GraphMutation", "WorldBelief", "RecoveryPlan", "Planner",
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("TaskContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DerivedEffectFlagsAreGetterOnlyAndFalseByConstruction()
    {
        foreach (var propertyName in new[] { "HasAppliedObligationMutation", "HasAction", "HasRecovery", "HasCompletion" })
        {
            var correction = typeof(ContainerSemanticCorrectionFact).GetProperty(propertyName);
            var input = typeof(ContainerObligationReevaluationInput).GetProperty(propertyName);
            Assert.NotNull(correction);
            Assert.NotNull(input);
            Assert.Null(correction!.SetMethod);
            Assert.Null(input!.SetMethod);
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
