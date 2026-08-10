using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>SC-CP14-TL-MVS-001 deterministic proof of the open-world type-level boundary.</summary>
public sealed class Cp14TypeLevelRepresentationScenarioTests
{
    [Fact]
    public void FsA_BoundedSafeTraversalSpecification_HasNoConcreteRouteOrWorkInventory()
    {
        var specification = Create(maximumDepth: 2);

        Assert.Equal("Settings", specification.Scope.ApplicationIdentity);
        Assert.Equal("Root", specification.Scope.SemanticRoot);
        Assert.Equal(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer), specification.TargetCategories);
        Assert.Equal(2, specification.MaximumDepth);
        Assert.Equal(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer), specification.Safety.AllowedInteractionCategories);
        Assert.Equal(TypeLevelCompletionRequirement.ExhaustiveWithinScope, specification.Completion);
        Assert.Equal("Settings", specification.Entry.ApplicationIdentity);
        Assert.Equal("Root", specification.Entry.ExpectedSemanticEntry);

        var propertyTypes = typeof(TypeLevelTraversalSpecification).GetProperties().Select(property => property.PropertyType).ToArray();
        var forbiddenSurfaceTypes = new[]
        {
            typeof(Plan),
            typeof(PlanStep),
            typeof(Observation),
            typeof(BranchInventoryEvidence),
            typeof(BranchProgressEvidence),
            typeof(GoalEvidence),
            typeof(CandidateAuthorizationEvidence),
            typeof(TargetGroundingEvidence),
            typeof(TargetGroundingCriterion),
        };
        Assert.DoesNotContain(propertyTypes, forbiddenSurfaceTypes.Contains);
        Assert.Equal(
            new[] { "Scope", "TargetCategories", "MaximumDepth", "Safety", "Completion", "Entry", "DispatchPolicy" },
            typeof(TypeLevelTraversalSpecification).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain(typeof(TypeLevelTraversalSpecification).GetProperties(), property => property.SetMethod is not null);
    }

    [Fact]
    public void FsB_DifferentObservedWorlds_DoNotMutateOrRedefineTheSpecification()
    {
        var specification = Create();
        var before = specification;
        var firstWorld = new Observation(
            ImmutableArray.Create(new ObservedElement("Network", null, 0), new ObservedElement("Display", null, 1)),
            "Settings",
            1);
        var secondWorld = new Observation(
            ImmutableArray.Create(new ObservedElement("Privacy", null, 0), new ObservedElement("Accessibility", null, 1), new ObservedElement("System", null, 2)),
            "Settings",
            2);

        Assert.NotEqual(firstWorld.Elements, secondWorld.Elements);
        Assert.Equal(before, specification);
        Assert.Equal("Root", specification.Scope.SemanticRoot);
        Assert.Equal(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer), specification.TargetCategories);
    }

    [Fact]
    public void FsC_DepthIsAnAuthoritativeInput_NotRuntimeProgress()
    {
        var shallow = Create(maximumDepth: 1);
        var deep = Create(maximumDepth: 2);

        Assert.NotEqual(shallow, deep);
        Assert.Equal(1, shallow.MaximumDepth);
        Assert.Equal(2, deep.MaximumDepth);
        Assert.DoesNotContain(typeof(TypeLevelTraversalSpecification).GetProperties(), property => property.Name.Contains("Progress", StringComparison.Ordinal));
    }

    [Fact]
    public void FsD_SafetyIsAnAuthoritativeInput_BeforeAnyConcreteInventoryExists()
    {
        var navigableOnly = Create(safety: ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer));
        var navigableAndStateChanging = Create(safety: ImmutableHashSet.Create(
            TypeLevelElementCategory.NavigableContainer,
            TypeLevelElementCategory.StateChangingControl));

        Assert.NotEqual(navigableOnly, navigableAndStateChanging);
        Assert.Equal(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer), navigableOnly.Safety.AllowedInteractionCategories);
        Assert.Equal(
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer, TypeLevelElementCategory.StateChangingControl),
            navigableAndStateChanging.Safety.AllowedInteractionCategories);
    }

    [Fact]
    public void FsE_RequiredDimensionsCannotDisappearIntoDefaults()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TypeLevelTaskScope(" ", "Root"));
        Assert.ThrowsAny<ArgumentException>(() => new TypeLevelTaskScope("Settings", " "));
        Assert.Throws<ArgumentException>(() => new TypeLevelSafetyBoundary(ImmutableHashSet<TypeLevelElementCategory>.Empty));
        Assert.Throws<ArgumentException>(() => new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope("Settings", "Root"),
            ImmutableHashSet<TypeLevelElementCategory>.Empty,
            0,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("Settings", "Root")));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(maximumDepth: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(completion: (TypeLevelCompletionRequirement)0));
        Assert.ThrowsAny<ArgumentException>(() => new TypeLevelEntryBoundary("Settings", " "));
    }

    private static TypeLevelTraversalSpecification Create(
        int maximumDepth = 3,
        ImmutableHashSet<TypeLevelElementCategory>? safety = null,
        TypeLevelCompletionRequirement completion = TypeLevelCompletionRequirement.ExhaustiveWithinScope)
        => new(
            new TypeLevelTaskScope("Settings", "Root"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth,
            new TypeLevelSafetyBoundary(safety ?? ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            completion,
            new TypeLevelEntryBoundary("Settings", "Root"));
}
