namespace UniClaw.Runtime.Model;

/// <summary>
/// Run-local execution hypothesis lifecycle. It records only the lifecycle of one
/// execution assumption — it never authorizes, decides, completes, or executes.
/// Transitions: Created → Active → Confirmed | Revised → Replaced.
/// <remarks>a hypothesis records an assumption + its lifecycle status; it drives no decision.</remarks>
/// </summary>
public enum ExecutionHypothesisStatus
{
    /// <summary>Hypothesis created from the directive boundaries; execution has not begun.</summary>
    Created = 1,

    /// <summary>Execution has begun under this hypothesis.</summary>
    Active = 2,

    /// <summary>An observation matched the hypothesis's expected transition.</summary>
    Confirmed = 3,

    /// <summary>An observation contradicted the hypothesis's expectation; a revision reason is recorded.</summary>
    Revised = 4,

    /// <summary>A revised hypothesis was superseded by a new hypothesis for the next execution phase.</summary>
    Replaced = 5,
}

/// <summary>
/// One immutable, passive run-local execution assumption. It is analogous to
/// <see cref="TraceEvent"/> — an observable record that records an assumption and its
/// lifecycle status but drives no decision and holds no authority. It carries NO
/// <see cref="Plan"/>, no element coordinates, no <see cref="DeviceAction"/>, no element
/// index, no scenario strings, and no authorization / completion authority.
/// </summary>
public sealed record ExecutionHypothesis
{
    /// <summary>Creates a validated hypothesis.</summary>
    /// <exception cref="ArgumentException">A required assumption field is blank, confidence is outside [0, 1], or status is undefined.</exception>
    public ExecutionHypothesis(
        string runId,
        string directiveReference,
        string objective,
        string expectedTransition,
        string expectedOutcome,
        float confidence,
        string? revisionReason,
        long? createdAtObservation,
        ExecutionHypothesisStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(directiveReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTransition);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedOutcome);
        if (confidence is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be within [0, 1].");
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        RunId = runId;
        DirectiveReference = directiveReference;
        Objective = objective;
        ExpectedTransition = expectedTransition;
        ExpectedOutcome = expectedOutcome;
        Confidence = confidence;
        RevisionReason = revisionReason;
        CreatedAtObservation = createdAtObservation;
        Status = status;
    }

    /// <summary>Run identity the hypothesis belongs to.</summary>
    public string RunId { get; init; }

    /// <summary>Reference to the directive whose declared boundaries seeded this hypothesis.</summary>
    public string DirectiveReference { get; init; }

    /// <summary>Current execution assumption (objective), derived from directive boundaries.</summary>
    public string Objective { get; init; }

    /// <summary>The execution transition this hypothesis expects to observe.</summary>
    public string ExpectedTransition { get; init; }

    /// <summary>The outcome this hypothesis expects to observe.</summary>
    public string ExpectedOutcome { get; init; }

    /// <summary>Assumption confidence in [0, 1].</summary>
    public float Confidence { get; init; }

    /// <summary>Non-blank when the hypothesis was revised due to a contradicting observation.</summary>
    public string? RevisionReason { get; init; }

    /// <summary>The observation sequence at which this hypothesis was created (may be null before first observation).</summary>
    public long? CreatedAtObservation { get; init; }

    /// <summary>Hypothesis lifecycle status.</summary>
    public ExecutionHypothesisStatus Status { get; init; }

    /// <summary>
    /// Run-outcome predicate used by the run-local ledger to derive the final hypothesis
    /// status. It lives in <c>Model/</c> because the hypothesis records an assumption about
    /// the run outcome, and it keeps <see cref="RunState"/> member access confined to the
    /// Model/Agent boundary (I-2: RunState's sole owner is the Agent). Completes on a
    /// <see cref="RunState.Completed"/> outcome; any non-completing terminal outcome does not.
    /// </summary>
    internal static bool Completes(RunState outcome)
        => outcome == RunState.Completed;
}
