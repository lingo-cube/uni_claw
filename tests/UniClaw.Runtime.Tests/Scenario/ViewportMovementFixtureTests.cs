using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class ViewportMovementFixtureTests
{
    [Fact]
    public async Task Continuous_OneTargetlessActionChangesVisibleElementsAndPreservesFixtureProgress()
    {
        var fixture = await ViewportMovementFixture.ContinuousAsync();
        var evidence = await fixture.RunAsync();

        Assert.Equal(ViewportMovementFixture.DefaultRunId, evidence.RunId);
        Assert.Equal(new[] { "A", "B", "C" }, evidence.Before.Elements.Select(element => element.Text));
        Assert.Equal(new[] { "D", "E", "F" }, evidence.After.Elements.Select(element => element.Text));
        Assert.True(evidence.After.SequenceNumber > evidence.Before.SequenceNumber);
        Assert.Equal("ScrollableList", evidence.SemanticPageBefore);
        Assert.Equal("ScrollableList", evidence.SemanticPageAfter);
        Assert.Equal(ActionResultOutcome.Dispatched, evidence.Dispatch.Outcome);
        Assert.Equal(new DeviceAction[] { new DeviceAction.ScrollForward() }, evidence.ActionHistory);
        Assert.Equal(evidence.ProgressBefore, evidence.ProgressAfter);
        Assert.Equal("Existing local progress", Assert.Single(evidence.ProgressBefore).TargetDescription);
    }

    [Fact]
    public async Task Stale_ReturnsSameSequenceWithoutFabricatingASecondAction()
    {
        var fixture = await ViewportMovementFixture.StaleAsync();
        var evidence = await fixture.RunAsync();

        Assert.Equal(evidence.Before.SequenceNumber, evidence.After.SequenceNumber);
        Assert.Equal(new[] { "D", "E", "F" }, evidence.After.Elements.Select(element => element.Text));
        Assert.Equal(new DeviceAction[] { new DeviceAction.ScrollForward() }, evidence.ActionHistory);
        Assert.Equal(evidence.ProgressBefore, evidence.ProgressAfter);
    }

    [Fact]
    public async Task PageChanged_ReturnsFreshDistinguishableIdentityConflict()
    {
        var fixture = await ViewportMovementFixture.PageChangedAsync();
        var evidence = await fixture.RunAsync();

        Assert.True(evidence.After.SequenceNumber > evidence.Before.SequenceNumber);
        Assert.Equal("ScrollableList", evidence.SemanticPageBefore);
        Assert.Equal("OtherPage", evidence.SemanticPageAfter);
        Assert.Equal("Other semantic page", Assert.Single(evidence.After.Elements).Text);
        Assert.Equal(new DeviceAction[] { new DeviceAction.ScrollForward() }, evidence.ActionHistory);
    }

    [Theory]
    [InlineData("continuous")]
    [InlineData("stale")]
    [InlineData("page-changed")]
    public async Task SameConfiguration_ReplaysDeterministically(string branch)
    {
        var first = await RunAsync(branch);
        var second = await RunAsync(branch);

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.Dispatch, second.Dispatch);
        Assert.Equal(first.ActionHistory, second.ActionHistory);
        AssertSameObservation(first.Before, second.Before);
        AssertSameObservation(first.After, second.After);
        Assert.Equal(first.ProgressBefore, second.ProgressBefore);
        Assert.Equal(first.ProgressAfter, second.ProgressAfter);
        Assert.Equal(first.SemanticPageBefore, second.SemanticPageBefore);
        Assert.Equal(first.SemanticPageAfter, second.SemanticPageAfter);
    }

    private static async Task<ViewportMovementEvidence> RunAsync(string branch)
    {
        var fixture = branch switch
        {
            "continuous" => await ViewportMovementFixture.ContinuousAsync(),
            "stale" => await ViewportMovementFixture.StaleAsync(),
            "page-changed" => await ViewportMovementFixture.PageChangedAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
        return await fixture.RunAsync();
    }

    private static void AssertSameObservation(Observation expected, Observation actual)
    {
        Assert.Equal(expected.ForegroundApplication, actual.ForegroundApplication);
        Assert.Equal(expected.SequenceNumber, actual.SequenceNumber);
        Assert.Equal(expected.Elements.Length, actual.Elements.Length);
        for (var index = 0; index < expected.Elements.Length; index++)
            Assert.Equal(expected.Elements[index], actual.Elements[index]);
    }
}
