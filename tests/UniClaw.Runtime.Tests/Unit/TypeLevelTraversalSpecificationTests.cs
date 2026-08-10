using System.Collections.Immutable;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class TypeLevelTraversalSpecificationTests
{
    [Fact]
    public void Constructor_PreservesAllSixDimensionsAsImmutableValues()
    {
        var specification = Create();

        Assert.Equal(new TypeLevelTaskScope("Settings", "SettingsRoot"), specification.Scope);
        Assert.Equal(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer), specification.TargetCategories);
        Assert.Equal(3, specification.MaximumDepth);
        Assert.Equal(new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)), specification.Safety);
        Assert.Equal(TypeLevelCompletionRequirement.ExhaustiveWithinScope, specification.Completion);
        Assert.Equal(new TypeLevelEntryBoundary("Settings", "SettingsRoot"), specification.Entry);
        Assert.Null(specification.TargetCategories.GetType().GetProperty("Count")?.SetMethod);
    }

    [Fact]
    public void EqualIndependentInputs_ProduceEqualSpecifications()
    {
        var first = Create();
        var second = Create();

        Assert.Equal(first, second);
        Assert.Equal(first.TargetCategories, second.TargetCategories);
        Assert.Equal(first.Safety, second.Safety);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-42)]
    public void Constructor_RejectsInvalidDepth(int maximumDepth)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Create(maximumDepth: maximumDepth));

    [Fact]
    public void Constructor_RejectsMissingOrEmptyRequiredDimensions()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TypeLevelTaskScope(" ", "root"));
        Assert.ThrowsAny<ArgumentException>(() => new TypeLevelTaskScope("Settings", " "));
        Assert.ThrowsAny<ArgumentException>(() => new TypeLevelEntryBoundary(" ", "entry"));
        Assert.ThrowsAny<ArgumentException>(() => new TypeLevelEntryBoundary("Settings", " "));
        Assert.Throws<ArgumentException>(() => new TypeLevelSafetyBoundary(ImmutableHashSet<TypeLevelElementCategory>.Empty));
        Assert.Throws<ArgumentException>(() => new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope("Settings", "root"),
            ImmutableHashSet<TypeLevelElementCategory>.Empty,
            0,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("Settings", "entry")));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(completion: (TypeLevelCompletionRequirement)0));
        Assert.Throws<ArgumentNullException>(() => new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope("Settings", "root"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            0,
            null!,
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("Settings", "entry")));
    }

    [Fact]
    public void Constructors_RejectCategoriesOutsideTheBoundedVocabulary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TypeLevelSafetyBoundary(
            ImmutableHashSet.Create((TypeLevelElementCategory)99)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope("Settings", "root"),
            ImmutableHashSet.Create((TypeLevelElementCategory)99),
            0,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("Settings", "entry")));
    }

    private static TypeLevelTraversalSpecification Create(
        int maximumDepth = 3,
        TypeLevelCompletionRequirement completion = TypeLevelCompletionRequirement.ExhaustiveWithinScope)
        => new(
            new TypeLevelTaskScope("Settings", "SettingsRoot"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            completion,
            new TypeLevelEntryBoundary("Settings", "SettingsRoot"));
}
