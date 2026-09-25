namespace UniClaw.Agent.Dsh;

/// <summary>
/// F3 (authority wording): Product truth is the canonical owner records and
/// the RuntimeOutcome envelope. Product Trace is a Product-owned
/// <b>diagnostic projection</b> over that truth — it is not itself canonical
/// evidence. DSH trajectory and diagnostics are realization-owned diagnostic
/// projections. The observer only unifies the display of these two projection
/// families; it never participates in recovery, Run/World reconstruction, or
/// outcome authority.
/// </summary>
public enum ObserverEvidenceSource
{
    ProductTrace,
    DshTrajectory,
    DshDiagnostic,
}

/// <summary>
/// One observer timeline event. The optional correlation fields (F4) —
/// DecisionId, Generation, PolicyId — exist only to correlate timeline rows
/// with Product Session / Run / Decision / transport generation / Policy.
/// They carry no authority: the unified timeline is a read model over
/// Product-owned and realization-owned diagnostic projections.
/// </summary>
public sealed record ObserverEvent(
    DateTimeOffset At,
    ObserverEvidenceSource Source,
    string Stage,
    string Message,
    string? ProductSessionId = null,
    string? DshSessionId = null,
    string? RunId = null,
    string? DecisionId = null,
    long? Generation = null,
    string? PolicyId = null);

/// <summary>Input projection assembled from existing Product and realization
/// evidence. It is a read model and owns no mutable runtime state.</summary>
public sealed record ObserverProjectionInput(
    string Task,
    string Goal,
    string RunId,
    string ProductSessionId,
    string? DshSessionId,
    ModelConfiguration? Model,
    int ConsultationCount,
    string? CurrentDecision,
    string? CurrentPolicy,
    int PolicyApplications,
    string? LastEffect,
    string? Verification,
    string? Outcome,
    IReadOnlyList<ObserverEvent> Events);

/// <summary>Unified, time ordered read model for the human-facing console.</summary>
public sealed record UnifiedObserverProjection(
    string WorkspaceId,
    string Task,
    string Goal,
    string RunId,
    string ProductSessionId,
    string? DshSessionId,
    ModelConfiguration? Model,
    int ConsultationCount,
    string? CurrentDecision,
    string? CurrentPolicy,
    int PolicyApplications,
    string? LastEffect,
    string? Verification,
    string? Outcome,
    IReadOnlyList<ObserverEvent> Timeline)
{
    public const string DefaultWorkspaceId = "uniclaw-observer";

    public static UnifiedObserverProjection Build(ObserverProjectionInput input,
        string workspaceId = DefaultWorkspaceId)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(workspaceId))
            throw new ArgumentException("workspace id is required", nameof(workspaceId));
        return new(
            workspaceId,
            input.Task,
            input.Goal,
            input.RunId,
            input.ProductSessionId,
            input.DshSessionId,
            input.Model,
            input.ConsultationCount,
            input.CurrentDecision,
            input.CurrentPolicy,
            input.PolicyApplications,
            input.LastEffect,
            input.Verification,
            input.Outcome,
            (input.Events ?? Array.Empty<ObserverEvent>())
                .OrderBy(static e => e.At)
                .ThenBy(static e => e.Source)
                .ToArray());
    }
}

/// <summary>Fixed internal observer workspace. It exposes only a snapshot
/// reader; there are no commands, DSH mutators, or Product lifecycle methods.</summary>
public sealed class ObserverWorkspace
{
    private readonly Func<UnifiedObserverProjection> _read;

    public ObserverWorkspace(Func<UnifiedObserverProjection> read)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
    }

    public UnifiedObserverProjection ReadOnlySnapshot() => _read();
}
