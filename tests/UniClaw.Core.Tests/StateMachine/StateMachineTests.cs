using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

// ===== TraversalFSM Tests =====

public class TraversalFSMTests
{
    [Fact(DisplayName = "FSM状态: TraversalState枚举有8个值,不含DynamicMatch")]
    public void TraversalState_8ValuesExcludingDynamicMatch()
    {
        var values = Enum.GetValues<TraversalState>();
        // Exactly 8 FSM states — DynamicMatch removed (it's a ChildrenStrategy value, not an FSM state)
        Assert.Equal(8, values.Length);
        Assert.False(Enum.IsDefined(typeof(TraversalState), "DynamicMatch"));
    }

    [Fact(DisplayName = "FSM迁移矩阵: 8个源状态全覆盖,所有TraversalState值在矩阵中")]
    public void TransitionMatrix_8SourceStatesCovered_AllStatesInMatrix()
    {
        Assert.Equal(8, TraversalFSM.TransitionMatrix.Count);
        // DynamicMatch is no longer in TraversalState enum (it's a ChildrenStrategy value)
        // All 8 TraversalState values are in the transition matrix
        foreach (TraversalState state in Enum.GetValues<TraversalState>())
            Assert.True(TraversalFSM.TransitionMatrix.ContainsKey(state));
    }

    [Fact(DisplayName = "FSM迁移矩阵: 无自环,任何状态不能迁移到自身")]
    public void TransitionMatrix_NoSelfLoops()
    {
        foreach (var (source, targets) in TraversalFSM.TransitionMatrix)
            Assert.DoesNotContain(source, targets);
    }

    [Fact(DisplayName = "FSM迁移: NodeSelect→Branch有效迁移被接受")]
    public void TransitionMatrix_ValidTransitionsAccepted()
    {
        var ctx = new TraversalRuntimeContext("test");
        var fsm = new TraversalFSM(ctx);

        // Test each valid transition sequentially
        // Start at NodeSelect → Branch (empty stack)
        fsm.TransitionTo(TraversalState.Branch); // NodeSelect → Branch
        Assert.Equal(TraversalState.Branch, fsm.CurrentState);
    }

    [Fact(DisplayName = "FSM迁移约束: PreconditionCheck→Branch被拒绝(D-1禁止)")]
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

    [Fact(DisplayName = "FSM迁移约束: NodeSelect→Execute无效迁移被拒绝")]
    public void TransitionMatrix_InvalidTransitionsRejected()
    {
        var ctx = new TraversalRuntimeContext("test");
        var fsm = new TraversalFSM(ctx);
        // NodeSelect only allows PreconditionCheck and Branch
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(TraversalState.Execute));
    }

    [Fact(DisplayName = "FSM步进: NodeSelect空栈 → Branch")]
    public void Step_NodeSelectWithEmptyStack_GoesToBranch()
    {
        var ctx = new TraversalRuntimeContext("test");
        var fsm = new TraversalFSM(ctx);
        var next = fsm.Step();
        Assert.Equal(TraversalState.Branch, next);
    }

    [Fact(DisplayName = "FSM步进: NodeSelect有栈 → PreconditionCheck")]
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
    [Fact(DisplayName = "完成检测: 超时优先级1 → IsComplete+Timeout+ShouldBacktrack")]
    public void Timeout_Priority1()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(30000, 10000, 3, 10, 5, 3, FallbackAction.Back);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.Timeout, result.Reason);
        Assert.True(result.ShouldBacktrack);
    }

    [Fact(DisplayName = "完成检测: 最大深度优先级2 → IsComplete+MaxDepth+ShouldBacktrack")]
    public void MaxDepth_Priority2()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(5000, 10000, 11, 10, 5, 3, FallbackAction.Back);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.MaxDepth, result.Reason);
        Assert.True(result.ShouldBacktrack);
    }

    [Fact(DisplayName = "完成检测: 无子节点优先级3 → IsComplete+AllVisited")]
    public void NoChildren_Priority3()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(5000, 10000, 3, 10, 0, 0, FallbackAction.Back);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.AllVisited, result.Reason);
    }

    [Fact(DisplayName = "完成检测: 所有子节点已访问优先级4 → IsComplete+AllVisited+SuggestedAction")]
    public void AllVisited_Priority4()
    {
        var detector = new CompletionDetector();
        var ctx = new CompletionContext(5000, 10000, 3, 10, 5, 5, FallbackAction.Skip);
        var result = detector.DetectCompletion(ctx);
        Assert.True(result.IsComplete);
        Assert.Equal(CompletionReason.AllVisited, result.Reason);
        Assert.Equal(FallbackAction.Skip, result.SuggestedAction);
    }

    [Fact(DisplayName = "完成检测: 不完整优先级5 → NotComplete+Incomplete")]
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
    [Fact(DisplayName = "回退决策: 超时 → 始终Back")]
    public void Timeout_AlwaysBack() =>
        Assert.Equal(FallbackAction.Back, new FallbackDecider().DecideFallback(
            new CompletionResult(true, CompletionReason.Timeout, FallbackAction.Skip, true), true));

    [Fact(DisplayName = "回退决策: 所有子节点已访问 → 使用CompletionResult建议的FallbackAction")]
    public void AllVisited_UsesSuggested() =>
        Assert.Equal(FallbackAction.Skip, new FallbackDecider().DecideFallback(
            new CompletionResult(true, CompletionReason.AllVisited, FallbackAction.Skip, false), true));

    [Fact(DisplayName = "回退决策: 不能继续 → Back")]
    public void CannotContinue_Back() =>
        Assert.Equal(FallbackAction.Back, new FallbackDecider().DecideFallback(
            new CompletionResult(false, CompletionReason.Incomplete, FallbackAction.Skip, false), false));

    [Fact(DisplayName = "回退决策: 不完整但可继续 → Skip")]
    public void Incomplete_CanContinue_Skip() =>
        Assert.Equal(FallbackAction.Skip, new FallbackDecider().DecideFallback(
            new CompletionResult(false, CompletionReason.Incomplete, FallbackAction.Skip, false), true));
}

// ===== ContainerActionExecutor Tests =====

public class ContainerActionExecutorTests
{
    [Fact(DisplayName = "容器动作执行: 4种FallbackAction全部可执行")]
    public void Execute_All4Hooks()
    {
        var executor = new ContainerActionExecutor();
        var ctx = new ContainerContext("n1", 3, new TraversalRuntimeContext("test"));
        Assert.Equal(FallbackAction.Back, executor.Execute(FallbackAction.Back, ctx).Action);
        Assert.Equal(FallbackAction.AutoEscape, executor.Execute(FallbackAction.AutoEscape, ctx).Action);
        Assert.Equal(FallbackAction.Skip, executor.Execute(FallbackAction.Skip, ctx).Action);
        Assert.Equal(FallbackAction.Abort, executor.Execute(FallbackAction.Abort, ctx).Action);
    }

    [Fact(DisplayName = "容器动作执行: Hook抛异常 → 回退到Back")]
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
    [Fact(DisplayName = "错误分类: App崩溃消息 → ErrorType.Crash")] public void Crash() => Assert.Equal(ErrorType.Crash, new ErrorClassifier().Classify(new ErrorClassificationContext("App crash detected")));
    [Fact(DisplayName = "错误分类: 权限拒绝消息 → ErrorType.Permission")] public void Permission() => Assert.Equal(ErrorType.Permission, new ErrorClassifier().Classify(new ErrorClassificationContext("Permission denied")));
    [Fact(DisplayName = "错误分类: 超时消息 → ErrorType.Timeout")] public void Timeout() => Assert.Equal(ErrorType.Timeout, new ErrorClassifier().Classify(new ErrorClassificationContext("Operation timed out")));
    [Fact(DisplayName = "错误分类: 网络断连消息 → ErrorType.Network")] public void Network() => Assert.Equal(ErrorType.Network, new ErrorClassifier().Classify(new ErrorClassificationContext("Network connection lost")));
    [Fact(DisplayName = "错误分类: 元素未找到消息 → ErrorType.UiElement")] public void UiElement() => Assert.Equal(ErrorType.UiElement, new ErrorClassifier().Classify(new ErrorClassificationContext("Element not found")));
    [Fact(DisplayName = "错误分类: 无法识别消息 → ErrorType.Unknown")] public void Unknown() => Assert.Equal(ErrorType.Unknown, new ErrorClassifier().Classify(new ErrorClassificationContext("Something weird")));
}

// ===== ErrorStrategySelector Tests =====

public class ErrorStrategySelectorTests
{
    [Fact(DisplayName = "错误策略: Crash → Abort")] public void Crash_Abort() => Assert.Equal(ErrorStrategy.Abort,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Crash, new StrategySelectionContext(0, 3, true, 5, true)));

    [Fact(DisplayName = "错误策略: 超时未达上限 → Retry")] public void Timeout_RetryUnderMax() => Assert.Equal(ErrorStrategy.Retry,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Timeout, new StrategySelectionContext(1, 3, true, 5, true)));

    [Fact(DisplayName = "错误策略: 超时已达上限 → Continue")] public void Timeout_ContinueWhenMaxed() => Assert.Equal(ErrorStrategy.Continue,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Timeout, new StrategySelectionContext(3, 3, true, 5, true)));

    [Fact(DisplayName = "错误策略: 深度1时Backtrack不可用 → Abort")] public void BacktrackNotApplicableWhenDepth1() => Assert.Equal(ErrorStrategy.Abort,
        new ErrorStrategySelector().SelectStrategy(ErrorType.Permission, new StrategySelectionContext(0, 3, true, 1, true)));
}

// ===== RecoveryExecutor Tests =====

public class RecoveryExecutorTests
{
    [Fact(DisplayName = "恢复执行: Retry第2次 → 退避4秒(min(2^2,10))")]
    public void RetryBackoff_4Seconds()
    {
        var executor = new RecoveryExecutor();
        var result = executor.Execute(ErrorStrategy.Retry, new ErrorRecoveryContext(ErrorType.Network, 2));
        Assert.Equal(4.0, result.BackoffDelaySeconds); // min(2^2, 10) = 4
    }

    [Fact(DisplayName = "恢复执行: 退避延迟上限10秒")]
    public void BackoffCappedAt10() => Assert.Equal(10, RecoveryExecutor.CalculateBackoffDelay(10));

    [Fact(DisplayName = "恢复执行: Abort → RecoveryOutcome.Failure")]
    public void AbortReturnsFailure()
    {
        var executor = new RecoveryExecutor();
        var result = executor.Execute(ErrorStrategy.Abort, new ErrorRecoveryContext(ErrorType.Crash, 0));
        Assert.Equal(RecoveryOutcome.Failure, result.Outcome);
    }

    [Fact(DisplayName = "恢复执行: RetryHook抛异常 → 回退到Abort")]
    public void ExceptionFallbackToAbort()
    {
        var executor = new RecoveryExecutor(retryHook: _ => throw new InvalidOperationException());
        Assert.Equal(ErrorStrategy.Abort, executor.Execute(ErrorStrategy.Retry, new ErrorRecoveryContext(ErrorType.Network, 0)).Strategy);
    }
}

// ===== PopupDetector Tests =====

public class PopupDetectorTests
{
    [Fact(DisplayName = "弹窗检测: 权限关键词 → PopupType.Permission")] public void Permission() => Assert.Equal(PopupType.Permission, new PopupDetector().Detect("Allow access"));
    [Fact(DisplayName = "弹窗检测: 错误关键词 → PopupType.Error")] public void Error() => Assert.Equal(PopupType.Error, new PopupDetector().Detect("An error occurred"));
    [Fact(DisplayName = "弹窗检测: 广告关键词 → PopupType.Ad")] public void Ad() => Assert.Equal(PopupType.Ad, new PopupDetector().Detect("Sponsored content"));
    [Fact(DisplayName = "弹窗检测: 对话关键词 → PopupType.Dialog")] public void Dialog() => Assert.Equal(PopupType.Dialog, new PopupDetector().Detect("Confirm selection"));
    [Fact(DisplayName = "弹窗检测: 无匹配关键词 → PopupType.Unknown")] public void Unknown() => Assert.Equal(PopupType.Unknown, new PopupDetector().Detect("xyz no match"));
    [Fact(DisplayName = "弹窗检测: 权限优先级高于错误 → Permission")] public void PriorityPermissionOverError() => Assert.Equal(PopupType.Permission, new PopupDetector().Detect("Permission denied error"));
}

// ===== PopupClassifier Tests =====

public class PopupClassifierTests
{
    [Fact(DisplayName = "弹窗分类: Permission弹窗有关闭目标 → AutoClose+DismissTarget")]
    public void Classify_PermissionDismissPriority()
    {
        var result = new PopupClassifier().Classify("Allow access", new List<string> { "deny", "allow", "ok" });
        Assert.Equal(PopupType.Permission, result.PopupType);
        Assert.Equal("allow", result.DismissTarget);
        Assert.Equal(DismissStrategy.AutoClose, result.DismissStrategy);
    }

    [Fact(DisplayName = "弹窗分类: Error弹窗无关闭目标 → AutoCloseOrBack(D-10)")]
    public void Classify_ErrorNoTarget_AutoCloseOrBack()
    {
        // D-10: Error popup without dismiss target → AutoCloseOrBack (Python: "auto_close_or_back")
        var result = new PopupClassifier().Classify("Error occurred");
        Assert.Equal(PopupType.Error, result.PopupType);
        Assert.Null(result.DismissTarget);
        Assert.Equal(DismissStrategy.AutoCloseOrBack, result.DismissStrategy);
    }

    [Fact(DisplayName = "弹窗分类: Error弹窗有关闭目标 → AutoClose(D-10)")]
    public void Classify_ErrorWithTarget_AutoClose()
    {
        // D-10: Error popup WITH dismiss target → AutoClose (Python: "auto_close" when target found)
        var result = new PopupClassifier().Classify("Error occurred", new List<string> { "ok", "close" });
        Assert.Equal(PopupType.Error, result.PopupType);
        Assert.Equal("ok", result.DismissTarget);
        Assert.Equal(DismissStrategy.AutoClose, result.DismissStrategy);
    }

    [Fact(DisplayName = "弹窗分类: Permission弹窗无关闭目标 → WaitTimeout(D-10)")]
    public void Classify_PermissionNoTarget_WaitTimeout()
    {
        // D-10: Permission popup without dismiss target → WaitTimeout (Python: "wait_timeout")
        var result = new PopupClassifier().Classify("Allow access to location");
        Assert.Equal(PopupType.Permission, result.PopupType);
        Assert.Null(result.DismissTarget);
        Assert.Equal(DismissStrategy.WaitTimeout, result.DismissStrategy);
    }

    [Fact(DisplayName = "弹窗分类: Ad弹窗无关闭目标 → Back(D-10)")]
    public void Classify_AdNoTarget_Back()
    {
        // D-10: Ad popup without dismiss target → Back (Python: "back")
        var result = new PopupClassifier().Classify("Sponsored content");
        Assert.Equal(PopupType.Ad, result.PopupType);
        Assert.Null(result.DismissTarget);
        Assert.Equal(DismissStrategy.Back, result.DismissStrategy);
    }

    [Fact(DisplayName = "弹窗分类: Ad弹窗有关闭目标 → AutoClose(D-10)")]
    public void Classify_AdWithTarget_AutoClose()
    {
        // D-10: Ad popup WITH dismiss target → AutoClose (Python: "auto_close" when target found)
        var result = new PopupClassifier().Classify("Sponsored content", new List<string> { "close", "skip" });
        Assert.Equal(PopupType.Ad, result.PopupType);
        Assert.Equal("close", result.DismissTarget);
        Assert.Equal(DismissStrategy.AutoClose, result.DismissStrategy);
    }

    [Fact(DisplayName = "弹窗分类: Dialog弹窗无关闭目标 → Back(D-10)")]
    public void Classify_DialogNoTarget_Back()
    {
        // D-10: Dialog popup without dismiss target → Back (Python: "back")
        var result = new PopupClassifier().Classify("Confirm your action");
        Assert.Equal(PopupType.Dialog, result.PopupType);
        Assert.Null(result.DismissTarget);
        Assert.Equal(DismissStrategy.Back, result.DismissStrategy);
    }

    [Fact(DisplayName = "弹窗分类: Dialog弹窗有关闭目标 → AutoClose(D-10)")]
    public void Classify_DialogWithTarget_AutoClose()
    {
        // D-10: Dialog popup WITH dismiss target → AutoClose (Python: "auto_close" when target found)
        var result = new PopupClassifier().Classify("Confirm your action", new List<string> { "ok", "cancel" });
        Assert.Equal(PopupType.Dialog, result.PopupType);
        Assert.Equal("ok", result.DismissTarget);
        Assert.Equal(DismissStrategy.AutoClose, result.DismissStrategy);
    }
}

// ===== GlobalFSM Tests =====

public class GlobalFSMTests
{
    [Fact(DisplayName = "全局FSM: GlobalState枚举有8个值")]
    public void GlobalState_8Values() => Assert.Equal(8, Enum.GetValues<GlobalState>().Length);

    [Fact(DisplayName = "全局FSM迁移: Error不能直接到Traversing")]
    public void TransitionMatrix_ErrorNotToTraversing()
    {
        // Drive FSM to Error state via valid path:
        // Idle → Initializing → Error
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Error);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Traversing));
    }

    [Fact(DisplayName = "全局FSM迁移: Recovering不能直接到Traversing")]
    public void TransitionMatrix_RecoveringNotToTraversing()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Error);
        fsm.TransitionTo(GlobalState.Recovering);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Traversing));
    }

    [Fact(DisplayName = "全局FSM迁移: Idle只能到Initializing")]
    public void TransitionMatrix_IdleOnlyToInitializing()
    {
        var fsm = new GlobalFSM();
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Traversing));
    }

    [Fact(DisplayName = "全局FSM迁移: Completed是终态,不可离开")]
    public void TransitionMatrix_CompletedIsTerminal()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        fsm.TransitionTo(GlobalState.Completed);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Idle));
    }

    [Fact(DisplayName = "全局FSM迁移: Terminated是终态,不可离开")]
    public void TransitionMatrix_TerminatedIsTerminal()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        fsm.TransitionTo(GlobalState.Paused);
        fsm.TransitionTo(GlobalState.Terminated);
        Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(GlobalState.Idle));
    }

    [Fact(DisplayName = "全局FSM迁移: Idle→Initializing有效迁移")]
    public void ValidTransition_IdleToInitializing()
    {
        var fsm = new GlobalFSM();
        var result = fsm.TransitionTo(GlobalState.Initializing);
        Assert.Equal(GlobalState.Initializing, fsm.CurrentState);
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "全局FSM回调: 进入状态时回调被调用")]
    public void Callback_InvokedOnStateEntry()
    {
        var fsm = new GlobalFSM();
        var invoked = false;
        fsm.RegisterStateCallback(GlobalState.Initializing, _ => invoked = true);
        fsm.TransitionTo(GlobalState.Initializing);
        Assert.True(invoked);
    }

    [Fact(DisplayName = "全局FSM回调: 回调抛异常不传播,迁移仍然成功")]
    public void Callback_ExceptionNotPropagated()
    {
        var fsm = new GlobalFSM();
        fsm.RegisterStateCallback(GlobalState.Initializing, _ => throw new InvalidOperationException());
        var result = fsm.TransitionTo(GlobalState.Initializing);
        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "全局FSM历史: 迁移历史记录状态变更")]
    public void TransitionHistory_RecordsChanges()
    {
        var fsm = new GlobalFSM();
        fsm.TransitionTo(GlobalState.Initializing);
        fsm.TransitionTo(GlobalState.Traversing);
        var history = fsm.GetTransitionHistory();
        Assert.Equal(2, history.Count);
        Assert.Equal(GlobalState.Idle, history[0].FromState);
    }

    [Fact(DisplayName = "全局FSM历史: 失败迁移不记录到历史")]
    public void TransitionHistory_FailedNotRecorded()
    {
        var fsm = new GlobalFSM();
        try { fsm.TransitionTo(GlobalState.Traversing); } catch { }
        Assert.Empty(fsm.GetTransitionHistory());
    }

    [Fact(DisplayName = "全局FSM恢复路径: Error→Recovering→Initializing→Traversing")]
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

// ===== StateRestorer Tests =====

public class StateRestorerTests
{
    [Fact(DisplayName = "状态恢复: 保存并恢复全部5个字段匹配(H-6/H-7)")]
    public void PreserveAndRestore_All5FieldsMatch()
    {
        // H-6: Save complete stack contents; H-7: Restore all 5 fields + validate
        var ctx = new TraversalRuntimeContext("test", maxDepth: 10);
        var node = new TestTraversalNode("root-node", "root", NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);
        ctx.GlobalState = GlobalState.Traversing;
        ctx.LastError = new Exception("test error message");

        var restorer = new StateRestorer();
        var stateId = restorer.PreserveState(ctx);

        // Modify context (simulating popup handling disruption)
        ctx.GlobalState = GlobalState.Error;
        ctx.LastError = new Exception("new error");
        ctx.CurrentFrame = null;

        // Restore all 5 fields
        restorer.RestoreState(stateId, ctx);

        // Validate restored state matches preserved
        var validation = restorer.ValidateRestoredState(ctx, stateId);
        Assert.True(validation.IsValid);

        // Verify specific fields
        Assert.Equal("root-node", ctx.CurrentFrame?.NodeId);
        Assert.Equal(GlobalState.Traversing, ctx.GlobalState);
        Assert.Equal(1, ctx.NodeStack.Depth);
    }

    [Fact(DisplayName = "状态恢复: 保存完整栈内容而非仅深度(H-6)")]
    public void PreserveState_SavesCompleteStackNotJustDepth()
    {
        // H-6: PreservedState has NodeStackFrames (List<IStackFrame>), not just NodeStackDepth (int)
        var ctx = new TraversalRuntimeContext("test", maxDepth: 10);
        var node1 = new TestTraversalNode("node-1", "screen1", NodeType.Screen);
        var node2 = new TestTraversalNode("node-2", "screen2", NodeType.Container);
        ctx.NodeStack.Push(node1);
        ctx.NodeStack.Push(node2);

        var restorer = new StateRestorer();
        var stateId = restorer.PreserveState(ctx);

        // Clear stack (popup handling disruption)
        ctx.NodeStack.Clear();
        Assert.Equal(0, ctx.NodeStack.Depth);

        // Restore — should get full stack back
        restorer.RestoreState(stateId, ctx);
        Assert.Equal(2, ctx.NodeStack.Depth);
        Assert.Equal("node-2", ctx.NodeStack.Peek(0)?.NodeId);
    }
}

// ===== PopupHandlerFallback Tests =====

public class PopupHandlerFallbackTests
{
    [Fact(DisplayName = "弹窗处理回退: 顶层异常 → back_fallback结果(H-8)")]
    public void HandlePopup_TopLevelException_ReturnsBackFallback()
    {
        // H-8: Any step exception → back_fallback result
        // Inject a throwing context that throws during preserve step
        var handler = new PopupHandler();
        var ctx = new ThrowingTraversalContext();

        var result = handler.HandlePopup("Allow access", ctx);

        Assert.False(result.Success);
        Assert.Equal("back_fallback", result.Action);
        Assert.Contains("Unhandled exception", result.Description);
    }

    /// <summary>
    /// A mock ITraversalContext that throws InvalidOperationException on NodeStack access,
    /// triggering the top-level catch in HandlePopup.
    /// </summary>
    private class ThrowingTraversalContext : ITraversalContext
    {
        public INodeStack NodeStack => throw new InvalidOperationException("Test: NodeStack throws");
        public IReadOnlyList<string> CurrentPath => [];
        public IReadOnlySet<string> VisitedPages => ImmutableHashSet<string>.Empty;
        public IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren => ImmutableDictionary<string, IReadOnlySet<string>>.Empty;
        public IReadOnlySet<string> VisitedNodes => ImmutableHashSet<string>.Empty;
        public ITraversalNode? CurrentFrame { get; set; } = null;
        public int StepCount => 0;
        public GlobalState GlobalState { get; set; } = GlobalState.Idle;
        public Exception? LastError { get; set; } = null;
    }
}

// ===== Helper: TestTraversalNode =====

internal sealed class TestTraversalNode : ITraversalNode
{
    public string NodeId { get; init; }
    public string Name { get; init; }
    public NodeType NodeType { get; init; }
    public List<string> StaticChildren { get; init; }
    public ChildrenStrategy ChildrenStrategy { get; init; }

    public TestTraversalNode(string nodeId, string name, NodeType nodeType, List<string>? staticChildren = null, ChildrenStrategy? childrenStrategy = null)
    {
        NodeId = nodeId;
        Name = name;
        NodeType = nodeType;
        StaticChildren = staticChildren ?? new List<string>();
        ChildrenStrategy = childrenStrategy ?? new ChildrenStrategy(ChildrenStrategyType.None);
    }
}
