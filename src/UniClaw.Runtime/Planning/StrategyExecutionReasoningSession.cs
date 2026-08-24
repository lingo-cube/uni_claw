using UniClaw.Runtime.Model;

#pragma warning disable CS1591

namespace UniClaw.Runtime.Planning;

/// <summary>
/// One-run RuntimeAgent reasoning session. It stages hypothesis changes until the
/// Agent accepts the passive pre-terminal proposal; it has no execution authority.
/// </summary>
public sealed class StrategyExecutionReasoningSession : IStrategyPreTerminalReasoningEvaluator
{
    private readonly object _sync = new();
    private readonly RuntimeExecutionIntent _intent;
    private readonly PreTerminalReasoningLedger _ledger;
    private ExecutionHypothesis _hypothesis;
    private bool _sealed;
    private DateTimeOffset? _sealedAt;
    private Pending? _pending;
    private readonly List<AcceptedReasoningRevision> _accepted = [];

    public StrategyExecutionReasoningSession(RuntimeExecutionIntent intent, string runId)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        _intent = intent;
        RunId = runId;
        StrategyExecutionId = $"strategy-execution:{runId}:{intent.StrategyId}";
        _ledger = new PreTerminalReasoningLedger("reasoning-0");
        _hypothesis = new ExecutionHypothesis(runId, intent.StrategyId,
            "Explore declared bounded scope", "Discover -> Verify -> Continue",
            "Declared evidence coverage", 1f, null, null, ExecutionHypothesisStatus.Created);
        _accepted.Add(new AcceptedReasoningRevision("reasoning-0", null, _hypothesis, null, null, null, null));
    }

    public string RunId { get; }
    public string StrategyExecutionId { get; }
    public string RuntimeExecutionIntentReference => _intent.StrategyId;
    public ExecutionHypothesis CurrentHypothesis { get { lock (_sync) return _hypothesis; } }
    public IReadOnlyList<ReasoningRevision> History => _ledger.History;
    public IReadOnlyList<AcceptedReasoningRevision> AcceptedHistory { get { lock (_sync) return _accepted.ToArray(); } }
    public string AcceptedReasoningRevisionReference => _ledger.Current.Reference;

    public ValueTask<PreTerminalContinuationProposal> EvaluateAsync(PreTerminalReasoningSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_sync)
        {
            if (_sealed) throw new InvalidOperationException("Reasoning session is sealed.");
            if (!string.Equals(snapshot.RunId, RunId, StringComparison.Ordinal)) throw new InvalidOperationException("Run correlation mismatch.");
            var view = snapshot.StrategyEvidence ?? throw new InvalidOperationException("Strategy execution requires exactly one evidence view.");
            if (!string.Equals(view.ContractVersion, StrategyExecutionEvidenceView.CurrentContractVersion, StringComparison.Ordinal)
                || !string.Equals(view.RuntimeExecutionIntentReference, RuntimeExecutionIntentReference, StringComparison.Ordinal)
                || view.StructuralProgressRevision != snapshot.DfsProgressRevision
                || string.IsNullOrWhiteSpace(view.EvidenceViewDigest)
                || !string.Equals(snapshot.StrategyExecutionId, StrategyExecutionId, StringComparison.Ordinal))
                throw new InvalidOperationException("Strategy evidence contract or session correlation mismatch.");
            if (!string.Equals(view.RunId, snapshot.RunId, StringComparison.Ordinal)
                || view.AcceptedObservationSequence != snapshot.AcceptedObservationSequence
                || view.BeliefRevision != snapshot.BeliefRevision
                || !string.Equals(view.BeliefDigest, snapshot.BeliefDigest, StringComparison.Ordinal)
                || !string.Equals(view.TraceDigest, snapshot.TraceDigest, StringComparison.Ordinal))
                throw new InvalidOperationException("Evidence view correlation mismatch.");

            var contradiction = view.ContradictionEvidenceReferences.Count != 0
                || view.StructuralProgressFacts.Any(x => x.Kind == StrategyStructuralProgressKind.ContradictionObserved);
            var decision = new RuntimeDecision(RunId,
                contradiction ? RuntimeDecisionState.Revise : RuntimeDecisionState.Continue,
                _hypothesis.DirectiveReference, view.EvidenceViewDigest,
                contradiction ? "Accepted evidence contradicts the current hypothesis." : "Accepted evidence supports bounded continuation.");
            var adaptationResult = StrategyHypothesisAdapter.Evaluate(_intent.Adaptation, decision, _hypothesis);
            var kind = adaptationResult is StrategyHypothesisAdapter.Result.Blocked
                ? PreTerminalContinuationKind.ContinuationNotSupported
                : contradiction ? PreTerminalContinuationKind.ContinuationSupportedAfterRevision : PreTerminalContinuationKind.ContinuationSupported;
            var adaptedResult = adaptationResult as StrategyHypothesisAdapter.Result.Adapted;
            var adapted = adaptedResult?.Adaptation.AdaptedHypothesis ?? _hypothesis;
            var next = $"reasoning-{_ledger.History.Count}";
            var proposal = new PreTerminalContinuationProposal(RunId, snapshot.CycleSequence,
                snapshot.AcceptedObservationSequence, snapshot.BeliefRevision, snapshot.TraceDigest,
                AcceptedReasoningRevisionReference, next, kind,
                view.CoverageEvidenceReferences, DateTimeOffset.UtcNow,
                StrategyExecutionId, RuntimeExecutionIntentReference, view.EvidenceViewDigest);
            if (_pending is not null) throw new InvalidOperationException("A reasoning evaluation is already pending acceptance.");
            var blockedViolation = adaptationResult is StrategyHypothesisAdapter.Result.Blocked blocked ? blocked.Violation : null;
            _pending = new Pending(next, snapshot.CycleSequence, snapshot.AcceptedObservationSequence, snapshot.BeliefRevision, kind, adapted, decision,
                adaptedResult?.Adaptation,
                view.EvidenceViewDigest, snapshot.TraceDigest, blockedViolation);
            return ValueTask.FromResult(proposal);
        }
    }

    public bool TryCommit(PreTerminalContinuationProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_sync)
        {
            if (_sealed || _pending is null || !string.Equals(proposal.RunId, RunId, StringComparison.Ordinal)
                || proposal.CycleSequence != _pending.CycleSequence
                || proposal.AcceptedObservationSequence != _pending.AcceptedObservationSequence
                || proposal.BeliefRevision != _pending.BeliefRevision
                || !string.Equals(proposal.BaseReasoningRevisionReference, AcceptedReasoningRevisionReference, StringComparison.Ordinal)
                || !string.Equals(proposal.ProposedReasoningRevisionReference, _pending.Reference, StringComparison.Ordinal)
                || proposal.Kind != _pending.Kind
                || !string.Equals(proposal.TraceDigest, _pending.TraceDigest, StringComparison.Ordinal)
                || !string.Equals(proposal.StrategyExecutionId, StrategyExecutionId, StringComparison.Ordinal)
                || !string.Equals(proposal.RuntimeExecutionIntentReference, RuntimeExecutionIntentReference, StringComparison.Ordinal)
                || !string.Equals(proposal.EvidenceViewDigest, _pending.EvidenceDigest, StringComparison.Ordinal)
                || !_ledger.TryCommit(proposal)) return false;
            _hypothesis = _pending.Hypothesis;
            _accepted.Add(new AcceptedReasoningRevision(proposal.ProposedReasoningRevisionReference,
                proposal.BaseReasoningRevisionReference, _hypothesis, _pending.Decision, _pending.Adaptation, _pending.EvidenceDigest, _pending.BoundaryViolation));
            _pending = null;
            return true;
        }
    }

    public StrategyExecutionReasoningReceipt Seal()
    {
        lock (_sync)
        {
            _sealed = true; _pending = null; _sealedAt ??= DateTimeOffset.UtcNow;
            return new StrategyExecutionReasoningReceipt(StrategyExecutionId, RunId, RuntimeExecutionIntentReference, _accepted.ToArray(), _sealedAt.Value, true);
        }
    }

    private sealed record Pending(string Reference, long CycleSequence, long AcceptedObservationSequence, long BeliefRevision, PreTerminalContinuationKind Kind, ExecutionHypothesis Hypothesis, RuntimeDecision Decision, HypothesisAdaptation? Adaptation, string EvidenceDigest, string TraceDigest, StrategyBoundaryViolation? BoundaryViolation);
}

public sealed record AcceptedReasoningRevision(string Reference, string? ParentReference,
    ExecutionHypothesis Hypothesis, RuntimeDecision? Decision, HypothesisAdaptation? Adaptation,
    string? EvidenceDigest, StrategyBoundaryViolation? BoundaryViolation);
