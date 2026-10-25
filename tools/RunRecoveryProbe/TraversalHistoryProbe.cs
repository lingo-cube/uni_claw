using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

internal static class TraversalHistoryProbe
{
    internal static async Task Run(string mode, string directory, string argument)
    {
        if (mode == "read-traversal")
        {
            var archive = new FileRunRecoveryArchive(directory);
            var read = archive.RestoreRunState(argument);
            File.WriteAllText(Path.Combine(directory, "traversal-reader.json"), JsonSerializer.Serialize(Snapshot(read)));
            var joint = archive.InspectRecovery(argument);
            File.WriteAllText(Path.Combine(directory, "traversal-resource-reader.json"), JsonSerializer.Serialize(new
            {
                joint.StateAndResourcesConsistent, joint.Reason, RunPosition = joint.Run?.HistoryPosition,
                ResourceRunPosition = joint.Resources.RunHistoryPosition, joint.Resources.Confirmed, joint.ContinuationSupported,
            }));
            return;
        }
        Directory.CreateDirectory(directory);
        using var journal = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var run = new RunModel(journal); var policy = new AgentPlanPolicy();
        var now = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        var proposal = new ObservationProposal(new("ui.container.observed", "switch:entry@ready"), IngressKind.Observation,
            ObservationContext.External, new("offline-traversal-probe", now, "capture:original", ["synthetic", "offline"]));
        var root = new EvidenceLedger().Admit(proposal).Admission.EvidenceId;
        var container = "ctr-" + root![3..15];
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "ui.container.observed" },
            new Seed(), new Occurrence(container)), DisabledRunTrace.Instance, run, new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect(), execution));
        var completion = new TaskCompletionSource<Finding?>(); PlanHandlerInput? callInput = null;
        var calls = 0; var consultations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = resources, ConsultationJournal = p25, RecoveryArchive = new FileRunRecoveryArchive(directory),
            NextInput = directive => directive.Context == ObservationContext.External ? new RunDriverInput.Observation([proposal]) : null,
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "original page" },
            ProjectTraversal = _ =>
            {
                var revision = kernel.CurrentBelief!; var occurrence = revision.Occurrences!.Single();
                return new(new(revision.RevisionId, $"{kernel.RunId}:traversal-probe-v1", container, revision.Containers!, revision.Relations!, []),
                    [new(new("entry", occurrence.Role, occurrence.SemanticDescriptor!, PlanCandidateDisposition.Ready, occurrence.EvidenceBasis),
                        new Dictionary<string, PolicyTruth> { ["always"] = PolicyTruth.Satisfied })], false, revision.EvidenceBasis.ToArray());
            },
            ConsultAgent = context => ++consultations == 1 ? new AgentDecision.Plan(context.DecisionId,
                new([new PlanItem.TraverseItem("current", [new("r1", "always", new("inspect", TraversalOperationKind.ReadOnly, null,
                    [new("english", "runtime.language-inspector", "1.0", "English")], []))], 0,
                    new("switch", "entry", "tap", null))], "offline original traversal", "original-plan", MaxLocalOperations: 16))
                : new AgentDecision.NoAction(new(context.DecisionId, "no further work")),
            CanBindPlanLanguage = _ => true, PlanHandlerTimeout = TimeSpan.FromSeconds(10),
            InspectPlanLanguageAsync = (_, input, _) => { calls++; callInput = input; return new(completion.Task); },
        });
        if (!kernel.AdmitContract(new("traversal-probe-v1", "original offline traversal", new HashSet<string> { "ui.container.observed" },
                new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], MaxConsultations: 4, MaxTotalSteps: 4,
                TaskInstanceId: "traversal-task", ProductSessionId: "traversal-session", ContractRef: "traversal-contract")).Accepted
            || !driver.Activate().Accepted) throw new InvalidOperationException("original admission/activation failed");
        var pending = driver.Drive();
        if (pending.Status != RunDriveStatus.WaitingForHandler)
            throw new InvalidOperationException($"original traversal must reach pending typed handler: {pending.Status}:{pending.Reason}; {JsonSerializer.Serialize(driver.PlanExecution)}");
        if (argument == "terminal")
        {
            completion.SetResult(new("original-finding", new(kernel.RunId!, domainCorrelation: callInput!.SourceRevisionId,
                operationId: callInput.OperationId), "1.0", "runtime.language-inspector", FindingDisposition.Violation,
                CapabilityStatus.Completed, "original violation remains a domain result"));
            await driver.DriveAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        if (run.IsTerminal || calls != 1) throw new InvalidOperationException("original Run must remain nonterminal with one inspection");
        File.WriteAllText(Path.Combine(directory, "traversal-original.json"), JsonSerializer.Serialize(Snapshot(run)));
        File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
        { ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, Calls = calls, Consultations = consultations, Deliveries = 0, Mode = argument }));
        File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    private static object Snapshot(RunModel run) => new
    {
        ProcessId = Environment.ProcessId, run.RunId, run.IsReadOnly, run.IsTerminal, run.TraversalHistoryAvailable,
        run.HistoryPosition, CoreHistoryCount = run.History.Count, Progress = run.State!.Progress,
        run.TraversalExecution, run.TraversalHistory, run.TraversalHandlers, run.TraversalOperations,
        run.TraversalGaps, run.TraversalCompletedWork,
    };
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
