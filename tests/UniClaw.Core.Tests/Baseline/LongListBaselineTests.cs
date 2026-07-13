using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.ExpectedBehavior;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Baseline;

/// <summary>
/// 长列表滚动基线测试 — 验证 20-30 项列表的完整遍历、跳跃恢复、自适应步长行为。
/// 共 3 个场景，涵盖 30 项均匀分布、25 项稀疏大间隙、20 项密集高重叠。
/// Spec reference: docs/system/layers/simulation-baseline.md §4.2
/// </summary>
[Collection("Baseline Tests")]
public class LongListBaselineTests
{
    private readonly BaselineReportCollector _collector;

    /// <summary>
    /// Constructor accepting the collection fixture.
    /// </summary>
    public LongListBaselineTests(BaselineTestsFixture fixture)
    {
        _collector = fixture.Collector;
    }

    // ── Shared Fixture: Single Page for Long List Scenarios ──────────────────

    /// <summary>
    /// Minimal single-page fixture for long list scroll testing.
    /// The actual scrollable content comes from ScrollDataStore.
    /// </summary>
    private static StateFixture LongListFixture() => new StateFixtureBuilder()
        .Page("long_list", p => p.Name("Long List"))
        .Build();

    /// <summary>
    /// Minimal single-page fixture for sparse long list testing.
    /// </summary>
    private static StateFixture SparseLongListFixture() => new StateFixtureBuilder()
        .Page("sparse_long_list", p => p.Name("Sparse Long List"))
        .Build();

    /// <summary>
    /// Minimal single-page fixture for dense long list testing.
    /// </summary>
    private static StateFixture DenseLongListFixture() => new StateFixtureBuilder()
        .Page("dense_long_list", p => p.Name("Dense Long List"))
        .Build();

    // ── Scroll Data for Long List Scenarios ────────────────────────────────────

    /// <summary>
    /// Long list scroll data: 30 items, 8 segments, even distribution (15% overlap).
    /// Segments at 0.0, 0.15, 0.30, 0.45, 0.60, 0.75, 0.90, 1.0
    /// </summary>
    private static ScrollDataStore LongListScrollData()
    {
        var builder = ScrollDataStore.CreateBuilder();
        var items = new List<MenuItem>();

        // Create 30 items
        for (int i = 1; i <= 30; i++)
        {
            double y = 0.10 + ((i - 1) % 5) * 0.18;
            items.Add(new MenuItem($"Item{i}", new Coordinate(0.5, y), MenuItemType.Button));
        }

        // Create 8 segments with 4-5 items each, 1 overlap
        int itemIndex = 0;
        int baseItemsPerSegment = 4;
        for (int seg = 0; seg < 8; seg++)
        {
            var threshold = seg switch
            {
                0 => 0.0,
                1 => 0.15,
                2 => 0.30,
                3 => 0.45,
                4 => 0.60,
                5 => 0.75,
                6 => 0.90,
                7 => 1.0,
                _ => seg / 7.0
            };

            var segmentItems = new List<MenuItem>();

            // Add overlap from previous segment
            if (seg > 0 && itemIndex > 0)
            {
                segmentItems.Add(items[itemIndex - 1]);
            }

            // Add current segment items
            int itemsInSegment = baseItemsPerSegment + (seg % 2);  // 4 or 5 items
            for (int i = 0; i < itemsInSegment && itemIndex < items.Count; i++)
            {
                segmentItems.Add(items[itemIndex]);
                itemIndex++;
            }

            builder.Add("long_list", new ScrollSegment(threshold, segmentItems.ToImmutableArray()));
        }

        return builder.Build();
    }

    /// <summary>
    /// Sparse long list scroll data: 25 items, 6 segments, large gaps (40%+).
    /// Segments at 0.0, 0.4, 0.7, 1.0 with large gaps to trigger jump detection.
    /// </summary>
    private static ScrollDataStore SparseLongListScrollData()
    {
        var builder = ScrollDataStore.CreateBuilder();
        var items = new List<MenuItem>();

        // Create 25 items
        for (int i = 1; i <= 25; i++)
        {
            double y = 0.10 + ((i - 1) % 5) * 0.18;
            items.Add(new MenuItem($"SparseItem{i}", new Coordinate(0.5, y), MenuItemType.Button));
        }

        // Create 6 segments with large gaps
        // Segments: 0.0 (3 items), 0.4 (4 items), 0.6 (4 items), 0.7 (4 items), 0.85 (5 items), 1.0 (5 items)
        int itemIndex = 0;
        double[] thresholds = { 0.0, 0.4, 0.6, 0.7, 0.85, 1.0 };
        int[] itemsPerSegment = { 3, 4, 4, 4, 5, 5 };

        for (int seg = 0; seg < 6; seg++)
        {
            var segmentItems = new List<MenuItem>();

            // Add overlap from previous segment
            if (seg > 0 && itemIndex > 0)
            {
                segmentItems.Add(items[itemIndex - 1]);
            }

            // Add current segment items
            for (int i = 0; i < itemsPerSegment[seg] && itemIndex < items.Count; i++)
            {
                segmentItems.Add(items[itemIndex]);
                itemIndex++;
            }

            builder.Add("sparse_long_list", new ScrollSegment(thresholds[seg], segmentItems.ToImmutableArray()));
        }

        return builder.Build();
    }

    /// <summary>
    /// Dense long list scroll data: 20 items, 10 segments, high overlap (80%+).
    /// Segments at 0.0, 0.1, 0.2, ..., 0.9 with high overlap to trigger adaptive step.
    /// </summary>
    private static ScrollDataStore DenseLongListScrollData()
    {
        var builder = ScrollDataStore.CreateBuilder();
        var items = new List<MenuItem>();

        // Create 20 items
        for (int i = 1; i <= 20; i++)
        {
            double y = 0.10 + ((i - 1) % 4) * 0.22;
            items.Add(new MenuItem($"DenseItem{i}", new Coordinate(0.5, y), MenuItemType.Button));
        }

        // Create 10 segments with high overlap
        // Each segment has 5 items, with 4 overlapping from previous segment (80% overlap)
        int itemIndex = 0;
        for (int seg = 0; seg < 10; seg++)
        {
            var threshold = seg / 9.0;
            var segmentItems = new List<MenuItem>();

            // Add 4 overlapping items from previous segment
            if (seg > 0 && itemIndex >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    segmentItems.Add(items[itemIndex - 4 + i]);
                }
            }

            // Add 1 new item
            if (itemIndex < items.Count)
            {
                segmentItems.Add(items[itemIndex]);
                itemIndex++;
            }

            builder.Add("dense_long_list", new ScrollSegment(threshold, segmentItems.ToImmutableArray()));
        }

        return builder.Build();
    }

    // ── Shared DynamicMatch Root Node ────────────────────────────────────────────

    /// <summary>
    /// DynamicMatch root node for long list traversal.
    /// Matches buttons from page analysis (populated by ScrollDataStore).
    /// </summary>
    private static TraversalNode CreateLongListRoot() => new TraversalNode(
        NodeId: "root",
        Name: "Long List",
        NodeType: NodeType.Container,
        Operation: new Operation(OperationType.NoAction),
        ChildrenStrategy: new ChildrenStrategy(
            ChildrenStrategyType.DynamicMatch,
            DynamicRules: new Dictionary<string, DynamicRule>
            {
                ["button_rule"] = new DynamicRule(
                    RuleId: "button_rule",
                    MatchCondition: new MatchCondition(Type: "button"),
                    ChildTemplate: "button_leaf",
                    Action: MatchAction.GenerateChild),
            }),
        ExitCondition: new ExitCondition(
            ExitConditionType.AllChildrenVisited,
            Fallback: FallbackAction.AutoEscape));

    // ── CreateLongListEngine Helper ───────────────────────────────────────────

    /// <summary>
    /// Helper: create TraversalEngine with scroll-enabled mock services.
    /// </summary>
    private static TraversalEngine CreateLongListEngine(StateFixture fixture, ScrollDataStore scrollData, TraversalPlan plan)
    {
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var action = new ScrollableMockActionExecutor(vision);
        return new TraversalEngine(plan, vision, action);
    }

    // ── Expected Behavior Helper ────────────────────────────────────────────

    /// <summary>
    /// Helper: load ExpectedBehavior from JSON for long list scenarios.
    /// </summary>
    private static ExpectedBehavior LoadLongListExpectedBehavior(string jsonFileName, StateFixture fixture)
    {
        var basePath = Path.Combine("Baseline", "Fixtures", "expected", "long-list", jsonFileName);
        var expected = ExpectedBehavior.FromJson(basePath);
        return expected.WithFixtureDerivation(fixture);
    }

    // ── Scenario 1: Long List Full Traversal (30 items) ───────────────────

    /// <summary>
    /// 长列表完整遍历 — 30项列表完整遍历。
    ///
    /// 验证点：
    ///   - 所有30项访问
    ///   - scroll_count ≥ 7
    ///   - final_progress = 1.0
    ///
    /// ExpectedBehavior: long-list-full-traversal.json
    /// Spec reference: simulation-baseline.md §4.2, Scenario 1
    /// </summary>
    [Fact]
    public void LongList_FullTraversal_AllItemsVisited()
    {
        // Arrange
        var fixture = LongListFixture();
        var scrollData = LongListScrollData();
        var root = CreateLongListRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.long-list",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Long List Full Traversal - 30 Items",
            PlanId: "long-list-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateLongListEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadLongListExpectedBehavior("long-list-full-traversal.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("long-list-full-traversal", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ── Scenario 2: Sparse List Full Traversal (25 items, jump recovery) ──

    /// <summary>
    /// 稀疏列表完整遍历 — 25项稀疏列表，大间隙触发跳跃恢复。
    ///
    /// 验证点：
    ///   - 所有25项访问
    ///   - jump_detected ≥ 2
    ///   - jump_recovered ≥ 2
    ///
    /// ExpectedBehavior: sparse-list-full-traversal.json
    /// Spec reference: simulation-baseline.md §4.2, Scenario 2
    /// </summary>
    [Fact]
    public void SparseList_FullTraversal_JumpRecoveryWorks()
    {
        // Arrange
        var fixture = SparseLongListFixture();
        var scrollData = SparseLongListScrollData();
        var root = CreateLongListRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.sparse-list",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Sparse List Full Traversal - 25 Items",
            PlanId: "sparse-list-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateLongListEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadLongListExpectedBehavior("sparse-list-full-traversal.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("sparse-list-full-traversal", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ── Scenario 3: Dense List Full Traversal (20 items, adaptive step) ────

    /// <summary>
    /// 密集列表完整遍历 — 20项密集列表，高重叠触发自适应步长。
    ///
    /// 验证点：
    ///   - 所有20项访问
    ///   - adaptive_step_increases ≥ 3
    ///
    /// ExpectedBehavior: dense-list-full-traversal.json
    /// Spec reference: simulation-baseline.md §4.2, Scenario 3
    /// </summary>
    [Fact]
    public void DenseList_FullTraversal_AdaptiveStepIncreases()
    {
        // Arrange
        var fixture = DenseLongListFixture();
        var scrollData = DenseLongListScrollData();
        var root = CreateLongListRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.dense-list",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Dense List Full Traversal - 20 Items",
            PlanId: "dense-list-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateLongListEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadLongListExpectedBehavior("dense-list-full-traversal.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("dense-list-full-traversal", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }
}
