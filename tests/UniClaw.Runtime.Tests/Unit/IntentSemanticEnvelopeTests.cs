using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class IntentSemanticEnvelopeTests
{
    [Fact]
    public void Project_Resolved_PreservesExactGoalAndClosedWorldPlan()
    {
        var goal = CreateGoal();
        var plan = new Plan(ImmutableArray.Create(new PlanStep("Wi-Fi", "SetSwitch true")));
        var representation = new IntentExecutionRepresentation.ClosedWorldConcrete(plan);

        var envelope = IntentSemanticEnvelope.Project("Ensure Wi-Fi is on", goal, representation);

        var resolved = Assert.IsType<IntentSemanticEnvelope.Resolved>(envelope);
        Assert.Same(goal, resolved.Goal);
        Assert.Same(representation, resolved.Representation);
        Assert.Same(plan, Assert.IsType<IntentExecutionRepresentation.ClosedWorldConcrete>(resolved.Representation).Plan);
    }

    [Fact]
    public void Project_Resolved_PreservesExactOpenWorldSpecificationWithoutPlan()
    {
        var specification = CreateSpecification();
        var representation = new IntentExecutionRepresentation.OpenWorldTypeLevel(specification);

        var envelope = IntentSemanticEnvelope.Project("Traverse safe settings entries", CreateGoal(), representation);

        var resolved = Assert.IsType<IntentSemanticEnvelope.Resolved>(envelope);
        var openWorld = Assert.IsType<IntentExecutionRepresentation.OpenWorldTypeLevel>(resolved.Representation);
        Assert.Same(specification, openWorld.Specification);
        Assert.DoesNotContain(typeof(IntentExecutionRepresentation.OpenWorldTypeLevel).GetProperties(), property => property.PropertyType == typeof(Plan));
    }

    [Fact]
    public void Project_Insufficient_ContainsNoExecutableProjection()
    {
        var envelope = IntentSemanticEnvelope.Project("Handle Wi-Fi", "Desired state was not supplied.");

        var insufficient = Assert.IsType<IntentSemanticEnvelope.Insufficient>(envelope);
        Assert.Equal("Handle Wi-Fi", insufficient.Intent);
        Assert.Equal("Desired state was not supplied.", insufficient.Reason);
        Assert.Equal(new[] { "Intent", "Reason" }, typeof(IntentSemanticEnvelope.Insufficient).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void EqualAuthoritativeInputs_ProduceEqualDeterministicProjectionValues()
    {
        var goal = CreateGoal();
        var plan = new Plan(ImmutableArray.Create(new PlanStep("Wi-Fi", "SetSwitch true")));
        var representation = new IntentExecutionRepresentation.ClosedWorldConcrete(plan);

        Assert.Equal(
            IntentSemanticEnvelope.Project("Ensure Wi-Fi is on", goal, representation),
            IntentSemanticEnvelope.Project("Ensure Wi-Fi is on", goal, representation));
        Assert.Equal(
            IntentSemanticEnvelope.Project("Handle Wi-Fi", "Missing desired state."),
            IntentSemanticEnvelope.Project("Handle Wi-Fi", "Missing desired state."));
    }

    [Fact]
    public void ConstructorsAndProjection_RejectMissingRequiredInputs()
    {
        var goal = CreateGoal();
        var closedWorld = new IntentExecutionRepresentation.ClosedWorldConcrete(new Plan(ImmutableArray<PlanStep>.Empty));

        Assert.ThrowsAny<ArgumentException>(() => IntentSemanticEnvelope.Project(" ", goal, closedWorld));
        Assert.Throws<ArgumentNullException>(() => IntentSemanticEnvelope.Project("intent", null!, closedWorld));
        Assert.Throws<ArgumentNullException>(() => IntentSemanticEnvelope.Project("intent", goal, null!));
        Assert.ThrowsAny<ArgumentException>(() => IntentSemanticEnvelope.Project("intent", " "));
        Assert.Throws<ArgumentNullException>(() => new IntentExecutionRepresentation.ClosedWorldConcrete(null!));
        Assert.Throws<ArgumentNullException>(() => new IntentExecutionRepresentation.OpenWorldTypeLevel(null!));
    }

    [Fact]
    public void PublicSurface_HasExactlySixRecordTypes_SevenValues_TwoProjectOverloads_AndNoSettersOrEnums()
    {
        var envelopeTypes = new[]
        {
            typeof(IntentSemanticEnvelope),
            typeof(IntentSemanticEnvelope.Resolved),
            typeof(IntentSemanticEnvelope.Insufficient),
            typeof(IntentExecutionRepresentation),
            typeof(IntentExecutionRepresentation.ClosedWorldConcrete),
            typeof(IntentExecutionRepresentation.OpenWorldTypeLevel),
        };
        var variantTypes = envelopeTypes.Where(type => !type.IsAbstract).ToArray();

        Assert.All(envelopeTypes, type => Assert.NotNull(type.GetProperty("EqualityContract", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)));
        Assert.Equal(7, variantTypes.SelectMany(type => type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly)).Count());
        Assert.Equal(2, typeof(IntentSemanticEnvelope).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly).Count(method => method.Name == "Project"));
        Assert.DoesNotContain(envelopeTypes, type => type.IsEnum);
        Assert.DoesNotContain(variantTypes.SelectMany(type => type.GetProperties()), property => property.SetMethod is not null);
    }

    private static Goal CreateGoal()
        => new(_ => new GoalEvidence(false, "Unproven.", 0));

    private static TypeLevelTraversalSpecification CreateSpecification()
        => new(
            new TypeLevelTaskScope("Settings", "Root"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            2,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("Settings", "Root"));
}
