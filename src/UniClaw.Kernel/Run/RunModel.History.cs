using System.Text.Json;

namespace UniClaw.Kernel.Run;

/// <summary>Trusted Owner-history storage. Append success is the durable state commit.</summary>
public interface IRunStateJournal
{
    bool CanInitialize { get; }
    string RecordingId { get; }
    /// <summary>Complete committed history; missing/rolled-back endpoints or uncommitted tails must throw.</summary>
    IReadOnlyList<RunHistoryRecord> ReadRecords();
    void Append(RunHistoryRecord record);
}

/// <summary>Success acknowledges the original commit, never just acceptance into a queue.</summary>
public interface IAsyncRunStateJournal : IRunStateJournal
{
    ValueTask AppendAsync(RunHistoryRecord record, CancellationToken cancellationToken = default);
}

public enum RunHistoryTransition { Admitted, Cycle, Action, Terminal, Traversal }

/// <summary>One original Owner's committed history position, never an execution permission.</summary>
public sealed record RunHistoryPosition(string RecordingId, int Sequence);

public sealed record RunOutcomeRecord(string ProofId, TerminalClassification Classification,
    ObligationStatus[] Obligations, string[] BasisEvidenceIds, string[] EffectEvidenceIds,
    string[] SituationEvidenceIds, int UnresolvedUncertainty, string Reason,
    string BasisComparer = "ordinal", string EffectComparer = "ordinal", string SituationComparer = "ordinal");

public sealed record RunHistoryRecord(int Sequence, RunHistoryTransition Transition, int Cycles, int Acts,
    RunIdentityRecord? Identity = null, RunObligation[]? Obligations = null, RunOutcomeRecord? Outcome = null,
    RunTraversalEvent? Traversal = null);

internal sealed class RunStatePersistenceException(string message, Exception? inner = null) : IOException(message, inner);

public sealed partial class RunModel
{
    private readonly IRunStateJournal? _stateJournal;
    private string? _statePersistenceFailure;
    private string? _restoredRecordingId;
    private int _committedHistorySequence;
    private int _stateOperationActive;
    public bool IsReadOnly { get; private set; }
    internal bool IsStatePersistenceFaulted => _statePersistenceFailure is not null;
    internal bool IsStateOperationActive => Volatile.Read(ref _stateOperationActive) != 0;
    internal string? StateRecordingId => _stateJournal?.RecordingId ?? _restoredRecordingId;
    public RunHistoryPosition? HistoryPosition => StateRecordingId is { } id && _committedHistorySequence > 0
        ? new(id, _committedHistorySequence) : null;

    internal void EnsureStateWritable()
    {
        EnsureStateAvailable();
        if (IsStateOperationActive) throw new InvalidOperationException("run-operation-in-progress");
    }

    private void EnsureStateAvailable()
    {
        if (IsReadOnly) throw new InvalidOperationException("restored-run-is-read-only");
        if (_statePersistenceFailure is not null) throw new RunStatePersistenceException(_statePersistenceFailure);
    }

    private async ValueTask<T> RecordOperationAsync<T>(Func<ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStateAvailable();
        if (Interlocked.CompareExchange(ref _stateOperationActive, 1, 0) != 0)
            throw new InvalidOperationException("run-operation-in-progress");
        try { return await operation().ConfigureAwait(false); }
        finally { Volatile.Write(ref _stateOperationActive, 0); }
    }

    private async ValueTask CommitStateAsync(RunState next, RunHistoryTransition transition, RunIdentityRecord? identity = null)
    {
        EnsureStateAvailable();
        var outcome = next.Outcome;
        var record = new RunHistoryRecord(_committedHistorySequence + 1, transition, next.Progress.Cycles, next.Progress.Acts,
            transition == RunHistoryTransition.Admitted ? identity : null,
            transition == RunHistoryTransition.Admitted ? next.ProofObligations.Obligations.ToArray() : null,
            outcome is null ? null : new(outcome.OutcomeProofId, outcome.Classification, outcome.Obligations.ToArray(),
                outcome.BasisEvidenceIds.Order(StringComparer.Ordinal).ToArray(), outcome.EffectEvidenceIds.Order(StringComparer.Ordinal).ToArray(),
                outcome.SituationEvidenceIds.Order(StringComparer.Ordinal).ToArray(), outcome.UnresolvedUncertainty, outcome.Reason,
                ComparerName(outcome.BasisEvidenceIds), ComparerName(outcome.EffectEvidenceIds), ComparerName(outcome.SituationEvidenceIds)),
            transition == RunHistoryTransition.Admitted ? new(TraversalSchema, RunTraversalTransition.Initialized) : null);
        await AppendHistoryAsync(record).ConfigureAwait(false);
        _history.Add(next);
        if (record.Traversal is { } initialized) ApplyTraversalEvent(initialized);
    }

    private async ValueTask AppendHistoryAsync(RunHistoryRecord record)
    {
        EnsureStateAvailable();
        if (_stateJournal is { } journal)
        {
            try
            {
                // This Owner has accepted the candidate; cancellation cannot make an
                // accepted write disappear. Settle the real acknowledgement first.
                if (journal is IAsyncRunStateJournal asyncJournal)
                    await asyncJournal.AppendAsync(record).ConfigureAwait(false);
                else journal.Append(record);
            }
            catch (Exception ex)
            {
                _statePersistenceFailure = "run-state-persistence-failed:" + ex.GetType().Name;
                throw new RunStatePersistenceException(_statePersistenceFailure, ex);
            }
        }
        _committedHistorySequence = record.Sequence;
    }

    /// <summary>Restore only the original Owner's recorded history. No admission,
    /// activation, Outcome re-emission or execution is performed or allowed.</summary>
    public static RunModel RestoreReadOnly(IRunStateJournal journal, RunIdentityRecord original, string expectedRecordingId)
    {
        if (journal.RecordingId != expectedRecordingId || !Guid.TryParseExact(expectedRecordingId, "N", out _))
            throw new InvalidDataException("run-history-recording-mismatch");
        if (original.Schema != IdentitySchema) throw new InvalidDataException("run-history-comparer-provenance-missing");
        var rows = journal.ReadRecords();
        if (rows.Count == 0) throw new InvalidDataException("run-history-missing");
        var view = ReadArchivedIdentity(original, original.RunId);
        var restored = new RunModel { IsReadOnly = true, RunId = original.RunId, View = view,
            _identityRuntimeAssigned = original.RuntimeAssigned, _restoredRecordingId = expectedRecordingId };
        foreach (var row in rows)
        {
            if (row.Sequence != restored._committedHistorySequence + 1 || row.Cycles < 0 || row.Acts < 0)
                throw new InvalidDataException("run-history-sequence");
            var prior = restored.State;
            if (prior is null)
            {
                if (row.Transition != RunHistoryTransition.Admitted || row.Cycles != 0 || row.Acts != 0
                    || row.Obligations is null || row.Outcome is not null
                    || JsonSerializer.Serialize(row.Identity) != JsonSerializer.Serialize(original))
                    throw new InvalidDataException("run-history-initial-identity");
                restored._history.Add(new(view, new(original.Objective, "pursuing"),
                    new(Array.AsReadOnly(row.Obligations.ToArray())), new(0, 0)));
                if (row.Traversal is { } initialized)
                {
                    if (initialized.Transition != RunTraversalTransition.Initialized)
                        throw new InvalidDataException("run-traversal-initialization");
                    restored.ApplyTraversalEvent(initialized);
                }
                restored._committedHistorySequence = row.Sequence;
                continue;
            }
            if (prior.Outcome is not null || row.Identity is not null || row.Obligations is not null)
                throw new InvalidDataException("run-history-after-terminal-or-identity-change");
            if (row.Transition == RunHistoryTransition.Traversal)
            {
                if (row.Cycles != prior.Progress.Cycles || row.Acts != prior.Progress.Acts || row.Outcome is not null
                    || row.Traversal is null || row.Traversal.Transition == RunTraversalTransition.Initialized)
                    throw new InvalidDataException("run-traversal-transition");
                restored.ApplyTraversalEvent(row.Traversal);
                restored._committedHistorySequence = row.Sequence;
                continue;
            }
            if (row.Traversal is not null) throw new InvalidDataException("run-history-unexpected-traversal");
            var valid = row.Transition switch
            {
                RunHistoryTransition.Cycle => row.Cycles == prior.Progress.Cycles + 1 && row.Acts == prior.Progress.Acts && row.Outcome is null,
                RunHistoryTransition.Action => row.Acts == prior.Progress.Acts + 1 && row.Cycles == prior.Progress.Cycles && row.Outcome is null,
                RunHistoryTransition.Terminal => row.Cycles == prior.Progress.Cycles && row.Acts == prior.Progress.Acts && row.Outcome is not null,
                _ => false,
            };
            if (!valid) throw new InvalidDataException("run-history-illegal-transition");
            OutcomeState? outcome = null;
            if (row.Outcome is { } record)
            {
                if (string.IsNullOrWhiteSpace(record.ProofId) || !Enum.IsDefined(record.Classification))
                    throw new InvalidDataException("run-history-outcome-shape");
                // Only freeze the original Assurance result. Do not judge it again.
                outcome = new(record.ProofId, record.Classification, Array.AsReadOnly(record.Obligations.ToArray()),
                    SnapshotSet(record.BasisEvidenceIds.ToHashSet(ReadComparer(record.BasisComparer))),
                    SnapshotSet(record.EffectEvidenceIds.ToHashSet(ReadComparer(record.EffectComparer))),
                    SnapshotSet(record.SituationEvidenceIds.ToHashSet(ReadComparer(record.SituationComparer))), record.UnresolvedUncertainty, record.Reason);
            }
            restored._history.Add(prior with { Progress = new(row.Cycles, row.Acts), Outcome = outcome });
            restored._committedHistorySequence = row.Sequence;
        }
        return restored;
    }
}
