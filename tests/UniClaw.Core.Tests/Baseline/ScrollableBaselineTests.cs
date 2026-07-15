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
/// 滚动基线测试 — 验证 WiFi 列表全屏遍历、回顶、元素去重、边界、稀疏、高重叠场景。
/// 场景内容改用 <see cref="PagedItemGenerator"/> 配置 + 共享 <see cref="SimulatedScreen"/> 驱动。
/// Spec reference: docs/system/layers/simulation-baseline.md §2
/// </summary>
[Collection("Baseline Tests")]
public class ScrollableBaselineTests
{
    private readonly BaselineReportCollector _collector;

    public ScrollableBaselineTests(BaselineTestsFixture fixture)
    {
        _collector = fixture.Collector;
    }

    // ── Fixtures (page shell + chrome; scroll content from PagedItemGenerator) ──

    private static StateFixture WiFiListFixture7Screens() => new StateFixtureBuilder()
        .Page("wifi_list", p => p
            .Name("Wi-Fi Settings")
            .BackButton("BackToSettings", 0.05, 0.05)
            .Switch("WiFi_Switch", "WiFi Switch", 0.90, 0.07))
        .Build();

    private static StateFixture SparseFixture() => new StateFixtureBuilder()
        .Page("sparse_list", p => p
            .Name("Sparse List"))
        .Build();

    private static StateFixture OverlappingFixture() => new StateFixtureBuilder()
        .Page("overlap_list", p => p
            .Name("Overlapping List"))
        .Build();

    // ── Scroll Content (PagedItemGenerator configs) ────────────────────────

    /// <summary>WiFi list content: 24 networks, pageSize 4, dense.</summary>
    private static IScrollContentSource WiFiContent() =>
        new PagedItemGenerator(totalCount: 24, pageSize: 4, fillRatio: 1.0, namePrefix: "Network_");

    /// <summary>Sparse list content: 8 items, pageSize 2, sparse.</summary>
    private static IScrollContentSource SparseJumpContent() =>
        new PagedItemGenerator(totalCount: 8, pageSize: 2, fillRatio: 0.5, namePrefix: "Item_");

    /// <summary>Overlapping list content: 17 items, pageSize 5, dense.</summary>
    private static IScrollContentSource OverlappingAdaptiveContent() =>
        new PagedItemGenerator(totalCount: 17, pageSize: 5, fillRatio: 1.0, namePrefix: "Item_");

    // ── DynamicMatch Root Node ────────────────────────────────────────────

    /// <summary>
    /// DynamicMatch root node for scroll-enabled traversal.
    /// Matches button/switch/back_button elements from page analysis (chrome from fixture,
    /// scroll content from PagedItemGenerator).
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
    /// Helper: create TraversalEngine with scroll-enabled mock services sharing one SimulatedScreen.
    /// </summary>
    private static TraversalEngine CreateScrollableEngine(
        StateFixture fixture, string scrollPageId, IScrollContentSource content, TraversalPlan plan)
    {
        var screen = new SimulatedScreen(fixture).WithScrollablePage(scrollPageId, content);
        var vision = new ScrollableMockVisionService(screen);
        var action = new ScrollableMockActionExecutor(screen);
        return new TraversalEngine(plan, vision, action);
    }

    // ── Expected Behavior Helper ────────────────────────────────────────

    private static ExpectedBehavior LoadScrollExpectedBehavior(string jsonFileName, StateFixture fixture)
    {
        var basePath = Path.Combine("Baseline", "Fixtures", "expected", "scroll", jsonFileName);
        var expected = ExpectedBehavior.FromJson(basePath);
        return expected.WithFixtureDerivation(fixture);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 1: WiFi List Full Traversal (multi-screen scroll)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表全屏遍历 — 多屏滚动遍历所有网络按钮。
    /// 验证点: 所有网络元素访问; 多次向下滚动; 到底终止。
    /// ExpectedBehavior: wifi-list-scroll-all-screens.json
    /// </summary>
    [Fact]
    public async Task WiFiList_ScrollThroughAllScreens_AllNetworksVisited()
    {
        var fixture = WiFiListFixture7Screens();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Full Traversal",
            PlanId: "wifi-list-scroll-all-screens-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, "wifi_list", WiFiContent(), plan);

        var result = await engine.RunAsync();

        var expected = LoadScrollExpectedBehavior("wifi-list-scroll-all-screens.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-scroll-all-screens", expected, result, report,
            executor: engine.ActionExecutor, vision: engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 2: WiFi List Scroll Back to Top
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表向上滚动场景 (向上/回顶终止语义延后, 此处验证列表可正常遍历到底)。
    /// ExpectedBehavior: wifi-list-scroll-back-to-top.json
    /// </summary>
    [Fact]
    public async Task WiFiList_ScrollBackToTop_ProgressRevertsCorrectly()
    {
        var fixture = WiFiListFixture7Screens();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Scroll Back to Top",
            PlanId: "wifi-list-scroll-back-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, "wifi_list", WiFiContent(), plan);

        var result = await engine.RunAsync();

        var expected = LoadScrollExpectedBehavior("wifi-list-scroll-back-to-top.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-scroll-back-to-top", expected, result, report,
            executor: engine.ActionExecutor, vision: engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 3: WiFi List Element Deduplication
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表元素去重 — 累积可见模式下重复元素只访问一次 (seen 集合 + VisitedChildren)。
    /// ExpectedBehavior: wifi-list-element-deduplication.json
    /// </summary>
    [Fact]
    public async Task WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce()
    {
        var fixture = WiFiListFixture7Screens();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Element Dedup",
            PlanId: "wifi-list-dedup-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, "wifi_list", WiFiContent(), plan);

        var result = await engine.RunAsync();

        var expected = LoadScrollExpectedBehavior("wifi-list-element-deduplication.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-element-deduplication", expected, result, report,
            executor: engine.ActionExecutor, vision: engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 4: WiFi List Boundary Conditions
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WiFi列表边界条件 — 初始 progress=0.0, 到底 IsEndOfList=true。
    /// ExpectedBehavior: wifi-list-boundary-conditions.json
    /// </summary>
    [Fact]
    public async Task WiFiList_BoundaryConditions_TopAndBottomCorrect()
    {
        var fixture = WiFiListFixture7Screens();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "WiFi List Boundary Conditions",
            PlanId: "wifi-list-boundary-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, "wifi_list", WiFiContent(), plan);

        var result = await engine.RunAsync();

        var expected = LoadScrollExpectedBehavior("wifi-list-boundary-conditions.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("wifi-list-boundary-conditions", expected, result, report,
            executor: engine.ActionExecutor, vision: engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 5: Sparse List (稀疏, 验证 seen-set 差分终止)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 稀疏列表遍历 — 大间隙稀疏分布, seen-set 差分终止 (跳跃检测管线已删)。
    /// ExpectedBehavior: sparse-list-jump-recovery.json
    /// </summary>
    [Fact]
    public async Task SparseList_JumpRecovery_AllElementsVisited()
    {
        var fixture = SparseFixture();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Sparse List Traversal",
            PlanId: "sparse-list-jump-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, "sparse_list", SparseJumpContent(), plan);

        var result = await engine.RunAsync();

        var expected = LoadScrollExpectedBehavior("sparse-list-jump-recovery.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("sparse-list-jump-recovery", expected, result, report,
            executor: engine.ActionExecutor, vision: engine.VisionProvider);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Scenario 6: Overlapping List (高重叠, 验证遍历到底)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 高重叠列表遍历 — 自适应步长管线已删, 验证 seen-set 差分下仍遍历到底。
    /// ExpectedBehavior: overlapping-list-adaptive-step.json
    /// </summary>
    [Fact]
    public async Task OverlappingList_AdaptiveStep_StepSizeIncreases()
    {
        var fixture = OverlappingFixture();
        var root = CreateScrollDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Overlapping List Traversal",
            PlanId: "overlap-adaptive-step-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateScrollableEngine(fixture, "overlap_list", OverlappingAdaptiveContent(), plan);

        var result = await engine.RunAsync();

        var expected = LoadScrollExpectedBehavior("overlapping-list-adaptive-step.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("overlapping-list-adaptive-step", expected, result, report,
            executor: engine.ActionExecutor, vision: engine.VisionProvider);
    }
}
