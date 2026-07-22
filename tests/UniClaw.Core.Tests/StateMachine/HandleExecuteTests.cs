using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// HandleExecute handler tests (8 scenarios).
/// Exercises the real HandleExecute logic via Step(StepContext).
/// </summary>
public class HandleExecuteTests
{
    /// <summary>
    /// Helper: creates a TraversalNode with the given Operation.
    /// </summary>
    private static TraversalNode CreateNode(string id, Operation operation,
        NodeType nodeType = NodeType.LeafAction)
        => new(id, id, nodeType, operation,
            new ChildrenStrategy(ChildrenStrategyType.None));

    /// <summary>
    /// Helper: drives FSM to Execute state with a node on the stack.
    /// </summary>
    private static TraversalFSM DriveToExecute(TraversalRuntimeContext ctx, TraversalNode node)
    {
        var fsm = new TraversalFSM(ctx);
        ctx.NodeStack.Push(node);
        fsm.TransitionTo(TraversalState.PreconditionCheck);  // NodeSelect → PreconditionCheck
        fsm.TransitionTo(TraversalState.Execute);             // PreconditionCheck → Execute
        return fsm;
    }

    /// <summary>
    /// Creates a StepContext with the given MockActionExecutor.
    /// </summary>
    private static StepContext CreateStepContext(MockActionExecutor action,
        TraversalRuntimeContext ctx, TraversalFSM fsm)
        => new(ctx, fsm, new UniBrainService(new MockVisionProvider(), new MockTraversalAdvisor(), new MockTextUnderstanding()), new DefaultScreenStateProvider(), action,
            null!, null!, null!, null!, null!);

    [Fact(DisplayName = "执行处理: Click操作成功 → ResultVerify+记录tap动作")]
    public async Task Execute_Click_Success()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor { NextResult = true };
        var coord = new Coordinate(0.5, 0.5);
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, coord));
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        var stepCtx = CreateStepContext(mockAction, ctx, fsm);
        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(mockAction.CallLog);
        Assert.Equal("tap", mockAction.CallLog[0].Action);
        Assert.Equal(0.5, mockAction.CallLog[0].Parameters["x"]);
        Assert.Equal(0.5, mockAction.CallLog[0].Parameters["y"]);
    }

    [Fact(DisplayName = "执行处理: Back操作成功 → ResultVerify+记录back动作")]
    public async Task Execute_Back_Success()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor { NextResult = true };
        var operation = new Operation(OperationType.Back);
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        var stepCtx = CreateStepContext(mockAction, ctx, fsm);
        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(mockAction.CallLog);
        Assert.Equal("back", mockAction.CallLog[0].Action);
    }

    [Fact(DisplayName = "执行处理: NoAction操作 → 跳过执行器直接ResultVerify")]
    public async Task Execute_NoAction()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor { NextResult = true };
        var operation = new Operation(OperationType.NoAction);
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        var stepCtx = CreateStepContext(mockAction, ctx, fsm);
        var result = await fsm.StepAsync(stepCtx);

        // NoAction → skip executor, return ResultVerify
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Empty(mockAction.CallLog);
    }

    [Fact(DisplayName = "执行处理: Click+Restore成功 → 2次调用(tap+back)")]
    public async Task Execute_WithRestore_Success()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor { NextResult = true };
        var clickOp = new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.3, 0.3)));
        var restoreAction = new RestoreAction(OperationType.Back);
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5)),
            Restore: restoreAction);
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        var stepCtx = CreateStepContext(mockAction, ctx, fsm);
        var result = await fsm.StepAsync(stepCtx);

        // Primary operation + restore = 2 calls
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal(2, mockAction.CallLog.Count);
        Assert.Equal("tap", mockAction.CallLog[0].Action);   // primary
        Assert.Equal("back", mockAction.CallLog[1].Action);   // restore
    }

    [Fact(DisplayName = "执行处理: Restore失败非关键 → 仍然ResultVerify")]
    public async Task Execute_WithRestore_Failure()
    {
        var ctx = new TraversalRuntimeContext("test");
        // Both primary and restore return false — restore failure is non-critical
        var mockAction = new MockActionExecutor { NextResult = false };
        var restoreAction = new RestoreAction(OperationType.Back);
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5)),
            Restore: restoreAction);
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        var stepCtx = CreateStepContext(mockAction, ctx, fsm);
        var result = await fsm.StepAsync(stepCtx);

        // Restore failure (false return) is non-critical → still ResultVerify, NOT ErrorHandling
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal(2, mockAction.CallLog.Count); // primary + restore both called
    }

    [Fact(DisplayName = "执行处理: 执行器返回false → 仍然ResultVerify(非关键)")]
    public async Task Execute_ActionReturnsFalse()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor { NextResult = false };
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5)));
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        var stepCtx = CreateStepContext(mockAction, ctx, fsm);
        var result = await fsm.StepAsync(stepCtx);

        // IActionExecutor returns false → still ResultVerify (non-critical, matches Python)
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(mockAction.CallLog);
        Assert.False(mockAction.CallLog[0].Success);
    }

    [Fact(DisplayName = "执行处理: 执行器抛异常 → ErrorHandling+记录LastError")]
    public async Task Execute_Exception()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor
        {
            NextResult = true,
            ThrowsOnNext = new TimeoutException("ADB timeout")
        };
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5)));
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        var stepCtx = CreateStepContext(mockAction, ctx, fsm);
        var result = await fsm.StepAsync(stepCtx);

        // Exception → ErrorHandling
        Assert.Equal(TraversalState.ErrorHandling, result);
        Assert.NotNull(ctx.LastError);
        Assert.IsType<TimeoutException>(ctx.LastError);
    }

    [Fact(DisplayName = "执行处理: 无StepContext → stub回退直接ResultVerify")]
    public async Task Execute_NullStepContext()
    {
        var ctx = new TraversalRuntimeContext("test");
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5)));
        var node = CreateNode("n1", operation);
        var fsm = DriveToExecute(ctx, node);

        // Step() without StepContext → stub fallback
        var result = await fsm.StepAsync();

        Assert.Equal(TraversalState.ResultVerify, result);
    }
}
