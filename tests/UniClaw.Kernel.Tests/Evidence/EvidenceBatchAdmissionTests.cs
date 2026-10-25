using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Evidence;

public sealed class EvidenceBatchAdmissionTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
    private static ObservationProposal Proposal(string value) => new(new("live.frame", value), IngressKind.Observation,
        ObservationContext.External, new("batch-test", Time, "batch", ["original"]));

    private sealed class HeldJournal(bool fail = false, bool afterCommit = false) : IAsyncEvidenceJournal
    {
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<EvidenceHistoryRecord> Original { get; } = [];
        public int BatchCalls { get; private set; }
        public int SyncCalls { get; private set; }
        public IReadOnlyList<EvidenceHistoryRecord> ReadRecords() => Original.ToArray();
        public void Append(EvidenceHistoryRecord record) { SyncCalls++; Original.Add(record); }
        public async ValueTask AppendBatchAsync(IReadOnlyList<EvidenceHistoryRecord> records, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            await Release.Task;
            if (!fail || afterCommit) Original.AddRange(records);
            if (fail) throw new IOException("original batch acknowledgement lost");
        }
    }

    [Fact]
    public async Task AdmissionsStayUnpublishedUntilTheWholeOriginalBatchIsAcknowledged()
    {
        var source = new HeldJournal(); var ledger = new EvidenceLedger(source); var original = Proposal("one");
        var pending = ledger.AdmitBatchAsync([original, original, Proposal("")]).AsTask();
        Assert.False(pending.IsCompleted); Assert.Empty(ledger.AdmissionLog); Assert.Empty(ledger.CanonicalRecords);
        Assert.Throws<InvalidOperationException>(() => ledger.Admit(Proposal("overlap")));
        source.Release.SetResult(); var result = await pending;
        Assert.Equal(new[] { AdmissionDecision.Accepted, AdmissionDecision.Accepted, AdmissionDecision.Rejected }, result.Select(r => r.Admission.Decision));
        Assert.Same(result[0].Record, result[1].Record); Assert.Single(ledger.CanonicalRecords);
        Assert.Equal(new[] { 1, 2, 3 }, source.Original.Select(r => r.Sequence)); Assert.Equal(1, source.BatchCalls);
        Assert.Equal(3, ledger.HistoryPosition!.Sequence);
        Assert.Equal(0, source.SyncCalls);
    }

    [Fact]
    public async Task SameBatchDerivedLineageAndDuplicateAreEquivalentToOrderedSingleAdmission()
    {
        var original = Proposal("root"); var rootId = new EvidenceLedger().Admit(original).Record!.EvidenceId;
        var parents = new List<string> { rootId }; var lineage = new List<string> { "derived" };
        var child = Proposal("child") with { DerivedFromEvidenceIds = parents, TransitiveEvidenceBasis = new HashSet<string> { rootId },
            FusionRule = "rule", FusionVersion = "1", Provenance = original.Provenance! with { TransformationLineage = lineage } };
        var proposals = new List<ObservationProposal> { original, child, child, Proposal("") };
        var reference = new EvidenceLedger(); var expected = proposals.Select(p => reference.Admit(p)).ToArray();
        var source = new HeldJournal(); var ledger = new EvidenceLedger(source); var pending = ledger.AdmitBatchAsync(proposals).AsTask();
        proposals.Clear(); parents.Clear(); lineage.Clear(); source.Release.SetResult(); var actual = await pending;
        Assert.Equal(expected.Select(r => r.Admission.Decision), actual.Select(r => r.Admission.Decision));
        Assert.Equal(expected.Select(r => r.Record?.EvidenceId), actual.Select(r => r.Record?.EvidenceId));
        Assert.Same(actual[1].Record, actual[2].Record); Assert.Equal(new[] { rootId }, actual[1].Record!.DerivedFromEvidenceIds);
        Assert.Equal(new[] { "derived" }, actual[1].Record!.Provenance.TransformationLineage);
        var restored = EvidenceLedger.RestoreReadOnly(source, source.RecordingId);
        Assert.Equal(2, restored.CanonicalRecords.Count); Assert.Equal(4, restored.AdmissionLog.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedBatchNeverPublishesAndFurtherAdmissionCannotRetry(bool afterCommit)
    {
        var source = new HeldJournal(true, afterCommit); var ledger = new EvidenceLedger(source);
        var pending = ledger.AdmitBatchAsync([Proposal("one"), Proposal("two")]).AsTask();
        source.Release.SetResult(); await Assert.ThrowsAnyAsync<IOException>(() => pending);
        Assert.Empty(ledger.AdmissionLog); Assert.Empty(ledger.CanonicalRecords);
        Assert.ThrowsAny<IOException>(() => ledger.Admit(Proposal("retry")));
        await Assert.ThrowsAnyAsync<IOException>(() => ledger.AdmitBatchAsync([Proposal("retry")]).AsTask());
        Assert.Equal(1, source.BatchCalls); Assert.Equal(afterCommit ? 2 : 0, source.Original.Count);
        if (afterCommit) Assert.Equal(2, EvidenceLedger.RestoreReadOnly(source, source.RecordingId).CanonicalRecords.Count);
    }

    [Fact]
    public async Task CancelledBeforeAdmissionNeverQueuesOrLatchesAStorageFault()
    {
        var source = new HeldJournal(); var ledger = new EvidenceLedger(source); using var cancellation = new CancellationTokenSource();
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ledger.AdmitBatchAsync([Proposal("one")], cancellation.Token).AsTask());
        Assert.Equal(0, source.BatchCalls); source.Release.SetResult(); await ledger.AdmitBatchAsync([Proposal("two")]);
        Assert.Single(ledger.CanonicalRecords);
    }

    [Fact]
    public async Task LaterBatchCanResolveAParentFromAlreadyAcknowledgedHistory()
    {
        var source = new HeldJournal(); source.Release.SetResult(); var ledger = new EvidenceLedger(source);
        var first = await ledger.AdmitBatchAsync([Proposal("root")]); var root = first[0].Record!.EvidenceId;
        var child = Proposal("child") with { DerivedFromEvidenceIds = [root], TransitiveEvidenceBasis = new HashSet<string> { root },
            FusionRule = "later-batch", FusionVersion = "1" };
        var result = await ledger.AdmitBatchAsync([child, child]);
        Assert.All(result, r => Assert.Equal(AdmissionDecision.Accepted, r.Admission.Decision));
        Assert.Same(result[0].Record, result[1].Record); Assert.Equal(2, source.BatchCalls);
        var original = EvidenceLedger.RestoreReadOnly(source, source.RecordingId);
        Assert.Equal(2, original.CanonicalRecords.Count); Assert.Equal(3, original.AdmissionLog.Count);
    }

    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "test"); }
    private sealed class NoEffect : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no effect expected"); }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DriverYieldsBeforeWorldAndAgentWithoutReenteringOrRepulling(bool cancel, bool fail)
    {
        var source = new HeldJournal(fail, true); var ledger = new EvidenceLedger(source); var policy = new AgentPlanPolicy();
        var world = new WorldModel(new HashSet<string> { "live.frame" });
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoEffect()));
        var pulls = 0; var consultations = 0;
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            NextInput = _ => { pulls++; return new RunDriverInput.Observation([Proposal("one"), Proposal("two")]); },
            ConsultAgent = context => { consultations++; return new AgentDecision.Blocked(context.DecisionId, "done"); },
        });
        Assert.True(kernel.AdmitContract(new("batch-v1", "batch observation", new HashSet<string> { "live.frame" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"], TaskInstanceId: "batch", ProductSessionId: "session", ContractRef: "contract")).Accepted);
        Assert.True(driver.Activate().Accepted); using var cancellation = new CancellationTokenSource();
        var pending = driver.DriveAsync(cancellation.Token);
        Assert.False(pending.IsCompleted); Assert.Null(kernel.CurrentBelief); Assert.Empty(ledger.AdmissionLog);
        Assert.Equal("drive-in-progress", driver.Drive().Reason); Assert.Equal("drive-in-progress", (await driver.DriveAsync()).Reason);
        Assert.Equal(0, consultations); Assert.Equal(1, pulls); Assert.Empty(kernel.EffectReceipts);
        if (cancel) cancellation.Cancel(); source.Release.SetResult(); var result = await pending;
        Assert.Equal(fail || cancel ? 0 : 1, consultations); Assert.Equal(1, pulls); Assert.Equal(0, source.SyncCalls);
        if (fail)
        {
            Assert.Null(kernel.CurrentBelief); Assert.Contains("evidence-persistence-failed", result.Reason);
            Assert.Equal(result.Reason, (await driver.DriveAsync()).Reason); Assert.Equal(1, source.BatchCalls);
        }
        else
        {
            Assert.NotNull(kernel.CurrentBelief); Assert.Equal(2, ledger.AdmissionLog.Count);
            if (cancel) Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, result.Status);
        }
    }
}
