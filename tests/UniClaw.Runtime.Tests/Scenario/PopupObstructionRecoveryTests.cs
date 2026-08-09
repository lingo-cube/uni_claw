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

/// <summary>
/// SC-P3-002 Task 3.1 formal Scenario proof：外部 Popup 是 Container-scope obstruction evidence；
/// 计划内只执行一次 bounded dismiss，随后由 fresh Observation + foreground + IsStillMine + reconciled page
/// 证明连续性。Rejected 或 page-changed 分支使用既有 Container Trap evidence 升级，最终 authority 保留在 Agent。
/// 本测试直接组合 Task 1.1 ScriptedEnvironment 与 Task 2.1 Runtime，不修改共享 ScenarioHarness。
/// </summary>
public sealed class PopupObstructionRecoveryTests
{
    private const string RunId = "sc-p3-002-formal-run";

    [Fact]
    public async Task Positive_BoundedDismiss_FreshEvidencePreservesSameContainerProgress_AndGoalEvidenceCompletes()
    {
        var run = await RunScenarioAsync("continuous");

        Assert.Equal(RunState.Completed, run.FinalState);
        var container = Assert.Single(run.Containers);

        // Evidence 1/2/6：始终只有同一 active Container；Popup 前 progress 可见，dismiss 后仍为前缀且继续追加。
        Assert.Equal(new[] { "WiFi" }, run.ProgressSnapshots[0].Select(step => step.TargetDescription));
        Assert.Equal(new[] { "WiFi", "Dismiss" }, run.ProgressSnapshots[1].Select(step => step.TargetDescription));
        Assert.Equal(run.ProgressSnapshots[1], container.ExecutedSteps);
        Assert.Equal(4, container.CurrentObservation!.SequenceNumber);
        Assert.Single(run.Agent.Trace.Where(entry =>
            entry.ContainerId == "NetworkSettings"
            && entry.StepId is null
            && entry.RecoveryId is null));

        // Evidence 3/4：仅 Launch + local progress Tap + 一次 dismiss Tap；dismiss 后 fresh seq=4 > obstruction seq=3。
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            run.Environment.ActionHistory);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, run.Environment.ObservationHistory.Select(observation => observation.SequenceNumber));
        Assert.Equal(2, run.Traversal.Journal.Count);
        Assert.Equal(3, run.Traversal.Journal[0].PostActionObservation!.SequenceNumber);
        Assert.Equal(4, run.Traversal.Journal[1].PostActionObservation!.SequenceNumber);
        Assert.IsType<TraversalStepResult.Succeeded>(run.Traversal.Journal[1].Result);

        // Evidence 5：Popup evidence 本身 Unknown/not-mine；fresh post-dismiss evidence 由三个既有判据共同证明连续。
        Assert.Equal(
            new ContinuityEvidence(3, true, false, null),
            run.ContinuityEvidence[0]);
        Assert.Equal(
            new ContinuityEvidence(4, true, true, "NetworkSettings"),
            run.ContinuityEvidence[1]);
        Assert.True(container.IsStillMine(run.Environment.ObservationHistory[3]));

        // Evidence 6/8：无 Recovery；dismiss/Traversal success 不直接完成，只有 seq=4 satisfied GoalEvidence 触发 Completed。
        Assert.Null(run.Agent.LastTrap);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RecoveryId is not null);
        Assert.Equal(2, run.GoalEvidence.Length);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.True(run.GoalEvidence[1].Satisfied);
        Assert.Equal(4, run.GoalEvidence[1].SourceObservationSequence);
        Assert.Equal(run.GoalEvidence[1].Reason, run.Agent.Reason);
        var dismissTraceIndex = Array.FindIndex(
            run.Agent.Trace.ToArray(),
            entry => entry.StepId == "Step-2" && entry.Action is DeviceAction.Tap);
        var completedIndex = Array.FindIndex(
            run.Agent.Trace.ToArray(),
            entry => entry.RunState == RunState.Completed);
        Assert.True(dismissTraceIndex >= 0 && completedIndex > dismissTraceIndex);
    }

    [Fact]
    public async Task Escalation_DismissRejected_NoFabricatedSuccess_NoProgressReset_AgentFails()
    {
        var run = await RunScenarioAsync("rejected");

        Assert.Equal(RunState.Failed, run.FinalState);
        var container = Assert.Single(run.Containers);
        Assert.Equal(new[] { "WiFi" }, run.ProgressSnapshots[0].Select(step => step.TargetDescription));
        Assert.Equal(new[] { "WiFi", "Dismiss" }, container.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.Equal(3, container.CurrentObservation!.SequenceNumber);

        // 一次 dismiss 被 Environment 明确 Rejected；无 Observe/redispatch/Recovery/Completed 伪造。
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            run.Environment.ActionHistory);
        Assert.Equal(new long[] { 1, 2, 3 }, run.Environment.ObservationHistory.Select(observation => observation.SequenceNumber));
        var dismissEntry = run.Traversal.Journal[^1];
        Assert.IsType<TraversalStepResult.Failed>(dismissEntry.Result);
        Assert.Null(dismissEntry.PostActionObservation);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RecoveryId is not null);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);

        // Container-scope structured evidence 到达 Agent；Agent 独占最终 Failed。
        var trap = run.Agent.LastTrap ?? throw new InvalidOperationException("Rejected dismiss 未升级 Container-scope evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(3, trap.Expected);
        Assert.Null(trap.Observed);
        Assert.Equal(new DeviceAction.Tap(0), trap.LastAction);
        Assert.Single(run.Agent.Trace.Where(entry => entry.TrapScope == TrapScope.Container));
        Assert.Single(run.GoalEvidence);
        Assert.False(run.GoalEvidence[0].Satisfied);
        Assert.Equal(RunState.Failed, run.Agent.Trace[^1].RunState);
    }

    [Fact]
    public async Task Escalation_PageChanged_FreshEvidenceRejectsContinuity_AndAgentOwnsRebindAndFailure()
    {
        var run = await RunScenarioAsync("page-changed");

        Assert.Equal(RunState.Failed, run.FinalState);
        Assert.Equal(2, run.Containers.Length);
        var original = run.Containers[0];
        var rebound = run.Containers[1];

        // 原 Container progress 保留；只有 Agent 依据 fresh page evidence 建立新的 SettingsMain Container。
        Assert.Equal(new[] { "WiFi" }, run.ProgressSnapshots[0].Select(step => step.TargetDescription));
        Assert.Equal(new[] { "WiFi", "Dismiss" }, run.ProgressSnapshots[1].Select(step => step.TargetDescription));
        Assert.Equal(run.ProgressSnapshots[1], original.ExecutedSteps);
        Assert.Equal("NetworkSettings", original.SemanticPageName);
        Assert.Equal("SettingsMain", rebound.SemanticPageName);
        Assert.Empty(rebound.ExecutedSteps);
        Assert.Equal("SettingsMain", run.Agent.Belief!.SemanticPage);

        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp("Settings"),
                new DeviceAction.Tap(0),
                new DeviceAction.Tap(0),
            },
            run.Environment.ActionHistory);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, run.Environment.ObservationHistory.Select(observation => observation.SequenceNumber));
        Assert.Equal(new ContinuityEvidence(4, true, false, "SettingsMain"), run.ContinuityEvidence[1]);

        var trap = run.Agent.LastTrap ?? throw new InvalidOperationException("page-changed continuity failure 未升级 evidence。");
        Assert.Equal(TrapKind.ContainerMismatch, trap.Kind);
        Assert.Equal(TrapScope.Container, trap.Scope);
        Assert.Equal(3, trap.Expected);
        Assert.Equal(4, trap.Observed);
        Assert.Single(run.Agent.Trace.Where(entry => entry.TrapScope == TrapScope.Container));
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RecoveryId is not null);
        Assert.DoesNotContain(run.Agent.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Equal(2, run.GoalEvidence.Length);
        Assert.All(run.GoalEvidence, evidence => Assert.False(evidence.Satisfied));
        Assert.Equal(RunState.Failed, run.Agent.Trace[^1].RunState);
    }

    [Theory]
    [InlineData("continuous")]
    [InlineData("rejected")]
    [InlineData("page-changed")]
    public async Task DeterministicReplay_SameRunIdEnvironmentAndActions_ReplaysAllFormalEvidence(string branch)
    {
        var first = await RunScenarioAsync(branch);
        var second = await RunScenarioAsync(branch);

        Assert.Equal(first.FinalState, second.FinalState);
        Assert.Equal(first.Agent.State, second.Agent.State);
        Assert.Equal(first.Agent.Reason, second.Agent.Reason);
        Assert.Equal(first.Agent.Belief, second.Agent.Belief);
        Assert.Equal(first.Agent.LastTrap, second.Agent.LastTrap);
        Assert.Equal(first.Environment.ActionHistory.ToArray(), second.Environment.ActionHistory.ToArray());
        AssertSameObservationHistory(first.Environment.ObservationHistory, second.Environment.ObservationHistory);
        AssertSameJournal(first.Traversal.Journal, second.Traversal.Journal);
        Assert.Equal(first.Agent.Trace.ToArray(), second.Agent.Trace.ToArray());
        Assert.Equal(first.GoalEvidence.ToArray(), second.GoalEvidence.ToArray());
        Assert.Equal(first.ContinuityEvidence.ToArray(), second.ContinuityEvidence.ToArray());
        Assert.Equal(
            first.Containers.Select(container => container.SemanticPageName),
            second.Containers.Select(container => container.SemanticPageName));
        Assert.Equal(first.ProgressSnapshots.Length, second.ProgressSnapshots.Length);
        for (var index = 0; index < first.ProgressSnapshots.Length; index++)
            Assert.Equal(first.ProgressSnapshots[index], second.ProgressSnapshots[index]);
        Assert.Equal(first.Containers.Length, second.Containers.Length);
        for (var index = 0; index < first.Containers.Length; index++)
            Assert.Equal(first.Containers[index].ExecutedSteps, second.Containers[index].ExecutedSteps);
    }

    private static void AssertSameObservationHistory(
        IReadOnlyList<Observation> expected,
        IReadOnlyList<Observation> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
            AssertSameObservation(expected[index], actual[index]);
    }

    private static void AssertSameJournal(
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
            if (expected[index].PostActionObservation is null)
            {
                Assert.Null(actual[index].PostActionObservation);
            }
            else
            {
                Assert.NotNull(actual[index].PostActionObservation);
                AssertSameObservation(expected[index].PostActionObservation!, actual[index].PostActionObservation!);
            }
        }
    }

    private static void AssertSameObservation(Observation expected, Observation actual)
    {
        Assert.Equal(expected.ForegroundApplication, actual.ForegroundApplication);
        Assert.Equal(expected.SequenceNumber, actual.SequenceNumber);
        Assert.Equal(expected.Elements.Length, actual.Elements.Length);
        for (var index = 0; index < expected.Elements.Length; index++)
            Assert.Equal(expected.Elements[index], actual.Elements[index]);
    }

    private static async Task<ScenarioRun> RunScenarioAsync(string branch)
    {
        var environment = CreateEnvironment(branch);
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "Settings", ScenarioIdentity.ResolveSemanticPage);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        var evidence = new List<GoalEvidence>();
        var progressSnapshots = new List<ImmutableArray<PlanStep>>();
        var continuityEvidence = new List<ContinuityEvidence>();

        RuntimeContainer CreateContainer(string pageName)
        {
            var container = new RuntimeContainer(pageName, ScenarioIdentity.IdentityRule(pageName), traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }

        var goal = new Goal(observation =>
        {
            var activeAtEvaluation = containers[0];
            progressSnapshots.Add(activeAtEvaluation.ExecutedSteps);
            var semanticPage = ScenarioIdentity.ResolveSemanticPage(observation);
            continuityEvidence.Add(new ContinuityEvidence(
                observation.SequenceNumber,
                observation.ForegroundApplication == "Settings",
                activeAtEvaluation.IsStillMine(observation),
                semanticPage));
            var satisfied = branch == "continuous"
                && observation.SequenceNumber == 4
                && semanticPage == "NetworkSettings";
            var item = new GoalEvidence(
                satisfied,
                satisfied
                    ? $"fresh Observation proves goal after verified continuity (seq={observation.SequenceNumber})"
                    : $"GoalEvidence remains unsatisfied (seq={observation.SequenceNumber})",
                observation.SequenceNumber);
            evidence.Add(item);
            return item;
        });
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ScenarioIdentity.ResolveSemanticPage,
            CreateContainer,
            recovery);

        var finalState = await agent.RunAsync(
            goal,
            new Plan([new PlanStep("WiFi", "Tap"), new PlanStep("Dismiss", "Tap")]),
            RunId,
            CancellationToken.None);
        return new ScenarioRun(
            finalState,
            agent,
            traversal,
            environment,
            containers.ToImmutableArray(),
            evidence.ToImmutableArray(),
            progressSnapshots.ToImmutableArray(),
            continuityEvidence.ToImmutableArray());
    }

    private static ScriptedEnvironment CreateEnvironment(string branch)
        => branch switch
        {
            "continuous" => ScriptedEnvironmentVariants.PopupRuntimeContinuous(),
            "rejected" => ScriptedEnvironmentVariants.PopupRuntimeDismissRejected(),
            "page-changed" => ScriptedEnvironmentVariants.PopupRuntimePageChanged(),
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "未知 SC-P3-002 formal branch。"),
        };

    private sealed record ContinuityEvidence(
        long SequenceNumber,
        bool ForegroundCompatible,
        bool IsStillMine,
        string? SemanticPage);

    private sealed record ScenarioRun(
        RunState FinalState,
        RuntimeAgent Agent,
        RuntimeTraversal Traversal,
        ScriptedEnvironment Environment,
        ImmutableArray<RuntimeContainer> Containers,
        ImmutableArray<GoalEvidence> GoalEvidence,
        ImmutableArray<ImmutableArray<PlanStep>> ProgressSnapshots,
        ImmutableArray<ContinuityEvidence> ContinuityEvidence);
}
