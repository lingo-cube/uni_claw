using UniClaw.Kernel.Run;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Run;

public sealed class RunAsyncStorageTests
{
    private static ExecutionContract Contract() => new("run-async-v1", "record original progress",
        new HashSet<string> { "switch" }, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
        TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract");

    private sealed class HeldJournal(RunHistoryTransition held = RunHistoryTransition.Admitted,
        bool fail = false, bool afterCommit = false, RunTraversalTransition? traversal = null) : IAsyncRunStateJournal
    {
        private readonly List<RunHistoryRecord> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public IReadOnlyList<RunHistoryRecord> ReadRecords() => _rows.ToArray();
        public void Append(RunHistoryRecord record) => throw new InvalidOperationException("synchronous Run storage");
        public async ValueTask AppendAsync(RunHistoryRecord record, CancellationToken cancellationToken = default)
        {
            Calls++;
            var hold = record.Transition == held && (traversal is null || record.Traversal?.Transition == traversal);
            if (hold)
            {
                Entered.TrySetResult(); await Release.Task;
                if (fail && !afterCommit) throw new IOException("before-run-commit");
            }
            _rows.Add(record);
            if (hold && fail) throw new IOException("run-ack-lost");
        }
    }

    [Fact]
    public async Task AdmissionIdentityAndStateArePublishedOnlyAfterOriginalStorageAcknowledges()
    {
        var source = new HeldJournal(); var run = new RunModel(source);
        var pending = run.AdmitContractAsync(Contract()).AsTask();
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Null(run.View); Assert.Empty(run.RunId);
        Assert.Null(run.State); Assert.Null(run.HistoryPosition); Assert.Empty(run.History);
        Assert.Throws<InvalidOperationException>(() => run.AdmitContract(Contract()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.AdmitContractAsync(Contract()).AsTask());
        source.Release.SetResult();
        Assert.True((await pending).Accepted); Assert.NotNull(run.View); Assert.NotEmpty(run.RunId);
        Assert.Single(run.History); Assert.Equal(1, run.HistoryPosition!.Sequence); Assert.True(run.TraversalHistoryAvailable);
    }

    [Fact]
    public async Task KernelAdmissionReturnsAnIncompleteTaskWhileRunStorageIsPending()
    {
        var source = new HeldJournal(); var run = new RunModel(source);
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "switch" }), DisabledRunTrace.Instance, run);
        // Probe the invocation itself so a synchronous bridge cannot hide inside an async method.
        var invocation = Task.Factory.StartNew(() => kernel.AdmitContractAsync(Contract()).AsTask(),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var pending = await invocation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(pending.IsCompleted); Assert.Null(kernel.RunView);
            source.Release.SetResult(); Assert.True((await pending).Accepted);
        }
        finally { source.Release.TrySetResult(); await (await invocation.WaitAsync(TimeSpan.FromSeconds(3))); }
    }

    private static OutcomeProof Proof() => new("original-proof", TerminalClassification.Completion, [],
        new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), 0, "original judgment");

    [Theory]
    [InlineData(RunHistoryTransition.Cycle)]
    [InlineData(RunHistoryTransition.Action)]
    [InlineData(RunHistoryTransition.Terminal)]
    public async Task ProgressAndTerminalKeepThePublishedStateUntilAcknowledgement(RunHistoryTransition transition)
    {
        var source = new HeldJournal(transition); var run = new RunModel(source);
        await run.AdmitContractAsync(Contract()); var original = run.State!;
        var pending = transition switch
        {
            RunHistoryTransition.Cycle => (Task)run.RecordCycleAsync().AsTask(),
            RunHistoryTransition.Action => run.RecordActionAsync().AsTask(),
            _ => run.TransitionToTerminalAsync(Proof(), original).AsTask(),
        };
        Assert.False(pending.IsCompleted); Assert.Same(original, run.State); Assert.Equal(1, run.HistoryPosition!.Sequence);
        Assert.Throws<InvalidOperationException>(() => run.RecordAction());
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.RecordCycleAsync().AsTask());
        source.Release.SetResult(); await pending;
        Assert.NotSame(original, run.State); Assert.Equal(2, run.HistoryPosition.Sequence);
        Assert.Equal(transition == RunHistoryTransition.Terminal, run.IsTerminal);
        Assert.Equal(transition == RunHistoryTransition.Cycle ? 1 : 0, run.State!.Progress.Cycles);
        Assert.Equal(transition == RunHistoryTransition.Action ? 1 : 0, run.State.Progress.Acts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrLostAcknowledgementNeverPublishesOrRetries(bool afterCommit)
    {
        var source = new HeldJournal(RunHistoryTransition.Cycle, true, afterCommit); var run = new RunModel(source);
        await run.AdmitContractAsync(Contract()); var original = run.State;
        var pending = run.RecordCycleAsync().AsTask(); source.Release.SetResult();
        await Assert.ThrowsAnyAsync<IOException>(() => pending);
        Assert.Same(original, run.State); Assert.Equal(1, run.HistoryPosition!.Sequence);
        Assert.Equal(afterCommit ? 2 : 1, source.ReadRecords().Count);
        await Assert.ThrowsAnyAsync<IOException>(() => run.RecordCycleAsync().AsTask());
        Assert.ThrowsAny<IOException>(() => run.RecordAction()); Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task CancellationBeforeAcceptanceWritesNothingAndAcceptedWorkStillSettles()
    {
        var source = new HeldJournal(); var run = new RunModel(source);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.AdmitContractAsync(Contract(), cancellationToken: cancelled.Token).AsTask());
        Assert.Equal(0, source.Calls); Assert.Null(run.View);
        using var accepted = new CancellationTokenSource();
        var pending = run.AdmitContractAsync(Contract(), cancellationToken: accepted.Token).AsTask(); accepted.Cancel();
        Assert.False(pending.IsCompleted); Assert.Null(run.State);
        source.Release.SetResult(); Assert.True((await pending).Accepted);
    }

    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "offline"); }
    private sealed class Effect : IEffectDriver
    {
        public int Deliveries { get; private set; }
        public DispatchResult Deliver(DispatchRequest request)
        { Deliveries++; return new(DispatchOutcome.DeliveryCompleted, "original", DateTimeOffset.Parse("2026-10-25T00:00:00Z")); }
    }
    private sealed class Seed : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => input.Previous is null
            ? new(AssociationDispositionKind.New, null, [new("(new)", [input.Current.EvidenceId], [])], [], "original seed")
            : new(AssociationDispositionKind.Insufficient, null, [], [], "seed once");
    }
    private sealed class Occurrence(string container) : IUiObservationStrategy
    { public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) => [new(container, "switch", "entry", "ready")]; }

    [Theory]
    [InlineData(RunHistoryTransition.Cycle, false, false)]
    [InlineData(RunHistoryTransition.Cycle, true, false)]
    [InlineData(RunHistoryTransition.Cycle, false, true)]
    [InlineData(RunHistoryTransition.Action, false, false)]
    [InlineData(RunHistoryTransition.Action, true, false)]
    public async Task DriverCannotAdvancePastUnacknowledgedRunProgress(RunHistoryTransition held, bool cancel, bool fail)
    {
        var source = new HeldJournal(held, fail, true); var run = new RunModel(source);
        var seed = new ObservationProposal(new("switch", "original"), IngressKind.Observation, ObservationContext.External,
            new("offline-run-async", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var evidenceId = new EvidenceLedger().Admit(seed).Admission.EvidenceId!;
        var policy = new AgentPlanPolicy(); var effect = new Effect(); var pulls = 0; var consultations = 0;
        var world = new WorldModel(new HashSet<string> { "switch" }, new Seed(), new Occurrence("ctr-" + evidenceId[3..15]));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, run, new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(effect));
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = directive => { pulls++; return directive.Context == ObservationContext.External ? new RunDriverInput.Observation([seed]) : null; },
            ConsultAgent = context => { consultations++; return new AgentDecision.Act(new(context.DecisionId, [new("switch", "entry", "tap", null)], "original")); },
        });
        Assert.True((await kernel.AdmitContractAsync(Contract())).Accepted); Assert.True(driver.Activate().Accepted);
        using var cancellation = new CancellationTokenSource();
        var invocation = Task.Factory.StartNew(() => driver.DriveAsync(cancellation.Token), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var pending = await invocation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(pending.IsCompleted); Assert.Equal(held == RunHistoryTransition.Action ? 1 : 0, effect.Deliveries);
            Assert.Equal(0, run.State!.Progress.Acts); Assert.Equal(held == RunHistoryTransition.Action ? 1 : 0, run.State.Progress.Cycles);
            Assert.Equal(1, pulls); Assert.Equal(1, consultations);
            Assert.Equal("drive-in-progress", driver.Drive().Reason);
            Assert.Equal("drive-in-progress", (await driver.DriveAsync()).Reason);
            Assert.Throws<InvalidOperationException>(() => kernel.Process(seed));
            if (cancel) { cancellation.Cancel(); Assert.False(pending.IsCompleted); }
            source.Release.SetResult(); var result = await pending;
            Assert.Equal(1, consultations);
            if (fail)
            {
                Assert.Contains("run-state-persistence-failed", result.Reason);
                Assert.Equal(0, effect.Deliveries); Assert.Equal(0, run.State.Progress.Cycles);
                Assert.Contains("run-state-persistence-failed", (await driver.DriveAsync()).Reason);
                Assert.Equal(2, source.Calls);
            }
            else if (cancel && held == RunHistoryTransition.Cycle) Assert.Equal(0, effect.Deliveries);
            else Assert.Equal(1, effect.Deliveries);
        }
        finally { source.Release.TrySetResult(); await (await invocation.WaitAsync(TimeSpan.FromSeconds(3))); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnacknowledgedAdmissionNeverPublishesItsIdentityEvenAfterAnOriginalCommit(bool afterCommit)
    {
        var source = new HeldJournal(RunHistoryTransition.Admitted, true, afterCommit); var run = new RunModel(source);
        var pending = run.AdmitContractAsync(Contract()).AsTask(); source.Release.SetResult();
        await Assert.ThrowsAnyAsync<IOException>(() => pending);
        Assert.Null(run.View); Assert.Empty(run.RunId); Assert.Null(run.State); Assert.Null(run.HistoryPosition);
        Assert.Equal(afterCommit ? 1 : 0, source.ReadRecords().Count);
        await Assert.ThrowsAnyAsync<IOException>(() => run.AdmitContractAsync(Contract()).AsTask()); Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task KernelCannotEmitOutcomeUntilTheOriginalTerminalStateIsAcknowledged()
    {
        var source = new HeldJournal(RunHistoryTransition.Terminal); var run = new RunModel(source);
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "switch" }), DisabledRunTrace.Instance,
            run, new ControlLoop(new AgentPlanPolicy()), new RuntimeAssurance(new Fresh()), new EffectBoundary(new Effect()));
        await kernel.AdmitContractAsync(Contract() with { Obligations = [new("goal", RunObligationKind.Objective, "switch", "ready", true)] });
        await kernel.ProcessBatchAsync([new(new("switch", "ready"), IngressKind.Observation, ObservationContext.External,
            new("offline", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]))]);
        var pending = kernel.EvaluateTerminalAsync().AsTask();
        Assert.False(pending.IsCompleted); Assert.False(kernel.IsRunTerminal); Assert.Null(run.State!.Outcome);
        Assert.Throws<InvalidOperationException>(() => kernel.EvaluateTerminal());
        source.Release.SetResult(); var result = await pending;
        Assert.True(result.Transition.Accepted); Assert.NotNull(result.Outcome); Assert.True(kernel.IsRunTerminal);
        Assert.False((await kernel.EvaluateTerminalAsync()).Transition.Accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TraversalHandlerCannotStartBeforeItsOriginalRunRecordIsAcknowledged(bool cancel)
    {
        var source = new HeldJournal(RunHistoryTransition.Traversal, traversal: RunTraversalTransition.Handler); var run = new RunModel(source);
        var seed = new ObservationProposal(new("switch", "original"), IngressKind.Observation, ObservationContext.External,
            new("offline-run-traversal", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]));
        var evidenceId = new EvidenceLedger().Admit(seed).Admission.EvidenceId!; var container = "ctr-" + evidenceId[3..15];
        var policy = new AgentPlanPolicy(); var effect = new Effect(); var handlers = 0; var consultations = 0;
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "switch" }, new Seed(), new Occurrence(container)),
            DisabledRunTrace.Instance, run, new ControlLoop(policy), new RuntimeAssurance(new Fresh()), new EffectBoundary(effect));
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = _ => new RunDriverInput.Observation([seed]),
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "original page" },
            ProjectTraversal = _ =>
            {
                var revision = kernel.CurrentBelief!; var occurrence = revision.Occurrences!.Single();
                return new(new(revision.RevisionId, $"{kernel.RunId}:{kernel.RunView!.Version}", container, revision.Containers!, revision.Relations!, []),
                    [new(new("entry", occurrence.Role, occurrence.SemanticDescriptor!, PlanCandidateDisposition.Ready, occurrence.EvidenceBasis),
                        new Dictionary<string, PolicyTruth> { ["always"] = PolicyTruth.Satisfied })], false, revision.EvidenceBasis.ToArray());
            },
            ConsultAgent = context => ++consultations == 1 ? new AgentDecision.Plan(context.DecisionId,
                new([new PlanItem.TraverseItem("current", [new("r1", "always", new("inspect", TraversalOperationKind.ReadOnly, null,
                    [new("english", "runtime.language-inspector", "1.0", "English")], []))], 0,
                    new("switch", "entry", "tap", null))], "offline traversal", "async-plan", MaxLocalOperations: 16))
                : new AgentDecision.Blocked(context.DecisionId, "done"),
            CanBindPlanLanguage = _ => true,
            InspectPlanLanguageAsync = (_, input, _) =>
            {
                handlers++;
                return ValueTask.FromResult<Finding?>(new("original-finding", new(kernel.RunId!, domainCorrelation: input.SourceRevisionId,
                    operationId: input.OperationId), "1.0", "runtime.language-inspector", FindingDisposition.Pass, CapabilityStatus.Completed, "original"));
            },
        });
        await kernel.AdmitContractAsync(Contract()); Assert.True(driver.Activate().Accepted);
        using var cancellation = new CancellationTokenSource(); var pending = driver.DriveAsync(cancellation.Token);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Equal(0, handlers); Assert.Empty(run.TraversalHandlers);
        Assert.Single(run.TraversalOperations); Assert.Equal(0, effect.Deliveries);
        if (cancel) cancellation.Cancel();
        source.Release.SetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(cancel ? 0 : 1, handlers); Assert.Equal(0, effect.Deliveries);
        Assert.Equal(cancel ? PlanHandlerCallStatus.Cancelled : PlanHandlerCallStatus.Completed, Assert.Single(run.TraversalHandlers).Status);
    }
}
