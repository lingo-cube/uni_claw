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

public sealed class SiblingBranchProgressScenarioTests
{
    [Fact]
    public async Task CompletePath_PreservesAWhileBExecutes_AndCompletesOnlyThroughGoalEvidence()
    {
        var run = CreateRun(SiblingBranchProgressFixture.Complete(), CompletePlan(), completeGoalFromProgress: true);

        var state = await run.RunAsync();

        Assert.Equal(RunState.Completed, state);
        var progress = Assert.Single(run.Agent.BranchProgress).Value;
        Assert.Equal(new[] { "Branch A", "Branch B" }, progress.ApprovedSiblingEvidence.Keys.Order());
        Assert.All(
            progress.ApprovedSiblingEvidence.Values,
            sequence => Assert.Equal(run.Environment.ObservationHistory[1].SequenceNumber, sequence));
        Assert.True(progress.IsSubtreeComplete);
        Assert.Equal(2, progress.CompletedSiblingEvidence.Count);
        Assert.Equal(
            run.Traversal.Journal[1].PostActionObservation!.SequenceNumber,
            progress.CompletedSiblingEvidence["Branch A"]);
        Assert.Equal(
            run.Traversal.Journal[4].PostActionObservation!.SequenceNumber,
            progress.CompletedSiblingEvidence["Branch B"]);
        Assert.True(
            run.Traversal.Journal[2].PostActionObservation!.SequenceNumber
            > progress.CompletedSiblingEvidence["Branch A"]);
        Assert.True(
            run.Traversal.Journal[5].PostActionObservation!.SequenceNumber
            > progress.CompletedSiblingEvidence["Branch B"]);
        Assert.All(
            run.Traversal.Journal,
            entry => Assert.IsType<DeviceAction.Tap>(entry.DispatchedAction));
        Assert.Equal(new DeviceAction.Tap(1), run.Traversal.Journal[2].DispatchedAction);
        Assert.Equal(new DeviceAction.Tap(1), run.Traversal.Journal[5].DispatchedAction);

        var afterFirstReturn = run.ProgressSnapshots[2]["ParentP"];
        Assert.Equal(new[] { "Branch A" }, afterFirstReturn.CompletedSiblingEvidence.Keys);
        Assert.False(afterFirstReturn.IsSubtreeComplete);
        Assert.Equal(
            new[] { "Branch A" },
            run.ProgressSnapshots[3]["ParentP"].CompletedSiblingEvidence.Keys);
        Assert.All(run.Evidence[..^1], evidence => Assert.False(evidence.Satisfied));
        Assert.True(run.Evidence[^1].Satisfied);
        Assert.Equal(run.Environment.ObservationHistory[^1].SequenceNumber, run.Evidence[^1].SourceObservationSequence);
        Assert.Equal(RunState.Completed, run.Agent.Trace[^1].RunState);
        Assert.DoesNotContain(
            run.Agent.Trace.Take(run.Agent.Trace.Count - 1),
            trace => trace.RunState == RunState.Completed);
    }

    [Fact]
    public async Task AOnly_LeavesBPendingAndCannotFabricateGoalCompletion()
    {
        var run = CreateRun(SiblingBranchProgressFixture.AOnly(), AOnlyPlan(), completeGoalFromProgress: true);

        var state = await run.RunAsync();

        Assert.Equal(RunState.Failed, state);
        var progress = run.Agent.BranchProgress["ParentP"];
        Assert.Equal(new[] { "Branch A", "Branch B" }, progress.ApprovedSiblingEvidence.Keys.Order());
        Assert.Equal(new[] { "Branch A" }, progress.CompletedSiblingEvidence.Keys);
        Assert.False(progress.IsSubtreeComplete);
        Assert.All(run.Evidence, evidence => Assert.False(evidence.Satisfied));
        Assert.DoesNotContain(run.Agent.Trace, trace => trace.RunState == RunState.Completed);
    }

    [Fact]
    public async Task EarlyReturn_DoesNotRecordChildCompletion()
    {
        var run = CreateRun(SiblingBranchProgressFixture.EarlyReturn(), EarlyReturnPlan());

        var state = await run.RunAsync();

        Assert.Equal(RunState.Failed, state);
        var progress = run.Agent.BranchProgress["ParentP"];
        Assert.Empty(progress.CompletedSiblingEvidence);
        Assert.False(progress.IsSubtreeComplete);
    }

    [Fact]
    public async Task RevisitA_IsIdempotentAndDoesNotCreateDistinctProgress()
    {
        var run = CreateRun(SiblingBranchProgressFixture.RevisitA(), RevisitAPlan());

        await run.RunAsync();

        var progress = run.Agent.BranchProgress["ParentP"];
        Assert.Equal(2, progress.ApprovedSiblingEvidence.Count);
        Assert.Equal(new[] { "Branch A" }, progress.CompletedSiblingEvidence.Keys);
        Assert.False(progress.IsSubtreeComplete);
    }

    [Fact]
    public async Task StaleParentReturn_IsRejectedWithoutMutatingParentProgressOrRedispatch()
    {
        var run = CreateRun(SiblingBranchProgressFixture.StaleParentAfterStartup(), AOnlyPlan());

        var state = await run.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Agent.BranchProgress["ParentP"].CompletedSiblingEvidence);
        Assert.Equal(4, run.Environment.ActionHistory.Count);
        Assert.Equal(3, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Equal(
            run.Traversal.Journal[^2].PostActionObservation!.SequenceNumber,
            run.Traversal.Journal[^3].PostActionObservation!.SequenceNumber);
    }

    [Fact]
    public async Task WrongParentReturn_CannotAttachCompletionToParentP()
    {
        var run = CreateRun(SiblingBranchProgressFixture.WrongParent(), AOnlyPlan());

        var state = await run.RunAsync();

        Assert.Equal(RunState.Failed, state);
        Assert.Empty(run.Agent.BranchProgress["ParentP"].CompletedSiblingEvidence);
        Assert.False(run.Agent.BranchProgress.ContainsKey("OtherParent"));
        Assert.Equal("OtherParent", run.Agent.Belief!.SemanticPage);
    }

    [Fact]
    public async Task EqualInputs_ReplayProgressTraceJournalEvidenceAndFinalState()
    {
        var first = CreateRun(SiblingBranchProgressFixture.Complete(), CompletePlan(), completeGoalFromProgress: true);
        var second = CreateRun(SiblingBranchProgressFixture.Complete(), CompletePlan(), completeGoalFromProgress: true);

        var firstState = await first.RunAsync();
        var secondState = await second.RunAsync();

        Assert.Equal(firstState, secondState);
        AssertProgressEqual(first.Agent.BranchProgress, second.Agent.BranchProgress);
        Assert.Equal(first.Agent.Trace, second.Agent.Trace);
        AssertJournalEqual(first.Traversal.Journal, second.Traversal.Journal);
        Assert.Equal(first.Environment.ActionHistory, second.Environment.ActionHistory);
        Assert.Equal(first.Environment.ObservationHistory.Count, second.Environment.ObservationHistory.Count);
        for (var index = 0; index < first.Environment.ObservationHistory.Count; index++)
        {
            AssertObservationEqual(
                first.Environment.ObservationHistory[index],
                second.Environment.ObservationHistory[index]);
        }
        Assert.Equal(first.Evidence, second.Evidence);
        Assert.Equal(first.ProgressSnapshots.Count, second.ProgressSnapshots.Count);
        for (var index = 0; index < first.ProgressSnapshots.Count; index++)
            AssertProgressEqual(first.ProgressSnapshots[index], second.ProgressSnapshots[index]);
    }

    private static BranchRun CreateRun(
        SiblingBranchProgressFixture fixture,
        Plan plan,
        bool completeGoalFromProgress = false)
    {
        var environment = fixture.Environment;
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        RuntimeAgent? agent = null;
        var evidence = new List<GoalEvidence>();
        var snapshots = new List<ImmutableDictionary<string, BranchProgressEvidence>>();
        var goal = new Goal(observation =>
        {
            var snapshot = agent!.BranchProgress.ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
            snapshots.Add(snapshot);
            var satisfied = completeGoalFromProgress
                && snapshot.TryGetValue("ParentP", out var progress)
                && progress.IsSubtreeComplete;
            var item = new GoalEvidence(
                satisfied,
                satisfied ? "Agent evaluated complete bounded sibling evidence." : "Sibling proof remains incomplete.",
                observation.SequenceNumber);
            evidence.Add(item);
            return item;
        });
        agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolveSemanticPage,
            semanticPage => new RuntimeContainer(
                semanticPage,
                observation => string.Equals(ResolveSemanticPage(observation), semanticPage, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        return new BranchRun(fixture.RunId, environment, agent, traversal, goal, plan, evidence, snapshots);
    }

    private static string? ResolveSemanticPage(Observation observation)
    {
        var texts = observation.Elements.Select(element => element.Text).ToHashSet(StringComparer.Ordinal);
        if (texts.Contains("Branch A") && texts.Contains("Branch B"))
            return "ParentP";
        if (texts.Contains("Complete A work") || texts.Contains("A local effect"))
            return "ChildA";
        if (texts.Contains("Complete B work") || texts.Contains("B local effect"))
            return "ChildB";
        if (texts.Contains("Conflicting parent"))
            return "OtherParent";
        return null;
    }

    private static Plan CompletePlan() => PlanOf(
        ("Branch A", "Tap"),
        ("Complete A work", "Tap"),
        ("Return to Parent P", "Tap"),
        ("Branch B", "Tap"),
        ("Complete B work", "Tap"),
        ("Return to Parent P", "Tap"));

    private static Plan AOnlyPlan() => PlanOf(
        ("Branch A", "Tap"),
        ("Complete A work", "Tap"),
        ("Return to Parent P", "Tap"),
        ("Stop before Branch B", "Tap"),
        ("Branch B", "Tap"));

    private static Plan EarlyReturnPlan() => PlanOf(
        ("Branch A", "Tap"),
        ("Return to Parent P", "Tap"),
        ("Stop before Branch B", "Tap"),
        ("Branch B", "Tap"));

    private static Plan RevisitAPlan() => PlanOf(
        ("Branch A", "Tap"),
        ("Complete A work", "Tap"),
        ("Return to Parent P", "Tap"),
        ("Branch A", "Tap"),
        ("Branch B", "Tap"));

    private static Plan PlanOf(params (string Target, string Action)[] steps)
        => new(steps.Select(step => new PlanStep(step.Target, step.Action)).ToImmutableArray());

    private static void AssertProgressEqual(
        IReadOnlyDictionary<string, BranchProgressEvidence> expected,
        IReadOnlyDictionary<string, BranchProgressEvidence> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var parent in expected.Keys)
        {
            var expectedProgress = expected[parent];
            var actualProgress = actual[parent];
            Assert.Equal(expectedProgress.ParentSemanticPage, actualProgress.ParentSemanticPage);
            Assert.Equal(
                expectedProgress.ApprovedSiblingEvidence.OrderBy(pair => pair.Key),
                actualProgress.ApprovedSiblingEvidence.OrderBy(pair => pair.Key));
            Assert.Equal(
                expectedProgress.CompletedSiblingEvidence.OrderBy(pair => pair.Key),
                actualProgress.CompletedSiblingEvidence.OrderBy(pair => pair.Key));
            Assert.Equal(expectedProgress.IsSubtreeComplete, actualProgress.IsSubtreeComplete);
        }
    }

    private static void AssertJournalEqual(
        IReadOnlyList<UniClaw.Runtime.Traversal.TraversalJournalEntry> expected,
        IReadOnlyList<UniClaw.Runtime.Traversal.TraversalJournalEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].StepId, actual[index].StepId);
            Assert.Equal(expected[index].SelectedElementIndex, actual[index].SelectedElementIndex);
            Assert.Equal(expected[index].DispatchedAction, actual[index].DispatchedAction);
            Assert.Equal(expected[index].Result, actual[index].Result);
            Assert.Equal(expected[index].RetryCount, actual[index].RetryCount);
            AssertObservationEqual(expected[index].PostActionObservation, actual[index].PostActionObservation);
        }
    }

    private static void AssertObservationEqual(Observation? expected, Observation? actual)
    {
        if (expected is null || actual is null)
        {
            Assert.Equal(expected, actual);
            return;
        }
        Assert.Equal(expected.ForegroundApplication, actual.ForegroundApplication);
        Assert.Equal(expected.SequenceNumber, actual.SequenceNumber);
        Assert.Equal(expected.Elements, actual.Elements);
    }

    private sealed record BranchRun(
        string RunId,
        ScriptedEnvironment Environment,
        RuntimeAgent Agent,
        RuntimeTraversal Traversal,
        Goal Goal,
        Plan Plan,
        List<GoalEvidence> Evidence,
        List<ImmutableDictionary<string, BranchProgressEvidence>> ProgressSnapshots)
    {
        public Task<RunState> RunAsync()
            => Agent.RunAsync(Goal, Plan, RunId, CancellationToken.None);
    }
}
