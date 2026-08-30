using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Container;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Traversal;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Run-local isolation: the HypothesisAdaptation is per-run state of the transient,
/// method-local ExecutionHypothesisLedger. It is never retained in an Agent /
/// Container / Traversal / Environment field (no Runtime state added anywhere), and two
/// separate runs produce independent LatestAdaptation values with no cross-contamination.
/// </summary>
public sealed class HypothesisAdaptationRunLocalIsolationTests
{
    [Fact]
    public void Agent_DeclaresNoHypothesisAdaptationOrLedgerField()
    {
        var fields = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();

        // The adaptation must not become Runtime state owned by the Agent (I-2: no new
        // mutable state owner). No Agent field may reference the adaptation or ledger.
        Assert.DoesNotContain(fields,
            type => type == typeof(HypothesisAdaptation)
                || type == typeof(ExecutionHypothesisLedger));
    }

    [Fact]
    public void Agent_DeclaresNoAdaptationFieldByName()
    {
        var fieldNames = typeof(RuntimeAgent)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.Name)
            .ToArray();

        Assert.DoesNotContain(fieldNames,
            name => name.Contains("Adaptation", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ContainerAndTraversal_DeclareNoHypothesisAdaptationField()
    {
        var containerFields = typeof(RuntimeContainer)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();
        var traversalFields = typeof(RuntimeTraversal)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType)
            .ToArray();

        Assert.DoesNotContain(containerFields, type => type == typeof(HypothesisAdaptation));
        Assert.DoesNotContain(traversalFields, type => type == typeof(HypothesisAdaptation));
    }

    [Fact]
    public void TwoSeparateLedgers_ProduceIndependentLatestAdaptations()
    {
        var firstResolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));
        var secondResolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));

        var first = new ExecutionHypothesisLedger(firstResolved, "run-A");
        first.Activate();
        first.ReviseFromEvidence(
            new[] { new DecisionRecord("run-A") { Reason = "open-world container inventory complete: sources=2, unresolved=0" } },
            RunState.Completed);
        first.Reconcile(new WorldBelief("RootPage", 1f, "Fresh observed world.", 1));
        first.Adapt();

        // The second ledger is untouched by the first's adaptation.
        var second = new ExecutionHypothesisLedger(secondResolved, "run-B");
        Assert.Null(second.LatestAdaptation);
        Assert.Single(second.History);
        Assert.Equal("run-B", second.History[0].RunId);

        // The first ledger's adaptation is fully self-contained for its run.
        Assert.NotNull(first.LatestAdaptation);
        Assert.Equal("run-A", first.LatestAdaptation!.RunId);
        Assert.Equal(HypothesisAdaptationType.Keep, first.LatestAdaptation.AdaptationType);
        Assert.All(first.History, entry => Assert.Equal("run-A", entry.RunId));
    }

    [Fact]
    public void FreshLedger_HasNoAdaptationUntilAdaptIsCalled()
    {
        var resolved = Assert.IsType<DirectiveDecompositionResult.Resolved>(
            DirectiveDecomposer.Decompose(DirectiveTestData.ValidDirective()));

        var ledger = new ExecutionHypothesisLedger(resolved, "run-1");
        Assert.Null(ledger.LatestAdaptation);
    }
}