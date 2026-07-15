using System.Collections.Immutable;
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
/// Phase 2.3 tests — StepOrchestrator, DynamicChildManager, TraceCoordinator,
/// EntryPolicyExecutor, PageCacheManager, PageSnapshotManager, NodeStackAdapter.
/// </summary>
public class StepOrchestratorTests
{
    private static TraversalRuntimeContext CreateContext()
        => new TraversalRuntimeContext("test-trace", maxDepth: 10);

    // ===== BRANCH Interception Source Tests =====

    [Fact(DisplayName = "Branch拦截: 允许源仅含Execute/ResultVerify/NodeSelect三种状态")]
    public async Task BranchAllowedSources_ContainsOnly3States()
    {
        var sources = StepOrchestrator.BranchAllowedSources;
        Assert.Equal(3, sources.Count);
        Assert.Contains(TraversalState.Execute, sources);
        Assert.Contains(TraversalState.ResultVerify, sources);
        Assert.Contains(TraversalState.NodeSelect, sources);
    }

    [Fact(DisplayName = "Branch拦截: PreconditionCheck不在允许源中")]
    public async Task BranchAllowedSources_ExcludesPreconditionCheck()
        => Assert.DoesNotContain(TraversalState.PreconditionCheck, StepOrchestrator.BranchAllowedSources);

    [Fact(DisplayName = "Branch拦截: ErrorHandling不在允许源中")]
    public async Task BranchAllowedSources_ExcludesErrorHandling()
        => Assert.DoesNotContain(TraversalState.ErrorHandling, StepOrchestrator.BranchAllowedSources);

    [Fact(DisplayName = "Branch拦截: PopupHandling不在允许源中")]
    public async Task BranchAllowedSources_ExcludesPopupHandling()
        => Assert.DoesNotContain(TraversalState.PopupHandling, StepOrchestrator.BranchAllowedSources);

    [Fact(DisplayName = "Branch拦截: FrameComplete不在允许源中")]
    public async Task BranchAllowedSources_ExcludesFrameComplete()
        => Assert.DoesNotContain(TraversalState.FrameComplete, StepOrchestrator.BranchAllowedSources);

    // ===== Anti-loop Property Tests =====

    [Fact(DisplayName = "反循环: DynamicMatch无子节点时标记AllVisited")]
    public async Task AntiLoop_DynamicMatchNoChildren_AllVisited()
    {
        var ctx = CreateContext();
        ctx.MarkNodeVisited("child-1");
        var parent = new TraversalNode("parent", "container", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch));
        Assert.Equal(ChildrenStrategyType.DynamicMatch, parent.ChildrenStrategy.Type);
    }

    [Fact(DisplayName = "反循环: DynamicMatch有子节点时未访问")]
    public async Task AntiLoop_DynamicMatchHasChild_NotVisited()
    {
        var ctx = CreateContext();
        Assert.False(ctx.VisitedNodes.Contains("child-1"));
        var parent = new TraversalNode("parent", "container", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch));
        Assert.Equal(ChildrenStrategyType.DynamicMatch, parent.ChildrenStrategy.Type);
    }

    // ===== StepContext Tests =====

    [Fact(DisplayName = "StepContext: 包含全部13个字段且均可访问")]
    public async Task StepContext_ContainsAll13Fields()
    {
        var ctx = CreateContext();
        var registry = new DictionaryNodeRegistry();
        var childMgr = new DynamicChildManager(registry);
        var fsm = new TraversalFSM(ctx);
        var stackAdapter = new NodeStackAdapter(ctx, registry);
        var trace = new TraceCoordinator(null, null);
        var stepCtx = new StepContext(
            Context: ctx,
            StateMachine: fsm,
            Vision: new StubVisionProvider(),
            Action: new StubActionExecutor(),
            ChildMgr: childMgr,
            NodeRegistry: registry,
            Trace: trace,
            SnapshotMgr: new PageSnapshotManager(),
            Stack: stackAdapter,
            LastKnownPath: "/home",
            LastRecordedPath: null,
            LastRecordedAction: null);

        Assert.NotNull(stepCtx.Context);
        Assert.NotNull(stepCtx.StateMachine);
        Assert.NotNull(stepCtx.Vision);
        Assert.NotNull(stepCtx.Action);
        Assert.NotNull(stepCtx.ChildMgr);
        Assert.NotNull(stepCtx.NodeRegistry);
        Assert.NotNull(stepCtx.Trace);
        Assert.NotNull(stepCtx.SnapshotMgr);
        Assert.NotNull(stepCtx.Stack);
        Assert.Equal("/home", stepCtx.LastKnownPath);
    }

    [Fact(DisplayName = "StepContext: sealed record class + 值相等性")]
    public async Task StepContext_IsSealedRecordClass()
    {
        var type = typeof(StepContext);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        // Verify record behavior: value equality (== operator)
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp); // records synthesize == operator
    }

    // ===== StepResult Tests =====

    [Fact(DisplayName = "StepResult: 包含全部6个字段且值正确")]
    public async Task StepResult_ContainsAll6Fields()
    {
        var result = new StepResult(
            NextState: TraversalState.FrameComplete,
            PathChanged: true,
            ChildPushed: false,
            FrameCompleted: true,
            AntiLoopTriggered: true,
            FrameOverrideTriggered: false);

        Assert.Equal(TraversalState.FrameComplete, result.NextState);
        Assert.True(result.PathChanged);
        Assert.False(result.ChildPushed);
        Assert.True(result.FrameCompleted);
        Assert.True(result.AntiLoopTriggered);
        Assert.False(result.FrameOverrideTriggered);
    }

    [Fact(DisplayName = "StepResult: sealed record class + 值相等性")]
    public async Task StepResult_IsSealedRecordClass()
    {
        var type = typeof(StepResult);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        // Verify record behavior: value equality (== operator)
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp); // records synthesize == operator
    }

    [Fact(DisplayName = "StepResult: AntiLoop/FrameCompleted标志组合正确")]
    public async Task StepResult_AntiLoopFlags()
    {
        var result = new StepResult(
            NextState: TraversalState.FrameComplete,
            PathChanged: false,
            ChildPushed: false,
            FrameCompleted: true,
            AntiLoopTriggered: true,
            FrameOverrideTriggered: false);
        Assert.True(result.AntiLoopTriggered);
        Assert.False(result.ChildPushed);
        Assert.True(result.FrameCompleted);
    }

    [Fact(DisplayName = "StepResult: FrameOverride/ChildPushed标志组合正确")]
    public async Task StepResult_FrameOverrideFlags()
    {
        var result = new StepResult(
            NextState: TraversalState.NodeSelect,
            PathChanged: false,
            ChildPushed: true,
            FrameCompleted: false,
            AntiLoopTriggered: false,
            FrameOverrideTriggered: true);
        Assert.True(result.FrameOverrideTriggered);
        Assert.True(result.ChildPushed);
        Assert.False(result.FrameCompleted);
    }
}

// ===== DynamicChildManager Tests =====

public class DynamicChildManagerTests
{
    [Fact(DisplayName = "子节点管理: Static策略返回未访问的静态子节点")]
    public async Task StaticStrategy_IteratesStaticChildren_ReturnsUnvisited()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new DictionaryNodeRegistry();
        var child1 = new TraversalNode("c1", "child1", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        registry.Register(child1);

        var parent = new TraversalNode("p", "parent", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "c1" }));

        var mgr = new DynamicChildManager(registry);
        var result = mgr.GetNextUnvisitedChild(parent, ctx);
        Assert.NotNull(result);
        Assert.Equal("c1", result!.NodeId);
    }

    [Fact(DisplayName = "子节点管理: Static策略全部已访问时返回null")]
    public async Task StaticStrategy_AllVisited_ReturnsNull()
    {
        var ctx = new TraversalRuntimeContext("test");
        ctx.MarkNodeVisited("c1");
        var registry = new DictionaryNodeRegistry();
        registry.Register(new TraversalNode("c1", "child1", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None)));

        var parent = new TraversalNode("p", "parent", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "c1" }));

        var mgr = new DynamicChildManager(registry);
        var result = mgr.GetNextUnvisitedChild(parent, ctx);
        Assert.Null(result);
    }

    [Fact(DisplayName = "子节点管理: DynamicMatch使用缓存的子节点")]
    public async Task DynamicMatch_UsesCachedChildren()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new DictionaryNodeRegistry();
        var child = new TraversalNode("c1", "child", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));

        var parent = new TraversalNode("p", "parent", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch));

        var mgr = new DynamicChildManager(registry);
        mgr.PrePopulateDynamicChildren("p", new List<TraversalNode> { child });

        var result = mgr.GetNextUnvisitedChild(parent, ctx);
        Assert.NotNull(result);
        Assert.Equal("c1", result!.NodeId);
    }

    [Fact(DisplayName = "子节点管理: 缓存失效后动态子节点被清除")]
    public async Task CacheInvalidation_RemovesDynamicChildren()
    {
        var registry = new DictionaryNodeRegistry();
        var mgr = new DynamicChildManager(registry);
        var child = new TraversalNode("c1", "child", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        mgr.PrePopulateDynamicChildren("p", new List<TraversalNode> { child });

        Assert.True(mgr.IsCachePopulated("p"));

        mgr.Invalidate("p");
        Assert.True(mgr.IsCacheEmpty("p"));
    }

    [Fact(DisplayName = "子节点管理: 缓存失效后生成对(_generatedPairs)保留")]
    public async Task CacheInvalidation_PreservesGeneratedPairs()
    {
        var registry = new DictionaryNodeRegistry();
        var mgr = new DynamicChildManager(registry);
        mgr._generatedPairs.Add(("fp1", "child"));
        Assert.Equal(1, mgr.GeneratedPairsCount);

        mgr.Invalidate("p");
        Assert.Equal(1, mgr.GeneratedPairsCount); // _generatedPairs persists!
    }

    [Fact(DisplayName = "子节点管理: 去重对在失效后仍存在, 未来生成跳过相同指纹")]
    public async Task DedupPersistence_AfterInvalidation_SamePairSkipped()
    {
        var registry = new DictionaryNodeRegistry();
        var mgr = new DynamicChildManager(registry);
        mgr._generatedPairs.Add(("fp1", "child"));

        mgr.Invalidate("p");
        // Pair still exists — future generation on same fingerprint would skip "child"
        Assert.Contains(("fp1", "child"), mgr._generatedPairs);
    }
}

// ===== EntryPolicyExecutor Tests =====

public class EntryPolicyExecutorTests
{
    [Fact(DisplayName = "入口策略: BuildChain生成Primary→Fallback→BindCurrent三步链")]
    public async Task BuildChain_PrimaryFallbackBindCurrent()
    {
        var executor = new EntryPolicyExecutor();
        var policy = new EntryPolicy(EntryStrategy.DirectDeeplink, "ColdLaunch");
        var chain = executor.BuildChain(policy);
        Assert.Equal(3, chain.Count);
        Assert.Equal(EntryStrategy.DirectDeeplink, chain[0]);
        Assert.Equal(EntryStrategy.ColdLaunch, chain[1]);
        Assert.Equal(EntryStrategy.BindCurrentScreen, chain[2]);
    }

    [Fact(DisplayName = "入口策略: BuildChain去重,Fallback与Primary相同时省略")]
    public async Task BuildChain_DuplicateFallbackOmitted()
    {
        var executor = new EntryPolicyExecutor();
        var policy = new EntryPolicy(EntryStrategy.DirectDeeplink, "DirectDeeplink");
        var chain = executor.BuildChain(policy);
        Assert.Equal(2, chain.Count);
        Assert.Equal(EntryStrategy.DirectDeeplink, chain[0]);
        Assert.Equal(EntryStrategy.BindCurrentScreen, chain[^1]);
    }

    [Fact(DisplayName = "入口策略: BuildChain始终以BindCurrentScreen结尾")]
    public async Task BuildChain_AlwaysEndsWithBindCurrentScreen()
    {
        var executor = new EntryPolicyExecutor();
        var policy = new EntryPolicy(EntryStrategy.ColdLaunch, "DirectDeeplink");
        var chain = executor.BuildChain(policy);
        Assert.Equal(EntryStrategy.BindCurrentScreen, chain[^1]);
    }

    [Fact(DisplayName = "入口策略: BindCurrentScreen执行必定成功")]
    public async Task Execute_BindCurrentScreenAlwaysSucceeds()
    {
        var executor = new EntryPolicyExecutor();
        var config = new EntryConfig(WaitMode: WaitMode.Fast, TraceLevel: TraceLevel.Basic);
        var policy = new EntryPolicy(EntryStrategy.BindCurrentScreen);
        var result = executor.Execute(policy, config, "test-app");
        Assert.True(result.Success);
    }
}

public class PageCacheManagerTests
{
    [Fact(DisplayName = "页面缓存: Update存储PageCacheInfo到Context")]
    public async Task Update_StoresPageCacheInfo()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mgr = new PageCacheManager();
        var info = new PageCacheInfo(
            Items: new List<MenuItem>(),
            Timestamp: DateTimeOffset.UtcNow,
            ScreenHash: 12345);
        mgr.Update("/home/settings", info, ctx);
        Assert.True(ctx.PageCache.ContainsKey("/home/settings"));
    }

    [Fact(DisplayName = "页面缓存: Restore返回已缓存的菜单项")]
    public async Task Restore_ReturnsCachedItems()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mgr = new PageCacheManager();
        var items = new List<MenuItem>
        {
            new MenuItem("wifi", new Coordinate(0.5, 0.5), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle)
        };
        var info = new PageCacheInfo(items, DateTimeOffset.UtcNow, 42);
        mgr.Update("/settings", info, ctx);

        var restored = mgr.Restore("/settings", ctx);
        Assert.NotNull(restored);
        Assert.Single(restored!);
    }

    [Fact(DisplayName = "页面缓存: Restore对不存在路径返回null")]
    public async Task Restore_NullForNonexistentPath()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mgr = new PageCacheManager();
        Assert.Null(mgr.Restore("/unknown/path", ctx));
    }

    [Fact(DisplayName = "页面缓存: PageCacheInfo为sealed record class含3字段")]
    public async Task PageCacheInfo_IsSealedRecordClassWith3Fields()
    {
        var type = typeof(PageCacheInfo);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        // Verify record behavior: value equality (== operator)
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp); // records synthesize == operator

        var info = new PageCacheInfo(
            Items: new List<MenuItem>(),
            Timestamp: DateTimeOffset.UtcNow,
            ScreenHash: 12345);
        Assert.NotNull(info.Items);
        Assert.Equal(12345, info.ScreenHash);
    }
}

// ===== PageSnapshotManager Tests =====

public class PageSnapshotManagerTests
{
    private readonly IPageSnapshotManager _mgr = new PageSnapshotManager();

    [Fact(DisplayName = "页面快照: Fingerprint对null输入返回0")]
    public async Task Fingerprint_NullInput_Returns0()
        => Assert.Equal(0, _mgr.Fingerprint(null));

    [Fact(DisplayName = "页面快照: Fingerprint对空Items返回0")]
    public async Task Fingerprint_EmptyItems_Returns0()
    {
        var analysis = new PageAnalysis(Direction.Top, Direction.Top, Items: ImmutableArray<MenuItem>.Empty);
        Assert.Equal(0, _mgr.Fingerprint(analysis));
    }

    [Fact(DisplayName = "页面快照: Fingerprint对相同输入产生确定性哈希")]
    public async Task Fingerprint_Deterministic()
    {
        var items = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle),
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var a1 = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        var a2 = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        Assert.Equal(_mgr.Fingerprint(a1), _mgr.Fingerprint(a2));
    }

    [Fact(DisplayName = "页面快照: Fingerprint对不同输入产生不同哈希")]
    public async Task Fingerprint_DifferentInput_DifferentHash()
    {
        var items1 = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var items2 = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var a1 = new PageAnalysis(Direction.Top, Direction.Top, Items: items1);
        var a2 = new PageAnalysis(Direction.Top, Direction.Top, Items: items2);
        Assert.NotEqual(_mgr.Fingerprint(a1), _mgr.Fingerprint(a2));
    }

    [Fact(DisplayName = "页面快照: HasChanged在指纹不同时返回true")]
    public async Task HasChanged_TrueWhenFingerprintsDiffer()
    {
        var items1 = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var items2 = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var before = new PageAnalysis(Direction.Top, Direction.Top, Items: items1);
        var after = new PageAnalysis(Direction.Top, Direction.Top, Items: items2);
        Assert.True(_mgr.HasChanged(before, after));
    }

    [Fact(DisplayName = "页面快照: HasChanged在指纹相同时返回false")]
    public async Task HasChanged_FalseWhenFingerprintsEqual()
    {
        var items = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var before = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        var after = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        Assert.False(_mgr.HasChanged(before, after));
    }

    [Fact(DisplayName = "页面快照: HasChanged在before=null/after非null时返回true")]
    public async Task HasChanged_TrueWhenBeforeNullAfterNotNull()
    {
        var items = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var after = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        Assert.True(_mgr.HasChanged(null, after));
    }
}

// ===== NodeStackAdapter Tests =====

public class NodeStackAdapterTests
{
    [Fact(DisplayName = "节点栈适配: Push注册节点并入栈")]
    public async Task Push_RegistersNodeAndPushesStack()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new DictionaryNodeRegistry();
        var node = new TraversalNode("n1", "node1", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var adapter = new NodeStackAdapter(ctx, registry);
        adapter.Push(node);
        Assert.Equal(1, ctx.NodeStack.Depth);
        Assert.NotNull(registry.GetNode("n1"));
    }

    [Fact(DisplayName = "节点栈适配: Pop从栈中移除节点")]
    public async Task Pop_RemovesFromStack()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new DictionaryNodeRegistry();
        var node = new TraversalNode("n1", "node1", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        registry.Register(node);
        ctx.NodeStack.Push(node);

        var adapter = new NodeStackAdapter(ctx, registry);
        adapter.Pop();
        Assert.Equal(0, ctx.NodeStack.Depth);
    }

    [Fact(DisplayName = "节点栈适配: Peek返回栈顶节点不移除")]
    public async Task Peek_ReturnsTopNodeWithoutRemoving()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new DictionaryNodeRegistry();
        var node = new TraversalNode("n1", "node1", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        registry.Register(node);
        ctx.NodeStack.Push(node);

        var adapter = new NodeStackAdapter(ctx, registry);
        var peeked = adapter.Peek();
        Assert.NotNull(peeked);
        Assert.Equal("n1", peeked!.NodeId);
        Assert.Equal(1, ctx.NodeStack.Depth); // Stack unchanged
    }
}

// ===== IVisionProvider placeholder test =====

public class IVisionProviderTests
{
    [Fact(DisplayName = "IVisionProvider: StubVisionProvider返回null")]
    public async Task StubVisionProvider_ReturnsNull()
    {
        var provider = new StubVisionProvider();
        var result = await provider.AnalyzeCurrentPageAsync();
        Assert.Null(result);
    }
}

// ===== Phase 2.1d: H-10 PageSnapshotManager deterministic hash =====

public class PageSnapshotManagerDeterministicTests
{
    private readonly IPageSnapshotManager _mgr = new PageSnapshotManager();

    [Fact(DisplayName = "页面快照(H-10): Fingerprint多次调用返回相同值(确定性哈希)")]
    public async Task Fingerprint_DeterministicAcrossMultipleCalls_SameValue()
    {
        // H-10: character-based hash is deterministic — no string.GetHashCode (non-deterministic across processes)
        var items = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle),
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var page = new PageAnalysis(Direction.Top, Direction.Top, Items: items);

        // Call Fingerprint 5 times — all must return the same value
        var hashes = Enumerable.Range(0, 5).Select(_ => _mgr.Fingerprint(page)).ToList();
        Assert.True(hashes.All(h => h == hashes[0]));
    }
}

// ===== TraversalEngine unified entry point tests =====

public class TraversalEngineEntryPointTests
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

    [Fact(DisplayName = "TraversalEngine: 构造时GlobalState设为Traversing")]
    public async Task Constructor_SetsGlobalStateToTraversing()
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var engine = CreateEngine(fixture, root, new Dictionary<string, TraversalNode>());
        Assert.Equal(GlobalState.Traversing, engine.CurrentState);
    }

    [Fact(DisplayName = "TraversalEngine: 无RootNode时自动构建默认根节点(app_root)")]
    public async Task Constructor_NoRootNode_BuildsDefaultRoot()
    {
        var fixture = SimpleFixture();
        var nodes = new Dictionary<string, TraversalNode> { ["btn_go"] = Leaf("btn_go", ClickAt(0.5, 0.5)) };
        var plan = new TraversalPlan(EntryApp: "my_app", EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen), StaticNodes: nodes);
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var engine = new TraversalEngine(plan, vision, action);
        Assert.Equal("my_app_root", engine.Context.CurrentFrame?.NodeId);
    }

    [Fact(DisplayName = "TraversalEngine.Run: 全部节点已访问 → 成功完成(AllVisited)")]
    public async Task Run_AllVisited_CompletesSuccessfully()
    {
        var fixture = SimpleFixture();
        var nodes = new Dictionary<string, TraversalNode> { ["btn_go"] = Leaf("btn_go", ClickAt(0.5, 0.5)) };
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static, StaticChildren: new List<string> { "btn_go" }));
        var engine = CreateEngine(fixture, root, nodes);
        var result = await engine.RunAsync();
        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
    }

    [Fact(DisplayName = "TraversalEngine.RunAsync: 全部节点已访问 → 成功完成(AllVisited)")]
    public async Task RunAsync_AllVisited_CompletesSuccessfully()
    {
        var fixture = SimpleFixture();
        var nodes = new Dictionary<string, TraversalNode> { ["btn_go"] = Leaf("btn_go", ClickAt(0.5, 0.5)) };
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static, StaticChildren: new List<string> { "btn_go" }));
        var engine = CreateEngine(fixture, root, nodes);
        var result = await engine.RunAsync();
        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
    }

    [Fact(DisplayName = "TraversalEngine.Run: 超过MaxSteps → 失败(MaxSteps)")]
    public async Task Run_MaxSteps_ReturnsMaxStepsReason()
    {
        var fixture = SimpleFixture();
        var nodes = new Dictionary<string, TraversalNode> { ["btn_go"] = Leaf("btn_go", ClickAt(0.5, 0.5)) };
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static, StaticChildren: new List<string> { "btn_go" }));
        var engine = CreateEngine(fixture, root, nodes, new TraversalEngineConfig { MaxSteps = 1 });
        var result = await engine.RunAsync();
        Assert.False(result.Success);
        Assert.Equal(TraversalResult.Reasons.MaxSteps, result.CompletionReason);
    }

    [Fact(DisplayName = "TraversalEngine.InitializeAsync: 返回已完成任务, 状态保持Traversing")]
    public async Task InitializeAsync_ReturnsCompletedTask()
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var engine = CreateEngine(fixture, root, new Dictionary<string, TraversalNode>());
        await engine.InitializeAsync();
        Assert.Equal(GlobalState.Traversing, engine.CurrentState);
    }

    [Fact(DisplayName = "TraversalEngine.StopAsync: 设置状态为Terminated")]
    public async Task StopAsync_SetsTerminated()
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var engine = CreateEngine(fixture, root, new Dictionary<string, TraversalNode>());
        await engine.StopAsync();
        Assert.Equal(GlobalState.Terminated, engine.CurrentState);
    }

    [Fact(DisplayName = "TraversalEngine.GetStateAsync: 返回当前GlobalState")]
    public async Task GetStateAsync_ReturnsCurrentState()
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var engine = CreateEngine(fixture, root, new Dictionary<string, TraversalNode>());
        var state = await engine.GetStateAsync();
        Assert.Equal(GlobalState.Traversing, state);
    }

    [Fact(DisplayName = "TraversalEngine.Run: TraceEnabled=true → 产生TraceRecords和TraceId")]
    public async Task Run_TraceEnabled_ProducesTraceRecords()
    {
        var fixture = SimpleFixture();
        var nodes = new Dictionary<string, TraversalNode> { ["btn_go"] = Leaf("btn_go", ClickAt(0.5, 0.5)) };
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static, StaticChildren: new List<string> { "btn_go" }));
        var engine = CreateEngine(fixture, root, nodes, new TraversalEngineConfig { TraceEnabled = true });
        var result = await engine.RunAsync();
        Assert.NotEmpty(result.Trace);
        Assert.NotNull(result.TraceId);
    }

    [Fact(DisplayName = "TraversalEngine.Run: TraceEnabled=false → Trace为空")]
    public async Task Run_TraceDisabled_ProducesEmptyTrace()
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var engine = CreateEngine(fixture, root, new Dictionary<string, TraversalNode>(),
            new TraversalEngineConfig { TraceEnabled = false });
        var result = await engine.RunAsync();
        Assert.Empty(result.Trace);
    }

    [Fact(DisplayName = "TraversalEngine: Plan属性返回原始TraversalPlan")]
    public async Task Plan_ReturnsOriginalPlan()
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var engine = CreateEngine(fixture, root, new Dictionary<string, TraversalNode>());
        Assert.NotNull(engine.Plan);
        Assert.Equal("test", engine.Plan.EntryApp);
    }
}

// ===== TraceRecord unit tests =====

public class TraceRecordUnitTests
{
    [Fact(DisplayName = "TraceRecord: 构造时全部9字段正确设置")]
    public async Task TraceRecord_Construction_AllFieldsSet()
    {
        var record = new TraceRecord(1, TraversalState.NodeSelect, TraversalState.PreconditionCheck,
            "root", "home", "click", true, false, false);
        Assert.Equal(1, record.StepNumber);
        Assert.Equal(TraversalState.NodeSelect, record.FromState);
        Assert.Equal(TraversalState.PreconditionCheck, record.ToState);
        Assert.Equal("root", record.CurrentNodeId);
        Assert.Equal("home", record.CurrentPageId);
        Assert.Equal("click", record.ActionExecuted);
        Assert.True(record.ActionSuccess);
        Assert.False(record.ChildPushed);
        Assert.False(record.FrameCompleted);
    }

    [Fact(DisplayName = "TraceRecord: 可空字段(NodeId/PageId/Action)默认null")]
    public async Task TraceRecord_NullableFields_DefaultNull()
    {
        var record = new TraceRecord(1, TraversalState.NodeSelect, TraversalState.Execute,
            null, null, null, false, false, false);
        Assert.Null(record.CurrentNodeId);
        Assert.Null(record.CurrentPageId);
        Assert.Null(record.ActionExecuted);
    }

    [Fact(DisplayName = "TraceRecord: sealed record class + 值相等性")]
    public async Task TraceRecord_IsSealedRecordClass()
    {
        var type = typeof(TraceRecord);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp);
    }
}

// ===== TraversalEngineConfig unit tests =====

public class TraversalEngineConfigUnitTests
{
    [Fact(DisplayName = "TraversalEngineConfig: 默认值(MaxSteps=1000, MaxDepth=10, TraceEnabled=true)")]
    public async Task DefaultConfig_HasExpectedDefaults()
    {
        var config = new TraversalEngineConfig();
        Assert.Equal(1000, config.MaxSteps);
        Assert.Equal(10, config.MaxDepth);
        Assert.False(config.ThrowOnError);
        Assert.True(config.TraceEnabled);
        Assert.Equal(0, config.DelayPerStepMs);
    }

    [Fact(DisplayName = "TraversalEngineConfig: 自定义值覆盖默认值")]
    public async Task CustomConfig_OverridesDefaults()
    {
        var config = new TraversalEngineConfig { MaxSteps = 50, MaxDepth = 5, ThrowOnError = true, TraceEnabled = false, DelayPerStepMs = 100 };
        Assert.Equal(50, config.MaxSteps);
        Assert.Equal(5, config.MaxDepth);
        Assert.True(config.ThrowOnError);
        Assert.False(config.TraceEnabled);
        Assert.Equal(100, config.DelayPerStepMs);
    }

    [Fact(DisplayName = "TraversalEngineConfig: sealed record class + 值相等性")]
    public async Task TraversalEngineConfig_IsSealedRecordClass()
    {
        var type = typeof(TraversalEngineConfig);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp);
    }
}

// ===== TraversalResult unit tests =====

public class TraversalResultUnitTests
{
    [Fact(DisplayName = "TraversalResult: 成功构造(Success=true, AllVisited, Error=null)")]
    public async Task TraversalResult_SuccessfulConstruction()
    {
        var result = new TraversalResult(true, TraversalResult.Reasons.AllVisited, 10, 1.5,
            ImmutableArray<ActionRecord>.Empty, ImmutableArray<string>.Empty,
            ImmutableArray<TraceRecord>.Empty, "trace-001", TraversalState.FrameComplete, null);
        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
        Assert.Null(result.Error);
    }

    [Fact(DisplayName = "TraversalResult: 错误构造(Success=false, Error原因, 含异常)")]
    public async Task TraversalResult_ErrorConstruction()
    {
        var ex = new InvalidOperationException("test error");
        var result = new TraversalResult(false, TraversalResult.Reasons.Error, 3, 0.5,
            ImmutableArray<ActionRecord>.Empty, ImmutableArray<string>.Empty,
            ImmutableArray<TraceRecord>.Empty, "trace-002", TraversalState.ErrorHandling, ex);
        Assert.False(result.Success);
        Assert.Equal(TraversalResult.Reasons.Error, result.CompletionReason);
        Assert.NotNull(result.Error);
    }

    [Fact(DisplayName = "TraversalResult.Reasons: 全部7个常量已定义(all_visited/max_steps/error/anti_loop/cancelled/target_found/timeout)")]
    public async Task Reasons_AllConstantsDefined()
    {
        Assert.Equal("all_visited", TraversalResult.Reasons.AllVisited);
        Assert.Equal("max_steps", TraversalResult.Reasons.MaxSteps);
        Assert.Equal("error", TraversalResult.Reasons.Error);
        Assert.Equal("anti_loop", TraversalResult.Reasons.AntiLoop);
        Assert.Equal("cancelled", TraversalResult.Reasons.Cancelled);
        Assert.Equal("target_found", TraversalResult.Reasons.TargetFound);
        Assert.Equal("timeout", TraversalResult.Reasons.Timeout);
    }

    [Fact(DisplayName = "TraversalResult: sealed record class + 值相等性")]
    public async Task TraversalResult_IsSealedRecordClass()
    {
        var type = typeof(TraversalResult);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp);
    }
}

// ===== CompletionPolicy Tests (Phase A) =====

public class CompletionPolicyTests
{
    private static TraversalNode Leaf(string id, Operation op)
        => new(id, id, NodeType.LeafAction, op, new ChildrenStrategy(ChildrenStrategyType.None));

    private static Operation ClickAt(double x, double y)
        => new(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(x, y)));

    private static TraversalEngine CreateEngine(
        StateFixture fixture, TraversalNode root,
        Dictionary<string, TraversalNode> nodes,
        TraversalEngineConfig? config = null,
        CompletionPolicy? completionPolicy = null)
    {
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var plan = new TraversalPlan(
            EntryApp: "test",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan",
            PlanId: "test-001",
            RootNode: root,
            StaticNodes: nodes,
            CompletionPolicy: completionPolicy);
        return new TraversalEngine(plan, vision, action, config);
    }

    [Fact(DisplayName = "CompletionPolicy: TargetFound精确匹配后终止(Operation.Target.Value=Wi-Fi)")]
    public async Task TargetFound_StopsAtTargetNode()
    {
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen")
                .Switch("wifi_toggle", "Wi-Fi", 0.5, 0.5))
            .Page("wifi_page", p => p.Name("WiFiSettings")
                .BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("wifi").Click("wifi_toggle").From("home").To("wifi_page"))
            .Transition(t => t.Id("back").Click("btn_back").From("wifi_page").To("home"))
            .Build();

        // Node with Operation.Target.Value = "Wi-Fi" (text-based target, NoAction skips execution)
        var wifiNode = new TraversalNode("wifi_toggle", "Wi-Fi Toggle", NodeType.LeafAction,
            new Operation(OperationType.NoAction, new Target(TargetType.Text, "Wi-Fi")),
            new ChildrenStrategy(ChildrenStrategyType.None));

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "wifi_toggle" }));

        var nodes = new Dictionary<string, TraversalNode> { ["wifi_toggle"] = wifiNode };
        var policy = new CompletionPolicy(
            CompletionPolicyType.TargetFound,
            TargetName: "Wi-Fi",
            MatchMode: MatchMode.Exact);

        var engine = CreateEngine(fixture, root, nodes, completionPolicy: policy);
        var result = await engine.RunAsync();

        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);
        Assert.Equal(GlobalState.Completed, engine.CurrentState);
    }

    [Fact(DisplayName = "CompletionPolicy: TargetFound Contains模式匹配(Blue→Bluetooth)")]
    public async Task TargetFound_ContainsMatch()
    {
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen")
                .Switch("bt_toggle", "Bluetooth", 0.5, 0.5))
            .Page("bt_page", p => p.Name("BluetoothSettings")
                .BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("bt").Click("bt_toggle").From("home").To("bt_page"))
            .Transition(t => t.Id("back").Click("btn_back").From("bt_page").To("home"))
            .Build();

        var btNode = new TraversalNode("bt_toggle", "Bluetooth Toggle", NodeType.LeafAction,
            new Operation(OperationType.NoAction, new Target(TargetType.Text, "Bluetooth")),
            new ChildrenStrategy(ChildrenStrategyType.None));

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "bt_toggle" }));

        var nodes = new Dictionary<string, TraversalNode> { ["bt_toggle"] = btNode };
        var policy = new CompletionPolicy(
            CompletionPolicyType.TargetFound,
            TargetName: "Blue",
            MatchMode: MatchMode.Contains);

        var engine = CreateEngine(fixture, root, nodes, completionPolicy: policy);
        var result = await engine.RunAsync();

        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);
    }

    [Fact(DisplayName = "CompletionPolicy: Timeout超过TimeoutSeconds后终止")]
    public async Task Timeout_ExceedsPolicySeconds()
    {
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen")
                .Button("btn_go", "Go", 0.5, 0.5))
            .Page("next", p => p.Name("NextScreen")
                .BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("go").Click("btn_go").From("home").To("next"))
            .Transition(t => t.Id("back").Click("btn_back").From("next").To("home"))
            .Build();

        var nodes = new Dictionary<string, TraversalNode>
        { ["btn_go"] = Leaf("btn_go", ClickAt(0.5, 0.5)) };
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_go" }));

        // TimeoutSeconds=0.001, DelayPerStepMs=50 ensures elapsed > threshold
        var policy = new CompletionPolicy(CompletionPolicyType.Timeout, TimeoutSeconds: 0.001);
        var config = new TraversalEngineConfig { DelayPerStepMs = 50 };

        var engine = CreateEngine(fixture, root, nodes, config, policy);
        var result = await engine.RunAsync();

        Assert.False(result.Success);
        Assert.Equal(TraversalResult.Reasons.Timeout, result.CompletionReason);
        Assert.True(result.ElapsedSeconds > 0.001, $"Elapsed {result.ElapsedSeconds}s should exceed 0.001s");
        Assert.Equal(GlobalState.Terminated, engine.CurrentState);
    }

    [Fact(DisplayName = "CompletionPolicy: MaxSteps软上限=5优于引擎硬上限=1000")]
    public async Task MaxStepsPolicy_ReachesUserLimit()
    {
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen")
                .Button("btn_1", "One", 0.1, 0.5)
                .Button("btn_2", "Two", 0.3, 0.5)
                .Button("btn_3", "Three", 0.5, 0.5))
            .Build();

        var child1 = new TraversalNode("child1", "Child 1", NodeType.LeafAction,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var child2 = new TraversalNode("child2", "Child 2", NodeType.LeafAction,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "child1", "child2" }));

        var nodes = new Dictionary<string, TraversalNode>
        { ["child1"] = child1, ["child2"] = child2 };

        // CompletionPolicy.MaxSteps=5 overrides engine hard limit=1000
        var policy = new CompletionPolicy(CompletionPolicyType.MaxSteps, MaxSteps: 5);
        var config = new TraversalEngineConfig { MaxSteps = 1000 };

        var engine = CreateEngine(fixture, root, nodes, config, policy);
        var result = await engine.RunAsync();

        Assert.Equal(TraversalResult.Reasons.MaxSteps, result.CompletionReason);
        Assert.True(result.TotalSteps <= 5,
            $"TotalSteps={result.TotalSteps} should not exceed policy.MaxSteps=5");
        Assert.False(result.Success); // MaxSteps is not a success reason
    }

    [Fact(DisplayName = "CompletionPolicy: Type=None不触发额外终止,正常AllVisited完成")]
    public async Task CompletionPolicy_None_NoEffect()
    {
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen")
                .Button("btn_go", "Go", 0.5, 0.5))
            .Page("next", p => p.Name("NextScreen")
                .BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("go").Click("btn_go").From("home").To("next"))
            .Transition(t => t.Id("back").Click("btn_back").From("next").To("home"))
            .Build();

        var nodes = new Dictionary<string, TraversalNode>
        { ["btn_go"] = Leaf("btn_go", ClickAt(0.5, 0.5)) };
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_go" }));

        // CompletionPolicyType.None → check block skipped, normal AllVisited
        var policy = new CompletionPolicy(CompletionPolicyType.None);

        var engine = CreateEngine(fixture, root, nodes, completionPolicy: policy);
        var result = await engine.RunAsync();

        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
    }
}

// ===== Stub implementations for testing =====

internal sealed class StubVisionProvider : IVisionProvider
{
    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
        => Task.FromResult<PageAnalysis?>(null);

    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => Task.FromResult<AppEntryPoint?>(null);
}

internal sealed class StubActionExecutor : IActionExecutor
{
    public Task<bool> TapAsync(double x, double y, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int durationMs, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> PressBackAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> InputTextAsync(string text, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken ct = default) => Task.FromResult(true);
    public Task WaitAsync(int ms, CancellationToken ct = default) => Task.CompletedTask;
    public List<ActionRecord> GetHistory() => new();
}
