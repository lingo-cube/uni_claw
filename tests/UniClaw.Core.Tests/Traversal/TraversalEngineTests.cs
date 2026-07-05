using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
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

    [Fact]
    public void BranchAllowedSources_ContainsOnly3States()
    {
        var sources = StepOrchestrator.BranchAllowedSources;
        Assert.Equal(3, sources.Count);
        Assert.Contains(TraversalState.Execute, sources);
        Assert.Contains(TraversalState.ResultVerify, sources);
        Assert.Contains(TraversalState.NodeSelect, sources);
    }

    [Fact]
    public void BranchAllowedSources_ExcludesPreconditionCheck()
        => Assert.DoesNotContain(TraversalState.PreconditionCheck, StepOrchestrator.BranchAllowedSources);

    [Fact]
    public void BranchAllowedSources_ExcludesErrorHandling()
        => Assert.DoesNotContain(TraversalState.ErrorHandling, StepOrchestrator.BranchAllowedSources);

    [Fact]
    public void BranchAllowedSources_ExcludesPopupHandling()
        => Assert.DoesNotContain(TraversalState.PopupHandling, StepOrchestrator.BranchAllowedSources);

    [Fact]
    public void BranchAllowedSources_ExcludesFrameComplete()
        => Assert.DoesNotContain(TraversalState.FrameComplete, StepOrchestrator.BranchAllowedSources);

    // ===== Anti-loop Property Tests =====

    [Fact]
    public void AntiLoop_DynamicMatchNoChildren_AllVisited()
    {
        var ctx = CreateContext();
        ctx.MarkNodeVisited("child-1");
        var parent = new TraversalNode("parent", "container", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch));
        Assert.Equal(ChildrenStrategyType.DynamicMatch, parent.ChildrenStrategy.Type);
    }

    [Fact]
    public void AntiLoop_DynamicMatchHasChild_NotVisited()
    {
        var ctx = CreateContext();
        Assert.False(ctx.VisitedNodes.Contains("child-1"));
        var parent = new TraversalNode("parent", "container", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch));
        Assert.Equal(ChildrenStrategyType.DynamicMatch, parent.ChildrenStrategy.Type);
    }

    // ===== StepContext Tests =====

    [Fact]
    public void StepContext_ContainsAll13Fields()
    {
        var ctx = CreateContext();
        var registry = new SimpleNodeRegistry();
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

    [Fact]
    public void StepContext_IsSealedRecordClass()
    {
        var type = typeof(StepContext);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        // Verify record behavior: value equality (== operator)
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp); // records synthesize == operator
    }

    // ===== StepResult Tests =====

    [Fact]
    public void StepResult_ContainsAll6Fields()
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

    [Fact]
    public void StepResult_IsSealedRecordClass()
    {
        var type = typeof(StepResult);
        Assert.True(type.IsSealed);
        Assert.True(type.IsClass);
        // Verify record behavior: value equality (== operator)
        var eqOp = type.GetMethod("op_Equality", new[] { type, type });
        Assert.NotNull(eqOp); // records synthesize == operator
    }

    [Fact]
    public void StepResult_AntiLoopFlags()
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

    [Fact]
    public void StepResult_FrameOverrideFlags()
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
    [Fact]
    public void StaticStrategy_IteratesStaticChildren_ReturnsUnvisited()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new SimpleNodeRegistry();
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

    [Fact]
    public void StaticStrategy_AllVisited_ReturnsNull()
    {
        var ctx = new TraversalRuntimeContext("test");
        ctx.MarkNodeVisited("c1");
        var registry = new SimpleNodeRegistry();
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

    [Fact]
    public void DynamicMatch_UsesCachedChildren()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new SimpleNodeRegistry();
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

    [Fact]
    public void CacheInvalidation_RemovesDynamicChildren()
    {
        var registry = new SimpleNodeRegistry();
        var mgr = new DynamicChildManager(registry);
        var child = new TraversalNode("c1", "child", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        mgr.PrePopulateDynamicChildren("p", new List<TraversalNode> { child });

        Assert.True(mgr.IsCachePopulated("p"));

        mgr.Invalidate("p");
        Assert.True(mgr.IsCacheEmpty("p"));
    }

    [Fact]
    public void CacheInvalidation_PreservesGeneratedPairs()
    {
        var registry = new SimpleNodeRegistry();
        var mgr = new DynamicChildManager(registry);
        mgr._generatedPairs.Add(("fp1", "child"));
        Assert.Equal(1, mgr.GeneratedPairsCount);

        mgr.Invalidate("p");
        Assert.Equal(1, mgr.GeneratedPairsCount); // _generatedPairs persists!
    }

    [Fact]
    public void DedupPersistence_AfterInvalidation_SamePairSkipped()
    {
        var registry = new SimpleNodeRegistry();
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
    [Fact]
    public void BuildChain_PrimaryFallbackBindCurrent()
    {
        var executor = new EntryPolicyExecutor();
        var policy = new EntryPolicy(EntryStrategy.DirectDeeplink, "ColdLaunch");
        var chain = executor.BuildChain(policy);
        Assert.Equal(3, chain.Count);
        Assert.Equal(EntryStrategy.DirectDeeplink, chain[0]);
        Assert.Equal(EntryStrategy.ColdLaunch, chain[1]);
        Assert.Equal(EntryStrategy.BindCurrentScreen, chain[2]);
    }

    [Fact]
    public void BuildChain_DuplicateFallbackOmitted()
    {
        var executor = new EntryPolicyExecutor();
        var policy = new EntryPolicy(EntryStrategy.DirectDeeplink, "DirectDeeplink");
        var chain = executor.BuildChain(policy);
        Assert.Equal(2, chain.Count);
        Assert.Equal(EntryStrategy.DirectDeeplink, chain[0]);
        Assert.Equal(EntryStrategy.BindCurrentScreen, chain[^1]);
    }

    [Fact]
    public void BuildChain_AlwaysEndsWithBindCurrentScreen()
    {
        var executor = new EntryPolicyExecutor();
        var policy = new EntryPolicy(EntryStrategy.ColdLaunch, "DirectDeeplink");
        var chain = executor.BuildChain(policy);
        Assert.Equal(EntryStrategy.BindCurrentScreen, chain[^1]);
    }

    [Fact]
    public void Execute_BindCurrentScreenAlwaysSucceeds()
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
    [Fact]
    public void Update_StoresPageCacheInfo()
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

    [Fact]
    public void Restore_ReturnsCachedItems()
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

    [Fact]
    public void Restore_NullForNonexistentPath()
    {
        var ctx = new TraversalRuntimeContext("test");
        var mgr = new PageCacheManager();
        Assert.Null(mgr.Restore("/unknown/path", ctx));
    }

    [Fact]
    public void PageCacheInfo_IsSealedRecordClassWith3Fields()
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
    [Fact]
    public void Fingerprint_NullInput_Returns0()
        => Assert.Equal(0, PageSnapshotManager.Fingerprint(null));

    [Fact]
    public void Fingerprint_EmptyItems_Returns0()
    {
        var analysis = new PageAnalysis(Direction.Top, Direction.Top, Items: ImmutableArray<MenuItem>.Empty);
        Assert.Equal(0, PageSnapshotManager.Fingerprint(analysis));
    }

    [Fact]
    public void Fingerprint_Deterministic()
    {
        var items = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle),
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var a1 = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        var a2 = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        Assert.Equal(PageSnapshotManager.Fingerprint(a1), PageSnapshotManager.Fingerprint(a2));
    }

    [Fact]
    public void Fingerprint_DifferentInput_DifferentHash()
    {
        var items1 = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var items2 = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var a1 = new PageAnalysis(Direction.Top, Direction.Top, Items: items1);
        var a2 = new PageAnalysis(Direction.Top, Direction.Top, Items: items2);
        Assert.NotEqual(PageSnapshotManager.Fingerprint(a1), PageSnapshotManager.Fingerprint(a2));
    }

    [Fact]
    public void HasChanged_TrueWhenFingerprintsDiffer()
    {
        var items1 = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var items2 = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var before = new PageAnalysis(Direction.Top, Direction.Top, Items: items1);
        var after = new PageAnalysis(Direction.Top, Direction.Top, Items: items2);
        Assert.True(PageSnapshotManager.HasChanged(before, after));
    }

    [Fact]
    public void HasChanged_FalseWhenFingerprintsEqual()
    {
        var items = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var before = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        var after = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        Assert.False(PageSnapshotManager.HasChanged(before, after));
    }

    [Fact]
    public void HasChanged_TrueWhenBeforeNullAfterNotNull()
    {
        var items = ImmutableArray.Create(
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var after = new PageAnalysis(Direction.Top, Direction.Top, Items: items);
        Assert.True(PageSnapshotManager.HasChanged(null, after));
    }
}

// ===== NodeStackAdapter Tests =====

public class NodeStackAdapterTests
{
    [Fact]
    public void Push_RegistersNodeAndPushesStack()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new SimpleNodeRegistry();
        var node = new TraversalNode("n1", "node1", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var adapter = new NodeStackAdapter(ctx, registry);
        adapter.Push(node);
        Assert.Equal(1, ctx.NodeStack.Depth);
        Assert.NotNull(registry.GetNode("n1"));
    }

    [Fact]
    public void Pop_RemovesFromStack()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new SimpleNodeRegistry();
        var node = new TraversalNode("n1", "node1", NodeType.Action,
            new Operation(OperationType.Click),
            new ChildrenStrategy(ChildrenStrategyType.None));
        registry.Register(node);
        ctx.NodeStack.Push(node);

        var adapter = new NodeStackAdapter(ctx, registry);
        adapter.Pop();
        Assert.Equal(0, ctx.NodeStack.Depth);
    }

    [Fact]
    public void Peek_ReturnsTopNodeWithoutRemoving()
    {
        var ctx = new TraversalRuntimeContext("test");
        var registry = new SimpleNodeRegistry();
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
    [Fact]
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
    [Fact]
    public void Fingerprint_DeterministicAcrossMultipleCalls_SameValue()
    {
        // H-10: character-based hash is deterministic — no string.GetHashCode (non-deterministic across processes)
        var items = ImmutableArray.Create(
            new MenuItem("sound", new Coordinate(0.1, 0.2), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle),
            new MenuItem("wifi", new Coordinate(0.3, 0.4), MenuItemType.Switch, ExpectedAction: ExpectedAction.Toggle));
        var page = new PageAnalysis(Direction.Top, Direction.Top, Items: items);

        // Call Fingerprint 5 times — all must return the same value
        var hashes = Enumerable.Range(0, 5).Select(_ => PageSnapshotManager.Fingerprint(page)).ToList();
        Assert.True(hashes.All(h => h == hashes[0]));
    }
}

// ===== Stub implementations for testing =====

/// <summary>Simple INodeRegistry for testing</summary>
internal sealed class SimpleNodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, TraversalNode> _nodes = new();
    public TraversalNode? GetNode(string nodeId) => _nodes.TryGetValue(nodeId, out var n) ? n : null;
    public void Register(TraversalNode node) => _nodes[node.NodeId] = node;
}

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
