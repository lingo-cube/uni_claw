using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunResourceVersionTests
{
    [Fact]
    public async Task PublicJournalReadsLatestAndReverseResourceHistoryAfterReopen()
    {
        var root = Directory.CreateTempSubdirectory("resource-version-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var identity = new RunResourceIdentity("run-1", "task-1", "session-1", "contract-1",
            "v1", 1, "policy-1", 4, 3);
        var initial = Snapshot(0, []);
        var confirmed = Snapshot(1, ["r1"]);
        string recordingId;

        try
        {
            await using (var journal = new FileRunResourceJournal(path))
            {
                recordingId = journal.RecordingId!;
                await journal.AppendAsync(new(1, RunResourcePhase.Initialize, "initialize", identity, initial));
                await journal.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-1"));
                await journal.AppendAsync(new(3, RunResourcePhase.Confirm, "dispatch", Snapshot: confirmed,
                    IntentId: "intent-1"));
            }

            await using var reopened = new FileRunResourceJournal(path, readOnly: true);
            var latest = reopened.ReadLatestVersion();
            Assert.Equal(3, latest.Sequence);
            Assert.Equal(recordingId, latest.RecordingId);
            Assert.Equal(identity, latest.Identity);
            AssertSnapshot(latest.Confirmed!, 1, ["r1"]);

            var original = reopened.ReadVersion(1, ResourceHistoryDirection.Reverse);
            Assert.Equal(1, original.Sequence);
            Assert.Equal(recordingId, original.RecordingId);
            Assert.Equal(identity, original.Identity);
            AssertSnapshot(original.Confirmed!, 0, []);

            var begun = reopened.ReadVersion(2);
            Assert.Equal(2, begun.Sequence);
            Assert.Equal(recordingId, begun.RecordingId);
            Assert.Equal(identity, begun.Identity);
            Assert.Null(begun.Record.Snapshot);
            AssertSnapshot(begun.Confirmed!, 0, []);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PublicJournalReadsLiteralConfirmedFactsAcrossForwardAndReverseHistory()
    {
        var root = Directory.CreateTempSubdirectory("resource-version-history-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var identity = new RunResourceIdentity("run-history", "task-history", "session-history", "contract-history",
            "v1", 1, "policy-history", 8, 8);
        var recordingId = string.Empty;
        var first = new RunResourceSnapshot(0, 1, 0, 0, [],
            [new("renewal-1", 0, 0, null, false, null, null, false)],
            [new("review-1", null, null, "actual-1", 0, 1, 0, 0, false, null, null, "revision-1", [], null)],
            ["alpha", "beta", "gamma"],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["keep"] = ["old", "shared"],
                ["remove"] = ["retired"],
            }, false, false, false);
        var second = new RunResourceSnapshot(0, 2, 0, 0, [],
            [new("renewal-2", 0, 0, "basis-2", false, null, "review-2", false)],
            [new("review-2", null, null, null, 0, 2, 0, 0, false, "continue", null, null, null, null)],
            ["gamma", "alpha"],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["remove"] = ["retired"],
                ["new"] = ["first", "second"],
                ["keep"] = ["shared"],
            }, false, false, false);
        var third = new RunResourceSnapshot(0, 3, 0, 0, [],
            [new("renewal-3", 0, 0, null, false, "declined", null, false)],
            [new("review-3", null, "expectation-3", null, 0, 3, 0, 0, false, null, ["evidence-3"], "projection-3", ["fact-3"], "item-3")],
            [], new Dictionary<string, IReadOnlyList<string>>(), false, false, false);

        try
        {
            await using (var journal = new FileRunResourceJournal(path, versionOptions: new ResourceVersionOptions
            {
                MaxTailRecords = 4,
            }))
            {
                recordingId = journal.RecordingId!;
                await journal.AppendAsync(new(1, RunResourcePhase.Initialize, "initialize", identity, Snapshot(0, [])));
                await AppendConsultationAsync(journal, 2, 3, "consultation-1", first);
                await AppendConsultationAsync(journal, 4, 5, "consultation-2", second);
                await AppendConsultationAsync(journal, 6, 7, "consultation-3", third);
            }

            await using var reopened = new FileRunResourceJournal(path, readOnly: true);
            AssertHistoryVersion(reopened.ReadVersion(3, ResourceHistoryDirection.Forward), recordingId, identity,
                3, "consultation-1", first, ["alpha", "beta", "gamma"], ["keep", "remove"]);
            AssertHistoryVersion(reopened.ReadVersion(3, ResourceHistoryDirection.Reverse), recordingId, identity,
                3, "consultation-1", first, ["alpha", "beta", "gamma"], ["keep", "remove"]);
            AssertHistoryVersion(reopened.ReadVersion(5, ResourceHistoryDirection.Forward), recordingId, identity,
                5, "consultation-2", second, ["gamma", "alpha"], ["remove", "new", "keep"]);
            AssertHistoryVersion(reopened.ReadVersion(5, ResourceHistoryDirection.Reverse), recordingId, identity,
                5, "consultation-2", second, ["gamma", "alpha"], ["remove", "new", "keep"]);
            AssertHistoryVersion(reopened.ReadVersion(7, ResourceHistoryDirection.Forward), recordingId, identity,
                7, "consultation-3", third, [], []);
            AssertHistoryVersion(reopened.ReadVersion(7, ResourceHistoryDirection.Reverse), recordingId, identity,
                7, "consultation-3", third, [], []);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LatestPendingBeginRetainsConfirmedFactsAndUnknownVersionsAreRejected()
    {
        var root = Directory.CreateTempSubdirectory("resource-version-pending-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var identity = new RunResourceIdentity("run-pending", "task-pending", "session-pending", "contract-pending",
            "v1", 1, "policy-pending", 4, 3);
        var confirmed = Snapshot(1, ["receipt-pending"]);
        string recordingId;

        try
        {
            await using (var journal = new FileRunResourceJournal(path))
            {
                recordingId = journal.RecordingId!;
                await journal.AppendAsync(new(1, RunResourcePhase.Initialize, "initialize", identity, Snapshot(0, [])));
                await journal.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-confirmed"));
                await journal.AppendAsync(new(3, RunResourcePhase.Confirm, "dispatch", Snapshot: confirmed,
                    IntentId: "intent-confirmed"));
                await journal.AppendAsync(new(4, RunResourcePhase.Begin, "dispatch", IntentId: "intent-pending"));
            }

            await using var reopened = new FileRunResourceJournal(path, readOnly: true);
            var latest = reopened.ReadLatestVersion();
            Assert.Equal(4, latest.Sequence);
            Assert.Equal(recordingId, latest.RecordingId);
            Assert.Equal(identity, latest.Identity);
            Assert.Equal(RunResourcePhase.Begin, latest.Record.Phase);
            Assert.Equal("dispatch", latest.Record.Operation);
            Assert.Equal("intent-pending", latest.Record.IntentId);
            Assert.Null(latest.Record.Snapshot);
            AssertSnapshot(latest.Confirmed!, 1, ["receipt-pending"]);

            Assert.Throws<ArgumentOutOfRangeException>(() => reopened.ReadVersion(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => reopened.ReadVersion(5, ResourceHistoryDirection.Reverse));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LatestReadCostStaysBoundedAfterTwoHundredAppendedRecordsAndReopenReadsOldState()
    {
        const int maxTailRecords = 8;
        const int operations = 100;
        var root = Directory.CreateTempSubdirectory("resource-version-growth-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var identity = new RunResourceIdentity("run-growth", "task-growth", "session-growth", "contract-growth",
            "v1", 1, "policy-growth", 128, 4);
        var options = new ResourceVersionOptions { MaxTailRecords = maxTailRecords };

        try
        {
            await using (var journal = new FileRunResourceJournal(path, versionOptions: options))
            {
                await journal.AppendAsync(new(1, RunResourcePhase.Initialize, "initialize", identity, Snapshot(0, [])));
                var receipts = new List<string>();
                for (var operation = 1; operation <= operations; operation++)
                {
                    var intentId = "intent-" + operation;
                    receipts.Add("receipt-" + operation);
                    await journal.AppendAsync(new(operation * 2, RunResourcePhase.Begin, "dispatch", IntentId: intentId));
                    await journal.AppendAsync(new(operation * 2 + 1, RunResourcePhase.Confirm, "dispatch",
                        Snapshot: Snapshot(operation, receipts.ToArray()), IntentId: intentId));
                }
            }

            await using var reopened = new FileRunResourceJournal(path, readOnly: true);
            var latest = reopened.ReadLatestVersion();
            Assert.Equal(operations * 2 + 1, latest.Sequence);
            Assert.Equal("run-growth", latest.Identity!.RunId);
            Assert.Equal(operations, latest.Confirmed!.StepsConsumed);
            Assert.Equal(operations, latest.Confirmed.ReceiptIds.Count);
            Assert.Equal("receipt-1", latest.Confirmed.ReceiptIds[0]);
            Assert.Equal("receipt-100", latest.Confirmed.ReceiptIds[^1]);
            Assert.True(latest.ReadCost.FramesRead <= maxTailRecords + 3,
                $"Expected at most {maxTailRecords + 3} frames; read {latest.ReadCost.FramesRead}.");
            Assert.Equal(1, latest.ReadCost.SnapshotReads);

            var old = reopened.ReadVersion(197, ResourceHistoryDirection.Reverse);
            Assert.Equal(197, old.Sequence);
            Assert.Equal("run-growth", old.Identity!.RunId);
            Assert.Equal(RunResourcePhase.Confirm, old.Record.Phase);
            Assert.Equal("dispatch", old.Record.Operation);
            Assert.Equal("intent-98", old.Record.IntentId);
            Assert.Equal(98, old.Confirmed!.StepsConsumed);
            Assert.Equal(98, old.Confirmed.ReceiptIds.Count);
            Assert.Equal("receipt-1", old.Confirmed.ReceiptIds[0]);
            Assert.Equal("receipt-98", old.Confirmed.ReceiptIds[^1]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async ValueTask AppendConsultationAsync(FileRunResourceJournal journal,
        int beginSequence, int confirmSequence, string correlationId, RunResourceSnapshot snapshot)
    {
        var key = new ConsultationKey("run-history", "contract-history", "boundary-" + correlationId, correlationId);
        var fingerprint = "fingerprint-" + correlationId;
        await journal.AppendAsync(new(beginSequence, RunResourcePhase.Begin, "consult",
            Consultation: key, RequestFingerprint: fingerprint));
        await journal.AppendAsync(new(confirmSequence, RunResourcePhase.Confirm, "consult", Snapshot: snapshot,
            Consultation: key, RequestFingerprint: fingerprint));
    }

    private static void AssertHistoryVersion(RunResourceVersion version, string recordingId,
        RunResourceIdentity identity, int sequence, string correlationId, RunResourceSnapshot expected,
        string[] progressFacts, string[] fundedItemKeys)
    {
        Assert.Equal(recordingId, version.RecordingId);
        Assert.Equal(sequence, version.Sequence);
        Assert.Equal(identity, version.Identity);
        Assert.Equal(sequence, version.Record.Sequence);
        Assert.Equal(RunResourcePhase.Confirm, version.Record.Phase);
        Assert.Equal("consult", version.Record.Operation);
        Assert.Equal(new ConsultationKey("run-history", "contract-history", "boundary-" + correlationId, correlationId),
            version.Record.Consultation);
        Assert.Equal("fingerprint-" + correlationId, version.Record.RequestFingerprint);
        var actual = version.Confirmed!;
        Assert.Equal(expected.StepsConsumed, actual.StepsConsumed);
        Assert.Equal(expected.RoundsConsumed, actual.RoundsConsumed);
        Assert.Equal(expected.GrantedSteps, actual.GrantedSteps);
        Assert.Equal(expected.GrantedRounds, actual.GrantedRounds);
        Assert.Equal(expected.ReceiptIds, actual.ReceiptIds);
        Assert.Equal(expected.UsedProgressFacts, actual.UsedProgressFacts);
        Assert.Equal(progressFacts, actual.UsedProgressFacts);
        Assert.Equal(fundedItemKeys, actual.FundedItems.Keys);
        foreach (var (key, values) in expected.FundedItems)
            Assert.Equal(values, actual.FundedItems[key]);
        Assert.Equal(expected.Renewals.Count, actual.Renewals.Count);
        for (var i = 0; i < expected.Renewals.Count; i++)
        {
            Assert.Equal(expected.Renewals[i].DecisionId, actual.Renewals[i].DecisionId);
            Assert.Equal(expected.Renewals[i].AdditionalSteps, actual.Renewals[i].AdditionalSteps);
            Assert.Equal(expected.Renewals[i].AdditionalRounds, actual.Renewals[i].AdditionalRounds);
            Assert.Equal(expected.Renewals[i].Justification, actual.Renewals[i].Justification);
            Assert.Equal(expected.Renewals[i].Granted, actual.Renewals[i].Granted);
            Assert.Equal(expected.Renewals[i].RejectionReason, actual.Renewals[i].RejectionReason);
            Assert.Equal(expected.Renewals[i].ReviewId, actual.Renewals[i].ReviewId);
            Assert.Equal(expected.Renewals[i].ReviewComplete, actual.Renewals[i].ReviewComplete);
        }
        Assert.Equal(expected.Reviews.Count, actual.Reviews.Count);
        for (var i = 0; i < expected.Reviews.Count; i++)
        {
            Assert.Equal(expected.Reviews[i].ReviewId, actual.Reviews[i].ReviewId);
            Assert.Equal(expected.Reviews[i].Outcome, actual.Reviews[i].Outcome);
            Assert.Equal(expected.Reviews[i].Expectation, actual.Reviews[i].Expectation);
            Assert.Equal(expected.Reviews[i].Actual, actual.Reviews[i].Actual);
            Assert.Equal(expected.Reviews[i].StepsConsumed, actual.Reviews[i].StepsConsumed);
            Assert.Equal(expected.Reviews[i].RoundsConsumed, actual.Reviews[i].RoundsConsumed);
            Assert.Equal(expected.Reviews[i].StepsGranted, actual.Reviews[i].StepsGranted);
            Assert.Equal(expected.Reviews[i].RoundsGranted, actual.Reviews[i].RoundsGranted);
            Assert.Equal(expected.Reviews[i].Complete, actual.Reviews[i].Complete);
            Assert.Equal(expected.Reviews[i].NextDirection, actual.Reviews[i].NextDirection);
            Assert.Equal(expected.Reviews[i].EvidenceRefs ?? [], actual.Reviews[i].EvidenceRefs ?? []);
            Assert.Equal(expected.Reviews[i].ProjectionRevision, actual.Reviews[i].ProjectionRevision);
            Assert.Equal(expected.Reviews[i].ProgressFactKeys ?? [], actual.Reviews[i].ProgressFactKeys ?? []);
            Assert.Equal(expected.Reviews[i].NextItemKey, actual.Reviews[i].NextItemKey);
        }
        Assert.False(actual.UnresolvedEffect);
        Assert.False(actual.PendingConsultation);
        Assert.False(actual.Terminal);
    }

    private static RunResourceSnapshot Snapshot(int stepsConsumed, IReadOnlyList<string> receiptIds) => new(
        stepsConsumed, 0, 0, 0, receiptIds, [], [], [], new Dictionary<string, IReadOnlyList<string>>(),
        UnresolvedEffect: false, PendingConsultation: false, Terminal: false);

    private static void AssertSnapshot(RunResourceSnapshot snapshot, int stepsConsumed, string[] receiptIds)
    {
        Assert.Equal(stepsConsumed, snapshot.StepsConsumed);
        Assert.Equal(0, snapshot.RoundsConsumed);
        Assert.Equal(0, snapshot.GrantedSteps);
        Assert.Equal(0, snapshot.GrantedRounds);
        Assert.Equal(receiptIds, snapshot.ReceiptIds);
        Assert.Empty(snapshot.Renewals);
        Assert.Empty(snapshot.Reviews);
        Assert.Empty(snapshot.UsedProgressFacts);
        Assert.Empty(snapshot.FundedItems);
        Assert.False(snapshot.UnresolvedEffect);
        Assert.False(snapshot.PendingConsultation);
        Assert.False(snapshot.Terminal);
    }
}
