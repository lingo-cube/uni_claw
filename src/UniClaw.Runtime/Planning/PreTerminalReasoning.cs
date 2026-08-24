using System.Collections.Immutable;
using UniClaw.Runtime.Model;

#pragma warning disable CS1591

namespace UniClaw.Runtime.Planning;

/// <summary>Internal RuntimeAgent reasoning revision identity and history.</summary>
public sealed record ReasoningRevision
{
    public ReasoningRevision(string reference, string? parentReference = null)
    {
        Reference = Validate(reference);
        if (parentReference is not null) ArgumentException.ThrowIfNullOrWhiteSpace(parentReference);
        ParentReference = parentReference;
    }

    public string Reference { get; }
    public string? ParentReference { get; }

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

/// <summary>RuntimeAgent-owned transactional reasoning history.</summary>
public sealed class PreTerminalReasoningLedger
{
    private ImmutableList<ReasoningRevision> _history;
    private readonly object _sync = new();

    public PreTerminalReasoningLedger(string initialReference = "reasoning-0")
    {
        _history = ImmutableList.Create(new ReasoningRevision(initialReference));
    }

    public ReasoningRevision Current { get { lock (_sync) return _history[^1]; } }
    public IReadOnlyList<ReasoningRevision> History { get { lock (_sync) return _history; } }

    /// <summary>Commits only the proposal whose parent is still current.</summary>
    public bool TryCommit(PreTerminalContinuationProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_sync)
        {
            if (!string.Equals(proposal.BaseReasoningRevisionReference, _history[^1].Reference, StringComparison.Ordinal))
                return false;
            if (_history.Any(x => string.Equals(x.Reference, proposal.ProposedReasoningRevisionReference, StringComparison.Ordinal)))
                return false;
            _history = _history.Add(new ReasoningRevision(
                proposal.ProposedReasoningRevisionReference,
                proposal.BaseReasoningRevisionReference));
            return true;
        }
    }
}

/// <summary>Small adapter that keeps revision state inside RuntimeAgent reasoning.</summary>
public sealed class LedgerPreTerminalReasoningEvaluator : IPreTerminalReasoningEvaluator
{
    private readonly PreTerminalReasoningLedger _ledger;
    private readonly Func<PreTerminalReasoningSnapshot, CancellationToken, ValueTask<PreTerminalContinuationProposal>> _evaluate;

    public LedgerPreTerminalReasoningEvaluator(
        Func<PreTerminalReasoningSnapshot, CancellationToken, ValueTask<PreTerminalContinuationProposal>> evaluate,
        PreTerminalReasoningLedger? ledger = null)
    {
        ArgumentNullException.ThrowIfNull(evaluate);
        _evaluate = evaluate;
        _ledger = ledger ?? new PreTerminalReasoningLedger();
    }

    public string AcceptedReasoningRevisionReference => _ledger.Current.Reference;

    public ValueTask<PreTerminalContinuationProposal> EvaluateAsync(
        PreTerminalReasoningSnapshot snapshot,
        CancellationToken cancellationToken)
        => _evaluate(snapshot, cancellationToken);

    public bool TryCommit(PreTerminalContinuationProposal proposal)
        => _ledger.TryCommit(proposal);
}
