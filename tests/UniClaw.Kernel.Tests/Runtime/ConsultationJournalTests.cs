using Xunit;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class ConsultationJournalTests
{
    private static ConsultationKey Key(string correlation = "decision-r-1") =>
        new("run-r", "view-r", "run-r:boundary-1", correlation);

    private static AgentDecision Decision(string id = "decision-r-1") =>
        new AgentDecision.NoAction(new AgentNoActionProposal(id, "journal-test"));

    [Fact]
    public void ReserveComplete_ReplaysPolymorphicDecision_AndDoesNotRecount()
    {
        var journal = new InMemoryConsultationJournal();
        var key = Key();
        var pending = journal.Reserve(key, "fp-1");
        Assert.Equal(ConsultationJournalStatus.Pending, pending.Status);
        var completed = journal.Complete(key, "fp-1", Decision());

        var replay = journal.Reserve(key, "fp-1");
        Assert.Equal(ConsultationJournalStatus.Completed, replay.Status);
        Assert.Equal(completed.Decision, replay.Decision);
        Assert.Equal(1, journal.ConsumedCount("run-r", "view-r"));
        Assert.False(journal.HasPending("run-r", "view-r"));
    }

    [Fact]
    public void SameCorrelation_ChangedPayloadIsRejected()
    {
        var journal = new InMemoryConsultationJournal();
        var key = Key();
        journal.Reserve(key, "fp-1");
        Assert.Throws<ConsultationJournalConflictException>(
            () => journal.Reserve(key, "fp-changed"));
        Assert.Throws<ConsultationJournalConflictException>(
            () => journal.Complete(key, "fp-changed", Decision()));
        Assert.True(journal.HasPending("run-r", "view-r"));
    }

    [Fact]
    public void RejectedTimeout_ConsumesOnce_AndPendingRemainsFailClosed()
    {
        var journal = new InMemoryConsultationJournal();
        var timeoutKey = Key();
        journal.Reserve(timeoutKey, "fp-timeout");
        var rejected = journal.Reject(timeoutKey, "fp-timeout", "timeout");
        Assert.Equal(ConsultationJournalStatus.Rejected, rejected.Status);
        Assert.Equal("timeout", rejected.RejectionReason);
        Assert.Equal(1, journal.ConsumedCount("run-r", "view-r"));

        var crashKey = Key("decision-r-2");
        journal.Reserve(crashKey, "fp-crash");
        Assert.True(journal.HasPending("run-r", "view-r"));
        Assert.Equal(1, journal.ConsumedCount("run-r", "view-r"));
    }
}
