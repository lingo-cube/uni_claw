using System.Collections.Immutable;
using Xunit;
using UniClaw.Runtime.Harness.Capture;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Harness;

public sealed class TraceCaptureFoundationTests
{
    [Fact]
    public async Task SC_TC_001_AlreadyOnCaptureHasObservationAndNoSetSwitch()
    {
        var session = Started("tc-001");
        var environment = new CapturingEnvironment(new FakeEnvironment([
            new Observation([new ObservedElement("Wi-Fi", true, 4)], "Settings", 1)],
            new ActionResult(ActionResultOutcome.Dispatched, "none", null)), session);

        await environment.ObserveAsync(default);
        var result = await session.FinalizeAndPersistAsync(new InMemoryTraceCaptureStore(), true, "Completed");

        Assert.True(result.CaptureSucceeded);
        Assert.Contains(result.Bundle.Records, x => x.Kind == CaptureRecordKind.Observation);
        Assert.DoesNotContain(result.Bundle.Records, x => x.ActionKind == nameof(DeviceAction.SetSwitch));
    }

    [Fact]
    public async Task SC_TC_002_CapturePreservesObservationActionResultOrderAndFrameSequence()
    {
        var session = Started("tc-002");
        var first = new Observation([new ObservedElement("Wi-Fi", false, 2)], "Settings", 10);
        var fresh = new Observation([new ObservedElement("Wi-Fi", true, 2)], "Settings", 11);
        var inner = new FakeEnvironment([first, fresh], new ActionResult(ActionResultOutcome.Dispatched, "SetSwitch", "ok"));
        var environment = new CapturingEnvironment(inner, session, observation => $"frame-{observation.SequenceNumber}");

        await environment.ObserveAsync(default);
        await environment.ExecuteAsync(new DeviceAction.SetSwitch(2, true), default);
        await environment.ObserveAsync(default);
        var bundle = session.Finalize(true, "Completed");
        var records = bundle.Records;

        Assert.Equal(new[] { CaptureRecordKind.Observation, CaptureRecordKind.ActionDispatch,
            CaptureRecordKind.ActionResult, CaptureRecordKind.Observation }, records.Select(x => x.Kind));
        Assert.Equal(new long[] { 10, 0, 0, 11 }, records.Select(x => x.SequenceNumber));
        Assert.Equal(new[] { "frame-10", "frame-11" },
            records.Where(x => x.Kind == CaptureRecordKind.Observation).Select(x => x.FrameId));
        Assert.Equal(1, inner.DispatchCount);
    }

    [Fact]
    public async Task SC_TC_003_CaptureStoreFailureDoesNotChangeInnerResultOrDispatchCount()
    {
        var session = Started("tc-003");
        var inner = new FakeEnvironment([new Observation([], "Settings", 1)],
            new ActionResult(ActionResultOutcome.Rejected, "tap", "blocked"));
        var environment = new CapturingEnvironment(inner, session);
        var expected = await environment.ExecuteAsync(new DeviceAction.Tap(1), default);
        var store = new FailingStore();
        var result = await session.FinalizeAndPersistAsync(store, false, "Failed");

        Assert.Equal(ActionResultOutcome.Rejected, expected.Outcome);
        Assert.Equal(1, inner.DispatchCount);
        Assert.False(result.CaptureSucceeded);
        Assert.Empty(store.PublishedIds);
    }

    [Fact]
    public async Task SC_TC_004_RuntimeFailureRemainsSeparateFromSuccessfulCapture()
    {
        var session = Started("tc-004");
        var store = new InMemoryTraceCaptureStore();
        var result = await session.FinalizeAndPersistAsync(store, false, "Failed");

        Assert.True(result.CaptureSucceeded);
        Assert.False(result.RuntimeSucceeded);
        Assert.Equal("Failed", result.RuntimeOutcome);
        Assert.Equal(CaptureState.Persisted, result.CaptureState);
        Assert.Equal(CaptureState.Persisted, result.Bundle.FinalState);
        Assert.Equal(CaptureState.Persisted, store.Bundles["tc-004"].FinalState);
    }

    [Fact]
    public void RuntimeTraceSnapshotIsPreservedStructurallyWithoutInferredEvents()
    {
        var session = Started("trace-snapshot");
        var trace = new TraceRun
        {
            TraceRunId = "trace-run",
            TraceId = "trace",
            RunId = "run",
            Spans = [],
        };

        var bundle = session.Finalize(true, "Completed", observabilityTrace: trace);

        Assert.Same(trace, bundle.ObservabilityTrace);
        Assert.Empty(bundle.ObservabilityTrace!.Spans);
        Assert.Equal("trace", bundle.TraceId);
    }

    [Fact]
    public async Task InMemoryStoreIsAppendOnlyOnCollision()
    {
        var store = new InMemoryTraceCaptureStore();
        var first = Bundle("collision");
        Assert.True((await store.SaveAsync(first)).Success);
        var second = await store.SaveAsync(first with { RuntimeOutcome = "different" });
        Assert.False(second.Success);
        Assert.NotEqual("different", store.Bundles["collision"].RuntimeOutcome);
    }

    [Fact]
    public async Task FileStoreRejectsMalformedHashAndUnsafeId()
    {
        var root = Temp();
        try
        {
            var store = new FileTraceCaptureStore(root);
            var badHash = Bundle("safe") with { Artifacts = [Artifact("a", "bad") with { ContentHash = "sha256:bad" }] };
            Assert.False((await store.SaveAsync(badHash)).Success);
            Assert.False(Directory.Exists(Path.Combine(root, "safe")));
            Assert.Empty(Directory.GetDirectories(root, ".staging-*"));
            Assert.False((await store.SaveAsync(Bundle("../escape"))).Success);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FileStorePublishesArtifactBytesAndChecksumAndRejectsCollision()
    {
        var root = Temp();
        try
        {
            var store = new FileTraceCaptureStore(root);
            var bundle = Bundle("artifact") with { Artifacts = [Artifact("a", "hello")] };
            var saved = await store.SaveAsync(bundle);
            Assert.True(saved.Success);
            Assert.Equal("hello", File.ReadAllText(Path.Combine(root, "artifact", "artifacts", "a.bin")));
            Assert.Contains(bundle.Artifacts[0].ContentHash!, File.ReadAllText(Path.Combine(root, "artifact", "checksums.sha256")));
            Assert.False((await store.SaveAsync(bundle)).Success);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FileStoreCancellationLeavesNoStagingDirectory()
    {
        var root = Temp();
        try
        {
            var store = new FileTraceCaptureStore(root);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await store.SaveAsync(Bundle("cancel"), cts.Token));
            Assert.Empty(Directory.GetDirectories(root, ".staging-*"));
            Assert.False(Directory.Exists(Path.Combine(root, "cancel")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FileStoreValidationFailureLeavesNoPartialPublication()
    {
        var root = Temp();
        try
        {
            var store = new FileTraceCaptureStore(root);
            var invalid = Bundle("invalid") with
            {
                Records = [new CaptureRecord { Order = 2, Kind = CaptureRecordKind.Observation }]
            };
            var result = await store.SaveAsync(invalid);
            Assert.False(result.Success);
            Assert.False(Directory.Exists(Path.Combine(root, "invalid")));
            Assert.Empty(Directory.GetDirectories(root, ".staging-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static TraceCaptureSession Started(string id) { var s = new TraceCaptureSession(id); s.Begin("trace"); return s; }
    private static TraceCaptureBundle Bundle(string id) => new() { CaptureSessionId = id, Records = [], Artifacts = [] };
    private static CaptureArtifact Artifact(string id, string text)
    {
        var bytes = text.Select(c => (byte)c).ToImmutableArray();
        return new CaptureArtifact { ArtifactId = id, FileName = id + ".bin", Content = bytes, ByteCount = bytes.Length, ContentHash = TraceCaptureSession.ComputeHash(bytes.AsSpan()) };
    }
    private static string Temp() => Directory.CreateTempSubdirectory("trace-capture-tests-").FullName;

    private sealed class FakeEnvironment(IReadOnlyList<Observation> observations, ActionResult result) : UniClaw.Runtime.Environment.IEnvironment
    {
        public int DispatchCount { get; private set; }
        private int _observationIndex;
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var index = Math.Min(_observationIndex++, observations.Count - 1);
            return Task.FromResult(observations[index]);
        }
        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken) { DispatchCount++; return Task.FromResult(result); }
    }
    private sealed class FailingStore : ITraceCaptureStore
    {
        public List<string> PublishedIds { get; } = [];
        public ValueTask<TraceCapturePersistenceResult> SaveAsync(TraceCaptureBundle bundle, CancellationToken cancellationToken = default) => ValueTask.FromResult(TraceCapturePersistenceResult.Fail("injected"));
    }
}
