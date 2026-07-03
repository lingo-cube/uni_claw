using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.Phase2;

// ===== TraversalFSM Tests =====

public class TraversalFSMTests
{
    [Fact]
    public void TraversalState_9ValuesIncludingDynamicMatch()
    {
        var values = Enum.GetValues<TraversalState>();
        // 9 values exist (including legacy DynamicMatch which is NOT an FSM state)
        Assert.Equal(9, values.Length);
        Assert.True(Enum.IsDefined(TraversalState.DynamicMatch));
    }

    [Fact]
    public void TransitionMatrix_8SourceStatesCovered_DynamicMatchNotInMatrix()
    {
        Assert.Equal(8, TraversalFSM.TransitionMatrix.Count);
        // DynamicMatch is NOT in the transition matrix (it's a ChildrenStrategyType, not an FSM state)
        Assert.False(TraversalFSM.TransitionMatrix.ContainsKey(TraversalState.DynamicMatch));
        foreach (var state in TraversalFSM.TransitionMatrix.Keys)
            Assert.True(TraversalFSM.TransitionMatrix.ContainsKey(state));
    }

    [Fact]
    public void TransitionMatrix_NoSelfLoops()
    {
        foreach (var (source, targets) in TraversalFSM.TransitionMatrix)
            Assert.DoesNotContain(source, targets);
    }

    [Fact]
    public void TransitionMatrix_ValidTransitionsAccepted()
    {
        var ctx = new TraversalRuntimeContext("test");
        var fsm = new TraversalFSM(ctx);

        // Test each valid transition sequentially
        // Start at NodeSelect → Branch (empty stack)
        fsm.TransitionTo(TraversalState.Branch); // NodeSelect → Branch
        Assert.Equal(TraversalState.Branch, fsm.CurrentState);
    }

    [Fact]
    public void TransitionMatrix_PreconditionCheckToBranch_Rejected()
    {
        var ctx = new TraversalRuntimeContext("test");
        var fsm = new TraversalFSM(ctx);

        // Drive to PreconditionCheck via valid path:
        // NodeSelect → Branch → NodeSelect (with stack) → PreconditionCheck
        var node = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.NodeStack.Push(node);
        fsm.TransitionTo(TraversalState.Branch);
        fsm.TransitionTo(TraversalState.NodeSelect);
        fsm.TransitionTo(TraversalState.PreconditionCheck);

        // Now try the D-1 forbidden transition
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(TraversalState.Branch));
    }

    [Fact]
    public void TransitionMatrix_InvalidTransitionsRejected()
    {
        var ctx = new TraversalRuntimeContext("test");
        var fsm = new TraversalFSM(ctx);
        // NodeSelect only allows PreconditionCheck and Branch
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(TraversalState.Execute));
    }

    [Fact]
    public void Step_NodeSelectWithEmptyStack_GoesToBranch()
    {
        var ctx = new TraversalRuntimeContext("test");
        var fsm = new TraversalFSM(ctx);
        var next = fsm.Step();
        Assert.Equal(TraversalState.Branch, next);
    }

    [Fact]
    public void Step_NodeSelectWithStack_GoesToPreconditionCheck()
    {
        var ctx = new TraversalRuntimeContext("test");
        var node = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);
        var fsm = new TraversalFSM(ctx);
        var next = fsm.Step();
        Assert.Equal(TraversalState.PreconditionCheck, next);
    }
}

// ===== CompletionDetector Tests =====

public class CompletionDetectorTests
{
    [Fact]
    public void Timeout_Priority1()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(30000, 10000, 3, 10, 5, 3, FallbackAction.Back);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.Timeout, result.Reason);
        Assert.True(result.ShouldBacktrack);
    }

    [Fact]
    public void MaxDepth_Priority2()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(5000, 10000, 11, 10, 5, 3, FallbackAction.Back);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.MaxDepth, result.Reason);
        Assert.True(result.ShouldBacktrack);
    }

    [Fact]
    public void NoChildren_Priority3()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(5000, 10000, 3, 10, 0, 0, FallbackAction.Back);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.AllVisited, result.Reason);
    }

    [Fact]
    public void AllVisited_Priority4()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(5000, 10000, 3, 10, 5, 5, FallbackAction.Skip);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.AllVisited, result.Reason);
        Assert.Equal(FallbackAction.Skip, result.SuggestedAction);
    }

    [Fact]
    public void Incomplete_Priority5()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(5000, 10000, 3, 10, 5, 3, FallbackAction.Back);
        var result = detector.DetectCompletion(ctx);
        Assert.False(result.IsComplete);
        Assert.Equal(CompletionReason.Incomplete, result.Reason);
    }
}

// ===== FallbackDecider Tests =====

public class FallbackDeciderTests
{
    [Fact]
    public void Timeout_AlwaysBack() =>
        Assert.Equal(FallbackAction.Back, new FallbackDecider().DecideFallback(
            new CompletionResult(true, CompletionReason.Timeout, FallbackAction.Skip, true), true));

    [Fact]
    public void AllVisited_UsesSuggested() =>
        Assert.Equal(FallbackAction.Skip, new FallbackDecider().DecideFallback(
            new CompletionResult(true, CompletionReason.AllVisited, FallbackAction.Skip, false), true));

    [Fact]
    public void CannotContinue_Back() =>
        Assert.Equal(FallbackAction.Back, new FallbackDecider().DecideFallback(
            new CompletionResult(false, CompletionReason.Incomplete, FallbackAction.Skip, false), false));

    [Fact]
    public void Incomplete_CanContinue_Skip() =>
        Assert.Equal(FallbackAction.Skip, new FallbackDecider().DecideFallback(
            new CompletionResult(false, CompletionReason.Incomplete, FallbackAction.Skip, false), true));
}

// ===== ContainerActionExecutor Tests =====

public class ContainerActionExecutorTests
{
    [Fact]
    public void Execute_All4Hooks()
    {
        var executor = new ContainerActionExecutor();
        var ctx = new ContainerContext("n1", 3, new TraversalRuntimeContext("test"));
        Assert.Equal(FallbackAction.Back, executor.Execute(FallbackAction.Back, ctx).Action);
        Assert.Equal(FallbackAction.AutoEscape, executor.Execute(FallbackAction.AutoEscape, ctx).Action);
        Assert.Equal(FallbackAction.Skip, executor.Execute(FallbackAction.Skip, ctx).Action);
        Assert.Equal(FallbackAction.Abort, executor.Execute(FallbackAction.Abort, ctx).Action);
    }

    [Fact]
    public void Execute_ExceptionFallbackToBack()
    {
        var executor = new ContainerActionExecutor(
            backHook: _ => throw new InvalidOperationException());
        var ctx = new ContainerContext("n1", 3, new TraversalRuntimeContext("test"));
        Assert.Equal(FallbackAction.Back, executor.Execute(FallbackAction.Back, ctx).Action);
    }
}

// ===== ErrorClassifier Tests =====

public class ErrorClassifierTests
{
    [Fact] public void Crash() => Assert.Equal(ErrorType.Crash, new ErrorClassifier().Classify(new ErrorClassificationContext("App crash detected")));
    [Fact] public void Permission() => Assert.Equal(ErrorType.Permission, new ErrorClassifier().Classify(new ErrorClassificationContext("Permission denied")));
    [Fact] public void Timeout() => Assert.Equal(ErrorType.Timeout, new ErrorClassifier().Classify(new ErrorClassificationContext("Operation timed out")));
    [Fact] public void Network() => Assert.Equal(ErrorType.Network, new ErrorClassifier().Classify(new ErrorClassificationContext("Network connection lost")));
    [Fact] public void UiElement() => Assert.Equal(ErrorType.UiElement, new ErrorClassifier().Classify(new ErrorClassificationContext("Element not found")));
    [Fact] public void Unknown() => Assert.Equal(ErrorType.Unknown, new ErrorClassifier().Classify(new ErrorClassificationContext("Something weird")));
}

// ===== ErrorStrategySelector Tests =====

public class ErrorStrategySelectorTests
{
    [Fact] public void Crash_Abort() => Assert.Equal(ErrorStrategy.Abort,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Crash, new StrategySelectionContext(0, 3, true, 5, true)));

    [Fact] public void Timeout_RetryUnderMax() => Assert.Equal(ErrorStrategy.Retry,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Timeout, new StrategySelectionContext(1, 3, true, 5, true)));

    [Fact] public void Timeout_ContinueWhenMaxed() => Assert.Equal(ErrorStrategy.Continue,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Timeout, new StrategySelectionContext(3, 3, true, 5, true)));

    [Fact] public void BacktrackNotApplicableWhenDepth1() => Assert.Equal(ErrorStrategy.Abort,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Permission, new StrategySelectionContext(0, 3, true, 1, true)));
}

// ===== RecoveryExecutor Tests =====

public class RecoveryExecutorTests
{
    [Fact]
    public void RetryBackoff_4Seconds()
    {
        var executor = new RecoveryExecutor();
        var result = executor.Execute(ErrorStrategy.Retry, new ErrorRecoveryContext(ErrorType.Network, 2));
        Assert.Equal(4.0, result.BackoffDelaySeconds); // min(2^2, 10) = 4
    }

    [Fact]
    public void BackoffCappedAt10() => Assert.Equal(10, RecoveryExecutor.CalculateBackoffDelay(10));

    [Fact]
    public void AbortReturnsFailure()
    {
        var executor = new RecoveryExecutor();
        var result = executor.Execute(ErrorStrategy.Abort, new ErrorRecoveryContext(ErrorType.Crash, 0));
        Assert.Equal(RecoveryOutcome.Failure, result.Outcome);
    }

    [Fact]
    public void ExceptionFallbackToAbort()
    {
        var executor = new RecoveryExecutor(retryHook: _ => throw new InvalidOperationException());
        Assert.Equal(ErrorStrategy.Abort, executor.Execute(ErrorStrategy.Retry, new ErrorRecoveryContext(ErrorType.Network, 0)).Strategy);
    }
}

// ===== PopupDetector Tests =====

public class PopupDetectorTests
{
    [Fact] public void Permission() => Assert.Equal(PopupType.Permission, new PopupDetector().Detect("Allow access"));
    [Fact] public void Error() => Assert.Equal(PopupType.Error, new PopupDetector().Detect("An error occurred"));
    [Fact] public void Ad() => Assert.Equal(PopupType.Ad, new PopupDetector().Detect("Sponsored content"));
    [Fact] public void Dialog() => Assert.Equal(PopupType.Dialog, new PopupDetector().Detect("Confirm selection"));
    [Fact] public void Unknown() => Assert.Equal(PopupType.Unknown, new PopupDetector().Detect("xyz no match"));
    [Fact] public void PriorityPermissionOverError() => Assert.Equal(PopupType.Permission, new PopupDetector().Detect("Permission denied error"));
}

// ===== PopupClassifier Tests =====

public class PopupClassifierTests
{
    [Fact]
    public void Classify_PermissionDismissPriority()
    {
        var result = new PopupClassifier().Classify("Allow access", new List<string> { "deny", "allow", "ok" });
        Assert.Equal(PopupType.Permission, result.PopupType);
        Assert.Equal("allow", result.DismissTarget);
        Assert.Equal(DismissStrategy.AutoClose, result.DismissStrategy);
    }

    [Fact]
    public void Classify_ErrorDismissStrategy()
    {
        var result = new PopupClassifier().Classify("Error occurred");
        Assert.Equal(PopupType.Error, result.PopupType);
        Assert.Equal(DismissStrategy.Back, result.DismissStrategy);
    }
}

// ===== GlobalFSM Tests =====

public class GlobalFSMTests
{
    [Fact]
    public void GlobalState_8Values() => Assert.Equal(8, Enum.GetValues<GlobalState>().Length);

    [Fact]
    public void TransitionMatrix_ErrorNotToTraversing()
    {
        // Drive FSM to Error state via valid path:
        // Idle → Initializing → Error
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Error);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Traversing));
    }

    [Fact]
    public void TransitionMatrix_RecoveringNotToTraversing()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Error);
        fsm.TransitionTo(GlobalState.Recovering);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Traversing));
    }

    [Fact]
    public void TransitionMatrix_IdleOnlyToInitializing()
    {
        var fsm = new GlobalFSM();
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Traversing));
    }

    [Fact]
    public void TransitionMatrix_CompletedIsTerminal()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        fsm.TransitionTo(GlobalState.Completed);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Idle));
    }

    [Fact]
    public void TransitionMatrix_TerminatedIsTerminal()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        fsm.TransitionTo(GlobalState.Paused);
        fsm.TransitionTo(GlobalState.Terminated);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Idle));
    }

    [Fact]
    public void ValidTransition_IdleToInitializing()
    {
        var fsm = new GlobalFSM();
        var result = fsm.TransitionTo(GlobalState.Initializing);
        Assert.Equal(GlobalState.Initializing, fsm.CurrentState);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Callback_InvokedOnStateEntry()
    {
        var fsm = new GlobalFSM();
        var invoked = false;
        fsm.RegisterStateCallback(GlobalState.Initializing, _ => invoked = true);
        fsm.TransitionTo(GlobalState.Initializing);
        Assert.True(invoked);
    }

    [Fact]
    public void Callback_ExceptionNotPropagated()
    {
        var fsm = new GlobalFSM();
        fsm.RegisterStateCallback(GlobalState.Initializing, _ => throw new InvalidOperationException());
        var result = fsm.TransitionTo(GlobalState.Initializing);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TransitionHistory_RecordsChanges()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        var history = fsm.GetTransitionHistory();
        Assert.Equal(2, history.Count);
        Assert.Equal(GlobalState.Idle, history[0].FromState);
    }

    [Fact]
    public void TransitionHistory_FailedNotRecorded()
    {
        var fsm = new GlobalFSM();
        try { fsm.TransitionTo(GlobalState.Traversing); } catch { }
        Assert.Empty(fsm.GetTransitionHistory());
    }

    [Fact]
    public void RecoveryPath_ErrorToRecoveringToInitializingToTraversing()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        fsm.TransitionTo(GlobalState.Error);
        fsm.TransitionTo(GlobalState.Recovering);
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        Assert.Equal(GlobalState.Traversing, fsm.CurrentState);
    }
}

// ===== Snapshot Isolation Tests =====

public class SnapshotIsolationTests
{
    [Fact]
    public void CreateReadOnlySnapshot_SnapshotUnaffectedByEngineModification()
    {
        var ctx = new TraversalRuntimeContext("test", maxDepth: 10);
        ctx.MarkVisited("home");
        ctx.MarkNodeVisited("node-1");
        ctx.IncrementStepCount();
        ctx.AppendPath("home");

        var snapshot = ctx.CreateReadOnlySnapshot();
        Assert.Contains("home", snapshot.VisitedPages);
        Assert.Contains("node-1", snapshot.VisitedNodes);
        Assert.Equal(1, snapshot.StepCount);

        ctx.MarkVisited("settings");
        ctx.MarkNodeVisited("node-2");
        ctx.IncrementStepCount();

        Assert.DoesNotContain("settings", snapshot.VisitedPages);
        Assert.DoesNotContain("node-2", snapshot.VisitedNodes);
        Assert.Equal(1, snapshot.StepCount);
    }
}
