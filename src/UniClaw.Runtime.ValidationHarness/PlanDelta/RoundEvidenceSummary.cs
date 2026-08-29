using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// Immutable summary of one run's observed result (spec "Frozen iterative loop
/// with independent runs" + "PlanDelta contract"): the run identity, the
/// strategy identity it executed, the deterministic terminal state, the observed
/// event kinds, and the evidence refs AVAILABLE in this round.
/// <see cref="EvidenceRefs"/> is the resolvable citation universe for the
/// round's PlanDelta — an evidence citation that does not resolve here is a hard
/// contract violation, not a soft warning. Validation tooling only; nothing here
/// is Runtime input.
/// </summary>
public sealed record RoundEvidenceSummary
{
    /// <summary>Create one immutable observed-result summary.</summary>
    public RoundEvidenceSummary(
        string runId,
        string strategyId,
        string terminalState,
        IReadOnlyList<string> eventKinds,
        IReadOnlyList<string> evidenceRefs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalState);
        ArgumentNullException.ThrowIfNull(eventKinds);
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        if (eventKinds.Any(string.IsNullOrWhiteSpace) || evidenceRefs.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Event kinds and evidence refs must be non-empty strings.");

        RunId = runId;
        StrategyId = strategyId;
        TerminalState = terminalState;
        EventKinds = eventKinds.ToImmutableArray();
        EvidenceRefs = evidenceRefs.ToImmutableArray();
    }

    /// <summary>DriverHost-owned run identity of the observed result.</summary>
    public string RunId { get; }

    /// <summary>Strategy identity that executed this run.</summary>
    public string StrategyId { get; }

    /// <summary>Deterministic terminal state of the run.</summary>
    public string TerminalState { get; }

    /// <summary>Observed event kinds of the run.</summary>
    public IReadOnlyList<string> EventKinds { get; }

    /// <summary>Evidence refs available in this round (the citation universe for the round's PlanDelta).</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }
}