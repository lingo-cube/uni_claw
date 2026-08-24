using System;
using System.Collections.Immutable;
using System.Linq;
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
/// SC-1: expected child reached → RuntimeDecision Continue. A bounded exploration
/// directive expects a child transition; the Fake U2 world shows the expected child
/// reached (trace: in-scope container inventory complete; belief SemanticPage non-null);
/// the reconciler classifies Continue. Execution authority is unchanged — the RunState
/// is the Agent's DFS result and the decision only records the outcome.
/// </summary>
public sealed class ReconciliationScenario1ContinueTests
{
    [Fact]
    public async Task ExpectedChildReached_ProducesContinueDecision_WhileAgentKeepsAuthority()
    {
        var fixture = U2OpenWorldSettingsFixture.Positive("recon-s1-run");
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
                observation => new GoalEvidence(
                    string.Equals(U2OpenWorldSettingsFixture.ResolveSemanticPage(observation), U2OpenWorldSettingsFixture.RootPage, StringComparison.Ordinal),
                    "Fresh root GoalEvidence is satisfied after bounded traversal completion.",
                    observation.SequenceNumber),
                U2OpenWorldSettingsFixture.EvaluateAuthorization,
                BranchInventoryEvaluator: U2OpenWorldSettingsFixture.EvaluateInventory));

        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(directive));

        var ledger = new ExecutionHypothesisLedger(resolved, "recon-s1-run");
        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "recon-s1-run", CancellationToken.None, ledger);

        // The expected child transition was observed in-scope: the trace records an
        // in-scope inventory complete inflection point, and the belief is understood.
        Assert.Contains(agent.Trace, entry => entry.Reason?.Contains(
            "open-world branch inventory complete", StringComparison.Ordinal) is true
            || entry.Reason?.Contains("open-world container inventory complete", StringComparison.Ordinal) is true);
        Assert.NotNull(agent.Belief);
        Assert.False(string.IsNullOrWhiteSpace(agent.Belief!.SemanticPage));

        // The reconciler classifies Continue: hypothesis consistent with the observed world.
        var decision = ledger.LatestDecision;
        Assert.NotNull(decision);
        Assert.Equal(RuntimeDecisionState.Continue, decision!.State);
        Assert.Equal("recon-s1-run", decision.RunId);

        // Execution authority unchanged: the RunState is the Agent's DFS result; the
        // decision records the outcome but never determines it.
        Assert.Equal(RunState.Completed, state);
        Assert.Equal(agent.State, state);
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, ledger.Current.Status);
    }
}