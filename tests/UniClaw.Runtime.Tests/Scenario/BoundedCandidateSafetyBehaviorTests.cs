using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class BoundedCandidateSafetyBehaviorTests
{
    [Fact]
    public async Task AuthorizedSafeCandidate_RecordsAllDenialsThenDispatchesExactlyOneTapAndCompletesFromGoalEvidence()
    {
        var fixture = BoundedCandidateSafetyRunFixture.Create();

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(new[] { 0, 1, 2, 3 }, fixture.AuthorizationOrder);
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
            },
            fixture.Environment.ActionHistory);
        Assert.Equal(new long[] { 1, 2, 3 }, fixture.Environment.ObservationHistory.Select(item => item.SequenceNumber));
        var journal = Assert.Single(fixture.Traversal.Journal);
        Assert.Equal(0, journal.SelectedElementIndex);
        Assert.Equal(new DeviceAction.Tap(0), journal.DispatchedAction);
        Assert.Equal(3, journal.PostActionObservation!.SequenceNumber);
        Assert.Equal(2, fixture.GoalEvidence.Count); // CP-06：seq2 初始评估（未满足）+ seq3（满足）
        var evidence = fixture.GoalEvidence[1];
        Assert.True(evidence.Satisfied);
        Assert.Equal(3, evidence.SourceObservationSequence);
        Assert.Empty(fixture.Agent.BranchProgress);

        var denied = fixture.Agent.Trace
            .Where(entry => entry.Reason?.StartsWith("bounded candidate ", StringComparison.Ordinal) is true)
            .ToArray();
        Assert.Equal(3, denied.Length);
        Assert.All(denied, entry =>
        {
            Assert.Null(entry.StepId);
            Assert.Null(entry.ActionId);
            Assert.Null(entry.Action);
            Assert.Contains("source-seq=2", entry.Reason, StringComparison.Ordinal);
        });
        Assert.Contains(denied, entry => entry.Reason!.Contains("text=Reset options, index=1", StringComparison.Ordinal));
        Assert.Contains(denied, entry => entry.Reason!.Contains("text=Wi-Fi, index=2", StringComparison.Ordinal));
        Assert.Contains(denied, entry => entry.Reason!.Contains("text=Custom operation, index=3", StringComparison.Ordinal));
        Assert.Equal(RunState.Completed, fixture.Agent.Trace[^1].RunState);
    }

    [Fact]
    public async Task NoAuthorizedCandidate_FailsExplicitlyWithoutTraversalDispatchOrFabricatedCompletion()
    {
        static CandidateAuthorizationEvidence RejectOrUnresolve(
            Observation observation,
            ObservedElement candidate)
        {
            if (!observation.Elements.Contains(candidate))
                throw new ArgumentException("candidate outside supplied Observation", nameof(candidate));
            return new CandidateAuthorizationEvidence(
                candidate.Index == 3 ? null : false,
                candidate.Index == 3 ? "bounded evidence unresolved" : "bounded evidence rejected");
        }
        var fixture = BoundedCandidateSafetyRunFixture.Create(RejectOrUnresolve);

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new[] { 0, 1, 2, 3 }, fixture.AuthorizationOrder);
        Assert.Empty(fixture.Traversal.Journal);
        Assert.DoesNotContain(fixture.Environment.ActionHistory, action => action is DeviceAction.Tap);
        Assert.Empty(fixture.GoalEvidence);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Contains("零 candidate dispatch", fixture.Agent.Reason, StringComparison.Ordinal);
        Assert.Equal(4, fixture.Agent.Trace.Count(entry =>
            entry.Reason?.StartsWith("bounded candidate ", StringComparison.Ordinal) is true));
        Assert.Empty(fixture.Agent.BranchProgress);
    }

    [Fact]
    public async Task CandidateEvaluatorAbsent_PreservesExistingFixedPlanExecution()
    {
        var fixture = BoundedCandidateSafetyRunFixture.Create(
            includeCandidateEvaluator: false,
            plan: new Plan([new PlanStep(BoundedCandidateSafetyFixture.SafeText, "Tap")]));

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Empty(fixture.AuthorizationOrder);
        Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Single(fixture.Traversal.Journal);
        Assert.DoesNotContain(fixture.Agent.Trace, entry =>
            entry.Reason?.StartsWith("bounded candidate ", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task AuthorizedCandidateStillSubjectToTraversalMechanicalRejection()
    {
        var fixture = BoundedCandidateSafetyRunFixture.Create(
            safeDispatchOutcome: ActionResultOutcome.Rejected);

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        var tap = Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Equal(new DeviceAction.Tap(0), tap);
        var journal = Assert.Single(fixture.Traversal.Journal);
        Assert.IsType<TraversalStepResult.Failed>(journal.Result);
        Assert.Null(journal.PostActionObservation);
        Assert.Single(fixture.GoalEvidence); // CP-06：seq2 初始评估唯一一次（dispatch 被 Rejected → 无 post-action 评估）
        Assert.False(fixture.GoalEvidence[0].Satisfied);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task MultipleAuthorizedCandidates_OnlyFirstEntersTraversal()
    {
        var fixture = BoundedCandidateSafetyRunFixture.Create(
            (_, candidate) => new CandidateAuthorizationEvidence(
                true,
                $"candidate index {candidate.Index} authorized for first-only proof"));

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(new[] { 0, 1, 2, 3 }, fixture.AuthorizationOrder);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        Assert.Single(fixture.Traversal.Journal);
    }

    [Fact]
    public async Task GroundedFixedPlan_PreparesSafetyReceipts_WithoutActivatingTransientCandidateInsertion()
    {
        var criterion = new TargetGroundingCriterion(
            (_, candidate) => new TargetGroundingEvidence(
                candidate.Text == BoundedCandidateSafetyFixture.SafeText,
                $"candidate text={candidate.Text}"),
            post => new TargetGroundingEvidence(
                post.Elements.Any(element => element.Text == "About phone details"),
                "first fresh Observation proves expected destination"));
        var fixture = BoundedCandidateSafetyRunFixture.Create(
            plan: new Plan([new PlanStep(BoundedCandidateSafetyFixture.SafeText, "Tap", TargetGroundingCriterion: criterion)]));

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(new[] { 0, 1, 2, 3 }, fixture.AuthorizationOrder);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        Assert.Equal(3, fixture.Agent.Trace.Count(entry =>
            entry.Reason?.StartsWith("bounded candidate ", StringComparison.Ordinal) is true));
        Assert.Equal(3, Assert.Single(fixture.Traversal.Journal).PostActionObservation!.SequenceNumber);
    }

    [Fact]
    public async Task GroundedFixedPlan_RejectedOrUnconfirmedFreshEffect_FailsOnceWithoutCompletionOrRedispatch()
    {
        foreach (bool? confirmation in new bool?[] { false, null })
        {
            var fixture = BoundedCandidateSafetyRunFixture.Create(plan: new Plan([
                GroundedStep(_ => new TargetGroundingEvidence(
                    confirmation,
                    confirmation is false ? "Wi-Fi Calling destination contradicts Wi-Fi expected effect" : "fresh effect remains unconfirmed"))]));

            var state = await fixture.RunAsync();

            Assert.Equal(RunState.Failed, state);
            Assert.Equal(new DeviceAction.Tap(0), Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
            Assert.Single(fixture.Traversal.Journal);
            Assert.DoesNotContain(fixture.Agent.Trace, trace => trace.RunState == RunState.Completed);
            Assert.Single(fixture.GoalEvidence); // initial evidence only; grounding is not GoalEvidence.
        }
    }

    [Fact]
    public async Task GroundedFixedPlan_InsufficientOrAbsentSafetyReceipt_IsStructuredZeroDispatchFailure()
    {
        var insufficient = BoundedCandidateSafetyRunFixture.Create(plan: new Plan([
            new PlanStep(BoundedCandidateSafetyFixture.SafeText, "Tap", TargetGroundingCriterion: new TargetGroundingCriterion(
                (_, _) => new TargetGroundingEvidence(null, "text alone is insufficient"),
                _ => new TargetGroundingEvidence(true, "unused")))]));
        var absentSafety = BoundedCandidateSafetyRunFixture.Create(
            includeCandidateEvaluator: false,
            plan: new Plan([GroundedStep(_ => new TargetGroundingEvidence(true, "unused"))]));

        Assert.Equal(RunState.Failed, await insufficient.RunAsync());
        Assert.Equal(RunState.Failed, await absentSafety.RunAsync());
        Assert.Empty(insufficient.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(absentSafety.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Contains("insufficient", insufficient.Agent.Reason, StringComparison.Ordinal);
        Assert.Contains("absent or not authorized", absentSafety.Agent.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroundedFixedPlan_UnsafeReceipt_IsStructuredZeroDispatchFailure()
    {
        var fixture = BoundedCandidateSafetyRunFixture.Create(
            (_, _) => new CandidateAuthorizationEvidence(false, "not safe"),
            plan: new Plan([GroundedStep(_ => new TargetGroundingEvidence(true, "unused"))]));

        Assert.Equal(RunState.Failed, await fixture.RunAsync());
        Assert.Empty(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Contains("not authorized", fixture.Agent.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroundedFixedPlan_EqualInputsReplayEqualEvidenceAndOutcome()
    {
        async Task<(RunState State, string? Reason, IReadOnlyList<DecisionRecord> Trace, IReadOnlyList<TraversalJournalEntry> Journal, IReadOnlyList<DeviceAction> Actions, IReadOnlyList<Observation> Observations, IReadOnlyList<GoalEvidence> GoalEvidence)> RunOnce()
        {
            var fixture = BoundedCandidateSafetyRunFixture.Create(plan: new Plan([
                GroundedStep(_ => new TargetGroundingEvidence(false, "Wi-Fi Calling contradicts expected Wi-Fi destination"))]));
            var state = await fixture.RunAsync();
            return (state, fixture.Agent.Reason, fixture.Agent.Trace, fixture.Traversal.Journal,
                fixture.Environment.ActionHistory, fixture.Environment.ObservationHistory, fixture.GoalEvidence);
        }

        var first = await RunOnce();
        var second = await RunOnce();

        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(
            first.Journal.Select(entry => (entry.StepId, entry.SelectedElementIndex, entry.DispatchedAction, entry.PostActionObservation?.SequenceNumber, entry.Result, entry.RetryCount)),
            second.Journal.Select(entry => (entry.StepId, entry.SelectedElementIndex, entry.DispatchedAction, entry.PostActionObservation?.SequenceNumber, entry.Result, entry.RetryCount)));
        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(
            first.Observations.Select(observation => $"{observation.SequenceNumber}:{observation.ForegroundApplication}:{string.Join(',', observation.Elements.Select(element => $"{element.Index}/{element.Text}/{element.SwitchState}"))}"),
            second.Observations.Select(observation => $"{observation.SequenceNumber}:{observation.ForegroundApplication}:{string.Join(',', observation.Elements.Select(element => $"{element.Index}/{element.Text}/{element.SwitchState}"))}"));
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
    }

    private static PlanStep GroundedStep(Func<Observation, TargetGroundingEvidence> postAction)
        => new(
            BoundedCandidateSafetyFixture.SafeText,
            "Tap",
            TargetGroundingCriterion: new TargetGroundingCriterion(
                (_, candidate) => new TargetGroundingEvidence(
                    candidate.Text == BoundedCandidateSafetyFixture.SafeText,
                    $"candidate text={candidate.Text}; Wi-Fi Calling remains only text-adjacent evidence"),
                postAction));
}
