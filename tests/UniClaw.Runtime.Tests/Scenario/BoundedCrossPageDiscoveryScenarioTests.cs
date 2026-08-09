using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Formal SC-P3-CAND-008 end-to-end Scenario and deterministic replay evidence.</summary>
public sealed class BoundedCrossPageDiscoveryScenarioTests
{
    [Fact]
    public async Task FormalPositive_InventoryAuthorizationFreshTransitionAndGoalEvidenceRemainDistinct()
    {
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.Positive());

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Completed, state);
        Assert.DoesNotContain(fixture.Plan.Steps, step =>
            step.TargetDescription is "Branch A" or "Branch C");
        Assert.Equal(new long[] { 1, 2, 3, 4 },
            fixture.Environment.ObservationHistory.Select(observation => observation.SequenceNumber));
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            fixture.Environment.ActionHistory);
        Assert.Equal(2, fixture.Traversal.Journal.Count);
        Assert.All(fixture.Traversal.Journal, entry =>
        {
            Assert.IsType<DeviceAction.Tap>(entry.DispatchedAction);
            Assert.IsType<TraversalStepResult.Succeeded>(entry.Result);
            Assert.NotNull(entry.PostActionObservation);
        });

        var inventoryTrace = fixture.Agent.Trace
            .Where(entry => entry.Reason?.StartsWith("branch inventory ", StringComparison.Ordinal) is true)
            .ToArray();
        Assert.Equal(3, inventoryTrace.Length);
        Assert.Contains("complete: depth=0", inventoryTrace[0].Reason!, StringComparison.Ordinal);
        Assert.Contains("complete: depth=1", inventoryTrace[1].Reason!, StringComparison.Ordinal);
        Assert.Contains("leaf: depth=2", inventoryTrace[2].Reason!, StringComparison.Ordinal);
        Assert.All(inventoryTrace, entry =>
        {
            Assert.Null(entry.Action);
            Assert.Null(entry.ActionId);
        });
        Assert.Equal(2, fixture.Agent.Trace.Count(entry => entry.Action is DeviceAction.Tap));
        Assert.Equal(RunState.Completed, fixture.Agent.Trace[^1].RunState);
        Assert.Contains("independently evidence-controlled", fixture.Agent.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task RequiredRejectedOrUnresolved_HasZeroTapAndNoFabricatedCompletion(bool? authorization)
    {
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.Positive(),
            authorizationEvaluator: (_, _) => new CandidateAuthorizationEvidence(
                authorization,
                authorization is false ? "Required branch rejected." : "Required branch unresolved."));

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.DoesNotContain(fixture.Environment.ActionHistory, action => action is DeviceAction.Tap);
        Assert.Empty(fixture.Traversal.Journal);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.False(fixture.Agent.BranchProgress["ParentP"].IsSubtreeComplete);
    }

    [Fact]
    public async Task PositiveEmptyLeaf_WithoutIndependentGoalEvidence_DoesNotComplete()
    {
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(
            BoundedCrossPageDiscoveryFixture.Positive(),
            goalEvidenceEvaluator: observation => new GoalEvidence(
                false,
                "Independent GoalEvidence deliberately remains unsatisfied.",
                observation.SequenceNumber));

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(2, fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(fixture.Agent.Trace, entry =>
            entry.Reason?.StartsWith("branch inventory leaf", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Contains("GoalEvidence remains unsatisfied", fixture.Agent.Reason!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("conflict")]
    public async Task StaleOrConflictingPostDispatchEvidence_PreservesValidParentInventoryAndNeverRedispatches(string branch)
    {
        var world = branch == "stale"
            ? BoundedCrossPageDiscoveryFixture.StaleChildAfterStartup()
            : BoundedCrossPageDiscoveryFixture.ConflictingChildAfterStartup();
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(world);

        var state = await fixture.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Single(fixture.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Single(fixture.Traversal.Journal);
        Assert.True(fixture.Agent.BranchProgress.TryGetValue("ParentP", out var parent));
        Assert.Equal("Branch A", Assert.Single(parent!.ApprovedSiblingEvidence).Key);
        Assert.Empty(parent.CompletedSiblingEvidence);
        Assert.DoesNotContain(fixture.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public void FreshInventoryRefresh_PreservesOnlyStillRequiredCompletedSiblingEvidence()
    {
        var prior = new BranchProgressEvidence(
            "ParentP",
            ImmutableDictionary<string, long>.Empty.Add("A", 4).Add("Legacy", 4),
            ImmutableDictionary<string, long>.Empty.Add("A", 5).Add("Legacy", 5));
        var freshInventory = new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty.Add("A", 9).Add("B", 9),
            "Fresh complete parent inventory.");
        var preserved = prior.CompletedSiblingEvidence
            .Where(entry => freshInventory.RequiredBranchEvidence!.ContainsKey(entry.Key))
            .ToImmutableDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var refreshed = new BranchProgressEvidence(
            "ParentP",
            freshInventory.RequiredBranchEvidence!,
            preserved);

        Assert.Equal(5, Assert.Single(refreshed.CompletedSiblingEvidence).Value);
        Assert.True(refreshed.CompletedSiblingEvidence.ContainsKey("A"));
        Assert.False(refreshed.CompletedSiblingEvidence.ContainsKey("Legacy"));
        Assert.True(refreshed.ApprovedSiblingEvidence.ContainsKey("B"));
        Assert.False(refreshed.IsSubtreeComplete);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("unresolved")]
    [InlineData("depth-bound")]
    [InlineData("stale")]
    [InlineData("conflict")]
    public async Task EqualInputs_ReplayEqualInventoryProgressActionsJournalTraceAndRunState(string branch)
    {
        var first = await RunSnapshotAsync(branch);
        var second = await RunSnapshotAsync(branch);

        Assert.Equal(first, second);
    }

    private static async Task<DiscoveryReplaySnapshot> RunSnapshotAsync(string branch)
    {
        var world = branch switch
        {
            "positive" => BoundedCrossPageDiscoveryFixture.Positive(),
            "unresolved" => BoundedCrossPageDiscoveryFixture.Unresolved(),
            "depth-bound" => BoundedCrossPageDiscoveryFixture.DepthBoundRoute(),
            "stale" => BoundedCrossPageDiscoveryFixture.StaleChildAfterStartup(),
            "conflict" => BoundedCrossPageDiscoveryFixture.ConflictingChildAfterStartup(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
        var fixture = BoundedCrossPageDiscoveryRunFixture.Create(world);
        var state = await fixture.RunAsync();
        return new DiscoveryReplaySnapshot(
            state,
            fixture.Agent.Reason,
            string.Join("\n", fixture.Environment.ActionHistory.Select(CanonicalAction)),
            string.Join("\n", fixture.Environment.ObservationHistory.Select(CanonicalObservation)),
            string.Join("\n", fixture.Traversal.Journal.Select(CanonicalJournal)),
            string.Join("\n", fixture.Agent.Trace.Select(CanonicalTrace)),
            string.Join("\n", fixture.Agent.BranchProgress
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => CanonicalProgress(entry.Key, entry.Value))));
    }

    private static string CanonicalAction(DeviceAction action) => action switch
    {
        DeviceAction.LaunchApp launch => $"Launch:{launch.ApplicationId}",
        DeviceAction.Tap tap => $"Tap:{tap.TargetElementIndex}",
        DeviceAction.SetSwitch setSwitch => $"Set:{setSwitch.TargetElementIndex}:{setSwitch.TargetState}",
        DeviceAction.ScrollForward => "ScrollForward",
        _ => action.GetType().Name,
    };

    private static string CanonicalObservation(Observation observation)
        => $"{observation.SequenceNumber}|{observation.ForegroundApplication}|"
           + string.Join(",", observation.Elements.Select(element =>
               $"{element.Index}:{element.Text}:{element.SwitchState}"));

    private static string CanonicalJournal(UniClaw.Runtime.Traversal.TraversalJournalEntry entry)
        => $"{entry.StepId}|{entry.SelectedElementIndex}|{entry.DispatchedAction?.GetType().Name}|"
           + $"{entry.PostActionObservation?.SequenceNumber}|{entry.Result.GetType().Name}|{entry.RetryCount}";

    private static string CanonicalTrace(TraceEvent entry)
        => $"{entry.RunId}|{entry.ContainerId}|{entry.StepId}|{entry.ActionId}|{entry.Action?.GetType().Name}|"
           + $"{entry.Reason}|{entry.RunState}|{entry.TrapKind}|{entry.TrapScope}|{entry.RecoveryId}";

    private static string CanonicalProgress(string parent, BranchProgressEvidence progress)
        => $"{parent}|approved:{string.Join(",", progress.ApprovedSiblingEvidence.OrderBy(item => item.Key))}"
           + $"|completed:{string.Join(",", progress.CompletedSiblingEvidence.OrderBy(item => item.Key))}";

    private sealed record DiscoveryReplaySnapshot(
        RunState State,
        string? Reason,
        string Actions,
        string Observations,
        string Journal,
        string Trace,
        string Progress);
}
