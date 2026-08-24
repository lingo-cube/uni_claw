using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Agent;

public sealed partial class Agent
{
    private long _preTerminalCycleSequence;
    private long _preTerminalDfsProgressRevision;
    private readonly HashSet<(string RunId, long ObservationSequence)> _preTerminalEvidenceReservations = [];
    private readonly object _preTerminalReservationLock = new();
    private readonly PreTerminalCheckpointValidator _preTerminalValidator = new();

    /// <summary>
    /// Optional Agent-owned reasoning seam. It is invoked only at an already
    /// accepted-evidence boundary and never authorizes or dispatches an action.
    /// A null evaluator is a strict no-op.
    /// </summary>
    private async Task<bool> TryEvaluatePreTerminalCheckpointAsync(
        string runId,
        string executionBoundaryReference,
        Observation acceptedObservation,
        long beliefRevision,
        string beliefDigest,
        long dfsProgressRevision,
        CancellationToken cancellationToken)
    {
        var evaluator = _preTerminalReasoningEvaluator;
        if (evaluator is null || _state != RunState.Running)
            return true;
        lock (_preTerminalReservationLock)
        {
            if (!_preTerminalEvidenceReservations.Add((runId, acceptedObservation.SequenceNumber)))
                return true;
        }

        var cycleSequence = ++_preTerminalCycleSequence;
        var strategyEvaluator = evaluator as IStrategyPreTerminalReasoningEvaluator;
        var evidenceView = strategyEvaluator is null ? null : new StrategyExecutionEvidenceView(
            runId, strategyEvaluator.RuntimeExecutionIntentReference, acceptedObservation.SequenceNumber, beliefRevision,
            beliefDigest, dfsProgressRevision,
            [new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, dfsProgressRevision, $"progress:{dfsProgressRevision}")],
            Array.Empty<string>(), Array.Empty<string>(), [$"trace:{_trace.Count}:{runId}"],
            $"trace:{_trace.Count}:{runId}");
        var snapshot = new PreTerminalReasoningSnapshot(
            PreTerminalReasoningSnapshot.CurrentContractVersion,
            runId,
            cycleSequence,
            acceptedObservation.SequenceNumber,
            beliefRevision,
            beliefDigest,
            dfsProgressRevision,
            $"trace:{_trace.Count}",
            $"trace:{_trace.Count}:{runId}",
            evaluator.AcceptedReasoningRevisionReference,
            executionBoundaryReference,
            DateTimeOffset.UtcNow.AddSeconds(5), strategyEvidence: evidenceView,
            strategyExecutionId: strategyEvaluator?.StrategyExecutionId,
            runtimeExecutionIntentReference: strategyEvaluator?.RuntimeExecutionIntentReference);

        PreTerminalContinuationProposal proposal;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            proposal = await evaluator.EvaluateAsync(snapshot, timeout.Token)
                .AsTask().WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _preTerminalValidator.Close(runId, cycleSequence);
            return false;
        }
        catch (TimeoutException)
        {
            _preTerminalValidator.Close(runId, cycleSequence);
            return false;
        }
        catch (Exception)
        {
            _preTerminalValidator.Close(runId, cycleSequence);
            return false;
        }

        var state = new PreTerminalCheckpointState(
            runId,
            _state,
            cycleSequence,
            acceptedObservation.SequenceNumber,
            beliefRevision,
            beliefDigest,
            dfsProgressRevision,
            snapshot.TraceDigest,
            evaluator.AcceptedReasoningRevisionReference,
            DateTimeOffset.UtcNow,
            executionBoundaryReference,
            strategyEvaluator?.StrategyExecutionId,
            strategyEvaluator?.RuntimeExecutionIntentReference,
            evidenceView?.EvidenceViewDigest);
        var validation = _preTerminalValidator.Validate(snapshot, proposal, state);
        if (!validation.Accepted)
            return false;
        if (!evaluator.TryCommit(proposal))
            return false;
        if (proposal.Kind == PreTerminalContinuationKind.ContinuationNotSupported)
            return false;
        return _state == RunState.Running;
    }
}
