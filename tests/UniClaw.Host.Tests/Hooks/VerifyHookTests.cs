using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Host.Hooks;
using Xunit;

namespace UniClaw.Host.Tests.Hooks;

/// <summary>
/// E6.3/E6.5 — plan-mode expected-change verification via <see cref="VerifyHook"/>
/// (spec: "Immediate verification is a non-mutating hook"). Covers the pass/fail
/// matrix and the intent-mode no-op.
/// </summary>
public class VerifyHookTests
{
    private const string RunId = "run-verify-001";

    [Fact(DisplayName = "VerifyHook: expected_change 包含匹配命中 → verify.pass")]
    public async Task ExpectedContains_PageMatches_RecordsPass()
    {
        var (service, recorder) = TraceFixture();
        var hook = new VerifyHook(recorder, RunId);
        var ctx = BuildContext(expectedChange: "Settings", page: "Settings");

        await hook.OnBeforeStepAsync(ctx);
        PopFrame(ctx); // leaf executed → engine pops the leaf and re-syncs to parent
        await hook.OnAfterStepAsync(ctx);

        var execution = service.GetExecutions().Single(e => e.Action == "verify.pass");
        Assert.Equal("pass", execution.Status);
        Assert.Equal(SpanType.StateDecision, execution.SpanType);
        Assert.Equal("Settings", execution.PageId);
        Assert.Equal("Settings", execution.TargetValue);
        Assert.Equal(1, execution.Context?.StepNumber);
    }

    [Fact(DisplayName = "VerifyHook: expected_change 要求页面跳转但未跳转 → verify.fail")]
    public async Task ExpectedPageChange_NoTransition_RecordsFail()
    {
        var (service, recorder) = TraceFixture();
        var hook = new VerifyHook(recorder, RunId);
        var ctx = BuildContext(expectedChange: "change", page: "Settings");

        await hook.OnBeforeStepAsync(ctx);
        PopFrame(ctx);
        await hook.OnAfterStepAsync(ctx);

        var execution = service.GetExecutions().Single(e => e.Action == "verify.fail");
        Assert.Equal("fail", execution.Status);
        Assert.Equal("Settings", execution.PageId);
    }

    [Fact(DisplayName = "VerifyHook: expected_change=change 且页面跳转 → verify.pass")]
    public async Task ExpectedPageChange_Transition_RecordsPass()
    {
        var (service, recorder) = TraceFixture();
        var hook = new VerifyHook(recorder, RunId);
        var ctx = BuildContext(expectedChange: "change", page: "Settings");

        await hook.OnBeforeStepAsync(ctx);
        ctx.SetCurrentPageAnalysis(Page("About phone"));
        PopFrame(ctx);
        await hook.OnAfterStepAsync(ctx);

        var execution = service.GetExecutions().Single(e => e.Action == "verify.pass");
        Assert.Equal("pass", execution.Status);
        Assert.Equal("About phone", execution.PageId);
    }

    [Fact(DisplayName = "VerifyHook: 节点无 expected_change 元数据 → 不记录 (intent 模式 no-op)")]
    public async Task NoExpectedChange_RecordsNothing()
    {
        var (service, recorder) = TraceFixture();
        var hook = new VerifyHook(recorder, RunId);
        var ctx = BuildContext(expectedChange: null, page: "Settings");

        await hook.OnBeforeStepAsync(ctx);
        PopFrame(ctx);
        await hook.OnAfterStepAsync(ctx);

        Assert.DoesNotContain(service.GetExecutions(), e => e.Action?.StartsWith("verify.", StringComparison.Ordinal) == true);
    }

    [Fact(DisplayName = "VerifyHook: 叶子仍是当前帧 (未执行) 的步骤 → 不记录")]
    public async Task FrameNotPopped_SkipsNonExecuteSteps()
    {
        var (service, recorder) = TraceFixture();
        var hook = new VerifyHook(recorder, RunId);
        var ctx = BuildContext(expectedChange: "change", page: "Settings");

        // No PopFrame: the leaf is still the live frame (NodeSelect/PreconditionCheck
        // step before execution), so the hook must not record a duplicate verify.
        await hook.OnBeforeStepAsync(ctx);
        await hook.OnAfterStepAsync(ctx);

        Assert.DoesNotContain(service.GetExecutions(), e => e.Action?.StartsWith("verify.", StringComparison.Ordinal) == true);
    }

    private static TraversalRuntimeContext BuildContext(string? expectedChange, string page)
    {
        var ctx = new TraversalRuntimeContext(RunId);
        var leaf = new TraversalNode(
            "step-about",
            "About phone",
            NodeType.LeafAction,
            new Operation(
                OperationType.Click,
                new Target(TargetType.Coordinate, new Coordinate(0.5, 0.7))),
            new ChildrenStrategy(ChildrenStrategyType.None),
            Meta: expectedChange is null
                ? null
                : new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["expected_change"] = expectedChange,
                });
        // Mirror the engine: root on the stack, leaf pushed on top as the live frame.
        ctx.NodeStack.Push(new TraversalNode(
            "root", "settings-root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None)));
        ctx.NodeStack.Push(leaf);
        ctx.SetCurrentFrame(leaf);
        ctx.SetCurrentPageAnalysis(Page(page));
        ctx.IncrementStepCount();
        return ctx;
    }

    /// <summary>
    /// Simulate the engine's post-execute leaf-pop: pop the leaf frame and
    /// re-sync CurrentFrame to the parent, exactly as <c>TraversalEngine.RunAsync</c>
    /// does before firing <c>OnAfterStep</c>.
    /// </summary>
    private static void PopFrame(TraversalRuntimeContext ctx)
    {
        ctx.NodeStack.Pop();
        ctx.SetCurrentFrame(ctx.NodeStack.Peek()?.Node);
    }

    private static PageAnalysis Page(string page) =>
        new(Direction.Left, Direction.Left, CurrentPath: [page]);

    private static (InMemoryTraceService Service, ITraceRecorder Recorder) TraceFixture()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceService(storage), new InMemoryTraceRecorder(storage));
    }
}
