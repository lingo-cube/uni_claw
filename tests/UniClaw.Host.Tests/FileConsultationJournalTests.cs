using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class FileConsultationJournalTests
{
    [Fact]
    public void TraversePlan_ReloadsThroughPublicJournalWithoutReorderingRulesOrHandlers()
    {
        var directory = Directory.CreateTempSubdirectory("traverse-journal-").FullName;
        var path = Path.Combine(directory, "consultations.jsonl");
        var key = new ConsultationKey("run-traverse", "view-1", "boundary-1", "decision-traverse-1");
        var firstBefore = new PlanLanguageHandler("before-first", "runtime.language-inspector", "1.0", "en",
            Required: false, RequirePass: true);
        var secondBefore = new PlanLanguageHandler("before-second", "runtime.language-inspector", "1.0", "fr",
            Required: true, RequirePass: false);
        var firstAfter = new PlanLanguageHandler("after-first", "runtime.language-inspector", "1.0", "en",
            Required: true, RequirePass: false);
        var secondAfter = new PlanLanguageHandler("after-second", "runtime.language-inspector", "1.0", "de",
            Required: false, RequirePass: true);
        var readFirst = new PlanLanguageHandler("readonly-first", "runtime.language-inspector", "1.0", "zh",
            Required: true, RequirePass: true);
        var readSecond = new PlanLanguageHandler("readonly-second", "runtime.language-inspector", "1.0", "ja",
            Required: false, RequirePass: false);
        var traverse = new PlanItem.TraverseItem(
            "settings.menu",
            new TraversalRule[]
            {
                new("navigate-first", "navigation", new TraversalOperation(
                    "open-candidate", TraversalOperationKind.Navigate,
                    new AgentActionStep("$candidate", "$candidate", "tap", null),
                    new[] { firstBefore, secondBefore }, new[] { firstAfter, secondAfter })),
                new("inspect-second", "readOnly", new TraversalOperation(
                    "inspect-current", TraversalOperationKind.ReadOnly, null,
                    new[] { readFirst, readSecond }, Array.Empty<PlanLanguageHandler>())),
            },
            MaxDepth: 1,
            ReturnStep: new AgentActionStep("ui.element", "Navigate up", "tap", null));

        try
        {
            using (var journal = new FileConsultationJournal(path))
            {
                journal.Reserve(key, "fp-traverse");
                journal.Complete(key, "fp-traverse", new AgentDecision.Plan("decision-traverse-1",
                    new AgentPlanProposal(new PlanItem[] { traverse }, "bounded traversal", "plan-traverse", 3,
                        "visit configured entries and return", MaxLocalOperations: 12)));
            }

            using var restored = new FileConsultationJournal(path);
            var entry = restored.Find(key);
            Assert.NotNull(entry);
            Assert.Equal(ConsultationJournalStatus.Completed, entry.Status);
            var plan = Assert.IsType<AgentDecision.Plan>(entry.Decision);
            Assert.Equal("decision-traverse-1", plan.DecisionId);
            Assert.Equal("plan-traverse", plan.Proposal.PlanId);
            Assert.Equal(3, plan.Proposal.Version);
            Assert.Equal(12, plan.Proposal.MaxLocalOperations);

            var actual = Assert.IsType<PlanItem.TraverseItem>(Assert.Single(plan.Proposal.Items));
            Assert.Equal("settings.menu", actual.Source);
            Assert.Equal(1, actual.MaxDepth);
            Assert.Equal(new AgentActionStep("ui.element", "Navigate up", "tap", null), actual.ReturnStep);
            Assert.Equal(new[] { "navigate-first", "inspect-second" }, actual.Rules.Select(rule => rule.RuleId));
            Assert.Equal(new[] { "navigation", "readOnly" }, actual.Rules.Select(rule => rule.Condition));

            var navigation = actual.Rules[0].Operation;
            Assert.Equal("open-candidate", navigation.OperationId);
            Assert.Equal(TraversalOperationKind.Navigate, navigation.Kind);
            Assert.Equal(new AgentActionStep("$candidate", "$candidate", "tap", null), navigation.Step);
            Assert.Equal(new[] { firstBefore, secondBefore }, navigation.Before);
            Assert.Equal(new[] { firstAfter, secondAfter }, navigation.AfterVerified);

            var readOnly = actual.Rules[1].Operation;
            Assert.Equal("inspect-current", readOnly.OperationId);
            Assert.Equal(TraversalOperationKind.ReadOnly, readOnly.Kind);
            Assert.Null(readOnly.Step);
            Assert.Equal(new[] { readFirst, readSecond }, readOnly.Before);
            Assert.Empty(readOnly.AfterVerified);
            Assert.Equal(1, restored.ConsumedCount("run-traverse", "view-1"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

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
