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

public sealed class AuxiliaryObservationIsolationTests
{
    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "offline");
    }

    private sealed record RunSnapshot(
        RunDriveStatus Status,
        string? Reason,
        AgentDecision OriginalDecision,
        ConsultationJournalEntry JournalEntry);

    private sealed record DriverFixture(
        UniKernel Kernel,
        KernelRunDriver Driver,
        InMemoryConsultationJournal Journal,
        Func<ConsultationJournalEntry?> JournalEntry,
        Func<AgentDecision?> OriginalDecision,
        Func<IReadOnlyList<ConsultationJournalEntry>> JournalEntries,
        Func<IReadOnlyList<AgentDecision>> OriginalDecisions);

    private static DriverFixture Compose(
        Func<AgentDecisionContext, AgentDecision?, CancellationToken, ValueTask>? observer = null,
        DiagnosticObservationOptions? observationOptions = null,
        Func<AgentDecisionContext, AgentDecision?>? consultAgent = null)
    {
        var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "live.frame" }),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy), new RuntimeAssurance(new Fresh()));
        Assert.True(kernel.AdmitContract(new("observer-isolation-v1", "observer isolation",
            new HashSet<string> { "live.frame" }, new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
            TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract")).Accepted);

        var journal = new InMemoryConsultationJournal();
        var keys = new List<ConsultationKey>();
        var originalDecisions = new List<AgentDecision>();
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ConsultationJournal = journal,
            DiagnosticObservationOptions = observationOptions ?? new(),
            NextInput = directive => new RunDriverInput.Observation([new(
                new("live.frame", "seed"), IngressKind.Observation, directive.Context,
                new("offline", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "seed", ["offline"]))]),
            ConsultAgent = context =>
            {
                keys.Add(new(context.RunId, context.ContractViewRef!, context.DecisionBoundaryId!, context.DecisionId));
                var decision = consultAgent?.Invoke(context)
                    ?? new AgentDecision.NoAction(new(context.DecisionId, "done",
                        new("observed", ["obs:" + kernel.CurrentBelief!.EvidenceBasis.First()])));
                originalDecisions.Add(decision);
                return decision;
            },
            ObserveConsultationAsync = observer,
        });

        Assert.True(driver.Activate().Accepted);
        IReadOnlyList<ConsultationJournalEntry> JournalEntries() => keys
            .Select(journal.Find).OfType<ConsultationJournalEntry>().ToArray();
        return new(kernel, driver, journal, () => JournalEntries().LastOrDefault(),
            () => originalDecisions.LastOrDefault(), JournalEntries, () => originalDecisions.ToArray());
    }

    private static async Task<RunSnapshot> RunAsync(bool observerThrows)
    {
        Func<AgentDecisionContext, AgentDecision?, CancellationToken, ValueTask>? observer = observerThrows
            ? (_, _, _) => ValueTask.FromException(new IOException("auxiliary export failed"))
            : null;
        var fixture = Compose(observer);
        await using var driver = fixture.Driver;
        var result = await driver.DriveAsync();
        await driver.CompleteDiagnosticObservationsAsync();
        Assert.NotNull(fixture.JournalEntry());
        Assert.NotNull(fixture.OriginalDecision());
        return new(result.Status, result.Reason, fixture.OriginalDecision()!, fixture.JournalEntry()!);
    }

    [Fact]
    public async Task ObserverFailureDoesNotChangeFormalConsultationSettlement()
    {
        var withoutObserver = await RunAsync(observerThrows: false);
        var withFailingObserver = await RunAsync(observerThrows: true);

        Assert.NotEqual(RunDriveStatus.AgentDecisionFailed, withoutObserver.Status);
        Assert.Equal(ConsultationJournalStatus.Completed, withoutObserver.JournalEntry.Status);
        Assert.Equal(withoutObserver.OriginalDecision, withoutObserver.JournalEntry.Decision);
        Assert.Equal(ConsultationJournalStatus.Completed, withFailingObserver.JournalEntry.Status);
        Assert.Equal(withFailingObserver.OriginalDecision, withFailingObserver.JournalEntry.Decision);
        Assert.Equal(withoutObserver.Status, withFailingObserver.Status);
        Assert.Equal(withoutObserver.Reason, withFailingObserver.Reason);
        Assert.Equal(withoutObserver.JournalEntry.ConsumedOrdinal, withFailingObserver.JournalEntry.ConsumedOrdinal);
        Assert.Null(withFailingObserver.JournalEntry.RejectionReason);
    }

    [Fact]
    public async Task StalledObserverDoesNotBlockDriveAndCloseReportsUnsettledWithoutChangingP25()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = Compose(async (_, _, _) =>
        {
            entered.TrySetResult();
            try { await release.Task; }
            finally { exited.TrySetResult(); }
        }, new DiagnosticObservationOptions
        {
            Capacity = 1,
            OperationTimeout = TimeSpan.FromSeconds(10),
            CloseTimeout = TimeSpan.FromMilliseconds(100),
        });
        await using var driver = fixture.Driver;

        try
        {
            var drive = driver.DriveAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var result = await drive.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.NotEqual(RunDriveStatus.AgentDecisionFailed, result.Status);

            var beforeClose = Assert.IsType<ConsultationJournalEntry>(fixture.JournalEntry());
            Assert.Equal(ConsultationJournalStatus.Completed, beforeClose.Status);
            Assert.Equal(fixture.OriginalDecision(), beforeClose.Decision);

            var closeStatus = await driver.CompleteDiagnosticObservationsAsync();
            Assert.Equal(1, closeStatus.TimedOut);
            Assert.True(closeStatus.Unsettled);
            Assert.Equal(beforeClose, fixture.JournalEntry());
        }
        finally
        {
            release.TrySetResult();
            await exited.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await driver.CompleteDiagnosticObservationsAsync();
        }
    }

    [Fact]
    public async Task ObserverExceptionIsReportedAfterCompletionWithoutRejectingP25()
    {
        var fixture = Compose((_, _, _) => ValueTask.FromException(new IOException("auxiliary export failed")));
        await using var driver = fixture.Driver;

        var result = await driver.DriveAsync();
        var observationStatus = await driver.CompleteDiagnosticObservationsAsync();
        var journalEntry = Assert.IsType<ConsultationJournalEntry>(fixture.JournalEntry());

        Assert.NotEqual(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal(ConsultationJournalStatus.Completed, journalEntry.Status);
        Assert.Equal(fixture.OriginalDecision(), journalEntry.Decision);
        Assert.Equal(1, observationStatus.Accepted);
        Assert.Equal(1, observationStatus.Failed);
        Assert.Equal(0, observationStatus.Completed);
        Assert.False(observationStatus.Unsettled);
    }

    [Fact]
    public async Task DiagnosticObserverCongestionDoesNotChangeFormalConsultationsOrRunResult()
    {
        static AgentDecision Consult(AgentDecisionContext context) => context.Phase == AgentDecisionPhase.DeferRoundsExhausted
            ? new AgentDecision.Blocked(context.DecisionId, "congestion-test-stop")
            : new AgentDecision.Defer(context.DecisionId, new ObserveSpec(null, 1));

        DriverFixture? congestedFixture = null;
        async Task<(RunDriveResult Result, IReadOnlyList<ConsultationJournalEntry> Entries, int ConsumedCount, DiagnosticObservationStatus Diagnostics)> DriveArm(
            bool congestObserver,
            TaskCompletionSource? entered = null,
            TaskCompletionSource? release = null,
            TaskCompletionSource? exited = null)
        {
            Func<AgentDecisionContext, AgentDecision?, CancellationToken, ValueTask>? observer = null;
            if (congestObserver)
            {
                observer = async (_, _, _) =>
                {
                    entered!.TrySetResult();
                    try { await release!.Task; }
                    finally { exited!.TrySetResult(); }
                };
            }

            var fixture = Compose(observer, new DiagnosticObservationOptions
            {
                Capacity = 1,
                OperationTimeout = TimeSpan.FromSeconds(30),
                CloseTimeout = TimeSpan.FromSeconds(5),
            }, Consult);
            if (congestObserver) congestedFixture = fixture;
            var driver = fixture.Driver;
            var result = await driver.DriveAsync().WaitAsync(TimeSpan.FromSeconds(2));
            if (congestObserver) await entered!.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var entries = fixture.JournalEntries();
            Assert.Equal(3, entries.Count);
            var count = fixture.Journal.ConsumedCount(entries[0].Key.RunId, entries[0].Key.ContractViewRef);
            var diagnostics = driver.DiagnosticObservations;

            if (congestObserver)
            {
                Assert.True(diagnostics.Dropped > 0);
                Assert.InRange(diagnostics.Accepted, 1, 2);
                Assert.InRange(diagnostics.Pending, 1, 2);
                Assert.Equal(3, count);
            }
            else
            {
                Assert.Equal(0, diagnostics.Accepted);
                await driver.CompleteDiagnosticObservationsAsync();
                await driver.DisposeAsync();
            }

            return (result, entries, count, diagnostics);
        }

        var withoutObserver = await DriveArm(congestObserver: false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        (RunDriveResult Result, IReadOnlyList<ConsultationJournalEntry> Entries, int ConsumedCount, DiagnosticObservationStatus Diagnostics) withObserver;
        try
        {
            withObserver = await DriveArm(congestObserver: true, entered, release, exited);

            Assert.Equal(withoutObserver.Result.Status, withObserver.Result.Status);
            Assert.Equal(withoutObserver.Result.Reason, withObserver.Result.Reason);
            Assert.Equal(withoutObserver.ConsumedCount, withObserver.ConsumedCount);
            Assert.Equal(3, withObserver.ConsumedCount);
            Assert.All(withObserver.Entries, entry => Assert.Equal(ConsultationJournalStatus.Completed, entry.Status));
            Assert.Equal(withoutObserver.Entries.Select(entry => entry.Status), withObserver.Entries.Select(entry => entry.Status));
            Assert.Equal(withoutObserver.Entries.Select(entry => entry.ConsumedOrdinal), withObserver.Entries.Select(entry => entry.ConsumedOrdinal));
            Assert.Equal(withoutObserver.Entries.Select(entry => entry.Decision!.GetType()),
                withObserver.Entries.Select(entry => entry.Decision!.GetType()));
        }
        finally
        {
            release.TrySetResult();
            if (congestedFixture is not null)
            {
                if (entered.Task.IsCompleted) await exited.Task.WaitAsync(TimeSpan.FromSeconds(2));
                await congestedFixture.Driver.CompleteDiagnosticObservationsAsync();
                await congestedFixture.Driver.DisposeAsync();
            }
        }
    }
}
