using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-P3-001 Task 3.1 formal Scenario proof：TimedOut 是 dispatch outcome，不是 world result；
/// Agent 只从 fresh post-action Observation 生成 GoalEvidence 并保留 completion authority（I-4 / I-10）。
/// </summary>
public sealed class UncertainActionVerificationTests
{
    private const string EffectApplied = "uncertain-action-effect-applied";
    private const string EffectAbsent = "uncertain-action-effect-absent";

    [Fact]
    public async Task Positive_TimedOutAfterWorldTransition_ObservesEffect_CompletesFromGoalEvidence()
    {
        await AssertVariantDispatchesTimedOutAsync(
            ScriptedEnvironmentVariants.UncertainActionEffectApplied(),
            expectedObservedElement: "WiFi");
        var harness = ScenarioHarness.Create(EffectApplied);

        var finalState = await harness.RunAsync();

        Assert.Equal(RunState.Completed, finalState);
        Assert.Equal(ScenarioHarness.DefaultRunId, harness.RunId);
        AssertSinglePlannedTap(harness);

        var journal = Assert.Single(harness.Traversal.Journal);
        Assert.IsType<TraversalStepResult.Succeeded>(journal.Result);
        Assert.Equal(0, journal.RetryCount);
        var postAction = journal.PostActionObservation
            ?? throw new InvalidOperationException("正向 TimedOut 步骤缺少 fresh post-action Observation。");
        Assert.Equal(3, postAction.SequenceNumber);
        Assert.Equal("WiFi", Assert.Single(postAction.Elements).Text);

        Assert.Equal(2, harness.Evidence.Count); // CP-06：seq2 初始评估（未满足）+ seq3（满足）
        var evidence = harness.Evidence[1];
        Assert.True(evidence.Satisfied);
        Assert.Equal(postAction.SequenceNumber, evidence.SourceObservationSequence);
        var completed = Assert.Single(harness.Agent.Trace.Where(trace => trace.RunState == RunState.Completed));
        Assert.Equal(evidence.Reason, completed.Reason);
        AssertCompletedAfterActionEvidence(harness.Agent.Trace);
    }

    [Fact]
    public async Task Negative_TimedOutWithoutWorldTransition_ObservesAbsence_FailsWithoutRedispatch()
    {
        await AssertVariantDispatchesTimedOutAsync(
            ScriptedEnvironmentVariants.UncertainActionEffectAbsent(),
            expectedObservedElement: "Network & Internet");
        var harness = ScenarioHarness.Create(EffectAbsent);

        var finalState = await harness.RunAsync();

        Assert.Equal(RunState.Failed, finalState);
        Assert.Equal(ScenarioHarness.DefaultRunId, harness.RunId);
        AssertSinglePlannedTap(harness);

        var journal = Assert.Single(harness.Traversal.Journal);
        Assert.IsType<TraversalStepResult.Succeeded>(journal.Result); // local protocol evidence，不是 world/Goal success
        Assert.Equal(0, journal.RetryCount);
        var postAction = journal.PostActionObservation
            ?? throw new InvalidOperationException("负向 TimedOut 步骤缺少 fresh post-action Observation。");
        Assert.Equal(3, postAction.SequenceNumber);
        Assert.Equal("Network & Internet", Assert.Single(postAction.Elements).Text);
        Assert.DoesNotContain(postAction.Elements, element => element.Text == "WiFi");

        Assert.Equal(2, harness.Evidence.Count); // CP-06：seq2 初始评估 + seq3，均未满足
        var evidence = harness.Evidence[^1];
        Assert.False(evidence.Satisfied);
        Assert.Equal(postAction.SequenceNumber, evidence.SourceObservationSequence);
        Assert.DoesNotContain(harness.Agent.Trace, trace => trace.RunState == RunState.Completed);
        Assert.Contains("Plan 步数耗尽", harness.Agent.Reason, StringComparison.Ordinal);
        Assert.Null(harness.Agent.LastTrap);
        Assert.DoesNotContain(harness.Agent.Trace, trace => trace.RecoveryId is not null);
    }

    [Theory]
    [InlineData(EffectApplied)]
    [InlineData(EffectAbsent)]
    public async Task DeterministicReplay_SameRunIdEnvironmentAndActions_ProducesSameEvidence(string variant)
    {
        var first = ScenarioHarness.Create(variant);
        var second = ScenarioHarness.Create(variant);

        var firstState = await first.RunAsync();
        var secondState = await second.RunAsync();

        Assert.Equal(ScenarioHarness.DefaultRunId, first.RunId);
        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(firstState, secondState);
        Assert.Equal(first.Environment.ActionHistory.ToArray(), second.Environment.ActionHistory.ToArray());
        Assert.Equal(first.Agent.Trace.ToArray(), second.Agent.Trace.ToArray());
        Assert.Equal(first.Evidence.ToArray(), second.Evidence.ToArray());
        AssertSameJournal(first.Traversal.Journal, second.Traversal.Journal);
    }

    private static async Task AssertVariantDispatchesTimedOutAsync(
        ScriptedEnvironment environment,
        string expectedObservedElement)
    {
        await environment.ExecuteAsync(
            new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication),
            CancellationToken.None);
        var before = await environment.ObserveAsync(CancellationToken.None);

        var dispatch = await environment.ExecuteAsync(new DeviceAction.Tap(0), CancellationToken.None);
        var after = await environment.ObserveAsync(CancellationToken.None);

        Assert.Equal(ActionResultOutcome.TimedOut, dispatch.Outcome);
        Assert.True(after.SequenceNumber > before.SequenceNumber);
        Assert.Equal(expectedObservedElement, Assert.Single(after.Elements).Text);
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication),
                new DeviceAction.Tap(0),
            },
            environment.ActionHistory.ToArray());
    }

    private static void AssertSinglePlannedTap(ScenarioHarness harness)
    {
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(ScenarioHarness.TargetApplication),
                new DeviceAction.Tap(0),
            },
            harness.Environment.ActionHistory.ToArray());
        Assert.Single(harness.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        var actionTrace = Assert.Single(harness.Agent.Trace.Where(trace => trace.ActionId is not null));
        Assert.Equal(new DeviceAction.Tap(0), actionTrace.Action);
    }

    private static void AssertCompletedAfterActionEvidence(IReadOnlyList<DecisionRecord> trace)
    {
        var actionIndex = trace.ToList().FindIndex(entry => entry.ActionId is not null);
        var completedIndex = trace.ToList().FindIndex(entry => entry.RunState == RunState.Completed);
        Assert.True(actionIndex >= 0);
        Assert.True(completedIndex > actionIndex, "Completed 必须发生在 action trace 与 post-action GoalEvidence 之后。");
    }

    private static void AssertSameJournal(
        IReadOnlyList<TraversalJournalEntry> expected,
        IReadOnlyList<TraversalJournalEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].StepId, actual[i].StepId);
            Assert.Equal(expected[i].SelectedElementIndex, actual[i].SelectedElementIndex);
            Assert.Equal(expected[i].DispatchedAction, actual[i].DispatchedAction);
            Assert.Equal(expected[i].Result, actual[i].Result);
            Assert.Equal(expected[i].RetryCount, actual[i].RetryCount);
            AssertSameObservation(expected[i].PostActionObservation, actual[i].PostActionObservation);
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
        Assert.Equal(expected.Elements.Length, actual.Elements.Length);
        for (var i = 0; i < expected.Elements.Length; i++)
            Assert.Equal(expected.Elements[i], actual.Elements[i]);
    }
}
