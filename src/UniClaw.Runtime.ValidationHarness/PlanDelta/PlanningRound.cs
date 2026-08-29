using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// One planning round's full record (spec "PlanDelta contract",
/// {PreviousPlan, ObservedResult, LoadedKnowledge, NewKnowledge,
/// RemainingUnknowns, PlanDelta, NextStrategy}; design D5 closed freedom
/// surface): the round index, the previous directive, the observed-result
/// summary (the round's citation universe), the loaded and newly-created
/// knowledge record ids, the honest remaining-unknown anchor ids, the validated
/// PlanDelta, and the next directive. Every round carries a NEW StrategyId —
/// the UniAgent idempotency identity, excluded from drift comparison. The
/// optional previous/next dispatch-policy summaries model the binding-created
/// dispatch policy, which <c>StrategyDirective</c> itself does not carry.
/// Validation artifact; no field ever enters the wire.
/// </summary>
public sealed record PlanningRound
{
    /// <summary>Create one immutable planning round.</summary>
    public PlanningRound(
        int roundIndex,
        StrategyDirective previousPlan,
        RoundEvidenceSummary observedResult,
        IReadOnlyList<string> loadedKnowledge,
        IReadOnlyList<string> newKnowledge,
        IReadOnlyList<string> remainingUnknowns,
        PlanDelta planDelta,
        StrategyDirective nextStrategy,
        DispatchPolicySummary? previousDispatchPolicySummary = null,
        DispatchPolicySummary? nextDispatchPolicySummary = null)
    {
        if (roundIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(roundIndex));
        ArgumentNullException.ThrowIfNull(previousPlan);
        ArgumentNullException.ThrowIfNull(observedResult);
        ArgumentNullException.ThrowIfNull(planDelta);
        ArgumentNullException.ThrowIfNull(nextStrategy);
        ArgumentNullException.ThrowIfNull(loadedKnowledge);
        ArgumentNullException.ThrowIfNull(newKnowledge);
        ArgumentNullException.ThrowIfNull(remainingUnknowns);
        if (loadedKnowledge.Any(string.IsNullOrWhiteSpace)
            || newKnowledge.Any(string.IsNullOrWhiteSpace)
            || remainingUnknowns.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Knowledge and unknown ids must be non-empty strings.");
        }

        RoundIndex = roundIndex;
        PreviousPlan = previousPlan;
        ObservedResult = observedResult;
        LoadedKnowledge = loadedKnowledge.ToImmutableArray();
        NewKnowledge = newKnowledge.ToImmutableArray();
        RemainingUnknowns = remainingUnknowns.ToImmutableArray();
        PlanDelta = planDelta;
        NextStrategy = nextStrategy;
        PreviousDispatchPolicySummary = previousDispatchPolicySummary;
        NextDispatchPolicySummary = nextDispatchPolicySummary;
    }

    /// <summary>Zero-based round index in campaign order.</summary>
    public int RoundIndex { get; }

    /// <summary>The directive that ran in the previous round.</summary>
    public StrategyDirective PreviousPlan { get; }

    /// <summary>Observed result summary (the round's evidence citation universe).</summary>
    public RoundEvidenceSummary ObservedResult { get; }

    /// <summary>Knowledge record ids loaded before this round (advisory input).</summary>
    public IReadOnlyList<string> LoadedKnowledge { get; }

    /// <summary>Knowledge record ids created from this round's observed result.</summary>
    public IReadOnlyList<string> NewKnowledge { get; }

    /// <summary>Honest remaining-unknown anchor ids after this round.</summary>
    public IReadOnlyList<string> RemainingUnknowns { get; }

    /// <summary>The round's plan revision (contract-validated).</summary>
    public PlanDelta PlanDelta { get; }

    /// <summary>The next directive (new StrategyId — the UniAgent idempotency identity).</summary>
    public StrategyDirective NextStrategy { get; }

    /// <summary>Previous round's dispatch policy summary, when declared (the DispatchPolicy freedom's evidence surface).</summary>
    public DispatchPolicySummary? PreviousDispatchPolicySummary { get; }

    /// <summary>Next round's dispatch policy summary, when declared.</summary>
    public DispatchPolicySummary? NextDispatchPolicySummary { get; }
}