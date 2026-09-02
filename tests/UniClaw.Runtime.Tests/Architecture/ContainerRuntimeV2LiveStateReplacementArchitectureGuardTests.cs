using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Agent;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>Mechanical guards for the single Agent-owned V2 physical-current slot.</summary>
public sealed class ContainerRuntimeV2LiveStateReplacementArchitectureGuardTests
{
    /// <summary>Guards against restoring the superseded mutable belief owner.</summary>
    [Fact]
    public void AgentHasExactlyOneV2StateFieldAndNoBeliefField()
    {
        var fields = typeof(RuntimeAgent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Single(fields.Where(field => field.FieldType == typeof(ContainerRuntimeV2State)));
        Assert.DoesNotContain(fields, field => string.Equals(field.Name, "_belief", StringComparison.Ordinal));
        Assert.DoesNotContain(fields, field => field.Name.Contains("latestFast", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, field => field.Name.Contains("latestSlow", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, field => field.Name.Contains("latestTrust", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, field => field.Name.Contains("latestCorrection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, field => field.Name.Contains("latestCheckpoint", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Guards that Agent compatibility writes use the V2 replacement seam.</summary>
    [Fact]
    public void AgentSourcesContainNoIndependentBeliefAssignment()
    {
        foreach (var path in new[]
        {
            "src/UniClaw.Runtime/Agent/Agent.cs",
            "src/UniClaw.Runtime/Agent/Agent.ContainerReconciliation.cs",
            "src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs",
            "src/UniClaw.Runtime/Agent/Agent.PlanRun.cs",
            "src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs",
            "src/UniClaw.Runtime/Agent/Agent.Recovery.cs",
        })
        {
            var source = File.ReadAllText(RepoPath(path));
            Assert.DoesNotContain("_belief =", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ReplaceV2Belief", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Guards the one-way read projection and Slow-disabled production path.</summary>
    [Fact]
    public void AgentBeliefIsReadOnlyAndProductionBuilderDisablesSlow()
    {
        var property = typeof(RuntimeAgent).GetProperty(nameof(RuntimeAgent.Belief));
        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
        var source = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.ContainerReconciliation.cs"));
        Assert.Contains("SlowContainerSemanticMode.Disabled", source, StringComparison.Ordinal);
        Assert.Contains("CompleteDisabled", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait(", source, StringComparison.Ordinal);
        Assert.Contains("ProjectV2Belief", source, StringComparison.Ordinal);
    }

    /// <summary>Guards that post-initial observations cannot silently bypass atomic V2 preparation.</summary>
    [Fact]
    public void PostInitialV2WritesHaveNoSilentReplaceBypass()
    {
        var reconciliation = File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.ContainerReconciliation.cs"));
        Assert.DoesNotContain("ReplaceV2Belief", reconciliation, StringComparison.Ordinal);
        Assert.Contains("TryCommitFreshObservedLocation", reconciliation, StringComparison.Ordinal);
        Assert.Contains("CommitContainerReconciliation", reconciliation, StringComparison.Ordinal);
        Assert.Equal(2, reconciliation.Split("_containerRuntimeV2State =", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("TryCommitFreshObservedLocation", File.ReadAllText(RepoPath("src/UniClaw.Runtime/Agent/Agent.cs")), StringComparison.Ordinal);
    }

    /// <summary>Guards that verified return can restore the parent's original V2 entry context.</summary>
    [Fact]
    public void ActivePathCarriesOnlyParentEntryEvidence()
    {
        var entry = typeof(ActiveAncestorPathEntry);
        var contextProperty = entry.GetProperty("ParentEntryContext");
        Assert.NotNull(contextProperty);
        Assert.Equal(typeof(ContainerEntryContext), contextProperty!.PropertyType);
        Assert.DoesNotContain(
            entry.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("CanonicalParent", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Guards that the public read model carries only opaque immutable value
    /// types: string / bool / opaque refs / transition values / revision /
    /// ImmutableArray&lt;string&gt; / the availability enum.  A Graph, live
    /// Container, ActiveContainerContext, progress, provider, action or
    /// recovery handle would fail this whitelist.
    /// </summary>
    [Fact]
    public void ContainerTransitionReadModelExposesOnlyOpaqueReadValueTypes()
    {
        var properties = typeof(ContainerTransitionReadModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEmpty(properties);
        foreach (var property in properties)
        {
            Assert.True(
                IsAllowedReadValueType(property.PropertyType),
                $"{nameof(ContainerTransitionReadModel)}.{property.Name} exposes forbidden type {property.PropertyType}");
        }
    }

    /// <summary>
    /// Guards the read model property surface against runtime-handle or
    /// authority vocabulary (Graph / action / goal / recovery / completion /
    /// provider / progress / handle / service / registry / cache / canonical
    /// parent / reverse edge), including string-typed aliases that a type
    /// whitelist alone could not catch.
    /// </summary>
    [Fact]
    public void ContainerTransitionReadModelExposesNoRuntimeHandleOrMutableSurfaceProperty()
    {
        string[] forbiddenFragments =
        {
            "Graph", "Action", "Goal", "Recovery", "Completion", "Provider",
            "Progress", "Handle", "Service", "Registry", "Cache",
            "CanonicalParent", "ReverseEdge", "ActiveContainerContext",
        };
        foreach (var property in typeof(ContainerTransitionReadModel)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain(
                forbiddenFragments,
                fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Guards at source level that no Agent partial file declares mutable
    /// latest Fast / Slow / trust / correction / checkpoint fields; latest
    /// assessment values are deliberately not retained.
    /// </summary>
    [Fact]
    public void AgentSourceContainsNoMutableLatestAssessmentOrCheckpointField()
    {
        foreach (var path in new[]
        {
            "src/UniClaw.Runtime/Agent/Agent.cs",
            "src/UniClaw.Runtime/Agent/Agent.ContainerReconciliation.cs",
            "src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs",
            "src/UniClaw.Runtime/Agent/Agent.PlanRun.cs",
            "src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs",
            "src/UniClaw.Runtime/Agent/Agent.Recovery.cs",
        })
        {
            var source = File.ReadAllText(RepoPath(path));
            foreach (var fragment in new[]
            {
                "latestFast", "latestSlow", "latestTrust", "latestCorrection", "latestCheckpoint",
            })
            {
                Assert.DoesNotContain(fragment, source, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>Guards that ContainerContext is a read-only derived projection with no setter.</summary>
    [Fact]
    public void AgentContainerContextIsReadOnlyDerivedProjection()
    {
        var property = typeof(RuntimeAgent).GetProperty(nameof(RuntimeAgent.ContainerContext));
        Assert.NotNull(property);
        Assert.True(property!.CanRead);
        Assert.False(property.CanWrite);
        Assert.Null(property.SetMethod);
    }

    private static bool IsAllowedReadValueType(Type propertyType)
    {
        var core = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return core == typeof(string)
            || core == typeof(bool)
            || core == typeof(ContainerNodeRef)
            || core == typeof(ContainerSliceRef)
            || core == typeof(ContainerRelationRef)
            || core == typeof(TransitionOccurrenceRef)
            || core == typeof(SemanticEvidenceRevision)
            || core == typeof(ContainerTransition)
            || core == typeof(ContainerTransitionOccurrence)
            || core == typeof(ContainerFastAssessmentAvailability)
            || core == typeof(ImmutableArray<string>);
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
