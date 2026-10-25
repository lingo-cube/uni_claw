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

internal static class RunAsyncProbe
{
    internal static async Task Run(string mode, string directory, string runId)
    {
        if (mode == "read-run-async")
        {
            var read = new FileRunRecoveryArchive(directory).RestoreRunState(runId);
            using var original = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"), true);
            File.WriteAllText(Path.Combine(directory, "run-reader.json"), JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, read.RunId, read.IsReadOnly, read.IsTerminal, read.HistoryPosition,
                read.TraversalHistoryAvailable, read.TraversalHandlers, read.TraversalOperations,
                Records = original.ReadRecords(), ContinuationSupported = false,
            }));
            return;
        }
        Directory.CreateDirectory(directory);
        await using var journal = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var held = new Held(journal); var run = new RunModel(held); var policy = new AgentPlanPolicy();
        var proposal = new ObservationProposal(new("ui.container.observed", "switch:entry@ready"), IngressKind.Observation,
            ObservationContext.External, new("offline-run-async-probe", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "capture:original", ["offline"]));
        var evidenceId = new EvidenceLedger().Admit(proposal).Admission.EvidenceId!;
        var container = "ctr-" + evidenceId[3..15];
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "ui.container.observed" },
            new Seed(), new Occurrence(container)), DisabledRunTrace.Instance, run, new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect(), execution));
        var consultations = 0; var handlers = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = resources, ConsultationJournal = p25, RecoveryArchive = new FileRunRecoveryArchive(directory),
            NextInput = directive => directive.Context == ObservationContext.External ? new RunDriverInput.Observation([proposal]) : null,
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "original page" },
            ProjectTraversal = _ =>
            {
                var revision = kernel.CurrentBelief!; var occurrence = revision.Occurrences!.Single();
                return new(new(revision.RevisionId, $"{kernel.RunId}:{kernel.RunView!.Version}", container, revision.Containers!, revision.Relations!, []),
                    [new(new("entry", occurrence.Role, occurrence.SemanticDescriptor!, PlanCandidateDisposition.Ready, occurrence.EvidenceBasis),
                        new Dictionary<string, PolicyTruth> { ["always"] = PolicyTruth.Satisfied })], false, revision.EvidenceBasis.ToArray());
            },
            ConsultAgent = context =>
            {
                consultations++;
                return new AgentDecision.Plan(context.DecisionId,
                    new([new PlanItem.TraverseItem("current", [new("r1", "always", new("inspect", TraversalOperationKind.ReadOnly, null,
                        [new("english", "runtime.language-inspector", "1.0", "English")], []))], 0,
                        new("switch", "entry", "tap", null))], "offline traversal", "run-async-plan", MaxLocalOperations: 16));
            },
            CanBindPlanLanguage = _ => true,
            InspectPlanLanguageAsync = (_, _, _) => { handlers++; throw new InvalidOperationException("unacknowledged Run must not call handler"); },
        });
        if (!(await kernel.AdmitContractAsync(new("run-async-v1", "original offline traversal", new HashSet<string> { "ui.container.observed" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], MaxConsultations: 4, MaxTotalSteps: 4,
            TaskInstanceId: "run-task", ProductSessionId: "run-session", ContractRef: "run-contract"))).Accepted || !driver.Activate().Accepted)
            throw new InvalidOperationException("original admission failed");
        var pending = driver.DriveAsync();
        await Task.WhenAny(held.Committed.Task, pending).WaitAsync(TimeSpan.FromSeconds(30));
        if (pending.IsCompleted)
        {
            var stopped = await pending;
            throw new InvalidOperationException($"Driver stopped before held Run commit: {stopped.Status}:{stopped.Reason}; {JsonSerializer.Serialize(driver.PlanExecution)}");
        }
        if (pending.IsCompleted || handlers != 0) throw new InvalidOperationException("Run acknowledgement bypassed");
        var rows = journal.ReadRecords();
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new { Rows = rows }));
        File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
        {
            ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, DriverPending = !pending.IsCompleted,
            OwnerPosition = run.HistoryPosition, SourceRows = rows.Count, Handlers = handlers, Consultations = consultations,
            PublishedHandlers = run.TraversalHandlers.Count, PublishedOperations = run.TraversalOperations.Count,
        }));
        File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
        await pending;
        throw new InvalidOperationException("lost pending Run acknowledgement");
    }

    private sealed class Held(IAsyncRunStateJournal inner) : IAsyncRunStateJournal
    {
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _acknowledge = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CanInitialize => inner.CanInitialize;
        public string RecordingId => inner.RecordingId;
        public IReadOnlyList<RunHistoryRecord> ReadRecords() => inner.ReadRecords();
        public void Append(RunHistoryRecord record) => throw new InvalidOperationException("synchronous Run storage");
        public async ValueTask AppendAsync(RunHistoryRecord record, CancellationToken cancellationToken = default)
        {
            await inner.AppendAsync(record, cancellationToken);
            if (record.Traversal?.Handler?.Status != PlanHandlerCallStatus.Running) return;
            Committed.TrySetResult();
            await _acknowledge.Task;
        }
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class NoEffect : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no Effect allowed"); }
    private sealed class Occurrence(string container) : IUiObservationStrategy
    { public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) => [new(container, "switch", "entry", "ready")]; }
    private sealed class Seed : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => input.Previous is null
            ? new(AssociationDispositionKind.New, null, [new("(new)", [input.Current.EvidenceId], [])], [], "original seed")
            : new(AssociationDispositionKind.Insufficient, null, [], [], "seed once");
    }
}
