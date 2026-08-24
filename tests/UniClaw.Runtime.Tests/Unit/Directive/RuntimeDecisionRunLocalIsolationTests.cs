using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Run-local isolation for the reconciliation decision: the ledger's LatestDecision is
/// per-run (a fresh ledger starts with a null LatestDecision, and two separate runs
/// reconcile independently with no cross-contamination). The ledger, the hypothesis, and
/// the decision are never retained in any Agent / Container / Traversal / Environment
/// field — no Runtime state is added to the Agent by this capability.
/// </summary>
public sealed class RuntimeDecisionRunLocalIsolationTests
{
    [Fact]
    public void FreshLedger_HasNullLatestDecision_UntilReconcileIsCalled()
    {
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));
        var ledger = new ExecutionHypothesisLedger(resolved, "run-A");

        Assert.Null(ledger.LatestDecision);

        ledger.Activate();
        var decision = ledger.Reconcile(new WorldBelief("RootPage", 1f, "Fresh world.", 1));

        Assert.NotNull(ledger.LatestDecision);
        Assert.Same(decision, ledger.LatestDecision);
        Assert.Equal("run-A", ledger.LatestDecision!.RunId);
    }

    [Fact]
    public void TwoSeparateLedgers_ReconcileIndependently_WithNoCrossContamination()
    {
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));

        var first = new ExecutionHypothesisLedger(resolved, "run-A");
        var second = new ExecutionHypothesisLedger(resolved, "run-B");

        first.Activate();
        first.ReviseFromEvidence(
            new[]
            {
                new TraceEvent("run-A") { Reason = "EXTERNAL_BOUNDARY_OBSERVED: boundary" },
                new TraceEvent("run-A") { Reason = "verified parent return; child retained" },
            },
            RunState.Completed);
        first.Reconcile(new WorldBelief("RootPage", 1f, "Fresh world.", 2));

        // The second ledger is untouched by the first's revision + reconciliation.
        Assert.Null(second.LatestDecision);
        Assert.Equal(ExecutionHypothesisStatus.Created, second.Current.Status);

        // The first ledger's decision is fully self-contained (same run id throughout).
        Assert.Equal("run-A", first.LatestDecision!.RunId);
        Assert.Equal("run-B", second.Current.RunId);
    }

    [Fact]
    public void Reconcile_OverwritesLatestDecision_ForTheSameRun()
    {
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));
        var ledger = new ExecutionHypothesisLedger(resolved, "run-A");
        ledger.Activate();

        var first = ledger.Reconcile(new WorldBelief("RootPage", 1f, "World 1.", 1));
        var second = ledger.Reconcile(new WorldBelief(null, 0f, "World 2 unknown.", 2));

        Assert.NotNull(ledger.LatestDecision);
        Assert.Same(second, ledger.LatestDecision);
        Assert.NotSame(first, ledger.LatestDecision);
    }

    [Fact]
    public void Agent_DeclaresNoLedgerHypothesisOrDecisionField()
    {
        var fields = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();

        // The ledger must not become Runtime state owned by the Agent (I-2: no new
        // mutable state owner). No Agent field may reference the ledger, hypothesis,
        // or runtime decision.
        Assert.DoesNotContain(fields,
            type => type == typeof(ExecutionHypothesisLedger)
                || type == typeof(ExecutionHypothesis)
                || type == typeof(RuntimeDecision));
    }

    [Fact]
    public void Agent_DeclaresNoLedgerOrDecisionFieldByName()
    {
        var fieldNames = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.Name)
            .ToArray();

        Assert.DoesNotContain(fieldNames,
            name => name.Contains("Ledger", System.StringComparison.Ordinal)
                || name.Contains("Hypothesis", System.StringComparison.Ordinal)
                || name.Contains("Decision", System.StringComparison.Ordinal));
    }
}
