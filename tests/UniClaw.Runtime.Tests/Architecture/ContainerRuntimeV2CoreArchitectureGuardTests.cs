using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

public sealed class ContainerRuntimeV2CoreArchitectureGuardTests
{
    [Fact]
    public void CurrentContainerRemainsThinAndGraphHasNoCurrentSlot()
    {
        var currentProperties = typeof(CurrentContainer)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();
        Assert.Equal(new[] { "NodeRef", "CurrentSliceRef", "EntryContext" }, currentProperties);

        var graphProperties = typeof(ContainerGraphSnapshot)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain(graphProperties, name =>
            name.Contains("Current", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Parent", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Route", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Action", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Authorize", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Recover", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Complete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicCollectionsAreImmutableValueSurfaces()
    {
        var modelTypes = new[]
        {
            typeof(ContainerGraphNode), typeof(ContainerSlice), typeof(ContainerEntryContext),
            typeof(CurrentContainer), typeof(ContainerTransitionOccurrence),
            typeof(ContainerGraphRelation), typeof(ContainerGraphSnapshot), typeof(ContainerRuntimeV2State),
            typeof(ContainerRuntimeV2ReductionInput), typeof(ContainerRuntimeV2Preparation),
            typeof(ContainerGraphRelationAssessment),
        };

        foreach (var property in modelTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
        {
            if (property.PropertyType.IsGenericType)
            {
                Assert.DoesNotContain(property.PropertyType.GetGenericTypeDefinition(), new[]
                {
                    typeof(List<>), typeof(Dictionary<,>), typeof(IList<>),
                    typeof(IDictionary<,>), typeof(ICollection<>)
                });
            }

            Assert.False(property.SetMethod?.IsPublic == true,
                $"{property.DeclaringType!.Name}.{property.Name} exposes a public setter.");
        }
    }

    [Fact]
    public void ReducerIsStaticAndHasNoAsyncOrExternalCallbackSurface()
    {
        var type = typeof(ContainerRuntimeV2Reducer);
        Assert.True(type.IsAbstract && type.IsSealed);
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            Assert.DoesNotContain(method.GetParameters(), parameter =>
                typeof(Delegate).IsAssignableFrom(parameter.ParameterType)
                || parameter.ParameterType == typeof(CancellationToken)
                || parameter.ParameterType.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase)
                || parameter.ParameterType.Name.Contains("Task", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(method.ReturnType.Name, "Task", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GraphAssessmentQueryIsStaticPureAndNonAuthoritative()
    {
        var type = typeof(ContainerGraphQuery);
        Assert.True(type.IsAbstract && type.IsSealed);
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (method.IsPublic)
            {
                Assert.DoesNotContain(method.GetParameters(), parameter =>
                    parameter.ParameterType == typeof(ContainerGraphRelation));
            }
            Assert.DoesNotContain(method.GetParameters(), parameter =>
                typeof(Delegate).IsAssignableFrom(parameter.ParameterType)
                || parameter.ParameterType == typeof(CancellationToken)
                || parameter.ParameterType.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase)
                || parameter.ParameterType.Name.Contains("Task", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(method.ReturnType.Name, "Task", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(method.Name, "Route", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(method.Name, "Authorize", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(method.Name, "Recover", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(method.Name, "Complete", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NewSymbolsDocumentWhyExistingResponsibilitiesCannotOwnV2Core()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs"));
        Assert.Contains("NEW_SYMBOL_JUSTIFICATION", source, StringComparison.Ordinal);
        Assert.Contains("SemanticPageName", source, StringComparison.Ordinal);
        Assert.Contains("ContainerTransition", source, StringComparison.Ordinal);
        Assert.Contains("RunExecutionGraph", source, StringComparison.Ordinal);
        Assert.Contains("cannot own", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionModelDoesNotReferenceForbiddenAuthorityLayers()
    {
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs"));
        foreach (var forbidden in new[] { "UniClaw.Runtime.Agent", "UniClaw.Runtime.Container", "DriverHost", "Environment", "Provider" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        Assert.DoesNotContain("IContainerGraphReader", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IContainerGraphRecorder", source, StringComparison.Ordinal);
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
