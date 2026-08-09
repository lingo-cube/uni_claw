using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Formal end-to-end proof for SC-P3-CAND-006 Required Assertions 1-12.</summary>
public sealed class BoundedCandidateSafetyScenarioTests
{
    [Fact]
    public async Task FormalScenario_SafeExecutesWhileDestructiveStateChangingAndUnresolvedRemainZeroDispatchEvidence()
    {
        var fixture = BoundedCandidateSafetyRunFixture.Create();

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Empty(fixture.Plan.Steps); // S/D/T/U were observed, not fixed executable PlanSteps.
        Assert.Equal(new[] { 0, 1, 2, 3 }, fixture.AuthorizationOrder);
        var initial = fixture.Environment.ObservationHistory[1];
        Assert.Equal(2, initial.SequenceNumber);
        Assert.Equal(new[] { 0, 1, 2, 3 }, initial.Elements.Select(candidate => candidate.Index));

        var tap = Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Equal(new DeviceAction.Tap(0), tap);
        var journal = Assert.Single(fixture.Traversal.Journal);
        Assert.Equal(0, journal.SelectedElementIndex);
        Assert.Equal(tap, journal.DispatchedAction);
        Assert.Equal(3, journal.PostActionObservation!.SequenceNumber);
        Assert.DoesNotContain(fixture.Traversal.Journal, entry => entry.SelectedElementIndex is 1 or 2 or 3);

        var denialEvents = CandidateDenialEvents(fixture);
        Assert.Equal(3, denialEvents.Length);
        AssertDenial(denialEvents, "rejected", BoundedCandidateSafetyFixture.DestructiveText, 1, 2);
        AssertDenial(denialEvents, "rejected", BoundedCandidateSafetyFixture.StateChangingText, 2, 2);
        AssertDenial(denialEvents, "unresolved", BoundedCandidateSafetyFixture.UnknownText, 3, 2);
        Assert.All(denialEvents, entry =>
        {
            Assert.Null(entry.StepId);
            Assert.Null(entry.ActionId);
            Assert.Null(entry.Action);
        });

        Assert.Empty(fixture.Agent.BranchProgress);
        var goalEvidence = Assert.Single(fixture.GoalEvidence);
        Assert.True(goalEvidence.Satisfied);
        Assert.Equal(3, goalEvidence.SourceObservationSequence);
        Assert.Equal(goalEvidence.Reason, fixture.Agent.Trace[^1].Reason);
        Assert.Equal(RunState.Completed, fixture.Agent.Trace[^1].RunState);
    }

    [Fact]
    public async Task FormalScenario_NoAuthorizedCandidateHasExplicitEvidenceZeroDispatchAndNoRequiredWorkOrCompletion()
    {
        static CandidateAuthorizationEvidence DenyAll(
            Observation observation,
            ObservedElement candidate)
        {
            Assert.Contains(candidate, observation.Elements);
            return new CandidateAuthorizationEvidence(
                candidate.Index == 3 ? null : false,
                candidate.Index == 3
                    ? "fresh bounded evidence remains unresolved"
                    : "fresh bounded evidence positively rejects candidate");
        }
        var fixture = BoundedCandidateSafetyRunFixture.Create(DenyAll);

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new[] { 0, 1, 2, 3 }, fixture.AuthorizationOrder);
        Assert.Empty(fixture.Traversal.Journal);
        Assert.DoesNotContain(fixture.Environment.ActionHistory, action => action is DeviceAction.Tap);
        Assert.Empty(fixture.Agent.BranchProgress);
        Assert.Empty(fixture.GoalEvidence);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Equal(4, CandidateDenialEvents(fixture).Length);
        Assert.Contains("零 candidate dispatch", fixture.Agent.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormalScenario_AuthorizationAndLocalTraversalSuccessDoNotFabricateGoalCompletion()
    {
        var fixture = BoundedCandidateSafetyRunFixture.Create(safeWorldChanges: false);

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        var journal = Assert.Single(fixture.Traversal.Journal);
        Assert.IsType<TraversalStepResult.Succeeded>(journal.Result);
        Assert.NotNull(journal.PostActionObservation);
        var evidence = Assert.Single(fixture.GoalEvidence);
        Assert.False(evidence.Satisfied);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Contains("Goal 证据未满足", fixture.Agent.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormalScenario_EqualInputsReplayEqualTraceJournalActionsObservationsEvidenceAndState()
    {
        var first = BoundedCandidateSafetyRunFixture.Create();
        var second = BoundedCandidateSafetyRunFixture.Create();

        var firstState = await first.RunAsync();
        var secondState = await second.RunAsync();

        Assert.Equal(firstState, secondState);
        Assert.Equal(first.AuthorizationOrder, second.AuthorizationOrder);
        Assert.Equal(first.Agent.Trace, second.Agent.Trace);
        Assert.Equal(first.Environment.ActionHistory, second.Environment.ActionHistory);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
        Assert.Equal(first.Agent.Reason, second.Agent.Reason);
        Assert.Equal(first.Agent.BranchProgress, second.Agent.BranchProgress);
        AssertObservationSequencesEqual(
            first.Environment.ObservationHistory,
            second.Environment.ObservationHistory);
        AssertJournalsEqual(first.Traversal.Journal, second.Traversal.Journal);
    }

    private static TraceEvent[] CandidateDenialEvents(BoundedCandidateSafetyRunFixture fixture)
        => fixture.Agent.Trace
            .Where(entry => entry.Reason?.StartsWith("bounded candidate ", StringComparison.Ordinal) is true)
            .ToArray();

    private static void AssertDenial(
        IEnumerable<TraceEvent> events,
        string outcome,
        string text,
        int index,
        long sequence)
    {
        var entry = Assert.Single(events, candidate =>
            candidate.Reason!.Contains($"text={text}, index={index}", StringComparison.Ordinal));
        Assert.Contains($"bounded candidate {outcome}", entry.Reason, StringComparison.Ordinal);
        Assert.Contains($"source-seq={sequence}", entry.Reason, StringComparison.Ordinal);
    }

    private static void AssertObservationSequencesEqual(
        IReadOnlyList<Observation> first,
        IReadOnlyList<Observation> second)
    {
        Assert.Equal(first.Count, second.Count);
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].ForegroundApplication, second[index].ForegroundApplication);
            Assert.Equal(first[index].SequenceNumber, second[index].SequenceNumber);
            Assert.Equal(first[index].Elements.ToArray(), second[index].Elements.ToArray());
        }
    }

    private static void AssertJournalsEqual(
        IReadOnlyList<TraversalJournalEntry> first,
        IReadOnlyList<TraversalJournalEntry> second)
    {
        Assert.Equal(first.Count, second.Count);
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].StepId, second[index].StepId);
            Assert.Equal(first[index].SelectedElementIndex, second[index].SelectedElementIndex);
            Assert.Equal(first[index].DispatchedAction, second[index].DispatchedAction);
            Assert.Equal(first[index].Result, second[index].Result);
            Assert.Equal(first[index].RetryCount, second[index].RetryCount);
            AssertObservationSequencesEqual(
                [first[index].PostActionObservation!],
                [second[index].PostActionObservation!]);
        }
    }
}
