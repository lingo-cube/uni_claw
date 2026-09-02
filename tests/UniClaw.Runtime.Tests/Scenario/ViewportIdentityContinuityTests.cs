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

/// <summary>
/// SC-P3-003 formal Scenario proof：一次 targetless ScrollForward 改变 visible snapshot，
/// 但 Container continuity 只由 fresh foreground + IsStillMine + reconciled semantic page 共同证明。
/// Positive 分支在 movement 后继续执行 D，锁定 progress 与 GoalEvidence authority；stale/conflict
/// 分支锁定 Container-scope escalation、无 blind redispatch 与 Agent higher-scope authority。
/// </summary>
public sealed class ViewportIdentityContinuityTests
{
    private const string RunId = "sc-p3-003-formal-run";

    [Fact]
    public async Task Positive_FreshViewportEvidencePreservesSameContainerProgress_ContinuesAndCompletesFromGoalEvidence()
    {
        var run = await RunScenarioAsync("continuous");

        Assert.Equal(RunState.Completed, run.FinalState);
        var container = Assert.Single(run.Containers);

        // Evidence 1/2/6：同一 Container 未替换；movement 前已有 A progress，movement 后继续追加 D。
        //（CP-06：seq2 初始评估快照在前（空 progress），后续快照整体 +1；后续 ordinary same 接受 seq5）
        Assert.Equal(new[] { "A" }, run.ProgressSnapshots[1].Select(step => step.TargetDescription));
        Assert.Equal(new[] { "A", "Viewport" }, run.ProgressSnapshots[2].Select(step => step.TargetDescription));
        Assert.Equal(new[] { "A", "Viewport", "D" }, run.ProgressSnapshots[3].Select(step => step.TargetDescription));
        Assert.Equal(run.ProgressSnapshots[3], container.ExecutedSteps);
        Assert.Equal(5, container.CurrentObservation!.SequenceNumber);
        Assert.Equal(new[] { "D", "E", "F" }, container.CurrentObservation.Elements.Select(element => element.Text));
        Assert.Single(run.Agent.Trace.Where(entry =>
            entry.ContainerId == "ScrollableList"
            && entry.StepId is null
            && entry.RecoveryId is null));

        // Evidence 3/4：只有一次无 element target 的 viewport action；fresh viewport evidence 是 seq4 D/E/F。
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.ScrollForward(),
                new DeviceAction.Tap(0),
            },
            run.Environment.ActionHistory);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, run.Environment.ObservationHistory.Select(item => item.SequenceNumber));
        var viewportEntry = run.Traversal.Journal[1];
        Assert.Null(viewportEntry.SelectedElementIndex);
        Assert.Equal(new DeviceAction.ScrollForward(), viewportEntry.DispatchedAction);
        Assert.Equal(4, viewportEntry.PostActionObservation!.SequenceNumber);
        Assert.Equal(new[] { "D", "E", "F" }, viewportEntry.PostActionObservation.Elements.Select(element => element.Text));

        // Evidence 5：三个既有判据共同接受 continuity；snapshot 集合本身不同，不是 identity authority。
        //（CP-06：seq2 初始记录在前，IdentityEvidence 整体 +1）
        Assert.Equal(
            new IdentityEvidence(4, true, true, "ScrollableList", 4),
            run.IdentityEvidence[2]);
        Assert.True(container.IsStillMine(viewportEntry.PostActionObservation));

        // Evidence 7/8：movement 后 D 仍可 grounding；scroll evidence 未完成 Goal，只有后续 seq5 GoalEvidence 完成。
        Assert.Null(run.Agent.LastTrap);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RecoveryId is not null);
        Assert.Equal(new[] { false, false, false, true }, run.GoalEvidence.Select(item => item.Satisfied)); // CP-06：seq2 初始评估在前
        Assert.Equal(4, run.GoalEvidence[2].SourceObservationSequence);
        Assert.Equal(5, run.GoalEvidence[3].SourceObservationSequence);
        Assert.Equal(run.GoalEvidence[3].Reason, run.Agent.Reason);
        var viewportTraceIndex = Array.FindIndex(
            run.Agent.Trace.ToArray(),
            entry => entry.Action is DeviceAction.ScrollForward);
        var continuedTapIndex = Array.FindIndex(
            run.Agent.Trace.ToArray(),
            entry => entry.StepId == "Step-3" && entry.Action is DeviceAction.Tap);
        var completedIndex = Array.FindIndex(
            run.Agent.Trace.ToArray(),
            entry => entry.RunState == RunState.Completed);
        Assert.True(viewportTraceIndex >= 0 && continuedTapIndex > viewportTraceIndex && completedIndex > continuedTapIndex);
    }

    [Fact]
    public async Task Escalation_StaleEvidenceDoesNotProveContinuityOrRedispatch_AndPreservesProgress()
    {
        var run = await RunScenarioAsync("stale");

        Assert.Equal(RunState.Failed, run.FinalState);
        var container = Assert.Single(run.Containers);
        Assert.Equal(new[] { "A", "Viewport" }, container.ExecutedSteps.Select(step => step.TargetDescription));
        // ordinary same Tap 的 seq3 已接受；stale viewport seq2 仍不得被 Container 接受。
        Assert.Equal(3, container.CurrentObservation!.SequenceNumber);
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.ScrollForward(),
            },
            run.Environment.ActionHistory);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Equal(new long[] { 1, 2, 3, 2 }, run.Environment.ObservationHistory.Select(item => item.SequenceNumber));

        var viewportEntry = run.Traversal.Journal[1];
        Assert.IsType<TraversalStepResult.Failed>(viewportEntry.Result);
        Assert.Null(viewportEntry.SelectedElementIndex);
        Assert.Equal(2, viewportEntry.PostActionObservation!.SequenceNumber);
        var trap = run.Agent.LastTrap ?? throw new InvalidOperationException("stale viewport evidence 未升级 Container-scope evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(3, trap.Expected);
        Assert.Equal(2, trap.Observed);
        Assert.Equal(new DeviceAction.ScrollForward(), trap.LastAction);
        Assert.Single(run.Agent.Trace.Where(entry => entry.TrapScope == TrapScope.Container));
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RecoveryId is not null);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Equal(2, run.GoalEvidence.Length); // CP-06：seq2 初始评估 + seq3，均未满足
        Assert.All(run.GoalEvidence, evidence => Assert.False(evidence.Satisfied));
    }

    [Fact]
    public async Task Escalation_SemanticConflictPreservesOriginalProgress_AndAgentAloneRebindsAndFails()
    {
        var run = await RunScenarioAsync("page-changed");

        Assert.Equal(RunState.Failed, run.FinalState);
        Assert.Equal(2, run.Containers.Length);
        var original = run.Containers[0];
        var rebound = run.Containers[1];
        Assert.Equal("ScrollableList", original.SemanticPageName);
        Assert.Equal(new[] { "A", "Viewport" }, original.ExecutedSteps.Select(step => step.TargetDescription));
        // ordinary same Tap 的 seq3 已接受；identity-conflict viewport seq4 仍不得被旧 Container 接受。
        Assert.Equal(3, original.CurrentObservation!.SequenceNumber);
        Assert.Equal("OtherPage", rebound.SemanticPageName);
        Assert.Empty(rebound.ExecutedSteps);
        Assert.Equal("OtherPage", run.Agent.Belief!.SemanticPage);

        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Equal(new long[] { 1, 2, 3, 4 }, run.Environment.ObservationHistory.Select(item => item.SequenceNumber));
        Assert.Equal(new IdentityEvidence(4, true, false, "OtherPage", 3), run.IdentityEvidence[2]); // CP-06：seq2 初始记录在前；ordinary same seq3 已接受
        var trap = run.Agent.LastTrap ?? throw new InvalidOperationException("viewport identity conflict 未升级 Container-scope evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(3, trap.Expected);
        Assert.Equal(4, trap.Observed);
        Assert.Equal(new DeviceAction.ScrollForward(), trap.LastAction);
        Assert.Single(run.Agent.Trace.Where(entry => entry.TrapScope == TrapScope.Container));
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RecoveryId is not null);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Equal(new[] { false, false, false }, run.GoalEvidence.Select(item => item.Satisfied)); // CP-06：seq2 初始评估在前
    }

    [Theory]
    [InlineData("continuous")]
    [InlineData("stale")]
    [InlineData("page-changed")]
    public async Task DeterministicReplay_EqualInputsReplayAllFormalEvidence(string branch)
    {
        var first = await RunScenarioAsync(branch);
        var second = await RunScenarioAsync(branch);

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.FinalState, second.FinalState);
        Assert.Equal(first.Agent.Reason, second.Agent.Reason);
        Assert.Equal(first.Agent.Belief, second.Agent.Belief);
        Assert.Equal(first.Agent.LastTrap, second.Agent.LastTrap);
        Assert.Equal(first.Environment.ActionHistory.ToArray(), second.Environment.ActionHistory.ToArray());
        AssertSameObservationHistory(first.Environment.ObservationHistory, second.Environment.ObservationHistory);
        AssertSameJournal(first.Traversal.Journal, second.Traversal.Journal);
        Assert.Equal(first.Agent.Trace.ToArray(), second.Agent.Trace.ToArray());
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
        Assert.Equal(first.IdentityEvidence, second.IdentityEvidence);
        Assert.Equal(first.ProgressSnapshots.Length, second.ProgressSnapshots.Length);
        for (var index = 0; index < first.ProgressSnapshots.Length; index++)
            Assert.Equal(first.ProgressSnapshots[index], second.ProgressSnapshots[index]);
        Assert.Equal(first.Containers.Length, second.Containers.Length);
        for (var index = 0; index < first.Containers.Length; index++)
        {
            Assert.Equal(first.Containers[index].SemanticPageName, second.Containers[index].SemanticPageName);
            AssertSameObservation(first.Containers[index].CurrentObservation, second.Containers[index].CurrentObservation);
            Assert.Equal(first.Containers[index].ExecutedSteps, second.Containers[index].ExecutedSteps);
        }
    }

    private static async Task<ScenarioRun> RunScenarioAsync(string branch)
    {
        var environment = branch switch
        {
            "continuous" => ScriptedEnvironmentVariants.ViewportContinuous(),
            "stale" => ScriptedEnvironmentVariants.ViewportRuntimeStale(),
            "page-changed" => ScriptedEnvironmentVariants.ViewportPageChanged(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "未知 SC-P3-003 formal branch。"),
        };
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        var evidence = new List<GoalEvidence>();
        var progressSnapshots = new List<ImmutableArray<PlanStep>>();
        var identityEvidence = new List<IdentityEvidence>();

        RuntimeContainer CreateContainer(string pageName)
        {
            var container = new RuntimeContainer(
                pageName,
                observation => string.Equals(ResolveSemanticPage(observation), pageName, StringComparison.Ordinal),
                traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }

        var goal = new Goal(observation =>
        {
            var original = containers[0];
            progressSnapshots.Add(original.ExecutedSteps);
            var semanticPage = ResolveSemanticPage(observation);
            identityEvidence.Add(new IdentityEvidence(
                observation.SequenceNumber,
                observation.ForegroundApplication == "Settings",
                original.IsStillMine(observation),
                semanticPage,
                original.CurrentObservation?.SequenceNumber));
            var satisfied = branch == "continuous"
                && observation.SequenceNumber == 5
                && semanticPage == "ScrollableList"
                && observation.Elements.Any(element => element.Text == "D");
            var item = new GoalEvidence(
                satisfied,
                satisfied
                    ? $"fresh post-viewport execution evidence satisfies Goal (seq={observation.SequenceNumber})"
                    : $"GoalEvidence remains unsatisfied (seq={observation.SequenceNumber})",
                observation.SequenceNumber);
            evidence.Add(item);
            return item;
        });
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolveSemanticPage,
            CreateContainer,
            recovery);
        var steps = branch == "continuous"
            ? new Plan([
                new PlanStep("A", "Tap"),
                new PlanStep("Viewport", "ScrollForward"),
                new PlanStep("D", "Tap"),
            ])
            : new Plan([
                new PlanStep("A", "Tap"),
                new PlanStep("Viewport", "ScrollForward"),
            ]);

        var finalState = await agent.RunAsync(goal, steps, RunId, CancellationToken.None);
        return new ScenarioRun(
            RunId,
            finalState,
            agent,
            traversal,
            environment,
            containers.ToImmutableArray(),
            evidence.ToImmutableArray(),
            progressSnapshots.ToImmutableArray(),
            identityEvidence.ToImmutableArray());
    }

    private static string? ResolveSemanticPage(Observation observation)
        => observation.Elements.Any(element => element.Text is "A" or "B" or "C" or "D" or "E" or "F")
            ? "ScrollableList"
            : observation.Elements.Any(element => element.Text == "Other semantic page")
                ? "OtherPage"
                : null;

    private static void AssertSameObservationHistory(
        IReadOnlyList<Observation> expected,
        IReadOnlyList<Observation> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
            AssertSameObservation(expected[index], actual[index]);
    }

    private static void AssertSameJournal(
        IReadOnlyList<TraversalJournalEntry> expected,
        IReadOnlyList<TraversalJournalEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].StepId, actual[index].StepId);
            Assert.Equal(expected[index].SelectedElementIndex, actual[index].SelectedElementIndex);
            Assert.Equal(expected[index].DispatchedAction, actual[index].DispatchedAction);
            Assert.Equal(expected[index].Result, actual[index].Result);
            Assert.Equal(expected[index].RetryCount, actual[index].RetryCount);
            AssertSameObservation(expected[index].PostActionObservation, actual[index].PostActionObservation);
        }
    }

    private static void AssertSameObservation(Observation? expected, Observation? actual)
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

    private sealed record IdentityEvidence(
        long SequenceNumber,
        bool ForegroundCompatible,
        bool IsStillMine,
        string? SemanticPage,
        long? ContainerObservationSequence);

    private sealed record ScenarioRun(
        string RunId,
        RunState FinalState,
        RuntimeAgent Agent,
        RuntimeTraversal Traversal,
        ScriptedEnvironment Environment,
        ImmutableArray<RuntimeContainer> Containers,
        ImmutableArray<GoalEvidence> GoalEvidence,
        ImmutableArray<ImmutableArray<PlanStep>> ProgressSnapshots,
        ImmutableArray<IdentityEvidence> IdentityEvidence);
}
