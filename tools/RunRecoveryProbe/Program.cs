using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Host;
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

var directory = Path.GetFullPath(args[1]);
if (args[0] is "write-coverage-delivery" or "retry-coverage-delivery")
{
    await CoverageReportProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] == "read-world-version")
{
    var reader = new WorldHistoryVersionReader(Path.Combine(directory, "world.journal.jsonl"), args[2]);
    var version = args.Length > 3 ? reader.ReadRevision(int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture)) : reader.ReadLatest();
    File.WriteAllText(Path.Combine(directory, "world-version-reader-result.json"), JsonSerializer.Serialize(new
    { ProcessId = Environment.ProcessId, reader.SourcePosition, reader.RevisionCount, reader.BaselineInterval, Version = version,
        FullWorldHistoryValidated = false, ContinuationSupported = false }));
    return;
}
if (args[0] == "measure-control-plan-growth")
{
    await ControlPlanGrowthProbe.Run(directory, args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
    return;
}
if (args[0] is "write-report-tool" or "read-report-tool")
{
    await ReportToolProbe.Run(args[0], directory);
    return;
}
if (args[0] is "write-report-delivery" or "read-report-delivery")
{
    await ReportDeliveryProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] is "write-report-tool" or "read-report-tool")
{
    await ReportToolProbe.Run(args[0], directory);
    return;
}
if (args[0] is "write-storage-closure" or "read-storage-closure")
{
    await StorageClosureAsyncProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] is "write-storage-barrier" or "read-storage-barrier")
{
    await StorageBarrierAsyncProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] is "write-control-plan-async" or "read-control-plan-async")
{
    await ControlPlanAsyncProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] is "write-run-async" or "read-run-async")
{
    await RunAsyncProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] == "write-world-async")
{
    await WorldAsyncProbe.Run(directory);
    return;
}
if (args[0] is "write-evidence-batch" or "read-evidence-batch")
{
    await EvidenceBatchProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] is "write-plan" or "read-plan")
{
    await RuntimePlanProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] is "write-control" or "read-control")
{
    await ControlTraversalProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] is "write-traversal" or "read-traversal")
{
    await TraversalHistoryProbe.Run(args[0], directory, args[2]);
    return;
}
if (args[0] == "read-four")
{
    var read = new FileRunRecoveryArchive(directory).InspectWorldRecovery(args[2]);
    File.WriteAllText(Path.Combine(directory, "four-reader-result.json"), JsonSerializer.Serialize(new
    {
        ProcessId = Environment.ProcessId, RunId = read.RunResourcesAndEvidence.RunAndResources.Run?.RunId,
        RunIsReadOnly = read.RunResourcesAndEvidence.RunAndResources.Run?.IsReadOnly,
        IsTerminal = read.RunResourcesAndEvidence.RunAndResources.Run?.IsTerminal,
        RunPosition = read.RunResourcesAndEvidence.RunAndResources.Run?.HistoryPosition,
        Resources = read.RunResourcesAndEvidence.RunAndResources.Resources,
        EvidenceIsReadOnly = read.RunResourcesAndEvidence.Evidence?.IsReadOnly,
        EvidencePosition = read.RunResourcesAndEvidence.Evidence?.HistoryPosition,
        ThreeHistoriesConsistent = read.RunResourcesAndEvidence.HistoriesConsistent,
        WorldPosition = read.World?.Position, WorldOperations = read.World?.Operations, WorldRevisions = read.World?.HistoricalRevisions,
        read.Point, read.HistoriesConsistent, read.Reason, read.ContinuationSupported,
    }));
    return;
}
if (args[0] == "read")
{
    var read = new FileRunRecoveryArchive(directory).Inspect(args[2]);
    File.WriteAllText(Path.Combine(directory, "reader-result.json"), JsonSerializer.Serialize(read));
    File.WriteAllText(Path.Combine(directory, "reader-process.json"), JsonSerializer.Serialize(new { ProcessId = Environment.ProcessId }));
    return;
}
if (args[0] == "read-evidence")
{
    var evidence = new FileRunRecoveryArchive(directory).RestoreEvidence(args[2]);
    File.WriteAllText(Path.Combine(directory, "evidence-reader-result.json"), JsonSerializer.Serialize(new
    {
        ProcessId = Environment.ProcessId, evidence.IsReadOnly, evidence.HistoryPosition,
        AdmissionCount = evidence.AdmissionLog.Count, CanonicalCount = evidence.CanonicalRecords.Count,
        Admissions = evidence.AdmissionLog, Records = evidence.CanonicalRecords,
    }));
    return;
}
if (args[0] == "read-world")
{
    var archive = new FileRunRecoveryArchive(directory);
    var run = archive.RestoreRunState(args[2]); var evidence = archive.RestoreEvidence(args[2]);
    using var source = new FileWorldJournal(Path.Combine(directory, "world.journal.jsonl"), true);
    var history = WorldHistoryRead.ReadOriginal(source, args[3], run.HistoryPosition!.RecordingId, evidence);
    File.WriteAllText(Path.Combine(directory, "world-reader-result.json"), JsonSerializer.Serialize(new
    {
        ProcessId = Environment.ProcessId, run.RunId, RunIsReadOnly = run.IsReadOnly, run.IsTerminal,
        EvidenceIsReadOnly = evidence.IsReadOnly, history.Position, history.RunRecordingId, history.EvidenceRecordingId,
        history.ContinuationSupported, history.HistoricalRevisions, history.Operations,
    }));
    return;
}
if (args[0] == "read-three")
{
    var read = new FileRunRecoveryArchive(directory).InspectEvidenceRecovery(args[2]);
    File.WriteAllText(Path.Combine(directory, "three-reader-result.json"), JsonSerializer.Serialize(new
    {
        ProcessId = Environment.ProcessId, read.RunAndResources.Run?.RunId,
        RunIsReadOnly = read.RunAndResources.Run?.IsReadOnly, read.RunAndResources.Run?.IsTerminal,
        RunPosition = read.RunAndResources.Run?.HistoryPosition, read.RunAndResources.Resources,
        StateAndResourcesConsistent = read.RunAndResources.StateAndResourcesConsistent,
        EvidenceIsReadOnly = read.Evidence?.IsReadOnly, EvidencePosition = read.Evidence?.HistoryPosition,
        AdmissionCount = read.Evidence?.AdmissionLog.Count, CanonicalCount = read.Evidence?.CanonicalRecords.Count,
        read.Point, read.HistoriesConsistent, read.Reason, read.ContinuationSupported,
    }));
    return;
}
if (args[0] == "read-joint")
{
    var read = new FileRunRecoveryArchive(directory).InspectRecovery(args[2]);
    File.WriteAllText(Path.Combine(directory, "joint-reader-result.json"), JsonSerializer.Serialize(new
    {
        ProcessId = Environment.ProcessId, read.Run?.RunId, read.Run?.IsReadOnly, read.Run?.IsTerminal,
        HistoryPosition = read.Run?.HistoryPosition, read.Resources, read.Point,
        read.StateAndResourcesConsistent, read.Reason, read.ContinuationSupported,
    }));
    return;
}
if (args[0] == "read-state")
{
    var restored = new FileRunRecoveryArchive(directory).RestoreRunState(args[2]);
    var readPolicy = new AgentPlanPolicy();
    var readKernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "live.frame" },
        new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy()), DisabledRunTrace.Instance,
        restored, new ControlLoop(readPolicy), new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery()));
    var readDriver = new KernelRunDriver(readKernel, readPolicy, new RunDriverInputs
    { NextInput = _ => throw new InvalidOperationException("restored Owner must not execute"),
        ConsultAgent = _ => throw new InvalidOperationException("restored Owner must not consult") });
    var activation = readDriver.Activate(); var drive = readDriver.Drive();
    var evaluationBlocked = false;
    try { readKernel.EvaluateTerminal(); }
    catch (InvalidOperationException ex) when (ex.Message == "restored-run-is-read-only") { evaluationBlocked = true; }
    File.WriteAllText(Path.Combine(directory, "state-reader-result.json"), JsonSerializer.Serialize(new
    {
        ProcessId = Environment.ProcessId, restored.RunId, restored.IsReadOnly, restored.IsTerminal,
        State = restored.State, HistoryCount = restored.History.Count, ActivationAccepted = activation.Accepted,
        DriveReason = drive.Reason, TerminalEvaluationBlocked = evaluationBlocked,
    }));
    return;
}
if (args[0] != "write") throw new ArgumentException("write/read mode required");
Directory.CreateDirectory(directory);
using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
    consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
var policy = new AgentPlanPolicy { GoalDirectedContinuationEnabled = true, HardCapTotalSteps = 20, HardCapConsultations = 10, RenewalThresholdSteps = 10, RenewalThresholdRounds = 4 };
var scope = new HashSet<string> { "live.frame" };
using var stateHistory = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
var runModel = new RunModel(stateHistory);
using var evidenceJournal = new FileEvidenceJournal(Path.Combine(directory, "evidence.journal.jsonl"));
var evidenceLedger = new EvidenceLedger(evidenceJournal);
var worldLinked = args.Length > 2 && args[2] is "world-linked" or "world-linked-empty";
var worldMode = worldLinked || args.Length > 2 && args[2] is "world" or "world-empty" or "world-gap";
var worldEmpty = args.Length > 2 && args[2] is "world-empty" or "world-linked-empty";
using var worldJournal = worldMode ? new FileWorldJournal(Path.Combine(directory, "world.journal.jsonl")) : null;
var world = new WorldModel(scope, new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy(),
    history: worldJournal is null ? null : new(worldJournal, stateHistory.RecordingId, evidenceJournal.RecordingId));
var kernel = new UniKernel(evidenceLedger, world,
    DisabledRunTrace.Instance, runModel, new ControlLoop(policy), new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery(), execution));
var observations = 0; var consultations = 0;
string? originalRootId = null, derivedEvidenceId = null;
var evidenceLinked = args.Length > 2 && args[2] == "evidence-linked";
var pending = args.Length > 2 && args[2] == "pending";
var terminal = args.Length > 2 && (args[2] == "terminal" || args[2] == "terminal-linked");
var terminalLinked = args.Length > 2 && args[2] == "terminal-linked";
var evidenceEmpty = worldEmpty || args.Length > 2 && args[2] == "evidence-empty";
var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
{
    ResourceJournal = resources, RecoveryArchive = new FileRunRecoveryArchive(directory),
    ConsultationJournal = pending ? new InterruptedP25(p25) : p25,
    PollCancellation = terminalLinked ? () => new("synthetic original stop", DateTimeOffset.UtcNow) : null,
    ProjectExecutionOverview = () => new("probe-overview", [], [], [], null, null, ["owner:a"],
        ProgressFactKeys: ["known:a"], ActionableItems: new Dictionary<string, ActionableItem>
        { ["pending:a"] = new(["owner:a"], ["known:a"]) }),
    ConsultAgent = context =>
    {
        consultations++;
        if (evidenceLinked && originalRootId is null) AppendOriginalEvidence();
        if (terminal) return new AgentDecision.Blocked(context.DecisionId, "original evidence-backed stop");
        var review = context.BudgetReview ?? throw new InvalidOperationException("Original review is required");
        return new AgentDecision.Defer(context.DecisionId, new(null, 1), ReviewResponse:
            new(review.ReviewId, BudgetReviewOutcome.RequestRenewal,
                new AgentProgressAssessment(context.ExecutionOverview!.OverviewId, "new original item", "inspect a",
                    "inspect a", ["item a"], ["owner:a"], NextItemKey: "pending:a"), new(2, 2, "original next work")));
    },
    NextInput = directive => !evidenceEmpty && ++observations == 1 ? new RunDriverInput.Observation([new(new("live.frame", "synthetic-seed"),
        IngressKind.Observation, directive.Context, new("recovery-probe", DateTimeOffset.UtcNow, "seed:original", ["synthetic", "offline"]))]) : null,
});
if (!kernel.AdmitContract(new("recovery-probe-v1", "original nonterminal run", scope,
    new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
    Obligations: terminal ? [new("stop", RunObligationKind.SafeStop, "live.frame", "synthetic-seed", true)] : null,
    MaxConsultations: 4, MaxTotalSteps: 10,
    TaskInstanceId: "probe-task", ProductSessionId: "probe-session", ContractRef: "probe-contract",
    AcceptedContractViewRef: "probe-view", ContractGeneration: 1)).Accepted || !driver.Activate().Accepted)
    throw new InvalidOperationException("original admission/activation failed");
string status;
try { status = driver.Drive().Status.ToString(); }
catch (IOException) when (pending) { status = "InterruptedP25Settlement"; }
if (terminal && !terminalLinked)
{
    if (!kernel.EvaluateTerminal().Transition.Accepted) throw new InvalidOperationException("Actual terminal proof not recorded");
    status = "RecordedTerminal";
}
if (kernel.IsRunTerminal != terminal || (!pending && !terminal && status != RunDriveStatus.WaitingForInput.ToString()))
    throw new InvalidOperationException("Kill carrier must stop at a nonterminal waiting boundary: " + status);
if (args.Length > 2 && args[2] == "evidence-derived") AppendOriginalEvidence();
void AppendOriginalEvidence()
{
    var proposal = new ObservationProposal(new("live.frame", "original-root"), IngressKind.Observation, ObservationContext.External,
        new("original-evidence-probe", DateTimeOffset.Parse("2026-10-24T12:00:00+08:00"), "capture:original", ["synthetic", "original"]));
    var root = evidenceLedger.Admit(proposal).Record!; originalRootId = root.EvidenceId;
    var child = proposal with { Claim = new("live.frame", "original-derived"), DerivedFromEvidenceIds = [root.EvidenceId],
        TransitiveEvidenceBasis = new HashSet<string> { root.EvidenceId }, FusionRule = "original-fusion", FusionVersion = "1" };
    derivedEvidenceId = evidenceLedger.Admit(child).Record!.EvidenceId;
    evidenceLedger.Admit(child);
    evidenceLedger.Admit(proposal with { Claim = new("", "rejected") });
}
if (worldMode)
{
    if (!worldEmpty)
    {
        world.RegisterContinuityDemand(new("original-world-demand", ContinuityDemandSourceKind.EntityScopedObligation,
            null, "button", "original", null, null, null));
        world.ResolveContinuity(new("original-world-demand"));
    }
    if (worldLinked) driver.Drive();
    File.WriteAllText(Path.Combine(directory, "world-original-expected.json"), JsonSerializer.Serialize(new
    { revisions = world.RevisionHistory, demands = world.ContinuityDemands, continuity = world.ContinuityLog },
        new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
{
    ProcessId = Environment.ProcessId, kernel.RunId, Status = status, Terminal = kernel.IsRunTerminal,
    Consultations = consultations, Deliveries = 0, Pending = pending,
    HistoryCount = runModel.History.Count, runModel.State!.Progress.Cycles, runModel.State.Progress.Acts,
    OutcomeProofId = runModel.State.Outcome?.OutcomeProofId,
    EvidenceAdmissionCount = evidenceLedger.AdmissionLog.Count, EvidenceCanonicalCount = evidenceLedger.CanonicalRecords.Count,
    OriginalRootId = originalRootId, DerivedEvidenceId = derivedEvidenceId,
    WorldRecordingId = worldJournal?.RecordingId, WorldPosition = world.HistoryPosition,
    WorldRevisionCount = world.RevisionHistory.Count, WorldDemandCount = world.ContinuityDemands.Count,
}));
File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
// Parent uses actual process Kill after reading readiness. No cancellation,
// Stop or Dispose path runs in this process before that ungraceful exit.
await Task.Delay(Timeout.InfiniteTimeSpan);

sealed class Fresh : IFreshnessEvaluator
{ public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "synthetic"); }
sealed class NoDelivery : IEffectDriver
{ public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("No delivery in identity probe"); }
sealed class InterruptedP25(IConsultationJournal inner) : IConsultationJournal
{
    public ConsultationJournalEntry? Find(ConsultationKey key) => inner.Find(key);
    public ConsultationJournalEntry Reserve(ConsultationKey key, string fingerprint) => inner.Reserve(key, fingerprint);
    public ConsultationJournalEntry Complete(ConsultationKey key, string fingerprint, AgentDecision decision) => throw new IOException("synthetic settlement interruption");
    public ConsultationJournalEntry Reject(ConsultationKey key, string fingerprint, string reason) => inner.Reject(key, fingerprint, reason);
    public int ConsumedCount(string run, string view) => inner.ConsumedCount(run, view);
    public bool HasPending(string run, string view) => inner.HasPending(run, view);
}
