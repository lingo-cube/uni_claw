using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class ViewportExplorationFixtureTests
{
    [Fact]
    public async Task Positive_RetainsV1V2V3AndProducesTrueTrueFalse()
    {
        var fixture = await ViewportExplorationFixture.PositiveAsync();
        var evidence = await fixture.RunAsync();

        Assert.Equal(new bool?[] { true, true, false }, evidence.Decisions.Select(item => item.ContinueExploration));
        Assert.All(evidence.Decisions, item => Assert.False(string.IsNullOrWhiteSpace(item.Reason)));
        Assert.Equal(3, evidence.AcceptedObservations.Length);
        Assert.Equal(new[] { "A", "B", "C", "More content" }, Texts(evidence.AcceptedObservations[0]));
        Assert.Equal(new[] { "B", "C", "D", "More content" }, Texts(evidence.AcceptedObservations[1]));
        Assert.Equal(new[] { "C", "D", "E", "End of list" }, Texts(evidence.AcceptedObservations[2]));
        Assert.Equal(new DeviceAction[] { new DeviceAction.ScrollForward(), new DeviceAction.ScrollForward() }, evidence.ActionHistory);
        Assert.Equal(evidence.ProgressBefore, evidence.ProgressAfter);
        Assert.True(evidence.LastContinuityAccepted);
    }

    [Fact]
    public async Task SameVisibleEvidence_IsUnresolvedRatherThanExhausted()
    {
        var fixture = await ViewportExplorationFixture.AmbiguousSameAsync();
        var evidence = await fixture.RunAsync();

        Assert.Equal(new bool?[] { true, null }, evidence.Decisions.Select(item => item.ContinueExploration));
        Assert.Equal(Texts(evidence.AcceptedObservations[0]), Texts(evidence.AcceptedObservations[1]));
        Assert.Single(evidence.ActionHistory);
        Assert.True(evidence.LastContinuityAccepted);
    }

    [Fact]
    public async Task RejectedDispatch_DoesNotObserveOrFabricateExhaustion()
    {
        var fixture = await ViewportExplorationFixture.RejectedAsync();
        var evidence = await fixture.RunAsync();

        Assert.Equal(ActionResultOutcome.Rejected, Assert.Single(evidence.Dispatches).Outcome);
        Assert.Single(evidence.AcceptedObservations);
        Assert.Single(evidence.EnvironmentObservations);
        Assert.Equal(true, Assert.Single(evidence.Decisions).ContinueExploration);
        Assert.Null(evidence.LastContinuityAccepted);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("page-changed")]
    public async Task UnacceptedContinuity_DoesNotAppendEvidence(string branch)
    {
        var fixture = branch == "stale"
            ? await ViewportExplorationFixture.StaleAsync()
            : await ViewportExplorationFixture.PageChangedAsync();
        var evidence = await fixture.RunAsync();

        Assert.False(evidence.LastContinuityAccepted);
        Assert.Single(evidence.AcceptedObservations);
        Assert.Single(evidence.Decisions);
        Assert.Single(evidence.ActionHistory);
        Assert.Equal(evidence.ProgressBefore, evidence.ProgressAfter);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("ambiguous")]
    [InlineData("rejected")]
    [InlineData("stale")]
    [InlineData("page-changed")]
    public async Task SameInputsReplayDeterministically(string branch)
    {
        var first = await RunAsync(branch);
        var second = await RunAsync(branch);

        Assert.Equal(first.RunId, second.RunId);
        AssertObservationSequenceEqual(first.AcceptedObservations, second.AcceptedObservations);
        Assert.Equal(first.Decisions, second.Decisions);
        Assert.Equal(first.Dispatches, second.Dispatches);
        Assert.Equal(first.ActionHistory, second.ActionHistory);
        AssertObservationSequenceEqual(first.EnvironmentObservations, second.EnvironmentObservations);
        Assert.Equal(first.ProgressBefore, second.ProgressBefore);
        Assert.Equal(first.ProgressAfter, second.ProgressAfter);
        Assert.Equal(first.LastContinuityAccepted, second.LastContinuityAccepted);
    }

    private static async Task<ViewportExplorationFixtureEvidence> RunAsync(string branch)
    {
        var fixture = branch switch
        {
            "positive" => await ViewportExplorationFixture.PositiveAsync(),
            "ambiguous" => await ViewportExplorationFixture.AmbiguousSameAsync(),
            "rejected" => await ViewportExplorationFixture.RejectedAsync(),
            "stale" => await ViewportExplorationFixture.StaleAsync(),
            "page-changed" => await ViewportExplorationFixture.PageChangedAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
        return await fixture.RunAsync();
    }

    private static string[] Texts(Observation observation)
        => observation.Elements.Select(element => element.Text).ToArray();

    private static void AssertObservationSequenceEqual(
        IEnumerable<Observation> expected,
        IEnumerable<Observation> actual)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();
        Assert.Equal(expectedArray.Length, actualArray.Length);
        for (var index = 0; index < expectedArray.Length; index++)
        {
            Assert.Equal(expectedArray[index].ForegroundApplication, actualArray[index].ForegroundApplication);
            Assert.Equal(expectedArray[index].SequenceNumber, actualArray[index].SequenceNumber);
            Assert.Equal(expectedArray[index].Elements, actualArray[index].Elements);
        }
    }
}
