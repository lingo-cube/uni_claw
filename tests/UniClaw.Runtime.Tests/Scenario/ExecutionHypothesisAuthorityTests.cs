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
/// No-authority-escalation proof for the execution hypothesis. The hypothesis and
/// ledger are passive records / a transient derivation: they expose no method that
/// authorizes an action, decides, completes, or executes, and they never bypass the
/// Agent. The RunState is produced by the Agent's existing DFS engine and the
/// GoalEvidence is evaluated by the existing injected evaluator — the hypothesis
/// status only reflects the outcome, it never determines it.
/// </summary>
public sealed class ExecutionHypothesisAuthorityTests
{
    [Fact]
    public void Hypothesis_ExposesNoAuthorizingOrDecidingMethod()
    {
        var forbiddenNames = new[]
        {
            "Authorize", "Decide", "Complete", "Execute", "Dispatch", "Evaluate",
            "CreateContainer", "SubRun", "StartRun", "Apply", "Mutate",
        };

        AssertNoForbiddenPublicMethods(typeof(ExecutionHypothesis), forbiddenNames);
        AssertNoForbiddenPublicMethods(typeof(ExecutionHypothesisLedger), forbiddenNames);
    }

    [Fact]
    public void HypothesisAndLedger_ExposeNoAuthorizationOrCompletionEvidence()
    {
        // Neither the passive record nor the ledger may produce authorization or
        // completion evidence; those belong to the Agent's injected evaluators.
        Assert.DoesNotContain(typeof(ExecutionHypothesis).GetProperties(),
            property => typeof(CandidateAuthorizationEvidence).IsAssignableFrom(property.PropertyType)
                || typeof(GoalEvidence).IsAssignableFrom(property.PropertyType));
        Assert.DoesNotContain(typeof(ExecutionHypothesisLedger).GetProperties(),
            property => typeof(CandidateAuthorizationEvidence).IsAssignableFrom(property.PropertyType)
                || typeof(GoalEvidence).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public async Task RunState_IsProducedByTheDfsEngine_NotByTheHypothesis()
    {
        // Fake-env end-to-end: run with a hypothesis ledger and assert the RunState is
        // exactly the DFS engine's result (Completed for the positive U2 world). The
        // ledger only records; it never decides the outcome.
        var (agent, resolved, ledger, _) = await BuildRunAsync();

        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "hyp-axis-run", CancellationToken.None, ledger);

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(RunState.Completed, agent.State);
        // The final hypothesis status reflects the Completed outcome but the RunState
        // came from the Agent's own traversal completion, not from the ledger.
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, ledger.Current.Status);
    }

    [Fact]
    public async Task GoalEvidence_IsEvaluatedByTheExistingEvaluator_NotByTheHypothesis()
    {
        var goalSequences = new List<long>();
        var (agent, resolved, ledger, fixture) = await BuildRunAsync(goalSequences);

        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "hyp-evidence-run", CancellationToken.None, ledger);

        // The injected evidence evaluator ran (updates state + produced evidence),
        // so GoalEvidence is the existing evaluator's result, never the hypothesis's.
        Assert.NotEmpty(goalSequences);
        Assert.Equal(RunState.Completed, state);
        // The hypothesis status reflects the outcome but did not determine it.
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, ledger.History[^1].Status);
        Assert.False(string.IsNullOrWhiteSpace(fixture.RunId));
    }

    [Fact]
    public void Ledger_ExposesNoDispatchOrContainerOrSubRunMethod()
    {
        AssertNoForbiddenPublicMethods(typeof(ExecutionHypothesisLedger), new[]
        {
            "Dispatch", "CreateContainer", "EnterContainer", "SubRun", "RunChild",
            "StartTraversal", "Navigate", "ExecuteStep",
        });
    }

    [Fact]
    public void AgentAuthorizationPath_DoesNotReferenceTheHypothesis()
    {
        // The Agent's own source declares no hypothesis type/field (see also the
        // run-local isolation test); authorization stays entirely in the Agent.
        var agentFields = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();
        Assert.DoesNotContain(agentFields,
            type => type == typeof(ExecutionHypothesisLedger)
                || type == typeof(ExecutionHypothesis));
    }

    private static void AssertNoForbiddenPublicMethods(Type type, string[] forbiddenNames)
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
