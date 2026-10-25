using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    private static readonly JsonSerializerOptions HistoryJson = new(JsonSerializerDefaults.Web);
    private WorldHistoryRecording? _history;
    private int _historySequence;
    private int _historyOperationActive;
    private string? _historyFailure;
    public WorldHistoryPosition? HistoryPosition => _history is null ? null : new(_history.Journal.RecordingId, _historySequence);
    internal string? HistoryExecutionBlockReason => _historyFailure ?? (Volatile.Read(ref _historyOperationActive) != 0 ? "world-operation-in-progress" : null);

    internal void EnsureHistoryAvailable()
    {
        if (_historyFailure is not null) throw new WorldPersistenceException(_historyFailure);
    }

    internal void EnsureHistoryExecutionAvailable()
    {
        EnsureHistoryAvailable();
        if (Volatile.Read(ref _historyOperationActive) != 0) throw new InvalidOperationException("world-operation-in-progress");
    }

    public static async ValueTask<WorldModel> CreateAsync(IReadOnlySet<string> relevanceScope,
        IAssociationStrategy? associationStrategy = null, IUiObservationStrategy? observationStrategy = null,
        IContinuityStrategy? continuityStrategy = null,
        IReadOnlyList<UniClaw.Kernel.Perception.UiHierarchy.NativeContextSignatureProfile>? nativeContextProfiles = null,
        WorldHistoryRecording? history = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var model = new WorldModel(relevanceScope, associationStrategy, observationStrategy, continuityStrategy, nativeContextProfiles);
        if (history is null) return model;
        ValidateHistory(history);
        var row = model.InitializationRecord(history);
        if (history.Journal is IAsyncWorldJournal source) await source.InitializeAsync(row).ConfigureAwait(false);
        else history.Journal.Append(row);
        model._history = history; model._historySequence = 1;
        return model;
    }

    private void InitializeHistory(WorldHistoryRecording? history)
    {
        if (history is null) return;
        ValidateHistory(history);
        history.Journal.Append(InitializationRecord(history));
        _history = history; _historySequence = 1;
    }
    private static void ValidateHistory(WorldHistoryRecording history)
    {
        if (!history.Journal.CanInitialize || history.Journal.ReadRecords().Count != 0
            || !Guid.TryParseExact(history.Journal.RecordingId, "N", out _)
            || !Guid.TryParseExact(history.RunRecordingId, "N", out _)
            || !Guid.TryParseExact(history.EvidenceRecordingId, "N", out _))
            throw new InvalidOperationException("world-history-original-empty-writer-required");
    }
    private WorldHistoryRecord InitializationRecord(WorldHistoryRecording history) =>
        new WorldHistoryRecord(1, "initialize", JsonSerializer.SerializeToElement(new
        {
            schema = "uniclaw.world.owner.v1", history.RunRecordingId, history.EvidenceRecordingId,
            configuration = new
            {
                relevanceScope = _relevanceScope.Order(StringComparer.Ordinal).ToArray(),
                worldModuleVersionId = typeof(WorldModel).Module.ModuleVersionId.ToString("N"),
                associationStrategy = StrategyIdentity(_associationStrategy),
                observationStrategy = StrategyIdentity(_observationStrategy),
                continuityStrategy = StrategyIdentity(_continuityStrategy),
                nativeProfiles = _nativeSemanticProfiles.Select(p => p.Fingerprint).ToArray(),
            },
        }, HistoryJson));
    private static object? StrategyIdentity(object? strategy) => strategy is null ? null : new
    { type = strategy.GetType().AssemblyQualifiedName, moduleVersionId = strategy.GetType().Module.ModuleVersionId.ToString("N") };

    // Explicit synchronous compatibility; both entrances use the same candidate/ack/publication path.
    private T RecordOperation<T>(string operation, object input, Func<T> apply) =>
        RecordOperationAsync(operation, input, apply).AsTask().GetAwaiter().GetResult();

    private async ValueTask<T> RecordOperationAsync<T>(string operation, object input, Func<T> apply,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHistoryAvailable();
        if (Interlocked.CompareExchange(ref _historyOperationActive, 1, 0) != 0)
            throw new InvalidOperationException("world-operation-reentrant");
        var previous = _owner;
        var completed = false;
        try
        {
            if (_history is null) return apply();
            _owner = previous.Copy();
            var result = apply();
            completed = true;
            var candidate = _owner;
            if (!Changed(previous, candidate)) { _owner = previous; return result; }
            var data = JsonSerializer.SerializeToElement(new
            {
                input,
                relevanceLog = candidate.RelevanceLog.Skip(previous.RelevanceLog.Count).ToArray(),
                associationLog = candidate.AssociationLog.Skip(previous.AssociationLog.Count).ToArray(),
                continuityLog = candidate.ContinuityLog.Skip(previous.ContinuityLog.Count).ToArray(),
                claimEvolutionLog = candidate.ClaimEvolutionLog.Skip(previous.ClaimEvolutionLog.Count).ToArray(),
                conflictResolutions = candidate.ConflictResolutions.Skip(previous.ConflictResolutions.Count).ToArray(),
                pageCorrections = candidate.PageCorrections.Skip(previous.PageCorrections.Count).ToArray(),
                contextDecisions = candidate.ContextDecisions.Skip(previous.ContextDecisions.Count).ToArray(),
                semanticVerifications = candidate.SemanticVerifications.Skip(previous.SemanticVerifications.Count).ToArray(),
                continuityDemands = candidate.Demands.ToArray(),
                processedEvidenceIds = candidate.WorldEvidence.Keys.Where(id => !previous.WorldEvidence.ContainsKey(id)).ToArray(),
                containerObservations = candidate.ContainerObservations.Select(p => new { containerId = p.Key, evidenceId = p.Value.EvidenceId }).ToArray(),
                latestPageObservationTime = candidate.LatestPageTime,
                revokedInterpretations = candidate.RevokedInterpretations.ToArray(),
            }, HistoryJson);
            // No publication until the complete outer operation has durable acknowledgement.
            // Existing views reference previous collections, never the uncommitted candidate.
            _owner = previous;
            try
            {
                var row = new WorldHistoryRecord(_historySequence + 1, operation, data);
                var revisions = candidate.Revisions.Skip(previous.Revisions.Count).ToArray();
                // The Owner has accepted this operation. Settle actual storage even if its
                // caller cancels; Driver handles cancellation after this acknowledgement.
                if (_history.Journal is IAsyncWorldJournal asyncJournal)
                    await asyncJournal.AppendOperationAsync(row, revisions).ConfigureAwait(false);
                else _history.Journal.AppendOperation(row, revisions);
            }
            catch (Exception ex)
            {
                _historyFailure = "world-persistence-failed:" + ex.GetType().Name;
                throw new WorldPersistenceException(_historyFailure, ex);
            }
            _historySequence++;
            _owner = candidate;
            return result;
        }
        catch (Exception ex)
        {
            _owner = previous;
            if (completed && _historyFailure is null)
            {
                _historyFailure = "world-persistence-failed:" + ex.GetType().Name;
                throw new WorldPersistenceException(_historyFailure, ex);
            }
            throw;
        }
        finally { Volatile.Write(ref _historyOperationActive, 0); }
    }

    private static bool Changed(OwnerState a, OwnerState b) =>
        a.Revisions.Count != b.Revisions.Count || a.RelevanceLog.Count != b.RelevanceLog.Count
        || a.AssociationLog.Count != b.AssociationLog.Count || a.ContinuityLog.Count != b.ContinuityLog.Count
        || a.ClaimEvolutionLog.Count != b.ClaimEvolutionLog.Count || a.ConflictResolutions.Count != b.ConflictResolutions.Count
        || a.PageCorrections.Count != b.PageCorrections.Count || a.ContextDecisions.Count != b.ContextDecisions.Count
        || a.SemanticVerifications.Count != b.SemanticVerifications.Count || !a.Demands.SequenceEqual(b.Demands);
    private sealed class OwnerState
    {
        public List<RelevanceJudgment> RelevanceLog = new();
        public List<WorldBeliefRevision> Revisions = new();
        public List<AssociationDecision> AssociationLog = new();
        public List<ContinuityDemand> Demands = new();
        public Dictionary<string, int> DemandPositions = new(StringComparer.Ordinal);
        public Dictionary<string, int> ActiveDemandCounts = new(StringComparer.Ordinal);
        public List<ContinuityDecision> ContinuityLog = new();
        public List<ClaimEvolutionDecision> ClaimEvolutionLog = new();
        public DateTimeOffset? LatestPageTime = null;
        public Dictionary<WorldBeliefRevision, WorldRevisionIndex> RevisionIndexes = new(ReferenceEqualityComparer.Instance);
        public List<ConflictResolution> ConflictResolutions = new();
        public Dictionary<string, EvidenceRecord> WorldEvidence = new(StringComparer.Ordinal);
        public List<PageCorrectionDecision> PageCorrections = new();
        public List<ContainerContextDecision> ContextDecisions = new();
        public List<SemanticVerificationDecision> SemanticVerifications = new();
        public HashSet<string> RevokedInterpretations = new(StringComparer.Ordinal);
        public Dictionary<string, EvidenceRecord> ContainerObservations = new(StringComparer.Ordinal);
        public OwnerState Copy() => new()
        {
            RelevanceLog = new(RelevanceLog),
            Revisions = new(Revisions),
            AssociationLog = new(AssociationLog),
            Demands = new(Demands),
            DemandPositions = new(DemandPositions, StringComparer.Ordinal),
            ActiveDemandCounts = new(ActiveDemandCounts, StringComparer.Ordinal),
            ContinuityLog = new(ContinuityLog),
            ClaimEvolutionLog = new(ClaimEvolutionLog),
            LatestPageTime = LatestPageTime,
            RevisionIndexes = new(RevisionIndexes, ReferenceEqualityComparer.Instance),
            ConflictResolutions = new(ConflictResolutions),
            WorldEvidence = new(WorldEvidence, StringComparer.Ordinal),
            PageCorrections = new(PageCorrections),
            ContextDecisions = new(ContextDecisions),
            SemanticVerifications = new(SemanticVerifications),
            RevokedInterpretations = new(RevokedInterpretations, StringComparer.Ordinal),
            ContainerObservations = new(ContainerObservations, StringComparer.Ordinal),
        };
    }

}
