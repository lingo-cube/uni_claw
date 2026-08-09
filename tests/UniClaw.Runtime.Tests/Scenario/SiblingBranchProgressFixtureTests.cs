using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class SiblingBranchProgressFixtureTests
{
    [Fact]
    public async Task Complete_ExposesParentAThenBAndReturnsToParent()
    {
        var evidence = await SiblingBranchProgressFixture.Complete().RunAsync();

        Assert.Equal(SiblingBranchProgressFixture.DefaultRunId, evidence.RunId);
        AssertElements(evidence.Observations[0], "Branch A", "Branch B");
        AssertElements(evidence.Observations[1], "Complete A work", "Return to Parent P");
        AssertElements(evidence.Observations[2], "A local effect", "Return to Parent P");
        AssertElements(evidence.Observations[3], "Branch A", "Branch B");
        AssertElements(evidence.Observations[4], "Complete B work", "Return to Parent P");
        AssertElements(evidence.Observations[5], "B local effect", "Return to Parent P");
        AssertElements(evidence.Observations[6], "Branch A", "Branch B");
        Assert.Equal(Enumerable.Range(1, 7).Select(value => (long)value),
            evidence.Observations.Select(observation => observation.SequenceNumber));
        Assert.All(evidence.Dispatches, dispatch => Assert.Equal(ActionResultOutcome.Dispatched, dispatch.Outcome));
        Assert.Equal(6, evidence.ActionHistory.Length);
    }

    [Fact]
    public async Task AOnly_ReturnsToParentWithoutAnyBranchBWorldEffect()
    {
        var evidence = await SiblingBranchProgressFixture.AOnly().RunAsync();

        AssertElements(evidence.Observations[^1], "Branch A", "Branch B");
        Assert.DoesNotContain(
            evidence.Observations.SelectMany(observation => observation.Elements),
            element => element.Text is "Complete B work" or "B local effect");
        Assert.Equal(3, evidence.ActionHistory.Length);
    }

    [Fact]
    public async Task EarlyReturn_DoesNotApplyChildLocalEffect()
    {
        var evidence = await SiblingBranchProgressFixture.EarlyReturn().RunAsync();

        AssertElements(evidence.Observations[1], "Complete A work", "Return to Parent P");
        AssertElements(evidence.Observations[2], "Branch A", "Branch B");
        Assert.DoesNotContain(
            evidence.Observations.SelectMany(observation => observation.Elements),
            element => element.Text == "A local effect");
    }

    [Fact]
    public async Task RevisitA_ReplaysChildWorldWithoutFixtureProgressConclusion()
    {
        var evidence = await SiblingBranchProgressFixture.RevisitA().RunAsync();

        AssertElements(evidence.Observations[3], "Branch A", "Branch B");
        AssertElements(evidence.Observations[4], "Complete A work", "Return to Parent P");
        Assert.Equal(4, evidence.ActionHistory.Length);
    }

    [Fact]
    public async Task StaleParent_ReturnsParentElementsWithNonAdvancingSequence()
    {
        var evidence = await SiblingBranchProgressFixture.StaleParent().RunAsync();

        AssertElements(evidence.Observations[^1], "Branch A", "Branch B");
        Assert.Equal(evidence.Observations[^2].SequenceNumber, evidence.Observations[^1].SequenceNumber);
    }

    [Fact]
    public async Task WrongParent_ReturnsFreshDistinguishableExternalEvidence()
    {
        var evidence = await SiblingBranchProgressFixture.WrongParent().RunAsync();

        Assert.True(evidence.Observations[^1].SequenceNumber > evidence.Observations[^2].SequenceNumber);
        AssertElements(evidence.Observations[^1], "Conflicting parent");
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("a-only")]
    [InlineData("early-return")]
    [InlineData("revisit")]
    [InlineData("stale")]
    [InlineData("wrong-parent")]
    public async Task SameInput_ReplaysObservationsDispatchesAndActionsDeterministically(string path)
    {
        var first = await Create(path).RunAsync();
        var second = await Create(path).RunAsync();

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.Dispatches, second.Dispatches);
        Assert.Equal(first.ActionHistory, second.ActionHistory);
        Assert.Equal(first.Observations.Length, second.Observations.Length);
        for (var index = 0; index < first.Observations.Length; index++)
            AssertSameObservation(first.Observations[index], second.Observations[index]);
    }

    private static SiblingBranchProgressFixture Create(string path) => path switch
    {
        "complete" => SiblingBranchProgressFixture.Complete(),
        "a-only" => SiblingBranchProgressFixture.AOnly(),
        "early-return" => SiblingBranchProgressFixture.EarlyReturn(),
        "revisit" => SiblingBranchProgressFixture.RevisitA(),
        "stale" => SiblingBranchProgressFixture.StaleParent(),
        "wrong-parent" => SiblingBranchProgressFixture.WrongParent(),
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown path."),
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
