using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Bounded-exploration directive representation: construction validation and the
/// fact that a <see cref="Directive"/> exposes only task-level declarations — no
/// Plan, no coordinates, no DeviceAction, no element index.
/// </summary>
public sealed class DirectiveTests
{
    [Fact]
    public void Constructor_ExposesExactlyTheDeclaredTaskLevelBoundaries()
    {
        var safety = DirectiveTestData.NavigableSafety();
        var dispatch = DirectiveTestData.NavigableEnterDispatch();
        var rules = DirectiveTestData.Rules();

        var directive = new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            maximumDepth: 4,
            safety,
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            rules,
            dispatch);

        Assert.Equal(new TypeLevelTaskScope("Settings", "SettingsRoot"), directive.Scope);
        Assert.Equal(new TypeLevelEntryBoundary("Settings", "SettingsRoot"), directive.Entry);
        Assert.Equal(4, directive.MaximumDepth);
        Assert.Equal(safety, directive.Safety);
        Assert.Equal(TypeLevelCompletionRequirement.ExhaustiveWithinScope, directive.Completion);
        Assert.Same(rules, directive.StrategyRules);
        Assert.Same(dispatch, directive.DispatchPolicy);
    }

    [Fact]
    public void UnsafeEmptyBoundary_IsRejectedBeforeAnyDirectiveIsCreated()
    {
        // An empty safety boundary is rejected at the boundary level itself, so a
        // Directive carrying an unsafe (empty) interaction boundary can never be
        // constructed — matching TypeLevelTraversalSpecification's guard.
        Assert.Throws<ArgumentException>(() =>
            new TypeLevelSafetyBoundary(ImmutableHashSet<TypeLevelElementCategory>.Empty));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-42)]
    public void Constructor_RejectsNegativeMaximumDepth(int maximumDepth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            maximumDepth,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            DirectiveTestData.Rules()));
    }

    [Fact]
    public void Constructor_RejectsNonExhaustiveCompletionRequirement()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            0,
            DirectiveTestData.NavigableSafety(),
            (TypeLevelCompletionRequirement)99,
            DirectiveTestData.Rules()));
    }

    [Fact]
    public void Constructor_RejectsMissingRequiredBoundaryInputs()
    {
        Assert.Throws<ArgumentNullException>(() => new Directive(
            null!,
            DirectiveTestData.Entry,
            0,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            DirectiveTestData.Rules()));
        Assert.Throws<ArgumentNullException>(() => new Directive(
            DirectiveTestData.Scope,
            null!,
            0,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            DirectiveTestData.Rules()));
        Assert.Throws<ArgumentNullException>(() => new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            0,
            null!,
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            DirectiveTestData.Rules()));
        Assert.Throws<ArgumentNullException>(() => new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            0,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            null!));
    }

    [Fact]
    public void Directive_ExposesNoPlanNoCoordinatesNoDeviceActionNoElementIndex()
    {
        var directive = DirectiveTestData.ValidDirective();

        // The Directive must expose NO Plan, no DeviceAction, no TraversalStepResult,
        // no ObservedElement, and no coordinate/bounds/element-index value.
        var forbiddenPropertyTypes = new[]
        {
            typeof(Plan),
            typeof(DeviceAction),
            typeof(TraversalStepResult),
            typeof(ObservedElement),
            typeof(ElementBounds),
        };
        var properties = typeof(Directive).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        Assert.Equal(
            new[] { "Scope", "Entry", "MaximumDepth", "Safety", "Completion", "StrategyRules", "DispatchPolicy" }
                .OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            properties.Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(properties, property => forbiddenPropertyTypes.Any(
            forbidden => forbidden.IsAssignableFrom(property.PropertyType)));
    }

    [Fact]
    public void StrategyRules_AreImmutableAndCarryOnlyDelegateRules()
    {
        var rules = DirectiveTestData.Rules(includeViewport: true, includeClassifier: true);

        var properties = typeof(DirectiveStrategyRules).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .ToArray();
        // Positional-record properties are get/init-only: no property may expose a
        // public, non-init set method (i.e. none is assignable after construction).
        Assert.DoesNotContain(properties, property =>
            property.SetMethod?.IsPublic is true && !IsInitOnly(property.SetMethod));
        Assert.Equal(
            new[] { "EvidenceEvaluator", "CandidateAuthorizationEvaluator", "BranchInventoryEvaluator", "ViewportExplorationEvaluator", "CategoryClassifier" }
                .OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            properties.Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    private static bool IsInitOnly(System.Reflection.MethodInfo setMethod)
        => setMethod.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

    [Fact]
    public void EqualDirectives_ProduceEqualDirectiveValues()
    {
        var first = DirectiveTestData.ValidDirective();
        var second = new Directive(
            DirectiveTestData.Scope,
            DirectiveTestData.Entry,
            first.MaximumDepth,
            DirectiveTestData.NavigableSafety(),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            DirectiveTestData.Rules());

        Assert.Equal(first.Scope, second.Scope);
        Assert.Equal(first.Entry, second.Entry);
        Assert.Equal(first.MaximumDepth, second.MaximumDepth);
        Assert.Equal(first.Safety, second.Safety);
    }
}
