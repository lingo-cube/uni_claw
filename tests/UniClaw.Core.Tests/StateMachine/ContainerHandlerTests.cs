using UniClaw.Core.Domain;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// ContainerHandler wrapper tests (3 scenarios):
/// (1) normal pipeline execution
/// (2) pipeline-level fallback with injected throwing Func (pipeline catch)
/// (3) constructor injection with custom executor hooks
/// Plus executor-level fallback comparison (D-G4).
/// </summary>
public class ContainerHandlerTests
{
    private static CompletionContext DefaultCompletionCtx() => new(
        ElapsedMs: 100, TimeoutMs: 5000, CurrentDepth: 2, MaxDepth: 5,
        TotalChildren: 3, VisitedChildCount: 3,
        ExitConditionFallback: FallbackAction.Back);

    private static MockTraversalContext MockTraversal() => new();

    /// <summary>
    /// (1) Normal pipeline execution — detect→decide→execute produces expected result.
    /// </summary>
    [Fact(DisplayName = "容器编排: 正常3步流水线 → 结果从executor返回")]
    public void HandleContainer_NormalPipeline_ReturnsExecutorResult()
    {
        var ctx = DefaultCompletionCtx();

        var handler = new ContainerHandler();
        var result = handler.HandleContainer(ctx, canContinue: true, "node-1", MockTraversal());

        Assert.Equal(FallbackAction.Back, result.Action);
        Assert.True(result.Success);
        Assert.Equal("Press back + pop frame", result.Description);
    }

    /// <summary>
    /// (2) Pipeline-level fallback — injected throwing Func causes pipeline catch.
    /// Returns ContainerActionResult(Back, false, "Unhandled exception...").
    /// D-G4: Pipeline fallback Success=false (pipeline crashed).
    /// </summary>
    [Fact(DisplayName = "容器编排: detect步骤抛异常 → 管道兜底 Back+Success=false")]
    public void HandleContainer_ThrowingStep_PipelineFallback()
    {
        var ctx = DefaultCompletionCtx();

        // Inject throwing detect Func → pipeline catch
        var handler = new ContainerHandler(
            detect: _ => throw new InvalidOperationException("Intentional test failure from detector"),
            decide: new FallbackDecider().DecideFallback,
            execute: new ContainerActionExecutor().Execute);

        var result = handler.HandleContainer(ctx, canContinue: true, "node-1", MockTraversal());

        Assert.Equal(FallbackAction.Back, result.Action);
        Assert.False(result.Success);
        Assert.Contains("Unhandled exception during container handling", result.Description);
        Assert.Contains("InvalidOperationException", result.Description);
    }

    /// <summary>
    /// (3) Constructor injection — custom executor hooks produce custom results.
    /// </summary>
    [Fact(DisplayName = "容器编排: 自定义executor hooks → 使用注入hooks")]
    public void HandleContainer_CustomExecutorHooks_UsedInsteadOfDefaults()
    {
        var ctx = DefaultCompletionCtx();

        var customExecutor = new ContainerActionExecutor(
            backHook: _ => new ContainerActionResult(FallbackAction.AutoEscape, true, "custom-back-result"));

        var handler = new ContainerHandler(executor: customExecutor);
        var result = handler.HandleContainer(ctx, canContinue: true, "node-custom", MockTraversal());

        Assert.Equal(FallbackAction.AutoEscape, result.Action);
        Assert.True(result.Success);
        Assert.Equal("custom-back-result", result.Description);
    }

    /// <summary>
    /// Executor-level fallback vs pipeline-level fallback (D-G4).
    /// Executor with throwing Back hook → executor catch returns DefaultBack (Success=true).
    /// </summary>
    [Fact(DisplayName = "容器编排: executor内部兜底 Back+Success=true (区别于管道兜底)")]
    public void HandleContainer_ExecutorInternalFallback_BackSuccessTrue()
    {
        var ctx = DefaultCompletionCtx();

        var throwingExecutor = new ContainerActionExecutor(
            backHook: _ => throw new InvalidOperationException("executor-internal-throw"));

        var handler = new ContainerHandler(executor: throwingExecutor);
        var result = handler.HandleContainer(ctx, canContinue: true, "node-1", MockTraversal());

        Assert.Equal(FallbackAction.Back, result.Action);
        Assert.True(result.Success); // executor fallback Success=true (D-G4)
        Assert.Equal("Press back + pop frame", result.Description);
    }

    // --- Mock helpers implementing the full ITraversalContext interface ---
    private sealed class MockTraversalContext : ITraversalContext
    {
        public INodeStack NodeStack => new MockNodeStack();
        public IReadOnlyList<string> CurrentPath => new List<string>();
        public IReadOnlySet<string> VisitedPages => new HashSet<string>();
        public IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren => new Dictionary<string, IReadOnlySet<string>>();
        public IReadOnlySet<string> VisitedNodes => new HashSet<string>();
        public ITraversalNode? CurrentFrame { get; set; } = null;
        public int StepCount => 0;
        public GlobalState GlobalState { get; set; } = GlobalState.Traversing;
        public Exception? LastError { get; set; } = null;
    }

    private sealed class MockNodeStack : INodeStack
    {
        public int Depth => 0;
        public int MaxDepth => 10;
        public bool IsEmpty => true;
        public bool Push(ITraversalNode node, List<string>? children = null) => true;
        public IStackFrame? Pop() => null;
        public IStackFrame? Peek(int offset = 0) => null;
        public void Clear() { }
    }
}
