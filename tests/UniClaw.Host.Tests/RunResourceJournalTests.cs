using System.Security.Cryptography;
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
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunResourceJournalTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("resource-journal-").FullName;
    private string PathFor(string name) => Path.Combine(_root, name);
    public void Dispose() => Directory.Delete(_root, true);

    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "synthetic freshness"); }
    private sealed class NoEffect : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no effect allowed"); }
    private sealed class Fault(IRunResourceJournal inner, Func<RunResourceRecord, bool> fail) : IRunResourceJournal
    {
        public bool CanInitialize => inner.CanInitialize;
        public IReadOnlyList<RunResourceRecord> ReadRecords() => inner.ReadRecords();
        public void Append(RunResourceRecord record)
        { if (fail(record)) throw new IOException("synthetic durable write failure"); inner.Append(record); }
    }
    private sealed class InterruptedP25(IConsultationJournal inner, bool afterCommit) : IConsultationJournal
    {
        public ConsultationJournalEntry? Find(ConsultationKey key) => inner.Find(key);
        public ConsultationJournalEntry Reserve(ConsultationKey key, string fingerprint) => inner.Reserve(key, fingerprint);
        public ConsultationJournalEntry Complete(ConsultationKey key, string fingerprint, AgentDecision decision)
        {
            if (afterCommit) inner.Complete(key, fingerprint, decision);
            throw new IOException("synthetic interruption at P25 settlement");
        }
        public ConsultationJournalEntry Reject(ConsultationKey key, string fingerprint, string reason) => inner.Reject(key, fingerprint, reason);
        public int ConsumedCount(string run, string view) => inner.ConsumedCount(run, view);
        public bool HasPending(string run, string view) => inner.HasPending(run, view);
    }

    private static (UniKernel Kernel, KernelRunDriver Driver) Compose(IRunResourceJournal journal,
        IConsultationJournal consultations, Func<AgentDecisionContext, AgentDecision?> consult,
        AgentPlanPolicy? policy = null, IEffectDriver? effects = null,
        Func<ObservationDirective, RunDriverInput?>? feed = null, IReliableExecutionSource? executions = null,
        Func<ExecutionOverviewProjection?>? overview = null, string contractRef = "resource-contract",
        string taskInstanceId = "resource-task")
    {
        policy ??= new AgentPlanPolicy();
        var scope = new HashSet<string> { "live.frame", "ui.*" };
        var kernel = new UniKernel(new EvidenceLedger(),
            new WorldModel(scope, new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy()),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(effects ?? new NoEffect(), executions));
        var serial = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = journal, ConsultationJournal = consultations, ConsultAgent = consult,
            ProjectExecutionOverview = overview,
            NextInput = feed ?? (_ => new RunDriverInput.Observation([new(new("live.frame", "synthetic-seed"),
                IngressKind.Observation, ObservationContext.External,
                new("synthetic-resource-test", DateTimeOffset.Parse("2026-10-24T00:00:00Z"),
                    "seed:" + ++serial, ["synthetic", "offline"]))])),
        });
        Assert.True(kernel.AdmitContract(new("resources-v1", "resource durability", scope,
            new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            MaxConsultations: 4, MaxTotalSteps: 10, TaskInstanceId: taskInstanceId,
            ProductSessionId: "resource-session", ContractRef: contractRef)).Accepted);
        Assert.True(driver.Activate().Accepted);
        return (kernel, driver);
    }

    [Fact]
    public void NoResponseIsAConfirmedPaidConsultationWithoutGrant()
    {
        var path = PathFor("resources");
        using var executions = new FileExecutionJournal(PathFor("executions"));
        using var journal = new FileRunResourceJournal(path, consultationPath: PathFor("p25"), executionPath: PathFor("executions"));
        using var p25 = new FileConsultationJournal(PathFor("p25"));
        var (_, driver) = Compose(journal, p25, _ => null, executions: executions);
        Assert.Equal("no-response", driver.Drive().Reason);
        var read = FileRunResourceJournal.Inspect(path, driver.ResourceBinding, PathFor("p25"), PathFor("executions"));
        Assert.True(read.AccountingComplete, read.Reason);
        Assert.Equal(1, read.Confirmed!.RoundsConsumed); Assert.Equal(3, read.RoundsRemaining);
        Assert.Equal(0, read.Confirmed.GrantedRounds);
        Export("no-response");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedSettlementNeverAutoConfirmsOrConsultsAgain(bool afterCommit)
    {
        var path = PathFor("resources");
        using var journal = new FileRunResourceJournal(path);
        using var durableP25 = new FileConsultationJournal(PathFor("p25"));
        var calls = 0;
        var (_, driver) = Compose(journal, new InterruptedP25(durableP25, afterCommit), ctx =>
        { calls++; return new AgentDecision.Blocked(ctx.DecisionId, "not yet adopted"); });
        Assert.Throws<IOException>(() => driver.Drive());
        Assert.Contains("resource-operation-interrupted", driver.Drive().Reason);
        Assert.Equal(1, calls);
        var read = FileRunResourceJournal.Inspect(path);
        Assert.False(read.AccountingComplete); Assert.Equal("consult", read.PendingOperation);
        Assert.Equal(2, journal.ReadRecords().Count); // initialization + unfinished Begin, no confirmation
        Export("interrupted-" + afterCommit);
    }

    [Fact]
    public void RawRenewalResponseDoesNotBecomeAnAcceptedGrant()
    {
        var path = PathFor("resources");
        using var executions = new FileExecutionJournal(PathFor("executions"));
        using var journal = new FileRunResourceJournal(path, consultationPath: PathFor("p25"), executionPath: PathFor("executions"));
        using var p25 = new FileConsultationJournal(PathFor("p25"));
        var calls = 0;
        var (_, driver) = Compose(journal, p25, ctx => ++calls == 1
            ? new AgentDecision.Defer(ctx.DecisionId, new(null, 1), new(100, 100, "over authorized bound"))
            : new AgentDecision.Blocked(ctx.DecisionId, "stop"),
            new() { HardCapTotalSteps = 10, HardCapConsultations = 4, RenewalThresholdSteps = 10, RenewalThresholdRounds = 4 }, executions: executions);
        driver.Drive();
        var read = FileRunResourceJournal.Inspect(path, driver.ResourceBinding, PathFor("p25"), PathFor("executions"));
        Assert.True(read.AccountingComplete, read.Reason);
        Assert.Equal(0, read.Confirmed!.GrantedSteps); Assert.Equal(0, read.Confirmed.GrantedRounds);
        var denied = Assert.Single(read.Confirmed.Renewals);
        Assert.False(denied.Granted); Assert.NotNull(denied.RejectionReason);
        Assert.Equal(100, denied.AdditionalSteps); Assert.Equal(2, read.RoundsRemaining);
        Export("denied-proposal");
    }

    [Fact]
    public void GoalDirectedGrantRetainsItsUsedItemProgressAndReviewHistory()
    {
        var path = PathFor("resources");
        using var executions = new FileExecutionJournal(PathFor("executions"));
        using var journal = new FileRunResourceJournal(path, consultationPath: PathFor("p25"), executionPath: PathFor("executions"));
        using var p25 = new FileConsultationJournal(PathFor("p25"));
        var calls = 0;
        var (_, driver) = Compose(journal, p25, ctx =>
        {
            if (++calls > 1) return new AgentDecision.Blocked(ctx.DecisionId, "stop");
            var review = Assert.IsType<BudgetReview>(ctx.BudgetReview);
            return new AgentDecision.Defer(ctx.DecisionId, new(null, 1), ReviewResponse:
                new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal,
                    new AgentProgressAssessment(ctx.ExecutionOverview!.OverviewId, "new item", "inspect item a",
                        "inspect a", ["item a"], ["owner:a"], NextItemKey: "pending:a"), new(1, 3, "next branch")));
        }, new() { GoalDirectedContinuationEnabled = true, RenewalThresholdSteps = 10, RenewalThresholdRounds = 4 },
            executions: executions, overview: () => new("branch", [], [], [], null, null, ["owner:a"],
                ProgressFactKeys: ["known:a"], ActionableItems: new Dictionary<string, ActionableItem>
                { ["pending:a"] = new(["owner:a"], ["known:a"]) }));
        driver.Drive();
        var read = FileRunResourceJournal.Inspect(path, driver.ResourceBinding, PathFor("p25"), PathFor("executions"));
        Assert.True(read.AccountingComplete, read.Reason);
        Assert.Single(read.Confirmed!.Renewals.Where(row => row.Granted));
        Assert.Contains("known:a", read.Confirmed.UsedProgressFacts);
        Assert.Equal(new[] { "known:a" }, read.Confirmed.FundedItems["pending:a"]);
        Assert.Contains(read.Confirmed.Reviews, row => row.NextItemKey == "pending:a" && row.RoundsGranted == 3);
        Assert.Equal(11, read.StepsRemaining); Assert.Equal(5, read.RoundsRemaining);
        Export("goal-directed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreexistingP25IsNotMigratedIntoFreshZeroAccounting(bool pending)
    {
        using var journal = new FileRunResourceJournal(PathFor("resources"));
        var p25 = new InMemoryConsultationJournal();
        var calls = 0;
        var (kernel, driver) = Compose(journal, p25, ctx =>
        { calls++; return new AgentDecision.Blocked(ctx.DecisionId, "must not consult"); });
        var key = new ConsultationKey(kernel.RunId, "resource-contract", "old-boundary", "old-decision");
        p25.Reserve(key, "old-fingerprint");
        if (!pending) p25.Reject(key, "old-fingerprint", "no-response");
        Assert.Contains("resource-history-uninitialized-or-legacy", driver.Drive().Reason);
        Assert.Equal(0, calls); Assert.Empty(journal.ReadRecords());
    }

    [Fact]
    public void AcceptedRenewalAndConsumptionSurviveReopenWithoutRecrediting()
    {
        var path = PathFor("resources");
        RunResourceBinding? binding = null;
        using var executions = new FileExecutionJournal(PathFor("executions"));
        using (var journal = new FileRunResourceJournal(path, consultationPath: PathFor("p25"), executionPath: PathFor("executions")))
        using (var p25 = new FileConsultationJournal(PathFor("p25")))
        {
            var calls = 0;
            var (kernel, driver) = Compose(journal, p25, ctx => ++calls == 1
                ? new AgentDecision.Defer(ctx.DecisionId, new(null, 1), new(2, 2, "synthetic next work"))
                : new AgentDecision.Blocked(ctx.DecisionId, "synthetic stop"),
                new() { HardCapTotalSteps = 20, HardCapConsultations = 10, RenewalThresholdSteps = 10, RenewalThresholdRounds = 4 }, executions: executions);
            driver.Drive();
            binding = driver.ResourceBinding;
            Assert.Equal(kernel.RunId, journal.ReadRecords()[0].Identity!.RunId);
            Assert.Equal(2, calls); Assert.Empty(kernel.EffectReceipts);
        }
        var before = SHA256.HashData(File.ReadAllBytes(path));
        for (var i = 0; i < 2; i++)
        {
            var read = FileRunResourceJournal.Inspect(path, binding, PathFor("p25"), PathFor("executions"));
            Assert.True(read.AccountingComplete, read.Reason);
            Assert.Equal(2, read.Confirmed!.RoundsConsumed);
            Assert.Equal(2, read.Confirmed.GrantedSteps); Assert.Equal(2, read.Confirmed.GrantedRounds);
            Assert.True(Assert.Single(read.Confirmed.Renewals).Granted);
            Assert.Equal(12, read.StepsRemaining); Assert.Equal(4, read.RoundsRemaining);
            Assert.False(read.ContinuationSupported);
        }
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        Export("accepted-renewal");
        using var reopened = new FileRunResourceJournal(path, readOnly: true);
        var consulted = 0;
        var (_, freshDriver) = Compose(reopened, new InMemoryConsultationJournal(), ctx =>
        { consulted++; return new AgentDecision.Blocked(ctx.DecisionId, "must not consult"); });
        Assert.Contains("resource-recovery-read-only", freshDriver.Drive().Reason);
        Assert.Equal(0, consulted);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Theory]
    [InlineData(RunResourcePhase.Begin, 0)]
    [InlineData(RunResourcePhase.Confirm, 1)]
    public void ConsultationWriteFailureBlocksFurtherConsultations(RunResourcePhase phase, int expectedCalls)
    {
        var path = PathFor("resources");
        using var journal = new FileRunResourceJournal(path);
        var faulty = new Fault(journal, row => row.Phase == phase);
        var calls = 0;
        var (kernel, driver) = Compose(faulty, new InMemoryConsultationJournal(), ctx =>
        { calls++; return new AgentDecision.Blocked(ctx.DecisionId, "synthetic stop"); });
        Assert.Contains("resource-accounting-failed", driver.Drive().Reason);
        driver.Drive();
        Assert.Equal(expectedCalls, calls); Assert.Empty(kernel.EffectReceipts);
        if (phase == RunResourcePhase.Confirm)
        {
            var read = FileRunResourceJournal.Inspect(path);
            Assert.False(read.AccountingComplete); Assert.Null(read.RoundsRemaining);
            Assert.Equal("consult", read.PendingOperation);
        }
        Export("write-failure-" + phase);
    }

    [Fact]
    public void ReceiptIsPreservedWhenResourceConfirmationFailsAndNothingIsRedispatched()
    {
        var path = PathFor("resources");
        using var journal = new FileRunResourceJournal(path);
        using var executions = new FileExecutionJournal(PathFor("executions"));
        var feed = new RunResourceAccountingTests.Source(navigation: true);
        var faulty = new Fault(journal, row => row.Phase == RunResourcePhase.Confirm && row.Operation == "dispatch");
        var (kernel, driver) = Compose(faulty, new InMemoryConsultationJournal(), ctx =>
            new AgentDecision.Act(new(ctx.DecisionId, [new("ui.element", "Open child", "tap", null)], "synthetic navigation")),
            effects: feed, feed: feed.Next, executions: executions);
        var result = driver.Drive();
        Assert.Contains("resource-accounting-failed", result.Reason);
        Assert.Equal(1, feed.Deliveries); Assert.Single(kernel.EffectReceipts);
        Assert.Equal(1, executions.AttemptCount); Assert.Empty(executions.DiscoverPending());
        driver.Drive(); Assert.Equal(1, feed.Deliveries);
        var read = FileRunResourceJournal.Inspect(path);
        Assert.False(read.AccountingComplete); Assert.Equal("dispatch", read.PendingOperation);
        Assert.Null(read.StepsRemaining);
        Export("receipt-confirm-failed");
    }

    [Fact]
    public void UnknownDeliveryRetainsConfirmedStepConsumptionAndBlocksBalanceUse()
    {
        var path = PathFor("resources");
        using var journal = new FileRunResourceJournal(path);
        using var executions = new FileExecutionJournal(PathFor("executions"));
        var feed = new RunResourceAccountingTests.Source(navigation: true, DispatchOutcome.UnknownOutcome);
        var (kernel, driver) = Compose(journal, new InMemoryConsultationJournal(), ctx =>
            new AgentDecision.Act(new(ctx.DecisionId, [new("ui.element", "Open child", "tap", null)], "synthetic unknown delivery")),
            effects: feed, feed: feed.Next, executions: executions);
        Assert.Equal(RunDriveStatus.UnconfirmedDelivery, driver.Drive().Status);
        Assert.Equal(1, feed.Deliveries); Assert.Single(kernel.EffectReceipts);
        Assert.Single(executions.DiscoverPending());
        var read = FileRunResourceJournal.Inspect(path);
        Assert.Equal(1, read.Confirmed!.StepsConsumed);
        Assert.False(read.AccountingComplete); Assert.Null(read.StepsRemaining);
        Export("unknown-delivery");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorruptedOrTornHistoryIsNeverReadAsInitialBudget(bool torn)
    {
        var path = PathFor("resources");
        using (var journal = new FileRunResourceJournal(path))
        {
            var (_, driver) = Compose(journal, new InMemoryConsultationJournal(), ctx => new AgentDecision.Blocked(ctx.DecisionId, "stop"));
            driver.Drive();
        }
        var bytes = File.ReadAllBytes(path);
        if (torn) File.WriteAllBytes(path, bytes[..^7]);
        else { bytes[bytes.Length / 2] ^= 1; File.WriteAllBytes(path, bytes); }
        var modified = File.ReadAllBytes(path);
        Assert.False(FileRunResourceJournal.Inspect(path).AccountingComplete);
        Assert.Null(FileRunResourceJournal.Inspect(path).RoundsRemaining);
        Assert.Equal(modified, File.ReadAllBytes(path));
        Export("corrupted-" + torn);
    }

    [Fact]
    public void ExistingEmptyOrMissingHistoryIsNotInitializedAsZeroAndWriterIsExclusive()
    {
        Assert.False(FileRunResourceJournal.Inspect(PathFor("missing")).AccountingComplete);
        using var writer = new FileRunResourceJournal(PathFor("owned"));
        Assert.Throws<IOException>(() => new FileRunResourceJournal(PathFor("owned")));
        var path = PathFor("empty"); File.WriteAllBytes(path, []);
        using var journal = new FileRunResourceJournal(path, readOnly: true);
        Assert.Throws<IOException>(() => new FileRunResourceJournal(path));
        var calls = 0;
        var (_, driver) = Compose(journal, new InMemoryConsultationJournal(), ctx =>
        { calls++; return new AgentDecision.Blocked(ctx.DecisionId, "must not consult"); });
        Assert.Contains("resource-history-uninitialized-or-legacy", driver.Drive().Reason);
        Assert.Equal(0, calls); Assert.Empty(File.ReadAllBytes(path));
    }

    [Fact]
    public void WaitingForVerificationKeepsOneUnconfirmedOperationUntilEvidenceArrives()
    {
        var path = PathFor("resources");
        using var journal = new FileRunResourceJournal(path);
        var feed = new RunResourceAccountingTests.Source(navigation: true);
        var waiting = true;
        var calls = 0;
        var (kernel, driver) = Compose(journal, new InMemoryConsultationJournal(), ctx => ++calls == 1
            ? new AgentDecision.Act(new(ctx.DecisionId, [new("ui.element", "Open child", "tap", null)], "navigate"))
            : new AgentDecision.Blocked(ctx.DecisionId, "stop"), effects: feed,
            feed: directive => directive.Context == ObservationContext.PostActionEffectFlow && waiting
                ? null : feed.Next(directive));
        Assert.Equal(RunDriveStatus.WaitingForInput, driver.Drive().Status);
        var rows = journal.ReadRecords();
        Assert.Equal(RunResourcePhase.Begin, rows[^1].Phase);
        Assert.Equal("verify", rows[^1].Operation);
        Assert.Equal(RunDriveStatus.WaitingForInput, driver.Drive().Status);
        Assert.Equal(rows.Count, journal.ReadRecords().Count);
        waiting = false;
        driver.Drive();
        Assert.Single(kernel.EffectReceipts); Assert.Equal(1, feed.Deliveries);
        Assert.Single(journal.ReadRecords().Where(row => row.Phase == RunResourcePhase.Begin && row.Operation == "verify"));
        Assert.False(FileRunResourceJournal.Inspect(path).Confirmed!.UnresolvedEffect);
        Export("waiting-verification");
    }

    [Fact]
    public void ResourceFileAloneCannotCertifyOriginalOwnerSources()
    {
        var path = PathFor("resources");
        using var journal = new FileRunResourceJournal(path);
        var (_, driver) = Compose(journal, new InMemoryConsultationJournal(), ctx => new AgentDecision.Blocked(ctx.DecisionId, "stop"));
        driver.Drive();
        var read = FileRunResourceJournal.Inspect(path);
        Assert.False(read.AccountingComplete);
        Assert.Null(read.RoundsRemaining);
        Assert.Equal(1, read.Confirmed!.RoundsConsumed);
    }

    [Theory]
    [InlineData("missing-p25")]
    [InlineData("missing-execution")]
    [InlineData("corrupt-p25")]
    [InlineData("corrupt-execution")]
    [InlineData("p25-suffix")]
    [InlineData("execution-suffix")]
    [InlineData("older-resource-prefix")]
    [InlineData("wrong-run")]
    [InlineData("wrong-view")]
    public void SourceOrBindingMismatchNeverCertifiesAnOlderBalance(string fault)
    {
        var path = PathFor("resources");
        var p25Path = PathFor("p25"); var executionPath = PathFor("executions");
        RunResourceBinding binding;
        using (var p25 = new FileConsultationJournal(p25Path))
        using (var executions = new FileExecutionJournal(executionPath))
        using (var journal = new FileRunResourceJournal(path, consultationPath: p25Path, executionPath: executionPath))
        {
            var (kernel, driver) = Compose(journal, p25, ctx => new AgentDecision.Blocked(ctx.DecisionId, "stop"), executions: executions);
            driver.Drive(); binding = driver.ResourceBinding!;
            Assert.Equal(kernel.RunId, journal.ReadRecords()[0].Identity!.RunId);
            Assert.True(FileRunResourceJournal.Inspect(path, binding, p25Path, executionPath).AccountingComplete);
            if (fault == "p25-suffix")
            {
                var key = new ConsultationKey(kernel.RunId, "resource-contract", "extra-boundary", "extra-call");
                p25.Reserve(key, "extra-fingerprint"); p25.Reject(key, "extra-fingerprint", "no-response");
            }
            if (fault == "execution-suffix")
                Assert.Equal(ExecutionCommitOutcome.Success, executions.CommitPrepare(new(null, "effect:extra", "intent:extra",
                    "binding:extra", "tap", "row:extra", null, "revision:extra", 1, "synthetic", "source suffix")).Outcome);
        }
        if (fault == "missing-p25") File.Delete(p25Path);
        if (fault == "missing-execution") File.Delete(executionPath);
        if (fault == "corrupt-p25") File.AppendAllText(p25Path, "invalid-source-tail\n");
        if (fault == "corrupt-execution") File.AppendAllText(executionPath, "invalid-source-tail");
        if (fault == "older-resource-prefix")
        {
            // Preserve the original v3 prefix-only reader contract. New v4 latest reads separately require its exact head.
            LegacyResourceFixture.ConvertToV3(path);
            File.WriteAllText(path, File.ReadLines(path).First() + "\n");
        }
        if (fault.StartsWith("wrong-"))
        {
            using var otherJournal = new FileRunResourceJournal(PathFor("other-resources"));
            var (_, other) = Compose(otherJournal, new InMemoryConsultationJournal(), ctx => new AgentDecision.Blocked(ctx.DecisionId, "other owner"),
                contractRef: fault == "wrong-view" ? "other-view" : "resource-contract",
                taskInstanceId: fault == "wrong-run" ? "other-task" : "resource-task");
            other.Drive(); binding = other.ResourceBinding!;
        }
        var before = File.ReadAllBytes(path);
        var read = FileRunResourceJournal.Inspect(path, binding, p25Path, executionPath);
        Assert.False(read.AccountingComplete); Assert.Null(read.RoundsRemaining);
        Assert.Equal(fault.StartsWith("wrong-") ? "resource-identity-mismatch" : "resource-sources-unverified", read.Reason);
        Assert.Equal(before, File.ReadAllBytes(path));
        Export(fault);
    }

    [Fact]
    public void OnlyOriginalDriverSuppliesTheBindingAndFreshReaderCannotMintOne()
    {
        var path = PathFor("resources");
        using var executions = new FileExecutionJournal(PathFor("executions"));
        using var p25 = new FileConsultationJournal(PathFor("p25"));
        using var journal = new FileRunResourceJournal(path, consultationPath: PathFor("p25"), executionPath: PathFor("executions"));
        var (_, driver) = Compose(journal, p25, ctx => new AgentDecision.Blocked(ctx.DecisionId, "stop"), executions: executions);
        Assert.Null(driver.ResourceBinding);
        driver.Drive();
        Assert.NotNull(driver.ResourceBinding);
        Assert.Throws<NotSupportedException>(() => System.Text.Json.JsonSerializer.Deserialize<RunResourceBinding>("{}"));
        Assert.True(FileRunResourceJournal.Inspect(path, driver.ResourceBinding, PathFor("p25"), PathFor("executions")).AccountingComplete);
        using var reader = new FileRunResourceJournal(path, readOnly: true);
        var calls = 0;
        var (_, fresh) = Compose(reader, new InMemoryConsultationJournal(), ctx =>
        { calls++; return new AgentDecision.Blocked(ctx.DecisionId, "must not consult"); });
        fresh.Drive();
        Assert.Null(fresh.ResourceBinding); Assert.Equal(0, calls);
        Assert.False(FileRunResourceJournal.Inspect(path, fresh.ResourceBinding, PathFor("p25"), PathFor("executions")).AccountingComplete);
    }

    private void Export(string label)
    {
        var root = Environment.GetEnvironmentVariable("RESOURCE_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(root)) return;
        var directory = Path.Combine(root, label); Directory.CreateDirectory(directory);
        foreach (var file in Directory.GetFiles(_root))
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var output = File.Create(Path.Combine(directory, Path.GetFileName(file)));
            input.CopyTo(output);
        }
    }
}
