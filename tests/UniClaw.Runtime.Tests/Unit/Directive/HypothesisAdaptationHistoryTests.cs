using System.Collections.Generic;
using System.Linq;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Hypothesis adaptation history preservation: Adapt() appends the adapted hypothesis to
/// the ledger's immutable History without rewriting or deleting prior entries, and the
/// full hypothesis sequence (initial → revised → replaced → adapted) remains observable
/// through the History property.
/// </summary>
public sealed class HypothesisAdaptationHistoryTests
{
    private static (ExecutionHypothesisLedger Ledger, DirectiveDecompositionResult.Resolved Resolved) CreateLedger()
    {
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));
        var ledger = new ExecutionHypothesisLedger(resolved, "run-1");
        return (ledger, resolved);
    }

    private static DecisionRecord BoundaryObserved(string runId)
        => new(runId) { Reason = "EXTERNAL_BOUNDARY_OBSERVED: SomeOwned -> External (owned=SomeOwner); obligation PENDING" };

    private static DecisionRecord InventoryComplete(string runId)
        => new(runId) { Reason = "open-world container inventory complete: sources=2, unresolved=0; discovery epoch FROZEN" };

    private static WorldBelief Known(string semanticPage = "RootPage")
        => new(semanticPage, 1f, "Fresh observed world.", 1);

    private static DecisionRecord AuthorityFailure(string runId)
        => new(runId)
        {
            Reason = "Open-world identity safety: ancestry cycle detected for branch identity 'X'; zero child dispatch.",
            RunState = RunState.Failed,
        };

    [Fact]
    public void Adapt_Keep_AppendsAConfirmedSnapshotWithoutRewritingHistory()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();
        ledger.ReviseFromEvidence(new[] { InventoryComplete("run-1") }, RunState.Completed);
        var beforeAdapt = ledger.History.ToArray();

        var decision = ledger.Reconcile(Known());
        Assert.Equal(RuntimeDecisionState.Continue, decision.State);

        var adaptation = ledger.Adapt();
        Assert.Equal(HypothesisAdaptationType.Keep, adaptation.AdaptationType);

        // prior entries are a value-equal prefix of the appended history (append-only).
        var afterAdapt = ledger.History.ToArray();
        Assert.Equal(beforeAdapt, afterAdapt.Take(beforeAdapt.Length));
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, afterAdapt[^1].Status);
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, ledger.Current.Status);
    }

    [Fact]
    public void Adapt_Replace_RecordsReplacedThenAppendsBoundaryAwareHypothesis()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();
        ledger.ReviseFromEvidence(new[] { BoundaryObserved("run-1") }, RunState.Completed);

        var decision = ledger.Reconcile(null);
        Assert.Equal(RuntimeDecisionState.Revise, decision.State);

        var beforeAdapt = ledger.History.ToArray();
        var adaptation = ledger.Adapt();
        Assert.Equal(HypothesisAdaptationType.Replace, adaptation.AdaptationType);

        // The superseded current is marked Replaced, then the boundary-aware hypothesis
        // (Status Created) is appended — both after the pre-existing prefix.
        var history = ledger.History.ToArray();
        Assert.Equal(beforeAdapt, history.Take(beforeAdapt.Length));
        Assert.Equal(ExecutionHypothesisStatus.Replaced, history[^2].Status);
        Assert.Equal(ExecutionHypothesisStatus.Created, history[^1].Status);
        Assert.Equal("External boundary relation requires bounded return handling", history[^1].Objective);
        Assert.Same(adaptation.AdaptedHypothesis, history[^1]);
        Assert.Equal(ExecutionHypothesisStatus.Created, ledger.Current.Status);
    }

    [Fact]
    public void Adapt_Escalate_AppendsInabilityRecordedAsRevisedWithoutRewriting()
    {
        var (ledger, _) = CreateLedger();
        ledger.Activate();
        ledger.ReviseFromEvidence(new[] { AuthorityFailure("run-1") }, RunState.Failed);

        var decision = ledger.Reconcile(null);
        Assert.Equal(RuntimeDecisionState.Escalate, decision.State);

        var beforeAdapt = ledger.History.ToArray();
        var adaptation = ledger.Adapt();
        Assert.Equal(HypothesisAdaptationType.Escalate, adaptation.AdaptationType);

        var history = ledger.History.ToArray();
        Assert.Equal(beforeAdapt, history.Take(beforeAdapt.Length));
        Assert.Equal(ExecutionHypothesisStatus.Revised, history[^1].Status);
        Assert.Contains("Escalation", history[^1].RevisionReason, System.StringComparison.Ordinal);
        Assert.Same(adaptation.AdaptedHypothesis, history[^1]);
        Assert.Equal(ExecutionHypothesisStatus.Revised, ledger.Current.Status);
    }

    [Fact]
    public void FullSequence_InitialRevisedReplacedAdapted_RemainsObservable()
    {
        var (ledger, _) = CreateLedger();

        // initial (Created)
        Assert.Equal(ExecutionHypothesisStatus.Created, ledger.History[0].Status);

        // activate → Active
        ledger.Activate();
        Assert.Equal(ExecutionHypothesisStatus.Active, ledger.History[^1].Status);

        // boundary contradiction → Revised
        ledger.ReviseFromEvidence(new[] { BoundaryObserved("run-1") }, RunState.Completed);
        Assert.Equal(ExecutionHypothesisStatus.Revised, ledger.History[^1].Status);
        Assert.Contains("EXTERNAL_BOUNDARY_OBSERVED", ledger.History[^1].RevisionReason, System.StringComparison.Ordinal);

        // decision → Replace adaptation → Replaced + adapted (Created)
        ledger.Reconcile(null);
        ledger.Adapt();

        var statuses = ledger.History.Select(entry => entry.Status).ToArray();
        Assert.Equal(
            new[]
            {
                ExecutionHypothesisStatus.Created,
                ExecutionHypothesisStatus.Active,
                ExecutionHypothesisStatus.Revised,
                ExecutionHypothesisStatus.Replaced,
                ExecutionHypothesisStatus.Created,
            },
            statuses);

        // The full sequence (initial → revised → replaced → adapted) is observable and
        // the whole history shares the same run identity.
        Assert.Equal("run-1", ledger.History[0].RunId);
        Assert.All(ledger.History, entry => Assert.Equal("run-1", entry.RunId));
        Assert.Equal("External boundary relation requires bounded return handling", ledger.Current.Objective);
    }

    [Fact]
    public void Adapt_WithoutReconcile_ThrowsAndChangesNothing()
    {
        var (ledger, _) = CreateLedger();
        var historyBefore = ledger.History.ToArray();

        Assert.Throws<System.InvalidOperationException>(() => ledger.Adapt());

        Assert.Equal(historyBefore, ledger.History);
        Assert.Null(ledger.LatestAdaptation);
    }
}