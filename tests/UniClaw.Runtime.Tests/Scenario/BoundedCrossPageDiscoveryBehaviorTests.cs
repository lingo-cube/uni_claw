using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class BoundedCrossPageDiscoveryBehaviorTests
{
    [Fact]
    public async Task Positive_DiscoversPToAToCOneFreshAuthorizedTapAtATime()
    {
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.Positive());

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            fixture.Environment.ActionHistory);
        Assert.Equal(2, fixture.Traversal.Journal.Count);
        Assert.All(fixture.Traversal.Journal, entry => Assert.IsType<DeviceAction.Tap>(entry.DispatchedAction));
        Assert.Equal(new[] { "ChildA", "ChildC", "ParentP" }, fixture.Agent.BranchProgress.Keys.OrderBy(key => key));
        Assert.Empty(fixture.Agent.BranchProgress["ChildC"].ApprovedSiblingEvidence);
        Assert.Contains(fixture.Agent.Trace, entry =>
            entry.Reason?.StartsWith("branch inventory leaf", StringComparison.Ordinal) is true);
        Assert.True(fixture.Agent.Trace[^1].RunState == RunState.Completed);
    }

    [Fact]
    public async Task UnresolvedInventory_ProducesZeroDiscoveredBranchDispatchAndNoCompletion()
    {
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.Unresolved());

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Single(fixture.Environment.ActionHistory);
        Assert.IsType<DeviceAction.LaunchApp>(fixture.Environment.ActionHistory[0]);
        Assert.Empty(fixture.Traversal.Journal);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Contains("inventory unresolved", fixture.Agent.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequiredButRejected_ProducesZeroTapAndAuthorizedNonRequiredCandidateIsNotEvaluated()
    {
        var evaluated = new List<string>();
        CandidateAuthorizationEvidence RejectRequired(Observation _, ObservedElement candidate)
        {
            evaluated.Add(candidate.Text);
            return new CandidateAuthorizationEvidence(
                candidate.Text == "Optional candidate X" ? true : false,
                candidate.Text == "Optional candidate X"
                    ? "X is executable but is not required."
                    : "Required branch rejected by bounded intent.");
        }
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.Positive(),
            RejectRequired);

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new[] { "Branch A" }, evaluated);
        Assert.Single(fixture.Environment.ActionHistory);
        Assert.Empty(fixture.Traversal.Journal);
        Assert.Contains(fixture.Agent.Trace, entry =>
            entry.Reason?.Contains("required branch authorization rejected", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task InvalidSourceEvidence_DoesNotReplaceProgressOrDispatch()
    {
        BranchInventoryEvidence InvalidSource(ImmutableArray<Observation> _, int __)
            => new(
                ImmutableDictionary<string, long>.Empty.Add("Branch A", 999),
                "Invalid source sequence for proof test.");
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.Positive(),
            inventoryEvaluator: InvalidSource);

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Single(fixture.Environment.ActionHistory);
        Assert.Empty(fixture.Traversal.Journal);
        Assert.Empty(fixture.Agent.BranchProgress);
        Assert.Contains("does not reference accepted source evidence", fixture.Agent.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DepthBound_DoesNotDispatchVisibleDeeperChild()
    {
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.DepthBoundRoute());

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(3, fixture.Environment.ActionHistory.Count);
        Assert.Equal(2, fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Equal(2, fixture.Traversal.Journal.Count);
        Assert.Contains("depth boundary", fixture.Agent.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Traversal.Journal, entry =>
            entry.DispatchedAction is DeviceAction.Tap && entry.SelectedElementIndex != 0);
    }

    [Fact]
    public async Task SameContainerViewportMovement_DoesNotConsumeSemanticDepth()
    {
        var depths = new List<int>();
        BranchInventoryEvidence Inventory(ImmutableArray<Observation> observations, int depth)
        {
            depths.Add(depth);
            return observations.Length == 1
                ? new BranchInventoryEvidence(null, "Need one bounded same-Container viewport movement.")
                : BoundedCrossPageDiscoveryFixture.EvaluateInventory(observations, depth);
        }
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.ViewportSameContainer(),
            authorizationEvaluator: (_, _) => new CandidateAuthorizationEvidence(false, "Stop after depth proof."),
            viewportEvaluator: _ => new ViewportExplorationEvidence(true, "One movement is positively required."),
            inventoryEvaluator: Inventory,
            plan: new Plan([new PlanStep("Viewport", "ScrollForward")]));

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new[] { 0, 0 }, depths);
        Assert.Equal(2, fixture.Environment.ActionHistory.Count);
        Assert.IsType<DeviceAction.ScrollForward>(fixture.Environment.ActionHistory[1]);
        Assert.Single(fixture.Traversal.Journal);
        Assert.DoesNotContain(fixture.Environment.ActionHistory, action => action is DeviceAction.Tap);
    }
}
