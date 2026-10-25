using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class StorageClosureAsyncTests
{
    private sealed class Resource : IAsyncRunResourceJournal
    {
        private readonly List<RunResourceRecord> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public IReadOnlyList<RunResourceRecord> ReadRecords() => _rows.ToArray();
        public void Append(RunResourceRecord row) => throw new IOException("sync resource");
        public ValueTask AppendAsync(RunResourceRecord row, CancellationToken cancellationToken = default) { _rows.Add(row); return ValueTask.CompletedTask; }
    }
    private sealed class HeldArchive(bool fail = false, bool afterCommit = false) : IAsyncRunRecoveryArchive
    {
        private RunRecoveryRecord? _row;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public void Initialize(RunRecoveryRecord row) => throw new IOException("sync archive");
        public RunRecoveryRecord Read() => _row ?? throw new IOException("no confirmed archive");
        public async ValueTask InitializeAsync(RunRecoveryRecord row, CancellationToken cancellationToken = default)
        {
            Calls++; if (afterCommit) _row = row; Entered.TrySetResult(); await Release.Task;
            if (fail) throw new IOException(afterCommit ? "archive ack lost" : "archive not committed"); _row = row;
        }
    }
    private static (UniKernel Kernel, KernelRunDriver Driver) Compose(IRunRecoveryArchive archive, Action consult, Func<AgentDecisionContext, AgentDecision?, CancellationToken, ValueTask>? observer = null)
    {
        var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "live.frame" }), DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(policy));
        Assert.True(kernel.AdmitContract(new("closure-v1", "closure", new HashSet<string> { "live.frame" }, new HashSet<string> { "tap" },
            new HashSet<string>(), ["goal"], TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract")).Accepted);
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = new Resource(), RecoveryArchive = archive, ObserveConsultationAsync = observer,
            NextInput = directive => new RunDriverInput.Observation([new(new("live.frame", "seed"), IngressKind.Observation,
                directive.Context, new("offline", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "seed", ["offline"]))]),
            ConsultAgent = context => { consult(); return new AgentDecision.Blocked(context.DecisionId, "done"); },
        });
        Assert.True(driver.Activate().Accepted); return (kernel, driver);
    }
    [Fact]
    public async Task ArchiveConfirmationDoesNotBlockCallerOrPublishBindingOrConsult()
    {
        var archive = new HeldArchive(); var calls = 0; var (kernel, driver) = Compose(archive, () => calls++);
        var pending = driver.DriveAsync(); await archive.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Null(driver.ResourceBinding); Assert.Equal(0, calls); Assert.Empty(kernel.EffectReceipts);
        Assert.Equal("drive-in-progress", driver.Drive().Reason); archive.Release.SetResult(); await pending;
        Assert.NotNull(driver.ResourceBinding); Assert.Equal(1, calls); Assert.Equal(kernel.RunId, archive.Read().Run.RunId);
    }

    private sealed class HeldWorld(bool fail = false) : IAsyncWorldJournal
    {
        private readonly List<WorldHistoryRecord> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<WorldHistoryRecord> ReadRecords() => _rows.ToArray();
        public void Append(WorldHistoryRecord record) => throw new IOException("sync world initialization");
        public async ValueTask InitializeAsync(WorldHistoryRecord row, CancellationToken cancellationToken = default)
        { Entered.TrySetResult(); await Release.Task; if (fail) throw new IOException("world initialize failure"); _rows.Add(row); }
        public ValueTask AppendOperationAsync(WorldHistoryRecord row, IReadOnlyList<WorldBeliefRevision> revisions, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    [Fact]
    public async Task WorldFactoryWaitsForOriginalOwnerInitializationBeforeReturning()
    {
        var source = new HeldWorld(); var pending = WorldModel.CreateAsync(new HashSet<string> { "live.frame" },
            history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert.False(pending.IsCompleted); Assert.Empty(source.ReadRecords());
        source.Release.SetResult(); var owner = await pending;
        Assert.Null(owner.Current); Assert.Equal(1, owner.HistoryPosition!.Sequence); Assert.Equal("initialize", Assert.Single(source.ReadRecords()).Operation);
    }

    private sealed class HeldEvidence(string producer, bool fail = false) : IAsyncEvidenceJournal
    {
        private readonly List<EvidenceHistoryRecord> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int HeldBatchSize { get; private set; }
        public IReadOnlyList<EvidenceHistoryRecord> ReadRecords() => _rows.ToArray();
        public void Append(EvidenceHistoryRecord row) => throw new IOException("sync reflux");
        public async ValueTask AppendBatchAsync(IReadOnlyList<EvidenceHistoryRecord> rows, CancellationToken cancellationToken = default)
        {
            if (rows.Any(row => row.Record?.Provenance.Producer == producer))
            { HeldBatchSize = rows.Count; Entered.TrySetResult(); await Release.Task; if (fail) throw new IOException("reflux failed"); }
            _rows.AddRange(rows);
        }
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class NoDelivery : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no effect allowed"); }
    private static (UniKernel Kernel, KernelRunDriver Driver) Reflux(HeldEvidence source, string kind)
    {
        var scope = new HashSet<string> { "live.frame", "x.state", "y.state" }; var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(source), new WorldModel(scope), DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery()));
        var pulls = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = directive => kind == "cancel" ? new RunDriverInput.Cancel("stop", DateTimeOffset.Parse("2026-10-25T00:01:00Z"))
                : ++pulls == 1 ? new RunDriverInput.Observation([new(new("live.frame", "seed"), IngressKind.Observation,
                    directive.Context, new("offline", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "seed", ["offline"]))]) : null,
            ConsultAgent = context => new AgentDecision.NoAction(new(context.DecisionId, "complete", new("observed",
                [kind == "adjudication" ? "dispatch:missing" : "obs:" + kernel.CurrentBelief!.EvidenceBasis.First()]))),
        });
        var obligations = kind == "cancel" ? new RunObligation[] { new("safe", RunObligationKind.SafeStop, "x.state", "done", true) }
            : [new("x", RunObligationKind.MaterialEffect, "x.state", "done", true), new("y", RunObligationKind.MaterialEffect, "y.state", "done", true)];
        Assert.True(kernel.AdmitContract(new("reflux-v1", "reflux", scope, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            Obligations: obligations, TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract")).Accepted);
        Assert.True(driver.Activate().Accepted); return (kernel, driver);
    }
    [Theory]
    [InlineData("cancel", "kernel.lifecycle", 1)]
    [InlineData("completion", "kernel.completion-verifier", 2)]
    public async Task CompletionAndCancelRefluxAwaitEvidenceWithoutPublishingTerminal(string kind, string producer, int rows)
    {
        var source = new HeldEvidence(producer); var (kernel, driver) = Reflux(source, kind);
        var pending = driver.DriveAsync();
        await Task.WhenAny(source.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.False(kernel.IsRunTerminal); Assert.Equal(rows, source.HeldBatchSize);
        Assert.Equal("drive-in-progress", driver.Drive().Reason); source.Release.SetResult(); var result = await pending;
        if (kind == "cancel") Assert.Equal(RunDriveStatus.Completed, result.Status);
        else { Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status); Assert.False(kernel.IsRunTerminal); }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ArchiveFailureBeforeOrAfterCommitLatchesWithoutNewConsultation(bool afterCommit)
    {
        var source = new HeldArchive(true, afterCommit); var calls = 0; var (_, driver) = Compose(source, () => calls++);
        var pending = driver.DriveAsync(); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); source.Release.SetResult();
        Assert.Contains("resource-accounting-failed", (await pending).Reason); Assert.Null(driver.ResourceBinding);
        Assert.Contains("resource-accounting-failed", (await driver.DriveAsync()).Reason); Assert.Equal(1, source.Calls); Assert.Equal(0, calls);
    }
    [Fact]
    public async Task ArchiveCancellationSettlesAcceptedIdentityBeforeStopping()
    {
        var source = new HeldArchive(); var calls = 0; var (_, driver) = Compose(source, () => calls++); using var cancel = new CancellationTokenSource();
        var pending = driver.DriveAsync(cancel.Token); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancel.Cancel();
        Assert.False(pending.IsCompleted); source.Release.SetResult(); await pending;
        Assert.NotNull(driver.ResourceBinding); Assert.NotNull(source.Read()); Assert.Equal(0, calls);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WorldFactoryAcceptedInitializationSettlesCancellationOrReportsFailure(bool fail)
    {
        var source = new HeldWorld(fail); using var cancel = new CancellationTokenSource();
        var pending = WorldModel.CreateAsync(new HashSet<string> { "live.frame" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")), cancellationToken: cancel.Token);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancel.Cancel(); Assert.False(pending.IsCompleted); source.Release.SetResult();
        if (fail) await Assert.ThrowsAsync<IOException>(() => pending.AsTask());
        else Assert.Equal(1, (await pending).HistoryPosition!.Sequence);
    }
    [Fact]
    public async Task AdjudicationAwaitsTheSameEvidenceBarrierAndDriveGateWithoutMintingProof()
    {
        var source = new HeldEvidence("kernel.completion-adjudicator"); var (kernel, driver) = Reflux(source, "adjudication");
        Assert.Equal(RunDriveStatus.AwaitingCompletionAdjudication, (await driver.DriveAsync()).Status);
        var pending = driver.ResumeWithAdjudicationAsync(true, null); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.False(kernel.IsRunTerminal); Assert.Equal(2, source.HeldBatchSize);
        Assert.Equal("drive-in-progress", driver.Drive().Reason);
        Assert.Equal("drive-in-progress", (await driver.ResumeWithAdjudicationAsync(false, "overlap")).Reason);
        source.Release.SetResult(); Assert.Equal(RunDriveStatus.TerminalNotProven, (await pending).Status); Assert.False(kernel.IsRunTerminal);
    }
    [Theory]
    [InlineData("cancel", "kernel.lifecycle")]
    [InlineData("completion", "kernel.completion-verifier")]
    public async Task FailedRefluxCannotPublishTerminalOrRetry(string kind, string producer)
    {
        var source = new HeldEvidence(producer, true); var (kernel, driver) = Reflux(source, kind);
        var pending = driver.DriveAsync(); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); source.Release.SetResult();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, (await pending).Status); Assert.False(kernel.IsRunTerminal);
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, (await driver.DriveAsync()).Status); Assert.False(kernel.IsRunTerminal);
    }

    [Fact]
    public async Task AsyncDiagnosticObserverHasIndependentCloseWithoutHoldingTheBusinessDriveGate()
    {
        var archive = new HeldArchive(); archive.Release.SetResult(); var calls = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (kernel, driver) = Compose(archive, () => calls++, async (_, _, _) => { entered.SetResult(); await release.Task; });
        var pending = driver.DriveAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            await pending.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(1, calls); Assert.False(kernel.IsRunTerminal);
            Assert.Equal(1, driver.DiagnosticObservations.Pending);
        }
        finally { release.TrySetResult(); await driver.CompleteDiagnosticObservationsAsync(); }
        Assert.Equal(1, driver.DiagnosticObservations.Completed);
    }

}
