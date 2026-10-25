using System.Text.Json;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed partial class TraversalExecutionTests
{
    private sealed class HeldAsyncPlan(string operation, bool fail = false, bool afterCommit = false, bool replacementOnly = false) : IAsyncRuntimePlanJournal
    {
        private readonly List<RuntimePlanHistoryRecord> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public IReadOnlyList<RuntimePlanHistoryRecord> ReadRecords() => _rows.ToArray();
        public void Append(RuntimePlanHistoryRecord row) => throw new IOException("synchronous plan write");
        public async ValueTask AppendAsync(RuntimePlanHistoryRecord row, CancellationToken cancellationToken = default)
        {
            Calls++;
            var hold = row.Operation == operation && (!replacementOnly || row.Sequence > 2);
            if (hold)
            {
                Entered.TrySetResult(); await Release.Task;
                if (fail && !afterCommit) throw new IOException("before-plan-commit");
            }
            _rows.Add(row);
            if (hold && fail) throw new IOException("plan-ack-lost");
        }
    }
    private sealed class HeldAsyncControl(string operation = "select", bool fail = false, bool afterCommit = false, bool batch = false) : IAsyncControlTraversalJournal
    {
        private readonly List<ControlTraversalHistoryRecord> _rows = [];
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public IReadOnlyList<ControlTraversalHistoryRecord> ReadRecords() => _rows.ToArray();
        public int BatchCalls { get; private set; }
        public bool CanAppendBatch => batch;
        public void Append(ControlTraversalHistoryRecord row)
        { if (row.Operation != "initialize") throw new IOException("synchronous control write"); _rows.Add(row); }
        public async ValueTask AppendAsync(ControlTraversalHistoryRecord row, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (row.Operation == operation)
            {
                Entered.TrySetResult(); await Release.Task;
                if (fail && !afterCommit) throw new IOException("before-control-commit");
            }
            _rows.Add(row);
            if (row.Operation == operation && fail) throw new IOException("control-ack-lost");
        }
        public async ValueTask AppendBatchAsync(IReadOnlyList<ControlTraversalHistoryRecord> rows, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            Assert.Equal(new[] { "configure-rules", "start" }, rows.Select(row => row.Operation));
            if (!fail || afterCommit) _rows.AddRange(rows);
            Entered.TrySetResult(); await Release.Task;
            if (fail) throw new IOException(afterCommit ? "control-batch-ack-lost" : "before-control-batch");
        }
    }

    [Theory]
    [InlineData("configure-rules")]
    [InlineData("start")]
    [InlineData("select")]
    [InlineData("drain")]
    [InlineData("read-only-completed")]
    public async Task GroupedStorageControlSelectionReturnsAnIncompleteTaskAndDoesNotHandOffWork(string operation)
    {
        var source = new HeldAsyncControl(operation); var run = new RunModel(new OriginalRunJournal());
        var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, source); var consultations = 0;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision), false), context =>
            ++consultations == 1 ? Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], []))
                : new AgentDecision.Blocked(context.DecisionId, "done"), run: run, control: control, policy: policy);
        var invocation = Task.Factory.StartNew(() => driver.DriveAsync(), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var pending = await invocation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(pending.IsCompleted); Assert.Empty(kernel.EffectReceipts);
            if (operation == "read-only-completed") Assert.Single(run.TraversalOperations); else Assert.Empty(run.TraversalOperations);
            Assert.DoesNotContain(control.TraversalHistory!.Records, row => row.Operation == operation);
            Assert.Equal("drive-in-progress", driver.Drive().Reason); Assert.Equal(1, consultations);
            source.Release.SetResult(); await pending;
            Assert.Contains(control.TraversalHistory.Records, row => row.Operation == operation);
        }
        finally { source.Release.TrySetResult(); await (await invocation.WaitAsync(TimeSpan.FromSeconds(3))); }
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("adopt")]
    public async Task GroupedStoragePlanMustAcknowledgeBeforePublishingOrExecuting(string operation)
    {
        var source = new HeldAsyncPlan(operation); var run = new RunModel(new OriginalRunJournal()); var consultations = 0;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision), false), context =>
            ++consultations == 1 ? Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], []))
                : new AgentDecision.Blocked(context.DecisionId, "done"), run: run, planJournal: source);
        var invocation = Task.Factory.StartNew(() => driver.DriveAsync(), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var pending = await invocation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(pending.IsCompleted); Assert.Empty(run.TraversalOperations); Assert.Empty(kernel.EffectReceipts);
            Assert.Null(driver.RuntimePlanHistory?.LatestCommittedAdoption); Assert.Null(driver.PlanExecution);
            Assert.Equal(operation == "initialize" ? 0 : 1, consultations);
            source.Release.SetResult(); await pending;
            Assert.NotNull(driver.RuntimePlanHistory!.LatestCommittedAdoption);
        }
        finally { source.Release.TrySetResult(); await (await invocation.WaitAsync(TimeSpan.FromSeconds(3))); }
    }

    [Fact]
    public async Task GroupedStorageControlInitializationBlocksActivationAndReentryUntilAcknowledged()
    {
        var source = new HeldAsyncControl("initialize"); var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, source);
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { UIWorldDoubles.Observed }), DisabledRunTrace.Instance,
            new RunModel(new OriginalRunJournal()), control);
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs { NextInput = _ => null, ConsultAgent = _ => null });
        var pending = kernel.AdmitContractAsync(Contract()).AsTask();
        Assert.False(pending.IsCompleted); Assert.Null(control.TraversalHistory); Assert.False(driver.Activate().Accepted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => kernel.AdmitContractAsync(Contract()).AsTask());
        source.Release.SetResult(); Assert.True((await pending).Accepted);
        Assert.Equal(1, control.TraversalHistory!.Position.Sequence); Assert.True(driver.Activate().Accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupedStorageAcceptedCancellationSettlesButNeverStartsWork(bool plan)
    {
        var controls = new HeldAsyncControl(plan ? "never" : "select"); var plans = new HeldAsyncPlan("adopt");
        var run = new RunModel(new OriginalRunJournal()); var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, controls);
        var calls = 0;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision), false), context =>
            ++calls == 1 ? Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], []))
                : new AgentDecision.Blocked(context.DecisionId, "done"), run: run, control: control, policy: policy, planJournal: plan ? plans : null);
        using var cancellation = new CancellationTokenSource(); var pending = driver.DriveAsync(cancellation.Token);
        await (plan ? plans.Entered.Task : controls.Entered.Task).WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel(); Assert.False(pending.IsCompleted); Assert.Empty(run.TraversalOperations);
        (plan ? plans.Release : controls.Release).SetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Empty(run.TraversalOperations); Assert.Empty(kernel.EffectReceipts); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task GroupedStorageLostAcknowledgementLatchesWithoutPublicationOrRetry(bool plan, bool afterCommit)
    {
        var controls = new HeldAsyncControl(plan ? "never" : "select", !plan, afterCommit);
        var plans = new HeldAsyncPlan("adopt", true, afterCommit); var run = new RunModel(new OriginalRunJournal());
        var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, controls);
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision), false), context =>
            Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], [])), run: run, control: control, policy: policy, planJournal: plan ? plans : null);
        var pending = driver.DriveAsync(); (plan ? plans.Release : controls.Release).SetResult(); var result = await pending;
        Assert.Contains(plan ? "runtime-plan-persistence-failed" : "control-traversal-persistence-failed", result.Reason);
        Assert.Empty(run.TraversalOperations); Assert.Empty(kernel.EffectReceipts);
        var position = plan ? driver.RuntimePlanHistory!.Position.Sequence : control.TraversalHistory!.Position.Sequence;
        Assert.Equal(position + (afterCommit ? 1 : 0), plan ? plans.ReadRecords().Count : controls.ReadRecords().Count);
        var count = plan ? plans.Calls : controls.Calls;
        Assert.Contains("persistence-failed", (await driver.DriveAsync()).Reason);
        Assert.Equal(count, plan ? plans.Calls : controls.Calls);
    }

    private static (UniKernel Kernel, KernelRunDriver Driver) ComposeAsyncReplacement(HeldAsyncControl controls, HeldAsyncPlan plans)
    {
        var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, controls); var calls = 0;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
        {
            if (++calls == 1)
            {
                var first = (AgentDecision.Plan)Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null,
                    [new("handler", "runtime.language-inspector", "1.0", "English")], []));
                return first with { Proposal = first.Proposal with { MaxLocalOperations = 1 } };
            }
            return new AgentDecision.Plan(context.DecisionId, new([new PlanItem.ControlItem(PlanControlKind.Stop, "replacement stop")],
                "replacement", "traversal-plan", 2));
        }, run: new RunModel(new OriginalRunJournal()), control: control, policy: policy, planJournal: plans,
            canBind: _ => true, inspect: (_, _) => throw new InvalidOperationException("replacement must not start old handler"));
        return (kernel, driver);
    }

    [Theory]
    [InlineData("replacement-begin", false)]
    [InlineData("abandon", false)]
    [InlineData("adopt", false)]
    [InlineData("replacement-begin", true)]
    [InlineData("abandon", true)]
    [InlineData("adopt", true)]
    public async Task GroupedStorageReplacementPreservesEachOriginalAcknowledgementBarrier(string stage, bool cancel)
    {
        var controls = new HeldAsyncControl(stage == "abandon" ? "abandon" : "never");
        var plans = new HeldAsyncPlan(stage == "abandon" ? "never" : stage, replacementOnly: true);
        var (kernel, driver) = ComposeAsyncReplacement(controls, plans);
        using var cancellation = new CancellationTokenSource(); var pending = driver.DriveAsync(cancellation.Token);
        await (stage == "abandon" ? controls.Entered.Task : plans.Entered.Task).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Equal(1, driver.RuntimePlanHistory!.LatestCommittedAdoption!.Versions["traversal-plan"]);
        Assert.Equal(stage == "replacement-begin" ? 2 : 3, plans.ReadRecords().Count);
        Assert.Equal(stage == "adopt" ? 1 : 0, controls.ReadRecords().Count(row => row.Operation == "abandon"));
        Assert.Empty(kernel.EffectReceipts);
        if (cancel) { cancellation.Cancel(); Assert.False(pending.IsCompleted); }
        (stage == "abandon" ? controls.Release : plans.Release).SetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "initialize", "adopt", "replacement-begin", "adopt" }, plans.ReadRecords().Select(row => row.Operation));
        Assert.Equal(2, driver.RuntimePlanHistory.LatestCommittedAdoption!.Versions["traversal-plan"]);
        Assert.Equal(1, controls.ReadRecords().Count(row => row.Operation == "abandon"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupedStorageReplacementControlFailureCannotReachTheNewAdoption(bool afterCommit)
    {
        var controls = new HeldAsyncControl("abandon", true, afterCommit); var plans = new HeldAsyncPlan("never");
        var (kernel, driver) = ComposeAsyncReplacement(controls, plans);
        var pending = driver.DriveAsync(); await controls.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        controls.Release.SetResult(); var result = await pending;
        Assert.Contains("control-traversal-persistence-failed", result.Reason); Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(new[] { "initialize", "adopt", "replacement-begin" }, plans.ReadRecords().Select(row => row.Operation));
        Assert.NotNull(driver.RuntimePlanHistory!.PendingReplacement);
        Assert.Equal(1, driver.RuntimePlanHistory.LatestCommittedAdoption!.Versions["traversal-plan"]);
        Assert.Equal(afterCommit ? 1 : 0, controls.ReadRecords().Count(row => row.Operation == "abandon"));
        var planCalls = plans.Calls; var controlCalls = controls.Calls;
        Assert.Contains("control-traversal-persistence-failed", (await driver.DriveAsync()).Reason);
        Assert.Equal(planCalls, plans.Calls); Assert.Equal(controlCalls, controls.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupedStorageNativePairPublishesNeitherRowBeforeItsJointAcknowledgement(bool cancel)
    {
        var source = new HeldAsyncControl("never", batch: true); var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, source);
        var run = new RunModel(new OriginalRunJournal()); var calls = 0;
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision), false), context =>
            ++calls == 1 ? Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], []))
                : new AgentDecision.Blocked(context.DecisionId, "done"), run: run, control: control, policy: policy);
        using var cancellation = new CancellationTokenSource(); var pending = driver.DriveAsync(cancellation.Token);
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted); Assert.Equal(1, control.TraversalHistory!.Position.Sequence); Assert.Equal(3, source.ReadRecords().Count);
        Assert.Empty(run.TraversalOperations); Assert.Empty(kernel.EffectReceipts);
        Assert.Throws<InvalidOperationException>(() => kernel.AdmitContract(Contract()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => kernel.AdmitContractAsync(Contract()).AsTask());
        if (cancel) { cancellation.Cancel(); Assert.False(pending.IsCompleted); }
        source.Release.SetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, source.BatchCalls);
        Assert.Equal(new[] { "initialize", "configure-rules", "start" }, control.TraversalHistory.Records.Take(3).Select(row => row.Operation));
        if (cancel) { Assert.Empty(run.TraversalOperations); Assert.DoesNotContain(source.ReadRecords(), row => row.Operation == "select"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupedStorageNativePairFailureNeverPublishesEitherRowOrRetries(bool afterCommit)
    {
        var source = new HeldAsyncControl("never", true, afterCommit, true); var policy = new AgentPlanPolicy(); var control = new ControlLoop(policy, source);
        var run = new RunModel(new OriginalRunJournal());
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
            Plan(context, new("inspect", TraversalOperationKind.ReadOnly, null, [], [])), run: run, control: control, policy: policy);
        var pending = driver.DriveAsync(); source.Release.SetResult(); var result = await pending;
        Assert.Contains("control-traversal-persistence-failed", result.Reason);
        Assert.Equal(1, control.TraversalHistory!.Position.Sequence); Assert.Equal(afterCommit ? 3 : 1, source.ReadRecords().Count);
        Assert.Empty(run.TraversalOperations); Assert.Empty(kernel.EffectReceipts);
        await driver.DriveAsync(); Assert.Equal(1, source.BatchCalls);
    }

}
