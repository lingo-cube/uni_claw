using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// P4-B2: PauseAsync/ResumeAsync TaskCompletionSource gate 测试。
/// 覆盖 8 个场景：前置校验、gate 阻塞/恢复、多周期、取消、hook、两步终止。
/// </summary>
public class TraversalEnginePauseResumeTests
{
    private static TraversalNode Leaf(string id, Operation op)
        => new(id, id, NodeType.LeafAction, op, new ChildrenStrategy(ChildrenStrategyType.None));

    private static Operation ClickAt(double x, double y)
        => new(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(x, y)));

    private static StateFixture SimpleFixture() => new StateFixtureBuilder()
        .Page("home", p => p.Name("HomeScreen").Button("btn_go", "Go", 0.5, 0.5))
        .Page("next", p => p.Name("NextScreen").BackButton("btn_back", 0.05, 0.05))
        .Transition(t => t.Id("go").Click("btn_go").From("home").To("next"))
        .Transition(t => t.Id("back").Click("btn_back").From("next").To("home"))
        .Build();

    private static TraversalEngine CreateEngine(
        StateFixture fixture, TraversalNode root,
        Dictionary<string, TraversalNode> nodes,
        TraversalEngineConfig? config = null)
    {
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var plan = new TraversalPlan(
            EntryApp: "test", EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan", PlanId: "test-001", RootNode: root, StaticNodes: nodes);
        return new TraversalEngine(plan, vision, action, config);
    }

    private static TraversalEngine CreateSimpleEngine(TraversalEngineConfig? config = null)
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        return CreateEngine(fixture, root, new Dictionary<string, TraversalNode>(), config);
    }

    // ── 6.1/6.2: PauseAsync blocks loop, ResumeAsync restores ─────

    [Fact(DisplayName = "P4-B2: PauseAsync 阻塞步骤循环, ResumeAsync 恢复")]
    public async Task PauseResume_Lifecycle_BlocksAndRestoresLoop()
    {
        var cts = new CancellationTokenSource();
        var engine = CreateSimpleEngine(new TraversalEngineConfig
        {
            MaxSteps = 1000,
            DelayPerStepMs = 10
        });

        // 在 RunAsync 开始前暂停 — 第一轮迭代的暂停检查即阻塞
        await engine.PauseAsync();
        Assert.Equal(GlobalState.Paused, engine.CurrentState);

        var runTask = Task.Run(() => engine.RunAsync(cts.Token));

        // 等待片刻 — 引擎应保持 Paused（循环阻塞在 gate 上）
        await Task.Delay(300);
        Assert.Equal(GlobalState.Paused, engine.CurrentState);

        // 恢复
        await engine.ResumeAsync();
        Assert.Equal(GlobalState.Traversing, engine.CurrentState);

        // 等待引擎完成
        cts.CancelAfter(20000);
        var result = await runTask;
        Assert.True(result.Success, $"Engine should complete after resume, got: {result.CompletionReason}");
    }

    // ── 6.3: PauseAsync precondition failure ──────────────────────

    [Fact(DisplayName = "P4-B2: PauseAsync 前置校验 — 非 Traversing 时抛出")]
    public async Task PauseAsync_WrongState_Throws()
    {
        var engine = CreateSimpleEngine();

        // 暂停一次 → 状态变为 Paused
        await engine.PauseAsync();
        Assert.Equal(GlobalState.Paused, engine.CurrentState);

        // 再次暂停（状态≠Traversing）→ 应抛出
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            engine.PauseAsync());
        Assert.Contains("Cannot pause when not Traversing", ex.Message);
    }

    // ── 6.4: ResumeAsync precondition failure ─────────────────────

    [Fact(DisplayName = "P4-B2: ResumeAsync 前置校验 — 非 Paused 时抛出")]
    public async Task ResumeAsync_WrongState_Throws()
    {
        var engine = CreateSimpleEngine();

        // 未暂停直接恢复 → 状态≠Paused → 应抛出
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            engine.ResumeAsync());
        Assert.Contains("Cannot resume when not Paused", ex.Message);

        // 暂停后恢复，再恢复 → 第二次应抛出
        await engine.PauseAsync();
        await engine.ResumeAsync();
        ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            engine.ResumeAsync());
        Assert.Contains("Cannot resume when not Paused", ex.Message);
    }

    // ── 6.5: Multiple pause/resume cycles ─────────────────────────

    [Fact(DisplayName = "P4-B2: 多次暂停/恢复周期正确工作")]
    public async Task MultiplePauseResume_CyclesCorrectly()
    {
        var cts = new CancellationTokenSource();
        var engine = CreateSimpleEngine(new TraversalEngineConfig
        {
            MaxSteps = 1000,
            DelayPerStepMs = 10
        });

        // 周期 1: Pause → Resume
        await engine.PauseAsync();
        Assert.Equal(GlobalState.Paused, engine.CurrentState);
        await engine.ResumeAsync();
        Assert.Equal(GlobalState.Traversing, engine.CurrentState);

        // 周期 2: Pause → Resume
        await engine.PauseAsync();
        Assert.Equal(GlobalState.Paused, engine.CurrentState);
        await engine.ResumeAsync();
        Assert.Equal(GlobalState.Traversing, engine.CurrentState);

        // 启动循环 — 引擎应正常完成
        var runTask = Task.Run(() => engine.RunAsync(cts.Token));
        cts.CancelAfter(20000);
        var result = await runTask;
        Assert.True(result.Success);
    }

    // ── 6.6: Cancel during pause → loop exits ─────────────────────

    [Fact(DisplayName = "P4-B2: 暂停时取消令牌 → 循环退出")]
    public async Task CancelDuringPause_ExitsLoop()
    {
        var cts = new CancellationTokenSource();
        var engine = CreateSimpleEngine(new TraversalEngineConfig
        {
            MaxSteps = 1000,
            DelayPerStepMs = 10
        });

        // 暂停（此时 RunAsync 尚未启动）
        await engine.PauseAsync();
        Assert.Equal(GlobalState.Paused, engine.CurrentState);

        // 启动循环（应在 gate 上阻塞）
        var runTask = Task.Run(() => engine.RunAsync(cts.Token));

        // 取消令牌 — gate 因 ct.Register 回调而打开，循环经 ct.ThrowIfCancellationRequested() 退出
        cts.Cancel();

        var result = await runTask;
        Assert.Equal(TraversalResult.Reasons.Cancelled, result.CompletionReason);
    }

    // ── 6.7: Hooks fire at correct sequence ───────────────────────

    [Fact(DisplayName = "P4-B2: OnPauseAsync/OnResumeAsync hooks 正确触发")]
    public async Task PauseResume_HooksFireCorrectly()
    {
        var engine = CreateSimpleEngine();
        var hook = new CaptureHook();
        engine.RegisterHook(hook);

        // PauseAsync → OnPauseAsync 应触发
        await engine.PauseAsync();
        Assert.Equal(1, hook.PauseCallCount);
        Assert.Equal(GlobalState.Paused, engine.CurrentState);

        // ResumeAsync → OnResumeAsync 应触发（在 gate 打开前）
        await engine.ResumeAsync();
        Assert.Equal(1, hook.ResumeCallCount);
        Assert.Equal(GlobalState.Traversing, engine.CurrentState);
    }

    // ── 6.8: Pause → Terminate two-step ───────────────────────────

    [Fact(DisplayName = "P4-B2: 暂停后终止 (Paused→Terminated) 两步终止工作")]
    public async Task PauseThenStop_TerminatesCorrectly()
    {
        var engine = CreateSimpleEngine();

        // 暂停
        await engine.PauseAsync();
        Assert.Equal(GlobalState.Paused, engine.CurrentState);

        // 从 Paused 直接终止（矩阵允许 Paused→Terminated）
        await engine.StopAsync();
        Assert.Equal(GlobalState.Terminated, engine.CurrentState);
    }

    /// <summary>
    /// CaptureHook — 记录 OnPauseAsync/OnResumeAsync 调用次数，用于验证 hook 触发。
    /// </summary>
    private sealed class CaptureHook : TraversalHookBase
    {
        public int PauseCallCount { get; private set; }
        public int ResumeCallCount { get; private set; }

        public override Task OnPauseAsync(ITraversalContext context)
        {
            PauseCallCount++;
            return Task.CompletedTask;
        }

        public override Task OnResumeAsync(ITraversalContext context)
        {
            ResumeCallCount++;
            return Task.CompletedTask;
        }
    }
}
