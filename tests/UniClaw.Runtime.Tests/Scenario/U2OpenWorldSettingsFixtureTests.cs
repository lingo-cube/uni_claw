using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class U2OpenWorldSettingsFixtureTests
{
    [Fact]
    public async Task PositiveWorld_ReplaysSameDynamicSiblingAndParentReturnSequence()
    {
        var first = await ReplayPositiveWorldAsync();
        var second = await ReplayPositiveWorldAsync();

        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(first.Observations.Count, second.Observations.Count);
        for (var index = 0; index < first.Observations.Count; index++)
        {
            Assert.Equal(first.Observations[index].ForegroundApplication, second.Observations[index].ForegroundApplication);
            Assert.Equal(first.Observations[index].SequenceNumber, second.Observations[index].SequenceNumber);
            Assert.Equal(first.Observations[index].Elements.ToArray(), second.Observations[index].Elements.ToArray());
        }
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(1),
                new DeviceAction.Tap(0),
            },
            first.Actions);
        Assert.Contains(first.Observations[0].Elements, element => element.Text == U2OpenWorldSettingsFixture.DangerousCandidate);
        Assert.Contains(first.Observations[1].Elements, element => element.Text == U2OpenWorldSettingsFixture.DeeperCandidate);
        Assert.Contains(first.Observations[3].Elements, element => element.Text == U2OpenWorldSettingsFixture.DeeperCandidate);
    }

    [Fact]
    public async Task AmbiguousAndWrongReturnWorlds_AreIndependentlyScripted()
    {
        var ambiguous = U2OpenWorldSettingsFixture.AmbiguousParentReturn();
        await ambiguous.Environment.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        _ = await ambiguous.Environment.ObserveAsync(CancellationToken.None);
        await ambiguous.Environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var ambiguousChild = await ambiguous.Environment.ObserveAsync(CancellationToken.None);
        Assert.Equal(2, ambiguousChild.Elements.Count(element => element.Text == U2OpenWorldSettingsFixture.RootPage));

        var wrong = U2OpenWorldSettingsFixture.WrongParentReturn();
        await wrong.Environment.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        _ = await wrong.Environment.ObserveAsync(CancellationToken.None);
        await wrong.Environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        _ = await wrong.Environment.ObserveAsync(CancellationToken.None);
        await wrong.Environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var wrongParent = await wrong.Environment.ObserveAsync(CancellationToken.None);
        Assert.Equal("OtherRoot", U2OpenWorldSettingsFixture.ResolveSemanticPage(wrongParent));
    }

    [Fact]
    public async Task StaleAndUnresolvedWorlds_DoNotFabricateFreshOrCompleteEvidence()
    {
        var stale = U2OpenWorldSettingsFixture.StaleChildObservation();
        await stale.Environment.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        _ = await stale.Environment.ObserveAsync(CancellationToken.None);
        var root = await stale.Environment.ObserveAsync(CancellationToken.None);
        await stale.Environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var child = await stale.Environment.ObserveAsync(CancellationToken.None);
        Assert.Equal(root.SequenceNumber, child.SequenceNumber);

        var unresolved = U2OpenWorldSettingsFixture.UnresolvedRoot();
        await unresolved.Environment.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        var observation = await unresolved.Environment.ObserveAsync(CancellationToken.None);
        var inventory = U2OpenWorldSettingsFixture.EvaluateInventory([observation], 0);
        Assert.Null(inventory.RequiredBranchEvidence);
        Assert.Contains("does not prove", inventory.Reason, StringComparison.Ordinal);
    }

    private static async Task<(
        IReadOnlyList<DeviceAction> Actions,
        IReadOnlyList<Observation> Observations)> ReplayPositiveWorldAsync()
    {
        var fixture = U2OpenWorldSettingsFixture.Positive();
        var environment = fixture.Environment;
        await environment.ExecuteAsync(new DeviceAction.LaunchApp("Settings"), CancellationToken.None);
        _ = await environment.ObserveAsync(CancellationToken.None);
        await environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        _ = await environment.ObserveAsync(CancellationToken.None);
        await environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        _ = await environment.ObserveAsync(CancellationToken.None);
        await environment.ExecuteAsync(new DeviceAction.Tap(1), CancellationToken.None);
        _ = await environment.ObserveAsync(CancellationToken.None);
        await environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        _ = await environment.ObserveAsync(CancellationToken.None);
        return (environment.ActionHistory, environment.ObservationHistory);
    }
}
