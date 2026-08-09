using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class RecoveryProgressResumeFixtureTests
{
    [Fact]
    public void PlanStep_TwoArgumentConstruction_RemainsCompatibleWithAbsentCriterion()
    {
        var step = new PlanStep("Target", "Tap");

        Assert.Equal("Target", step.TargetDescription);
        Assert.Equal("Tap", step.ActionDescription);
        Assert.Null(step.BranchEffectEvidenceEvaluator);
    }

    [Fact]
    public async Task Survived_ScriptsAEffectDriftRecoveryFreshProofAndRemainingB()
    {
        var fixture = RecoveryProgressResumeFixture.Survived();
        var evidence = await fixture.RunAsync();

        Assert.Equal(RecoveryProgressResumeFixture.DefaultRunId, evidence.RunId);
        AssertElements(evidence.Observations[0], "Branch A", "Branch B");
        AssertElements(evidence.Observations[1], "A external effect", "Return to Parent P");
        Assert.False(evidence.Observations[1].Elements[0].SwitchState);
        Assert.True(evidence.Observations[2].Elements[0].SwitchState);
        AssertElements(evidence.Observations[3], "Branch A", "Branch B", "A external effect");
        Assert.Equal("Launcher", evidence.Observations[4].ForegroundApplication);
        Assert.Equal("Settings", evidence.Observations[5].ForegroundApplication);
        Assert.True(evidence.Observations[5].Elements[2].SwitchState);
        AssertElements(evidence.Observations[6], "Complete B work", "Return to Parent P");
        Assert.True(evidence.CriterionOutcome);
        Assert.Equal(Enumerable.Range(1, 7).Select(value => (long)value),
            evidence.Observations.Select(observation => observation.SequenceNumber));
        Assert.Collection(
            evidence.ActionHistory,
            action => Assert.IsType<DeviceAction.Tap>(action),
            action => Assert.IsType<DeviceAction.SetSwitch>(action),
            action => Assert.IsType<DeviceAction.Tap>(action),
            action => Assert.IsType<DeviceAction.LaunchApp>(action),
            action => Assert.IsType<DeviceAction.Tap>(action));
        Assert.Single(evidence.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.All(evidence.Dispatches, dispatch => Assert.Equal(ActionResultOutcome.Dispatched, dispatch.Outcome));
    }

    [Fact]
    public async Task Contradicted_FreshRecoveredEvidenceReturnsFalse()
    {
        var evidence = await RecoveryProgressResumeFixture.Contradicted().RunAsync();

        Assert.False(evidence.Observations[5].Elements[2].SwitchState);
        Assert.False(evidence.CriterionOutcome);
        Assert.Single(evidence.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact]
    public async Task Unobservable_FreshRecoveredEvidenceReturnsNull()
    {
        var evidence = await RecoveryProgressResumeFixture.Unobservable().RunAsync();

        AssertElements(evidence.Observations[5], "Branch A", "Branch B");
        Assert.Null(evidence.CriterionOutcome);
        Assert.Single(evidence.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Theory]
    [InlineData("survived")]
    [InlineData("contradicted")]
    [InlineData("unobservable")]
    public async Task EqualInputs_ReplayWorldCriterionDispatchAndActionEvidence(string branch)
    {
        var first = await Create(branch).RunAsync();
        var second = await Create(branch).RunAsync();

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.CriterionOutcome, second.CriterionOutcome);
        Assert.Equal(first.Dispatches, second.Dispatches);
        Assert.Equal(first.ActionHistory, second.ActionHistory);
        Assert.Equal(first.Observations.Length, second.Observations.Length);
        for (var index = 0; index < first.Observations.Length; index++)
            AssertSameObservation(first.Observations[index], second.Observations[index]);
    }

    private static RecoveryProgressResumeFixture Create(string branch) => branch switch
    {
        "survived" => RecoveryProgressResumeFixture.Survived(),
        "contradicted" => RecoveryProgressResumeFixture.Contradicted(),
        "unobservable" => RecoveryProgressResumeFixture.Unobservable(),
        _ => throw new ArgumentOutOfRangeException(nameof(branch)),
    };

    private static void AssertElements(Observation observation, params string[] expected)
        => Assert.Equal(expected, observation.Elements.Select(element => element.Text));

    private static void AssertSameObservation(Observation expected, Observation actual)
    {
        Assert.Equal(expected.ForegroundApplication, actual.ForegroundApplication);
        Assert.Equal(expected.SequenceNumber, actual.SequenceNumber);
        Assert.Equal(expected.Elements.Length, actual.Elements.Length);
        for (var index = 0; index < expected.Elements.Length; index++)
            Assert.Equal(expected.Elements[index], actual.Elements[index]);
    }
}
