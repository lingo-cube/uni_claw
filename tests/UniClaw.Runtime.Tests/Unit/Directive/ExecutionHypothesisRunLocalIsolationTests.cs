using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Run-local isolation: the ExecutionHypothesisLedger is a transient, method-local
/// derivation. It is never retained in an Agent / Container / Traversal / Environment
/// field (no Runtime state added to the Agent), and two separate runs produce
/// independent ledgers with no cross-contamination.
/// </summary>
public sealed class ExecutionHypothesisRunLocalIsolationTests
{
    [Fact]
    public void Agent_DeclaresNoExecutionHypothesisOrLedgerField()
    {
        var fields = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();

        // The ledger must not become Runtime state owned by the Agent (I-2: no new
        // mutable state owner). No Agent field may reference the ledger or hypothesis.
        Assert.DoesNotContain(fields,
            type => type == typeof(ExecutionHypothesisLedger)
                || type == typeof(ExecutionHypothesis));
    }

    [Fact]
    public void Agent_DeclaresNoHypothesisFieldByName()
    {
        var fieldNames = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.Name)
            .ToArray();

        Assert.DoesNotContain(fieldNames,
            name => name.Contains("Hypothesis", System.StringComparison.Ordinal));
    }

    [Fact]
    public void TwoSeparateLedgers_AreIndependentWithNoCrossContamination()
    {
        var firstResolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));
        var secondResolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));

        var first = new ExecutionHypothesisLedger(firstResolved, "run-A");
        var second = new ExecutionHypothesisLedger(secondResolved, "run-B");

        // Independent starting points.
        Assert.Equal("run-A", first.Current.RunId);
        Assert.Equal("run-B", second.Current.RunId);

        first.Activate();
        first.ReviseFromEvidence(
            new[]
            {
                new TraceEvent("run-A") { Reason = "EXTERNAL_BOUNDARY_OBSERVED: boundary" },
                new TraceEvent("run-A") { Reason = "verified parent return; child retained" },
            },
            RunState.Completed);

        // The second ledger is untouched by the first's revision.
        Assert.Equal(ExecutionHypothesisStatus.Created, second.Current.Status);
        Assert.Single(second.History);
        Assert.Equal("run-B", second.History[0].RunId);

        // The first ledger's history is fully self-contained (same run id throughout).
        Assert.All(first.History, entry => Assert.Equal("run-A", entry.RunId));
    }

    [Fact]
    public void EachRunGetsAFreshLedgerFromTheDecomposedDirective()
    {
        // The Planning entry does not cache a ledger; each invocation derives a fresh one.
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));

        var first = new ExecutionHypothesisLedger(resolved, "run-1");
        first.Activate();
        first.ReviseFromEvidence(
            new[] { new TraceEvent("run-1") { Reason = "EXTERNAL_BOUNDARY_OBSERVED: boundary" } },
            RunState.Failed);

        var second = new ExecutionHypothesisLedger(resolved, "run-2");
        Assert.Equal(ExecutionHypothesisStatus.Created, second.Current.Status);
        Assert.Single(second.History);
        Assert.Equal("run-2", second.History[0].RunId);
    }
}
