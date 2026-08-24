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
/// Adaptation SC-1: expected child reached → Decision Continue → Keep adaptation. The
/// Fake U2 world shows the expected child transition reached in-scope; the reconciler
/// classifies Continue; the ledger applies a Keep adaptation (hypothesis Confirmed). The
/// adaptation records only — execution authority is unchanged and the RunState is the
/// Agent's DFS result.
/// </summary>
public sealed class AdaptationScenario1KeepTests
{
    [Fact]
    public async Task ExpectedChildReached_ProducesKeepAdaptation_WhileAgentKeepsAuthority()
    {
        var fixture = U2OpenWorldSettingsFixture.Positive("adapt-s1-run");
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

        var ledger = new ExecutionHypothesisLedger(resolved, "adapt-s1-run");
        var state = await DirectiveExecution.RunDirectiveAsync(
            agent, resolved, "adapt-s1-run", CancellationToken.None, ledger);

        // The expected child transition was observed in-scope (trace inflection) with an
        // understood belief — the decision is Continue.
        Assert.Contains(agent.Trace, entry => entry.Reason?.Contains(
            "open-world branch inventory complete", StringComparison.Ordinal) is true
            || entry.Reason?.Contains("open-world container inventory complete", StringComparison.Ordinal) is true);
        Assert.NotNull(agent.Belief);

        var decision = ledger.LatestDecision;
        Assert.NotNull(decision);
        Assert.Equal(RuntimeDecisionState.Continue, decision!.State);

        // The Continue decision drives a Keep adaptation: the hypothesis remains the
        // confirmed in-scope hypothesis — no new assumption, no replacement.
        var adaptation = ledger.LatestAdaptation;
        Assert.NotNull(adaptation);
        Assert.Equal(HypothesisAdaptationType.Keep, adaptation!.AdaptationType);
        Assert.Equal("adapt-s1-run", adaptation.RunId);
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, ledger.Current.Status);
        Assert.False(string.IsNullOrWhiteSpace(adaptation.AdaptationReason));

        // Execution authority unchanged: the RunState is the Agent's DFS result, never the
        // adaptation's; the adaptation only records the outcome.
        Assert.Equal(RunState.Completed, state);
        Assert.Equal(agent.State, state);
    }
}