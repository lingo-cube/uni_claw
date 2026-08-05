using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Coordinate = UniClaw.Core.Domain.Models.Content.Coordinate;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// 滚动循环终止单测 (滚动 = 操作 + 判断 模型)。
/// 直接测试 <see cref="InterceptionHandler.TryHandleScroll"/> 与
/// <see cref="TraversalRuntimeContext"/> 的 per-frame seen 元素集合 API。
/// </summary>
public class ScrollLoopTerminationTests
{
    // ── TryHandleScroll 契约测试 ──────────────────────────────────

    [Fact(DisplayName = "TryHandleScroll: 滚出未见元素 → Continue (nextState=NodeSelect)")]
    public async Task TryHandleScroll_UnseenElements_Continues()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        ctx.Context.SetCurrentPageAnalysis(Page("a"));        // 滚动前可见页 (page 0)
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;
        vision.EnqueueAnalysis(Page("a", "b"));               // 滚动后揭示新元素 b

        var (result, frameCompleted, childPushed, nextState) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);

        Assert.True(result);                                   // 滚出未见元素 → 继续
        Assert.Equal(1, action.SwipeCount);                    // 执行了一次 swipe (操作)
        Assert.Equal(TraversalState.NodeSelect, nextState);
        Assert.False(frameCompleted);
        Assert.False(childPushed);
        Assert.Equal(1, childMgr.InvalidateCount);             // 子节点缓存已失效
    }

    [Fact(DisplayName = "TryHandleScroll: 全是已见元素 → 第一次空差分重试, 第二次到底")]
    public async Task TryHandleScroll_AllSeen_Stops()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        ctx.Context.SetCurrentPageAnalysis(Page("a", "b"));    // 滚动前 page 0
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;
        vision.EnqueueAnalysis(Page("a", "b"));                // 滚动后无新元素 (第 1 次空差分)
        vision.EnqueueAnalysis(Page("a", "b"));                // 第 2 次空差分

        // 第 1 次空差分 → 重试 (MaxEmptyScrollRetries=1, 允许 1 次重试)
        var (result, frameCompleted, childPushed, nextState) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);
        Assert.True(result);                                   // 空帧重试, 不消耗 budget
        Assert.Equal(1, action.SwipeCount);

        // 第 2 次空差分 → 真正到底
        (result, frameCompleted, childPushed, nextState) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);
        Assert.False(result);                                  // 到底 → 由调用方完成帧
        Assert.Equal(2, action.SwipeCount);
        Assert.False(frameCompleted);
        Assert.Equal(2, childMgr.InvalidateCount);
        // seen 集合已在到底时清理
        Assert.True(ctx.Context.RecordSeenElementIds("list", new[] { "a" }));
    }

    [Fact(DisplayName = "TryHandleScroll: 不可滚动 → 不 swipe, 直接完成")]
    public async Task TryHandleScroll_NonScrollable_CompletesWithoutSwipe()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        vision.HasScrollValue = false;                         // 不可滚动
        vision.IsEndOfListValue = false;

        var (result, frameCompleted, childPushed, nextState) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);

        Assert.False(result);
        Assert.Equal(0, action.SwipeCount);                    // 不可滚动时不执行 swipe
        Assert.Equal(0, childMgr.InvalidateCount);
    }

    [Fact(DisplayName = "TryHandleScroll: 已到底 (IsEndOfList) → 不 swipe, 直接完成")]
    public async Task TryHandleScroll_AlreadyAtEnd_CompletesWithoutSwipe()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = true;                        // 已到底

        var (result, frameCompleted, childPushed, nextState) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);

        Assert.False(result);
        Assert.Equal(0, action.SwipeCount);
        Assert.Equal(0, childMgr.InvalidateCount);
    }

    [Fact(DisplayName = "TryHandleScroll: 多次滚动累积 seen 集合, 直到连续空差分确认到底")]
    public async Task TryHandleScroll_AccumulatesSeenAcrossScrolls_UntilExhausted()
    {
        var (ctx, vision, action, _) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;

        // page 0: [a]; page 1: [a,b]; page 2: [a,b,c]; page 3: [a,b,c]; page 4: [a,b,c] (到底)
        ctx.Context.SetCurrentPageAnalysis(Page("a"));
        vision.EnqueueAnalysis(Page("a", "b"));
        vision.EnqueueAnalysis(Page("a", "b", "c"));
        vision.EnqueueAnalysis(Page("a", "b", "c"));          // 第 1 次空差分 → 重试
        vision.EnqueueAnalysis(Page("a", "b", "c"));          // 第 2 次空差分 → 到底

        // 第 1 次滚动: a → a,b (揭示 b) → Continue
        var (result_fc, fc, cp, ns) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);
        Assert.True(result_fc);
        Assert.Equal(1, action.SwipeCount);

        // 第 2 次滚动: 当前页 a,b → a,b,c (揭示 c) → Continue
        (result_fc, fc, cp, ns) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);
        Assert.True(result_fc);
        Assert.Equal(2, action.SwipeCount);

        // 第 3 次滚动: a,b,c → a,b,c (第 1 次空差分) → 重试
        (result_fc, fc, cp, ns) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);
        Assert.True(result_fc);                                // 空帧重试
        Assert.Equal(3, action.SwipeCount);

        // 第 4 次滚动: a,b,c → a,b,c (第 2 次空差分, MaxEmptyScrollRetries=1 耗尽) → Stop
        (result_fc, fc, cp, ns) = await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);
        Assert.False(result_fc);
        Assert.Equal(4, action.SwipeCount);
    }

    // ── seen 元素集合 API 测试 ──────────────────────────────────

    [Fact(DisplayName = "RecordSeenElementIds: 首次记录返回 true, 重复记录返回 false")]
    public async Task RecordSeenElementIds_TracksNewVsSeen()
    {
        var ctx = new TraversalRuntimeContext("t");
        Assert.True(ctx.RecordSeenElementIds("n", new[] { "a", "b" }));   // 全新
        Assert.True(ctx.RecordSeenElementIds("n", new[] { "b", "c" }));   // c 是新的
        Assert.False(ctx.RecordSeenElementIds("n", new[] { "a", "b" }));  // 全是已见
    }

    [Fact(DisplayName = "ClearSeenElementIds: 清理后该帧集合重置")]
    public async Task ClearSeenElementIds_ResetsFrameSet()
    {
        var ctx = new TraversalRuntimeContext("t");
        ctx.RecordSeenElementIds("n", new[] { "a", "b" });
        Assert.False(ctx.RecordSeenElementIds("n", new[] { "a" }));        // 已见

        ctx.ClearSeenElementIds("n");

        Assert.True(ctx.RecordSeenElementIds("n", new[] { "a" }));         // 清理后重新视为新
    }

    [Fact(DisplayName = "seen 集合按 nodeId 隔离 (不同帧独立)")]
    public async Task RecordSeenElementIds_IsolatedPerNodeId()
    {
        var ctx = new TraversalRuntimeContext("t");
        ctx.RecordSeenElementIds("frameA", new[] { "x" });
        // frameB 不知道 frameA 的元素
        Assert.True(ctx.RecordSeenElementIds("frameB", new[] { "x" }));
    }

    // ── helpers ──────────────────────────────────────────────

    private static (StepContext ctx, FakeScrollVision vision, FakeScrollAction action, FakeChildMgr childMgr) BuildStepContext()
    {
        var runtime = new TraversalRuntimeContext("scroll-test");
        var fsm = new TraversalFSM(runtime);
        var vision = new FakeScrollVision();
        var action = new FakeScrollAction();
        var childMgr = new FakeChildMgr();
        var trace = new TraceCoordinator();   // null recorder → Active=false, 全部 no-op
        var snapshotMgr = new AlwaysUnchangedSnapshotManager();

        var ctx = new StepContext(
            Context: runtime,
            StateMachine: fsm,
            Brain: new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding()),
            ScreenState: vision,
            Action: action,
            ChildMgr: childMgr,
            NodeRegistry: null!,
            Trace: trace,
            SnapshotMgr: snapshotMgr,
            Stack: null!);
        return (ctx, vision, action, childMgr);
    }

    private static TraversalNode DynamicMatchFrame(string id) =>
        new(id, id, NodeType.Screen, new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch));

    private static PageAnalysis Page(params string[] names)
    {
        var items = names.Select(n => new MenuItem(n, new Coordinate(0.5, 0.5))).ToImmutableArray();
        return new PageAnalysis(Direction.Left, Direction.Top, Items: items);
    }

    // ── fakes ──────────────────────────────────────────────

    private sealed class FakeScrollVision : IPageAnalyzer, IScreenStateProvider
    {
        public bool HasScrollValue;
        public bool IsEndOfListValue;
        public ScrollSwipeConfig? PageScrollSwipeConfig;
        private readonly Queue<PageAnalysis?> _queue = new();

        public void EnqueueAnalysis(PageAnalysis? analysis) => _queue.Enqueue(analysis);

        bool IScreenStateProvider.HasScroll() => HasScrollValue;
        double IScreenStateProvider.GetScrollProgress() => 0.0;
        bool IScreenStateProvider.IsEndOfList() => IsEndOfListValue;
        ScrollSwipeConfig? IScreenStateProvider.GetScrollSwipeConfig() => PageScrollSwipeConfig;

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
        {
            var result = _queue.Count > 0 ? _queue.Dequeue() : null;
            return Task.FromResult(result);
        }

        public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
            => Task.FromResult<AppEntryPoint?>(new AppEntryPoint(targetApp, 0.5, 0.5));

        /// <inheritdoc />
        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(new PageTypeVerification(
                IsMatch: false,
                Confidence: 0.0,
                ActualType: expectedType));
        }
    }

    private sealed class FakeScrollAction : IActionExecutor
    {
        public int SwipeCount { get; private set; }
        public double LastSwipeStartX { get; private set; }
        public double LastSwipeStartY { get; private set; }
        public double LastSwipeEndX { get; private set; }
        public double LastSwipeEndY { get; private set; }

        public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int durationMs, CancellationToken ct = default)
        {
            SwipeCount++;
            LastSwipeStartX = sx;
            LastSwipeStartY = sy;
            LastSwipeEndX = ex;
            LastSwipeEndY = ey;
            return Task.FromResult(true);
        }

        public Task<bool> TapAsync(double x, double y, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> PressBackAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> InputTextAsync(string text, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken ct = default) => Task.FromResult(true);
        public Task WaitAsync(int milliseconds, CancellationToken ct = default) => Task.CompletedTask;
        public List<ActionRecord> GetHistory() => new();
    }

    private sealed class FakeChildMgr : IDynamicChildManager
    {
        public int InvalidateCount { get; private set; }
        public TraversalNode? GetNextUnvisitedChild(TraversalNode node, ITraversalContext context) => null;
        public void Generate(TraversalNode node, ITraversalContext context) { }
        public void Invalidate(string nodeId) => InvalidateCount++;
        public int? GetCachedFingerprint(string nodeId) => null;
        public int GetCachedChildCount(string nodeId) => 0;
    }

    // ── ScrollSwipeConfig 测试 ──────────────────────────────────

    [Fact(DisplayName = "ScrollSwipeConfig: 默认值等于之前硬编码常量")]
    public void ScrollSwipeConfig_Defaults_MatchHardcodedConstants()
    {
        var cfg = new ScrollSwipeConfig();

        Assert.Equal(0.5, cfg.StartX);
        Assert.Equal(0.7, cfg.StartY);
        Assert.Equal(0.5, cfg.EndX);
        Assert.Equal(0.3, cfg.EndY);
        Assert.Equal(300, cfg.DurationMs);
    }

    [Fact(DisplayName = "TryHandleScrollAsync: 页面级 config 优先于引擎默认")]
    public async Task TryHandleScrollAsync_UsesPageLevelConfig_WhenAvailable()
    {
        var (ctx, vision, action, _) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        ctx.Context.SetCurrentPageAnalysis(Page("a"));
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;
        vision.EnqueueAnalysis(Page("a", "b"));

        // 注入页面级 config: StartY=0.85, EndY=0.55 (自定义坐标系)
        vision.PageScrollSwipeConfig = new ScrollSwipeConfig(StartY: 0.85, EndY: 0.55);

        // 引擎默认 config
        ctx = ctx with { ScrollSwipe = new ScrollSwipeConfig(StartY: 0.7, EndY: 0.3) };

        await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);

        // 验证使用了页面级 config (而非引擎默认)
        Assert.Equal(1, action.SwipeCount);
        Assert.Equal(0.85, action.LastSwipeStartY);
        Assert.Equal(0.55, action.LastSwipeEndY);
        Assert.Equal(0.5, action.LastSwipeStartX);   // 页面 config 未覆盖的字段使用默认
    }

    [Fact(DisplayName = "TryHandleScrollAsync: 页面级 config 为 null 时回退到引擎默认")]
    public async Task TryHandleScrollAsync_FallsBackToEngineDefault_WhenPageConfigNull()
    {
        var (ctx, vision, action, _) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        ctx.Context.SetCurrentPageAnalysis(Page("a"));
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;
        vision.EnqueueAnalysis(Page("a", "b"));

        // 页面级 config 保持 null（默认）
        vision.PageScrollSwipeConfig = null;

        // 引擎默认 config: 自定义值
        ctx = ctx with { ScrollSwipe = new ScrollSwipeConfig(StartY: 0.8, EndY: 0.4) };

        await new InterceptionHandler().TryHandleScrollAsync(ctx, frame);

        Assert.Equal(1, action.SwipeCount);
        Assert.Equal(0.8, action.LastSwipeStartY);
        Assert.Equal(0.4, action.LastSwipeEndY);
    }

    [Fact(DisplayName = "SimulatedScreen.WithScrollablePage: scrollSwipe 参数存储并可检索页面级 config")]
    public void SimulatedScreen_WithScrollablePage_StoresAndRetrievesScrollSwipeConfig()
    {
        var fixture = new StateFixtureBuilder()
            .Page("settings_list", p => p.Name("Settings"))
            .Build();
        var screen = new SimulatedScreen(fixture);

        var customConfig = new ScrollSwipeConfig(StartY: 0.9, EndY: 0.1, DurationMs: 500);
        screen.WithScrollablePage("settings_list", new FixedScrollContentSource(), scrollSwipe: customConfig);

        var retrieved = screen.GetScrollSwipeConfig("settings_list");
        Assert.NotNull(retrieved);
        Assert.Equal(0.9, retrieved!.StartY);
        Assert.Equal(0.1, retrieved.EndY);
        Assert.Equal(500, retrieved.DurationMs);

        // 未配置 config 的页面返回 null
        screen.WithScrollablePage("no_config_page", new FixedScrollContentSource());
        Assert.Null(screen.GetScrollSwipeConfig("no_config_page"));
    }

    // ── Minimal scroll content source for test ──────────────────
    private sealed class FixedScrollContentSource : IScrollContentSource
    {
        public int PageSize => 10;
        public int? TotalCount => 0;
        public ImmutableArray<MockItem> GetPage(int index)
            => ImmutableArray<MockItem>.Empty;
    }

    // ── helper fakes ──────────────────────────────────────────

    private sealed class AlwaysUnchangedSnapshotManager : IPageSnapshotManager
    {
        public int Fingerprint(PageAnalysis? pageAnalysis) => 0;
        public bool HasChanged(PageAnalysis? before, PageAnalysis? after) => false;
    }
}
