using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>SC-CP12-MVS-001 production-shaped deterministic Wi-Fi versus Wi-Fi Calling proof.</summary>
public sealed class Cp12TargetGroundingScenarioTests
{
    [Fact]
    public async Task WifiGrounding_SelectsWifi_NotWifiCalling_AndCompletesOnlyFromGoalEvidence()
    {
        var run = Create(confirmation: true);

        var state = await run.Agent.RunAsync(run.Goal, run.Plan, "cp12-wifi", CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(new[] { 0, 1 }, run.CandidateEvaluationOrder);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        var journal = Assert.Single(run.Traversal.Journal);
        Assert.Equal(0, journal.SelectedElementIndex);
        Assert.Equal(3, journal.PostActionObservation!.SequenceNumber);
        Assert.True(journal.PostActionObservation.SequenceNumber > run.GoalEvidence[0].SourceObservationSequence);
        Assert.Equal(2, run.GoalEvidence.Count);
        Assert.True(run.GoalEvidence[^1].Satisfied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task ContradictedOrUnconfirmedFreshEffect_FailsOnceWithoutRedispatchOrCompletion(bool? confirmation)
    {
        var run = Create(confirmation);

        var state = await run.Agent.RunAsync(run.Goal, run.Plan, $"cp12-{confirmation}", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        var journal = Assert.Single(run.Traversal.Journal);
        Assert.Equal(0, journal.SelectedElementIndex);
        Assert.True(journal.PostActionObservation!.SequenceNumber > run.GoalEvidence[0].SourceObservationSequence);
        var failed = Assert.IsType<TraversalStepResult.Failed>(journal.Result);
        Assert.Contains(confirmation is false ? "rejected" : "unconfirmed", failed.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Single(run.GoalEvidence); // Initial GoalEvidence only; grounding never becomes GoalEvidence.
    }

    [Fact]
    public async Task UnsafeSafetyReceipt_PreventsDispatch()
    {
        var run = Create(confirmation: true, safeAuthorized: false);

        var state = await run.Agent.RunAsync(run.Goal, run.Plan, "cp12-unsafe", CancellationToken.None);

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Contains("not authorized", run.Agent.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EqualInputsReplayEqualGroundingReceiptJournalActionsObservationsGoalEvidenceAndState()
    {
        async Task<RunReceipt> ExecuteAsync()
        {
            var run = Create(confirmation: false);
            var state = await run.Agent.RunAsync(run.Goal, run.Plan, "cp12-replay", CancellationToken.None);
            return new RunReceipt(
                state,
                run.Agent.Trace.Select(trace => (trace.Reason, trace.RunState, trace.Action)).ToArray(),
                run.Traversal.Journal.Select(entry => (entry.SelectedElementIndex, entry.DispatchedAction, entry.PostActionObservation?.SequenceNumber, entry.Result)).ToArray(),
                run.Environment.ActionHistory.ToArray(),
                run.Environment.ObservationHistory.Select(observation => observation.SequenceNumber).ToArray(),
                run.GoalEvidence.ToArray());
        }

        var first = await ExecuteAsync();
        var second = await ExecuteAsync();
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.Journal, second.Journal);
        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(first.Observations, second.Observations);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
    }

    private static Run Create(bool? confirmation, bool safeAuthorized = true)
    {
        var screens = new[]
        {
            new ScreenConfig("Launcher", "Launcher", []),
            new ScreenConfig("Candidates", "Settings", [
                new ElementConfig("Wi-Fi", false, new TransitionConfig(ScreenTransitionAction.Tap, confirmation is true ? "WifiSettings" : confirmation is false ? "WifiCallingSettings" : "UnknownSettings")),
                new ElementConfig("Wi-Fi Calling", null, new TransitionConfig(ScreenTransitionAction.Tap, "WifiCallingSettings"))]),
            new ScreenConfig("WifiSettings", "Settings", [new ElementConfig("Wi-Fi Settings", null, null)]),
            new ScreenConfig("WifiCallingSettings", "Settings", [new ElementConfig("Wi-Fi Calling Settings", null, null)]),
            new ScreenConfig("UnknownSettings", "Settings", [new ElementConfig("Network destination unavailable", null, null)]),
        };
        var environment = new ScriptedEnvironment("Launcher", "Candidates", screens);
        var traversal = new RuntimeTraversal(environment);
        var goalEvidence = new List<GoalEvidence>();
        var candidateEvaluationOrder = new List<int>();
        static string? Page(Observation observation) => observation.Elements.Any(element => element.Text is "Wi-Fi" or "Wi-Fi Calling") ? "Candidates" : "Settings";
        var goal = new Goal(
            observation =>
            {
                var satisfied = observation.Elements.Any(element => element.Text == "Wi-Fi Settings");
                var evidence = new GoalEvidence(satisfied, satisfied ? "Goal evidence confirms Wi-Fi Settings." : "Goal remains unproven.", observation.SequenceNumber);
                goalEvidence.Add(evidence);
                return evidence;
            },
            (_, _) => new CandidateAuthorizationEvidence(safeAuthorized, safeAuthorized ? "independent safe navigation receipt" : "unsafe navigation receipt"));
        RuntimeContainer Factory(string page) => new(page, observation => Page(observation) == page, traversal.ExecuteStep, forwardsAuthorizationReceipts: true);
        var startup = new RuntimeStartup(environment, "Settings", Page);
        var recovery = new RuntimeRecovery(environment, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, token => environment.ObserveAsync(token), Page, Factory, recovery);
        var criterion = new TargetGroundingCriterion(
            (_, candidate) =>
            {
                candidateEvaluationOrder.Add(candidate.Index);
                var textMatches = candidate.Text.Contains("Wi-Fi", StringComparison.Ordinal);
                var navigationCompatibleSupport = candidate.SwitchState is not null;
                return new TargetGroundingEvidence(
                    textMatches && navigationCompatibleSupport,
                    navigationCompatibleSupport
                        ? $"text predicate matched '{candidate.Text}' and SwitchState provides the additional observable navigation-compatible support."
                        : $"text predicate matched '{candidate.Text}', but SwitchState is absent so observable support remains insufficient.");
            },
            observation => observation.Elements.Any(element => element.Text == "Wi-Fi Settings")
                ? new TargetGroundingEvidence(true, "fresh Wi-Fi Settings effect confirmed.")
                : observation.Elements.Any(element => element.Text == "Wi-Fi Calling Settings")
                    ? new TargetGroundingEvidence(false, "Wi-Fi Calling Settings contradicts the expected Wi-Fi Settings effect.")
                    : new TargetGroundingEvidence(null, "fresh destination is neither Wi-Fi Settings nor Wi-Fi Calling Settings."));
        return new Run(agent, traversal, environment, goal, new Plan([new PlanStep("Wi-Fi", "Tap", TargetGroundingCriterion: criterion)]), goalEvidence, candidateEvaluationOrder);
    }

    private sealed record Run(RuntimeAgent Agent, RuntimeTraversal Traversal, ScriptedEnvironment Environment, Goal Goal, Plan Plan, IReadOnlyList<GoalEvidence> GoalEvidence, IReadOnlyList<int> CandidateEvaluationOrder);
    private sealed record RunReceipt(
        RunState State,
        (string? Reason, RunState? RunState, DeviceAction? Action)[] Trace,
        (int? SelectedElementIndex, DeviceAction? DispatchedAction, long? SequenceNumber, TraversalStepResult Result)[] Journal,
        DeviceAction[] Actions,
        long[] Observations,
        GoalEvidence[] GoalEvidence);
}
