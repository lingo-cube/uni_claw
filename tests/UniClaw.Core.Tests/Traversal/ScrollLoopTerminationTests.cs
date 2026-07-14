using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Coordinate = UniClaw.Core.Domain.Models.Content.Coordinate;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// 滚动循环终止单测 (滚动 = 操作 + 判断 模型)。
/// 直接测试 <see cref="StepOrchestrator.TryHandleScroll"/> 与
/// <see cref="TraversalRuntimeContext"/> 的 per-frame seen 元素集合 API。
/// </summary>
public class ScrollLoopTerminationTests
{
    // ── TryHandleScroll 契约测试 ──────────────────────────────────

    [Fact(DisplayName = "TryHandleScroll: 滚出未见元素 → Continue (nextState=NodeSelect)")]
    public void TryHandleScroll_UnseenElements_Continues()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        ctx.Context.SetCurrentPageAnalysis(Page("a"));        // 滚动前可见页 (page 0)
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;
        vision.EnqueueAnalysis(Page("a", "b"));               // 滚动后揭示新元素 b

        bool frameCompleted = false;
        bool childPushed = false;
        var nextState = TraversalState.Branch;

        bool result = StepOrchestrator.TryHandleScroll(ctx, frame, ref frameCompleted, ref childPushed, ref nextState);

        Assert.True(result);                                   // 滚出未见元素 → 继续
        Assert.Equal(1, action.SwipeCount);                    // 执行了一次 swipe (操作)
        Assert.Equal(TraversalState.NodeSelect, nextState);
        Assert.False(frameCompleted);
        Assert.False(childPushed);
        Assert.Equal(1, childMgr.InvalidateCount);             // 子节点缓存已失效
    }

    [Fact(DisplayName = "TryHandleScroll: 全是已见元素 → Stop (到底)")]
    public void TryHandleScroll_AllSeen_Stops()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        ctx.Context.SetCurrentPageAnalysis(Page("a", "b"));    // 滚动前 page 0
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;
        vision.EnqueueAnalysis(Page("a", "b"));                // 滚动后无新元素 → 到底

        bool frameCompleted = false;
        bool childPushed = false;
        var nextState = TraversalState.NodeSelect;

        bool result = StepOrchestrator.TryHandleScroll(ctx, frame, ref frameCompleted, ref childPushed, ref nextState);

        Assert.False(result);                                  // 到底 → 由调用方完成帧
        Assert.Equal(1, action.SwipeCount);                    // 仍执行了一次 swipe (经验式到底检测)
        Assert.False(frameCompleted);                          // TryHandleScroll 不设置完成标志 (由调用方决定)
        Assert.Equal(1, childMgr.InvalidateCount);
        // seen 集合已在到底时清理: 再次记录相同元素应判定为"有新" (说明被清空过)
        Assert.True(ctx.Context.RecordSeenElementIds("list", new[] { "a" }));
    }

    [Fact(DisplayName = "TryHandleScroll: 不可滚动 → 不 swipe, 直接完成")]
    public void TryHandleScroll_NonScrollable_CompletesWithoutSwipe()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        vision.HasScrollValue = false;                         // 不可滚动
        vision.IsEndOfListValue = false;

        bool frameCompleted = false;
        bool childPushed = false;
        var nextState = TraversalState.NodeSelect;

        bool result = StepOrchestrator.TryHandleScroll(ctx, frame, ref frameCompleted, ref childPushed, ref nextState);

        Assert.False(result);
        Assert.Equal(0, action.SwipeCount);                    // 不可滚动时不执行 swipe
        Assert.Equal(0, childMgr.InvalidateCount);
    }

    [Fact(DisplayName = "TryHandleScroll: 已到底 (IsEndOfList) → 不 swipe, 直接完成")]
    public void TryHandleScroll_AlreadyAtEnd_CompletesWithoutSwipe()
    {
        var (ctx, vision, action, childMgr) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = true;                        // 已到底

        bool frameCompleted = false;
        bool childPushed = false;
        var nextState = TraversalState.NodeSelect;

        bool result = StepOrchestrator.TryHandleScroll(ctx, frame, ref frameCompleted, ref childPushed, ref nextState);

        Assert.False(result);
        Assert.Equal(0, action.SwipeCount);
        Assert.Equal(0, childMgr.InvalidateCount);
    }

    [Fact(DisplayName = "TryHandleScroll: 多次滚动累积 seen 集合, 直到无新元素终止")]
    public void TryHandleScroll_AccumulatesSeenAcrossScrolls_UntilExhausted()
    {
        var (ctx, vision, action, _) = BuildStepContext();
        var frame = DynamicMatchFrame("list");
        vision.HasScrollValue = true;
        vision.IsEndOfListValue = false;

        // page 0: [a]; page 1: [a,b]; page 2: [a,b,c]; page 3: [a,b,c] (到底)
        ctx.Context.SetCurrentPageAnalysis(Page("a"));
        vision.EnqueueAnalysis(Page("a", "b"));
        vision.EnqueueAnalysis(Page("a", "b", "c"));
        vision.EnqueueAnalysis(Page("a", "b", "c"));

        bool fc = false, cp = false;
        var ns = TraversalState.NodeSelect;

        // 第 1 次滚动: a → a,b (揭示 b) → Continue
        Assert.True(StepOrchestrator.TryHandleScroll(ctx, frame, ref fc, ref cp, ref ns));
        Assert.Equal(1, action.SwipeCount);

        // 模拟引擎把当前页推进到滚动后页 (AnalyzeCurrentPageAsync 已 SetCurrentPageAnalysis)
        // 第 2 次滚动: 当前页 a,b → a,b,c (揭示 c) → Continue
        Assert.True(StepOrchestrator.TryHandleScroll(ctx, frame, ref fc, ref cp, ref ns));
        Assert.Equal(2, action.SwipeCount);

        // 第 3 次滚动: 当前页 a,b,c → a,b,c (无新) → Stop
        Assert.False(StepOrchestrator.TryHandleScroll(ctx, frame, ref fc, ref cp, ref ns));
        Assert.Equal(3, action.SwipeCount);
    }

    // ── seen 元素集合 API 测试 ──────────────────────────────────

    [Fact(DisplayName = "RecordSeenElementIds: 首次记录返回 true, 重复记录返回 false")]
    public void RecordSeenElementIds_TracksNewVsSeen()
    {
        var ctx = new TraversalRuntimeContext("t");
        Assert.True(ctx.RecordSeenElementIds("n", new[] { "a", "b" }));   // 全新
        Assert.True(ctx.RecordSeenElementIds("n", new[] { "b", "c" }));   // c 是新的
        Assert.False(ctx.RecordSeenElementIds("n", new[] { "a", "b" }));  // 全是已见
    }

    [Fact(DisplayName = "ClearSeenElementIds: 清理后该帧集合重置")]
    public void ClearSeenElementIds_ResetsFrameSet()
    {
        var ctx = new TraversalRuntimeContext("t");
        ctx.RecordSeenElementIds("n", new[] { "a", "b" });
        Assert.False(ctx.RecordSeenElementIds("n", new[] { "a" }));        // 已见

        ctx.ClearSeenElementIds("n");

        Assert.True(ctx.RecordSeenElementIds("n", new[] { "a" }));         // 清理后重新视为新
    }

    [Fact(DisplayName = "seen 集合按 nodeId 隔离 (不同帧独立)")]
    public void RecordSeenElementIds_IsolatedPerNodeId()
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
            Vision: vision,
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

    private sealed class FakeScrollVision : IVisionProvider
    {
        public bool HasScrollValue;
        public bool IsEndOfListValue;
        private readonly Queue<PageAnalysis?> _queue = new();

        public void EnqueueAnalysis(PageAnalysis? analysis) => _queue.Enqueue(analysis);

        bool IVisionProvider.HasScroll() => HasScrollValue;
        double IVisionProvider.GetScrollProgress() => 0.0;
        bool IVisionProvider.IsEndOfList() => IsEndOfListValue;

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
        {
            var result = _queue.Count > 0 ? _queue.Dequeue() : null;
            return Task.FromResult(result);
        }

        public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
            => Task.FromResult<AppEntryPoint?>(new AppEntryPoint(0.5, 0.5));
    }

    private sealed class FakeScrollAction : IActionExecutor
    {
        public int SwipeCount { get; private set; }

        public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int durationMs, CancellationToken ct = default)
        {
            SwipeCount++;
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
    }

    private sealed class AlwaysUnchangedSnapshotManager : IPageSnapshotManager
    {
        public int Fingerprint(PageAnalysis? pageAnalysis) => 0;
        public bool HasChanged(PageAnalysis? before, PageAnalysis? after) => false;
    }
}
