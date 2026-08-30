using System.Collections.Generic;
using System.Linq;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// ExecutionHypothesisLedger behavior: it is a run-local derivation that creates an
/// initial hypothesis (Status Created) from a decomposed directive with NO scenario
/// strings, activates it, revises it from trace inflection points (Confirm / Revise /
/// Replace) and the run outcome, and exposes an immutable History snapshot. It holds no
/// authority methods.
/// </summary>
public sealed class ExecutionHypothesisLedgerTests
{
    private static (ExecutionHypothesisLedger Ledger, DirectiveDecompositionResult.Resolved Resolved) CreateLedger()
    {
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));
        var ledger = new ExecutionHypothesisLedger(resolved, "run-1");
        return (ledger, resolved);
    }

    private static DecisionRecord BoundaryObserved(string runId)
        => new(runId) { Reason = "EXTERNAL_BOUNDARY_OBSERVED: Settings/SettingsRoot -> External (owned=Settings); obligation PENDING" };

    private static DecisionRecord VerifiedParentReturn(string runId)
        => new(runId) { Reason = "verified parent return; child 'Safe section A' progress retained (seq=5)" };

    private static DecisionRecord Exhausted(string runId)
        => new(runId) { Reason = "open-world container inventory complete: sources=2, unresolved=0; discovery epoch FROZEN" };

    private static DecisionRecord BoundedLeaf(string runId)
        => new(runId) { Reason = "open-world branch inventory bounded-leaf: depth=0, source-seq=3; no child required" };

    [Fact]
    public void Constructor_CreatesInitialHypothesisFromDirectiveBoundariesWithNoScenarioStrings()
    {
        var (ledger, _) = CreateLedger();

        var initial = ledger.Current;
        Assert.Equal(ExecutionHypothesisStatus.Created, initial.Status);
        Assert.Equal("run-1", initial.RunId);
        Assert.Equal("Settings/SettingsRoot", initial.DirectiveReference);
        Assert.Equal("Explore declared scope within bounded depth", initial.Objective);
        Assert.Equal("Discover -> Authorize -> Expand", initial.ExpectedTransition);
        Assert.Equal("Exhaustive coverage within declared scope", initial.ExpectedOutcome);

        // The initial hypothesis carries no scenario-specific strings.
        Assert.DoesNotContain("Safe section", initial.Objective, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Factory reset", initial.Objective, System.StringComparison.Ordinal);
        Assert.Null(initial.RevisionReason);

        // History begins with exactly this initial hypothesis.
        Assert.Single(ledger.History);
        Assert.Same(initial, ledger.History[0]);
    }

    [Fact]
    public void Activate_MarksTheCurrentHypothesisActive()
    {
        var (ledger, _) = CreateLedger();
        Assert.Equal(ExecutionHypothesisStatus.Created, ledger.Current.Status);

        ledger.Activate();

        Assert.Equal(ExecutionHypothesisStatus.Active, ledger.Current.Status);
        Assert.Equal(ExecutionHypothesisStatus.Active, ledger.History[^1].Status);
    }

    [Fact]
    public void ReviseFromEvidence_MapsBoundaryToRevisedThenContinuedToReplaced()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();

        var trace = new List<DecisionRecord>
        {
            Exhausted("run-1"),
            BoundaryObserved("run-1"),
            VerifiedParentReturn("run-1"),
            BoundedLeaf("run-1"),
        };

        ledger.ReviseFromEvidence(trace, RunState.Completed);

        // Created -> Active -> Confirmed (inventory) -> Revised (boundary) ->
        // Replaced -> Created (continue siblings) -> Confirmed (outcome)
        var history = ledger.History.Select(entry => entry.Status).ToArray();
        Assert.Equal(
            new[]
            {
                ExecutionHypothesisStatus.Created,
                ExecutionHypothesisStatus.Active,
                ExecutionHypothesisStatus.Confirmed,
                ExecutionHypothesisStatus.Revised,
                ExecutionHypothesisStatus.Replaced,
                ExecutionHypothesisStatus.Created,
                ExecutionHypothesisStatus.Confirmed,
            },
            history);

        // The revision records the boundary reason from the trace.
        var revised = ledger.History.Single(entry => entry.Status == ExecutionHypothesisStatus.Revised);
        Assert.Contains("EXTERNAL_BOUNDARY_OBSERVED", revised.RevisionReason, System.StringComparison.Ordinal);

        // The replaced hypothesis is superseded by a "continue siblings" Created one.
        var superseding = ledger.History.Single(entry =>
            entry.Status == ExecutionHypothesisStatus.Created
            && entry.Objective.Contains("Continue remaining siblings", System.StringComparison.Ordinal));
        Assert.NotNull(superseding);
    }

    [Fact]
    public void ReviseFromEvidence_ConfirmsOnMatchingInventoryEvidence()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();

        ledger.ReviseFromEvidence(new[] { Exhausted("run-1"), BoundedLeaf("run-1") }, RunState.Completed);

        // Created -> Active -> Confirmed (inventory) -> Confirmed (outcome)
        var history = ledger.History.Select(entry => entry.Status).ToArray();
        Assert.Equal(
            new[]
            {
                ExecutionHypothesisStatus.Created,
                ExecutionHypothesisStatus.Active,
                ExecutionHypothesisStatus.Confirmed,
            },
            history);
    }

    [Fact]
    public void ReviseFromEvidence_FinalStatusDerivedFromCompletedOutcome()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();

        // No contradicting or confirming inflection; the Completed outcome Confirms.
        ledger.ReviseFromEvidence(System.Array.Empty<DecisionRecord>(), RunState.Completed);

        var history = ledger.History.Select(entry => entry.Status).ToArray();
        Assert.Equal(
            new[]
            {
                ExecutionHypothesisStatus.Created,
                ExecutionHypothesisStatus.Active,
                ExecutionHypothesisStatus.Confirmed,
            },
            history);
    }

    [Fact]
    public void ReviseFromEvidence_FinalStatusDerivedFromFailedOutcome()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();

        ledger.ReviseFromEvidence(System.Array.Empty<DecisionRecord>(), RunState.Failed);

        // A Failed outcome never fabricates completion: the Active hypothesis is Revised.
        var history = ledger.History.Select(entry => entry.Status).ToArray();
        Assert.Equal(
            new[]
            {
                ExecutionHypothesisStatus.Created,
                ExecutionHypothesisStatus.Active,
                ExecutionHypothesisStatus.Revised,
            },
            history);
        Assert.False(string.IsNullOrWhiteSpace(ledger.History[^1].RevisionReason));
    }

    [Fact]
    public void History_IsAnImmutableSnapshotOfTheSequence()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();
        var snapshotBefore = ledger.History.ToArray();

        // Further revision does not mutate the earlier snapshot.
        ledger.ReviseFromEvidence(new[] { Exhausted("run-1") }, RunState.Completed);

        var snapshotAfter = ledger.History.ToArray();
        Assert.True(snapshotAfter.Length > snapshotBefore.Length);
        // The earlier snapshot is a prefix of the later one (append-only).
        Assert.Equal(snapshotBefore, snapshotAfter.Take(snapshotBefore.Length));
        Assert.Equal(ExecutionHypothesisStatus.Active, snapshotAfter[^2].Status);
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, snapshotAfter[^1].Status);
    }
}
