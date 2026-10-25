using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldHistoryDeltaTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _root = Directory.CreateTempSubdirectory("world-delta-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void NewNativeJournalWritesDeltasAndReturnsEveryOriginalRevisionInOrder()
    {
        var path = Path.Combine(_root, "world");
        var originals = new[]
        {
            Row(1, """{"revisionId":"rev-1","worldState":{"a":{"value":"old"},"b":{"value":"keep"}},"evidenceBasis":["e1","e2"],"worldGraph":["a","b"],"nullable":null}"""),
            Row(2, """{"revisionId":"rev-2","worldState":{"b":{"value":"keep"},"a":{"value":"new"},"c":{"value":"added"}},"evidenceBasis":["e2","e1","e3"],"worldGraph":["a","x","b"],"nullable":[1]}"""),
            Row(3, """{"revisionId":"rev-3","worldState":{"c":{"value":"added"},"b":{"value":"keep"}},"evidenceBasis":["e3"],"worldGraph":[],"nullable":null}"""),
        };
        using (var writer = new FileWorldJournal(path)) foreach (var row in originals) writer.Append(row);
        Assert.Contains("uniclaw.world.history.v3", File.ReadAllText(path));
        using var reader = new FileWorldJournal(path, true);
        Assert.Equal(originals.Select(r => r.Data.GetRawText()), reader.ReadRecords().Select(r => r.Data.GetRawText()));
        var versions = new WorldHistoryVersionReader(path, reader.RecordingId);
        for (var i = 1; i <= originals.Length; i++)
            Assert.Equal(originals[i - 1].Data.GetProperty("revisions")[0].GetRawText(), versions.ReadRevision(i).Revision.GetRawText());
    }

    private static WorldHistoryRecord Row(int number, string revision) => new(number, "reconcile",
        JsonDocument.Parse("{\"input\":{\"id\":" + number + "},\"revisions\":[" + revision + "],\"log\":[]}").RootElement.Clone());

    [Fact]
    public void AcknowledgedAppendDoesNotRetainTheCallersJsonDocumentLifetime()
    {
        var path = Path.Combine(_root, "document-lifetime");
        var rows = new[] { Row(1, """{"worldState":{"a":1},"worldGraph":["a"]}"""),
            Row(2, """{"worldState":{"a":1,"b":2},"worldGraph":["a","b"]}""") };
        using (var writer = new FileWorldJournal(path))
            foreach (var row in rows)
            {
                using var callerDocument = JsonDocument.Parse(row.Data.GetRawText());
                writer.Append(row with { Data = callerDocument.RootElement });
            }
        using var reader = new FileWorldJournal(path, true);
        Assert.Equal(rows.Select(r => r.Data.GetRawText()), reader.ReadRecords().Select(r => r.Data.GetRawText()));
    }

    [Fact]
    public void AnUnchangedMapWithChangedPropertyOrderStillRetainsTheNewOrder()
    {
        var path = Path.Combine(_root, "order");
        var rows = new[] { Row(1, """{"worldState":{"a":1,"b":2},"nested":{"x":1,"y":2}}"""),
            Row(2, """{"worldState":{"b":2,"a":1},"nested":{"y":2,"x":1}}""") };
        using (var writer = new FileWorldJournal(path)) foreach (var row in rows) writer.Append(row);
        using var reader = new FileWorldJournal(path, true);
        Assert.Equal(rows.Select(r => r.Data.GetRawText()), reader.ReadRecords().Select(r => r.Data.GetRawText()));
    }

    [Fact]
    public void LegacyFullRevisionFilesRemainReadableWithoutBeingRewritten()
    {
        var path = Path.Combine(_root, "legacy");
        var rows = new[] { Row(1, """{"revisionId":"rev-1","worldState":{"x":1},"evidenceBasis":["e1"]}""") };
        WriteV1(path, Guid.NewGuid().ToString("N"), rows);
        var before = SHA256.HashData(File.ReadAllBytes(path));
        using var reader = new FileWorldJournal(path, true);
        Assert.Equal(rows[0].Data.GetRawText(), reader.ReadRecords()[0].Data.GetRawText());
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
    }

    private sealed class TypedJournal : IWorldJournal
    {
        public bool CanInitialize => true;
        public string RecordingId { get; } = Guid.NewGuid().ToString("N");
        public int TypedOperations { get; private set; }
        public IReadOnlyList<WorldHistoryRecord> ReadRecords() => [];
        public void Append(WorldHistoryRecord record) => Assert.Equal("initialize", record.Operation);
        public void AppendOperation(WorldHistoryRecord operation, IReadOnlyList<WorldBeliefRevision> revisions)
        {
            Assert.False(operation.Data.TryGetProperty("revisions", out _));
            if (operation.Operation == "reconcile") Assert.Single(revisions);
            TypedOperations++;
        }
    }

    private static ObservationProposal Seed(string subject, string value, int number) => new(new(subject, value), IngressKind.Observation,
        ObservationContext.External, new("delta-test", DateTimeOffset.Parse("2026-10-25T00:00:00Z").AddSeconds(number), "original", ["synthetic"]));

    [Fact]
    public void WorldOwnerUsesTheTypedJournalSeamBeforePublishing()
    {
        var journal = new TypedJournal();
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        kernel.Process(Seed("switch", "on", 0));
        Assert.Equal(2, journal.TypedOperations);
        Assert.Single(world.RevisionHistory);
    }

    [Fact]
    public void ProductionWorldRevisionsRoundTripAndOccupyLessThanTheFullFile()
    {
        var path = Path.Combine(_root, "native");
        var ep = Path.Combine(_root, "evidence");
        const int count = 128;
        string worldId, evidenceId;
        HistoryWriteCounts nativeWriteCounts;
        var runId = Guid.NewGuid().ToString("N");
        string[] expected;
        using (var evidence = new FileEvidenceJournal(ep))
        using (var journal = new FileWorldJournal(path))
        {
            worldId = journal.RecordingId; evidenceId = evidence.RecordingId;
            var world = new WorldModel(Enumerable.Range(0, count).Select(i => "item-" + i).ToHashSet(), history: new(journal, runId, evidenceId));
            var kernel = new UniKernel(new EvidenceLedger(evidence), world, DisabledRunTrace.Instance);
            for (var i = 0; i < count; i++) kernel.Process(Seed("item-" + i, new string('x', 512) + i, i));
            nativeWriteCounts = journal.WriteStatistics;
            // v1 also wrote the typed revision into a JsonElement, then serialized the outer record.
            // Normalize that existing JSON escaping step before comparing the original field order.
            expected = world.RevisionHistory.Select(r => JsonSerializer.Serialize(JsonSerializer.SerializeToElement(r, Json), Json)).ToArray();
        }
        using var er = new FileEvidenceJournal(ep, true);
        using var reader = new FileWorldJournal(path, true);
        var history = WorldHistoryRead.ReadOriginal(reader, worldId, runId, EvidenceLedger.RestoreReadOnly(er, evidenceId));
        Assert.Equal(expected.Length, history.HistoricalRevisions.Count);
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], history.HistoricalRevisions[i].GetRawText());
        var baseline = WriteV1(Path.Combine(_root, "full-v1"), worldId, reader.ReadRecords());
        var delta = new FileInfo(path).Length;
        Assert.True(delta < baseline / 3, $"delta={delta}, full={baseline}");
        var legacyPath = Path.Combine(_root, "full-v1");
        var nativeRead = MeasureFullRead(path);
        var legacyRead = MeasureFullRead(legacyPath);
        Assert.True(nativeRead.AllocatedBytes < 128L * 1024 * 1024,
            $"Full native World read allocated {nativeRead.AllocatedBytes} bytes for 128 observations");
        var nativeJson = nativeRead.Records.Select(r => r.Data.GetRawText()).ToArray();
        var legacyJson = legacyRead.Records.Select(r => r.Data.GetRawText()).ToArray();
        Assert.Equal(nativeJson, legacyJson);
        var nativeLogical = CountLogicalRecords(nativeRead.Records);
        var legacyLogical = CountLogicalRecords(legacyRead.Records);
        Assert.Equal(nativeLogical, legacyLogical);
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_DELTA_EXPORT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            foreach (var file in Directory.GetFiles(_root)) File.Copy(file, Path.Combine(output, Path.GetFileName(file)), true);
            File.WriteAllText(Path.Combine(output, "size.json"), JsonSerializer.Serialize(new
            {
                observations = count, operations = history.Operations.Count, revisions = expected.Length,
                logicalJsonEqual = true, logicalOperations = nativeLogical.Operations, logicalRevisions = nativeLogical.Revisions,
                nativeV3 = new { fileBytes = new FileInfo(path).Length, headBytes = new FileInfo(path + ".head").Length,
                    constructorAndReadElapsedMilliseconds = nativeRead.Elapsed.TotalMilliseconds, constructorAndReadAllocatedBytes = nativeRead.AllocatedBytes },
                legacyV1 = new { fileBytes = new FileInfo(legacyPath).Length, headBytes = new FileInfo(legacyPath + ".head").Length,
                    constructorAndReadElapsedMilliseconds = legacyRead.Elapsed.TotalMilliseconds, constructorAndReadAllocatedBytes = legacyRead.AllocatedBytes },
                nativeWriter = nativeWriteCounts,
                fullBytes = baseline, deltaBytes = delta, reduction = 1d - (double)delta / baseline,
                absentMetrics = new { latestOnlyBuyer = "not_measured_no_buyer", filesystemSyscallOrReadByteCounter = "not_measured", peakMemory = "not_measured" },
                qualification = "Same actual World operation outputs, same recording ID, uncompressed files. Constructor+ReadRecords wall time and current-thread managed allocations; no latest-only buyer, filesystem syscall/read-byte counter, or peak-memory claim."
            }, Json));
        }
    }

    private static (IReadOnlyList<WorldHistoryRecord> Records, TimeSpan Elapsed, long AllocatedBytes) MeasureFullRead(string path)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        using var reader = new FileWorldJournal(path, true);
        var records = reader.ReadRecords();
        timer.Stop();
        return (records, timer.Elapsed, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    }

    private static (int Operations, int Revisions) CountLogicalRecords(IReadOnlyList<WorldHistoryRecord> records)
    {
        var revisions = 0;
        foreach (var record in records)
            if (record.Data.TryGetProperty("revisions", out var values) && values.ValueKind == JsonValueKind.Array)
                revisions += values.GetArrayLength();
        return (records.Count, revisions);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("range")]
    [InlineData("order")]
    [InlineData("missing")]
    public void InvalidDeltaIsRejectedEvenWithAConsistentFrameHashAndHead(string defect)
    {
        var path = Path.Combine(_root, "invalid");
        using (var writer = new FileWorldJournal(path))
            writer.Append(Row(1, """{"worldState":{"a":1},"worldGraph":["a"]}"""));
        var frames = File.ReadAllLines(path).Select(line => JsonNode.Parse(line)!.AsObject()).ToArray();
        var row = JsonNode.Parse(frames[1]["payload"]!.GetValue<string>())!;
        var packet = row["data"]!["revisions"]![0]!;
        if (defect == "base") packet["base"] = 1;
        if (defect == "range") packet["arrays"]!["worldGraph"] = new JsonObject { ["prefix"] = 10, ["suffix"] = 0, ["items"] = new JsonArray() };
        if (defect == "order") packet["order"]!.AsArray().Add("worldState");
        if (defect == "missing") packet["values"]!.AsObject().Remove("worldState");
        frames[1]["payload"] = row.ToJsonString(Json);
        RewriteFrames(path, frames);
        Assert.Throws<InvalidDataException>(() => new FileWorldJournal(path, true));
    }

    private static void RewriteFrames(string path, JsonObject[] frames)
    {
        var previous = "";
        foreach (var frame in frames)
        {
            frame["previous"] = previous;
            var payload = frame["payload"]?.GetValue<string>();
            previous = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "\n" + frame["recordingId"]!.GetValue<string>()
                + "\n" + frame["sequence"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture) + "\n" + (payload ?? ""))));
            frame["hash"] = previous;
        }
        File.WriteAllLines(path, frames.Select(f => f.ToJsonString(Json)));
        File.WriteAllText(path + ".head", JsonSerializer.Serialize(new { schema = "uniclaw.world.history-head.v1",
            recordingId = frames[0]["recordingId"]!.GetValue<string>(), sequence = frames.Length - 1, hash = previous }, Json) + "\n");
    }

    [Fact]
    public void NativeCommitFailureDoesNotPublishWorldAndLatchesTheWriter()
    {
        var path = Path.Combine(_root, "blocked-head");
        using var journal = new FileWorldJournal(path);
        var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        var kernel = new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance);
        var published = world.RevisionHistory;
        File.Delete(path + ".head");
        Directory.CreateDirectory(path + ".head");
        Assert.ThrowsAny<IOException>(() => kernel.Process(Seed("switch", "on", 0)));
        Assert.Empty(published);
        Assert.Equal(1, world.HistoryPosition!.Sequence);
        Assert.ThrowsAny<IOException>(() => world.Current);
        Assert.ThrowsAny<IOException>(() => kernel.Process(Seed("switch", "off", 1)));
        Assert.Throws<IOException>(() => journal.Append(Row(3, "{}")));
    }

    private static long WriteV1(string path, string recording, IReadOnlyList<WorldHistoryRecord> rows)
    {
        var previous = "";
        using var file = new StreamWriter(path, false, new UTF8Encoding(false));
        for (var sequence = 0; sequence <= rows.Count; sequence++)
        {
            var payload = sequence == 0 ? null : JsonSerializer.Serialize(rows[sequence - 1], Json);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "\n" + recording + "\n"
                + sequence.ToString(CultureInfo.InvariantCulture) + "\n" + (payload ?? ""))));
            file.WriteLine(JsonSerializer.Serialize(new { schema = "uniclaw.world.history.v1", recordingId = recording,
                sequence, previous, payload, hash }, Json));
            previous = hash;
        }
        file.Flush();
        File.WriteAllText(path + ".head", JsonSerializer.Serialize(new { schema = "uniclaw.world.history-head.v1", recordingId = recording,
            sequence = rows.Count, hash = previous }, Json) + "\n");
        return new FileInfo(path).Length;
    }
}
