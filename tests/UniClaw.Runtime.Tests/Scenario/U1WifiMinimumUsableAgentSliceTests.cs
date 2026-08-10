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

/// <summary>
/// U1 minimum usable task proof. This stays at the existing structured Goal + Plan boundary:
/// it deliberately does not synthesize a Goal or Plan from natural language (CP-14 remains deferred).
/// </summary>
public sealed class U1WifiMinimumUsableAgentSliceTests
{
    [Fact]
    public async Task AlreadyOn_NonEmptyPlan_CompletesFromInitialGoalEvidence_WithoutMutation()
    {
        var run = Create(WorldCase.AlreadyOn);

        var state = await run.RunAsync("u1-already-on");

        Assert.Equal(RunState.Completed, state);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        var evidence = Assert.Single(run.GoalEvidence);
        Assert.True(evidence.Satisfied);
        Assert.Equal(2, evidence.SourceObservationSequence);
        Assert.Equal(RunState.Completed, run.Agent.Trace[^1].RunState);
    }

    [Fact]
    public async Task Off_GroundedWifi_SafelyNavigatesThenEnablesWifi_AndCompletesOnlyFromFreshGoalEvidence()
    {
        var run = Create(WorldCase.Off);

        var state = await run.RunAsync("u1-off");

        Assert.Equal(RunState.Completed, state);
        Assert.Equal(new[] { 0, 1 }, run.SafetyReceiptOrder);
        Assert.Equal(new[] { 0, 1 }, run.GroundingOrder);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        Assert.Equal(new DeviceAction.SetSwitch(0, true), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>()));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, run.Environment.ObservationHistory.Select(item => item.SequenceNumber));
        Assert.Equal(2, run.Traversal.Journal.Count);
        Assert.Equal(3, run.Traversal.Journal[0].PostActionObservation!.SequenceNumber);
        Assert.Equal(4, run.Traversal.Journal[1].PostActionObservation!.SequenceNumber);
        Assert.Equal(3, run.GoalEvidence.Count);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.False(run.GoalEvidence[1].Satisfied);
        Assert.True(run.GoalEvidence[2].Satisfied);
        Assert.Equal(4, run.GoalEvidence[2].SourceObservationSequence);
        Assert.True(run.PostActionEvidenceSequences.All(sequence => sequence == 3));
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed && entry.Reason!.Contains("grounding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AmbiguousGrounding_PreservesAmbiguity_AndDoesNotDispatchOrComplete()
    {
        var run = Create(WorldCase.Ambiguous);

        var state = await run.RunAsync("u1-ambiguous");

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new[] { 0, 1 }, run.SafetyReceiptOrder);
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.Single(run.Traversal.Journal);
        Assert.Contains("ambiguous", run.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(run.GoalEvidence);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task TimedOutTapWithUnchangedWorld_FreshlyObservesButDoesNotRedispatchOrComplete()
    {
        var run = Create(WorldCase.TimedOutUnconfirmed);

        var state = await run.RunAsync("u1-timeout");

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        var journal = Assert.Single(run.Traversal.Journal);
        Assert.Equal(3, journal.PostActionObservation!.SequenceNumber);
        Assert.IsType<TraversalStepResult.Failed>(journal.Result);
        Assert.Contains("unconfirmed", run.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(run.GoalEvidence);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task WrongWifiCallingPostState_IsRejectedWithoutSwitchRedispatchOrCompletion()
    {
        var run = Create(WorldCase.WrongDestination);

        var state = await run.RunAsync("u1-wrong-destination");

        Assert.Equal(RunState.Failed, state);
        Assert.Equal(new DeviceAction.Tap(0), Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>()));
        Assert.Empty(run.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        var journal = Assert.Single(run.Traversal.Journal);
        Assert.Equal(3, journal.PostActionObservation!.SequenceNumber);
        Assert.Contains("rejected", run.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(run.GoalEvidence);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
    }

    [Fact]
    public async Task SameStructuredTaskAndWorld_ReplaySameTraceJournalActionsObservationsAndGoalEvidence()
    {
        async Task<Receipt> ExecuteAsync()
        {
            var run = Create(WorldCase.Off);
            var state = await run.RunAsync("u1-replay");
            return new Receipt(
                state,
                run.Agent.Trace.Select(item => (item.RunState, item.Reason, item.Action, item.StepId)).ToArray(),
                run.Traversal.Journal.Select(item => (item.SelectedElementIndex, item.DispatchedAction, item.PostActionObservation?.SequenceNumber, item.Result)).ToArray(),
                run.Environment.ActionHistory.ToArray(),
                run.Environment.ObservationHistory.Select(item => item.SequenceNumber).ToArray(),
                run.GoalEvidence.ToArray(),
                run.SafetyReceiptOrder.ToArray(),
                run.GroundingOrder.ToArray());
        }

        var first = await ExecuteAsync();
        var second = await ExecuteAsync();

        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.Journal, second.Journal);
        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(first.Observations, second.Observations);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
        Assert.Equal(first.SafetyOrder, second.SafetyOrder);
        Assert.Equal(first.GroundingOrder, second.GroundingOrder);
    }

    private static Run Create(WorldCase worldCase)
    {
        var navigationOutcome = worldCase == WorldCase.TimedOutUnconfirmed
            ? ActionResultOutcome.TimedOut
            : ActionResultOutcome.Dispatched;
        var destination = worldCase switch
        {
            WorldCase.Off => "WifiOff",
            WorldCase.WrongDestination => "WifiCallingSettings",
            WorldCase.TimedOutUnconfirmed => "Candidates",
            WorldCase.Ambiguous => "WifiOff",
            WorldCase.AlreadyOn => "WifiOn",
            _ => throw new ArgumentOutOfRangeException(nameof(worldCase)),
        };
        bool? firstCandidateState = worldCase == WorldCase.Ambiguous ? null : false;
        var screens = new[]
        {
            new ScreenConfig("Launcher", "Launcher", []),
            new ScreenConfig("Candidates", "Settings", [
                new ElementConfig("Wi-Fi", firstCandidateState, new TransitionConfig(ScreenTransitionAction.Tap, destination, DispatchOutcome: navigationOutcome)),
                new ElementConfig("Wi-Fi Calling", null, new TransitionConfig(ScreenTransitionAction.Tap, "WifiCallingSettings"))]),
            new ScreenConfig("WifiOff", "Settings", [
                new ElementConfig("Wi-Fi", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "WifiOn", true))]),
            new ScreenConfig("WifiOn", "Settings", [new ElementConfig("Wi-Fi", true, null)]),
            new ScreenConfig("WifiCallingSettings", "Settings", [new ElementConfig("Wi-Fi Calling Settings", null, null)]),
        };
        var initialScreen = worldCase == WorldCase.AlreadyOn ? "WifiOn" : "Launcher";
        var launchScreen = worldCase == WorldCase.AlreadyOn ? "WifiOn" : "Candidates";
        var environment = new ScriptedEnvironment(initialScreen, launchScreen, screens);
        var traversal = new RuntimeTraversal(environment);
        var safetyReceiptOrder = new List<int>();
        var groundingOrder = new List<int>();
        var postActionEvidenceSequences = new List<long>();
        var goalEvidence = new List<GoalEvidence>();
        var goal = new Goal(
            observation =>
            {
                var satisfied = observation.Elements.Any(element => element.Text == "Wi-Fi" && element.SwitchState is true);
                var evidence = new GoalEvidence(satisfied, satisfied ? "Fresh Wi-Fi ON GoalEvidence." : "Wi-Fi ON is not yet evidenced.", observation.SequenceNumber);
                goalEvidence.Add(evidence);
                return evidence;
            },
            (observation, candidate) =>
            {
                safetyReceiptOrder.Add(candidate.Index);
                return new CandidateAuthorizationEvidence(
                    candidate.Text is "Wi-Fi" or "Wi-Fi Calling",
                    $"independent safety receipt for candidate={candidate.Text}, index={candidate.Index}, source-seq={observation.SequenceNumber}");
            });
        var criterion = new TargetGroundingCriterion(
            (_, candidate) =>
            {
                groundingOrder.Add(candidate.Index);
                // The discriminator is current observable state-bearing support, not a text-only match.
                var broadWifiTextMatch = candidate.Text.Contains("Wi-Fi", StringComparison.Ordinal);
                var supported = worldCase == WorldCase.Ambiguous
                    ? broadWifiTextMatch
                    : broadWifiTextMatch && candidate.SwitchState is false;
                return new TargetGroundingEvidence(
                    supported,
                    candidate.SwitchState is false
                        ? $"broad Wi-Fi text match for '{candidate.Text}' plus state-bearing SwitchState=false support"
                        : $"broad Wi-Fi text match for '{candidate.Text}' lacks required state-bearing SwitchState=false support");
            },
            observation =>
            {
                // Deliberately derives only from the supplied fresh post-action Observation.
                postActionEvidenceSequences.Add(observation.SequenceNumber);
                return observation.Elements.Any(element => element.Text == "Wi-Fi" && element.SwitchState is false)
                    && !observation.Elements.Any(element => element.Text == "Wi-Fi Calling")
                    ? new TargetGroundingEvidence(true, "fresh Wi-Fi settings evidence confirms expected local effect")
                    : observation.Elements.Any(element => element.Text == "Wi-Fi Calling Settings")
                        ? new TargetGroundingEvidence(false, "fresh Wi-Fi Calling Settings contradicts expected Wi-Fi destination")
                        : new TargetGroundingEvidence(null, "fresh Observation cannot confirm expected Wi-Fi settings effect");
            });
        var plan = new Plan([
            new PlanStep("Wi-Fi", "Tap", TargetGroundingCriterion: criterion),
            new PlanStep("Wi-Fi", "SetSwitch true"),
        ]);
        static string? Page(Observation observation)
        {
            if (!string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal))
                return null;
            if (observation.Elements.Any(element => element.Text == "Wi-Fi Calling Settings"))
                return "WifiCalling";
            // The candidate list has an explicit adjacent candidate marker. The settings page has
            // only the state-bearing Wi-Fi row, so post-action evidence cannot conflate them.
            if (observation.Elements.Any(element => element.Text == "Wi-Fi Calling"))
                return "Candidates";
            if (observation.Elements.Any(element => element.Text == "Wi-Fi"))
                return "WifiSettings";
            return null;
        }
        RuntimeContainer Factory(string page) => new(
            page,
            observation => Page(observation) == page,
            traversal.ExecuteStep,
            forwardsAuthorizationReceipts: true);
        var startup = new RuntimeStartup(environment, "Settings", Page);
        var recovery = new RuntimeRecovery(environment, _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, token => environment.ObserveAsync(token), Page, Factory, recovery);
        return new Run(agent, traversal, environment, goal, plan, goalEvidence, safetyReceiptOrder, groundingOrder, postActionEvidenceSequences);
    }

    private enum WorldCase { AlreadyOn, Off, Ambiguous, TimedOutUnconfirmed, WrongDestination }

    private sealed record Run(
        RuntimeAgent Agent,
        RuntimeTraversal Traversal,
        ScriptedEnvironment Environment,
        Goal Goal,
        Plan Plan,
        IReadOnlyList<GoalEvidence> GoalEvidence,
        IReadOnlyList<int> SafetyReceiptOrder,
        IReadOnlyList<int> GroundingOrder,
        IReadOnlyList<long> PostActionEvidenceSequences)
    {
        internal Task<RunState> RunAsync(string runId) => Agent.RunAsync(Goal, Plan, runId, CancellationToken.None);
    }

    private sealed record Receipt(
        RunState State,
        (RunState? State, string? Reason, DeviceAction? Action, string? StepId)[] Trace,
        (int? Selected, DeviceAction? Action, long? Observation, TraversalStepResult Result)[] Journal,
        DeviceAction[] Actions,
        long[] Observations,
        GoalEvidence[] GoalEvidence,
        int[] SafetyOrder,
        int[] GroundingOrder);
}
