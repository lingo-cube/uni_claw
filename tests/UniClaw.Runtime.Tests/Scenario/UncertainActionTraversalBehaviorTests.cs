using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-P3-001 Task 2.1 behavior proof：Traversal 在 TimedOut 后取得 fresh Observation 并复用既有
/// local verification；不重复派发，不进入 SC-P2-002 pre-dispatch retry，不判定 Goal completion。
/// 完整 Harness / Trace / deterministic replay 证据仍由 Task 3.1 购买。
/// </summary>
public sealed class UncertainActionTraversalBehaviorTests
{
    [Fact]
    public async Task TimedOut_WorldEffectApplied_ObservesFreshWorld_AndDispatchesExactlyOnce()
    {
        var (environment, transition) = CreateTimedOutEnvironment(applyWorldEffect: true);
        var before = await environment.ObserveAsync(CancellationToken.None);
        var traversal = new RuntimeTraversal(environment, maxRetries: 1);

        var result = traversal.ExecuteStep(
            new PlanStep("Action available", "Tap"),
            before,
            before.Elements);

        Assert.Equal(ActionResultOutcome.TimedOut, transition.DispatchOutcome);
        Assert.IsType<TraversalStepResult.Succeeded>(result); // 仅表示 local protocol 已取得 fresh evidence
        Assert.Equal(new DeviceAction[] { new DeviceAction.Tap(0) }, environment.ActionHistory.ToArray());

        var entry = Assert.Single(traversal.Journal);
        Assert.Equal(new DeviceAction.Tap(0), entry.DispatchedAction);
        Assert.Equal(0, entry.RetryCount); // TimedOut 不进入 SC-P2-002 pre-dispatch retry
        var postAction = entry.PostActionObservation
            ?? throw new InvalidOperationException("TimedOut 后缺少 fresh post-action Observation。");
        Assert.Equal(before.SequenceNumber + 1, postAction.SequenceNumber);
        Assert.Equal("Target reached", Assert.Single(postAction.Elements).Text);

        var evidence = EvaluateTargetEvidence(postAction);
        Assert.True(evidence.Satisfied); // 来自 Observation，而非 TimedOut
        Assert.Equal(postAction.SequenceNumber, evidence.SourceObservationSequence);
    }

    [Fact]
    public async Task TimedOut_WorldEffectAbsent_ObservesFreshWorld_WithoutGoalEvidenceOrRedispatch()
    {
        var (environment, transition) = CreateTimedOutEnvironment(applyWorldEffect: false);
        var before = await environment.ObserveAsync(CancellationToken.None);
        var traversal = new RuntimeTraversal(environment, maxRetries: 1);

        var result = traversal.ExecuteStep(
            new PlanStep("Action available", "Tap"),
            before,
            before.Elements);

        Assert.Equal(ActionResultOutcome.TimedOut, transition.DispatchOutcome);
        Assert.IsType<TraversalStepResult.Succeeded>(result); // local protocol result，不是 semantic action/world success
        Assert.Equal(new DeviceAction[] { new DeviceAction.Tap(0) }, environment.ActionHistory.ToArray());

        var entry = Assert.Single(traversal.Journal);
        Assert.Equal(0, entry.RetryCount);
        var postAction = entry.PostActionObservation
            ?? throw new InvalidOperationException("TimedOut 后缺少 fresh post-action Observation。");
        Assert.Equal(before.SequenceNumber + 1, postAction.SequenceNumber);
        Assert.Equal("Action available", Assert.Single(postAction.Elements).Text);
        Assert.DoesNotContain(postAction.Elements, element => element.Text == "Target reached");

        var evidence = EvaluateTargetEvidence(postAction);
        Assert.False(evidence.Satisfied); // world evidence 不支持目标，TimedOut 不得编造 GoalEvidence
        Assert.Equal(postAction.SequenceNumber, evidence.SourceObservationSequence);
    }

    private static GoalEvidence EvaluateTargetEvidence(Observation observation)
    {
        var satisfied = observation.Elements.Any(element => element.Text == "Target reached");
        return new GoalEvidence(
            satisfied,
            satisfied ? "目标世界可见。" : "目标世界不可见。",
            observation.SequenceNumber);
    }

    private static (ScriptedEnvironment Environment, TransitionConfig Transition)
        CreateTimedOutEnvironment(bool applyWorldEffect)
    {
        const string beforeScreen = "Before";
        const string afterScreen = "After";
        var transition = new TransitionConfig(
            ScreenTransitionAction.Tap,
            applyWorldEffect ? afterScreen : beforeScreen,
            DispatchOutcome: ActionResultOutcome.TimedOut);
        var environment = new ScriptedEnvironment(
            beforeScreen,
            launchNextScreenName: null,
            [
                new ScreenConfig(
                    beforeScreen,
                    "Settings",
                    [new ElementConfig("Action available", SwitchState: null, Transition: transition)]),
                new ScreenConfig(
                    afterScreen,
                    "Settings",
                    [new ElementConfig("Target reached", SwitchState: null, Transition: null)]),
            ]);
        return (environment, transition);
    }
}
