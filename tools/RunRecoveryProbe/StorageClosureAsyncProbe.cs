using System.Diagnostics;
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

internal static class StorageClosureAsyncProbe
{
    internal static async Task Run(string mode, string directory, string argument)
    {
        if (mode == "read-storage-closure")
        {
            var source = new FileRunRecoveryArchive(directory); var original = source.Read(); var restoredRun = source.RestoreRunState(argument);
            var joint = source.InspectWorldRecovery(argument);
            File.WriteAllText(Path.Combine(directory, "reader.json"), JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, Original = original, restoredRun.RunId, restoredRun.IsReadOnly, restoredRun.IsTerminal,
                joint.HistoriesConsistent, joint.ContinuationSupported, ResourcePosition = joint.RunResourcesAndEvidence.RunAndResources.Resources.LastSequence,
                WorldPosition = joint.World?.Position,
            }));
            return;
        }
        Directory.CreateDirectory(directory); var initialization = new Dictionary<string, double>(); var timer = Stopwatch.StartNew();
        await using var evidence = await FileEvidenceJournal.CreateAsync(Path.Combine(directory, "evidence.journal.jsonl"));
        initialization["evidenceFile"] = timer.Elapsed.TotalMilliseconds; timer.Restart();
        await using var worlds = await FileWorldJournal.CreateAsync(Path.Combine(directory, "world.journal.jsonl"));
        initialization["worldFile"] = timer.Elapsed.TotalMilliseconds;
        await using var states = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        await using var controls = new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"));
        await using var plans = new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"));
        await using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        await using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        await using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var archive = new FileRunRecoveryArchive(directory); var held = new HeldArchive(archive, argument == "archive");
        var seed = new ObservationProposal(new("ui.container.observed", "switch:entry@ready"), IngressKind.Observation,
            ObservationContext.External, new("offline-storage-closure", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var eid = new EvidenceLedger().Admit(seed).Admission.EvidenceId!; var container = "ctr-" + eid[3..15];
        var policy = new AgentPlanPolicy(); var delivery = new Delivery();
        timer.Restart();
        var world = await WorldModel.CreateAsync(new HashSet<string> { "ui.container.observed" }, new Seed(), new Occurrence(container),
            history: new(worlds, states.RecordingId, evidence.RecordingId));
        initialization["worldOwner"] = timer.Elapsed.TotalMilliseconds;
        var run = new RunModel(states);
        var kernel = new UniKernel(new EvidenceLedger(evidence), world, DisabledRunTrace.Instance, run, new ControlLoop(policy, controls),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(delivery, execution));
        var consultations = 0; var observations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = resources, RecoveryArchive = held, ConsultationJournal = p25, RuntimePlanJournal = plans,
            NextInput = _ => ++observations == 1 ? new RunDriverInput.Observation([seed]) : null,
            ConsultAgent = context => { consultations++; return new AgentDecision.Act(new(context.DecisionId, [new("switch", "entry", "tap", null)], "original")); },
        });
        if (!(await kernel.AdmitContractAsync(new("closure-native-v1", "original async closure", new HashSet<string> { "ui.container.observed" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract"))).Accepted || !driver.Activate().Accepted)
            throw new InvalidOperationException("original admission failed");
        timer.Restart(); var pending = driver.DriveAsync();
        if (argument == "archive")
        {
            await Task.WhenAny(held.Committed.Task, pending).WaitAsync(TimeSpan.FromSeconds(20));
            if (pending.IsCompleted) throw new InvalidOperationException("driver escaped archive wait: " + await pending);
            File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(archive.Read()));
            File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, kernel.RunId, run.IsTerminal, DriverPending = !pending.IsCompleted,
                Consultations = consultations, Deliveries = delivery.Calls, Observations = observations,
                ResourceBound = driver.ResourceBinding is not null, ArchiveStatistics = archive.WriteStatistics,
            }));
            File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json")); await pending;
            throw new InvalidOperationException("lost acknowledgement wait");
        }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(20)); timer.Stop();
        var statistics = new Dictionary<string, object>
        {
            ["run"] = states.WriteStatistics, ["evidence"] = evidence.WriteStatistics, ["world"] = worlds.WriteStatistics,
            ["control"] = controls.WriteStatistics, ["plan"] = plans.WriteStatistics, ["resource"] = resources.WriteStatistics,
            ["consultation"] = p25.WriteStatistics, ["archive"] = archive.WriteStatistics,
            ["execution"] = new HistoryWriteCounts(execution.ManagedWriteCalls, execution.ManagedFlushCalls, 0, 0, execution.FrameBytesSubmitted,
                execution.CommittedBatches, execution.PeakBatchRecords, execution.PeakBatchBytes),
        };
        var rows = new Dictionary<string, int>
        {
            ["run"] = states.ReadRecords().Count, ["evidence"] = evidence.ReadRecords().Count, ["world"] = worlds.ReadRecords().Count,
            ["control"] = controls.ReadRecords().Count, ["plan"] = plans.ReadRecords().Count, ["resource"] = resources.ReadRecords().Count,
            ["consultation"] = File.ReadAllLines(Path.Combine(directory, "consultations.journal.jsonl")).Length,
            ["archive"] = File.ReadAllLines(Path.Combine(directory, "run-identity.json")).Length,
            ["execution"] = CountLengthFrames(Path.Combine(directory, "exec.journal")),
        };
        File.WriteAllText(Path.Combine(directory, "performance.json"), JsonSerializer.Serialize(new
        {
            SourceRows = rows, Statistics = statistics, InitializationElapsedMilliseconds = initialization,
            DriverElapsedMilliseconds = timer.Elapsed.TotalMilliseconds, DeliveryCalls = delivery.Calls, Receipts = kernel.EffectReceipts.Count,
            Result = result.Status.ToString(), CollectionWindowMilliseconds = 0, AdmissionQueueRecords = 0,
            InFlightLimitPerOwner = 1,
            Qualification = "One actual Driver, nine native writers; measured elapsed includes CPU and awaited IO, not a latency improvement claim. Native managed calls, not disk syscalls. Evidence/World seq0 markers are extra frames, not semantic record batches.",
        }));
    }
    private static int CountLengthFrames(string path)
    {
        using var stream = File.OpenRead(path); using var reader = new BinaryReader(stream); var count = 0;
        while (stream.Position < stream.Length) { var bytes = reader.ReadBytes(4); if (bytes.Length != 4) throw new InvalidDataException("header");
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes); if (length <= 0 || length > stream.Length - stream.Position) throw new InvalidDataException("frame");
            stream.Position += length; count++; }
        return count;
    }
    private sealed class HeldArchive(IAsyncRunRecoveryArchive inner, bool hold) : IAsyncRunRecoveryArchive
    {
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Initialize(RunRecoveryRecord record) => throw new IOException("sync archive");
        public RunRecoveryRecord Read() => inner.Read();
        public async ValueTask InitializeAsync(RunRecoveryRecord record, CancellationToken cancellationToken = default)
        { await inner.InitializeAsync(record, cancellationToken); if (hold) { Committed.TrySetResult(); await new TaskCompletionSource().Task; } }
    }
    private sealed class Delivery : IEffectDriver
    { public int Calls { get; private set; } public DispatchResult Deliver(DispatchRequest request) { Calls++; return new(DispatchOutcome.DeliveryCompleted, "original", DateTimeOffset.Parse("2026-10-25T00:00:00Z")); } }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class Occurrence(string container) : IUiObservationStrategy
    { public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) => [new(container, "switch", "entry", "ready")]; }
    private sealed class Seed : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => input.Previous is null
            ? new(AssociationDispositionKind.New, null, [new("(new)", [input.Current.EvidenceId], [])], [], "seed")
            : new(AssociationDispositionKind.Insufficient, null, [], [], "seed once");
    }
}
