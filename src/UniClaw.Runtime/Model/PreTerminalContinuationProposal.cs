using System.Collections.ObjectModel;

#pragma warning disable CS1591

namespace UniClaw.Runtime.Model;

/// <summary>Passive, Agent-facing result of one RuntimeAgent reasoning evaluation.</summary>
public enum PreTerminalContinuationKind
{
    ContinuationSupported = 1,
    ContinuationSupportedAfterRevision = 2,
    ContinuationNotSupported = 3,
}

public sealed record PreTerminalContinuationProposal
{
    public PreTerminalContinuationProposal(
        string runId,
        long cycleSequence,
        long acceptedObservationSequence,
        long beliefRevision,
        string traceDigest,
        string baseReasoningRevisionReference,
        string proposedReasoningRevisionReference,
        PreTerminalContinuationKind kind,
        IReadOnlyList<string>? supportingEvidenceReferences = null,
        DateTimeOffset? evaluatedAt = null,
        string? strategyExecutionId = null,
        string? runtimeExecutionIntentReference = null,
        string? evidenceViewDigest = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseReasoningRevisionReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedReasoningRevisionReference);
        if (acceptedObservationSequence < 0) throw new ArgumentOutOfRangeException(nameof(acceptedObservationSequence));
        if (beliefRevision < 0) throw new ArgumentOutOfRangeException(nameof(beliefRevision));
        if (cycleSequence < 0) throw new ArgumentOutOfRangeException(nameof(cycleSequence));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.Equals(baseReasoningRevisionReference, proposedReasoningRevisionReference, StringComparison.Ordinal))
            throw new ArgumentException("A proposal must identify a new reasoning revision.", nameof(proposedReasoningRevisionReference));

        RunId = runId;
        CycleSequence = cycleSequence;
        AcceptedObservationSequence = acceptedObservationSequence;
        BeliefRevision = beliefRevision;
        TraceDigest = traceDigest;
        BaseReasoningRevisionReference = baseReasoningRevisionReference;
        ProposedReasoningRevisionReference = proposedReasoningRevisionReference;
        Kind = kind;
        SupportingEvidenceReferences = new ReadOnlyCollection<string>(
            (supportingEvidenceReferences ?? Array.Empty<string>()).ToArray());
        EvaluatedAt = evaluatedAt;
        StrategyExecutionId = strategyExecutionId;
        RuntimeExecutionIntentReference = runtimeExecutionIntentReference;
        EvidenceViewDigest = evidenceViewDigest;
    }

    public string RunId { get; }
    public long CycleSequence { get; }
    public long AcceptedObservationSequence { get; }
    public long BeliefRevision { get; }
    public string TraceDigest { get; }
    public string BaseReasoningRevisionReference { get; }
    public string ProposedReasoningRevisionReference { get; }
    public PreTerminalContinuationKind Kind { get; }
    public IReadOnlyList<string> SupportingEvidenceReferences { get; }
    public DateTimeOffset? EvaluatedAt { get; }
    public string? StrategyExecutionId { get; }
    public string? RuntimeExecutionIntentReference { get; }
    public string? EvidenceViewDigest { get; }
}

/// <summary>Optional RuntimeAgent reasoning seam; it returns passive data only.</summary>
public interface IPreTerminalReasoningEvaluator
{
    string AcceptedReasoningRevisionReference { get; }

    ValueTask<PreTerminalContinuationProposal> EvaluateAsync(
        PreTerminalReasoningSnapshot snapshot,
        CancellationToken cancellationToken);

    bool TryCommit(PreTerminalContinuationProposal proposal);
}

/// <summary>Optional passive marker for a strategy-bound evaluator.</summary>
public interface IStrategyPreTerminalReasoningEvaluator : IPreTerminalReasoningEvaluator
{
    string StrategyExecutionId { get; }
    string RuntimeExecutionIntentReference { get; }
}
