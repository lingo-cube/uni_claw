using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunResourceVersionIntegrityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("delete")]
    [InlineData("modify")]
    public async Task LatestReadRejectsMissingOrChangedCurrentBaseline(string fault)
    {
        var root = Directory.CreateTempSubdirectory("resource-baseline-integrity-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        try
        {
            await WriteV4(path, "baseline-run");
            var baseline = CurrentBaselinePath(path);
            if (fault == "delete") File.Delete(baseline);
            else await File.WriteAllTextAsync(baseline, "{}");

            await using var journal = new FileRunResourceJournal(path, readOnly: true);
            if (fault == "delete")
                Assert.Throws<FileNotFoundException>(() => journal.ReadLatestVersion());
            else
                Assert.Throws<InvalidDataException>(() => journal.ReadLatestVersion());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ReadOnlyConstructionRejectsTornV4Tail()
    {
        var root = Directory.CreateTempSubdirectory("resource-torn-tail-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        try
        {
            await WriteV4(path, "torn-run");
            await File.AppendAllTextAsync(path, "{\"partial\":", new UTF8Encoding(false));
            Assert.Throws<InvalidDataException>(() => new FileRunResourceJournal(path, readOnly: true));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ReadOnlyConstructionRejectsRestoredEarlyHeadAfterNewFrameWasCommitted()
    {
        var root = Directory.CreateTempSubdirectory("resource-old-head-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var headPath = path + ".head";
        try
        {
            await using (var journal = new FileRunResourceJournal(path,
                versionOptions: new ResourceVersionOptions { MaxTailRecords = 1 }))
            {
                await journal.AppendAsync(Initialize("old-head-run"));
                var oldHead = await File.ReadAllBytesAsync(headPath);
                await journal.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-1"));
                await File.WriteAllBytesAsync(headPath, oldHead);
            }

            Assert.Throws<InvalidDataException>(() => new FileRunResourceJournal(path, readOnly: true));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task HeadPublicationFailureLocksWriterAndLeavesOnlyUncommittedTail()
    {
        var root = Directory.CreateTempSubdirectory("resource-head-publish-failure-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var headPath = path + ".head";
        var savedHeadPath = headPath + ".saved";
        try
        {
            await using (var journal = new FileRunResourceJournal(path))
            {
                await journal.AppendAsync(Initialize("publish-failure-run"));
                var committedPrefix = await File.ReadAllBytesAsync(path);
                File.Move(headPath, savedHeadPath);
                Directory.CreateDirectory(headPath);

                await Assert.ThrowsAnyAsync<IOException>(async () =>
                    await journal.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-unpublished")));
                Assert.True(new FileInfo(path).Length > committedPrefix.Length,
                    "The frame must have been written before head publication failed.");
                Assert.True((await File.ReadAllBytesAsync(path)).Take(committedPrefix.Length).SequenceEqual(committedPrefix),
                    "The previously committed log prefix must remain byte-for-byte intact.");
                await Assert.ThrowsAsync<IOException>(async () =>
                    await journal.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-after-failure")));
            }

            Directory.Delete(headPath);
            File.Move(savedHeadPath, headPath);
            Assert.Throws<InvalidDataException>(() => new FileRunResourceJournal(path, readOnly: true));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CancellationBeforeCallDoesNotAppendButCancellationAfterAcceptanceWaitsForCommit()
    {
        const int factCount = 12_000;
        var root = Directory.CreateTempSubdirectory("resource-cancel-settlement-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var facts = Enumerable.Range(0, factCount)
            .Select(i => new string('f', 800) + i.ToString("D5"))
            .ToArray();
        var identity = Identity("cancel-settlement-run") with { InitialSteps = 1 };
        var confirmed = new RunResourceSnapshot(1, 0, 0, 0, ["receipt-large"], [], [], facts,
            new Dictionary<string, IReadOnlyList<string>>(), false, false, false);
        var logicalValidation = RunResourceAccounting.Inspect([
            new(1, RunResourcePhase.Initialize, "initialize", identity, Snapshot()),
            new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-large"),
            new(3, RunResourcePhase.Confirm, "dispatch", Snapshot: confirmed, IntentId: "intent-large"),
        ]);
        Assert.Equal(1, logicalValidation.Confirmed!.StepsConsumed);
        Assert.Equal(factCount, logicalValidation.Confirmed.UsedProgressFacts.Count);
        try
        {
            await using (var journal = new FileRunResourceJournal(path))
            {
                await journal.AppendAsync(new(1, RunResourcePhase.Initialize, "initialize", identity, Snapshot()));
                await journal.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-large"));
                var beforeCanceledCall = new FileInfo(path).Length;
                using (var alreadyCanceled = new CancellationTokenSource())
                {
                    alreadyCanceled.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                        await journal.AppendAsync(new(3, RunResourcePhase.Confirm, "dispatch",
                            Snapshot: confirmed,
                            IntentId: "intent-large"), alreadyCanceled.Token));
                }
                Assert.Equal(beforeCanceledCall, new FileInfo(path).Length);

                using var acceptedCancellation = new CancellationTokenSource();
                var accepted = journal.AppendAsync(new(3, RunResourcePhase.Confirm, "dispatch",
                    Snapshot: confirmed,
                    IntentId: "intent-large"), acceptedCancellation.Token);
                Assert.False(accepted.IsCompleted, "The large accepted append should still be doing real I/O.");
                acceptedCancellation.Cancel();
                await accepted;
            }

            await using var reopened = new FileRunResourceJournal(path, readOnly: true);
            var latest = reopened.ReadLatestVersion();
            Assert.Equal(3, latest.Sequence);
            Assert.Equal(RunResourcePhase.Confirm, latest.Record.Phase);
            Assert.Equal(1, latest.Confirmed!.StepsConsumed);
            Assert.Equal("receipt-large", Assert.Single(latest.Confirmed.ReceiptIds));
            Assert.Equal(factCount, latest.Confirmed.UsedProgressFacts.Count);
            Assert.Equal(facts[0], latest.Confirmed.UsedProgressFacts[0]);
            Assert.Equal(facts[^1], latest.Confirmed.UsedProgressFacts[^1]);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task LatestReadRejectsBaselineCopiedFromDifferentRecording()
    {
        var root = Directory.CreateTempSubdirectory("resource-swapped-baseline-").FullName;
        var firstPath = Path.Combine(root, "first.jsonl");
        var secondPath = Path.Combine(root, "second.jsonl");
        try
        {
            await WriteV4(firstPath, "swap-run-a");
            await WriteV4(secondPath, "swap-run-b");
            var first = CurrentBaselinePath(firstPath);
            var second = CurrentBaselinePath(secondPath);
            Assert.NotEqual(HeadRecordingId(firstPath), HeadRecordingId(secondPath));
            var secondBytes = await File.ReadAllBytesAsync(second);
            await File.WriteAllBytesAsync(first, secondBytes);
            var head = JsonNode.Parse(await File.ReadAllTextAsync(firstPath + ".head"))!;
            var baselineEntry = head["baselines"]![0]!;
            baselineEntry["hash"] = FileHash(second);
            baselineEntry["bytes"] = secondBytes.Length;
            await File.WriteAllTextAsync(firstPath + ".head", head.ToJsonString(Json) + "\n");

            await using var journal = new FileRunResourceJournal(firstPath, readOnly: true);
            Assert.Throws<InvalidDataException>(() => journal.ReadLatestVersion());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task FullReadRejectsNonEmptyInitializationSourcePrefixEvenWithValidV4FrameChain()
    {
        var root = Directory.CreateTempSubdirectory("resource-init-source-prefix-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var consultationPath = Path.Combine(root, "consultations.jsonl");
        var executionPath = Path.Combine(root, "execution.jsonl");
        try
        {
            await File.WriteAllTextAsync(consultationPath, "", new UTF8Encoding(false));
            await File.WriteAllTextAsync(executionPath, "", new UTF8Encoding(false));
            await using (var writer = new FileRunResourceJournal(path, consultationPath: consultationPath,
                executionPath: executionPath))
            {
                await writer.AppendAsync(Initialize("init-source-run"));
                await writer.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-1"));
                await writer.AppendAsync(new(3, RunResourcePhase.Confirm, "dispatch",
                    Snapshot: new(1, 0, 0, 0, ["receipt-1"], [], [], [],
                        new Dictionary<string, IReadOnlyList<string>>(), false, false, false), IntentId: "intent-1"));
            }

            RehashV4ChainWithChangedInitialSourceLength(path);
            await using var reader = new FileRunResourceJournal(path, readOnly: true);
            Assert.Throws<InvalidDataException>(() => reader.ReadRecords());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task WritableReopenRejectsExistingRecordingWithoutChangingAnyFile()
    {
        var root = Directory.CreateTempSubdirectory("resource-writer-reopen-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        try
        {
            await using (var writer = new FileRunResourceJournal(path))
            {
                await writer.AppendAsync(Initialize("writer-reopen-run"));
                await writer.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-1"));
            }
            var before = CaptureFileHashes(root);

            Assert.Throws<IOException>(() => new FileRunResourceJournal(path));

            var after = CaptureFileHashes(root);
            Assert.Equal(before.Keys, after.Keys);
            foreach (var (name, hash) in before) Assert.Equal(hash, after[name]);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task LatestReadRejectsHeadBoundToCopiedDuplicateFinalFrame()
    {
        var root = Directory.CreateTempSubdirectory("resource-head-binding-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        var headPath = path + ".head";
        try
        {
            await using (var writer = new FileRunResourceJournal(path))
            {
                await writer.AppendAsync(Initialize("head-binding-run"));
                await writer.AppendAsync(new(2, RunResourcePhase.Begin, "dispatch", IntentId: "intent-1"));
                await writer.AppendAsync(new(3, RunResourcePhase.Confirm, "dispatch",
                    Snapshot: new(1, 0, 0, 0, ["receipt-1"], [], [], [],
                        new Dictionary<string, IReadOnlyList<string>>(), false, false, false), IntentId: "intent-1"));
            }

            var originalLog = await File.ReadAllBytesAsync(path);
            var copiedFrameOffset = originalLog.Length;
            var copyStart = Array.LastIndexOf(originalLog, (byte)'\n', originalLog.Length - 2) + 1;
            await using (var log = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                await log.WriteAsync(originalLog.AsMemory(copyStart));

            var head = JsonNode.Parse(await File.ReadAllTextAsync(headPath))!;
            head["length"] = new FileInfo(path).Length;
            head["lastOffset"] = copiedFrameOffset;
            await File.WriteAllTextAsync(headPath, JsonSerializer.Serialize(head, Json) + "\n", new UTF8Encoding(false));

            await using var reader = new FileRunResourceJournal(path, readOnly: true);
            Assert.Throws<InvalidDataException>(() => reader.ReadLatestVersion());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("{}\n")]
    [InlineData("[]\n")]
    [InlineData("{\"schema\":17}\n")]
    public async Task PublicInspectReturnsUnreadableForMalformedFirstFrame(string contents)
    {
        var root = Directory.CreateTempSubdirectory("resource-inspect-malformed-").FullName;
        var path = Path.Combine(root, "resources.jsonl");
        try
        {
            await File.WriteAllTextAsync(path, contents, new UTF8Encoding(false));
            var result = FileRunResourceJournal.Inspect(path);
            Assert.False(result.AccountingComplete);
            Assert.Null(result.Confirmed);
            Assert.StartsWith("resource-history-unreadable:", result.Reason);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("uniclaw.runtime.resource.v2")]
    [InlineData("uniclaw.runtime.resource.v3")]
    public async Task LegacyPhysicalFramesReadWithoutChangingFilesOrInventingRecording(string schema)
    {
        var root = Directory.CreateTempSubdirectory("resource-legacy-integrity-").FullName;
        var path = Path.Combine(root, "legacy.jsonl");
        var identity = Identity("legacy-run");
        var row = Initialize("legacy-run");
        var recordingId = schema.EndsWith("v3", StringComparison.Ordinal) ? Guid.NewGuid().ToString("N") : null;
        try
        {
            WriteLegacyFrame(path, schema, row, recordingId);
            if (recordingId is not null)
                await File.WriteAllTextAsync(path + ".head", JsonSerializer.Serialize(new
                {
                    schema = "uniclaw.runtime.resource-head.v1", recordingId, sequence = 1, hash = FrameHash(path)
                }, Json) + "\n");
            using (var frame = JsonDocument.Parse(File.ReadAllText(path)))
                Assert.Equal(JsonValueKind.Null, frame.RootElement.GetProperty("sources").ValueKind);
            var beforeHash = FileHash(path);
            var beforeHeadHash = File.Exists(path + ".head") ? FileHash(path + ".head") : null;

            await using var journal = new FileRunResourceJournal(path, readOnly: true);
            if (recordingId is null) Assert.Null(journal.RecordingId);
            else Assert.Equal(recordingId, journal.RecordingId);
            AssertRecordEqual(row, Assert.Single(journal.ReadRecords()));
            var version = journal.ReadVersion(1);
            Assert.Equal(recordingId ?? "", version.RecordingId);
            Assert.Equal(1, version.Sequence);
            AssertRecordEqual(row, version.Record);
            Assert.Equal(identity, version.Identity);
            Assert.Equal(JsonSerializer.Serialize(row.Snapshot, Json), JsonSerializer.Serialize(version.Confirmed, Json));
            AssertRecordEqual(row, journal.ReadLatestVersion().Record);
            Assert.Equal(beforeHash, FileHash(path));
            if (beforeHeadHash is not null) Assert.Equal(beforeHeadHash, FileHash(path + ".head"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static async Task WriteV4(string path, string runId)
    {
        await using var journal = new FileRunResourceJournal(path);
        await journal.AppendAsync(Initialize(runId));
    }

    private static RunResourceRecord Initialize(string runId) => new(1, RunResourcePhase.Initialize,
        "initialize", Identity(runId), Snapshot());

    private static RunResourceIdentity Identity(string runId) => new(runId, "task-" + runId,
        "session-" + runId, "contract-1", "v1", 1, "policy-1", 4, 3);

    private static RunResourceSnapshot Snapshot() => new(0, 0, 0, 0, [], [], [], [],
        new Dictionary<string, IReadOnlyList<string>>(), false, false, false);

    private static string CurrentBaselinePath(string path)
    {
        using var head = JsonDocument.Parse(File.ReadAllText(path + ".head"));
        var baselines = head.RootElement.GetProperty("baselines");
        var name = baselines[baselines.GetArrayLength() - 1].GetProperty("fileName").GetString()!;
        return Path.Combine(path + ".snapshots", name);
    }

    private static string HeadRecordingId(string path)
    {
        using var head = JsonDocument.Parse(File.ReadAllText(path + ".head"));
        return head.RootElement.GetProperty("recordingId").GetString()!;
    }

    private sealed record LegacyFrame(string Schema, string Previous, string Payload, object? Sources, string Hash,
        string? RecordingId = null);

    private static void WriteLegacyFrame(string path, string schema, RunResourceRecord row, string? recordingId)
    {
        var payload = JsonSerializer.Serialize(row, Json);
        var sources = JsonSerializer.Serialize<object?>(null, Json);
        var hashInput = "\n" + payload + "\n" + sources + (schema.EndsWith("v3", StringComparison.Ordinal) ? "\n" + recordingId : "");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));
        var frame = new LegacyFrame(schema, "", payload, null, hash, recordingId);
        File.WriteAllText(path, JsonSerializer.Serialize(frame, Json) + "\n", new UTF8Encoding(false));
    }

    private static string FrameHash(string path)
    {
        using var frame = JsonDocument.Parse(File.ReadAllText(path));
        return frame.RootElement.GetProperty("hash").GetString()!;
    }

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static SortedDictionary<string, string> CaptureFileHashes(string root) => new(
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path), FileHash, StringComparer.Ordinal), StringComparer.Ordinal);

    private static void RehashV4ChainWithChangedInitialSourceLength(string path)
    {
        var lines = File.ReadAllLines(path);
        var frames = lines.Select(line => JsonNode.Parse(line)!).ToArray();
        frames[0]["sources"]!["consultation"]!["length"] = 1;
        var previous = "";
        var serialized = new string[frames.Length];
        var offsets = new long[frames.Length];
        long offset = 0;
        for (var i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            frame["previous"] = previous;
            frame["hash"] = "";
            var hashInput = JsonSerializer.SerializeToUtf8Bytes(frame, Json);
            previous = Convert.ToHexString(SHA256.HashData(hashInput));
            frame["hash"] = previous;
            serialized[i] = JsonSerializer.Serialize(frame, Json);
            offsets[i] = offset;
            offset += Encoding.UTF8.GetByteCount(serialized[i]) + 1;
        }
        File.WriteAllText(path, string.Join("\n", serialized) + "\n", new UTF8Encoding(false));

        var headPath = path + ".head";
        var head = JsonNode.Parse(File.ReadAllText(headPath))!;
        head["hash"] = previous;
        head["length"] = offset;
        head["lastOffset"] = offsets[^1];
        File.WriteAllText(headPath, JsonSerializer.Serialize(head, Json) + "\n", new UTF8Encoding(false));
    }

    private static void AssertRecordEqual(RunResourceRecord expected, RunResourceRecord actual) =>
        Assert.Equal(JsonSerializer.Serialize(expected, Json), JsonSerializer.Serialize(actual, Json));
}
