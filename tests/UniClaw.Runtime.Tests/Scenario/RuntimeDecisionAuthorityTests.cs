using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Container;
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

/// <summary>
/// No-authority-escalation proof for the runtime reconciliation decision. The
/// RuntimeDecision is a passive record and the HypothesisReconciler is a stateless pure
/// function: neither exposes a method that authorizes, decides, completes, executes,
/// dispatches, creates a container, or initiates a sub-run. The RunState is produced by
/// the Agent's existing DFS engine, the GoalEvidence is evaluated by the existing
/// injected evaluator, and Escalate is a record — the RuntimeAgent never performs an
/// escalation action.
/// </summary>
public sealed class RuntimeDecisionAuthorityTests
{
    [Fact]
    public void DecisionAndReconciler_ExposeNoAuthorizingOrDecidingMethod()
    {
        var forbiddenNames = new[]
        {
            "Authorize", "Decide", "Complete", "Execute", "Dispatch", "Evaluate",
            "CreateContainer", "SubRun", "StartRun", "Apply", "Mutate",
        };

        AssertNoForbiddenPublicInstanceMethods(typeof(RuntimeDecision), forbiddenNames);
        AssertNoForbiddenPublicStaticMethods(typeof(HypothesisReconciler), forbiddenNames);
    }

    [Fact]
    public void Decision_ExposesNoAuthorizationOrCompletionEvidence()
    {
        Assert.DoesNotContain(typeof(RuntimeDecision).GetProperties(),
            property => typeof(CandidateAuthorizationEvidence).IsAssignableFrom(property.PropertyType)
                || typeof(GoalEvidence).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public void Reconciler_ExposesOnlyThePureReconcileEntry()
    {
        var methods = typeof(HypothesisReconciler)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(new[] { "Reconcile" }, methods);
    }

    [Fact]
    public async Task RunState_IsProducedByTheDfsEngine_NotByTheDecision()
    {
        var (agent, resolved, ledger, fixture) = await BuildRunAsync();

        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "rd-axis-run", CancellationToken.None, ledger);

        // The RunState is exactly the Agent's own DFS result; the decision only records.
        Assert.Equal(RunState.Completed, state);
        Assert.Equal(RunState.Completed, agent.State);

        // Reconcile ran as part of the integration, producing a decision that reflects
        // the outcome but did not determine it. The decision's run identity is the
        // ledger's (hypothesis) run identity.
        var decision = ledger.LatestDecision;
        Assert.NotNull(decision);
        Assert.Equal(fixture.RunId, decision!.RunId);
        Assert.Equal(RuntimeDecisionState.Continue, decision.State);
    }

    [Fact]
    public async Task GoalEvidence_IsEvaluatedByTheExistingEvaluator_NotByTheDecision()
    {
        var goalSequences = new List<long>();
        var (agent, resolved, ledger, fixture) = await BuildRunAsync(goalSequences);

        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "rd-evidence-run", CancellationToken.None, ledger);

        // The injected evidence evaluator ran, so GoalEvidence is the existing
        // evaluator's result, never the decision's.
        Assert.NotEmpty(goalSequences);
        Assert.Equal(RunState.Completed, state);
        // The decision reflects the Completed outcome but did not determine it.
        Assert.Equal(RuntimeDecisionState.Continue, ledger.LatestDecision!.State);
        Assert.False(string.IsNullOrWhiteSpace(fixture.RunId));
    }

    [Fact]
    public void DecisionAndReconciler_ExposeNoDispatchContainerOrSubRunMethod()
    {
        AssertNoForbiddenPublicInstanceMethods(typeof(RuntimeDecision), new[]
        {
            "Dispatch", "CreateContainer", "EnterContainer", "SubRun", "RunChild",
            "StartTraversal", "Navigate", "ExecuteStep",
        });
        AssertNoForbiddenPublicStaticMethods(typeof(HypothesisReconciler), new[]
        {
            "Dispatch", "CreateContainer", "EnterContainer", "SubRun", "RunChild",
            "StartTraversal", "Navigate", "ExecuteStep",
        });
    }

    [Fact]
    public void Escalate_IsARecord_NotAnAction()
    {
        // Escalate is a state value on a passive record; there is no escalation
        // callback, dispatch, or action anywhere on the model or reconciler.
        Assert.Contains(Enum.GetValues<RuntimeDecisionState>(), value => value == RuntimeDecisionState.Escalate);
        AssertNoForbiddenPublicInstanceMethods(typeof(RuntimeDecision), new[]
        {
            "Escalate", "Raise", "Notify", "Callback", "Request",
        });
    }

    [Fact]
    public void AgentAuthorizationPath_DoesNotReferenceTheDecisionOrReconciler()
    {
        var agentFields = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.DoesNotContain(agentFields,
            type => type == typeof(RuntimeDecision)
                || type == typeof(ExecutionHypothesisLedger)
                || type == typeof(ExecutionHypothesis));
    }

    private static void AssertNoForbiddenPublicInstanceMethods(Type type, string[] forbiddenNames)
    {
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        foreach (var forbidden in forbiddenNames)
        {
            Assert.DoesNotContain(publicMethods,
                name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    private static void AssertNoForbiddenPublicStaticMethods(Type type, string[] forbiddenNames)
    {
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        foreach (var forbidden in forbiddenNames)
        {
            Assert.DoesNotContain(publicMethods,
                name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Builds a Fake U2 world agent + resolved decomposition + ledger, reusing the
    /// U2OpenWorldSettingsFixture. When <paramref name="goalSequences"/> is supplied, the
    /// evidence evaluator records the observation sequence it saw (proving the existing
    /// evaluator ran).
    /// </summary>
    private static async Task<(RuntimeAgent Agent, DirectiveDecompositionResult.Resolved, ExecutionHypothesisLedger, U2OpenWorldSettingsFixture)> BuildRunAsync(
        List<long>? goalSequences = null)
    {
        var fixture = U2OpenWorldSettingsFixture.Positive();
        var environment = fixture.Environment;
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", U2OpenWorldSettingsFixture.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            U2OpenWorldSettingsFixture.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);

        var directive = new Directive(
            new TypeLevelTaskScope("Settings", U2OpenWorldSettingsFixture.RootPage),
            new TypeLevelEntryBoundary("Settings", U2OpenWorldSettingsFixture.RootPage),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new DirectiveStrategyRules(
                observation =>
                {
                    goalSequences?.Add(observation.SequenceNumber);
                    return new GoalEvidence(
                        string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), U2OpenWorldSettingsFixture.RootPage, StringComparison.Ordinal),
                        "Fresh root GoalEvidence is satisfied only after Agent derives bounded traversal completion.",
                        observation.SequenceNumber);
                },
                U2OpenWorldSettingsFixture.EvaluateAuthorization,
                BranchInventoryEvaluator: U2OpenWorldSettingsFixture.EvaluateInventory));

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        var ledger = new ExecutionHypothesisLedger(resolved, fixture.RunId);
        return (agent, resolved, ledger, fixture);
    }
}
