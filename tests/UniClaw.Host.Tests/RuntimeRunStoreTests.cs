using System.Text.Json;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RuntimeRunStoreTests
{
    [Fact]
    public void CreatePersistsProjectionAndAppendOnlyEventsAcrossStoreInstances()
    {
        var root = Path.Combine(Path.GetTempPath(), "uniclaw-run-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = JsonDocument.Parse("{\"id\":\"project-a\"}").RootElement.Clone();
            var store = new RuntimeRunStore(root);
            var run = store.Create(new RuntimeRunStore.CreateRequest(
                "session-a", null, "launch-a", "idem-a", "corr-a", project, null, project, null));

            Assert.StartsWith("run-", run.RunId);
            Assert.Equal("starting", run.Status);
            Assert.Equal(1, run.LastEventSequence);
            Assert.NotNull(store.Get(run.RunId));
            Assert.Equal(run.RunId, store.FindByIdempotencyKey("idem-a")!.RunId);

            var reloaded = new RuntimeRunStore(root);
            var page = reloaded.ReadEvents(run.RunId);
            Assert.Single(page.Events);
            Assert.Equal("run.accepted", page.Events[0].EventType);
            Assert.Equal(run.RunId, reloaded.FindByLaunchId("launch-a")!.RunId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TransitionCarriesReasonOutcomeAndSupportsSourceAndCursorQueries()
    {
        var root = Path.Combine(Path.GetTempPath(), "uniclaw-run-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new RuntimeRunStore(root);
            var run = store.Create(new RuntimeRunStore.CreateRequest(
                "session-b", null, "launch-b", "idem-b", "corr-b", null, null, null, null));
            var completed = store.Transition(run.RunId, "completed", "finalize", "run.completed", "runtime", "uniclaw-runtime", "done", p => p with
            {
                Outcome = "completion",
                Reason = "evidence recorded"
            });

            Assert.Equal("completed", completed.Status);
            Assert.Equal("completion", completed.Outcome);
            Assert.Equal("evidence recorded", completed.Reason);
            Assert.NotNull(completed.EndedAt);

            var firstPage = store.ReadEvents(run.RunId, source: "runtime", limit: 1);
            Assert.Single(firstPage.Events);
            Assert.Equal("run.accepted", firstPage.Events[0].EventType);
            Assert.NotNull(firstPage.NextCursor);
            var secondPage = store.ReadEvents(run.RunId, source: "runtime", cursor: firstPage.NextCursor);
            Assert.Single(secondPage.Events);
            Assert.Equal("run.completed", secondPage.Events[0].EventType);
            Assert.Null(secondPage.NextCursor);
            Assert.Throws<InvalidOperationException>(() => store.Transition(run.RunId, "failed", "failed", "run.failed", "runtime", "uniclaw-runtime", "late"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidCursorIsRejectedAndPathTraversalCannotReadAProjection()
    {
        var root = Path.Combine(Path.GetTempPath(), "uniclaw-run-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new RuntimeRunStore(root);
            var run = store.Create(new RuntimeRunStore.CreateRequest(
                "session-c", null, "launch-c", "idem-c", "corr-c", null, null, null, null));
            Assert.Throws<FormatException>(() => store.ReadEvents(run.RunId, cursor: "not-a-cursor"));
            Assert.Null(store.Get("../outside"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ListFiltersBySessionAndUsesOpaqueStableCursor()
    {
        var root = Path.Combine(Path.GetTempPath(), "uniclaw-run-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new RuntimeRunStore(root);
            var first = store.Create(new RuntimeRunStore.CreateRequest("session-list", null, "launch-list-1", "idem-list-1", "corr-1", null, null, null, null), DateTimeOffset.Parse("2026-10-04T06:00:00Z"));
            var second = store.Create(new RuntimeRunStore.CreateRequest("session-list", null, "launch-list-2", "idem-list-2", "corr-2", null, null, null, null), DateTimeOffset.Parse("2026-10-04T06:01:00Z"));
            store.Create(new RuntimeRunStore.CreateRequest("other-session", null, "launch-list-3", "idem-list-3", "corr-3", null, null, null, null), DateTimeOffset.Parse("2026-10-04T06:02:00Z"));

            var page = store.List(productSessionId: "session-list", limit: 1);
            Assert.Single(page.Runs);
            Assert.Equal(second.RunId, page.Runs[0].RunId);
            Assert.NotNull(page.NextCursor);
            var next = store.List(productSessionId: "session-list", cursor: page.NextCursor, limit: 1);
            Assert.Single(next.Runs);
            Assert.Equal(first.RunId, next.Runs[0].RunId);
            Assert.Null(next.NextCursor);
            Assert.Throws<FormatException>(() => store.List(cursor: "invalid"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
