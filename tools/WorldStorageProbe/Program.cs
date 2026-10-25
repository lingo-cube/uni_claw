using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;

if (args.FirstOrDefault() == "verify-existing")
{
    if (args.Length != 4) throw new ArgumentException("usage: WorldStorageProbe verify-existing <worldPath> <evidencePath> <outputJson>");
    VerifyExisting(args[1], args[2], args[3]);
    return;
}

if (args.Length is < 1 or > 2 || !int.TryParse(args.ElementAtOrDefault(1) ?? "128", out var count) || count < 1)
    throw new ArgumentException("usage: WorldStorageProbe <outputDirectory> [count=128]");

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var nativePath = Path.Combine(outputDirectory, "native-v3-interval32.world.jsonl");
var evidencePath = Path.Combine(outputDirectory, "native-v3-interval32.evidence.jsonl");
var v2Path = Path.Combine(outputDirectory, "fixture-v2-same-logical-rows.world.jsonl");
var v1Path = Path.Combine(outputDirectory, "fixture-v1-same-logical-rows.world.jsonl");
var interval8Path = Path.Combine(outputDirectory, "fixture-v3-interval8-same-logical-rows.world.jsonl");
var interval64Path = Path.Combine(outputDirectory, "fixture-v3-interval64-same-logical-rows.world.jsonl");
var reportPath = Path.Combine(outputDirectory, "world-storage-cost.json");
var allPaths = new[] { nativePath, evidencePath, v2Path, v1Path, interval8Path, interval64Path, reportPath }
    .SelectMany(path => new[] { path, path + ".head" }).ToArray();
if (allPaths.Any(File.Exists))
    throw new IOException("world-storage-probe-output-already-exists");

var runId = Guid.NewGuid().ToString("N");
string worldRecordingId;
string evidenceRecordingId;
HistoryWriteCounts nativeWriteStatistics;
var productionTimer = Stopwatch.StartNew();
var productionAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
using (var evidence = new FileEvidenceJournal(evidencePath))
using (var journal = new FileWorldJournal(nativePath, new WorldHistoryVersionOptions { BaselineInterval = 32 }))
{
    worldRecordingId = journal.RecordingId;
    evidenceRecordingId = evidence.RecordingId;
    var subjects = Enumerable.Range(0, count).Select(index => "probe.subject." + index).ToHashSet(StringComparer.Ordinal);
    var world = new WorldModel(subjects, history: new WorldHistoryRecording(journal, runId, evidenceRecordingId));
    var kernel = new UniKernel(new EvidenceLedger(evidence), world, DisabledRunTrace.Instance);
    for (var index = 0; index < count; index++)
        kernel.Process(Observation(index));
    nativeWriteStatistics = journal.WriteStatistics;
}
productionTimer.Stop();
var productionAllocated = GC.GetAllocatedBytesForCurrentThread() - productionAllocatedBefore;

IReadOnlyList<WorldHistoryRecord> sourceRows;
using (var nativeReader = new FileWorldJournal(nativePath, readOnly: true))
    sourceRows = nativeReader.ReadRecords();
using (var evidenceReader = new FileEvidenceJournal(evidencePath, readOnly: true))
{
    var evidence = EvidenceLedger.RestoreReadOnly(evidenceReader, evidenceRecordingId);
    using var nativeReader = new FileWorldJournal(nativePath, readOnly: true);
    _ = WorldHistoryRead.ReadOriginal(nativeReader, worldRecordingId, runId, evidence);
}

var fixtureTimer = Stopwatch.StartNew();
WriteV1(v1Path, worldRecordingId, sourceRows);
WriteV2(v2Path, worldRecordingId, sourceRows);
RewriteV3(interval8Path, sourceRows, 8);
RewriteV3(interval64Path, sourceRows, 64);
fixtureTimer.Stop();

var nativeHistory = ReadAndVerifyOriginal(nativePath, evidencePath, worldRecordingId, evidenceRecordingId, runId);
var v1History = ReadAndVerifyOriginal(v1Path, evidencePath, worldRecordingId, evidenceRecordingId, runId);
var v2History = ReadAndVerifyOriginal(v2Path, evidencePath, worldRecordingId, evidenceRecordingId, runId);
var interval8History = ReadAndVerifyOriginal(interval8Path, evidencePath, ReadRecordingId(interval8Path), evidenceRecordingId, runId);
var interval64History = ReadAndVerifyOriginal(interval64Path, evidencePath, ReadRecordingId(interval64Path), evidenceRecordingId, runId);
AssertSameRawHistory(nativeHistory, v1History, "v1");
AssertSameRawHistory(nativeHistory, v2History, "v2");
AssertSameRawHistory(nativeHistory, interval8History, "v3-interval8");
AssertSameRawHistory(nativeHistory, interval64History, "v3-interval64");

var revisions = nativeHistory.HistoricalRevisions.Count;
var revisionOracle = nativeHistory.HistoricalRevisions.Select(revision => new RevisionOracle(
    revision.GetProperty("revisionId").GetString() ?? throw new InvalidDataException("world-probe-missing-revision-id"),
    Sha256(revision.GetRawText()))).ToArray();
var report = new
{
    count,
    logical = new { operations = nativeHistory.Operations.Count, revisions, sourceWorldRecordingId = worldRecordingId,
        sourceEvidenceRecordingId = evidenceRecordingId, runRecordingId = runId, rawJsonEqual = true },
    nativeV3Production = new
    {
        path = Path.GetFileName(nativePath), baselineInterval = 32, writerManagedElapsedMilliseconds = productionTimer.Elapsed.TotalMilliseconds,
        writerManagedAllocatedBytesCurrentThread = productionAllocated, writeStatistics = nativeWriteStatistics,
        storage = InspectStorage(nativePath), fullRead = MeasureFullReadSamples(nativePath),
        latest = MeasureVersionQuerySamples(nativePath, worldRecordingId, revisionOracle, latest: true),
        midpoint = MeasureVersionQuerySamples(nativePath, worldRecordingId, revisionOracle, latest: false)
    },
    sameSourceFormatFixtures = new
    {
        v2 = new { path = Path.GetFileName(v2Path), schema = "uniclaw.world.history.v2", sameRecordingId = true,
            storage = InspectStorage(v2Path), fullRead = MeasureFullReadSamples(v2Path),
            latest = MeasureVersionQuerySamples(v2Path, worldRecordingId, revisionOracle, latest: true),
            midpoint = MeasureVersionQuerySamples(v2Path, worldRecordingId, revisionOracle, latest: false) },
        v1 = new { path = Path.GetFileName(v1Path), schema = "uniclaw.world.history.v1", sameRecordingId = true,
            storage = InspectStorage(v1Path), fullRead = MeasureFullReadSamples(v1Path),
            latest = MeasureVersionQuerySamples(v1Path, worldRecordingId, revisionOracle, latest: true),
            midpoint = MeasureVersionQuerySamples(v1Path, worldRecordingId, revisionOracle, latest: false) }
    },
    intervalRewriteFixtures = new
    {
        interval8 = new { path = Path.GetFileName(interval8Path), physicalRecordingId = ReadRecordingId(interval8Path),
            sameLogicalRows = true, fixtureRewrite = true, storage = InspectStorage(interval8Path),
            latest = MeasureVersionQuerySamples(interval8Path, ReadRecordingId(interval8Path), revisionOracle, latest: true),
            midpoint = MeasureVersionQuerySamples(interval8Path, ReadRecordingId(interval8Path), revisionOracle, latest: false) },
        interval64 = new { path = Path.GetFileName(interval64Path), physicalRecordingId = ReadRecordingId(interval64Path),
            sameLogicalRows = true, fixtureRewrite = true, storage = InspectStorage(interval64Path),
            latest = MeasureVersionQuerySamples(interval64Path, ReadRecordingId(interval64Path), revisionOracle, latest: true),
            midpoint = MeasureVersionQuerySamples(interval64Path, ReadRecordingId(interval64Path), revisionOracle, latest: false) }
    },
    fixtureGeneration = new { elapsedMilliseconds = fixtureTimer.Elapsed.TotalMilliseconds,
        qualification = "v1/v2 and interval8/64 are regenerated same-logical-row fixtures. Their generation time is not production typed writer performance." },
    metricLimits = new
    {
        managed = "Elapsed time and GC.GetAllocatedBytesForCurrentThread are bounded three-sample observations after one warmup; they are not stable-speed claims.",
        writeStatistics = "Native v3 WriteStatistics are actual managed frame/head write and flush call counts; not physical I/O/syscall/durability measurements.",
        notMeasured = new[] { "physical IO", "peak memory" },
        fullHistory = "FileWorldJournal constructor plus ReadRecords and WorldHistoryRead remain full physical/logical history work; full-chain scanning is O(n).",
        versionReader = "WorldHistoryVersionReader validates the full physical frame/head chain at construction; ReadCost.RevisionsDecoded describes only data reconstruction from the selected baseline. ContinuationSupported is false."
    }
};
File.WriteAllText(reportPath, JsonSerializer.Serialize(report, json) + Environment.NewLine);
Console.WriteLine(JsonSerializer.Serialize(report, json));

static ObservationProposal Observation(int index) => new(new("probe.subject." + index, new string('x', 512) + index),
    IngressKind.Observation, ObservationContext.External,
    new("world-storage-probe", DateTimeOffset.Parse("2026-10-26T00:00:00Z").AddSeconds(index), "synthetic", ["probe"]));

static WorldHistoryRead ReadAndVerifyOriginal(string worldPath, string evidencePath, string worldId, string evidenceId, string runId)
{
    using var evidenceSource = new FileEvidenceJournal(evidencePath, readOnly: true);
    var evidence = EvidenceLedger.RestoreReadOnly(evidenceSource, evidenceId);
    using var worldSource = new FileWorldJournal(worldPath, readOnly: true);
    var history = WorldHistoryRead.ReadOriginal(worldSource, worldId, runId, evidence);
    return history;
}

static void AssertSameRawHistory(WorldHistoryRead expected, WorldHistoryRead actual, string name)
{
    if (expected.Operations.Count != actual.Operations.Count || expected.HistoricalRevisions.Count != actual.HistoricalRevisions.Count)
        throw new InvalidDataException("world-probe-row-count-" + name);
    for (var index = 0; index < expected.Operations.Count; index++)
        if (expected.Operations[index].Sequence != actual.Operations[index].Sequence || expected.Operations[index].Operation != actual.Operations[index].Operation
            || expected.Operations[index].Data.GetRawText() != actual.Operations[index].Data.GetRawText())
            throw new InvalidDataException("world-probe-raw-json-mismatch-" + name + "-" + index);
    for (var index = 0; index < expected.HistoricalRevisions.Count; index++)
        if (expected.HistoricalRevisions[index].GetRawText() != actual.HistoricalRevisions[index].GetRawText())
            throw new InvalidDataException("world-probe-raw-revision-mismatch-" + name + "-" + index);
}

static int CountRevisions(IReadOnlyList<WorldHistoryRecord> rows) => rows.Sum(row =>
    row.Data.TryGetProperty("revisions", out var revisions) ? revisions.GetArrayLength() : 0);

static object MeasureFullReadSamples(string path)
{
    _ = MeasureFullRead(path); // warmup
    var samples = Enumerable.Range(0, 3).Select(_ => MeasureFullRead(path)).ToArray();
    return new { warmupDiscarded = true, samples };
}

static object MeasureFullRead(string path)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var timer = Stopwatch.StartNew();
    using var reader = new FileWorldJournal(path, readOnly: true);
    var rows = reader.ReadRecords();
    timer.Stop();
    return new { elapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
        allocatedManagedBytesCurrentThread = GC.GetAllocatedBytesForCurrentThread() - allocated, operations = rows.Count,
        revisions = CountRevisions(rows) };
}

static object MeasureVersionQuerySamples(string path, string recordingId, IReadOnlyList<RevisionOracle> oracle, bool latest)
{
    _ = MeasureVersionQuery(path, recordingId, oracle, latest); // warmup
    var samples = Enumerable.Range(0, 3).Select(_ => MeasureVersionQuery(path, recordingId, oracle, latest)).ToArray();
    return new { kind = latest ? "latest" : "revision", requestedRevision = latest ? oracle.Count : Math.Max(1, oracle.Count / 2), warmupDiscarded = true, samples };
}

static object MeasureVersionQuery(string path, string recordingId, IReadOnlyList<RevisionOracle> oracle, bool latest)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var timer = Stopwatch.StartNew();
    var reader = new WorldHistoryVersionReader(path, recordingId);
    var requestedRevision = latest ? oracle.Count : Math.Max(1, oracle.Count / 2);
    var result = latest ? reader.ReadLatest() : reader.ReadRevision(requestedRevision);
    timer.Stop();
    if (result is null) throw new InvalidDataException("world-probe-empty-version-history");
    var expected = oracle[requestedRevision - 1];
    var revisionId = result.Revision.GetProperty("revisionId").GetString();
    var revisionSha256 = Sha256(result.Revision.GetRawText());
    if (result.RevisionNumber != requestedRevision || revisionId != expected.RevisionId || revisionSha256 != expected.Sha256)
        throw new InvalidDataException("world-probe-version-query-oracle-mismatch");
    return new { elapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
        allocatedManagedBytesCurrentThread = GC.GetAllocatedBytesForCurrentThread() - allocated,
        result.RevisionNumber, result.OperationSequence, result.SourcePosition, revisionId, revisionSha256,
        result.BaselineRevisionNumber, readCost = new { result.ReadCost.RevisionsDecoded, result.ReadCost.FramesValidated, result.ReadCost.SourceBytes },
        result.ContinuationSupported, result.FullWorldHistoryValidated };
}

static object InspectStorage(string path)
{
    var frames = File.ReadLines(path).Select(line => JsonDocument.Parse(line)).ToArray();
    try
    {
        var packets = frames.Skip(1).SelectMany(frame =>
        {
            var payload = frame.RootElement.GetProperty("payload").GetString()!;
            using var row = JsonDocument.Parse(payload);
            return row.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("revisions", out var revisions)
                ? revisions.EnumerateArray().Select(value => value.GetRawText()).ToArray() : Array.Empty<string>();
        }).ToArray();
        var baseline = packets.Where(packet => IsBaseline(packet)).ToArray();
        return new { fileBytes = new FileInfo(path).Length, headBytes = new FileInfo(path + ".head").Length,
            frames = frames.Length, revisionPackets = packets.Length, baselinePackets = baseline.Length,
            baselineSerializedBytes = baseline.Sum(packet => Encoding.UTF8.GetByteCount(packet)),
            deltaSerializedBytes = packets.Except(baseline).Sum(packet => Encoding.UTF8.GetByteCount(packet)) };
    }
    finally { foreach (var frame in frames) frame.Dispose(); }
}

static bool IsBaseline(string packet)
{
    using var json = JsonDocument.Parse(packet);
    return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("baseline", out var value) && value.GetBoolean();
}

static string ReadRecordingId(string path)
{
    using var first = JsonDocument.Parse(File.ReadLines(path).First());
    return first.RootElement.GetProperty("recordingId").GetString()!;
}

static void RewriteV3(string path, IReadOnlyList<WorldHistoryRecord> rows, int interval)
{
    using var writer = new FileWorldJournal(path, new WorldHistoryVersionOptions { BaselineInterval = interval });
    foreach (var row in rows) writer.Append(row);
}

static void WriteV2(string path, string recordingId, IReadOnlyList<WorldHistoryRecord> rows)
{
    var codec = new WorldRevisionDeltaWriter();
    WriteFrames(path, "uniclaw.world.history.v2", recordingId, rows, row => row with
    {
        Data = RenderData(row.Data, revision => codec.Encode(revision))
    });
}

static void WriteV1(string path, string recordingId, IReadOnlyList<WorldHistoryRecord> rows) =>
    WriteFrames(path, "uniclaw.world.history.v1", recordingId, rows, row => row);

static JsonElement RenderData(JsonElement original, Func<JsonElement, JsonElement> encode)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
        foreach (var property in original.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (property.Name != "revisions") { property.Value.WriteTo(writer); continue; }
            writer.WriteStartArray();
            foreach (var revision in property.Value.EnumerateArray()) encode(revision).WriteTo(writer);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
    using var document = JsonDocument.Parse(stream.ToArray());
    return document.RootElement.Clone();
}

static void WriteFrames(string path, string schema, string recordingId, IReadOnlyList<WorldHistoryRecord> rows,
    Func<WorldHistoryRecord, WorldHistoryRecord> transform)
{
    var previous = "";
    using var file = new StreamWriter(path, false, new UTF8Encoding(false));
    for (var sequence = 0; sequence <= rows.Count; sequence++)
    {
        var payload = sequence == 0 ? null : JsonSerializer.Serialize(transform(rows[sequence - 1]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var hash = Hash(previous, recordingId, sequence, payload);
        file.WriteLine(JsonSerializer.Serialize(new { schema, recordingId, sequence, previous, payload, hash }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        previous = hash;
    }
    file.Flush();
    File.WriteAllText(path + ".head", JsonSerializer.Serialize(new { schema = "uniclaw.world.history-head.v1", recordingId,
        sequence = rows.Count, hash = previous }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "\n");
}

static string Hash(string previous, string recordingId, int sequence, string? payload) => Convert.ToHexString(SHA256.HashData(
    Encoding.UTF8.GetBytes(previous + "\n" + recordingId + "\n" + sequence.ToString(CultureInfo.InvariantCulture) + "\n" + (payload ?? ""))));

static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

static void VerifyExisting(string inputWorldPath, string inputEvidencePath, string outputJson)
{
    var worldPath = Path.GetFullPath(inputWorldPath);
    var evidencePath = Path.GetFullPath(inputEvidencePath);
    var reportPath = Path.GetFullPath(outputJson);
    var sources = new[] { worldPath, worldPath + ".head", evidencePath, evidencePath + ".head" };
    if (sources.Contains(reportPath, StringComparer.Ordinal))
        throw new ArgumentException("world-storage-probe-output-must-not-replace-source");
    var before = SourceHashes(sources);
    WorldHistoryRead history;
    string worldRecordingId;
    string evidenceRecordingId;
    using (var evidenceSource = new FileEvidenceJournal(evidencePath, readOnly: true))
    using (var worldSource = new FileWorldJournal(worldPath, readOnly: true))
    {
        worldRecordingId = worldSource.RecordingId;
        evidenceRecordingId = evidenceSource.RecordingId;
        var rows = worldSource.ReadRecords();
        if (rows.Count == 0 || rows[0].Sequence != 1 || rows[0].Operation != "initialize")
            throw new InvalidDataException("world-storage-probe-initialization-missing");
        var initialization = rows[0].Data;
        var runRecordingId = initialization.GetProperty("runRecordingId").GetString()
            ?? throw new InvalidDataException("world-storage-probe-run-id-missing");
        var initializationEvidenceId = initialization.GetProperty("evidenceRecordingId").GetString()
            ?? throw new InvalidDataException("world-storage-probe-evidence-id-missing");
        if (initializationEvidenceId != evidenceRecordingId)
            throw new InvalidDataException("world-storage-probe-evidence-recording-mismatch");
        history = WorldHistoryRead.ReadOriginal(worldSource, worldRecordingId, runRecordingId,
            EvidenceLedger.RestoreReadOnly(evidenceSource, evidenceRecordingId));
    }
    var versionReader = new WorldHistoryVersionReader(worldPath, worldRecordingId);
    var latest = versionReader.ReadLatest();
    string? originalFullReaderLatestSha256 = null;
    if (history.HistoricalRevisions.Count == 0)
    {
        if (latest is not null) throw new InvalidDataException("world-storage-probe-unexpected-latest-revision");
    }
    else
    {
        if (latest is null) throw new InvalidDataException("world-storage-probe-missing-latest-revision");
        var expected = history.HistoricalRevisions[^1].GetRawText();
        var actual = latest.Revision.GetRawText();
        originalFullReaderLatestSha256 = Sha256(expected);
        if (actual != expected || Sha256(actual) != originalFullReaderLatestSha256)
            throw new InvalidDataException("world-storage-probe-latest-query-mismatch");
    }
    var after = SourceHashes(sources);
    if (!before.SequenceEqual(after)) throw new IOException("world-storage-probe-source-mutated");
    var report = new
    {
        mode = "verify-existing",
        source = new
        {
            worldPath, evidencePath, worldRecordingId, evidenceRecordingId, worldSchema = ReadJournalSchema(worldPath), evidenceSchema = ReadJournalSchema(evidencePath),
            operations = history.Operations.Count, revisions = history.HistoricalRevisions.Count, hashesBefore = before, hashesAfter = after,
            sourceHashesUnchanged = true
        },
        latest = latest is null ? null : new
        {
            latest.RevisionNumber, latest.OperationSequence, latest.SourcePosition,
            revisionId = latest.Revision.GetProperty("revisionId").GetString(), revisionSha256 = Sha256(latest.Revision.GetRawText()),
            originalFullReaderRevisionSha256 = originalFullReaderLatestSha256,
            latest.BaselineRevisionNumber, readCost = new { latest.ReadCost.FramesValidated, latest.ReadCost.RevisionsDecoded, latest.ReadCost.SourceBytes },
            latest.ContinuationSupported, latest.FullWorldHistoryValidated, matchesOriginalFullReader = true
        },
        qualification = "Read-only compatibility verification. Original WorldHistoryRead validates the complete historical source; WorldHistoryVersionReader is a frozen data projection and never continuation authority."
    };
    File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }) + Environment.NewLine);
}

static string[] SourceHashes(IEnumerable<string> paths) => paths.Select(path => path + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();

static string ReadJournalSchema(string path)
{
    using var frame = JsonDocument.Parse(File.ReadLines(path).First());
    return frame.RootElement.GetProperty("schema").GetString() ?? throw new InvalidDataException("world-storage-probe-schema-missing");
}

sealed record RevisionOracle(string RevisionId, string Sha256);
