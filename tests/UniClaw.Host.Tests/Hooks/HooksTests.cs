using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Host.Hooks;
using UniClaw.Host.Safety;
using Xunit;

namespace UniClaw.Host.Tests.Hooks;

public class SafetyContextHookTests
{
    private const string RunId = "run-hook-001";

    [Fact(DisplayName = "SafetyContextHook: OnBeforeStep 推送真实 candidate, OnAfterStep 恢复")]
    public async Task BeforeStep_PushesCandidate_AfterStep_Restores()
    {
        var ctx = BuildContext(stepNumber: 1);
        var safetyContext = new SafetyExecutionContext();
        var hook = new SafetyContextHook(
            safetyContext,
            RunId,
            "com.android.settings",
            "Settings",
            maxSteps: 100,
            maxScrolls: 10);

        await hook.OnBeforeStepAsync(ctx);

        var candidate = safetyContext.Current;
        Assert.NotNull(candidate);
        Assert.Equal("click", candidate.Action);
        Assert.Equal(1, candidate.StepNumber);
        Assert.Equal(RunId, candidate.RunId);
        Assert.Equal("com.android.settings", candidate.PackageName);
        Assert.Equal("engine_hook", candidate.Source);
        Assert.Equal("Settings", candidate.PageIdentity);

        await hook.OnAfterStepAsync(ctx);
        Assert.Null(safetyContext.Current);
    }

    [Fact(DisplayName = "SafetyContextHook: 连续步骤覆盖旧 scope, 无堆叠泄漏")]
    public async Task ConsecutiveSteps_ReplaceScope_NoLeak()
    {
        var safetyContext = new SafetyExecutionContext();
        var hook = new SafetyContextHook(
            safetyContext,
            RunId,
            "com.android.settings",
            "Settings",
            maxSteps: 100,
            maxScrolls: 10);

        var step1 = BuildContext(stepNumber: 1);
        var step2 = BuildContext(stepNumber: 2);

        await hook.OnBeforeStepAsync(step1);
        await hook.OnBeforeStepAsync(step2);

        Assert.Equal(2, safetyContext.Current!.StepNumber);

        await hook.OnAfterStepAsync(step2);
        Assert.Null(safetyContext.Current);
    }

    [Fact(DisplayName = "SafetyContextHook: 最后一个预算步骤仍可执行")]
    public async Task LastBudgetedStep_HasOneCurrentStepRemaining()
    {
        var safetyContext = new SafetyExecutionContext();
        var hook = new SafetyContextHook(
            safetyContext,
            RunId,
            "com.android.settings",
            "Settings",
            maxSteps: 12,
            maxScrolls: 6);
        var context = BuildContext(stepNumber: 12);

        await hook.OnBeforeStepAsync(context);

        Assert.Equal(1, safetyContext.Current!.RemainingSteps);
        await hook.OnAfterStepAsync(context);
    }

    [Fact(DisplayName = "SafetyContextHook: candidate 穿过 engine 异步 hook 边界后仍可见")]
    public async Task BeforeStep_AcrossAsyncHookBoundary_RemainsVisibleToCaller()
    {
        var ctx = BuildTextNavigationContext(stepNumber: 3);
        var safetyContext = new SafetyExecutionContext();
        var hook = new SafetyContextHook(
            safetyContext,
            RunId,
            "com.android.settings",
            "Settings",
            maxSteps: 100,
            maxScrolls: 10);

        await InvokeThroughEngineLikeBoundaryAsync(hook, ctx);

        Assert.NotNull(safetyContext.Current);
        Assert.Equal("engine_hook", safetyContext.Current.Source);
        Assert.Equal(3, safetyContext.Current.StepNumber);
        Assert.Equal("Network & internet", safetyContext.Current.Target);
        Assert.Equal("navigation_row", safetyContext.Current.Semantic);
        Assert.Equal("Settings", safetyContext.Current.PageIdentity);
        Assert.True(safetyContext.Current.CoordinatesTrusted);

        await hook.OnAfterStepAsync(ctx);
        Assert.Null(safetyContext.Current);
    }

    private static async Task InvokeThroughEngineLikeBoundaryAsync(
        ITraversalHook hook,
        ITraversalContext context)
    {
        await Task.Yield();
        await hook.OnBeforeStepAsync(context);
    }

    private static TraversalRuntimeContext BuildContext(int stepNumber)
    {
        var ctx = new TraversalRuntimeContext(RunId);
        ctx.AppendPath("Settings");
        ctx.SetCurrentFrame(new TraversalNode(
            "node-1",
            "Wifi",
            NodeType.LeafAction,
            new Operation(
                OperationType.Click,
                new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5))),
            new ChildrenStrategy(ChildrenStrategyType.None)));
        for (var i = 0; i < stepNumber; i++)
            ctx.IncrementStepCount();
        return ctx;
    }

    private static TraversalRuntimeContext BuildTextNavigationContext(int stepNumber)
    {
        var ctx = new TraversalRuntimeContext(RunId);
        ctx.SetCurrentFrame(new TraversalNode(
            "node-text-navigation",
            "menu_container",
            NodeType.Container,
            new Operation(
                OperationType.Click,
                new Target(TargetType.Text, "Network & internet")),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch)));
        for (var i = 0; i < stepNumber; i++)
            ctx.IncrementStepCount();
        return ctx;
    }
}

public class BoundaryHookTests
{
    private const string RunId = "run-boundary-001";

    [Fact(DisplayName = "BoundaryHook: 边界内页面不记录违规")]
    public async Task WithinBoundary_NoViolationRecorded()
    {
        var (service, recorder) = TraceFixture();
        var hook = new BoundaryHook(
            () => Task.FromResult("com.android.settings"),
            "com.android.settings",
            new[] { "Settings" },
            recorder,
            RunId);

        var ctx = new TraversalRuntimeContext(RunId);
        ctx.AppendPath("Settings");

        await hook.OnAfterStepAsync(ctx);
        Assert.Empty(service.GetExecutions());
    }

    [Fact(DisplayName = "BoundaryHook: 包名越界记录 package_boundary 违规")]
    public async Task PackageViolation_Recorded()
    {
        var (service, recorder) = TraceFixture();
        var hook = new BoundaryHook(
            () => Task.FromResult("com.other.app"),
            "com.android.settings",
            new[] { "Settings" },
            recorder,
            RunId);

        var ctx = new TraversalRuntimeContext(RunId);
        ctx.AppendPath("Settings");

        await hook.OnAfterStepAsync(ctx);

        var execution = service.GetExecutions().Single(
            e => e.Action == "boundary.package_boundary");
        Assert.Equal("violation", execution.Status);
        Assert.Equal(SpanType.ErrorHandling, execution.SpanType);
        Assert.Equal("Settings", execution.PageId);
    }

    [Fact(DisplayName = "BoundaryHook: 页面前缀越界记录 page_boundary 违规")]
    public async Task PageBoundaryViolation_Recorded()
    {
        var (service, recorder) = TraceFixture();
        var hook = new BoundaryHook(
            () => Task.FromResult("com.android.settings"),
            "com.android.settings",
            new[] { "Settings" },
            recorder,
            RunId);

        var ctx = new TraversalRuntimeContext(RunId);
        ctx.AppendPath("Settings");
        ctx.AppendPath("Browser");

        await hook.OnAfterStepAsync(ctx);

        var execution = service.GetExecutions().Single(
            e => e.Action == "boundary.page_boundary");
        Assert.Equal("violation", execution.Status);
        Assert.Equal(SpanType.ErrorHandling, execution.SpanType);
    }

    [Fact(DisplayName = "BoundaryHook: enumerate 允许 Settings 包内首层子页")]
    public async Task EnumerateFirstLevelChildPage_NoViolationRecorded()
    {
        var (service, recorder) = TraceFixture();
        var hook = new BoundaryHook(
            () => Task.FromResult("com.android.settings"),
            "com.android.settings",
            new[] { "Settings" },
            recorder,
            RunId,
            allowFirstLevelChildPages: true);
        var ctx = new TraversalRuntimeContext(RunId);
        ctx.NodeStack.Push(new TraversalNode(
            "root",
            "Settings",
            NodeType.Screen,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None)));
        ctx.NodeStack.Push(new TraversalNode(
            "child",
            "Network & internet",
            NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None)));
        ctx.SetCurrentPageAnalysis(new PageAnalysis(
            Direction.Left,
            Direction.Left,
            CurrentPath: ["Network & internet"]));

        await hook.OnAfterStepAsync(ctx);

        Assert.Empty(service.GetExecutions());
    }

    private static (InMemoryTraceService Service, ITraceRecorder Recorder) TraceFixture()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceService(storage), new InMemoryTraceRecorder(storage));
    }
}
