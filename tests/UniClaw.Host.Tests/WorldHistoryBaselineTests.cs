using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldHistoryBaselineTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _root = Directory.CreateTempSubdirectory("world-baselines-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void DefaultWorldFileCommitsIndependentBaselinesConsumedByTheOriginalHistoryReader()
    {
        var path = Path.Combine(_root, "world"); var ep = Path.Combine(_root, "evidence");
        var runId = Guid.NewGuid().ToString("N"); string worldId, evidenceId;
        string[] expected;
        using (var evidence = new FileEvidenceJournal(ep))
        using (var journal = new FileWorldJournal(path))
        {
            worldId = journal.RecordingId; evidenceId = evidence.RecordingId;
            var world = new WorldModel(new HashSet<string> { "switch" }, history: new(journal, runId, evidenceId));
            var kernel = new UniKernel(new EvidenceLedger(evidence), world, DisabledRunTrace.Instance);
            for (var i = 0; i < 35; i++) kernel.Process(new ObservationProposal(new("switch", "value-" + i), IngressKind.Observation,
                ObservationContext.External, new("baseline-test", DateTimeOffset.Parse("2026-10-25T00:00:00Z").AddSeconds(i), "original", ["synthetic"])));
            expected = world.RevisionHistory.Select(r => JsonSerializer.Serialize(JsonSerializer.SerializeToElement(r, Json), Json)).ToArray();
        }
        using var er = new FileEvidenceJournal(ep, true);
        using var reader = new FileWorldJournal(path, true);
        var history = WorldHistoryRead.ReadOriginal(reader, worldId, runId, EvidenceLedger.RestoreReadOnly(er, evidenceId));
        Assert.Equal(expected, history.HistoricalRevisions.Select(r => r.GetRawText()));
        var baselines = new List<int>();
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            using var frame = JsonDocument.Parse(line);
            using var row = JsonDocument.Parse(frame.RootElement.GetProperty("payload").GetString()!);
            if (!row.RootElement.GetProperty("data").TryGetProperty("revisions", out var revisions)) continue;
            foreach (var revision in revisions.EnumerateArray())
                if (revision.TryGetProperty("baseline", out var baseline) && baseline.GetBoolean())
                    baselines.Add(revision.GetProperty("base").GetInt32() + 1);
        }
        Assert.Equal(new[] { 1, 33 }, baselines);
        Assert.False(history.ContinuationSupported);
        var latest = new WorldHistoryVersionReader(path, worldId).ReadLatest()!;
        Assert.Equal(expected[^1], latest.Revision.GetRawText());
        Assert.Equal(history.Position, latest.SourcePosition);
        Assert.Equal(history.Operations.Last(row => row.Data.GetProperty("revisions").GetArrayLength() != 0).Sequence, latest.OperationSequence);
        Assert.Equal(33, latest.BaselineRevisionNumber); Assert.Equal(3, latest.ReadCost.RevisionsDecoded);
    }

    [Fact]
    public void LatestAndHistoricalDataDecodeOnlyTheirNearestIndependentBaselineAndTail()
    {
        var path = Path.Combine(_root, "versions"); string recording;
        using (var writer = new FileWorldJournal(path, new WorldHistoryVersionOptions { BaselineInterval = 3 }))
        {
            recording = writer.RecordingId;
            for (var i = 1; i <= 10; i++) writer.Append(new(i, "reconcile", JsonSerializer.SerializeToElement(new
            {
                input = new { number = i },
                revisions = new[] { new { revisionId = "rev-" + i, worldState = new { subject = "value-" + i }, nullable = (string?)null } }
            }, Json)));
        }
        var versions = new WorldHistoryVersionReader(path, recording);
        Assert.Equal(10, versions.RevisionCount);
        var latest = versions.ReadLatest()!;
        Assert.Equal(10, latest.RevisionNumber); Assert.Equal(10, latest.OperationSequence);
        Assert.Equal(10, latest.SourcePosition.Sequence);
        Assert.Equal("value-10", latest.Revision.GetProperty("worldState").GetProperty("subject").GetString());
        Assert.Equal(10, latest.BaselineRevisionNumber); Assert.Equal(1, latest.ReadCost.RevisionsDecoded);
        var historical = versions.ReadRevision(8);
        Assert.Equal("value-8", historical.Revision.GetProperty("worldState").GetProperty("subject").GetString());
        Assert.Equal(7, historical.BaselineRevisionNumber); Assert.Equal(2, historical.ReadCost.RevisionsDecoded);
        Assert.Equal(11, historical.ReadCost.FramesValidated);
        Assert.False(historical.ContinuationSupported);
        Assert.Throws<ArgumentOutOfRangeException>(() => versions.ReadRevision(11));
        Assert.Throws<InvalidDataException>(() => new WorldHistoryVersionReader(path, Guid.NewGuid().ToString("N")));
    }

    [Theory]
    [InlineData("missing-baseline")]
    [InlineData("dependent-baseline")]
    [InlineData("interval-change")]
    [InlineData("revision-gap")]
    [InlineData("duplicate-revision")]
    public void SignedButInvalidBaselineOrRevisionSequenceIsRejected(string defect)
    {
        var path = Path.Combine(_root, "bad-baseline");
        var recording = WriteSimple(path);
        var frames = File.ReadAllLines(path).Select(line => JsonNode.Parse(line)!.AsObject()).ToArray();
        var row = JsonNode.Parse(frames[4]["payload"]!.GetValue<string>())!;
        var packet = row["data"]!["revisions"]![0]!;
        if (defect == "missing-baseline") packet["baseline"] = false;
        if (defect == "dependent-baseline") packet["values"]!.AsObject().Remove("nullable");
        if (defect == "interval-change") packet["interval"] = 4;
        if (defect == "revision-gap") packet["base"] = 4;
        if (defect == "duplicate-revision") row["data"]!["revisions"]!.AsArray().Add(packet.DeepClone());
        frames[4]["payload"] = row.ToJsonString(Json);
        RewriteFrames(path, frames);
        Assert.Throws<InvalidDataException>(() => new FileWorldJournal(path, true));
        Assert.Throws<InvalidDataException>(() => new WorldHistoryVersionReader(path, recording));
    }

    [Fact]
    public void AGoodLatestBaselineCannotHideCorruptionInTheEarlierPhysicalChain()
    {
        var path = Path.Combine(_root, "corrupt-prefix"); var recording = WriteSimple(path);
        var frames = File.ReadAllLines(path);
        var first = JsonNode.Parse(frames[1])!; first["payload"] = first["payload"]!.GetValue<string>() + " ";
        frames[1] = first.ToJsonString(Json); File.WriteAllLines(path, frames);
        Assert.Throws<InvalidDataException>(() => new WorldHistoryVersionReader(path, recording));
        Assert.Throws<InvalidDataException>(() => new FileWorldJournal(path, true));
    }

    [Fact]
    public void EmptyHistoryHasNoLatestRevisionAndInvalidPolicyDoesNotCreateAFile()
    {
        var path = Path.Combine(_root, "empty"); string recording;
        using (var writer = new FileWorldJournal(path)) recording = writer.RecordingId;
        var reader = new WorldHistoryVersionReader(path, recording);
        Assert.Equal(0, reader.RevisionCount); Assert.Null(reader.ReadLatest()); Assert.Null(reader.BaselineInterval);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadRevision(1));
        var invalid = Path.Combine(_root, "invalid-policy");
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileWorldJournal(invalid, new WorldHistoryVersionOptions { BaselineInterval = 0 }));
        Assert.False(File.Exists(invalid));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(32)]
    public void EveryRetainedRevisionHasTheDeclaredDecodeBoundAndOriginalValues(int interval)
    {
        var path = Path.Combine(_root, "all-versions"); var recording = WriteSimple(path, interval);
        var versions = new WorldHistoryVersionReader(path, recording);
        using var full = new FileWorldJournal(path, true);
        var expected = full.ReadRecords().SelectMany(row => row.Data.GetProperty("revisions").EnumerateArray()).ToArray();
        for (var i = 1; i <= expected.Length; i++)
        {
            var actual = versions.ReadRevision(i);
            Assert.Equal(expected[i - 1].GetRawText(), actual.Revision.GetRawText());
            Assert.InRange(actual.ReadCost.RevisionsDecoded, 1, interval);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void LegacyFullAndDeltaFilesAreReadWithoutRewriteOrAFalseNewPolicyClaim(int version)
    {
        var path = Path.Combine(_root, "legacy"); var recording = Guid.NewGuid().ToString("N");
        var full = new[] { """{"revisionId":"rev-1","worldState":{"x":1},"nullable":null}""",
            """{"revisionId":"rev-2","worldState":{"x":2},"nullable":null}""" };
        var packets = version == 1 ? full : new[]
        {
            """{"base":0,"order":["revisionId","worldState","nullable"],"values":{"revisionId":"rev-1","nullable":null},"maps":{"worldState":{"set":{"x":1},"remove":[],"order":{"prefix":0,"suffix":0,"items":["x"]}}},"arrays":{}}""",
            """{"base":1,"order":["revisionId","worldState","nullable"],"values":{"revisionId":"rev-2"},"maps":{"worldState":{"set":{"x":2},"remove":[],"order":{"prefix":1,"suffix":0,"items":[]}}},"arrays":{}}"""
        };
        var frames = new List<JsonObject> { new() { ["schema"] = "uniclaw.world.history.v" + version,
            ["recordingId"] = recording, ["sequence"] = 0, ["payload"] = null } };
        for (var i = 0; i < packets.Length; i++)
        {
            using var packet = JsonDocument.Parse(packets[i]);
            var row = new WorldHistoryRecord(i + 1, "reconcile", JsonSerializer.SerializeToElement(new { input = new { }, revisions = new[] { packet.RootElement } }, Json));
            frames.Add(new() { ["schema"] = "uniclaw.world.history.v" + version, ["recordingId"] = recording,
                ["sequence"] = i + 1, ["payload"] = JsonSerializer.Serialize(row, Json) });
        }
        RewriteFrames(path, frames.ToArray());
        var original = SHA256.HashData(File.ReadAllBytes(path));
        var reader = new WorldHistoryVersionReader(path, recording);
        Assert.Null(reader.BaselineInterval);
        Assert.Equal(full[1], reader.ReadLatest()!.Revision.GetRawText());
        Assert.Equal(version == 1 ? 1 : 2, reader.ReadLatest()!.ReadCost.RevisionsDecoded);
        using var strict = new FileWorldJournal(path, true);
        Assert.Equal(full, strict.ReadRecords().Select(row => row.Data.GetProperty("revisions")[0].GetRawText()));
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(path)));
        if (version == 2)
        {
            var originalFrames = File.ReadAllLines(path);
            foreach (var fault in new[] { "base", "range", "order", "missing" })
            {
                var invalidPath = path + "-" + fault;
                var changed = originalFrames.Select(line => JsonNode.Parse(line)!.AsObject()).ToArray();
                var row = JsonNode.Parse(changed[1]["payload"]!.GetValue<string>())!;
                var packet = row["data"]!["revisions"]![0]!;
                if (fault == "base") packet["base"] = 1;
                if (fault == "range") packet["maps"]!["worldState"]!["order"]!["prefix"] = 10;
                if (fault == "order") packet["order"]!.AsArray().Add("worldState");
                if (fault == "missing") packet["maps"]!.AsObject().Remove("worldState");
                changed[1]["payload"] = row.ToJsonString(Json); RewriteFrames(invalidPath, changed);
                Assert.Throws<InvalidDataException>(() => new FileWorldJournal(invalidPath, true));
                Assert.Throws<InvalidDataException>(() => new WorldHistoryVersionReader(invalidPath, recording).ReadLatest());
            }
        }
    }

    [Fact]
    public void ADataVersionDoesNotReplaceStrictValidationOfEarlierLogicalHistory()
    {
        var path = Path.Combine(_root, "bad-earlier-delta"); var recording = WriteSimple(path);
        var frames = File.ReadAllLines(path).Select(line => JsonNode.Parse(line)!.AsObject()).ToArray();
        var row = JsonNode.Parse(frames[2]["payload"]!.GetValue<string>())!;
        row["data"]!["revisions"]![0]!["maps"]!["worldState"]!["remove"] = new JsonArray("never-present");
        frames[2]["payload"] = row.ToJsonString(Json); RewriteFrames(path, frames);
        Assert.Throws<InvalidDataException>(() => new FileWorldJournal(path, true));
        var data = new WorldHistoryVersionReader(path, recording).ReadLatest()!;
        Assert.Equal(10, data.Revision.GetProperty("worldState").GetProperty("x").GetInt32());
        Assert.Equal(1, data.ReadCost.RevisionsDecoded);
        Assert.False(data.FullWorldHistoryValidated); Assert.False(data.ContinuationSupported);
    }

    [Fact]
    public void AnOversizePeriodicBaselineDoesNotPublishAnUncommittedRevisionOrEnableWriterRetry()
    {
        var path = Path.Combine(_root, "baseline-quota");
        using var writer = new FileWorldJournal(path, new WorldHistoryVersionOptions { BaselineInterval = 3 }, maxOperationBytes: 2000);
        var state = new Dictionary<string, string>();
        WorldHistoryRecord Row(int number) => new(number, "reconcile", JsonSerializer.SerializeToElement(new
        { input = new { }, revisions = new[] { new { revisionId = "rev-" + number, worldState = state } } }, Json));
        for (var i = 1; i <= 3; i++) { state["item-" + i] = new string('x', 400); writer.Append(Row(i)); }
        state["item-4"] = new string('x', 400);
        Assert.Throws<InvalidDataException>(() => writer.Append(Row(4)));
        Assert.Throws<IOException>(() => writer.Append(Row(4)));
        var latest = new WorldHistoryVersionReader(path, writer.RecordingId).ReadLatest()!;
        Assert.Equal(3, latest.RevisionNumber); Assert.Equal(3, latest.SourcePosition.Sequence);
        Assert.Equal(3, latest.Revision.GetProperty("worldState").EnumerateObject().Count());
        using var original = new FileWorldJournal(path, true);
        Assert.Equal(3, original.ReadRecords().Count);
    }

    private static string WriteSimple(string path, int interval = 3)
    {
        using var writer = new FileWorldJournal(path, new WorldHistoryVersionOptions { BaselineInterval = interval });
        for (var i = 1; i <= 10; i++) writer.Append(new(i, "reconcile", JsonSerializer.SerializeToElement(new
        { input = new { number = i }, revisions = new[] { new { revisionId = "rev-" + i, worldState = new { x = i }, nullable = (string?)null } } }, Json)));
        return writer.RecordingId;
    }

    private static void RewriteFrames(string path, JsonObject[] frames)
    {
        var previous = "";
        foreach (var frame in frames)
        {
            frame["previous"] = previous;
            previous = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "\n" + frame["recordingId"]!.GetValue<string>()
                + "\n" + frame["sequence"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture) + "\n" + (frame["payload"]?.GetValue<string>() ?? ""))));
            frame["hash"] = previous;
        }
        File.WriteAllLines(path, frames.Select(frame => frame.ToJsonString(Json)));
        File.WriteAllText(path + ".head", JsonSerializer.Serialize(new { schema = "uniclaw.world.history-head.v1",
            recordingId = frames[0]["recordingId"]!.GetValue<string>(), sequence = frames.Length - 1, hash = previous }, Json) + "\n");
    }
}
