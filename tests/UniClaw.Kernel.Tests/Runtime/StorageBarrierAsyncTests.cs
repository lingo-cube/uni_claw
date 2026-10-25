using UniClaw.Kernel.Control;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class StorageBarrierAsyncTests
{
    private sealed class HeldResource(string phase, bool fail = false, bool afterCommit = false) : IAsyncRunResourceJournal
    {
        private readonly List<RunResourceRecord> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public IReadOnlyList<RunResourceRecord> ReadRecords() => _rows.ToArray();
        public void Append(RunResourceRecord row) => throw new IOException("synchronous resource write");
        public async ValueTask AppendAsync(RunResourceRecord row, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (row.Phase.ToString() == phase)
            {
                Entered.TrySetResult(); await Release.Task;
                if (fail && !afterCommit) throw new IOException("before resource commit");
            }
            _rows.Add(row);
            if (row.Phase.ToString() == phase && fail) throw new IOException("resource ack lost");
        }
    }

    private static (UniKernel Kernel, KernelRunDriver Driver) Compose(IRunResourceJournal? resource,
        IConsultationJournal? consultations, Func<AgentDecisionContext, AgentDecision?> consult)
    {
        var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "live.frame" }),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy));
        Assert.True(kernel.AdmitContract(new("barrier-v1", "barrier test", new HashSet<string> { "live.frame" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract")).Accepted);
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = resource, ConsultationJournal = consultations,
            NextInput = directive => new RunDriverInput.Observation([new(new("live.frame", "original"), IngressKind.Observation,
                directive.Context, new("offline", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]))]),
            ConsultAgent = consult,
        });
        Assert.True(driver.Activate().Accepted);
        return (kernel, driver);
    }

    [Theory]
    [InlineData("Initialize", 0)]
    [InlineData("Begin", 0)]
    [InlineData("Confirm", 1)]
    public async Task ResourceConfirmationDoesNotBlockCallerOrAllowNextExternalCall(string phase, int expectedConsultations)
    {
        var source = new HeldResource(phase); var calls = 0;
        var (kernel, driver) = Compose(source, null, context => { calls++; return new AgentDecision.Blocked(context.DecisionId, "done"); });
        var pending = driver.DriveAsync();
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Equal(expectedConsultations, calls); Assert.Empty(kernel.EffectReceipts);
        if (phase == "Initialize") Assert.Null(driver.ResourceBinding);
        Assert.Equal("drive-in-progress", driver.Drive().Reason);
        source.Release.SetResult(); await pending;
        Assert.NotNull(driver.ResourceBinding); Assert.Equal(1, calls);
    }

    private sealed class HeldConsultation(string operation, bool fail = false, bool afterCommit = false) : IAsyncConsultationJournal
    {
        private readonly InMemoryConsultationJournal _inner = new();
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public ConsultationJournalEntry? Find(ConsultationKey key) => _inner.Find(key);
        public int ConsumedCount(string runId, string view) => _inner.ConsumedCount(runId, view);
        public bool HasPending(string runId, string view) => _inner.HasPending(runId, view);
        public ConsultationJournalEntry Reserve(ConsultationKey key, string fp) => throw new IOException("synchronous P25 reserve");
        public ConsultationJournalEntry Complete(ConsultationKey key, string fp, AgentDecision decision) => throw new IOException("synchronous P25 complete");
        public ConsultationJournalEntry Reject(ConsultationKey key, string fp, string reason) => throw new IOException("synchronous P25 reject");
        public ValueTask<ConsultationJournalEntry> ReserveAsync(ConsultationKey key, string fp, CancellationToken cancellationToken = default) => Write("reserve", () => _inner.Reserve(key, fp));
        public ValueTask<ConsultationJournalEntry> CompleteAsync(ConsultationKey key, string fp, AgentDecision decision, CancellationToken cancellationToken = default) => Write("complete", () => _inner.Complete(key, fp, decision));
        public ValueTask<ConsultationJournalEntry> RejectAsync(ConsultationKey key, string fp, string reason, CancellationToken cancellationToken = default) => Write("reject", () => _inner.Reject(key, fp, reason));
        private async ValueTask<ConsultationJournalEntry> Write(string name, Func<ConsultationJournalEntry> save)
        {
            Calls++;
            if (name != operation) return save();
            ConsultationJournalEntry? row = afterCommit ? save() : null;
            Entered.TrySetResult(); await Release.Task;
            if (fail) throw new IOException(afterCommit ? "P25 ack lost" : "before P25 commit");
            return row ?? save();
        }
    }

    [Theory]
    [InlineData("reserve", 0)]
    [InlineData("complete", 1)]
    [InlineData("reject", 1)]
    public async Task ConsultationConfirmationPrecedesCallOrResultUse(string operation, int expectedCalls)
    {
        var source = new HeldConsultation(operation); var calls = 0;
        var (kernel, driver) = Compose(null, source, context => { calls++; return operation == "reject" ? null : new AgentDecision.Blocked(context.DecisionId, "done"); });
        var pending = driver.DriveAsync();
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Equal(expectedCalls, calls); Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(0, source.ConsumedCount(kernel.RunId, kernel.RunView!.AcceptedContractViewRef ?? kernel.RunView.Version));
        Assert.Equal("drive-in-progress", driver.Drive().Reason);
        source.Release.SetResult(); await pending; Assert.Equal(1, calls);
    }

    private sealed class CountingDelivery : IEffectDriver
    {
        public int Calls { get; private set; }
        public DispatchResult Deliver(DispatchRequest request) { Calls++; return new(DispatchOutcome.DeliveryCompleted, "actual", DateTimeOffset.Parse("2026-10-25T00:00:00Z")); }
    }
    private sealed class HeldExecution(string operation, ExecutionCommitOutcome outcome = ExecutionCommitOutcome.Success, bool fail = false) : IAsyncReliableExecutionSource
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int PrepareCalls { get; private set; }
        public ExecutionCommit CommitPrepare(ExecutionRegistration row) => throw new IOException("sync prepare");
        public async ValueTask<ExecutionCommit> CommitPrepareAsync(ExecutionRegistration row, CancellationToken cancellationToken = default)
        { PrepareCalls++; await Hold("prepare"); return new(outcome, "attempt-original"); }
        private async ValueTask Hold(string name) { if (operation == name) { Entered.TrySetResult(); await Release.Task; if (fail) throw new IOException("execution ack lost"); } }
        public ValueTask AppendSubmissionAsync(string id, string note, CancellationToken cancellationToken = default) => Hold("submission");
        public ValueTask AppendReceiptAsync(string id, EffectReceipt? receipt, CancellationToken cancellationToken = default) => Hold("receipt");
        public ValueTask AppendLateFeedbackAsync(string? id, ExecutionFeedback feedback, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<string> LinkRetryAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ExecutionLink> LinkCompensationAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void AppendSubmission(string id, string note) => throw new IOException("sync submission");
        public void AppendReceipt(string id, EffectReceipt? receipt) => throw new IOException("sync receipt");
        public void AppendLateFeedback(string? id, ExecutionFeedback feedback) => throw new NotSupportedException();
        public string LinkRetry(string id) => throw new NotSupportedException();
        public ExecutionLink LinkCompensation(string id) => throw new NotSupportedException();
        public IReadOnlyList<PendingExecution> DiscoverPending() => [];
        public ExecutionAttemptView? GetAttempt(string id) => null;
    }
    private static (CanonicalBinding Binding, AssuranceJudgment Judgment, BindingView View) Grounded(string id = "binding") =>
        (new(id, "intent", "tap", "switch.wifi", "true", "rev-1", 1),
         new("intent", id, "rev-1", true, [], null, new(FreshnessSufficiency.Sufficient, "offline")),
         new("rev-1", 1, HasTargetSubjectClaim: true));

    [Theory]
    [InlineData("prepare", 0)]
    [InlineData("submission", 1)]
    [InlineData("receipt", 1)]
    public async Task ExecutionConfirmationKeepsDeliveryBehindPrepareAndPreservesActualReceipt(string operation, int expectedCalls)
    {
        var source = new HeldExecution(operation); var delivery = new CountingDelivery(); var boundary = new EffectBoundary(delivery, source);
        var (binding, judgment, view) = Grounded();
        var pending = boundary.DispatchAsync(binding, judgment, view);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Equal(expectedCalls, delivery.Calls);
        Assert.Equal(expectedCalls, boundary.ReceiptLog.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => boundary.DispatchAsync(binding, judgment, view).AsTask());
        source.Release.SetResult(); var result = await pending;
        Assert.True(result.Gate.Allowed); Assert.Same(Assert.Single(boundary.ReceiptLog), result.Receipt); Assert.Equal(1, delivery.Calls);
    }
    [Theory]
    [InlineData("Initialize", false)] [InlineData("Initialize", true)]
    [InlineData("Begin", false)] [InlineData("Begin", true)]
    [InlineData("Confirm", false)] [InlineData("Confirm", true)]
    public async Task ResourceFailureBeforeOrAfterCommitPreventsBlindRetry(string phase, bool afterCommit)
    {
        var source = new HeldResource(phase, true, afterCommit); var calls = 0;
        var (_, driver) = Compose(source, null, context => { calls++; return new AgentDecision.Blocked(context.DecisionId, "done"); });
        var pending = driver.DriveAsync(); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        source.Release.SetResult(); Assert.Contains("resource-accounting-failed", (await pending).Reason);
        var writes = source.Calls; var previous = calls;
        Assert.Contains("resource-accounting-failed", (await driver.DriveAsync()).Reason);
        Assert.Equal(writes, source.Calls); Assert.Equal(previous, calls);
    }
    [Theory]
    [InlineData("reserve", false)] [InlineData("reserve", true)]
    [InlineData("complete", false)] [InlineData("complete", true)]
    [InlineData("reject", false)] [InlineData("reject", true)]
    public async Task ConsultationFailureBeforeOrAfterCommitPreventsNewCorrelation(string operation, bool afterCommit)
    {
        var source = new HeldConsultation(operation, true, afterCommit); var calls = 0;
        var (_, driver) = Compose(null, source, context => { calls++; return operation == "reject" ? null : new AgentDecision.Blocked(context.DecisionId, "done"); });
        var pending = driver.DriveAsync(); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); source.Release.SetResult();
        Assert.Contains("consultation-persistence-failed", (await pending).Reason);
        var writes = source.Calls; var previous = calls;
        Assert.Contains("consultation-persistence-failed", (await driver.DriveAsync()).Reason);
        Assert.Equal(writes, source.Calls); Assert.Equal(previous, calls);
    }
    [Theory]
    [InlineData("Initialize")] [InlineData("Begin")]
    public async Task CancellationDuringResourceConfirmationSettlesThenStopsBeforeConsultation(string phase)
    {
        var source = new HeldResource(phase); var calls = 0; using var cancel = new CancellationTokenSource();
        var (_, driver) = Compose(source, null, context => { calls++; return new AgentDecision.Blocked(context.DecisionId, "done"); });
        var pending = driver.DriveAsync(cancel.Token); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancel.Cancel();
        Assert.False(pending.IsCompleted); source.Release.SetResult(); await pending; Assert.Equal(0, calls);
    }
    [Theory]
    [InlineData("reserve", 0)] [InlineData("complete", 1)] [InlineData("reject", 1)]
    public async Task CancellationDuringConsultationConfirmationSettlesWithoutNewCall(string operation, int expectedCalls)
    {
        var source = new HeldConsultation(operation); var calls = 0; using var cancel = new CancellationTokenSource();
        var (_, driver) = Compose(null, source, context => { calls++; return operation == "reject" ? null : new AgentDecision.Blocked(context.DecisionId, "done"); });
        var pending = driver.DriveAsync(cancel.Token); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancel.Cancel();
        Assert.False(pending.IsCompleted); source.Release.SetResult(); await pending; Assert.Equal(expectedCalls, calls);
    }
    [Theory]
    [InlineData(ExecutionCommitOutcome.Failure)] [InlineData(ExecutionCommitOutcome.Unknown)]
    public async Task ExecutionNonSuccessDoesNotDeliver(ExecutionCommitOutcome outcome)
    {
        var source = new HeldExecution("prepare", outcome); var delivery = new CountingDelivery(); var boundary = new EffectBoundary(delivery, source);
        var (binding, judgment, view) = Grounded(); var pending = boundary.DispatchAsync(binding, judgment, view);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); source.Release.SetResult();
        var result = await pending; Assert.False(result.Gate.Allowed); Assert.Null(result.Receipt); Assert.Equal(0, delivery.Calls);
    }
    [Theory]
    [InlineData("prepare", 0)] [InlineData("submission", 1)] [InlineData("receipt", 1)]
    public async Task ExecutionCancellationSettlesAcceptedStorageAndKeepsActualDelivery(string operation, int calls)
    {
        var source = new HeldExecution(operation); var delivery = new CountingDelivery(); var boundary = new EffectBoundary(delivery, source);
        using var cancel = new CancellationTokenSource(); var (binding, judgment, view) = Grounded();
        var pending = boundary.DispatchAsync(binding, judgment, view, cancel.Token);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancel.Cancel(); Assert.False(pending.IsCompleted);
        source.Release.SetResult(); var result = await pending; Assert.Equal(calls, delivery.Calls);
        if (calls == 0) Assert.Null(result.Receipt); else Assert.Same(Assert.Single(boundary.ReceiptLog), result.Receipt);
    }
    [Theory]
    [InlineData("prepare", 0)] [InlineData("submission", 1)] [InlineData("receipt", 1)]
    public async Task ExecutionStorageFailureKeepsActualReceiptAndLatchesFurtherDelivery(string operation, int calls)
    {
        var source = new HeldExecution(operation, fail: true); var delivery = new CountingDelivery(); var boundary = new EffectBoundary(delivery, source);
        var (binding, judgment, view) = Grounded(); var pending = boundary.DispatchAsync(binding, judgment, view);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); source.Release.SetResult(); var result = await pending;
        Assert.Equal(calls, delivery.Calls); if (calls == 1) Assert.Same(Assert.Single(boundary.ReceiptLog), result.Receipt);
        (binding, judgment, view) = Grounded("another-binding");
        Assert.False((await boundary.DispatchAsync(binding, judgment, view)).Gate.Allowed);
        Assert.Equal(calls, delivery.Calls); Assert.Equal(1, source.PrepareCalls);
    }

    [Fact]
    public async Task DriverCancellationDuringExecutionPrepareStopsActualDeliveryAfterConfirmation()
    {
        var source = new HeldExecution("prepare"); var delivery = new CountingDelivery();
        var seed = new ObservationProposal(new("ui.container.observed", "switch:entry@ready"), IngressKind.Observation,
            ObservationContext.External, new("offline-barrier", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var eid = new EvidenceLedger().Admit(seed).Admission.EvidenceId!; var container = "ctr-" + eid[3..15]; var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "ui.container.observed" }, new SeedAssociation(), new SeedOccurrence(container)),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy), new RuntimeAssurance(new Fresh()), new EffectBoundary(delivery, source));
        var observations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = _ => ++observations == 1 ? new RunDriverInput.Observation([seed]) : null,
            ConsultAgent = context => new AgentDecision.Act(new(context.DecisionId, [new("switch", "entry", "tap", null)], "original action")),
        });
        Assert.True(kernel.AdmitContract(new("driver-barrier-v1", "barrier", new HashSet<string> { "ui.container.observed" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract")).Accepted);
        Assert.True(driver.Activate().Accepted); using var cancel = new CancellationTokenSource();
        var pending = driver.DriveAsync(cancel.Token); await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Equal(0, delivery.Calls); cancel.Cancel(); source.Release.SetResult();
        await pending; Assert.Equal(0, delivery.Calls); Assert.Empty(kernel.EffectReceipts);
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class SeedOccurrence(string container) : UniClaw.Kernel.World.UiRealization.IUiObservationStrategy
    { public IReadOnlyList<UniClaw.Kernel.World.UiRealization.ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) => [new(container, "switch", "entry", "ready")]; }
    private sealed class SeedAssociation : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => input.Previous is null
            ? new(AssociationDispositionKind.New, null, [new("(new)", [input.Current.EvidenceId], [])], [], "seed")
            : new(AssociationDispositionKind.Insufficient, null, [], [], "seed once");
    }

}
