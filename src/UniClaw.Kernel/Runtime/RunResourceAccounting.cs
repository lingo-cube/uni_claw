using System.Text.Json;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Runtime;

/// <summary>Host persistence only; Runtime owns the meaning of these records.</summary>
public interface IRunResourceJournal
{
    bool CanInitialize { get; }
    string? RecordingId => null;
    IReadOnlyList<RunResourceRecord> ReadRecords();
    void Append(RunResourceRecord record);
}

/// <summary>Confirmation, not enqueue acknowledgement; accepted writes settle before cancellation.</summary>
public interface IAsyncRunResourceJournal : IRunResourceJournal
{
    ValueTask AppendAsync(RunResourceRecord record, CancellationToken cancellationToken = default);
}

public enum RunResourcePhase { Initialize, Begin, Confirm }

public sealed record RunResourceIdentity(string RunId, string TaskInstanceId, string ProductSessionId,
    string ContractViewRef, string ContractVersion, int? ContractGeneration, string PolicyFingerprint,
    int InitialSteps, int InitialRounds);

/// <summary>Original-driver read binding. Not serializable recovery state or
/// an execution permission; journal identity values cannot construct one.</summary>
public sealed class RunResourceBinding
{
    internal RunResourceBinding(RunResourceIdentity identity, string? recordingId = null)
        => (Identity, RecordingId) = (identity, recordingId);
    internal string? RecordingId { get; }
    internal RunResourceIdentity Identity { get; }
}

/// <summary>Confirmed resource facts, never an executable driver checkpoint.</summary>
public sealed record RunResourceSnapshot(int StepsConsumed, int RoundsConsumed, int GrantedSteps, int GrantedRounds,
    IReadOnlyList<string> ReceiptIds, IReadOnlyList<BudgetRenewalRecord> Renewals,
    IReadOnlyList<BudgetReviewHistoryEntry> Reviews, IReadOnlyList<string> UsedProgressFacts,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FundedItems,
    bool UnresolvedEffect, bool PendingConsultation, bool Terminal);

public sealed record RunResourceRecord(int Sequence, RunResourcePhase Phase, string Operation,
    RunResourceIdentity? Identity = null, RunResourceSnapshot? Snapshot = null,
    ConsultationKey? Consultation = null, string? RequestFingerprint = null, string? IntentId = null,
    string? ReceiptId = null, RunHistoryPosition? RunHistoryPosition = null, EvidenceHistoryPosition? EvidenceHistoryPosition = null,
    WorldHistoryPosition? WorldHistoryPosition = null);

public sealed record RunResourceRead(RunResourceIdentity? Identity, RunResourceSnapshot? Confirmed,
    string? PendingOperation, bool AccountingComplete, string? Reason)
{
    public RunHistoryPosition? RunHistoryPosition { get; init; }
    public EvidenceHistoryPosition? EvidenceHistoryPosition { get; init; }
    public WorldHistoryPosition? WorldHistoryPosition { get; init; }
    public string? RecordingId { get; init; }
    public int LastSequence { get; init; }
    public bool CommittedEndpointVerified { get; init; }
    // No balance read can reconstitute Run/Control/World or authorize delivery.
    public bool ContinuationSupported => false;
    public int? StepsRemaining => AccountingComplete
        ? checked(Identity!.InitialSteps + Confirmed!.GrantedSteps - Confirmed.StepsConsumed) : null;
    public int? RoundsRemaining => AccountingComplete
        ? checked(Identity!.InitialRounds + Confirmed!.GrantedRounds - Confirmed.RoundsConsumed) : null;
}

/// <summary>Read-only Runtime interpretation of its original single-View record stream.</summary>
public static partial class RunResourceAccounting
{
    // The storage adapter attests the exact original source prefixes; the
    // binding is issued by the original driver, never by this read.
    public static RunResourceRead Inspect(IReadOnlyList<RunResourceRecord> records,
        RunResourceBinding? binding = null, bool sourcePrefixVerified = false, string? recordingId = null)
    {
        RunResourceIdentity? identity = null;
        RunResourceSnapshot? snapshot = null;
        RunResourceRecord? pending = null;
        RunHistoryPosition? position = null;
        EvidenceHistoryPosition? evidencePosition = null;
        WorldHistoryPosition? worldPosition = null;
        var consultations = new HashSet<ConsultationKey>();
        try
        {
            for (var index = 0; index < records.Count; index++)
            {
                var row = records[index];
                if (row.Sequence != index + 1) throw new InvalidDataException("sequence");
                if (index > 0 && position is null && row.RunHistoryPosition is not null)
                    throw new InvalidDataException("run-history-position-initial-link-missing");
                if (row.RunHistoryPosition is { } nextPosition)
                {
                    if (!Guid.TryParseExact(nextPosition.RecordingId, "N", out _) || nextPosition.Sequence < 1
                        || (position is not null && (position.RecordingId != nextPosition.RecordingId || position.Sequence > nextPosition.Sequence)))
                        throw new InvalidDataException("run-history-position");
                    position = nextPosition;
                }
                else if (position is not null) throw new InvalidDataException("run-history-position-missing");
                if (index > 0 && evidencePosition is null && row.EvidenceHistoryPosition is not null)
                    throw new InvalidDataException("evidence-history-position-initial-link-missing");
                if (row.EvidenceHistoryPosition is { } nextEvidence)
                {
                    if (!Guid.TryParseExact(nextEvidence.RecordingId, "N", out _) || nextEvidence.Sequence < 0
                        || (evidencePosition is not null && (evidencePosition.RecordingId != nextEvidence.RecordingId
                            || evidencePosition.Sequence > nextEvidence.Sequence)))
                        throw new InvalidDataException("evidence-history-position");
                    evidencePosition = nextEvidence;
                }
                else if (evidencePosition is not null) throw new InvalidDataException("evidence-history-position-missing");
                if (index > 0 && worldPosition is null && row.WorldHistoryPosition is not null)
                    throw new InvalidDataException("world-history-position-initial-link-missing");
                if (row.WorldHistoryPosition is { } nextWorld)
                {
                    if (!Guid.TryParseExact(nextWorld.RecordingId, "N", out _) || nextWorld.Sequence < 1
                        || (worldPosition is not null && (worldPosition.RecordingId != nextWorld.RecordingId
                            || worldPosition.Sequence > nextWorld.Sequence)))
                        throw new InvalidDataException("world-history-position");
                    worldPosition = nextWorld;
                }
                else if (worldPosition is not null) throw new InvalidDataException("world-history-position-missing");
                switch (row.Phase)
                {
                    case RunResourcePhase.Initialize when index == 0:
                        identity = row.Identity ?? throw new InvalidDataException("identity");
                        if (new[] { identity.RunId, identity.TaskInstanceId, identity.ProductSessionId,
                                identity.ContractViewRef, identity.ContractVersion, identity.PolicyFingerprint }.Any(string.IsNullOrWhiteSpace)
                            || identity.InitialSteps < 0 || identity.InitialRounds < 0)
                            throw new InvalidDataException("identity");
                        snapshot = row.Snapshot ?? throw new InvalidDataException("initial-state");
                        if (snapshot.StepsConsumed != 0 || snapshot.RoundsConsumed != 0
                            || snapshot.GrantedSteps != 0 || snapshot.GrantedRounds != 0)
                            throw new InvalidDataException("initial-state");
                        Validate(snapshot, identity);
                        break;
                    case RunResourcePhase.Begin when identity is not null && pending is null:
                        if (row.Snapshot is not null || row.Identity is not null
                            || (row.Operation != "consult" && row.Operation != "dispatch" && row.Operation != "verify" && row.Operation != "checkpoint"))
                            throw new InvalidDataException("begin");
                        if (row.Operation == "consult")
                        {
                            var key = row.Consultation ?? throw new InvalidDataException("consultation-key");
                            if (key.RunId != identity.RunId || key.ContractViewRef != identity.ContractViewRef
                                || string.IsNullOrWhiteSpace(key.DecisionBoundaryId)
                                || string.IsNullOrWhiteSpace(key.ConsultationCorrelationId)
                                || string.IsNullOrWhiteSpace(row.RequestFingerprint) || !consultations.Add(key))
                                throw new InvalidDataException("unsupported-or-conflicting-consultation-view");
                        }
                        else if (row.Operation == "dispatch" && string.IsNullOrWhiteSpace(row.IntentId))
                            throw new InvalidDataException("intent");
                        else if (row.Operation == "verify" && (string.IsNullOrWhiteSpace(row.ReceiptId)
                            || !snapshot!.ReceiptIds.Contains(row.ReceiptId))) throw new InvalidDataException("receipt");
                        pending = row;
                        break;
                    case RunResourcePhase.Confirm when pending is not null:
                        if (row.Operation != pending.Operation || row.Consultation != pending.Consultation
                            || row.RequestFingerprint != pending.RequestFingerprint || row.IntentId != pending.IntentId
                            || row.ReceiptId != pending.ReceiptId || row.Identity is not null)
                            throw new InvalidDataException("confirmation-correlation");
                        var next = row.Snapshot ?? throw new InvalidDataException("confirmation-state");
                        Validate(next, identity!);
                        if (pending.Operation == "checkpoint" && ((row.RunHistoryPosition is null && row.EvidenceHistoryPosition is null && row.WorldHistoryPosition is null)
                            || JsonSerializer.Serialize(next with { Terminal = snapshot!.Terminal }) != JsonSerializer.Serialize(snapshot)))
                            throw new InvalidDataException("checkpoint-accounting-change");
                        if (next.StepsConsumed < snapshot!.StepsConsumed || next.RoundsConsumed < snapshot.RoundsConsumed
                            || next.GrantedSteps < snapshot.GrantedSteps || next.GrantedRounds < snapshot.GrantedRounds
                            || next.StepsConsumed - snapshot.StepsConsumed > (pending.Operation == "dispatch" ? 1 : 0)
                            || next.RoundsConsumed - snapshot.RoundsConsumed > (pending.Operation == "consult" ? 1 : 0))
                            throw new InvalidDataException("resource-transition");
                        snapshot = next;
                        pending = null;
                        break;
                    default: throw new InvalidDataException("record-phase");
                }
            }
            if (identity is null) return new(null, null, null, false, "resource-history-missing");
            var reason = pending is not null ? "resource-operation-unconfirmed"
                : snapshot!.PendingConsultation ? "consultation-pending"
                : snapshot.UnresolvedEffect ? "effect-unresolved"
                : binding is null ? "resource-identity-unverified"
                : identity != binding.Identity ? "resource-identity-mismatch"
                : binding.RecordingId != recordingId ? "resource-recording-mismatch"
                : !sourcePrefixVerified ? "resource-sources-unverified" : null;
            return new(identity, snapshot, pending?.Operation, reason is null, reason)
            { RunHistoryPosition = position, EvidenceHistoryPosition = evidencePosition, WorldHistoryPosition = worldPosition,
                RecordingId = recordingId, LastSequence = records.Count };
        }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException or ArgumentException or NullReferenceException)
        { return new(identity, snapshot, pending?.Operation, false, "resource-history-invalid:" + ex.Message); }
    }

    private static void Validate(RunResourceSnapshot state, RunResourceIdentity identity)
    {
        if (state.StepsConsumed < 0 || state.RoundsConsumed < 0 || state.GrantedSteps < 0 || state.GrantedRounds < 0
            || state.ReceiptIds.Distinct(StringComparer.Ordinal).Count() != state.StepsConsumed
            || state.ReceiptIds.Count != state.StepsConsumed
            || state.Renewals.Any(r => r.Granted && (r.AdditionalSteps < 0 || r.AdditionalRounds < 0 || !r.ReviewComplete))
            || state.Renewals.Where(r => r.Granted).Sum(r => (long)r.AdditionalSteps) != state.GrantedSteps
            || state.Renewals.Where(r => r.Granted).Sum(r => (long)r.AdditionalRounds) != state.GrantedRounds)
            throw new InvalidDataException("resource-facts");
        var steps = checked(identity.InitialSteps + state.GrantedSteps - state.StepsConsumed);
        var rounds = checked(identity.InitialRounds + state.GrantedRounds - state.RoundsConsumed);
        if (steps < 0 || rounds < 0) throw new InvalidDataException("negative-balance");
    }
}
