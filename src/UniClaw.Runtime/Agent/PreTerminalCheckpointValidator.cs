using UniClaw.Runtime.Model;

#pragma warning disable CS1591

namespace UniClaw.Runtime.Agent;

public enum PreTerminalCheckpointRejection
{
    None = 0,
    NotRunning,
    RunMismatch,
    CycleMismatch,
    DuplicateCycle,
    ObservationMismatch,
    BeliefMismatch,
    DfsProgressMismatch,
    TraceMismatch,
    RevisionMismatch,
    DeadlineExpired,
    Cancelled,
    InvalidProposal,
    Terminal,
}

public sealed record PreTerminalCheckpointState
{
    public PreTerminalCheckpointState(
        string runId,
        RunState runState,
        long cycleSequence,
        long acceptedObservationSequence,
        long beliefRevision,
        string beliefDigest,
        long dfsProgressRevision,
        string traceDigest,
        string acceptedReasoningRevisionReference,
        DateTimeOffset now,
        string executionBoundaryReference = "",
        string? strategyExecutionId = null,
        string? runtimeExecutionIntentReference = null,
        string? evidenceViewDigest = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(beliefDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedReasoningRevisionReference);
        RunId = runId;
        RunState = runState;
        CycleSequence = cycleSequence;
        AcceptedObservationSequence = acceptedObservationSequence;
        BeliefRevision = beliefRevision;
        BeliefDigest = beliefDigest;
        DfsProgressRevision = dfsProgressRevision;
        TraceDigest = traceDigest;
        AcceptedReasoningRevisionReference = acceptedReasoningRevisionReference;
        Now = now;
        ExecutionBoundaryReference = executionBoundaryReference;
        StrategyExecutionId = strategyExecutionId;
        RuntimeExecutionIntentReference = runtimeExecutionIntentReference;
        EvidenceViewDigest = evidenceViewDigest;
    }

    public string RunId { get; init; }
    public RunState RunState { get; init; }
    public long CycleSequence { get; init; }
    public long AcceptedObservationSequence { get; init; }
    public long BeliefRevision { get; init; }
    public string BeliefDigest { get; init; }
    public long DfsProgressRevision { get; init; }
    public string TraceDigest { get; init; }
    public string AcceptedReasoningRevisionReference { get; init; }
    public DateTimeOffset Now { get; init; }
    public string ExecutionBoundaryReference { get; init; }
    public string? StrategyExecutionId { get; init; }
    public string? RuntimeExecutionIntentReference { get; init; }
    public string? EvidenceViewDigest { get; init; }
}


public sealed record PreTerminalCheckpointValidationResult(
    bool Accepted,
    PreTerminalCheckpointRejection Rejection)
{
    public static PreTerminalCheckpointValidationResult Accept() => new(true, PreTerminalCheckpointRejection.None);
    public static PreTerminalCheckpointValidationResult Reject(PreTerminalCheckpointRejection reason) => new(false, reason);
}

/// <summary>Agent-owned correlation and authority validation for passive proposals.</summary>
public sealed class PreTerminalCheckpointValidator
{
    private readonly HashSet<(string RunId, long CycleSequence)> _closedCycles = [];
    private readonly object _cycleLock = new();

    public PreTerminalCheckpointValidationResult Validate(
        PreTerminalReasoningSnapshot snapshot,
        PreTerminalContinuationProposal proposal,
        PreTerminalCheckpointState current,
        bool cancelled = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(current);

        PreTerminalCheckpointValidationResult Reject(PreTerminalCheckpointRejection reason)
        {
            lock (_cycleLock) _closedCycles.Add((snapshot.RunId, snapshot.CycleSequence));
            return PreTerminalCheckpointValidationResult.Reject(reason);
        }

        if (current.RunState != RunState.Running) return Reject(PreTerminalCheckpointRejection.Terminal);
        if (cancelled) return Reject(PreTerminalCheckpointRejection.Cancelled);
        if (!string.Equals(snapshot.ContractVersion, PreTerminalReasoningSnapshot.CurrentContractVersion, StringComparison.Ordinal)
            || !string.Equals(snapshot.ExecutionBoundaryReference, current.ExecutionBoundaryReference, StringComparison.Ordinal))
            return Reject(PreTerminalCheckpointRejection.InvalidProposal);
        if (current.RunId != snapshot.RunId || proposal.RunId != current.RunId)
            return Reject(PreTerminalCheckpointRejection.RunMismatch);
        if (snapshot.CycleSequence != current.CycleSequence || proposal.CycleSequence != current.CycleSequence)
            return Reject(IsClosed(snapshot.RunId, snapshot.CycleSequence)
                ? PreTerminalCheckpointRejection.DuplicateCycle
                : PreTerminalCheckpointRejection.CycleMismatch);
        lock (_cycleLock)
        {
            if (!_closedCycles.Add((snapshot.RunId, snapshot.CycleSequence)))
                return PreTerminalCheckpointValidationResult.Reject(PreTerminalCheckpointRejection.DuplicateCycle);
        }
        if (snapshot.AcceptedObservationSequence != current.AcceptedObservationSequence
            || proposal.AcceptedObservationSequence != current.AcceptedObservationSequence)
            return Reject(PreTerminalCheckpointRejection.ObservationMismatch);
        if (snapshot.BeliefRevision != current.BeliefRevision
            || proposal.BeliefRevision != current.BeliefRevision
            || !string.Equals(snapshot.BeliefDigest, current.BeliefDigest, StringComparison.Ordinal))
            return Reject(PreTerminalCheckpointRejection.BeliefMismatch);
        if (snapshot.DfsProgressRevision != current.DfsProgressRevision)
            return Reject(PreTerminalCheckpointRejection.DfsProgressMismatch);
        if (!string.Equals(snapshot.TraceDigest, current.TraceDigest, StringComparison.Ordinal)
            || !string.Equals(proposal.TraceDigest, current.TraceDigest, StringComparison.Ordinal))
            return Reject(PreTerminalCheckpointRejection.TraceMismatch);
        var strategyMode = snapshot.StrategyExecutionId is not null || current.StrategyExecutionId is not null || proposal.StrategyExecutionId is not null;
        if (strategyMode && (snapshot.StrategyEvidence is null
            || !string.Equals(snapshot.StrategyEvidence.ContractVersion, StrategyExecutionEvidenceView.CurrentContractVersion, StringComparison.Ordinal)
            || !string.Equals(snapshot.StrategyEvidence.RunId, snapshot.RunId, StringComparison.Ordinal)
            || snapshot.StrategyEvidence.AcceptedObservationSequence != snapshot.AcceptedObservationSequence
            || snapshot.StrategyEvidence.BeliefRevision != snapshot.BeliefRevision
            || !string.Equals(snapshot.StrategyEvidence.BeliefDigest, snapshot.BeliefDigest, StringComparison.Ordinal)
            || snapshot.StrategyEvidence.StructuralProgressRevision != snapshot.DfsProgressRevision
            || !string.Equals(snapshot.StrategyEvidence.TraceDigest, snapshot.TraceDigest, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(snapshot.StrategyExecutionId)
            || string.IsNullOrWhiteSpace(snapshot.RuntimeExecutionIntentReference)
            || string.IsNullOrWhiteSpace(snapshot.EvidenceViewDigest)
            || string.IsNullOrWhiteSpace(proposal.StrategyExecutionId)
            || !string.Equals(snapshot.StrategyExecutionId, current.StrategyExecutionId, StringComparison.Ordinal)
            || !string.Equals(snapshot.StrategyExecutionId, proposal.StrategyExecutionId, StringComparison.Ordinal)
            || !string.Equals(snapshot.RuntimeExecutionIntentReference, current.RuntimeExecutionIntentReference, StringComparison.Ordinal)
            || !string.Equals(snapshot.RuntimeExecutionIntentReference, proposal.RuntimeExecutionIntentReference, StringComparison.Ordinal)
            || !string.Equals(snapshot.EvidenceViewDigest, current.EvidenceViewDigest, StringComparison.Ordinal)
            || !string.Equals(snapshot.EvidenceViewDigest, proposal.EvidenceViewDigest, StringComparison.Ordinal)))
            return Reject(PreTerminalCheckpointRejection.InvalidProposal);
        if (!string.Equals(snapshot.AcceptedReasoningRevisionReference, current.AcceptedReasoningRevisionReference, StringComparison.Ordinal)
            || !string.Equals(proposal.BaseReasoningRevisionReference, snapshot.AcceptedReasoningRevisionReference, StringComparison.Ordinal)
            || !string.Equals(proposal.BaseReasoningRevisionReference, current.AcceptedReasoningRevisionReference, StringComparison.Ordinal))
            return Reject(PreTerminalCheckpointRejection.RevisionMismatch);
        if (current.Now > snapshot.Deadline) return Reject(PreTerminalCheckpointRejection.DeadlineExpired);
        if (!Enum.IsDefined(proposal.Kind)) return Reject(PreTerminalCheckpointRejection.InvalidProposal);
        if (proposal.BaseReasoningRevisionReference == proposal.ProposedReasoningRevisionReference)
            return Reject(PreTerminalCheckpointRejection.InvalidProposal);

        return PreTerminalCheckpointValidationResult.Accept();
    }

    public void Close(string runId, long cycleSequence)
    {
        lock (_cycleLock) _closedCycles.Add((runId, cycleSequence));
    }

    public bool IsClosed(string runId, long cycleSequence)
    {
        lock (_cycleLock) return _closedCycles.Contains((runId, cycleSequence));
    }
}
