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
/// Scroll-Enabled Baseline Tests — 6 scroll scenarios covering full scroll behavior.
/// Uses DynamicMatch strategy (matching existing SimulationBaselineTests pattern)
/// + ScrollableMockVisionService with ScrollDataStore for multi-segment scroll simulation.
/// Spec reference: docs/system/layers/simulation-baseline.md §2
/// </summary>
[Collection("Baseline Tests")]
public class ScrollableBaselineTests
{
    private readonly BaselineReportCollector _collector;

    /// <summary>
    /// Constructor accepting the collection fixture.
    /// </summary>
    public ScrollableBaselineTests(BaselineTestsFixture fixture)
    {
        _collector = fixture.Collector;
    }

    // ── Shared WiFi List Fixture (single scrollable page) ──────────────────

    /// <summary>
    /// Minimal WiFi list fixture for scroll-enabled traversal testing.
    /// Single scrollable page — actual visible elements come from ScrollDataStore.
    /// Fixture provides the page shell; scroll data provides segment-based element visibility.
    /// Spec reference: simulation-baseline.md §2.0
    /// </summary>
    private static StateFixture WiFiListFixture7Screens() => new StateFixtureBuilder()
        .Page("wifi_list", p => p
            .Name("Wi-Fi Settings"))
        .Build();

    /// <summary>
    /// Fixture for sparse jump recovery scenario.
    /// Segments at 0.0, 0.4, 0.7, 1.0 with large gaps to trigger jump detection.
    /// </summary>
    private static StateFixture SparseFixture() => new StateFixtureBuilder()
        .Page("sparse_list", p => p
            .Name("Sparse List"))
        .Build();

    /// <summary>
    /// Fixture for high-overlap adaptive step scenario.
    /// 70%+ overlap between segments to trigger adaptive step growth.
    /// </summary>
    private static StateFixture OverlappingFixture() => new StateFixtureBuilder()
        .Page("overlap_list", p => p
            .Name("Overlapping List"))
        .Build();

    // ── WiFi List Scroll Data (7 screens, 25 elements, overlaps) ────────────────

    /// <summary>
    /// Scroll data for 7-screen WiFi list simulation (25 elements total).
    /// Segment 0.0 (6 elements): BackToSettings, WiFi Switch, Network1-4
    /// Segment 0.2 (4 elements): Network3 (overlap), Network5-7
    /// Segment 0.4 (4 elements): Network6 (overlap), Network8-10
    /// Segment 0.6 (4 elements): Network11-14
    /// Segment 0.8 (4 elements): Network15-18
    /// Segment 1.0 (7 elements): Network19-25
    /// Overlaps: Network3 (0.0/0.2), Network6 (0.2/0.4), Network18 (0.8/1.0)
    /// </summary>
    private static ScrollDataStore WiFiScrollData()
    {
        // Segment 0.0: 6 elements
        var seg0 = ImmutableArray.Create(
            new MenuItem("BackToSettings", new Coordinate(0.05, 0.05), MenuItemType.BackButton),
            new MenuItem("WiFi Switch", new Coordinate(0.90, 0.07), MenuItemType.Switch),
            new MenuItem("Network1", new Coordinate(0.50, 0.15), MenuItemType.Button),
            new MenuItem("Network2", new Coordinate(0.50, 0.24), MenuItemType.Button),
            new MenuItem("Network3", new Coordinate(0.50, 0.33), MenuItemType.Button),
            new MenuItem("Network4", new Coordinate(0.50, 0.42), MenuItemType.Button));

        // Segment 0.2: 4 elements (Network3 overlap)
        var seg02 = ImmutableArray.Create(
            new MenuItem("Network3", new Coordinate(0.50, 0.33), MenuItemType.Button),
            new MenuItem("Network5", new Coordinate(0.50, 0.51), MenuItemType.Button),
            new MenuItem("Network6", new Coordinate(0.50, 0.60), MenuItemType.Button),
            new MenuItem("Network7", new Coordinate(0.50, 0.69), MenuItemType.Button));

        // Segment 0.4: 4 elements (Network6 overlap)
        var seg04 = ImmutableArray.Create(
            new MenuItem("Network6", new Coordinate(0.50, 0.60), MenuItemType.Button),
            new MenuItem("Network8", new Coordinate(0.50, 0.78), MenuItemType.Button),
            new MenuItem("Network9", new Coordinate(0.50, 0.15), MenuItemType.Button),
            new MenuItem("Network10", new Coordinate(0.50, 0.24), MenuItemType.Button));

        // Segment 0.6: 4 elements
        var seg06 = ImmutableArray.Create(
            new MenuItem("Network11", new Coordinate(0.50, 0.33), MenuItemType.Button),
            new MenuItem("Network12", new Coordinate(0.50, 0.42), MenuItemType.Button),
            new MenuItem("Network13", new Coordinate(0.50, 0.51), MenuItemType.Button),
            new MenuItem("Network14", new Coordinate(0.50, 0.60), MenuItemType.Button));

        // Segment 0.8: 4 elements
        var seg08 = ImmutableArray.Create(
            new MenuItem("Network15", new Coordinate(0.50, 0.69), MenuItemType.Button),
            new MenuItem("Network16", new Coordinate(0.50, 0.78), MenuItemType.Button),
            new MenuItem("Network17", new Coordinate(0.50, 0.15), MenuItemType.Button),
            new MenuItem("Network18", new Coordinate(0.50, 0.24), MenuItemType.Button));

        // Segment 1.0: 7 elements (Network18 overlap)
        var seg10 = ImmutableArray.Create(
            new MenuItem("Network18", new Coordinate(0.50, 0.24), MenuItemType.Button),
            new MenuItem("Network19", new Coordinate(0.50, 0.33), MenuItemType.Button),
            new MenuItem("Network20", new Coordinate(0.50, 0.42), MenuItemType.Button),
            new MenuItem("Network21", new Coordinate(0.50, 0.51), MenuItemType.Button),
            new MenuItem("Network22", new Coordinate(0.50, 0.60), MenuItemType.Button),
            new MenuItem("Network23", new Coordinate(0.50, 0.69), MenuItemType.Button),
            new MenuItem("Network24", new Coordinate(0.50, 0.78), MenuItemType.Button));

        return ScrollDataStore.CreateBuilder()
            .Add("wifi_list",
                new ScrollSegment(0.0, seg0),
                new ScrollSegment(0.2, seg02),
                new ScrollSegment(0.4, seg04),
                new ScrollSegment(0.6, seg06),
                new ScrollSegment(0.8, seg08),
                new ScrollSegment(1.0, seg10))
            .Build();
    }

    // ── Sparse Jump Scroll Data (4 segments, gaps > 30%) ──────────────────

    /// <summary>
    /// Sparse scroll data to trigger jump detection.
    /// Segments at 0.0 (2 elements), 0.4 (2 elements), 0.7 (2 elements), 1.0 (2 elements).
    /// Gaps: 0.0→0.4 (40%), 0.4→0.7 (30% boundary), 0.7→1.0 (30% boundary).
    /// Default step = 30% → gap from 0.0 to 0.4 is 40% > 30% → jump detected.
    /// </summary>
    private static ScrollDataStore SparseJumpData()
    {
        var seg0 = ImmutableArray.Create(
            new MenuItem("Item1", new Coordinate(0.50, 0.15), MenuItemType.Button),
            new MenuItem("Item2", new Coordinate(0.50, 0.24), MenuItemType.Button));

        var seg04 = ImmutableArray.Create(
            new MenuItem("Item3", new Coordinate(0.50, 0.15), MenuItemType.Button),
            new MenuItem("Item4", new Coordinate(0.50, 0.24), MenuItemType.Button));

        var seg07 = ImmutableArray.Create(
            new MenuItem("Item5", new Coordinate(0.50, 0.15), MenuItemType.Button),
            new MenuItem("Item6", new Coordinate(0.50, 0.24), MenuItemType.Button));

        var seg10 = ImmutableArray.Create(
            new MenuItem("Item7", new Coordinate(0.50, 0.15), MenuItemType.Button),
            new MenuItem("Item8", new Coordinate(0.50, 0.24), MenuItemType.Button));

        return ScrollDataStore.CreateBuilder()
            .Add("sparse_list",
                new ScrollSegment(0.0, seg0),
                new ScrollSegment(0.4, seg04),
                new ScrollSegment(0.7, seg07),
                new ScrollSegment(1.0, seg10))
            .Build();
    }

    // ── Overlapping Adaptive Scroll Data (70%+ overlap) ────────────────────

    /// <summary>
    /// High-overlap scroll data to trigger adaptive step growth.
    /// 5 segments with 15 total unique elements, 70%+ overlap between adjacent segments.
    /// </summary>
    private static ScrollDataStore OverlappingAdaptiveData()
    {
        var seg0 = ImmutableArray.Create(
            new MenuItem("Item1", new Coordinate(0.50, 0.10), MenuItemType.Button),
            new MenuItem("Item2", new Coordinate(0.50, 0.20), MenuItemType.Button),
            new MenuItem("Item3", new Coordinate(0.50, 0.30), MenuItemType.Button),
            new MenuItem("Item4", new Coordinate(0.50, 0.40), MenuItemType.Button),
            new MenuItem("Item5", new Coordinate(0.50, 0.50), MenuItemType.Button));

        var seg025 = ImmutableArray.Create(
            new MenuItem("Item4", new Coordinate(0.50, 0.40), MenuItemType.Button),
            new MenuItem("Item5", new Coordinate(0.50, 0.50), MenuItemType.Button),
            new MenuItem("Item6", new Coordinate(0.50, 0.60), MenuItemType.Button),
            new MenuItem("Item7", new Coordinate(0.50, 0.70), MenuItemType.Button),
            new MenuItem("Item8", new Coordinate(0.50, 0.80), MenuItemType.Button));

        var seg050 = ImmutableArray.Create(
            new MenuItem("Item7", new Coordinate(0.50, 0.70), MenuItemType.Button),
            new MenuItem("Item8", new Coordinate(0.50, 0.80), MenuItemType.Button),
            new MenuItem("Item9", new Coordinate(0.50, 0.10), MenuItemType.Button),
            new MenuItem("Item10", new Coordinate(0.50, 0.20), MenuItemType.Button),
            new MenuItem("Item11", new Coordinate(0.50, 0.30), MenuItemType.Button));

        var seg075 = ImmutableArray.Create(
            new MenuItem("Item10", new Coordinate(0.50, 0.20), MenuItemType.Button),
            new MenuItem("Item11", new Coordinate(0.50, 0.30), MenuItemType.Button),
            new MenuItem("Item12", new Coordinate(0.50, 0.40), MenuItemType.Button),
            new MenuItem("Item13", new Coordinate(0.50, 0.50), MenuItemType.Button),
            new MenuItem("Item14", new Coordinate(0.50, 0.60), MenuItemType.Button));

        var seg10 = ImmutableArray.Create(
            new MenuItem("Item13", new Coordinate(0.50, 0.50), MenuItemType.Button),
            new MenuItem("Item14", new Coordinate(0.50, 0.60), MenuItemType.Button),
            new MenuItem("Item15", new Coordinate(0.50, 0.70), MenuItemType.Button),
            new MenuItem("Item16", new Coordinate(0.50, 0.80), MenuItemType.Button),
            new MenuItem("Item17", new Coordinate(0.50, 0.10), MenuItemType.Button));

        return ScrollDataStore.CreateBuilder()
            .Add("overlap_list",
                new ScrollSegment(0.0, seg0),
                new ScrollSegment(0.25, seg025),
                new ScrollSegment(0.50, seg050),
                new ScrollSegment(0.75, seg075),
                new ScrollSegment(1.0, seg10))
            .Build();
    }

    // ── DynamicMatch Root Node ────────────────────────────────────────────

    /// <summary>
    /// DynamicMatch root node for scroll-enabled traversal.
    /// Matches button and switch elements from page analysis (populated by ScrollDataStore).
    /// Same pattern as SimulationBaselineTests.CreateDynamicMatchRoot().
    /// ExitCondition: AllChildrenVisited + AutoEscape.
    /// </summary>
    private static TraversalNode CreateScrollDynamicMatchRoot() => new TraversalNode(
        NodeId: "root",
        Name: "Scroll List Traversal",
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
                ["switch_rule"] = new DynamicRule(
                    RuleId: "switch_rule",
                    MatchCondition: new MatchCondition(Type: "switch"),
                    ChildTemplate: "switch_leaf",
                    Action: MatchAction.GenerateChild),
                ["back_button_rule"] = new DynamicRule(
                    RuleId: "back_button_rule",
                    MatchCondition: new MatchCondition(Type: "back_button"),
                    ChildTemplate: "back_button_leaf",
                    Action: MatchAction.GenerateChild),
            }),
        ExitCondition: new ExitCondition(
            ExitConditionType.AllChildrenVisited,
            Fallback: FallbackAction.AutoEscape));

    // ── CreateScrollableEngine Helper ────────────────────────────────────

    /// <summary>
    /// Helper: create TraversalEngine with scroll-enabled mock services.
    /// Uses ScrollableMockVisionService and ScrollableMockActionExecutor.
    /// </summary>
    private static TraversalEngine CreateScrollableEngine(
        StateFixture fixture,
        ScrollDataStore scrollData,
        TraversalPlan plan)
    {
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        var action = new ScrollableMockActionExecutor(vision);
        return new TraversalEngine(plan, vision, action);
    }

    // ── Expected Behavior Helper ────────────────────────────────────────

    /// <summary>
    /// Helper: load ExpectedBehavior from JSON for scroll scenarios.
    /// </summary>
    private static ExpectedBehavior LoadScrollExpectedBehavior(string jsonFileName, StateFixture fixture)
    {
        var basePath = Path.Combine("Baseline", "Fixtures", "expected", "scroll", jsonFileName);
        var expected = ExpectedBehavior.FromJson(basePath);
        return expected.WithFixtureDerivation(fixture);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 1: WiFi List Full Traversal (7-screen scroll)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表全屏遍历 — 7屏滚动遍历所有网络按钮。
    ///
    /// 验证点：
    ///   - 所有24个网络元素访问（包含Network3/6/18重叠去重）
    ///   - 多次向下滚动操作
    ///   - 最终进度 = 1.0（到底）
    ///
    /// ExpectedBehavior: wifi-list-scroll-all-screens.json
    /// Spec reference: simulation-baseline.md §2.1
    /// </summary>
    [Fact]
    public void WiFiList_ScrollThroughAllScreens_AllNetworksVisited()
    {
        // Arrange
        var fixture = WiFiListFixture7Screens();
        var scrollData = WiFiScrollData();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Full Traversal - 7 Screens",
            PlanId: "wifi-list-scroll-all-screens-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadScrollExpectedBehavior("wifi-list-scroll-all-screens.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-scroll-all-screens", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 2: WiFi List Scroll Back to Top
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表向上滚动 — 点击BackToSettings后触发向上滚动。
    ///
    /// 验证点：
    ///   - 向上滚动发生（scrollUpCount >= 1）
    ///   - BackToSettings 元素已访问
    ///
    /// ExpectedBehavior: wifi-list-scroll-back-to-top.json
    /// Spec reference: simulation-baseline.md §2.2
    /// </summary>
    [Fact]
    public void WiFiList_ScrollBackToTop_ProgressRevertsCorrectly()
    {
        // Arrange
        var fixture = WiFiListFixture7Screens();
        var scrollData = WiFiScrollData();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Scroll Back to Top",
            PlanId: "wifi-list-scroll-back-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadScrollExpectedBehavior("wifi-list-scroll-back-to-top.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-scroll-back-to-top", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 3: WiFi List Element Deduplication
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表元素去重 — 验证重叠元素只访问一次。
    ///
    /// 验证点：
    ///   - Network3 只出现一次 (出现在 segment 0.0 和 0.2)
    ///   - Network6 只出现一次 (出现在 segment 0.2 和 0.4)
    ///   - Network18 只出现一次 (出现在 segment 0.8 和 1.0)
    ///
    /// ExpectedBehavior: wifi-list-element-deduplication.json
    /// Spec reference: simulation-baseline.md §2.3
    /// </summary>
    [Fact]
    public void WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce()
    {
        // Arrange
        var fixture = WiFiListFixture7Screens();
        var scrollData = WiFiScrollData();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Element Dedup",
            PlanId: "wifi-list-dedup-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadScrollExpectedBehavior("wifi-list-element-deduplication.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-element-deduplication", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 4: WiFi List Boundary Conditions
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表边界条件 — 验证 progress 0.0/1.0 的边界行为。
    ///
    /// 验证点：
    ///   - 初始 progress = 0.0
    ///   - 最终 IsEndOfList = true
    ///
    /// ExpectedBehavior: wifi-list-boundary-conditions.json
    /// Spec reference: simulation-baseline.md §2.4
    /// </summary>
    [Fact]
    public void WiFiList_BoundaryConditions_TopAndBottomCorrect()
    {
        // Arrange
        var fixture = WiFiListFixture7Screens();
        var scrollData = WiFiScrollData();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Boundary Conditions",
            PlanId: "wifi-list-boundary-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadScrollExpectedBehavior("wifi-list-boundary-conditions.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-boundary-conditions", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 5: Sparse List Jump Recovery
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 稀疏列表跳跃恢复 — 大间隙触发跳跃检测和恢复。
    ///
    /// 验证点：
    ///   - 跳跃检测触发 (jumpDetected >= 1)
    ///   - 恢复成功 (jumpRecovered >= 1)
    ///   - 所有 8 个元素已访问
    ///
    /// ExpectedBehavior: sparse-list-jump-recovery.json
    /// Spec reference: simulation-baseline.md §2.5
    /// </summary>
    [Fact]
    public void SparseList_JumpRecovery_AllElementsVisited()
    {
        // Arrange
        var fixture = SparseFixture();
        var scrollData = SparseJumpData();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Sparse List Jump Recovery",
            PlanId: "sparse-list-jump-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadScrollExpectedBehavior("sparse-list-jump-recovery.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("sparse-list-jump-recovery", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 6: Overlapping List Adaptive Step
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 高重叠列表自适应步长 — 70%+ 重叠触发步长增长。
    ///
    /// 验证点：
    ///   - 自适应步长增长触发 (adaptiveStepIncreases >= 1)
    ///   - 所有 17 个元素已访问
    ///
    /// ExpectedBehavior: overlapping-list-adaptive-step.json
    /// Spec reference: simulation-baseline.md §2.6
    /// </summary>
    [Fact]
    public void OverlappingList_AdaptiveStep_StepSizeIncreases()
    {
        // Arrange
        var fixture = OverlappingFixture();
        var scrollData = OverlappingAdaptiveData();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Overlapping List Adaptive Step",
            PlanId: "overlap-adaptive-step-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, scrollData, plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadScrollExpectedBehavior("overlapping-list-adaptive-step.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("overlapping-list-adaptive-step", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }
}
