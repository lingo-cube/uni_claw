using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Formal end-to-end proof for SC-P3-CAND-007.</summary>
public sealed class ViewportExplorationScenarioTests
{
    private const string RunId = "sc-p3-cand-007-formal-run";

    [Fact]
    public async Task Positive_V1V2V3PreservesContainerProgressStopsAtPositiveEndAndCompletesFromGoalEvidence()
    {
        var run = await RunScenarioAsync("positive");

        Assert.Equal(RunState.Completed, run.State);
        var container = Assert.Single(run.Containers);
        Assert.Equal("ScrollableList", container.SemanticPageName);
        Assert.Equal(
            new[] { "A", "Viewport-1", "Viewport-2" },
            container.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(new long[] { 2, 4, 5 }, container.ViewportExplorationObservations.Select(item => item.SequenceNumber));
        Assert.Equal(
            new[]
            {
                new[] { "A", "B", "C", "More content" },
                new[] { "B", "C", "D", "More content" },
                new[] { "C", "D", "E", "End of list" },
            },
            container.ViewportExplorationObservations.Select(Texts));

        Assert.Equal(new bool?[] { true, true, false }, run.Decisions.Select(item => item.ContinueExploration));
        Assert.Equal(new[] { "continue", "continue", "exhausted" }, ExplorationTraceOutcomes(run.Agent));
        Assert.Equal(2, run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>().Count());
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.ScrollForward(),
                new DeviceAction.ScrollForward(),
            },
            run.Environment.ActionHistory);
        Assert.Equal(3, run.Traversal.Journal.Count);
        Assert.Equal(new long[] { 3, 4, 5 }, run.Traversal.Journal.Select(item => item.PostActionObservation!.SequenceNumber));
        Assert.Equal(new[] { false, false, true }, run.GoalEvidence.Select(item => item.Satisfied));
        Assert.Equal(run.GoalEvidence[^1].Reason, run.Agent.Reason);
        Assert.Null(run.Agent.LastTrap);
        Assert.DoesNotContain(run.Agent.Trace, item => item.RecoveryId is not null);

        var exhaustionIndex = Array.FindIndex(
            run.Agent.Trace.ToArray(),
            item => item.Reason?.StartsWith("viewport exploration exhausted", StringComparison.Ordinal) == true);
        var completionIndex = Array.FindIndex(
            run.Agent.Trace.ToArray(),
            item => item.RunState == RunState.Completed);
        Assert.True(exhaustionIndex >= 0 && completionIndex > exhaustionIndex);
    }

    [Fact]
    public async Task AmbiguousSameEvidenceIsUnresolvedAndCannotDispatchAgainOrComplete()
    {
        var run = await RunScenarioAsync("ambiguous");

        Assert.Equal(RunState.Failed, run.State);
        var container = Assert.Single(run.Containers);
        Assert.Equal(new bool?[] { true, null }, run.Decisions.Select(item => item.ContinueExploration));
        Assert.Equal(2, container.ViewportExplorationObservations.Length);
        Assert.Equal(Texts(container.ViewportExplorationObservations[0]), Texts(container.ViewportExplorationObservations[1]));
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Contains("unresolved", run.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(run.Agent.Trace, item => item.RunState == RunState.Completed);
    }

    [Fact]
    public async Task BoundReachedWithContinuationEvidenceRemainsIncompleteNotExhausted()
    {
        var run = await RunScenarioAsync("bound");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Equal(new bool?[] { true, true }, run.Decisions.Select(item => item.ContinueExploration));
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Contains("bound reached", run.Agent.Reason, StringComparison.Ordinal);
        Assert.Contains("semantic exhaustion 未获证明", run.Agent.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(
            run.Agent.Trace,
            item => item.Reason?.StartsWith("viewport exploration exhausted", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("stale")]
    [InlineData("page-changed")]
    public async Task DispatchOrContinuityFailurePreservesAcceptedHistoryAndNeverBecomesExhaustion(string branch)
    {
        var run = await RunScenarioAsync(branch);

        Assert.Equal(RunState.Failed, run.State);
        var original = run.Containers[0];
        Assert.Single(original.ViewportExplorationObservations);
        Assert.Equal(new[] { "A", "Viewport-1" }, original.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.DoesNotContain(
            run.Agent.Trace,
            item => item.Reason?.StartsWith("viewport exploration exhausted", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(run.Agent.Trace, item => item.RunState == RunState.Completed);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("ambiguous")]
    [InlineData("bound")]
    [InlineData("rejected")]
    [InlineData("stale")]
    [InlineData("page-changed")]
    public async Task DeterministicReplay_EqualInputsReplayFormalEvidence(string branch)
    {
        var first = await RunScenarioAsync(branch);
        var second = await RunScenarioAsync(branch);

        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Agent.Reason, second.Agent.Reason);
        Assert.Equal(first.Agent.Belief, second.Agent.Belief);
        Assert.Equal(first.Agent.LastTrap, second.Agent.LastTrap);
        Assert.Equal(first.Agent.Trace, second.Agent.Trace);
        Assert.Equal(first.Environment.ActionHistory, second.Environment.ActionHistory);
        AssertObservationHistory(first.Environment.ObservationHistory, second.Environment.ObservationHistory);
        AssertJournal(first.Traversal.Journal, second.Traversal.Journal);
        Assert.Equal(first.Decisions, second.Decisions);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
        Assert.Equal(first.Containers.Length, second.Containers.Length);
        for (var index = 0; index < first.Containers.Length; index++)
        {
            Assert.Equal(first.Containers[index].SemanticPageName, second.Containers[index].SemanticPageName);
            Assert.Equal(first.Containers[index].ExecutedSteps, second.Containers[index].ExecutedSteps);
            AssertObservationHistory(
                first.Containers[index].ViewportExplorationObservations,
                second.Containers[index].ViewportExplorationObservations);
        }
    }

    private static async Task<FormalRun> RunScenarioAsync(string branch)
    {
        var environment = branch switch
        {
            "positive" or "bound" => ScriptedEnvironmentVariants.ViewportExplorationPositive(),
            "ambiguous" => ScriptedEnvironmentVariants.ViewportExplorationAmbiguousSame(),
            "rejected" => ScriptedEnvironmentVariants.ViewportExplorationRejected(),
            "stale" => ScriptedEnvironmentVariants.ViewportExplorationFormalStale(),
            "page-changed" => ScriptedEnvironmentVariants.ViewportExplorationPageChanged(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ResolvePage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        var decisions = new List<ViewportExplorationEvidence>();
        var goalEvidence = new List<GoalEvidence>();

        RuntimeContainer CreateContainer(string page)
        {
            var container = new RuntimeContainer(
                page,
                observation => string.Equals(ResolvePage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }

        ViewportExplorationEvidence Evaluate(ImmutableArray<Observation> observations)
        {
            var result = ViewportExplorationFixture.Evaluate(observations);
            decisions.Add(result);
            return result;
        }

        var goal = new Goal(
            observation =>
            {
                var satisfied = branch == "positive"
                    && observation.Elements.Any(element => element.Text == "End of list");
                var evidence = new GoalEvidence(
                    satisfied,
                    satisfied
                        ? $"GoalEvidence independently proves all required bounded evidence at seq={observation.SequenceNumber}."
                        : $"GoalEvidence remains unsatisfied at seq={observation.SequenceNumber}.",
                    observation.SequenceNumber);
                goalEvidence.Add(evidence);
                return evidence;
            },
            ViewportExplorationEvaluator: Evaluate);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolvePage,
            CreateContainer,
            recovery);
        var viewportCount = branch switch
        {
            "positive" => 3,
            "ambiguous" => 2,
            _ => 1,
        };
        var steps = ImmutableArray.CreateBuilder<PlanStep>();
        steps.Add(new PlanStep("A", "Tap"));
        for (var index = 0; index < viewportCount; index++)
            steps.Add(new PlanStep($"Viewport-{index + 1}", "ScrollForward"));

        var state = await agent.RunAsync(
            goal,
            new Plan(steps.ToImmutable()),
            RunId,
            CancellationToken.None);
        return new FormalRun(
            state,
            agent,
            traversal,
            environment,
            containers.ToImmutableArray(),
            decisions.ToImmutableArray(),
            goalEvidence.ToImmutableArray());
    }

    private static string[] ExplorationTraceOutcomes(RuntimeAgent agent)
        => agent.Trace
            .Where(item => item.Reason?.StartsWith("viewport exploration ", StringComparison.Ordinal) == true)
            .Select(item => item.Reason!.Split(':', 2)[0]["viewport exploration ".Length..])
            .ToArray();

    private static string[] Texts(Observation observation)
        => observation.Elements.Select(element => element.Text).ToArray();

    private static string? ResolvePage(Observation observation)
        => observation.Elements.Any(element => element.Text is "A" or "B" or "C" or "D" or "E")
            ? "ScrollableList"
            : observation.Elements.Any(element => element.Text == "Other semantic page")
                ? "OtherPage"
                : null;

    private static void AssertObservationHistory(
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

    private static void AssertJournal(
        IEnumerable<TraversalJournalEntry> expected,
        IEnumerable<TraversalJournalEntry> actual)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();
        Assert.Equal(expectedArray.Length, actualArray.Length);
        for (var index = 0; index < expectedArray.Length; index++)
        {
            Assert.Equal(expectedArray[index].StepId, actualArray[index].StepId);
            Assert.Equal(expectedArray[index].SelectedElementIndex, actualArray[index].SelectedElementIndex);
            Assert.Equal(expectedArray[index].DispatchedAction, actualArray[index].DispatchedAction);
            Assert.Equal(expectedArray[index].Result, actualArray[index].Result);
            Assert.Equal(expectedArray[index].RetryCount, actualArray[index].RetryCount);
            if (expectedArray[index].PostActionObservation is null || actualArray[index].PostActionObservation is null)
            {
                Assert.Equal(expectedArray[index].PostActionObservation, actualArray[index].PostActionObservation);
            }
            else
            {
                AssertObservationHistory(
                    [expectedArray[index].PostActionObservation!],
                    [actualArray[index].PostActionObservation!]);
            }
        }
    }

    private sealed record FormalRun(
        RunState State,
        RuntimeAgent Agent,
        RuntimeTraversal Traversal,
        ScriptedEnvironment Environment,
        ImmutableArray<RuntimeContainer> Containers,
        ImmutableArray<ViewportExplorationEvidence> Decisions,
        ImmutableArray<GoalEvidence> GoalEvidence);
}
