using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>SC-U3-F1-001 formal production-shaped UI-order variation proof.</summary>
public sealed class U3F1WifiVariationScenarioTests
{
    [Fact]
    public async Task AlreadyOn_ChangedLayoutCompletesFromFreshGoalEvidenceWithoutMutation()
    {
        var run = U3F1WifiVariationFixture.Create(U3F1WifiVariationWorld.AlreadyOnLayoutVariant);

        var state = await run.RunAsync("u3-f1-already-on");

        Assert.Equal(U3F1WifiVariationFixture.Intent, run.Envelope.Intent);
        var closedWorld = Assert.IsType<IntentExecutionRepresentation.ClosedWorldConcrete>(run.Envelope.Representation);
        Assert.Same(run.Plan, closedWorld.Plan);
        Assert.Equal(RunState.Completed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        var evidence = Assert.Single(run.GoalEvidence);
        Assert.True(evidence.Satisfied);
        Assert.Equal(2, evidence.SourceObservationSequence);
        Assert.DoesNotContain(run.Agent.Trace, item => item.Action is not null);
    }

    [Fact]
    public async Task Off_ReorderedSimilarCandidatesGroundsCurrentWifiIndexAndCompletesFromFreshEvidence()
    {
        var run = U3F1WifiVariationFixture.Create(U3F1WifiVariationWorld.OffReordered);

        var state = await run.RunAsync("u3-f1-reordered-off");

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(new[] { 0, 1, 2 }, run.SafetyOrder);
        Assert.Equal(new[] { 0, 1, 2 }, run.GroundingOrder);
        Assert.Equal(new DeviceAction.Tap(2), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        Assert.Equal(new DeviceAction.SetSwitch(1, true), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>()));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, run.Environment.ObservationHistory.Select(item => item.SequenceNumber));
        Assert.Equal(2, run.Traversal.Journal.Count);
        Assert.Equal(2, run.Traversal.Journal[0].SelectedElementIndex);
        Assert.Equal(3, run.Traversal.Journal[0].PostActionObservation!.SequenceNumber);
        Assert.Equal(1, run.Traversal.Journal[1].SelectedElementIndex);
        Assert.Equal(4, run.Traversal.Journal[1].PostActionObservation!.SequenceNumber);
        Assert.Equal(new long[] { 3 }, run.PostActionEvidenceSequences);
        Assert.Equal(3, run.GoalEvidence.Count);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.False(run.GoalEvidence[1].Satisfied);
        Assert.True(run.GoalEvidence[2].Satisfied);
        Assert.Equal(4, run.GoalEvidence[2].SourceObservationSequence);
        Assert.Equal(RunState.Completed, run.Agent.Trace[^1].RunState);
    }

    [Fact]
    public async Task ReorderedButAmbiguous_PreservesInsufficiencyWithoutDispatchOrCompletion()
    {
        var run = U3F1WifiVariationFixture.Create(U3F1WifiVariationWorld.AmbiguousReordered);

        var state = await run.RunAsync("u3-f1-reordered-ambiguous");

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new[] { 0, 1, 2 }, run.SafetyOrder);
        Assert.Equal(new[] { 0, 1, 2 }, run.GroundingOrder);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        var journal = Assert.Single(run.Traversal.Journal);
        Assert.Null(journal.DispatchedAction);
        Assert.IsType<TraversalStepResult.Failed>(journal.Result);
        Assert.Contains("ambiguous", run.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(run.GoalEvidence);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.DoesNotContain(run.Agent.Trace, item => item.RunState == RunState.Completed);
    }

    [Fact]
    public async Task EqualInputsReplayEqualProjectionEvaluationsActionsEvidenceAndFinalState()
    {
        async Task<ReplayReceipt> ExecuteAsync()
        {
            var run = U3F1WifiVariationFixture.Create(U3F1WifiVariationWorld.OffReordered);
            var state = await run.RunAsync("u3-f1-replay");
            return new ReplayReceipt(
                run.Envelope.Intent,
                state,
                run.SafetyOrder.ToArray(),
                run.GroundingOrder.ToArray(),
                run.Environment.ActionHistory.ToArray(),
                run.Environment.ObservationHistory.Select(item =>
                    $"{item.SequenceNumber}|{item.ForegroundApplication}|{string.Join(",", item.Elements.Select(element => $"{element.Index}:{element.Text}:{element.SwitchState}"))}").ToArray(),
                run.Traversal.Journal.Select(item =>
                    $"{item.StepId}|{item.SelectedElementIndex}|{item.DispatchedAction}|{item.PostActionObservation?.SequenceNumber}|{item.Result}|{item.RetryCount}").ToArray(),
                run.Agent.Trace.Select(item =>
                    $"{item.RunState}|{item.Reason}|{item.Action}|{item.StepId}|{item.ActionId}|{item.ContainerId}").ToArray(),
                run.GoalEvidence.ToArray(),
                run.PostActionEvidenceSequences.ToArray(),
                run.Agent.Reason);
        }

        var first = await ExecuteAsync();
        var second = await ExecuteAsync();

        Assert.Equal(first.Intent, second.Intent);
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.SafetyOrder, second.SafetyOrder);
        Assert.Equal(first.GroundingOrder, second.GroundingOrder);
        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(first.Observations, second.Observations);
        Assert.Equal(first.Journal, second.Journal);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
        Assert.Equal(first.PostActionEvidenceSequences, second.PostActionEvidenceSequences);
        Assert.Equal(first.Reason, second.Reason);
    }

    private sealed record ReplayReceipt(
        string Intent,
        RunState State,
        int[] SafetyOrder,
        int[] GroundingOrder,
        DeviceAction[] Actions,
        string[] Observations,
        string[] Journal,
        string[] Trace,
        GoalEvidence[] GoalEvidence,
        long[] PostActionEvidenceSequences,
        string? Reason);
}
