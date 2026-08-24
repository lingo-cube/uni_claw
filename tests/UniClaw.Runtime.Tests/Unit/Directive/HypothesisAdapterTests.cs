using System;
using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// HypothesisAdapter adaptation discipline: a stateless, pure, deterministic static
/// function mapping (RuntimeDecision, ExecutionHypothesis) → HypothesisAdaptation. Keep
/// (Continue → confirm), Replace (Revise → boundary-aware replacement, NO
/// SystemBack/DeviceAction/Tap), Escalate (Escalate → record inability, NO
/// recovery/retry). It contains no scenario strings, observes no world, and dispatches
/// no action.
/// </summary>
public sealed class HypothesisAdapterTests
{
    private static ExecutionHypothesis Hypothesis(
        ExecutionHypothesisStatus status = ExecutionHypothesisStatus.Active,
        string? revisionReason = null)
        => new(
            runId: "run-1",
            directiveReference: "Application/Root",
            objective: "Explore declared scope within bounded depth",
            expectedTransition: "Discover -> Authorize -> Expand",
            expectedOutcome: "Exhaustive coverage within declared scope",
            confidence: 0.8f,
            revisionReason: revisionReason,
            createdAtObservation: null,
            status: status);

    private static RuntimeDecision Decision(RuntimeDecisionState state, string reason)
        => new("run-1", state, "run-1", "in-scope progress", reason);

    // --- Keep ----------------------------------------------------------------------

    [Fact]
    public void Keep_WhenDecisionContinue_ConfirmsTheCurrentHypothesis()
    {
        var current = Hypothesis(ExecutionHypothesisStatus.Active);
        var adaptation = HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Continue, "In-scope progress confirms the hypothesis against the observed world."),
            current);

        Assert.Equal(HypothesisAdaptationType.Keep, adaptation.AdaptationType);
        Assert.Equal("run-1", adaptation.RunId);
        Assert.Equal("run-1", adaptation.DecisionReference);
        Assert.Equal(current.RunId, adaptation.PreviousHypothesisReference);

        // Adapted hypothesis = current with Status Confirmed; no new assumption.
        var adapted = adaptation.AdaptedHypothesis;
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, adapted.Status);
        Assert.Equal(current.Objective, adapted.Objective);
        Assert.Equal(current.ExpectedTransition, adapted.ExpectedTransition);
        Assert.Equal(current.ExpectedOutcome, adapted.ExpectedOutcome);
        Assert.Equal(current.Confidence, adapted.Confidence);
        Assert.Equal(current.DirectiveReference, adapted.DirectiveReference);
    }

    [Fact]
    public void Keep_WhenAlreadyConfirmed_KeepsTheHypothesisUnchanged()
    {
        var current = Hypothesis(ExecutionHypothesisStatus.Confirmed);
        var adaptation = HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Continue, "In-scope progress confirms the hypothesis against the observed world."),
            current);

        Assert.Equal(HypothesisAdaptationType.Keep, adaptation.AdaptationType);
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, adaptation.AdaptedHypothesis.Status);
        Assert.Equal(current, adaptation.AdaptedHypothesis);
    }

    [Fact]
    public void Keep_CreatesNoNewAssumptionAndNoAction()
    {
        var current = Hypothesis(ExecutionHypothesisStatus.Active);
        var adaptation = HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Continue, "In-scope progress confirms the hypothesis against the observed world."),
            current);

        // No new objective, no revision, no confidence change — only the status.
        Assert.Equal(current.Objective, adaptation.AdaptedHypothesis.Objective);
        Assert.Equal(current.Confidence, adaptation.AdaptedHypothesis.Confidence);
        Assert.Null(adaptation.AdaptedHypothesis.RevisionReason);
        Assert.Equal(ExecutionHypothesisStatus.Confirmed, adaptation.AdaptedHypothesis.Status);
    }

    // --- Replace -------------------------------------------------------------------

    [Fact]
    public void Replace_WhenDecisionRevise_SupersedesWithABoundaryAwareHypothesis()
    {
        var current = Hypothesis(ExecutionHypothesisStatus.Revised);
        var adaptation = HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Revise, "External boundary observation contradicts the in-scope hypothesis expectation."),
            current);

        Assert.Equal(HypothesisAdaptationType.Replace, adaptation.AdaptationType);
        Assert.Equal(current.RunId, adaptation.PreviousHypothesisReference);

        // The adapted hypothesis is a NEW Created hypothesis with a generic
        // boundary-aware objective — NOT a with-projection of the current, NOT a
        // SystemBack instruction, NOT a scenario string.
        var adapted = adaptation.AdaptedHypothesis;
        Assert.Equal(ExecutionHypothesisStatus.Created, adapted.Status);
        Assert.Equal("External boundary relation requires bounded return handling", adapted.Objective);
        Assert.Equal(current.RunId, adapted.RunId);
        Assert.Equal(current.DirectiveReference, adapted.DirectiveReference);
        Assert.Null(adapted.RevisionReason);
        Assert.NotEqual(current.Objective, adapted.Objective);
    }

    [Fact]
    public void Replace_ContainsNoSystemBackDeviceActionOrTap()
    {
        var adaptation = HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Revise, "External boundary observation contradicts the in-scope hypothesis expectation."),
            Hypothesis(ExecutionHypothesisStatus.Active));

        var text = AdaptationText(adaptation);
        Assert.DoesNotContain("SystemBack", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceAction", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tap", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Navigate", text, StringComparison.Ordinal);
    }

    // --- Escalate ------------------------------------------------------------------

    [Fact]
    public void Escalate_WhenDecisionEscalate_RecordsInabilityWithEscalationReason()
    {
        var current = Hypothesis(ExecutionHypothesisStatus.Active);
        var adaptation = HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Escalate, "Authority boundary exceeded: the run failed at an authority-boundary indicator."),
            current);

        Assert.Equal(HypothesisAdaptationType.Escalate, adaptation.AdaptationType);

        // Adapted hypothesis = current with Status Revised + escalation-marked reason.
        var adapted = adaptation.AdaptedHypothesis;
        Assert.Equal(ExecutionHypothesisStatus.Revised, adapted.Status);
        Assert.Equal(current.Objective, adapted.Objective);
        Assert.Contains("Escalation", adapted.RevisionReason, StringComparison.Ordinal);
        Assert.Contains("authority", adapted.RevisionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Escalation", adaptation.AdaptationReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Escalate_ContainsNoRecoveryRetryOrDispatch()
    {
        var adaptation = HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Escalate, "Authority boundary exceeded: the run failed at an authority-boundary indicator."),
            Hypothesis(ExecutionHypothesisStatus.Active));

        var text = AdaptationText(adaptation);
        Assert.DoesNotContain("Recovery", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Retry", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatch", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemBack", text, StringComparison.Ordinal);
    }

    // --- Determinism / statelessness / no scenario strings -------------------------

    [Fact]
    public void Adapt_IsDeterministic_IdenticalInputsProduceIdenticalAdaptations()
    {
        var current = Hypothesis(ExecutionHypothesisStatus.Active);
        var decision = Decision(RuntimeDecisionState.Revise, "External boundary observation contradicts the in-scope hypothesis expectation.");

        var first = HypothesisAdapter.Adapt(decision, current);
        var second = HypothesisAdapter.Adapt(decision, current);

        Assert.Equal(first, second);
        Assert.Equal(first.AdaptationReason, second.AdaptationReason);
        Assert.Equal(first.AdaptedHypothesis, second.AdaptedHypothesis);
    }

    [Fact]
    public void Adapt_HoldsNoState_NoStaticFields()
    {
        // The pure static function keeps no static state of any kind.
        Assert.Empty(typeof(HypothesisAdapter).GetFields(
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void Adapt_RejectsNullInputs()
    {
        Assert.ThrowsAny<ArgumentException>(() => HypothesisAdapter.Adapt(null!, Hypothesis()));
        Assert.ThrowsAny<ArgumentException>(() => HypothesisAdapter.Adapt(
            Decision(RuntimeDecisionState.Continue, "reason"), null!));
    }

    [Fact]
    public void Adapt_RejectsUndefinedDecisionState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HypothesisAdapter.Adapt(
            Decision((RuntimeDecisionState)99, "reason"), Hypothesis()));
    }

    [Theory]
    [InlineData(RuntimeDecisionState.Continue)]
    [InlineData(RuntimeDecisionState.Revise)]
    [InlineData(RuntimeDecisionState.Escalate)]
    public void Adaptation_ContainsNoScenarioStrings(RuntimeDecisionState state)
    {
        var adaptation = state switch
        {
            RuntimeDecisionState.Continue => HypothesisAdapter.Adapt(
                Decision(RuntimeDecisionState.Continue, "In-scope progress confirms the hypothesis against the observed world."),
                Hypothesis(ExecutionHypothesisStatus.Active)),
            RuntimeDecisionState.Revise => HypothesisAdapter.Adapt(
                Decision(RuntimeDecisionState.Revise, "External boundary observation contradicts the in-scope hypothesis expectation."),
                Hypothesis(ExecutionHypothesisStatus.Revised)),
            _ => HypothesisAdapter.Adapt(
                Decision(RuntimeDecisionState.Escalate, "Authority boundary exceeded: the run failed at an authority-boundary indicator."),
                Hypothesis(ExecutionHypothesisStatus.Active)),
        };

        var text = AdaptationText(adaptation);
        Assert.DoesNotContain("Settings", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Location", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Battery", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DeveloperOptions", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Wi-Fi", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Safe section", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Factory reset", text, StringComparison.Ordinal);
    }

    /// <summary>Collects every string the adapter produced (adaptation + adapted hypothesis).</summary>
    private static string AdaptationText(HypothesisAdaptation adaptation)
    {
        var hypothesis = adaptation.AdaptedHypothesis;
        return string.Join(" ", new[]
        {
            adaptation.AdaptationReason,
            hypothesis.Objective,
            hypothesis.ExpectedTransition,
            hypothesis.ExpectedOutcome,
            hypothesis.RevisionReason ?? string.Empty,
        });
    }
}