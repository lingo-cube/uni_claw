using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class CoreHistoryVersionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("core-history-versions-").FullName;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HistoryVersionOptions SmallTail = new() { MaxTailRecords = 4 };
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task ControlDataVersionsPreserveEveryOriginalInputStateAndResultInBothDirections()
    {
        var path = Path.Combine(_root, "control");
        var expected = Enumerable.Range(1, 25).Select(ControlRow).ToArray();
        await using (var writer = new FileControlTraversalJournal(path, SmallTail))
        {
            await writer.AppendAsync(expected[0]);
            await writer.AppendBatchAsync(expected[1..3]);
            foreach (var row in expected[3..]) await writer.AppendAsync(row);
            Assert.Equal(Serialize(expected), Serialize(writer.ReadRecords()));
            Assert.Equal(7, writer.VersionWriteStatistics.BaselineWrites);
        }
        var before = Digests();
        using var reader = new FileControlTraversalJournal(path, true);
        Assert.False(reader.CanInitialize);
        foreach (var row in expected)
        {
            Assert.Equal(Serialize(row), Serialize(reader.ReadVersion(row.Sequence).Record));
            Assert.Equal(Serialize(row), Serialize(reader.ReadVersion(row.Sequence, HistoryVersionDirection.Reverse).Record));
        }
        var latest = reader.ReadLatestVersion();
        Assert.Equal(25, latest.Sequence);
        Assert.Equal(reader.RecordingId, latest.RecordingId);
        Assert.Equal(Serialize(expected[^1]), Serialize(latest.Record));
        Assert.InRange(latest.ReadCost.FramesRead, 1, SmallTail.MaxTailRecords + 3);
        Assert.Equal(1, latest.ReadCost.SnapshotReads);
        Assert.True(latest.ReadCost.BytesRead < new FileInfo(path).Length);
        Assert.Equal(Serialize(expected), Serialize(reader.ReadRecords()));
        Assert.Equal(before, Digests());
    }

    [Fact]
    public async Task PlanVersionMapAndUnchangedAdoptionFactsRoundTripAfterReopening()
    {
        var path = Path.Combine(_root, "plans");
        var expected = Enumerable.Range(1, 25).Select(i => new RuntimePlanHistoryRecord(
            "uniclaw.runtime.plan.adoption.v1", i, "fixture",
            Enumerable.Range(1, (i + 1) / 2).ToDictionary(n => "plan-" + n, n => n == (i + 1) / 2 ? i : n),
            JsonSerializer.SerializeToElement(new { graphId = "original-graph-" + i, nodes = new[] { "immutable", "采纳事实" } }, Json),
            DecisionId: "decision-" + i)).ToArray();
        await using (var writer = new FileRuntimePlanJournal(path, SmallTail))
            foreach (var row in expected) await writer.AppendAsync(row);
        var before = Digests();
        using var reader = new FileRuntimePlanJournal(path, true);
        foreach (var row in expected)
        {
            Assert.Equal(Serialize(row), Serialize(reader.ReadVersion(row.Sequence).Record));
            Assert.Equal(Serialize(row), Serialize(reader.ReadVersion(row.Sequence, HistoryVersionDirection.Reverse).Record));
        }
        Assert.Equal(Serialize(expected[^1]), Serialize(reader.ReadLatestVersion().Record));
        Assert.Equal(Serialize(expected), Serialize(reader.ReadRecords()));
        Assert.Equal(before, Digests());
    }

    [Theory]
    [InlineData("head-missing")]
    [InlineData("head-source")]
    [InlineData("torn")]
    [InlineData("suffix")]
    [InlineData("last-hash")]
    [InlineData("baseline")]
    public void LatestDataNeverAcceptsUnboundOrIncompleteSources(string fault)
    {
        var path = Path.Combine(_root, "control");
        using (var writer = new FileControlTraversalJournal(path, SmallTail))
            foreach (var i in Enumerable.Range(1, 13)) writer.Append(ControlRow(i));
        if (fault == "head-missing") File.Delete(path + ".head");
        if (fault == "head-source")
        {
            var head = JsonNode.Parse(File.ReadAllText(path + ".head"))!;
            head["recordingId"] = Guid.NewGuid().ToString("N");
            File.WriteAllText(path + ".head", head.ToJsonString() + "\n");
        }
        if (fault == "torn") File.WriteAllBytes(path, File.ReadAllBytes(path)[..^1]);
        if (fault == "suffix") File.AppendAllText(path, "{}\n");
        if (fault == "last-hash")
        {
            var lines = File.ReadAllLines(path);
            var last = JsonNode.Parse(lines[^1])!;
            last["hash"] = new string('0', 64);
            lines[^1] = last.ToJsonString();
            File.WriteAllLines(path, lines);
        }
        if (fault == "baseline")
            foreach (var file in Directory.GetFiles(path + ".versions")) File.WriteAllText(file, "{}\n");
        var error = Record.Exception(() =>
        {
            using var reader = new FileControlTraversalJournal(path, true);
            reader.ReadLatestVersion();
        });
        Assert.True(error is IOException or InvalidDataException or JsonException,
            error?.ToString() ?? "Invalid source was accepted");
    }

    [Fact]
    public void LatestDataIsBoundedWhileFullHistoryStillDetectsOlderPrefixCorruption()
    {
        var path = Path.Combine(_root, "control");
        using (var writer = new FileControlTraversalJournal(path, SmallTail))
            foreach (var i in Enumerable.Range(1, 25)) writer.Append(ControlRow(i));
        var lines = File.ReadAllLines(path);
        Assert.Contains("input-001", lines[0]);
        lines[0] = lines[0].Replace("input-001", "input-999", StringComparison.Ordinal);
        File.WriteAllLines(path, lines);
        using var reader = new FileControlTraversalJournal(path, true);
        Assert.Equal(25, reader.ReadLatestVersion().Sequence);
        Assert.Throws<InvalidDataException>(() => reader.ReadRecords());
    }

    [Fact]
    public void VersionRequestsOutsideTheRetainedSequenceRangeAreRejected()
    {
        var path = Path.Combine(_root, "plan");
        using var writer = new FileRuntimePlanJournal(path, SmallTail);
        writer.Append(new("uniclaw.runtime.plan.adoption.v1", 1, "fixture", new Dictionary<string, int>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.ReadVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.ReadVersion(2));
    }

    [Fact]
    public void SupportedV1FilesStillReturnOriginalRecordsAndDataVersionsWithoutRewriting()
    {
        var controlPath = Path.Combine(_root, "legacy-control");
        var planPath = Path.Combine(_root, "legacy-plan");
        var controls = Enumerable.Range(1, 3).Select(ControlRow).ToArray();
        var plans = Enumerable.Range(1, 3).Select(i => new RuntimePlanHistoryRecord("uniclaw.runtime.plan.adoption.v1",
            i, "fixture", new Dictionary<string, int> { ["original"] = i })).ToArray();
        CoreHistoryLegacyFixture.WriteControl(controlPath, "11223344556677889900112233445566", controls, _ => { });
        CoreHistoryLegacyFixture.WriteRuntimePlan(planPath, "22334455667788990011223344556677", plans, _ => { });
        var before = Digests();
        using var control = new FileControlTraversalJournal(controlPath, true);
        using var plan = new FileRuntimePlanJournal(planPath, true);
        Assert.Equal(Serialize(controls), Serialize(control.ReadRecords()));
        Assert.Equal(Serialize(plans), Serialize(plan.ReadRecords()));
        foreach (var direction in new[] { HistoryVersionDirection.Forward, HistoryVersionDirection.Reverse })
        {
            Assert.Equal(Serialize(controls[1]), Serialize(control.ReadVersion(2, direction).Record));
            Assert.Equal(Serialize(plans[1]), Serialize(plan.ReadVersion(2, direction).Record));
        }
        Assert.Equal(Serialize(controls[^1]), Serialize(control.ReadLatestVersion().Record));
        Assert.Equal(Serialize(plans[^1]), Serialize(plan.ReadLatestVersion().Record));
        Assert.Equal(before, Digests());
    }

    [Fact]
    public async Task FailedAcknowledgementDoesNotExposeVersionsThroughOriginalWriter()
    {
        var path = Path.Combine(_root, "failed-control");
        await using var writer = new FileControlTraversalJournal(path, SmallTail);
        Assert.Empty(writer.ReadRecords());
        Directory.CreateDirectory(path + ".head");
        await Assert.ThrowsAnyAsync<IOException>(() => writer.AppendAsync(ControlRow(1)).AsTask());
        Assert.Throws<IOException>(() => writer.ReadRecords());
        Assert.Throws<IOException>(() => writer.ReadLatestVersion());
        await Assert.ThrowsAsync<IOException>(() => writer.AppendAsync(ControlRow(1)).AsTask());
    }

    [Fact]
    public void ConfiguredByteThresholdCanCheckpointBeforeTheRecordCountBound()
    {
        var path = Path.Combine(_root, "byte-bound");
        using var writer = new FileControlTraversalJournal(path, new HistoryVersionOptions
        {
            MaxTailRecords = 100, MinDeltaBytesForSnapshot = 1, SnapshotByteRatio = 1
        });
        foreach (var i in Enumerable.Range(1, 25)) writer.Append(ControlRow(i));
        Assert.True(writer.VersionWriteStatistics.BaselineWrites > 1);
        Assert.Equal(writer.VersionWriteStatistics.BaselineWrites, writer.VersionWriteStatistics.BaselineFlushes);
        Assert.True(writer.VersionWriteStatistics.BaselineBytes > 0);
        Assert.Equal(Serialize(ControlRow(25)), Serialize(writer.ReadLatestVersion().Record));
    }

    [Fact]
    public void AValidBaselineFromAnotherRecordingCannotServeTheOriginalSource()
    {
        var path = Path.Combine(_root, "original");
        var foreign = Path.Combine(_root, "foreign");
        using (var writer = new FileControlTraversalJournal(path, SmallTail))
            foreach (var i in Enumerable.Range(1, 13)) writer.Append(ControlRow(i));
        using (var writer = new FileControlTraversalJournal(foreign, SmallTail))
            foreach (var i in Enumerable.Range(1, 13)) writer.Append(ControlRow(i));
        var head = JsonNode.Parse(File.ReadAllText(path + ".head"))!;
        var originalRef = head["baselines"]!.AsArray()[^1]!;
        var foreignHead = JsonNode.Parse(File.ReadAllText(foreign + ".head"))!;
        var foreignRef = foreignHead["baselines"]!.AsArray()[^1]!;
        var bytes = File.ReadAllBytes(Path.Combine(foreign + ".versions", foreignRef["fileName"]!.GetValue<string>()));
        File.WriteAllBytes(Path.Combine(path + ".versions", originalRef["fileName"]!.GetValue<string>()), bytes);
        originalRef["hash"] = Convert.ToHexString(SHA256.HashData(bytes));
        originalRef["bytes"] = bytes.Length;
        File.WriteAllText(path + ".head", head.ToJsonString() + "\n");
        using var reader = new FileControlTraversalJournal(path, true);
        Assert.Throws<InvalidDataException>(() => reader.ReadLatestVersion());
    }

    private static ControlTraversalHistoryRecord ControlRow(int i)
    {
        var state = JsonSerializer.SerializeToNode(new
        {
            stable = new string('x', 4096), current = i, empty = (string?)null,
            path = Enumerable.Range(0, i % 6).Select(n => new { id = "item-" + n, done = n < i % 3 }).ToArray(),
            map = i % 2 == 0 ? new Dictionary<string, object?> { ["b/特殊~"] = true, ["a"] = null }
                : new Dictionary<string, object?> { ["a"] = i, ["removed"] = "prior" }
        }, Json)!;
        if (i % 3 == 0) state.AsObject().Remove("empty");
        return new("uniclaw.control.traversal.v1", i, "fixture",
            JsonSerializer.SerializeToElement(new { source = $"input-{i:D3}", original = new[] { i, -i } }, Json),
            JsonSerializer.SerializeToElement(state, Json),
            JsonSerializer.SerializeToElement(new { source = "result-" + i, ok = i % 2 == 0 }, Json));
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private string[] Digests() => Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(p => Path.GetRelativePath(_root, p) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).ToArray();
}
