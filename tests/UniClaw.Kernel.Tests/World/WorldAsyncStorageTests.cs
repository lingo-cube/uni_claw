using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldAsyncStorageTests
{
    private static ObservationProposal Proposal(string value = "on") => new(new("switch", value), IngressKind.Observation,
        ObservationContext.External, new("world-async", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["synthetic"]));

    private sealed class MemoryJournal : IWorldJournal
    {
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public List<WorldHistoryRecord> Records { get; } = [];
        public IReadOnlyList<WorldHistoryRecord> ReadRecords() => Records.ToArray();
        public void Append(WorldHistoryRecord record) => Records.Add(record);
    }

    private sealed class HeldJournal(bool fail = false, bool afterCommit = false, string hold = "reconcile") : IAsyncWorldJournal
    {
        private readonly MemoryJournal _inner = new();
        public bool CanInitialize => true;
        public string RecordingId => _inner.RecordingId;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int AsyncCalls { get; private set; }
        public IReadOnlyList<WorldHistoryRecord> ReadRecords() => _inner.ReadRecords();
        public void Append(WorldHistoryRecord record)
        {
            Assert.Equal("initialize", record.Operation);
            _inner.Append(record);
        }
        public async ValueTask AppendOperationAsync(WorldHistoryRecord operation, IReadOnlyList<WorldBeliefRevision> revisions,
            CancellationToken cancellationToken = default)
        {
            AsyncCalls++;
            if (operation.Operation == hold)
            {
                Entered.TrySetResult();
                await Release.Task;
                if (fail && !afterCommit) throw new IOException("before-world-commit");
            }
            ((IWorldJournal)_inner).AppendOperation(operation, revisions);
            if (operation.Operation == hold && fail) throw new IOException("world-ack-lost");
        }
    }

    [Fact]
    public async Task RevisionIsPublishedOnlyAfterAsyncOriginalStorageAcknowledges()
    {
        var source = new HeldJournal();
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var record = new EvidenceLedger().Admit(Proposal()).Record!;
        var relevance = await world.JudgeRelevanceAsync(record);
        var original = world.RevisionHistory;
        var pending = world.ReconcileAsync(record, relevance).AsTask();
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted);
        Assert.Null(world.Current);
        Assert.Empty(world.RevisionHistory);
        Assert.Equal(2, world.HistoryPosition!.Sequence);
        Assert.Throws<InvalidOperationException>(() => world.Reconcile(record, relevance));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ReconcileAsync(record, relevance).AsTask());
        source.Release.SetResult();
        var revision = await pending;
        Assert.Same(revision, world.Current);
        Assert.Empty(original);
        Assert.Single(world.RevisionHistory);
        Assert.Equal(3, world.HistoryPosition.Sequence);
        Assert.Equal(2, source.AsyncCalls);
    }

    [Fact]
    public async Task KernelReturnsAnIncompleteTaskWhileWorldCommitIsPending()
    {
        var source = new HeldJournal(); var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance);
        // This invocation probe detects blocking before an async method returns its Task.
        // It does not turn a synchronous implementation into a passing async test.
        var invocation = Task.Factory.StartNew(() => kernel.ProcessBatchAsync([Proposal()]).AsTask(), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var pending = await invocation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(pending.IsCompleted);
            Assert.Null(kernel.CurrentBelief);
            Assert.Single(ledger.AdmissionLog);
            Assert.Throws<InvalidOperationException>(() => kernel.Process(Proposal("overlap")));
            source.Release.SetResult();
            var result = await pending;
            Assert.NotNull(Assert.Single(result).ResultingRevision);
        }
        finally { source.Release.TrySetResult(); await (await invocation.WaitAsync(TimeSpan.FromSeconds(3))); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitFailureNeverPublishesAndCannotBeRetried(bool afterCommit)
    {
        var source = new HeldJournal(true, afterCommit);
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var record = new EvidenceLedger().Admit(Proposal()).Record!;
        var relevance = await world.JudgeRelevanceAsync(record); var published = world.RevisionHistory;
        var pending = world.ReconcileAsync(record, relevance).AsTask();
        source.Release.SetResult();
        await Assert.ThrowsAnyAsync<IOException>(() => pending);
        Assert.Empty(published);
        Assert.Equal(2, world.HistoryPosition!.Sequence);
        Assert.Equal(afterCommit ? 3 : 2, source.ReadRecords().Count);
        Assert.ThrowsAny<IOException>(() => world.Current);
        await Assert.ThrowsAnyAsync<IOException>(() => world.ReconcileAsync(record, relevance).AsTask());
        Assert.Equal(2, source.AsyncCalls);
    }

    [Fact]
    public async Task CancellationBeforeAcceptanceWritesNothingAndAcceptedWorkStillSettles()
    {
        var source = new HeldJournal(); using var cancellation = new CancellationTokenSource();
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var record = new EvidenceLedger().Admit(Proposal()).Record!;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => world.JudgeRelevanceAsync(record, cancellation.Token).AsTask());
        Assert.Equal(0, source.AsyncCalls); Assert.Single(source.ReadRecords());
        var relevance = await world.JudgeRelevanceAsync(record);
        using var acceptedCancellation = new CancellationTokenSource();
        var pending = world.ReconcileAsync(record, relevance, cancellationToken: acceptedCancellation.Token).AsTask();
        acceptedCancellation.Cancel(); await Task.Yield();
        Assert.False(pending.IsCompleted); Assert.Null(world.Current);
        source.Release.SetResult();
        Assert.Same(await pending, world.Current);
    }

    [Fact]
    public async Task DemandOnlyOperationsUseTheSameAcknowledgedAsyncPath()
    {
        var source = new HeldJournal(hold: "demand-register");
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var record = new EvidenceLedger().Admit(Proposal()).Record!;
        await world.ReconcileAsync(record, await world.JudgeRelevanceAsync(record));
        var demand = new ContinuityDemand("demand-one", ContinuityDemandSourceKind.EntityScopedObligation, null, "button", "one", null, null, null);
        var pending = world.RegisterContinuityDemandAsync(demand).AsTask();
        Assert.False(pending.IsCompleted); Assert.Empty(world.ContinuityDemands);
        source.Release.SetResult();
        var handle = await pending;
        Assert.Single(world.ContinuityDemands);
        await world.ResolveContinuityAsync(handle);
        await world.RevokeContinuityDemandAsync(demand.DemandId);
        Assert.Empty(world.ContinuityDemands);
        Assert.Equal(new[] { "initialize", "relevance", "reconcile", "demand-register", "continuity", "demand-revoke" }, source.ReadRecords().Select(r => r.Operation));
    }

    [Fact]
    public async Task ContractAdmissionWaitsForItsEntityDemandToBeAcknowledged()
    {
        var source = new HeldJournal(hold: "demand-register"); var policy = new AgentPlanPolicy();
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy));
        var pending = kernel.AdmitContractAsync(new("world-contract", "entity demand", new HashSet<string> { "switch" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            Obligations: [new RunObligation("entity", RunObligationKind.Objective, "switch", "on", Mandatory: true, EntityScope: new TargetDescriptor("switch", null))],
            TaskInstanceId: "world", ProductSessionId: "session", ContractRef: "contract")).AsTask();
        Assert.False(pending.IsCompleted); Assert.Empty(world.ContinuityDemands);
        source.Release.SetResult();
        Assert.True((await pending).Accepted); Assert.Single(world.ContinuityDemands);
        Assert.Equal(1, source.AsyncCalls);
    }

    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "test"); }
    private sealed class NoEffect : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no effect expected"); }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DriverAwaitsWorldBeforeConsultingAndCancellationCannotBypassTheCommit(bool cancel, bool fail)
    {
        var source = new HeldJournal(fail, true); var ledger = new EvidenceLedger(); var policy = new AgentPlanPolicy();
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(source, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect()));
        var pulls = 0; var consultations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = _ => { pulls++; return new RunDriverInput.Observation([Proposal()]); },
            ConsultAgent = context => { consultations++; return new AgentDecision.Blocked(context.DecisionId, "done"); },
        });
        Assert.True((await kernel.AdmitContractAsync(new("world-v1", "world observation", new HashSet<string> { "switch" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], TaskInstanceId: "world", ProductSessionId: "session", ContractRef: "contract"))).Accepted);
        Assert.True(driver.Activate().Accepted); using var cancellation = new CancellationTokenSource();
        var published = world.RevisionHistory;
        var pending = driver.DriveAsync(cancellation.Token);
        Assert.False(pending.IsCompleted); Assert.Null(kernel.CurrentBelief); Assert.Single(ledger.AdmissionLog);
        Assert.Equal("drive-in-progress", driver.Drive().Reason);
        Assert.Equal("drive-in-progress", (await driver.DriveAsync()).Reason);
        Assert.Equal(0, consultations); Assert.Equal(1, pulls); Assert.Empty(kernel.EffectReceipts);
        if (cancel) { cancellation.Cancel(); await Task.Yield(); Assert.False(pending.IsCompleted); }
        source.Release.SetResult(); var result = await pending;
        Assert.Equal(fail || cancel ? 0 : 1, consultations); Assert.Equal(1, pulls);
        if (fail)
        {
            Assert.Empty(published); Assert.Contains("world-persistence-failed", result.Reason);
            Assert.Equal(result.Reason, (await driver.DriveAsync()).Reason); Assert.Equal(2, source.AsyncCalls);
        }
        else
        {
            Assert.NotNull(kernel.CurrentBelief);
            if (cancel) Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, result.Status);
        }
    }
}
