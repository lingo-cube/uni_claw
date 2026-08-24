using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// PHASE 5 — evidence-based validation proofs across scenarios.
///
/// 1. The SAME generic Runtime capability works with different scenario worlds
///    (tree + diamond) and produces equivalent evaluation semantics.
/// 2. Removing scenario knowledge (a world with no goal signal) fails closed.
/// 3. Incorrect evidence (all-NonInteractive) fails closed.
/// 4. Incomplete evidence cannot satisfy completion.
/// </summary>
public sealed class CrossScenarioEvidenceValidationTests
{
    [Fact]
    public async Task SameRuntime_DifferentTopologies_EquivalentEvaluationSemantics()
    {
        var treeHost = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        var treeResult = await treeHost.RunAndEvaluateAsync(runId: "cross-tree");

        var diamondHost = EvidenceRuntimeHost.Create(GenericDiamondWorld.Create(), GenericDiamondWorld.Specification());
        var diamondResult = await diamondHost.RunAndEvaluateAsync(runId: "cross-diamond");

        // Both scenarios: same evaluation semantics — completed, goal evidence
        // satisfied, full required coverage, belief consistent at root.
        Assert.True(treeResult.Passed, treeResult.Summary);
        Assert.True(diamondResult.Passed, diamondResult.Summary);
        Assert.Equal(RunState.Completed, treeResult.TerminalState);
        Assert.Equal(RunState.Completed, diamondResult.TerminalState);
        Assert.True(treeResult.GoalEvidenceSatisfied);
        Assert.True(diamondResult.GoalEvidenceSatisfied);

        // Coverage proportional to the declared world, not to a script.
        Assert.Equal(GenericTreeWorld.Specification().RequiredCoverage.Count, treeResult.CoveredContainers.Count);
        Assert.Equal(GenericDiamondWorld.Specification().RequiredCoverage.Count, diamondResult.CoveredContainers.Count);
        Assert.Contains(GenericDiamondWorld.X1, diamondResult.CoveredContainers); // grandchild discovered

        // Belief consistency at root for both.
        Assert.Equal(GenericTreeWorld.Root, treeHost.Agent?.Belief?.SemanticPage);
        Assert.Equal(GenericDiamondWorld.Root, diamondHost.Agent?.Belief?.SemanticPage);
    }

    [Fact]
    public async Task WorldWithoutGoalSignal_FailsClosed_NoCompletion()
    {
        // Removing scenario knowledge: a world where the goal signal element is
        // absent from every screen. Evidence can never be satisfied → fail closed.
        var noSignal = GenericTreeWorld.Create() with
        {
            GoalSignals = [],
        };

        var host = EvidenceRuntimeHost.Create(noSignal, GenericTreeWorld.Specification());
        var result = await host.RunAndEvaluateAsync(runId: "cross-nosignal");

        Assert.False(result.GoalEvidenceSatisfied);
        Assert.NotEqual(RunState.Completed, result.TerminalState);
    }

    [Fact]
    public async Task IncorrectEvidence_AllNonInteractive_ZeroDispatchFailsClosed()
    {
        var host = EvidenceRuntimeHost.Create(GenericDiamondWorld.Create(), GenericDiamondWorld.Specification());
        var result = await host.RunAndEvaluateAsync(
            classifier: _ => UniClaw.Runtime.Tests.Scenario.Fakes.FixtureSemanticRole.NonInteractive,
            runId: "cross-wrong");

        Assert.NotEqual(RunState.Completed, result.TerminalState);
        Assert.False(result.GoalEvidenceSatisfied);
    }

    [Fact]
    public async Task IncompleteEvidence_MissingBranch_CannotComplete()
    {
        // Declare 3 children in the specification but the world only exposes 2:
        // coverage can never be proven → run must not complete.
        var incompleteSpec = GenericTreeWorld.Specification() with
        {
            RequiredCoverage = GenericTreeWorld.Specification().RequiredCoverage.Add("GhostNode"),
        };

        var host = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), incompleteSpec);
        var result = await host.RunAndEvaluateAsync(runId: "cross-ghost");

        Assert.NotEqual(RunState.Completed, result.TerminalState);
        Assert.False(result.GoalEvidenceSatisfied);
    }
}
