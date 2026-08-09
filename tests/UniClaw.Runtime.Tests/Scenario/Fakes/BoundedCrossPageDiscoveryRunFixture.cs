using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>Scenario-specific SC-P3-CAND-008 Task 2.1 Runtime wiring.</summary>
internal sealed class BoundedCrossPageDiscoveryRunFixture
{
    private BoundedCrossPageDiscoveryRunFixture(
        BoundedCrossPageDiscoveryFixture world,
        RuntimeAgent agent,
        RuntimeTraversal traversal,
        Goal goal,
        Plan plan)
    {
        World = world;
        Agent = agent;
        Traversal = traversal;
        Goal = goal;
        Plan = plan;
    }

    internal BoundedCrossPageDiscoveryFixture World { get; }

    internal ScriptedEnvironment Environment => World.Environment;

    internal RuntimeAgent Agent { get; }

    internal RuntimeTraversal Traversal { get; }

    internal Goal Goal { get; }

    internal Plan Plan { get; }

    internal Task<RunState> RunAsync()
        => Agent.RunAsync(Goal, Plan, World.RunId, CancellationToken.None);

    internal static BoundedCrossPageDiscoveryRunFixture Create(
        BoundedCrossPageDiscoveryFixture world,
        Func<Observation, ObservedElement, CandidateAuthorizationEvidence>? authorizationEvaluator = null,
        Func<ImmutableArray<Observation>, ViewportExplorationEvidence>? viewportEvaluator = null,
        Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>? inventoryEvaluator = null,
        Func<Observation, GoalEvidence>? goalEvidenceEvaluator = null,
        Plan? plan = null)
    {
        var environment = world.Environment;
        var traversal = new RuntimeTraversal(environment);
        var baseGoal = world.Goal;
        var goal = new Goal(
            goalEvidenceEvaluator ?? baseGoal.EvidenceEvaluator,
            authorizationEvaluator ?? baseGoal.CandidateAuthorizationEvaluator,
            viewportEvaluator,
            inventoryEvaluator ?? baseGoal.BranchInventoryEvaluator);
        var startup = new RuntimeStartup(
            environment,
            "Settings",
            BoundedCrossPageDiscoveryFixture.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(
            environment,
            _ => ImmutableArray<DeviceAction>.Empty,
            (_, _) => null,
            (_, _) => true);
        RuntimeContainer ContainerFactory(string semanticPage) => new(
            semanticPage,
            observation => string.Equals(
                BoundedCrossPageDiscoveryFixture.ResolveSemanticPage(observation),
                semanticPage,
                StringComparison.Ordinal),
            traversal.ExecuteStep);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            BoundedCrossPageDiscoveryFixture.ResolveSemanticPage,
            ContainerFactory,
            recovery);
        return new BoundedCrossPageDiscoveryRunFixture(
            world,
            agent,
            traversal,
            goal,
            plan ?? world.InitialPlan);
    }
}
