using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Runtime;

public sealed partial class KernelRunDriver
{
    /// <summary>Available only after this original driver initialized accounting.
    /// Existing histories never mint a new binding; this does not authorize continuation.</summary>
    public RunResourceBinding? ResourceBinding { get; private set; }

    private bool _resourceReady;
    private int _resourceSequence;
    private RunResourceRecord? _resourceOperation;
    private bool _resourceVerificationSettled;
    private string? _resourceFailure;
    private UniClaw.Kernel.Run.RunHistoryPosition? _resourceRunPosition;
    private EvidenceHistoryPosition? _resourceEvidencePosition;
    private WorldHistoryPosition? _resourceWorldPosition;
    private sealed class ResourceRecordingException : Exception;

    private async ValueTask EnsureResourceRecordingAsync()
    {
        if (_inputs.ResourceJournal is not { } journal) return;
        if (_resourceFailure is not null) throw new ResourceRecordingException();
        if (_resourceReady) return;
        var view = _kernel.RunView!;
        try
        {
            var records = journal.ReadRecords();
            if (records.Count > 0)
                throw new InvalidOperationException("resource-recovery-read-only");
            if (!journal.CanInitialize || _kernel.EffectReceipts.Count != 0
                || _consultationJournal.HasPending(_kernel.RunId, ResourceViewRef)
                || _consultationJournal.ConsumedCount(_kernel.RunId, ResourceViewRef) != 0)
                throw new InvalidOperationException("resource-history-uninitialized-or-legacy");
            var policy = RunResourcePolicyRecord.Capture(_plan);
            var identity = new RunResourceIdentity(_kernel.RunId, view.TaskInstanceId!, view.ProductSessionId!,
                ResourceViewRef, view.Version, view.ContractGeneration,
                policy.Fingerprint(),
                view.MaxTotalSteps, view.MaxConsultations);
            var position = _kernel.RunHistoryPosition;
            await AppendResourceAsync(journal, new(1, RunResourcePhase.Initialize, "initialize", identity, CaptureResourceFacts(), RunHistoryPosition: position,
                EvidenceHistoryPosition: _kernel.EvidenceHistoryPosition, WorldHistoryPosition: _kernel.WorldHistoryPosition));
            _resourceSequence = 1;
            _resourceRunPosition = position;
            _resourceEvidencePosition = _kernel.EvidenceHistoryPosition;
            _resourceWorldPosition = _kernel.WorldHistoryPosition;
            if (_inputs.RecoveryArchive is { } archive)
            {
                if (journal.RecordingId is not { Length: > 0 } recordingId)
                    throw new InvalidOperationException("resource-recording-identity-missing");
                var original = new RunRecoveryRecord(RunResourceAccounting.RecoverySchema, _kernel.CaptureRunIdentity(), identity, policy, recordingId,
                    _kernel.RunStateRecordingId, _kernel.EvidenceRecordingId, _kernel.WorldHistoryPosition?.RecordingId,
                    _kernel.ControlTraversalHistoryPosition?.RecordingId, _inputs.RuntimePlanJournal?.RecordingId);
                if (archive is IAsyncRunRecoveryArchive native) await native.InitializeAsync(original).ConfigureAwait(false);
                else archive.Initialize(original);
            }
            _resourceReady = true;
            ResourceBinding = new RunResourceBinding(identity, journal.RecordingId);
        }
        catch (Exception ex) { FailResourceRecording(ex); }
    }

    private static async ValueTask AppendResourceAsync(IRunResourceJournal journal, RunResourceRecord row)
    {
        if (journal is IAsyncRunResourceJournal native) await native.AppendAsync(row).ConfigureAwait(false);
        else journal.Append(row);
    }

    private string ResourceViewRef => _kernel.RunView!.AcceptedContractViewRef
        ?? _kernel.RunView.ContractRef ?? _kernel.RunView.Version;

    private async ValueTask BeginResourceOperationAsync(string operation, ConsultationKey? key = null,
        string? fingerprint = null, string? intentId = null, string? receiptId = null)
    {
        if (_inputs.ResourceJournal is not { } journal) return;
        await CompleteResourceOperationAsync();
        if (_resourceOperation is { } pending)
        {
            // WaitingForInput re-enters the same verification, without either
            // confirming it or opening a second operation.
            if (pending.Operation == "verify" && operation == "verify" && pending.ReceiptId == receiptId) return;
            FailResourceRecording(new InvalidOperationException("resource-operation-not-settled"));
        }
        try
        {
            var row = new RunResourceRecord(_resourceSequence + 1, RunResourcePhase.Begin,
                operation, Consultation: key, RequestFingerprint: fingerprint, IntentId: intentId, ReceiptId: receiptId,
                RunHistoryPosition: _kernel.RunHistoryPosition, EvidenceHistoryPosition: _kernel.EvidenceHistoryPosition,
                WorldHistoryPosition: _kernel.WorldHistoryPosition);
            await AppendResourceAsync(journal, row);
            _resourceSequence = row.Sequence;
            _resourceRunPosition = row.RunHistoryPosition;
            _resourceEvidencePosition = row.EvidenceHistoryPosition;
            _resourceWorldPosition = row.WorldHistoryPosition;
            _resourceOperation = row;
            _resourceVerificationSettled = false;
        }
        catch (Exception ex) { FailResourceRecording(ex); }
    }

    private async ValueTask CompleteResourceOperationAsync()
    {
        if (_resourceFailure is not null) throw new ResourceRecordingException();
        if (_resourceOperation is not { } operation || _inputs.ResourceJournal is not { } journal) return;
        if (operation.Operation == "verify" && !_resourceVerificationSettled) return;
        try
        {
            var row = operation with { Sequence = _resourceSequence + 1, Phase = RunResourcePhase.Confirm,
                Snapshot = CaptureResourceFacts(), RunHistoryPosition = _kernel.RunHistoryPosition,
                EvidenceHistoryPosition = _kernel.EvidenceHistoryPosition, WorldHistoryPosition = _kernel.WorldHistoryPosition };
            await AppendResourceAsync(journal, row);
            _resourceSequence = row.Sequence;
            _resourceRunPosition = row.RunHistoryPosition;
            _resourceEvidencePosition = row.EvidenceHistoryPosition;
            _resourceWorldPosition = row.WorldHistoryPosition;
            _resourceOperation = null;
        }
        catch (Exception ex) { FailResourceRecording(ex); }
    }

    private async ValueTask CheckpointOwnerHistoriesAsync()
    {
        if (_inputs.ResourceJournal is null || !_resourceReady || _resourceOperation is not null
            || (_kernel.RunHistoryPosition == _resourceRunPosition
                && _kernel.EvidenceHistoryPosition == _resourceEvidencePosition
                && _kernel.WorldHistoryPosition == _resourceWorldPosition)) return;
        await BeginResourceOperationAsync("checkpoint");
        await CompleteResourceOperationAsync();
    }

    private RunResourceSnapshot CaptureResourceFacts() => new(_stepsDispatched,
        _consultationJournal.ConsumedCount(_kernel.RunId, ResourceViewRef),
        _grantedRenewalSteps, _grantedRenewalRounds,
        _kernel.EffectReceipts.Select(receipt => receipt.ReceiptId).ToArray(),
        _renewalLedger.ToArray(), _budgetReviewHistory.ToArray(),
        _lastGrantedProgressFactKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
        _fundedItemProgress.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal),
        _inFlightReceiptId is not null || _kernel.EffectReceipts.Any(receipt => receipt.Outcome.IsUnconfirmedOutcome()),
        _consultationJournal.HasPending(_kernel.RunId, ResourceViewRef), _kernel.IsRunTerminal);

    private void FailResourceRecording(Exception exception)
    {
        _resourceFailure ??= "resource-accounting-failed:" + (exception is InvalidOperationException
            ? exception.Message : exception.GetType().Name);
        throw new ResourceRecordingException();
    }
}
