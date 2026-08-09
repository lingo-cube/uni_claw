using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class RecoveryProgressResumeScenarioTests
{
    [Fact]
    public async Task Positive_RevalidatesAFromFreshEvidence_ContinuesB_AndCompletesOnlyFromGoalEvidence()
    {
        var run = await RecoveryProgressScenarioHarness
            .Create(RecoveryProgressResumeFixture.AgentSurvived())
            .RunAsync();

        Assert.Equal(RunState.Completed, run.State);
        Assert.True(run.CriterionOutcome);
        var boundary = run.DriftBoundary
            ?? throw new InvalidOperationException("Expected Agent drift boundary.");
        Assert.Contains(
            run.ProgressSnapshots,
            snapshot => snapshot.TryGetValue("ParentP", out var historical)
                && historical.CompletedSiblingEvidence.TryGetValue("Branch A", out var sequence)
                && sequence <= boundary);
        Assert.Contains(
            run.Observations,
            observation => observation.SequenceNumber > boundary
                && observation.ForegroundApplication == "Settings"
                && observation.Elements.Any(element => element.Text == "Branch A")
                && observation.Elements.Any(element => element.Text == "Branch B"));
        var progress = run.FinalProgress["ParentP"];
        Assert.True(progress.CompletedSiblingEvidence["Branch A"] > boundary);
        Assert.True(progress.CompletedSiblingEvidence["Branch B"] > boundary);
        Assert.True(progress.IsSubtreeComplete);
        Assert.Contains(
            run.ProgressSnapshots,
            snapshot => snapshot.TryGetValue("ParentP", out var item)
                && item.CompletedSiblingEvidence.TryGetValue("Branch A", out var aSequence)
                && aSequence > boundary
                && !item.CompletedSiblingEvidence.ContainsKey("Branch B"));
        AssertExpectedPositiveActions(run.ActionHistory);
        Assert.True(run.GoalEvidence[^1].Satisfied);
        Assert.Equal(run.Observations[^1].SequenceNumber, run.GoalEvidence[^1].SourceObservationSequence);
        Assert.Equal(RunState.Completed, run.Trace[^1].RunState);
        Assert.DoesNotContain(run.Trace[..^1], entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task Contradicted_ExcludesA_PreservesHistory_AndCannotCompleteOrReplay()
    {
        var run = await RecoveryProgressScenarioHarness
            .Create(RecoveryProgressResumeFixture.AgentContradicted())
            .RunAsync();

        Assert.Equal(RunState.Failed, run.State);
        Assert.False(run.CriterionOutcome);
        Assert.Contains("contradicted", run.Reason, StringComparison.Ordinal);
        Assert.Empty(run.FinalProgress["ParentP"].CompletedSiblingEvidence);
        AssertExpectedFailureActions(run.ActionHistory);
        Assert.Contains(run.Journal, entry =>
            entry.PostActionObservation?.Elements.Any(element =>
                element.Text == "A external effect" && element.SwitchState == true) == true);
        Assert.DoesNotContain(run.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unresolved_MissingOrUnobservableCriterion_CannotContributeCompleteOrReplay(bool missingCriterion)
    {
        var fixture = missingCriterion
            ? RecoveryProgressResumeFixture.AgentSurvived(includeCriterion: false)
            : RecoveryProgressResumeFixture.AgentUnobservable();
        var run = await RecoveryProgressScenarioHarness.Create(fixture).RunAsync();

        Assert.Equal(RunState.Failed, run.State);
        Assert.Null(run.CriterionOutcome);
        Assert.Contains("unresolved", run.Reason, StringComparison.Ordinal);
        var boundary = run.DriftBoundary
            ?? throw new InvalidOperationException("Expected Agent drift boundary.");
        Assert.True(run.FinalProgress["ParentP"].CompletedSiblingEvidence["Branch A"] <= boundary);
        AssertExpectedFailureActions(run.ActionHistory);
        Assert.DoesNotContain(run.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("contradicted")]
    [InlineData("unobservable")]
    [InlineData("missing")]
    public async Task EqualInputs_ReplayProgressActionsObservationsJournalTraceEvidenceAndState(string branch)
    {
        var first = await RunAsync(branch);
        var second = await RunAsync(branch);

        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.DriftBoundary, second.DriftBoundary);
        Assert.Equal(first.CriterionOutcome, second.CriterionOutcome);
        AssertProgressEqual(first.FinalProgress, second.FinalProgress);
        Assert.Equal(first.ProgressSnapshots.Length, second.ProgressSnapshots.Length);
        for (var index = 0; index < first.ProgressSnapshots.Length; index++)
            AssertProgressEqual(first.ProgressSnapshots[index], second.ProgressSnapshots[index]);
        Assert.Equal(first.ActionHistory, second.ActionHistory);
        AssertObservationsEqual(first.Observations, second.Observations);
        AssertJournalEqual(first.Journal, second.Journal);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
    }

    private static async Task<RecoveryProgressScenarioEvidence> RunAsync(string branch)
    {
        var fixture = branch switch
        {
            "positive" => RecoveryProgressResumeFixture.AgentSurvived(),
            "contradicted" => RecoveryProgressResumeFixture.AgentContradicted(),
            "unobservable" => RecoveryProgressResumeFixture.AgentUnobservable(),
            "missing" => RecoveryProgressResumeFixture.AgentSurvived(includeCriterion: false),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
        return await RecoveryProgressScenarioHarness.Create(fixture).RunAsync();
    }

    private static void AssertExpectedPositiveActions(ImmutableArray<DeviceAction> actual)
        => Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.SetSwitch(0, true),
                new DeviceAction.Tap(1),
                new DeviceAction.Tap(1),
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(1),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(1),
            },
            actual);

    private static void AssertExpectedFailureActions(ImmutableArray<DeviceAction> actual)
        => Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.SetSwitch(0, true),
                new DeviceAction.Tap(1),
                new DeviceAction.Tap(1),
                new DeviceAction.LaunchApp("Settings"),
            },
            actual);

    private static void AssertProgressEqual(
        IReadOnlyDictionary<string, BranchProgressEvidence> expected,
        IReadOnlyDictionary<string, BranchProgressEvidence> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key].ParentSemanticPage, actual[key].ParentSemanticPage);
            Assert.Equal(expected[key].ApprovedSiblingEvidence, actual[key].ApprovedSiblingEvidence);
            Assert.Equal(expected[key].CompletedSiblingEvidence, actual[key].CompletedSiblingEvidence);
        }
    }

    private static void AssertObservationsEqual(
        ImmutableArray<Observation> expected,
        ImmutableArray<Observation> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].ForegroundApplication, actual[index].ForegroundApplication);
            Assert.Equal(expected[index].SequenceNumber, actual[index].SequenceNumber);
            Assert.Equal(expected[index].Elements.Length, actual[index].Elements.Length);
            for (var element = 0; element < expected[index].Elements.Length; element++)
                Assert.Equal(expected[index].Elements[element], actual[index].Elements[element]);
        }
    }

    private static void AssertJournalEqual(
        ImmutableArray<UniClaw.Runtime.Traversal.TraversalJournalEntry> expected,
        ImmutableArray<UniClaw.Runtime.Traversal.TraversalJournalEntry> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].StepId, actual[index].StepId);
            Assert.Equal(expected[index].SelectedElementIndex, actual[index].SelectedElementIndex);
            Assert.Equal(expected[index].DispatchedAction, actual[index].DispatchedAction);
            Assert.Equal(expected[index].RetryCount, actual[index].RetryCount);
            Assert.Equal(expected[index].Result, actual[index].Result);
            if (expected[index].PostActionObservation is { } expectedObservation
                && actual[index].PostActionObservation is { } actualObservation)
            {
                AssertObservationsEqual([expectedObservation], [actualObservation]);
            }
            else
            {
                Assert.Equal(expected[index].PostActionObservation, actual[index].PostActionObservation);
            }
        }
    }
}
