using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

public sealed class FastContainerResolverArchitectureGuardTests
{
    [Fact]
    public void ResolverIsStaticAndHasNoMutableState()
    {
        var type = typeof(FastContainerResolver);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
    }

    [Fact]
    public void PublicFastContractsAreImmutableAndUseClosedImmutableCollections()
    {
        var types = new[]
        {
            typeof(FastContainerResolutionRequest),
            typeof(FastContainerAssessment),
        };
        var mutableDefinitions = new[]
        {
            typeof(List<>), typeof(Dictionary<,>), typeof(IList<>),
            typeof(IDictionary<,>), typeof(ICollection<>),
        };

        foreach (var property in types.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            if (property.PropertyType.IsGenericType)
            {
                Assert.DoesNotContain(property.PropertyType.GetGenericTypeDefinition(), mutableDefinitions);
            }

            Assert.False(property.SetMethod?.IsPublic == true,
                $"{property.DeclaringType!.Name}.{property.Name} exposes a public setter.");
        }

        Assert.True(typeof(ImmutableArray<>).IsGenericType);
    }

    [Fact]
    public void ResolverHasNoExecutionOrExternalCallbackSurface()
    {
        foreach (var method in typeof(FastContainerResolver).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            Assert.DoesNotContain(method.GetParameters(), parameter =>
                typeof(Delegate).IsAssignableFrom(parameter.ParameterType)
                || parameter.ParameterType == typeof(CancellationToken)
                || parameter.ParameterType.Name.Contains("Task", StringComparison.OrdinalIgnoreCase)
                || parameter.ParameterType.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(method.ReturnType.Name, "Task", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FastModelDoesNotReferenceAuthorityLayersOrActions()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/World/FastContainerResolver.cs"));
        foreach (var forbidden in new[]
        {
            "DeviceAction", "ActionResult", "GoalEvidence", "WorldBelief",
            "DriverHost", "Agent", "Recovery", "Completion", "Memory",
            "Dispatch", "Authorize", "CurrentContainerState",
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FastTrustIsDerivedAndNotAStoredContractProperty()
    {
        var trust = typeof(FastContainerAssessment).GetProperty(nameof(FastContainerAssessment.FastTrusted));
        Assert.NotNull(trust);
        Assert.Null(trust!.SetMethod);
        Assert.DoesNotContain(typeof(FastContainerAssessment).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.Name.Contains("trust", StringComparison.OrdinalIgnoreCase));
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
