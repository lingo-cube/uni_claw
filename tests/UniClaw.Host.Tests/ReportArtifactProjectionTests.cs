using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class ReportArtifactProjectionTests
{
    [Fact]
    public void TerminalRunCanReceiveArtifactsAndReopenWithoutChangingIdentityOrOutcome()
    {
        var root = Path.Combine(Path.GetTempPath(), "uniclaw-report-artifacts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var observedAt = DateTimeOffset.Parse("2026-10-25T01:02:03Z");
            var artifactAt = DateTimeOffset.Parse("2026-10-25T01:03:04Z");
            var store = new RuntimeRunStore(root);
            var created = store.Create(new RuntimeRunStore.CreateRequest(
                "session-report", "task-report", "launch-report", "idem-report", "corr-report", null, null, null, null), observedAt);
            var terminal = store.Transition(created.RunId, "completed", "finalize", "run.completed", "runtime", "uniclaw-runtime", "done", p => p with
            {
                Outcome = "completion",
                Reason = "report pending"
            }, observedAt);
            var artifact = new RuntimeRunStore.RuntimeArtifactRef("artifact-report", "report", "present", "report-deliveries/report-test");

            var updated = store.UpdateArtifacts(terminal.RunId, new[] { artifact }, artifactAt);

            Assert.Equal(new[] { artifact }, updated.Artifacts);
            Assert.Equal(terminal.Revision + 1, updated.Revision);
            Assert.Equal(artifactAt, updated.ObservedAt);
            Assert.Equal(terminal.LastEventSequence + 1, updated.LastEventSequence);
            Assert.Equal("current", updated.Consistency);
            Assert.Equal(terminal.RunId, updated.RunId);
            Assert.Equal(terminal.ProductSessionId, updated.ProductSessionId);
            Assert.Equal(terminal.TaskInstanceId, updated.TaskInstanceId);
            Assert.Equal(terminal.LaunchId, updated.LaunchId);
            Assert.Equal(terminal.CorrelationId, updated.CorrelationId);
            Assert.Equal(terminal.Status, updated.Status);
            Assert.Equal(terminal.Phase, updated.Phase);
            Assert.Equal(terminal.StartedAt, updated.StartedAt);
            Assert.Equal(terminal.EndedAt, updated.EndedAt);
            Assert.Equal(terminal.Outcome, updated.Outcome);
            Assert.Equal(terminal.Reason, updated.Reason);

            var replacement = new RuntimeRunStore.RuntimeArtifactRef("artifact-replacement", "report", "present", "report-deliveries/report-test-2");
            var callerArtifacts = new[] { replacement };
            var updatedAgain = store.UpdateArtifacts(terminal.RunId, callerArtifacts, artifactAt.AddSeconds(1));
            callerArtifacts[0] = artifact;
            Assert.Equal(artifact, updated.Artifacts.Single());
            Assert.Equal(replacement, updatedAgain.Artifacts.Single());

            var reopened = new RuntimeRunStore(root);
            var persisted = reopened.Get(terminal.RunId)!;
            Assert.Equal(updatedAgain.RunId, persisted.RunId);
            Assert.Equal(updatedAgain.Status, persisted.Status);
            Assert.Equal(updatedAgain.Phase, persisted.Phase);
            Assert.Equal(updatedAgain.EndedAt, persisted.EndedAt);
            Assert.Equal(updatedAgain.Outcome, persisted.Outcome);
            Assert.Equal(updatedAgain.Reason, persisted.Reason);
            Assert.Equal(updatedAgain.Revision, persisted.Revision);
            Assert.Equal(updatedAgain.ObservedAt, persisted.ObservedAt);
            Assert.Equal(updatedAgain.LastEventSequence, persisted.LastEventSequence);
            Assert.Equal(updatedAgain.Consistency, persisted.Consistency);
            Assert.Equal(updatedAgain.Artifacts, persisted.Artifacts);
            var events = reopened.ReadEvents(terminal.RunId).Events;
            Assert.Equal("run.artifacts-updated", events[^1].EventType);
            Assert.Equal("runtime", events[^1].Source);
            Assert.Equal("uniclaw-runtime", events[^1].Authority);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
