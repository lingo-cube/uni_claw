using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// PHASE 3 + PHASE 5 — generic evidence-driven validation proofs.
///
/// These tests prove the Runtime's GENERIC capability using a scenario-neutral
/// world (Container A → B/C/D) plus the shared fixture semantic capability.
/// They assert evidence outcomes (coverage, goal evidence, terminal state,
/// authorization), never click sequences or navigation routes.
/// </summary>
public sealed class GenericEvidenceValidationTests
{
    private const string RunId = "generic-evidence-1";

    [Fact]
    public async Task GenericWorld_ExhaustiveCoverage_CompletesWithSatisfiedGoalEvidence()
    {
        var host = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        var result = await host.RunAndEvaluateAsync(runId: RunId);

        Assert.True(result.Passed, result.Summary);
        Assert.Equal(RunState.Completed, result.TerminalState);

        // Coverage: all four containers discovered and completed.
        Assert.Contains(GenericTreeWorld.Root, result.DiscoveredContainers);
        Assert.Contains(GenericTreeWorld.B, result.DiscoveredContainers);
        Assert.Contains(GenericTreeWorld.C, result.DiscoveredContainers);
        Assert.Contains(GenericTreeWorld.D, result.DiscoveredContainers);

        // Goal evidence satisfied by observation evidence.
        Assert.True(result.GoalEvidenceSatisfied);
        Assert.Contains(host.EvidenceReceipts, e => e.Satisfied);

        // The run ended on the root (belief consistency).
        Assert.Equal(GenericTreeWorld.Root, host.Agent?.Belief?.SemanticPage);
    }

    [Fact]
    public async Task GenericWorld_RootInventory_ProvesThreeAuthorizedChildren()
    {
        var host = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        var result = await host.RunAndEvaluateAsync(runId: RunId);
        Assert.True(result.Passed, result.Summary);

        // Authorization respected: every dispatch was authorized (the host's
        // authorization evaluator accepts candidates; the evidence contract
        // requires authorization before dispatch — trace reflects it).
        var agent = host.Agent!;
        Assert.DoesNotContain(agent.Trace, t =>
            t.Reason?.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) is true);

        // Container completeness evidence: root proves a complete 3-child inventory.
        var rootProgress = agent.BranchProgress[GenericTreeWorld.Root];
        Assert.Equal(3, rootProgress.ApprovedSiblingEvidence.Count);
        Assert.Equal(3, rootProgress.CompletedSiblingEvidence.Count);
    }

    [Fact]
    public async Task GenericWorld_DeterministicReplay_SameEvidence()
    {
        static string Key(EvidenceRuntimeHost h) => string.Join("|",
            (h.Agent?.Trace ?? []).Select(t => $"{t.RunState}:{t.ContainerId}:{t.Reason}"))
            + "::" + string.Join(",", (h.Environment?.ActionHistory ?? []).Select(a => a.GetType().Name));

        var first = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        await first.RunAndEvaluateAsync(runId: RunId);

        var second = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        await second.RunAndEvaluateAsync(runId: RunId);

        Assert.Equal(Key(first), Key(second));
        Assert.Equal(
            first.Agent?.BranchProgress.Select(p => p.Key).OrderBy(x => x, StringComparer.Ordinal),
            second.Agent?.BranchProgress.Select(p => p.Key).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GenericWorld_MissingChildEvidence_FailsClosed()
    {
        // A fixture where the root declares 3 children but only 2 are observable
        // (child D has no matching element on the root screen): inventory cannot
        // prove completeness → run must NOT complete with satisfied evidence.
        var incomplete = GenericTreeWorld.Create() with
        {
            Screens =
            [
                new EvidenceScreen(GenericTreeWorld.Root, IsLaunchTarget: true,
                    [
                        new EvidenceElement(GenericTreeWorld.B, TransitionTo: GenericTreeWorld.B),
                        new EvidenceElement(GenericTreeWorld.C, TransitionTo: GenericTreeWorld.C),
                        // D declared as a child relation but NO element on root screen:
                        new EvidenceElement(GenericTreeWorld.GoalSignalText),
                    ], ForegroundApplication: GenericTreeWorld.App),
                .. GenericTreeWorld.Create().Screens.Where(s => s.Identity is not GenericTreeWorld.Root)
            ],
        };

        var host = EvidenceRuntimeHost.Create(incomplete, GenericTreeWorld.Specification());
        var result = await host.RunAndEvaluateAsync(runId: RunId);

        // Incomplete evidence cannot satisfy completion (fail closed).
        Assert.False(result.GoalEvidenceSatisfied);
        Assert.NotEqual(RunState.Completed, result.TerminalState);
    }

    [Fact]
    public async Task GenericWorld_CorruptedSemanticEvidence_FailsClosed()
    {
        // All-text-navigation classifies EVERY element as a navigation candidate —
        // including the goal-signal text — but a classifier that marks everything
        // NonInteractive leaves no authorized navigation source: zero dispatch.
        var host = EvidenceRuntimeHost.Create(GenericTreeWorld.Create(), GenericTreeWorld.Specification());
        var result = await host.RunAndEvaluateAsync(
            classifier: _ => FixtureSemanticRole.NonInteractive,
            runId: RunId);

        Assert.NotEqual(RunState.Completed, result.TerminalState);
        // Zero navigation/state dispatch — only the Startup launch is legitimate.
        Assert.DoesNotContain(host.Environment?.ActionHistory ?? [], a => a is DeviceAction.Tap or DeviceAction.SetSwitch);
    }
}
