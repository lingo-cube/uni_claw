using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;

if (args.Length != 3 || args[0] is not ("legacy" or "versioned") || !int.TryParse(args[2], out var count) || count < 1)
    throw new ArgumentException("usage: ResourceStorageProbe <legacy|versioned> <outputDirectory> <count>");
var mode = args[0]; var directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
var journalPath = Path.Combine(directory, "resource.journal.jsonl");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var identity = new RunResourceIdentity("probe-run", "probe-task", "probe-session", "probe-view", "v1", 1,
    "probe-policy", count + 1, count);
var receipts = new List<string>(); var renewals = new List<BudgetRenewalRecord>();
var reviews = new List<BudgetReviewHistoryEntry>(); var used = new List<string>();
var funded = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
var transcript = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
RunResourceSnapshot Snapshot(int rounds) => new(0, rounds, 0, 0, receipts.ToArray(), renewals.ToArray(),
    reviews.ToArray(), used.ToArray(), new Dictionary<string, IReadOnlyList<string>>(funded, StringComparer.Ordinal),
    false, false, false);
void AddInput(RunResourceRecord row) => transcript.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row, json) + "\n"));
var writerTimer = Stopwatch.StartNew(); var writerAllocatedBefore = GC.GetTotalAllocatedBytes(true); var gc0 = GC.CollectionCount(0);
var gc1 = GC.CollectionCount(1); var gc2 = GC.CollectionCount(2); HistoryWriteCounts writes; long writerRetained; long writerAllocated;
await using (var journal = new FileRunResourceJournal(journalPath))
{
    var seq = 1;
    var initial = new RunResourceRecord(seq, RunResourcePhase.Initialize, "initialize", identity, Snapshot(0));
    AddInput(initial); await journal.AppendAsync(initial);
    for (var i = 1; i <= count; i++)
    {
        var suffix = i.ToString("D5"); var key = new ConsultationKey(identity.RunId, identity.ContractViewRef, "boundary-" + suffix, "consult-" + suffix);
        var begin = new RunResourceRecord(++seq, RunResourcePhase.Begin, "consult", Consultation: key,
            RequestFingerprint: "request-fingerprint-" + suffix);
        AddInput(begin); await journal.AppendAsync(begin);
        var reason = "denied: insufficient verified progress for renewal " + suffix + " [policy review remains incomplete]";
        var reviewId = "review-" + suffix;
        renewals.Add(new("decision-" + suffix, 0, 0, "retain budget pending confirmed progress " + suffix,
            false, reason, reviewId, false));
        reviews.Add(new(reviewId, null, "show verified progress " + suffix, "no qualifying progress " + suffix,
            0, i, 0, 0, false, "continue within original allowance " + suffix,
            ["evidence-fact-" + suffix], "projection-v1", ["progress-fact-" + suffix], "item-" + suffix));
        used.Add("progress-fact-" + suffix);
        funded["funded-item-" + suffix] = ["funded-progress-" + suffix, "funding-reason-" + suffix];
        var confirm = begin with { Sequence = ++seq, Phase = RunResourcePhase.Confirm, Snapshot = Snapshot(i) };
        AddInput(confirm); await journal.AppendAsync(confirm);
    }
    writes = journal.WriteStatistics;
    writerTimer.Stop(); writerAllocated = GC.GetTotalAllocatedBytes(false) - writerAllocatedBefore;
    writerRetained = GC.GetTotalMemory(true); GC.KeepAlive(journal);
}
var inputHash = Convert.ToHexString(transcript.GetHashAndReset());
int latestRounds; int latestRenewals; int latestReviews; int latestFacts; int latestFunded; int readFrames; int readSnapshots; long readBytes;
var queryTimer = Stopwatch.StartNew(); var queryAllocatedBefore = GC.GetTotalAllocatedBytes(true);
await using (var reader = new FileRunResourceJournal(journalPath, readOnly: true))
{
    if (mode == "legacy")
    {
        var latest = reader.ReadRecords().Last(); var snapshot = latest.Snapshot!;
        latestRounds = snapshot.RoundsConsumed; latestRenewals = snapshot.Renewals.Count;
        latestReviews = snapshot.Reviews.Count; latestFacts = snapshot.UsedProgressFacts.Count; latestFunded = snapshot.FundedItems.Count;
        readFrames = -1; readSnapshots = -1; readBytes = new FileInfo(journalPath).Length;
    }
    else
    {
        var latest = reader.ReadLatestVersion(); var snapshot = latest.Confirmed!;
        latestRounds = snapshot.RoundsConsumed; latestRenewals = snapshot.Renewals.Count;
        latestReviews = snapshot.Reviews.Count; latestFacts = snapshot.UsedProgressFacts.Count; latestFunded = snapshot.FundedItems.Count;
        readFrames = latest.ReadCost.FramesRead; readSnapshots = latest.ReadCost.SnapshotReads; readBytes = latest.ReadCost.BytesRead;
    }
}
queryTimer.Stop(); var queryAllocated = GC.GetTotalAllocatedBytes(false) - queryAllocatedBefore;
var logBytes = new FileInfo(journalPath).Length;
var snapshotDir = journalPath + ".snapshots";
var snapshotFiles = Directory.Exists(snapshotDir) ? Directory.EnumerateFiles(snapshotDir).ToArray() : [];
var snapshotCount = snapshotFiles.Length; var snapshotBytes = snapshotFiles.Sum(p => new FileInfo(p).Length);
var headBytes = File.Exists(journalPath + ".head") ? new FileInfo(journalPath + ".head").Length : 0L;
var result = new { mode, count, inputRecordSha256 = inputHash, latest = new { roundsConsumed = latestRounds,
    deniedRenewals = latestRenewals, reviews = latestReviews, usedProgressFacts = latestFacts, fundedItems = latestFunded },
    storedBytes = new { log = logBytes, snapshots = snapshotBytes, snapshotFiles = snapshotCount, head = headBytes, total = logBytes + snapshotBytes + headBytes },
    writes = new { writes.FrameWrites, writes.FrameFlushes, writes.HeadWrites, writes.HeadFlushes,
        writes.FrameBytesSubmitted, writes.CommittedBatches }, latestRead = new { frames = readFrames,
        snapshots = readSnapshots, bytes = readBytes, elapsedMilliseconds = queryTimer.Elapsed.TotalMilliseconds,
        allocatedManagedBytes = queryAllocated }, writer = new { elapsedMilliseconds = writerTimer.Elapsed.TotalMilliseconds,
        allocatedManagedBytes = writerAllocated, retainedManagedBytesAfterFullGcWhileJournalAlive = writerRetained },
    gcCollections = new { gen0 = GC.CollectionCount(0) - gc0, gen1 = GC.CollectionCount(1) - gc1,
        gen2 = GC.CollectionCount(2) - gc2 }, sourcePrefixHashing = "disabled; isolated from resource format; not measured",
    metricLimits = "WriteStatistics counts managed frame/head calls only and excludes added snapshot-file writes/flushes; retained is post-full-GC while writer remains alive, not peak; no OS syscall or physical durability claim" };
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
