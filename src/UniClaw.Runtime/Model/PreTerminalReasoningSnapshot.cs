using System.Collections.ObjectModel;

#pragma warning disable CS1591

namespace UniClaw.Runtime.Model;

/// <summary>Immutable, Agent-owned evidence boundary for one pre-terminal evaluation.</summary>
public sealed record PreTerminalReasoningSnapshot
{
    public const string CurrentContractVersion = "pre-terminal-cycle.v1";

    public PreTerminalReasoningSnapshot(
        string contractVersion,
        string runId,
        long cycleSequence,
        long acceptedObservationSequence,
        long beliefRevision,
        string beliefDigest,
        long dfsProgressRevision,
        string traceCursorReference,
        string traceDigest,
        string acceptedReasoningRevisionReference,
        string executionBoundaryReference,
        DateTimeOffset deadline,
        IReadOnlyList<string>? traceReferences = null,
        StrategyExecutionEvidenceView? strategyEvidence = null,
        string? strategyExecutionId = null,
        string? runtimeExecutionIntentReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(beliefDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceCursorReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedReasoningRevisionReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionBoundaryReference);
        if (cycleSequence < 0) throw new ArgumentOutOfRangeException(nameof(cycleSequence));
        if (acceptedObservationSequence < 0) throw new ArgumentOutOfRangeException(nameof(acceptedObservationSequence));
        if (beliefRevision < 0) throw new ArgumentOutOfRangeException(nameof(beliefRevision));
        if (dfsProgressRevision < 0) throw new ArgumentOutOfRangeException(nameof(dfsProgressRevision));
        if (deadline == default) throw new ArgumentException("A deadline is required.", nameof(deadline));

        ContractVersion = contractVersion;
        RunId = runId;
        CycleSequence = cycleSequence;
        AcceptedObservationSequence = acceptedObservationSequence;
        BeliefRevision = beliefRevision;
        BeliefDigest = beliefDigest;
        DfsProgressRevision = dfsProgressRevision;
        TraceCursorReference = traceCursorReference;
        TraceDigest = traceDigest;
        AcceptedReasoningRevisionReference = acceptedReasoningRevisionReference;
        ExecutionBoundaryReference = executionBoundaryReference;
        Deadline = deadline;
        TraceReferences = new ReadOnlyCollection<string>(
            (traceReferences ?? Array.Empty<string>()).ToArray());
        StrategyEvidence = strategyEvidence;
        StrategyExecutionId = strategyExecutionId;
        RuntimeExecutionIntentReference = runtimeExecutionIntentReference;
        EvidenceViewDigest = strategyEvidence?.EvidenceViewDigest;
    }

    public string ContractVersion { get; }
    public string RunId { get; }
    public long CycleSequence { get; }
    public long AcceptedObservationSequence { get; }
    public long BeliefRevision { get; }
    public string BeliefDigest { get; }
    public long DfsProgressRevision { get; }
    public string TraceCursorReference { get; }
    public IReadOnlyList<string> TraceReferences { get; }
    public string TraceDigest { get; }
    public string AcceptedReasoningRevisionReference { get; }
    public string ExecutionBoundaryReference { get; }
    public DateTimeOffset Deadline { get; }
    public StrategyExecutionEvidenceView? StrategyEvidence { get; }
    public string? StrategyExecutionId { get; }
    public string? RuntimeExecutionIntentReference { get; }
    public string? EvidenceViewDigest { get; }
}
