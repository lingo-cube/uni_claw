using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class FileConsultationJournalTests
{
    [Fact]
    public void DynamicPlanAndExplicitContinuation_ReloadWithoutLosingPositionOrBody()
    {
        var directory = Directory.CreateTempSubdirectory("plan-journal-").FullName;
        var path = Path.Combine(directory, "consultations.jsonl");
        var planKey = new ConsultationKey("run-plan", "view-1", "boundary-1", "decision-1");
        var resumeKey = new ConsultationKey("run-plan", "view-1", "boundary-2", "decision-2");
        var position = new PlanExecutionPosition("plan-A", 1, 0, 2, 1, "entry-C", 7);
        try
        {
            using (var journal = new FileConsultationJournal(path))
            {
                journal.Reserve(planKey, "fp-plan");
                journal.Complete(planKey, "fp-plan", new AgentDecision.Plan("decision-1", new AgentPlanProposal(
                    new PlanItem[] { new PlanItem.ForEachItem("records.pending", new PlanItem[]
                    { new PlanItem.ActItem("$candidate", "$candidate", "tap", null) }, 3, true) },
                    "inspect", "plan-A", 1, "record inspection", 20)));
                journal.Reserve(resumeKey, "fp-resume");
                journal.Complete(resumeKey, "fp-resume", new AgentDecision.ContinuePlan("decision-2", position));
            }
            using var restored = new FileConsultationJournal(path);
            var plan = Assert.IsType<AgentDecision.Plan>(restored.Find(planKey)!.Decision);
            var loop = Assert.IsType<PlanItem.ForEachItem>(Assert.Single(plan.Proposal.Items));
            Assert.Equal("records.pending", loop.Source);
            Assert.True(loop.ContinueOnBlocked);
            Assert.IsType<PlanItem.ActItem>(Assert.Single(loop.Body));
            var resume = Assert.IsType<AgentDecision.ContinuePlan>(restored.Find(resumeKey)!.Decision);
            Assert.Equal(position, resume.Position);
            Assert.Equal(2, restored.ConsumedCount("run-plan", "view-1"));
        }
        finally { Directory.Delete(directory, true); }
    }

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

    [Fact]
    public void ReviewResponse_ReloadsWithDecision_AndDoesNotMoveIntoLegacyProposal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uniclaw-consultation-review-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "journal.jsonl");
        var key = new ConsultationKey("run-review", "view-1", "boundary-1", "decision-review-1");
        var response = new BudgetReviewResponse("review-1", BudgetReviewOutcome.RequestRenewal,
            new AgentProgressAssessment("overview-1", "progressing", "inspect the next known menu",
                "record its visible state", ["visible state"], ["obs:1"]),
            new BudgetRenewalRequest(20, 0, "one known branch remains"));
        try
        {
            using (var journal = new FileConsultationJournal(path))
            {
                journal.Reserve(key, "fp-review");
                journal.Complete(key, "fp-review", new AgentDecision.Act(
                    new AgentActionProposal("decision-review-1", [new AgentActionStep("button", "Apps", "tap", null)], null),
                    response));
            }

            using var restored = new FileConsultationJournal(path);
            var act = Assert.IsType<AgentDecision.Act>(restored.Find(key)!.Decision);
            Assert.Equal(response.ReviewId, act.ReviewResponse?.ReviewId);
            Assert.Equal(response.Outcome, act.ReviewResponse?.Outcome);
            Assert.Equal(response.Renewal, act.ReviewResponse?.Renewal);
            Assert.Equal(response.Assessment?.OverviewId, act.ReviewResponse?.Assessment?.OverviewId);
            Assert.Equal(response.Assessment?.Judgment, act.ReviewResponse?.Assessment?.Judgment);
            Assert.Equal(response.Assessment?.ExpectedObservableEvidence,
                act.ReviewResponse?.Assessment?.ExpectedObservableEvidence);
            Assert.Equal(response.Assessment?.EvidenceRefs, act.ReviewResponse?.Assessment?.EvidenceRefs);
            Assert.Null(act.Proposal.Renewal);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
