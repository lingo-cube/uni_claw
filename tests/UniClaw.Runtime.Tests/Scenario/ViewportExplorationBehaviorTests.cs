using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class ViewportExplorationBehaviorTests
{
    private const string RunId = "sc-p3-cand-007-behavior-run";

    [Fact]
    public async Task Positive_TrueTrueFalseDispatchesExactlyTwoAndCompletesOnlyFromGoalEvidence()
    {
        var run = await RunAsync("positive", viewportStepCount: 3, goalSatisfiedAtEnd: true);

        Assert.Equal(RunState.Completed, run.State);
        Assert.Equal(2, run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>().Count());
        Assert.Equal(new bool?[] { true, true, false }, run.Decisions.Select(item => item.ContinueExploration));
        Assert.Equal(3, Assert.Single(run.Containers).ViewportExplorationObservations.Length);
        Assert.Contains("GoalEvidence", run.Agent.Reason, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "continue", "continue", "exhausted" },
            ExplorationTraceOutcomes(run.Agent));
    }

    [Fact]
    public async Task PositiveExhaustionWithoutGoalEvidenceStopsWithoutThirdDispatchOrCompletion()
    {
        var run = await RunAsync("positive", viewportStepCount: 3, goalSatisfiedAtEnd: false);

        Assert.Equal(RunState.Failed, run.State);
        Assert.Equal(2, run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>().Count());
        Assert.Contains("positively exhausted", run.Agent.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(run.Agent.Trace, item => item.RunState == RunState.Completed);
    }

    [Fact]
    public async Task AmbiguousSameEvidenceStopsUnresolvedWithoutBlindSecondMovement()
    {
        var run = await RunAsync("ambiguous", viewportStepCount: 2, goalSatisfiedAtEnd: false);

        Assert.Equal(RunState.Failed, run.State);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Equal(new bool?[] { true, null }, run.Decisions.Select(item => item.ContinueExploration));
        Assert.Contains("unresolved", run.Agent.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(run.Agent.Trace, item => item.RunState == RunState.Completed);
    }

    [Fact]
    public async Task BoundReachedWhileContinuationTrueIsUnresolvedNotExhausted()
    {
        var run = await RunAsync("positive", viewportStepCount: 1, goalSatisfiedAtEnd: false);

        Assert.Equal(RunState.Failed, run.State);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Equal(new bool?[] { true, true }, run.Decisions.Select(item => item.ContinueExploration));
        Assert.Contains("bound reached", run.Agent.Reason, StringComparison.Ordinal);
        Assert.Contains("semantic exhaustion 未获证明", run.Agent.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("stale")]
    [InlineData("page-changed")]
    public async Task DispatchOrContinuityFailureDoesNotFabricateExhaustion(string branch)
    {
        var run = await RunAsync(branch, viewportStepCount: 2, goalSatisfiedAtEnd: false);

        Assert.Equal(RunState.Failed, run.State);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.DoesNotContain(
            run.Agent.Trace,
            item => item.Reason?.Contains("viewport exploration exhausted", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(run.Agent.Trace, item => item.RunState == RunState.Completed);
    }

    [Fact]
    public async Task EvaluatorAbsentPreservesExistingFixedPlanBehavior()
    {
        var environment = ScriptedEnvironmentVariants.ViewportExplorationPositive();
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ResolvePage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolvePage,
            page => new RuntimeContainer(page, observation => ResolvePage(observation) == page, traversal.ExecuteStep),
            recovery);
        var goal = new Goal(observation => new GoalEvidence(
            observation.Elements.Any(element => element.Text == "D"),
            "existing fixed-plan evidence",
            observation.SequenceNumber));

        var state = await agent.RunAsync(
            goal,
            new Plan([new PlanStep("Viewport", "ScrollForward")]),
            RunId,
            CancellationToken.None);

        Assert.Equal(RunState.Completed, state);
        Assert.Single(environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.DoesNotContain(agent.Trace, item => item.Reason?.Contains("viewport exploration", StringComparison.Ordinal) == true);
    }

    private static async Task<BehaviorRun> RunAsync(
        string branch,
        int viewportStepCount,
        bool goalSatisfiedAtEnd)
    {
        var environment = branch switch
        {
            "positive" => ScriptedEnvironmentVariants.ViewportExplorationPositive(),
            "ambiguous" => ScriptedEnvironmentVariants.ViewportExplorationAmbiguousSame(),
            "rejected" => ScriptedEnvironmentVariants.ViewportExplorationRejected(),
            "stale" => ScriptedEnvironmentVariants.ViewportExplorationRuntimeStale(),
            "page-changed" => ScriptedEnvironmentVariants.ViewportExplorationPageChanged(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ResolvePage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        var decisions = new List<ViewportExplorationEvidence>();

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
                var satisfied = goalSatisfiedAtEnd
                    && observation.Elements.Any(element => element.Text == "End of list");
                return new GoalEvidence(
                    satisfied,
                    satisfied
                        ? $"GoalEvidence independently proves completion at seq={observation.SequenceNumber}."
                        : $"GoalEvidence unsatisfied at seq={observation.SequenceNumber}.",
                    observation.SequenceNumber);
            },
            ViewportExplorationEvaluator: Evaluate);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolvePage,
            CreateContainer,
            recovery);
        var plan = new Plan(Enumerable.Range(0, viewportStepCount)
            .Select(index => new PlanStep($"Viewport-{index + 1}", "ScrollForward"))
            .ToImmutableArray());

        var state = await agent.RunAsync(goal, plan, RunId, CancellationToken.None);
        return new BehaviorRun(
            state,
            agent,
            environment,
            traversal,
            containers.ToImmutableArray(),
            decisions.ToImmutableArray());
    }

    private static string[] ExplorationTraceOutcomes(RuntimeAgent agent)
        => agent.Trace
            .Where(item => item.Reason?.StartsWith("viewport exploration ", StringComparison.Ordinal) == true)
            .Select(item => item.Reason!.Split(':', 2)[0]["viewport exploration ".Length..])
            .ToArray();

    private static string? ResolvePage(Observation observation)
        => observation.Elements.Any(element => element.Text is "A" or "B" or "C" or "D" or "E")
            ? "ScrollableList"
            : observation.Elements.Any(element => element.Text == "Other semantic page")
                ? "OtherPage"
                : null;

    private sealed record BehaviorRun(
        RunState State,
        RuntimeAgent Agent,
        ScriptedEnvironment Environment,
        RuntimeTraversal Traversal,
        ImmutableArray<RuntimeContainer> Containers,
        ImmutableArray<ViewportExplorationEvidence> Decisions);
}
