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

internal static class EvidenceBatchProbe
{
    internal static async Task Run(string mode, string directory, string runId)
    {
        if (mode == "read-evidence-batch")
        {
            var read = new FileRunRecoveryArchive(directory).RestoreEvidence(runId);
            using var source = new FileEvidenceJournal(Path.Combine(directory, "evidence.journal.jsonl"), true);
            File.WriteAllText(Path.Combine(directory, "reader.json"), JsonSerializer.Serialize(new
            { ProcessId = Environment.ProcessId, read.IsReadOnly, Admissions = read.AdmissionLog.Count, Canonical = read.CanonicalRecords.Count, Rows = source.ReadRecords() }));
            return;
        }
        Directory.CreateDirectory(directory);
        using var states = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"));
        await using var original = new FileEvidenceJournal(Path.Combine(directory, "evidence.journal.jsonl"));
        using var execution = new FileExecutionJournal(Path.Combine(directory, "exec.journal"));
        using var p25 = new FileConsultationJournal(Path.Combine(directory, "consultations.journal.jsonl"));
        using var resources = new FileRunResourceJournal(Path.Combine(directory, "resource.journal.jsonl"),
            consultationPath: Path.Combine(directory, "consultations.journal.jsonl"), executionPath: Path.Combine(directory, "exec.journal"));
        var run = new RunModel(states); var policy = new AgentPlanPolicy(); EvidenceLedger? ledger = null; UniKernel? kernel = null;
        ledger = new EvidenceLedger(new Held(original, directory, () => (run, ledger!, kernel!)));
        var scope = new HashSet<string> { "live.frame" };
        kernel = new UniKernel(ledger, new WorldModel(scope), DisabledRunTrace.Instance, run, new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect(), execution));
        var root = new ObservationProposal(new("live.frame", "root"), IngressKind.Observation, ObservationContext.External,
            new("batch-probe", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var rootId = new EvidenceLedger().Admit(root).Record!.EvidenceId;
        var child = root with { Claim = new("live.frame", "child"), DerivedFromEvidenceIds = [rootId],
            TransitiveEvidenceBasis = new HashSet<string> { rootId }, FusionRule = "offline-fusion", FusionVersion = "1" };
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ConsultationJournal = p25, ResourceJournal = resources, RecoveryArchive = new FileRunRecoveryArchive(directory),
            NextInput = _ => new RunDriverInput.Observation([root, root, child, root with { Claim = new("", "invalid") }]),
            ConsultAgent = _ => throw new InvalidOperationException("unacknowledged observation must not consult"),
        });
        if (!kernel.AdmitContract(new("batch-probe-v1", "original observation batch", scope, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            TaskInstanceId: "batch-task", ProductSessionId: "batch-session", ContractRef: "batch-contract")).Accepted || !driver.Activate().Accepted)
            throw new InvalidOperationException("original admission failed");
        var result = await driver.DriveAsync(); throw new InvalidOperationException("did not reach batch commit: " + result.Reason);
    }
    private sealed class Held(IAsyncEvidenceJournal inner, string directory, Func<(RunModel Run, EvidenceLedger Ledger, UniKernel Kernel)> state) : IAsyncEvidenceJournal
    {
        public bool CanInitialize => inner.CanInitialize; public string RecordingId => inner.RecordingId;
        public IReadOnlyList<EvidenceHistoryRecord> ReadRecords() => inner.ReadRecords();
        public void Append(EvidenceHistoryRecord record) => throw new InvalidOperationException("synchronous observation storage");
        public async ValueTask AppendBatchAsync(IReadOnlyList<EvidenceHistoryRecord> rows, CancellationToken cancellationToken = default)
        {
            await inner.AppendBatchAsync(rows, cancellationToken);
            var (run, ledger, kernel) = state();
            File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new { Rows = inner.ReadRecords() }));
            File.WriteAllText(Path.Combine(directory, "ready.pending"), JsonSerializer.Serialize(new
            { ProcessId = Environment.ProcessId, run.RunId, run.IsTerminal, PublishedAdmissions = ledger.AdmissionLog.Count,
                PublishedCanonical = ledger.CanonicalRecords.Count, Belief = kernel.CurrentBelief, SourceRows = inner.ReadRecords().Count }));
            File.Move(Path.Combine(directory, "ready.pending"), Path.Combine(directory, "ready.json"));
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class NoEffect : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no delivery"); }
}
