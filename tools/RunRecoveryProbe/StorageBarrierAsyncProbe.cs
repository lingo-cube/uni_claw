using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

internal static class StorageBarrierAsyncProbe
{
    internal static async Task Run(string mode, string directory, string kind)
    {
        if (mode == "read-storage-barrier")
        {
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "expected.json")));
            var key = expected.RootElement.GetProperty("Key").Deserialize<ConsultationKey>();
            var id = expected.RootElement.GetProperty("AttemptId").GetString();
            using var resourceRead = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"), true);
            using var consultationRead = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
            using var executionRead = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
            var runRead = new FileRunRecoveryArchive(directory).RestoreRunState(kind);
            File.WriteAllText(Path.Combine(directory, "reader.json"), JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, runRead.RunId, runRead.IsReadOnly,
                ResourceRows = resourceRead.ReadRecords(), Consultation = key is null ? null : consultationRead.Find(key),
                Execution = id is null ? null : executionRead.GetAttempt(id), ContinuationSupported = false,
            }));
            return;
        }
        Directory.CreateDirectory(directory);
        var measurement = kind is "single" or "batch";
        await using var states = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        await using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        await using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        await using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdResource = new HeldResource(resources, entered, kind == "resource");
        var holdP25 = new HeldP25(p25, entered, kind == "consultation");
        var holdExecution = new HeldExecution(execution, entered, kind);
        var run = new RunModel(states); var policy = new AgentPlanPolicy(); var delivery = new Delivery();
        var seed = new ObservationProposal(new("ui.container.observed", "switch:entry@ready"), IngressKind.Observation,
            ObservationContext.External, new("offline-storage-barrier", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var eid = new EvidenceLedger().Admit(seed).Admission.EvidenceId!; var container = "ctr-" + eid[3..15];
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "ui.container.observed" },
            new Seed(), new Occurrence(container)), DisabledRunTrace.Instance, run, new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(delivery, holdExecution));
        var consultations = 0; var observations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = holdResource, ConsultationJournal = holdP25, RecoveryArchive = new FileRunRecoveryArchive(directory),
            NextInput = directive => ++observations == 1 ? new RunDriverInput.Observation([seed]) : null,
            ConsultAgent = context => { consultations++; return new AgentDecision.Act(new(context.DecisionId, [new("switch", "entry", "tap", null)], "original actual delivery")); },
        });
        if (!(await kernel.AdmitContractAsync(new("barrier-native-v1", "original storage barriers", new HashSet<string> { "ui.container.observed" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract"))).Accepted
            || !driver.Activate().Accepted) throw new InvalidOperationException("admission failed");
        var pending = driver.DriveAsync();
        if (measurement)
        {
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(20));
            File.WriteAllText(Path.Combine(directory, "measurement.json"), JsonSerializer.Serialize(new
            {
                delivery.Calls, Receipts = kernel.EffectReceipts.Count, Attempt = execution.GetAttempt(holdExecution.AttemptId!),
                execution.ManagedWriteCalls, execution.ManagedFlushCalls, execution.FrameBytesSubmitted,
                execution.CommittedBatches, execution.PeakBatchRecords, execution.PeakBatchBytes,
                Result = result.Status.ToString(), Qualification = "Same actual Driver delivery, result pair batched vs sequential; managed calls only.",
            }));
            return;
        }
        await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(20));
        if (pending.IsCompleted) throw new InvalidOperationException("driver escaped target barrier: " + await pending);
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            ResourceRows = resources.ReadRecords(), Consultation = holdP25.Key is null ? null : p25.Find(holdP25.Key),
            Execution = holdExecution.AttemptId is null ? null : execution.GetAttempt(holdExecution.AttemptId),
            holdP25.Key, holdExecution.AttemptId,
        }));
        File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
        {
            ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, DriverPending = !pending.IsCompleted,
            Kind = kind, Consultations = consultations, Deliveries = delivery.Calls, Observations = observations,
            PublishedReceipts = kernel.EffectReceipts.Count, ResourceRows = resources.ReadRecords().Count,
            ResourceBound = driver.ResourceBinding is not null, execution.AttemptCount, execution.ManagedFlushCalls,
        }));
        File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
        await pending; throw new InvalidOperationException("lost held acknowledgement");
    }
    private sealed class HeldResource(IAsyncRunResourceJournal inner, TaskCompletionSource entered, bool hold) : IAsyncRunResourceJournal
    {
        public bool CanInitialize => inner.CanInitialize;
        public string? RecordingId => inner.RecordingId;
        public IReadOnlyList<RunResourceRecord> ReadRecords() => inner.ReadRecords();
        public void Append(RunResourceRecord record) => throw new IOException("sync resource");
        public async ValueTask AppendAsync(RunResourceRecord row, CancellationToken cancellationToken = default)
        { await inner.AppendAsync(row, cancellationToken); if (hold && row.Phase == RunResourcePhase.Begin && row.Operation == "consult") { entered.TrySetResult(); await new TaskCompletionSource().Task; } }
    }
    private sealed class HeldP25(IAsyncConsultationJournal inner, TaskCompletionSource entered, bool hold) : IAsyncConsultationJournal
    {
        public ConsultationKey? Key { get; private set; }
        public ConsultationJournalEntry? Find(ConsultationKey key) => inner.Find(key);
        public int ConsumedCount(string run, string view) => inner.ConsumedCount(run, view);
        public bool HasPending(string run, string view) => inner.HasPending(run, view);
        public ConsultationJournalEntry Reserve(ConsultationKey key, string fp) => throw new IOException("sync reserve");
        public ConsultationJournalEntry Complete(ConsultationKey key, string fp, AgentDecision decision) => throw new IOException("sync complete");
        public ConsultationJournalEntry Reject(ConsultationKey key, string fp, string reason) => throw new IOException("sync reject");
        public async ValueTask<ConsultationJournalEntry> ReserveAsync(ConsultationKey key, string fp, CancellationToken cancellationToken = default)
        { Key = key; var row = await inner.ReserveAsync(key, fp, cancellationToken); if (hold) { entered.TrySetResult(); await new TaskCompletionSource().Task; } return row; }
        public ValueTask<ConsultationJournalEntry> CompleteAsync(ConsultationKey key, string fp, AgentDecision decision, CancellationToken cancellationToken = default) => inner.CompleteAsync(key, fp, decision, cancellationToken);
        public ValueTask<ConsultationJournalEntry> RejectAsync(ConsultationKey key, string fp, string reason, CancellationToken cancellationToken = default) => inner.RejectAsync(key, fp, reason, cancellationToken);
    }
    private sealed class HeldExecution(FileExecutionJournal inner, TaskCompletionSource entered, string kind) : IAsyncReliableExecutionSource
    {
        public string? AttemptId { get; private set; }
        public ExecutionCommit CommitPrepare(ExecutionRegistration row) => throw new IOException("sync prepare");
        public async ValueTask<ExecutionCommit> CommitPrepareAsync(ExecutionRegistration row, CancellationToken cancellationToken = default)
        { var result = await inner.CommitPrepareAsync(row, cancellationToken); AttemptId = result.AttemptId; if (kind == "prepare") { entered.TrySetResult(); await new TaskCompletionSource().Task; } return result; }
        public async ValueTask AppendResultAsync(string id, string note, EffectReceipt receipt, CancellationToken cancellationToken = default)
        {
            if (kind == "single") { await inner.AppendSubmissionAsync(id, note, cancellationToken); await inner.AppendReceiptAsync(id, receipt); }
            else await inner.AppendResultAsync(id, note, receipt, cancellationToken);
            if (kind == "result") { entered.TrySetResult(); await new TaskCompletionSource().Task; }
        }
        public ValueTask AppendSubmissionAsync(string id, string note, CancellationToken cancellationToken = default) => inner.AppendSubmissionAsync(id, note, cancellationToken);
        public ValueTask AppendReceiptAsync(string id, EffectReceipt? receipt, CancellationToken cancellationToken = default) => inner.AppendReceiptAsync(id, receipt, cancellationToken);
        public ValueTask AppendLateFeedbackAsync(string? id, ExecutionFeedback feedback, CancellationToken cancellationToken = default) => inner.AppendLateFeedbackAsync(id, feedback, cancellationToken);
        public ValueTask<string> LinkRetryAsync(string id, CancellationToken cancellationToken = default) => inner.LinkRetryAsync(id, cancellationToken);
        public ValueTask<ExecutionLink> LinkCompensationAsync(string id, CancellationToken cancellationToken = default) => inner.LinkCompensationAsync(id, cancellationToken);
        public void AppendSubmission(string id, string note) => throw new IOException("sync submission");
        public void AppendReceipt(string id, EffectReceipt? receipt) => throw new IOException("sync receipt");
        public void AppendLateFeedback(string? id, ExecutionFeedback feedback) => throw new IOException("sync feedback");
        public string LinkRetry(string id) => throw new IOException("sync retry");
        public ExecutionLink LinkCompensation(string id) => throw new IOException("sync compensation");
        public IReadOnlyList<PendingExecution> DiscoverPending() => inner.DiscoverPending();
        public ExecutionAttemptView? GetAttempt(string id) => inner.GetAttempt(id);
    }
    private sealed class Delivery : IEffectDriver
    { public int Calls { get; private set; } public DispatchResult Deliver(DispatchRequest request) { Calls++; return new(DispatchOutcome.DeliveryCompleted, "original delivery", DateTimeOffset.Parse("2026-10-25T00:00:00Z")); } }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class Occurrence(string container) : IUiObservationStrategy
    { public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) => [new(container, "switch", "entry", "ready")]; }
    private sealed class Seed : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => input.Previous is null
            ? new(AssociationDispositionKind.New, null, [new("(new)", [input.Current.EvidenceId], [])], [], "original seed")
            : new(AssociationDispositionKind.Insufficient, null, [], [], "seed once");
    }
}
