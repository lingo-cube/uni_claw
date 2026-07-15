using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// Step(StepContext) tests (3 scenarios).
/// Tests the StepContext overload integration: real logic vs stub fallback vs exception routing.
/// </summary>
public class StepContextTests
{
    [Fact(DisplayName = "步进上下文: 提供StepContext → 使用真实Handler逻辑")]
    public async Task Step_WithStepContext()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor { NextResult = true };
        var coord = new Coordinate(0.5, 0.5);
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, coord));
        var node = new TraversalNode("n1", "n1", NodeType.LeafAction, operation,
            new ChildrenStrategy(ChildrenStrategyType.None));

        var fsm = new TraversalFSM(ctx);
        ctx.NodeStack.Push(node);
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        var stepCtx = new StepContext(ctx, fsm, new MockVisionProvider(), mockAction,
            null!, null!, null!, null!, null!);

        var result = await fsm.StepAsync(stepCtx);

        // Handlers use real logic when StepContext is provided
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(mockAction.CallLog);
    }

    [Fact(DisplayName = "步进上下文: 无StepContext → stub回退返回ResultVerify")]
    public async Task Step_NullStepContext()
    {
        var ctx = new TraversalRuntimeContext("test");
        var coord = new Coordinate(0.5, 0.5);
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, coord));
        var node = new TraversalNode("n1", "n1", NodeType.LeafAction, operation,
            new ChildrenStrategy(ChildrenStrategyType.None));

        var fsm = new TraversalFSM(ctx);
        ctx.NodeStack.Push(node);
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        // Step() without StepContext → stub fallback
        var result = await fsm.StepAsync();

        // Stub: returns ResultVerify, no action executed
        Assert.Equal(TraversalState.ResultVerify, result);
    }

    [Fact(DisplayName = "步进上下文: 异常路由到ErrorHandling+记录LastError")]
    public async Task Step_ExceptionRouting()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mockAction = new MockActionExecutor
        {
            NextResult = true,
            ThrowsOnNext = new TimeoutException("ADB timeout")
        };
        var coord = new Coordinate(0.5, 0.5);
        var operation = new Operation(OperationType.Click, new Target(TargetType.Coordinate, coord));
        var node = new TraversalNode("n1", "n1", NodeType.LeafAction, operation,
            new ChildrenStrategy(ChildrenStrategyType.None));

        var fsm = new TraversalFSM(ctx);
        ctx.NodeStack.Push(node);
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        var stepCtx = new StepContext(ctx, fsm, new MockVisionProvider(), mockAction,
            null!, null!, null!, null!, null!);

        var result = await fsm.StepAsync(stepCtx);

        // Exception routes to ErrorHandling, same behavior for Step() and Step(ctx)
        Assert.Equal(TraversalState.ErrorHandling, result);
        Assert.NotNull(ctx.LastError);
        Assert.IsType<TimeoutException>(ctx.LastError);
    }
}
