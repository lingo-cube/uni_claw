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

internal static class ControlPlanAsyncProbe
{
    internal static async Task Run(string mode, string directory, string argument)
    {
        if (mode == "read-control-plan-async")
        {
            var archive = new FileRunRecoveryArchive(directory); var restoredRun = archive.RestoreRunState(argument);
            var readControl = archive.ReadControlTraversalHistory(argument); var readPlan = archive.ReadRuntimePlanHistory(argument);
            File.WriteAllText(Path.Combine(directory, "control-plan-reader.json"), JsonSerializer.Serialize(new
            {
                ProcessId = Environment.ProcessId, restoredRun.RunId, restoredRun.IsReadOnly, restoredRun.IsTerminal,
                ControlPosition = readControl.Position, ControlRecords = readControl.Records, ControlState = readControl.State,
                PlanPosition = readPlan.Position, PlanRecords = readPlan.Records,
                ContinuationSupported = readControl.ContinuationSupported || readPlan.ContinuationSupported,
            }));
            return;
        }
        var measurement = argument is "single" or "batch";
        Directory.CreateDirectory(directory);
        await using var states = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        await using var controlSource = new FileControlTraversalJournal(Path.Combine(directory, "control-traversal.journal.jsonl"));
        await using var planSource = new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"));
        using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldControl = new HeldControl(controlSource, committed, argument == "control", argument != "single");
        var heldPlan = new HeldPlan(planSource, committed, argument == "plan");
        var run = new RunModel(states); var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, heldControl);
        var seed = new ObservationProposal(new("ui.container.observed", "switch:entry@ready"), IngressKind.Observation,
            ObservationContext.External, new("offline-group-async", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var evidenceId = new EvidenceLedger().Admit(seed).Admission.EvidenceId!; var container = "ctr-" + evidenceId[3..15];
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "ui.container.observed" },
            new Seed(), new Occurrence(container)), DisabledRunTrace.Instance, run, control,
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect(), execution));
        var handlers = 0; var consultations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            RuntimePlanJournal = heldPlan, ResourceJournal = resources, ConsultationJournal = p25, RecoveryArchive = new FileRunRecoveryArchive(directory),
            NextInput = directive => directive.Context == ObservationContext.External ? new RunDriverInput.Observation([seed]) : null,
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
                if (++consultations > 1) return new AgentDecision.Blocked(context.DecisionId, "done");
                return new AgentDecision.Plan(context.DecisionId,
                    new([new PlanItem.TraverseItem("current", [new("r1", "always", new("inspect", TraversalOperationKind.ReadOnly, null,
                        (measurement ? [] : new PlanLanguageHandler[] { new("english", "runtime.language-inspector", "1.0", "English") }), []))], 0,
                        new("switch", "entry", "tap", null))], "offline traversal", "group-async-plan", MaxLocalOperations: 16));
            },
            CanBindPlanLanguage = _ => true,
            InspectPlanLanguageAsync = (_, _, _) => { handlers++; throw new InvalidOperationException("unacknowledged owner must not call handler"); },
        });
        if (!(await kernel.AdmitContractAsync(new("group-async-v1", "original control and plan", new HashSet<string> { "ui.container.observed" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], MaxConsultations: 4, MaxTotalSteps: 4,
            TaskInstanceId: "group-task", ProductSessionId: "group-session", ContractRef: "group-contract"))).Accepted || !driver.Activate().Accepted)
            throw new InvalidOperationException("original admission failed");
        var pending = driver.DriveAsync();
        if (measurement)
        {
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(20));
            File.WriteAllText(Path.Combine(directory, "measurement.json"), JsonSerializer.Serialize(new
            {
                ControlRows = controlSource.ReadRecords().Count, ControlOperations = controlSource.ReadRecords().Select(row => row.Operation).ToArray(),
                ControlStatistics = controlSource.WriteStatistics, PlanStatistics = planSource.WriteStatistics,
                CompletedOperations = run.TraversalOperations.Count(operation => operation.Completed),
                Result = result.Status.ToString(), Qualification = "Actual same Driver scenario, one native writer with batch support masked vs native batch; managed calls, not physical syscalls.",
            }));
            return;
        }
        await Task.WhenAny(committed.Task, pending).WaitAsync(TimeSpan.FromSeconds(30));
        if (pending.IsCompleted)
        {
            var stopped = await pending;
            throw new InvalidOperationException($"Driver escaped held owner: {stopped.Status}:{stopped.Reason}");
        }
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        { ControlRows = controlSource.ReadRecords(), PlanRows = planSource.ReadRecords() }));
        File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
        {
            ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, Kind = argument, DriverPending = !pending.IsCompleted,
            OwnerControlPosition = control.TraversalHistory!.Position, OwnerPlanPosition = driver.RuntimePlanHistory!.Position,
            SourceControlRows = controlSource.ReadRecords().Count, SourcePlanRows = planSource.ReadRecords().Count,
            ControlStatistics = controlSource.WriteStatistics, PlanStatistics = planSource.WriteStatistics,
            PublishedPlan = driver.PlanExecution is not null, PublishedOperations = run.TraversalOperations.Count,
            Handlers = handlers, Consultations = consultations,
        }));
        File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
        await pending;
        throw new InvalidOperationException("lost original acknowledgement wait");
    }
    private sealed class HeldControl(IAsyncControlTraversalJournal inner, TaskCompletionSource committed, bool hold, bool batch) : IAsyncControlTraversalJournal
    {
        private readonly TaskCompletionSource _acknowledge = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CanInitialize => inner.CanInitialize;
        public string RecordingId => inner.RecordingId;
        public IReadOnlyList<ControlTraversalHistoryRecord> ReadRecords() => inner.ReadRecords();
        public bool CanAppendBatch => batch && inner.CanAppendBatch;
        public ValueTask AppendBatchAsync(IReadOnlyList<ControlTraversalHistoryRecord> rows, CancellationToken cancellationToken = default) =>
            inner.AppendBatchAsync(rows, cancellationToken);
        public void Append(ControlTraversalHistoryRecord row) => throw new InvalidOperationException("synchronous control write");
        public async ValueTask AppendAsync(ControlTraversalHistoryRecord row, CancellationToken cancellationToken = default)
        {
            await inner.AppendAsync(row, cancellationToken);
            if (!hold || row.Operation != "select") return;
            committed.TrySetResult(); await _acknowledge.Task;
        }
    }
    private sealed class HeldPlan(IAsyncRuntimePlanJournal inner, TaskCompletionSource committed, bool hold) : IAsyncRuntimePlanJournal
    {
        private readonly TaskCompletionSource _acknowledge = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CanInitialize => inner.CanInitialize;
        public string RecordingId => inner.RecordingId;
        public IReadOnlyList<RuntimePlanHistoryRecord> ReadRecords() => inner.ReadRecords();
        public void Append(RuntimePlanHistoryRecord row) => throw new InvalidOperationException("synchronous plan write");
        public async ValueTask AppendAsync(RuntimePlanHistoryRecord row, CancellationToken cancellationToken = default)
        {
            await inner.AppendAsync(row, cancellationToken);
            if (!hold || row.Operation != "adopt") return;
            committed.TrySetResult(); await _acknowledge.Task;
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
