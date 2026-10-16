using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class FileConsultationJournalTests
{
    [Fact]
    public void CompletedAndRejectedEntries_ReloadWithoutRecountingRetries()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uniclaw-consultation-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "journal.jsonl");
        var completedKey = new ConsultationKey("run-1", "view-1", "boundary-1", "decision-1");
        var rejectedKey = new ConsultationKey("run-1", "view-1", "boundary-2", "decision-2");
        try
        {
            using (var journal = new FileConsultationJournal(path))
            {
                var reserved = journal.Reserve(completedKey, "fp-1");
                journal.Complete(completedKey, "fp-1", new AgentDecision.NoAction(
                    new AgentNoActionProposal("decision-1", "done")));
                journal.Reserve(rejectedKey, "fp-2");
                journal.Reject(rejectedKey, "fp-2", "no-response");
                Assert.Equal(2, journal.ConsumedCount("run-1", "view-1"));
                Assert.Equal(ConsultationJournalStatus.Completed,
                    journal.Reserve(completedKey, "fp-1").Status);
                Assert.Equal(1, reserved.ConsumedOrdinal);
            }

            using var restored = new FileConsultationJournal(path);
            Assert.Equal(2, restored.ConsumedCount("run-1", "view-1"));
            Assert.Equal("no-response", restored.Find(rejectedKey)!.RejectionReason);
            Assert.Equal(ConsultationJournalStatus.Completed,
                restored.Find(completedKey)!.Status);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SkipDecision_RoundTripsAsZeroEffectDecision()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uniclaw-consultation-skip-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "journal.jsonl");
        var key = new ConsultationKey("run-skip", "view-1", "boundary-1", "decision-skip-1");
        try
        {
            using (var journal = new FileConsultationJournal(path))
            {
                journal.Reserve(key, "fp-skip");
                journal.Complete(key, "fp-skip", new AgentDecision.Skip(
                    "decision-skip-1", "Wi-Fi name", "unsupported dialog", ["evidence:dialog"]));
            }

            using var restored = new FileConsultationJournal(path);
            var skip = Assert.IsType<AgentDecision.Skip>(restored.Find(key)!.Decision);
            Assert.Equal("Wi-Fi name", skip.Target);
            Assert.Equal("unsupported dialog", skip.Reason);
            Assert.Equal(["evidence:dialog"], skip.EvidenceRefs);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
