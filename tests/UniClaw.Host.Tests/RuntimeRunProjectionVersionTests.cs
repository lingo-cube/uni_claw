using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

public sealed class RuntimeRunProjectionVersionTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("runtime-projection-version-").FullName;
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
    public void Dispose() => Directory.Delete(_root, true);
    private static RuntimeRunStore.CreateRequest Request() => new("session-original", "task-original", "launch-original",
        "idem-original", "correlation-original",
        JsonSerializer.SerializeToElement(new { id = "project-original", name = "项目+原始" }),
        JsonSerializer.SerializeToElement(new { id = "set-original", cases = new[] { "a", "b" } }),
        JsonSerializer.SerializeToElement(new { id = "definition-original", version = 7 }),
        JsonSerializer.SerializeToElement(new { device = "fixture-only", flags = new object?[] { true, null, "a/b~c" } }),
        "accepted-view-original", 1);

    [Fact]
    public void CorruptCoreSnapshotIsUnavailableRatherThanAnAbsentRun()
    {
        var store = new RuntimeRunStore(_root);
        var run = store.Create(Request(), Time);
        File.WriteAllText(Path.Combine(_root, run.RunId + ".snapshot.json"), "{");
        RuntimeRunStore.RuntimeRunProjection? observed = null;
        var error = Record.Exception(() => observed = store.Get(run.RunId));
        output.WriteLine($"Original run={run.RunId}; Get returned={observed?.RunId ?? "<null>"}; error={error?.GetType().Name ?? "<none>"}");
        Assert.True(error is IOException or InvalidDataException or JsonException, "Corrupt stored identity was treated as absent");
    }

    [Fact]
    public void CorruptIdempotencySourceNeverMintsASecondRunForTheSameLaunch()
    {
        var store = new RuntimeRunStore(_root);
        var run = store.Create(Request(), Time);
        File.WriteAllText(Path.Combine(_root, run.RunId + ".snapshot.json"), "{");
        RuntimeRunStore.RuntimeRunProjection? observed = null;
        var error = Record.Exception(() => observed = new RuntimeRunStore(_root).Create(Request(), Time));
        var snapshots = Directory.GetFiles(_root, "*.snapshot.json");
        output.WriteLine($"Original run={run.RunId}; repeated Create returned={observed?.RunId ?? "<none>"}; snapshot count={snapshots.Length}; error={error?.GetType().Name ?? "<none>"}");
        Assert.True(error is IOException or InvalidDataException or JsonException, "Same launch minted a new identity after its stored source was corrupted");
        Assert.Single(snapshots);
    }

    [Fact]
    public void EveryRetainedVersionPreservesIdentityBindingOutcomeAndArtifactsInBothDirections()
    {
        var store = new RuntimeRunStore(_root);
        var created = store.Create(Request(), Time);
        var running = store.Transition(created.RunId, "running", "execute", "run.started", "dsh", "dsh-host", "attached",
            p => p with { HostSessionRef = new("dsh", "original-host-session") }, Time.AddSeconds(1));
        var terminal = store.Transition(created.RunId, "completed", "finalize", "run.completed", "runtime", "uniclaw-runtime", "finished",
            p => p with { Outcome = "completion", Reason = "original-result", Artifacts = [new("report", "report", "partial")] }, Time.AddSeconds(2));
        var delivered = store.UpdateArtifacts(created.RunId, [new("report", "report", "present", "report-deliveries/original")], Time.AddSeconds(3));
        var expected = new[] { created, running, terminal, delivered };
        var reader = new RuntimeRunStore(_root);
        foreach (var projection in expected)
            foreach (var direction in new[] { HistoryVersionDirection.Forward, HistoryVersionDirection.Reverse })
            {
                var version = reader.ReadProjectionVersion(created.RunId, projection.Revision, direction);
                Assert.Equal(Serialize(projection), Serialize(version.Projection));
                Assert.Equal(projection.Revision, version.Revision);
                Assert.Equal(1, version.EarliestRevision);
                Assert.True(version.CompleteHistory);
            }
        var latest = reader.ReadProjectionVersion(created.RunId);
        Assert.Equal(Serialize(delivered), Serialize(latest.Projection));
        Assert.InRange(latest.ReadCost, 0, 1);
        Assert.Equal("completion", latest.Projection.Outcome);
        Assert.Equal("original-result", latest.Projection.Reason);
        Assert.Equal(created.RunId, reader.FindByIdempotencyKey("idem-original")!.RunId);
        Assert.Equal(new[] { "run.accepted", "run.started", "run.completed", "run.artifacts-updated" },
            reader.ReadEvents(created.RunId).Events.Select(e => e.EventType));
        var page = reader.ReadEvents(created.RunId, source: "runtime", limit: 1);
        Assert.Single(page.Events);
        Assert.Equal("run.accepted", page.Events[0].EventType);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(new[] { "run.completed", "run.artifacts-updated" }, reader.ReadEvents(created.RunId, "runtime", page.NextCursor).Events.Select(e => e.EventType));
    }

    [Fact]
    public void LegacyLatestRemainsReadableAndAnUpdateAnchorsOnlyTheKnownRetainedRange()
    {
        var legacy = LegacyProjection(7);
        var legacyEvents = Enumerable.Range(1, 7).Select(i => new RuntimeRunStore.RuntimeRunEvent("uniclaw.workspace.runtime-run-event.v1",
            "uniclaw.workspace.contract.v1", "legacy-event-" + i, legacy.RunId, "legacy.event", "runtime", "uniclaw-runtime", Time.AddSeconds(i),
            Time.AddSeconds(i), i, "original legacy event " + i, PayloadAvailable: false)).ToArray();
        var snapshot = Path.Combine(_root, legacy.RunId + ".snapshot.json");
        var events = Path.Combine(_root, legacy.RunId + ".events.ndjson");
        File.WriteAllText(snapshot, Serialize(legacy));
        File.WriteAllLines(events, legacyEvents.Select(Serialize));
        var snapshotBefore = File.ReadAllBytes(snapshot);
        var eventsBefore = File.ReadAllBytes(events);
        var store = new RuntimeRunStore(_root);
        Assert.Equal(Serialize(legacy), Serialize(store.Get(legacy.RunId)));
        var oldLatest = store.ReadProjectionVersion(legacy.RunId);
        Assert.False(oldLatest.CompleteHistory);
        Assert.Equal(7, oldLatest.EarliestRevision);
        Assert.Equal(snapshotBefore, File.ReadAllBytes(snapshot));
        Assert.Equal(eventsBefore, File.ReadAllBytes(events));
        var terminal = store.Transition(legacy.RunId, "failed", "attach", "run.failed", "runtime", "uniclaw-runtime", "original failure",
            p => p with { Outcome = "failure", Reason = "known legacy attach failure" }, Time.AddSeconds(8));
        var reader = new RuntimeRunStore(_root);
        foreach (var direction in new[] { HistoryVersionDirection.Forward, HistoryVersionDirection.Reverse })
        {
            var baseline = reader.ReadProjectionVersion(legacy.RunId, 7, direction);
            Assert.False(baseline.CompleteHistory);
            Assert.Equal(7, baseline.EarliestRevision);
            Assert.Equal(Serialize(legacy), Serialize(baseline.Projection));
            Assert.Equal(Serialize(terminal), Serialize(reader.ReadProjectionVersion(legacy.RunId, 8, direction).Projection));
        }
        Assert.Throws<InvalidOperationException>(() => reader.ReadProjectionVersion(legacy.RunId, 6));
        Assert.Equal(legacyEvents.Select(e => e.EventId), reader.ReadEvents(legacy.RunId).Events.Take(7).Select(e => e.EventId));
        Assert.Equal(eventsBefore, File.ReadAllBytes(events));
    }

    [Fact]
    public void ExistingVersionRecordsCannotBeChangedWithoutFullHistoryReadersRejectingThem()
    {
        var store = new RuntimeRunStore(_root);
        var run = store.Create(Request(), Time);
        store.Transition(run.RunId, "completed", "finalize", "run.completed", "runtime", "uniclaw-runtime", "done", observedAt: Time.AddSeconds(1));
        var frame = Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".events.ndjson", StringComparison.Ordinal) && !p.EndsWith(".snapshot.json", StringComparison.Ordinal)
                && File.ReadAllText(p).Contains("run.accepted", StringComparison.Ordinal)));
        File.WriteAllText(frame, File.ReadAllText(frame).Replace("run.accepted", "run.rejected", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => new RuntimeRunStore(_root).ReadProjectionVersion(run.RunId, 1));
        Assert.Throws<InvalidDataException>(() => new RuntimeRunStore(_root).ReadEvents(run.RunId));
    }

    [Fact]
    public void ARejectedMutationCallbackDoesNotPublishAnEventOrPoisonTheUnchangedRun()
    {
        var store = new RuntimeRunStore(_root);
        var run = store.Create(Request(), Time);
        Assert.Throws<InvalidOperationException>(() => store.Transition(run.RunId, "running", "execute", "run.started", "runtime", "uniclaw-runtime", "rejected",
            _ => throw new InvalidOperationException("known invalid callback"), Time.AddSeconds(1)));
        Assert.Equal(Serialize(run), Serialize(store.Get(run.RunId)));
        Assert.Single(store.ReadEvents(run.RunId).Events);
        Assert.Equal(2, store.Transition(run.RunId, "failed", "admission", "run.failed", "runtime", "uniclaw-runtime", "explicit terminal",
            observedAt: Time.AddSeconds(2)).Revision);
    }

    [Fact]
    public void LatestSnapshotValuesMustMatchTheirConfirmedVersion()
    {
        var store = new RuntimeRunStore(_root);
        var run = store.Create(Request(), Time);
        store.Transition(run.RunId, "completed", "finalize", "run.completed", "runtime", "uniclaw-runtime", "done", observedAt: Time.AddSeconds(1));
        var path = Path.Combine(_root, run.RunId + ".snapshot.json");
        var snapshot = JsonNode.Parse(File.ReadAllText(path))!;
        snapshot["projection"]!["outcome"] = "invented-outcome";
        File.WriteAllText(path, snapshot.ToJsonString());
        Assert.Throws<InvalidDataException>(() => new RuntimeRunStore(_root).Get(run.RunId));
    }

    [Fact]
    public void UnconfirmedRevisionSuffixCannotBecomeLatestOrDisappearBehindTheOldSnapshot()
    {
        var store = new RuntimeRunStore(_root);
        var run = store.Create(Request(), Time);
        var directory = Path.Combine(_root, run.RunId + ".revisions");
        var current = Path.Combine(directory, run.Revision.ToString("D20") + ".revision.json");
        var unconfirmed = Path.Combine(directory, (run.Revision + 1).ToString("D20") + ".revision.json");
        File.Copy(current, unconfirmed);
        Assert.Throws<InvalidDataException>(() => new RuntimeRunStore(_root).Get(run.RunId));
        Assert.Throws<InvalidDataException>(() => new RuntimeRunStore(_root).ReadEvents(run.RunId));
    }

    [Fact]
    public void ConfiguredByteLimitRejectsAnOversizedIdentityBeforePublishingIt()
    {
        var request = Request() with { ProjectRef = JsonSerializer.SerializeToElement(new { data = new string('x', 32768) }) };
        var store = new RuntimeRunStore(_root, 4096);
        Assert.Throws<InvalidDataException>(() => store.Create(request, Time));
        Assert.Empty(Directory.GetFiles(_root, "*.snapshot.json"));
        Assert.Null(store.FindByIdempotencyKey(request.IdempotencyKey));
        Assert.Throws<IOException>(() => store.Create(Request(), Time));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevisionExhaustionIsRejectedBeforeAnyNewVersionIsPublished(bool artifacts)
    {
        var legacy = LegacyProjection(long.MaxValue);
        var path = Path.Combine(_root, legacy.RunId + ".snapshot.json");
        File.WriteAllText(path, Serialize(legacy));
        var before = File.ReadAllBytes(path);
        var store = new RuntimeRunStore(_root);
        var error = Record.Exception(() =>
        {
            if (artifacts) store.UpdateArtifacts(legacy.RunId, [], Time.AddSeconds(8));
            else store.Transition(legacy.RunId, "failed", "admission", "run.failed", "runtime", "uniclaw-runtime", "version exhausted", observedAt: Time.AddSeconds(8));
        });
        Assert.IsType<OverflowException>(error);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(long.MaxValue, store.Get(legacy.RunId)!.Revision);
        Assert.False(Directory.Exists(Path.Combine(_root, legacy.RunId + ".revisions")));
    }

    private static RuntimeRunStore.RuntimeRunProjection LegacyProjection(long revision) => new(
        "uniclaw.workspace.runtime-run-projection.v1", "uniclaw.workspace.contract.v1", "run-legacy", "session-legacy", "task-legacy",
        new("dsh", "legacy-host"), null, null, null, "launch-legacy", "corr-legacy", null, "running", "execute", Time, null, null, null,
        revision, Time.AddSeconds(7), revision, "current", [], "idem-legacy", "view-legacy", 3);

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
