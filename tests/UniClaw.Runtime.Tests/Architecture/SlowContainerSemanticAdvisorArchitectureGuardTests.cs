using System.Collections;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>Mechanical boundary guards for the provider-neutral Slow seam.</summary>
public sealed class SlowContainerSemanticAdvisorArchitectureGuardTests
{
    private static readonly Type[] PublicContracts =
    [
        typeof(SlowContainerSemanticRequest),
        typeof(SlowContainerSemanticAssessment),
        typeof(SlowContainerSemanticInvocation),
        typeof(SlowContainerSemanticConsumption),
    ];

    [Fact]
    public void AdvisorPortHasOnlyTheExactAsyncAssessmentMethod()
    {
        var methods = typeof(ISlowContainerSemanticAdvisor).GetMethods();

        var method = Assert.Single(methods);
        Assert.Equal("AssessAsync", method.Name);
        Assert.Equal(typeof(Task<SlowContainerSemanticAssessment>), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(SlowContainerSemanticRequest), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    [Fact]
    public void PublicContractsHaveNoPublicSettersOrMutableCollectionProperties()
    {
        foreach (var contract in PublicContracts)
        {
            foreach (var property in contract.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.Null(property.SetMethod);
                Assert.False(property.PropertyType.IsArray, $"{contract.Name}.{property.Name} is an array");
                if (!property.PropertyType.IsGenericType)
                    continue;

                var definition = property.PropertyType.GetGenericTypeDefinition();
                Assert.DoesNotContain(
                    definition,
                    new[]
                    {
                        typeof(List<>),
                        typeof(Dictionary<,>),
                        typeof(IList<>),
                        typeof(IDictionary<,>),
                        typeof(ICollection<>),
                    });
            }
        }
    }

    [Fact]
    public void ConsumerIsStatelessAndExposesNoExternalCallbackSurface()
    {
        var type = typeof(SlowContainerSemanticConsumer);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            Assert.DoesNotContain(typeof(Delegate), method.GetParameters().Select(parameter => parameter.ParameterType));
            Assert.DoesNotContain(typeof(IAsyncEnumerable<>), method.GetParameters().Select(parameter => parameter.ParameterType));
        }
    }

    [Fact]
    public void ConsumerSeparatesAcquisitionFromLatestRevisionProjection()
    {
        var acquire = Assert.Single(
            typeof(SlowContainerSemanticConsumer).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "AcquireAsync"));
        Assert.Equal(typeof(Task<SlowContainerSemanticInvocation>), acquire.ReturnType);
        Assert.DoesNotContain(
            acquire.GetParameters(),
            parameter => parameter.ParameterType == typeof(SemanticEvidenceRevision));

        var project = Assert.Single(
            typeof(SlowContainerSemanticConsumer).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "Project"));
        Assert.Equal(typeof(SlowContainerSemanticConsumption), project.ReturnType);
        Assert.Contains(project.GetParameters(), parameter => parameter.ParameterType == typeof(SemanticEvidenceRevision));
    }

    [Fact]
    public void ProductionSourceContainsNoAuthorityOrBackendSurface()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/World/SlowContainerSemanticAdvisor.cs"));

        foreach (var forbidden in new[]
        {
            "DeviceAction", "ActionResult", "Goal", "Plan", "Recovery", "Completion",
            "GraphMutation", "CurrentContainerState", "DriverHost", "Agent", "ProviderImplementation",
            "HttpClient", "ModelId", "Credential", "latest",
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ConsumptionEffectIsDerivedConstantAndNotStoredState()
    {
        var properties = typeof(SlowContainerSemanticConsumption).GetProperties();
        var advisoryOnly = Assert.Single(properties.Where(property => property.Name == "IsAdvisoryOnly"));
        var runtimeEffect = Assert.Single(properties.Where(property => property.Name == "HasRuntimeEffect"));
        Assert.Null(advisoryOnly.SetMethod);
        Assert.Null(runtimeEffect.SetMethod);
        Assert.DoesNotContain(
            typeof(SlowContainerSemanticConsumption).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => field.Name.Contains("Effect", StringComparison.OrdinalIgnoreCase));
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
