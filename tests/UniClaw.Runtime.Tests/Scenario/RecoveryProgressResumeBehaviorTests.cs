using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class RecoveryProgressResumeBehaviorTests
{
    [Fact]
    public async Task Revalidated_RefreshesAAndContinuesBWithoutReplayingAPrefix()
    {
        var run = RecoveryProgressScenarioHarness.Create(RecoveryProgressResumeFixture.AgentSurvived());

        var state = await run.Agent.RunAsync(run.Goal, run.Plan, run.RunId, CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        var boundary = run.Agent.LastTrap?.Observed
            ?? throw new InvalidOperationException("Expected Agent drift boundary.");
        var progress = run.Agent.BranchProgress["ParentP"];
        Assert.True(progress.CompletedSiblingEvidence["Branch A"] > boundary);
        Assert.True(progress.CompletedSiblingEvidence["Branch B"] > boundary);
        Assert.True(progress.IsSubtreeComplete);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.Equal(
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
            run.Environment.ActionHistory);
        Assert.Contains(
            run.Agent.Trace,
            entry => entry.Reason?.Contains("branch progress revalidated", StringComparison.Ordinal) == true);
        Assert.True(run.GoalEvidence[^1].Satisfied);
        Assert.Equal(RunState.Completed, run.Agent.Trace[^1].RunState);
    }

    [Fact]
    public async Task Contradicted_ExcludesAAndFailsWithoutBlindReplayOrGoalCompletion()
    {
        var run = RecoveryProgressScenarioHarness.Create(RecoveryProgressResumeFixture.AgentContradicted());

        var state = await run.Agent.RunAsync(run.Goal, run.Plan, run.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Contains("contradicted", run.Agent.Reason, StringComparison.Ordinal);
        Assert.Empty(run.Agent.BranchProgress["ParentP"].CompletedSiblingEvidence);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        AssertNoAPrefixReplayOnFailure(run.Environment.ActionHistory);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.All(run.GoalEvidence, evidence => Assert.False(evidence.Satisfied));
    }

    [Fact]
    public async Task Unobservable_RetainsHistoricalAOnlyAndFailsWithoutBlindReplay()
    {
        var run = RecoveryProgressScenarioHarness.Create(RecoveryProgressResumeFixture.AgentUnobservable());

        var state = await run.Agent.RunAsync(run.Goal, run.Plan, run.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Contains("unresolved", run.Agent.Reason, StringComparison.Ordinal);
        var boundary = run.Agent.LastTrap?.Observed
            ?? throw new InvalidOperationException("Expected Agent drift boundary.");
        var aSequence = run.Agent.BranchProgress["ParentP"].CompletedSiblingEvidence["Branch A"];
        Assert.True(aSequence <= boundary);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        AssertNoAPrefixReplayOnFailure(run.Environment.ActionHistory);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task MissingCriterion_IsUnresolvedAndCannotUsePositionProofAsBranchProof()
    {
        var run = RecoveryProgressScenarioHarness.Create(
            RecoveryProgressResumeFixture.AgentSurvived(includeCriterion: false));

        var state = await run.Agent.RunAsync(run.Goal, run.Plan, run.RunId, CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Contains("unresolved", run.Agent.Reason, StringComparison.Ordinal);
        var boundary = run.Agent.LastTrap?.Observed
            ?? throw new InvalidOperationException("Expected Agent drift boundary.");
        Assert.True(
            run.Agent.BranchProgress["ParentP"].CompletedSiblingEvidence["Branch A"]
            <= boundary);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        AssertNoAPrefixReplayOnFailure(run.Environment.ActionHistory);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    private static void AssertNoAPrefixReplayOnFailure(IReadOnlyList<DeviceAction> actionHistory)
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
            actionHistory);

}
