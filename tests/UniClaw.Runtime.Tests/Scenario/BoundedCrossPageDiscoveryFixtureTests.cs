using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class BoundedCrossPageDiscoveryFixtureTests
{
    [Fact]
    public void ExistingGoalConstruction_RemainsCompatibleAndHasNoInventoryCriterion()
    {
        var goal = new Goal(_ => new GoalEvidence(false, "not complete", null));

        Assert.Null(goal.BranchInventoryEvaluator);
    }

    [Fact]
    public async Task Positive_ExpressesPToAToCWithoutConcreteTargetsInPlan()
    {
        var evidence = await BoundedCrossPageDiscoveryFixture.Positive().RunAsync();

        Assert.DoesNotContain(evidence.InitialPlan.Steps, step =>
            step.TargetDescription.Contains("Branch A", StringComparison.Ordinal)
            || step.TargetDescription.Contains("Branch C", StringComparison.Ordinal));
        Assert.Equal(new[] { "Branch A", "Branch C", "Bounded leaf" },
            evidence.Observations.Select(observation => observation.Elements[0].Text));
        Assert.Equal("Branch A", Assert.Single(evidence.Inventories[0].RequiredBranchEvidence!).Key);
        Assert.Equal("Branch C", Assert.Single(evidence.Inventories[1].RequiredBranchEvidence!).Key);
        Assert.Empty(evidence.Inventories[2].RequiredBranchEvidence!);
        Assert.Equal(new DeviceAction[] { new DeviceAction.Tap(0), new DeviceAction.Tap(0) }, evidence.ActionHistory);
        Assert.All(evidence.Dispatches, result => Assert.Equal(ActionResultOutcome.Dispatched, result.Outcome));
    }

    [Fact]
    public async Task UnresolvedAndDepthBound_DoNotFabricateEmptyInventory()
    {
        var unresolved = await BoundedCrossPageDiscoveryFixture.Unresolved().RunAsync();
        var depthBound = await BoundedCrossPageDiscoveryFixture.DepthBound().RunAsync();

        Assert.Null(Assert.Single(unresolved.Inventories).RequiredBranchEvidence);
        Assert.Null(Assert.Single(depthBound.Inventories).RequiredBranchEvidence);
        Assert.Contains("depth boundary", depthBound.Inventories[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(unresolved.ActionHistory);
        Assert.Empty(depthBound.ActionHistory);
    }

    [Fact]
    public async Task ViewportMovement_ProvidesFreshEvidenceWithoutConsumingSemanticDepth()
    {
        var evidence = await BoundedCrossPageDiscoveryFixture.ViewportSameContainer().RunAsync();

        Assert.Equal(new long[] { 1, 2 }, evidence.Observations.Select(observation => observation.SequenceNumber));
        Assert.All(evidence.Inventories, inventory =>
            Assert.Equal("Branch A", Assert.Single(inventory.RequiredBranchEvidence!).Key));
        Assert.IsType<DeviceAction.ScrollForward>(Assert.Single(evidence.ActionHistory));
    }

    [Fact]
    public async Task StaleAndConflictingEvidence_RemainDeterministicallyDistinguishable()
    {
        var stale = await BoundedCrossPageDiscoveryFixture.StaleChild().RunAsync();
        var conflicting = await BoundedCrossPageDiscoveryFixture.ConflictingChild().RunAsync();

        Assert.Equal(stale.Observations[0].SequenceNumber, stale.Observations[1].SequenceNumber);
        Assert.NotEqual("Settings", conflicting.Observations[1].ForegroundApplication);
        Assert.Null(conflicting.Inventories[1].RequiredBranchEvidence);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("unresolved")]
    [InlineData("depth-bound")]
    [InlineData("viewport")]
    [InlineData("stale")]
    [InlineData("conflict")]
    public async Task SameInputs_ReplayEqualWorldAndInventoryEvidence(string path)
    {
        var first = await RunAsync(path);
        var second = await RunAsync(path);

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.InitialPlan.Steps.ToArray(), second.InitialPlan.Steps.ToArray());
        Assert.Equal(first.Inventories.Length, second.Inventories.Length);
        for (var index = 0; index < first.Inventories.Length; index++)
        {
            Assert.Equal(first.Inventories[index].Reason, second.Inventories[index].Reason);
            AssertEvidenceMapEqual(
                first.Inventories[index].RequiredBranchEvidence,
                second.Inventories[index].RequiredBranchEvidence);
        }
        Assert.Equal(first.Dispatches.ToArray(), second.Dispatches.ToArray());
        Assert.Equal(first.ActionHistory.ToArray(), second.ActionHistory.ToArray());
        Assert.Equal(first.Observations.Length, second.Observations.Length);
        for (var index = 0; index < first.Observations.Length; index++)
        {
            Assert.Equal(first.Observations[index].ForegroundApplication, second.Observations[index].ForegroundApplication);
            Assert.Equal(first.Observations[index].SequenceNumber, second.Observations[index].SequenceNumber);
            Assert.Equal(first.Observations[index].Elements, second.Observations[index].Elements);
        }
    }

    private static Task<BoundedCrossPageFixtureEvidence> RunAsync(string path) => path switch
    {
        "positive" => BoundedCrossPageDiscoveryFixture.Positive().RunAsync(),
        "unresolved" => BoundedCrossPageDiscoveryFixture.Unresolved().RunAsync(),
        "depth-bound" => BoundedCrossPageDiscoveryFixture.DepthBound().RunAsync(),
        "viewport" => BoundedCrossPageDiscoveryFixture.ViewportSameContainer().RunAsync(),
        "stale" => BoundedCrossPageDiscoveryFixture.StaleChild().RunAsync(),
        "conflict" => BoundedCrossPageDiscoveryFixture.ConflictingChild().RunAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    private static void AssertEvidenceMapEqual(
        IReadOnlyDictionary<string, long>? expected,
        IReadOnlyDictionary<string, long>? actual)
    {
        if (expected is null || actual is null)
        {
            Assert.Equal(expected is null, actual is null);
            return;
        }

        Assert.Equal(expected.OrderBy(entry => entry.Key), actual.OrderBy(entry => entry.Key));
    }
}
