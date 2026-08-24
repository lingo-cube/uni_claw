namespace UniClaw.Runtime.Model;

/// <summary>
/// The bounded, decision-driven modification of the run-local execution hypothesis.
/// Keep means the current hypothesis remains valid. Replace means the current
/// hypothesis no longer explains reality and is superseded by a new boundary-aware
/// hypothesis. Escalate means the RuntimeAgent cannot adapt inside its current
/// authority and records its inability.
/// <remarks>The adaptation type <b>records</b> a hypothesis modification; it never
/// performs it. Replace does not execute SystemBack; Escalate does not recover.</remarks>
/// </summary>
public enum HypothesisAdaptationType
{
    /// <summary>The current hypothesis remains valid; the decision confirms it.</summary>
    Keep = 1,

    /// <summary>The current hypothesis no longer explains reality and is superseded by a new boundary-aware hypothesis.</summary>
    Replace = 2,

    /// <summary>The RuntimeAgent cannot adapt inside its current authority; inability is recorded. A passive record — not an action.</summary>
    Escalate = 3,
}

/// <summary>
/// One immutable, passive record of a bounded, decision-driven modification of the
/// execution hypothesis. It carries a run identity, an adaptation type, a reference to
/// the <see cref="RuntimeDecision"/> that drove the adaptation, a reference to the
/// previous (superseded) <see cref="ExecutionHypothesis"/>, the adapted
/// <see cref="ExecutionHypothesis"/> itself, and a generic adaptation reason.
/// <para>
/// It is analogous to <see cref="RuntimeDecision"/> and <see cref="ExecutionHypothesis"/>
/// — an observable record that drives no decision and holds no authority. It carries NO
/// Plan, NO DeviceAction, NO Tap instruction, NO UI element selection, NO Goal
/// modification, NO Traversal control, and NO execution / recovery / authorization
/// authority. It must not be consulted by the Agent for decisions, authorization,
/// completion, or execution.
/// </para>
/// </summary>
public sealed record HypothesisAdaptation
{
    /// <summary>Creates a validated hypothesis adaptation record.</summary>
    /// <exception cref="ArgumentException">A required field is blank, the type is undefined, or the adapted hypothesis is null.</exception>
    public HypothesisAdaptation(
        string runId,
        HypothesisAdaptationType adaptationType,
        string decisionReference,
        string previousHypothesisReference,
        ExecutionHypothesis adaptedHypothesis,
        string adaptationReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousHypothesisReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(adaptationReason);
        ArgumentNullException.ThrowIfNull(adaptedHypothesis);
        if (!Enum.IsDefined(adaptationType))
            throw new ArgumentOutOfRangeException(nameof(adaptationType));

        RunId = runId;
        AdaptationType = adaptationType;
        DecisionReference = decisionReference;
        PreviousHypothesisReference = previousHypothesisReference;
        AdaptedHypothesis = adaptedHypothesis;
        AdaptationReason = adaptationReason;
    }

    /// <summary>Run identity the adaptation belongs to.</summary>
    public string RunId { get; init; }

    /// <summary>The adaptation type (Keep / Replace / Escalate).</summary>
    public HypothesisAdaptationType AdaptationType { get; init; }

    /// <summary>Reference to the <see cref="RuntimeDecision"/> that drove this adaptation (its hypothesis reference).</summary>
    public string DecisionReference { get; init; }

    /// <summary>Reference to the previous (superseded) <see cref="ExecutionHypothesis"/> (its run identity).</summary>
    public string PreviousHypothesisReference { get; init; }

    /// <summary>The adapted <see cref="ExecutionHypothesis"/> (confirmed, boundary-aware replacement, or revised record).</summary>
    public ExecutionHypothesis AdaptedHypothesis { get; init; }

    /// <summary>Generic adaptation reason derived from the decision reason; never a scenario string.</summary>
    public string AdaptationReason { get; init; }
}