using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>SC-CP14-MVS-001 deterministic proof of the pure dual-mode semantic envelope.</summary>
public sealed class Cp14IntentSemanticEnvelopeScenarioTests
{
    [Fact]
    public async Task ScenarioA_SameIntentAndGoal_AlreadyOnAvoidsWork_WhileOffExecutesTheExactClosedWorldPlan()
    {
        var evidence = new List<GoalEvidence>();
        var goal = ScenarioGoals.EnableWifi(evidence);
        var plan = ScenarioPlans.WifiEnableSequence();
        var representation = new IntentExecutionRepresentation.ClosedWorldConcrete(plan);
        var envelope = IntentSemanticEnvelope.Project("Ensure Wi-Fi is on", goal, representation);
        var extractedPlan = Assert.IsType<IntentExecutionRepresentation.ClosedWorldConcrete>(envelope.Representation).Plan;
        var alreadyOn = CreateRuntime(ScriptedEnvironmentVariants.InitialGoalSatisfied());
        var off = CreateRuntime(ScriptedEnvironmentVariants.Happy());

        var alreadyOnState = await alreadyOn.Agent.RunAsync(envelope.Goal, extractedPlan, "cp14-already-on", CancellationToken.None);
        var offState = await off.Agent.RunAsync(envelope.Goal, extractedPlan, "cp14-off", CancellationToken.None);

        Assert.Same(plan, extractedPlan);
        Assert.Equal(RunState.Completed, alreadyOnState);
        Assert.DoesNotContain(alreadyOn.Environment.ActionHistory, action => action is DeviceAction.Tap or DeviceAction.SetSwitch);
        Assert.Equal(RunState.Completed, offState);
        Assert.Equal(2, off.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Equal(new DeviceAction.SetSwitch(1, true), Assert.Single(off.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>()));
        Assert.True(evidence[^1].Satisfied);
        Assert.Equal(5, evidence[^1].SourceObservationSequence);
    }

    [Fact]
    public void ScenarioB_OpenWorldProjection_PreservesSpecificationWithoutFabricatingConcreteWork()
    {
        var specification = CreateSpecification();

        var envelope = IntentSemanticEnvelope.Project(
            "Traverse safe Settings entries within the declared depth.",
            CreateGoal(),
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));

        var openWorld = Assert.IsType<IntentExecutionRepresentation.OpenWorldTypeLevel>(envelope.Representation);
        Assert.Same(specification, openWorld.Specification);
        Assert.DoesNotContain(typeof(IntentExecutionRepresentation.OpenWorldTypeLevel).GetProperties(), property => property.PropertyType == typeof(Plan));
        Assert.Equal(new[] { "Specification" }, typeof(IntentExecutionRepresentation.OpenWorldTypeLevel).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void ScenarioC_ExplicitClosedWorldRoute_IsNeitherRewrittenNorConvertedToOpenWorld()
    {
        var plan = new Plan(ImmutableArray.Create(new PlanStep("Wi-Fi", "SetSwitch true")));
        var envelope = IntentSemanticEnvelope.Project(
            "Ensure Wi-Fi is on",
            CreateGoal(),
            new IntentExecutionRepresentation.ClosedWorldConcrete(plan));

        var closedWorld = Assert.IsType<IntentExecutionRepresentation.ClosedWorldConcrete>(envelope.Representation);
        Assert.Same(plan, closedWorld.Plan);
        Assert.IsNotType<IntentExecutionRepresentation.OpenWorldTypeLevel>(envelope.Representation);
    }

    [Fact]
    public void ScenarioD_ExplicitInsufficiency_ExposesNoGoalOrExecutionRepresentation()
    {
        var envelope = IntentSemanticEnvelope.Project("Handle Wi-Fi", "Desired state and execution representation are absent.");

        var insufficient = Assert.IsType<IntentSemanticEnvelope.Insufficient>(envelope);
        Assert.Equal(new[] { "Intent", "Reason" }, typeof(IntentSemanticEnvelope.Insufficient).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain(typeof(IntentSemanticEnvelope.Insufficient).GetProperties(), property =>
            property.PropertyType == typeof(Goal)
            || property.PropertyType == typeof(Plan)
            || property.PropertyType == typeof(TypeLevelTraversalSpecification)
            || property.PropertyType == typeof(IntentExecutionRepresentation));
    }

    private static Goal CreateGoal()
        => new(_ => new GoalEvidence(false, "Goal remains evidence-owned.", 0));

    private static TypeLevelTraversalSpecification CreateSpecification()
        => new(
            new TypeLevelTaskScope("Settings", "Root"),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            2,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary("Settings", "Root"));

    private static (RuntimeAgent Agent, ScriptedEnvironment Environment) CreateRuntime(ScriptedEnvironment environment)
    {
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, ScenarioHarness.TargetApplication, ScenarioIdentity.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, observation => ScenarioIdentity.ResolveSemanticPage(observation) == page, traversal.ExecuteStep);
        return (
            new RuntimeAgent(startup, traversal, cancellationToken => environment.ObserveAsync(cancellationToken), ScenarioIdentity.ResolveSemanticPage, Factory, recovery),
            environment);
    }
}
