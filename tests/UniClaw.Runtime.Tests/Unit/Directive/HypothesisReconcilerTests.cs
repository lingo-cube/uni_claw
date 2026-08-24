using System.Collections.Generic;
using System.Linq;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// HypothesisReconciler classification discipline: a stateless, pure, deterministic
/// function mapping (ExecutionHypothesis, WorldBelief?, Trace) → RuntimeDecision. It
/// classifies Continue / Revise / Escalate from generic trace reasons + belief state,
/// contains no scenario strings, observes no world, and dispatches no action.
/// </summary>
public sealed class HypothesisReconcilerTests
{
    private static ExecutionHypothesis Hypothesis(
        ExecutionHypothesisStatus status,
        string revisionReason = "In-scope hypothesis")
        => new(
            runId: "run-1",
            directiveReference: "Settings/SettingsRoot",
            objective: "Explore declared scope within bounded depth",
            expectedTransition: "Discover -> Authorize -> Expand",
            expectedOutcome: "Exhaustive coverage within declared scope",
            confidence: 0.8f,
            revisionReason: status == ExecutionHypothesisStatus.Revised ? "EXTERNAL_BOUNDARY_OBSERVED: boundary" : null,
            createdAtObservation: null,
            status: status);

    private static WorldBelief Known(string semanticPage = "RootPage")
        => new(semanticPage, 1f, "Fresh observed world.", 1);

    private static WorldBelief Unknown()
        => new(null, 0f, "No matching semantic resolution.", 1);

    private static TraceEvent Trace(string reason)
        => new("run-1") { Reason = reason };

    private static TraceEvent Failed(string reason)
        => new("run-1") { Reason = reason, RunState = RunState.Failed };

    private static IReadOnlyList<TraceEvent> ExhaustedAndVerifiedReturn() => new[]
    {
        Trace("open-world container inventory complete: sources=2, unresolved=0; discovery epoch FROZEN"),
        Trace("verified parent return; child 'Safe section A' progress retained (seq=5)"),
    };

    // --- Continue ------------------------------------------------------------------

    [Fact]
    public void Continue_WhenConfirmed_WithKnownBelief_AndInScopeProgress()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Confirmed),
            Known(),
            ExhaustedAndVerifiedReturn());

        Assert.Equal(RuntimeDecisionState.Continue, decision.State);
        Assert.Equal("run-1", decision.RunId);
        Assert.Equal("run-1", decision.HypothesisReference);
        Assert.False(string.IsNullOrWhiteSpace(decision.EvidenceReference));
        Assert.False(string.IsNullOrWhiteSpace(decision.DecisionReason));
    }

    [Fact]
    public void Continue_WhenActive_WithKnownBelief_AndInScopeProgress()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Known(),
            ExhaustedAndVerifiedReturn());

        Assert.Equal(RuntimeDecisionState.Continue, decision.State);
    }

    // --- Revise --------------------------------------------------------------------

    [Fact]
    public void Revise_WhenExternalBoundaryObserved_EvenWithConfirmedHypothesis()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Confirmed),
            Known(),
            new[]
            {
                Trace("open-world container inventory complete: sources=1, unresolved=0"),
                Trace("EXTERNAL_BOUNDARY_OBSERVED: SomeOwned -> External (owned=SomeOwner); obligation PENDING"),
            });

        Assert.Equal(RuntimeDecisionState.Revise, decision.State);
        Assert.Contains("boundary", decision.DecisionReason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Revise_WhenHypothesisStatusIsRevised()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Revised),
            Known(),
            ExhaustedAndVerifiedReturn());

        Assert.Equal(RuntimeDecisionState.Revise, decision.State);
    }

    [Fact]
    public void Revise_WhenWorldBeliefIsUnknown()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Unknown(),
            ExhaustedAndVerifiedReturn());

        Assert.Equal(RuntimeDecisionState.Revise, decision.State);
        Assert.Contains("unknown", decision.DecisionReason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Revise_WhenNoConfirmingInScopeProgress()
    {
        // Active hypothesis + known belief but the trace carries only a boundary-return
        // revision with no confirming in-scope inventory/verified-return evidence.
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Known(),
            new[] { Trace("EXTERNAL_BOUNDARY_OBSERVED: SomeOwned -> External (owned=SomeOwner)") });

        Assert.Equal(RuntimeDecisionState.Revise, decision.State);
    }

    // --- Escalate ------------------------------------------------------------------

    [Fact]
    public void Escalate_WhenRunFailed_WithIdentitySafetyBoundaryReason()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Known(),
            new[] { Failed("Open-world identity safety: ancestry cycle detected for branch identity 'X'；zero child dispatch。") });

        Assert.Equal(RuntimeDecisionState.Escalate, decision.State);
    }

    [Fact]
    public void Escalate_WhenRunFailed_WithDepthCutoffReason()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Known(),
            new[] { Failed("In-scope inventory requires traversal beyond declared depth=2; bounded cutoff is not exhaustion.") });

        Assert.Equal(RuntimeDecisionState.Escalate, decision.State);
    }

    [Fact]
    public void Escalate_WhenRunFailed_WithBoundaryNotHandledReason()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Known(),
            new[] { Failed("Authorized boundary source 'X' was not handled; fail closed.") });

        Assert.Equal(RuntimeDecisionState.Escalate, decision.State);
    }

    [Fact]
    public void Escalate_WhenHypothesisRevised_AndRunFailed()
    {
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Revised),
            Known(),
            new[] { Failed("EXTERNAL_BOUNDARY_OBSERVED: boundary; run failed") });

        Assert.Equal(RuntimeDecisionState.Escalate, decision.State);
    }

    [Fact]
    public void Escalate_HasNoAuthorityBoundaryIndicatorIsNotEscalateWhenRunDidNotFail()
    {
        // An identity-safety reason that did NOT accompany a Failed run is not a
        // terminal authority failure — it should not be masked as Escalate.
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Known(),
            new[] { Trace("Open-world identity safety: ancestry cycle detected; zero child dispatch.") });

        Assert.Equal(RuntimeDecisionState.Revise, decision.State);
    }

    // --- Determinism / purity / no scenario strings --------------------------------

    [Fact]
    public void Reconcile_IsDeterministic_IdenticalInputsProduceIdenticalDecisions()
    {
        var hypothesis = Hypothesis(ExecutionHypothesisStatus.Confirmed);
        var belief = Known();
        var trace = ExhaustedAndVerifiedReturn();

        var first = HypothesisReconciler.Reconcile(hypothesis, belief, trace);
        var second = HypothesisReconciler.Reconcile(hypothesis, belief, trace);

        Assert.Equal(first, second);
        Assert.Equal(first.DecisionReason, second.DecisionReason);
    }

    [Fact]
    public void Reconcile_PerformsNoObservationAndDispatchesNoAction_()
    {
        // The pure function takes only its inputs and returns a decision; it cannot
        // dispatch. Calling it twice returns the same shape with no side effects.
        var decision = HypothesisReconciler.Reconcile(
            Hypothesis(ExecutionHypothesisStatus.Active),
            Known(),
            ExhaustedAndVerifiedReturn());

        Assert.NotNull(decision);
        Assert.Empty(typeof(HypothesisReconciler).GetFields(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
    }

    [Theory]
    [InlineData(RuntimeDecisionState.Continue)]
    [InlineData(RuntimeDecisionState.Revise)]
    [InlineData(RuntimeDecisionState.Escalate)]
    public void DecisionReason_ContainsNoScenarioStrings(RuntimeDecisionState state)
    {
        var decision = state switch
        {
            RuntimeDecisionState.Continue => HypothesisReconciler.Reconcile(
                Hypothesis(ExecutionHypothesisStatus.Confirmed), Known(), ExhaustedAndVerifiedReturn()),
            RuntimeDecisionState.Revise => HypothesisReconciler.Reconcile(
                Hypothesis(ExecutionHypothesisStatus.Revised), Known(), ExhaustedAndVerifiedReturn()),
            _ => HypothesisReconciler.Reconcile(
                Hypothesis(ExecutionHypothesisStatus.Active), Known(),
                new[] { Failed("Open-world identity safety: cycle detected; zero child dispatch.") }),
        };

        var text = decision.DecisionReason + " " + decision.EvidenceReference;
        Assert.DoesNotContain("Settings", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Location", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Battery", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("DeveloperOptions", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Wi-Fi", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Safe section", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Factory reset", text, System.StringComparison.Ordinal);
    }
}
