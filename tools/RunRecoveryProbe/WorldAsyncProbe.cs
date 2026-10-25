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

internal static class WorldAsyncProbe
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        using var states = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        await using var evidence = new FileEvidenceJournal(Path.Combine(directory, "evidence.journal.jsonl"));
        await using var original = new FileWorldJournal(Path.Combine(directory, "world.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var run = new RunModel(states); var policy = new AgentPlanPolicy(); var held = new Held(original);
        var scope = new HashSet<string> { "live.frame" }; var ledger = new EvidenceLedger(evidence);
        var world = new WorldModel(scope, history: new(held, states.RecordingId, evidence.RecordingId));
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, run, new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect(), execution));
        var seed = new ObservationProposal(new("live.frame", "original"), IngressKind.Observation, ObservationContext.External,
            new("world-async-probe", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ConsultationJournal = p25, ResourceJournal = resources, RecoveryArchive = new FileRunRecoveryArchive(directory),
            NextInput = _ => new RunDriverInput.Observation([seed]),
            ConsultAgent = _ => throw new InvalidOperationException("unacknowledged World must not consult"),
        });
        if (!(await kernel.AdmitContractAsync(new("world-async-v1", "original World operation", scope, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            TaskInstanceId: "world-task", ProductSessionId: "world-session", ContractRef: "world-contract"))).Accepted || !driver.Activate().Accepted)
            throw new InvalidOperationException("original admission failed");
        var pending = driver.DriveAsync();
        await held.Committed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        if (pending.IsCompleted) throw new InvalidOperationException("Driver escaped original World acknowledgement");
        var rows = original.ReadRecords();
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new { Rows = rows }));
        File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
        {
            ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, DriverPending = !pending.IsCompleted,
            WorldRecordingId = original.RecordingId, OwnerPosition = world.HistoryPosition, PublishedWorldRevisions = world.RevisionHistory.Count,
            Belief = kernel.CurrentBelief, EvidenceAdmissions = ledger.AdmissionLog.Count, SourceRows = rows.Count,
        }));
        File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
        var result = await pending;
        throw new InvalidOperationException("did not retain pending World acknowledgement: " + result.Reason);
    }
    private sealed class Held(IAsyncWorldJournal inner) : IAsyncWorldJournal
    {
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _acknowledge = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CanInitialize => inner.CanInitialize;
        public string RecordingId => inner.RecordingId;
        public IReadOnlyList<WorldHistoryRecord> ReadRecords() => inner.ReadRecords();
        public void Append(WorldHistoryRecord record)
        {
            if (record.Operation != "initialize") throw new InvalidOperationException("synchronous World operation");
            inner.Append(record);
        }
        public async ValueTask AppendOperationAsync(WorldHistoryRecord operation, IReadOnlyList<WorldBeliefRevision> revisions,
            CancellationToken cancellationToken = default)
        {
            await inner.AppendOperationAsync(operation, revisions, cancellationToken);
            if (operation.Operation != "reconcile") return;
            Committed.TrySetResult();
            await _acknowledge.Task;
        }
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class NoEffect : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no delivery"); }
}
