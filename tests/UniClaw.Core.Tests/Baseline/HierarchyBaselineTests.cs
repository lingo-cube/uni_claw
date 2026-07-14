using System.Collections.Immutable;
using System.IO;
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
/// 4 层级导航基线测试 — 验证深层 DFS 遍历、多页面滚动状态管理、多层返回导航。
/// 共 4 个场景，涵盖完整遍历、目标搜索、多页面滚动、深层返回。
/// Spec reference: docs/system/layers/simulation-baseline.md §4.1
/// </summary>
[Collection("Baseline Tests")]
public class HierarchyBaselineTests
{
    private readonly BaselineReportCollector _collector;

    /// <summary>
    /// Constructor accepting the collection fixture.
    /// </summary>
    public HierarchyBaselineTests(BaselineTestsFixture fixture)
    {
        _collector = fixture.Collector;
    }

    // ── Shared 4-Level Hierarchy Fixture (load from JSON) ─────────────────────────

    private static readonly StateFixture AdvancedSettingsFixture =
        StateFixture.FromJson(File.ReadAllText(
            Path.Combine("Fixtures", "hierarchy-advanced-settings.json")));

    // ── Shared DynamicMatch Root Node ────────────────────────────────────────────

    /// <summary>
    /// DynamicMatch root node for hierarchy traversal.
    /// Matches buttons and switches from page analysis.
    /// ExitCondition: AllChildrenVisited + AutoEscape.
    /// </summary>
    private static TraversalNode CreateHierarchyRoot() => new TraversalNode(
        NodeId: "root",
        Name: "Advanced Settings",
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
            }),
        ExitCondition: new ExitCondition(
            ExitConditionType.AllChildrenVisited,
            Fallback: FallbackAction.AutoEscape));

    // ── CreateHierarchyEngine Helper ─────────────────────────────────────────

    /// <summary>
    /// Helper: create TraversalEngine with scroll-enabled mock services sharing one SimulatedScreen.
    /// 3 scrollable pages (network_list/app_list/perm_list) each backed by a PagedItemGenerator.
    /// </summary>
    private static TraversalEngine CreateHierarchyEngine(TraversalPlan plan)
    {
        var fixture = AdvancedSettingsFixture;
        var screen = new SimulatedScreen(fixture)
            .WithScrollablePage("network_list", new PagedItemGenerator(totalCount: 25, pageSize: 5, fillRatio: 1.0, namePrefix: "Network_"))
            .WithScrollablePage("app_list", new PagedItemGenerator(totalCount: 30, pageSize: 5, fillRatio: 1.0, namePrefix: "App_"))
            .WithScrollablePage("perm_list", new PagedItemGenerator(totalCount: 20, pageSize: 5, fillRatio: 1.0, namePrefix: "Perm_"));
        var vision = new ScrollableMockVisionService(screen);
        var action = new ScrollableMockActionExecutor(screen);
        return new TraversalEngine(plan, vision, action);
    }

    // ── Expected Behavior Helper ────────────────────────────────────────────

    /// <summary>
    /// Helper: load ExpectedBehavior from JSON for hierarchy scenarios.
    /// </summary>
    private static ExpectedBehavior LoadHierarchyExpectedBehavior(string jsonFileName, StateFixture fixture)
    {
        var basePath = Path.Combine("Baseline", "Fixtures", "expected", "hierarchy", jsonFileName);
        var expected = ExpectedBehavior.FromJson(basePath);
        return expected.WithFixtureDerivation(fixture);
    }

    // ── Scenario 1: Full Traversal (4 levels, all 12 pages) ──────────────

    /// <summary>
    /// 4层级完整遍历 — DFS遍历所有4层级，3个可滚动页面。
    ///
    /// 验证点：
    ///   - 所有12页访问
    ///   - 75+唯一元素访问
    ///   - scroll_count ≥ 15
    ///
    /// ExpectedBehavior: hierarchy-full-traversal.json
    /// Spec reference: simulation-baseline.md §4.1, Scenario 1
    /// </summary>
    [Fact]
    public void Hierarchy_FullTraversal_AllLevelsVisited()
    {
        // Arrange
        var root = CreateHierarchyRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.advanced-settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "4-Level Hierarchy Full Traversal",
            PlanId: "hierarchy-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateHierarchyEngine(plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var fixture = AdvancedSettingsFixture;
        var expected = LoadHierarchyExpectedBehavior("hierarchy-full-traversal.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("hierarchy-full-traversal", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ── Scenario 2: Target Search (Level 3) ────────────────────────────

    /// <summary>
    /// 4层级目标搜索 — 在第3层找到目标元素，提前终止。
    ///
    /// 验证点：
    ///   - 在app_list中找到目标元素
    ///   - 最多8页访问
    ///   - target_found: true
    ///
    /// ExpectedBehavior: hierarchy-target-search.json
    /// Spec reference: simulation-baseline.md §4.1, Scenario 2
    /// </summary>
    [Fact]
    public void Hierarchy_TargetSearchLevel3_StopsAtTarget()
    {
        // Arrange
        var root = CreateHierarchyRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.advanced-settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Hierarchy Target Search - Level 3",
            PlanId: "hierarchy-target-search-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>(),
            CompletionPolicy: new CompletionPolicy(
                Type: CompletionPolicyType.TargetFound,
                TargetName: "App15",  // Target in app_list
                MatchMode: MatchMode.Exact,
                ActionOnFound: TargetFoundAction.MarkAndStop));

        var engine = CreateHierarchyEngine(plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var fixture = AdvancedSettingsFixture;
        var expected = LoadHierarchyExpectedBehavior("hierarchy-target-search.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("hierarchy-target-search", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ── Scenario 3: Multi-Scroll Traversal ───────────────────────────────

    /// <summary>
    /// 多页面滚动遍历 — 访问所有3个可滚动页面。
    ///
    /// 验证点：
    ///   - 3个可滚动页面访问
    ///   - scroll_count ≥ 15
    ///   - 每个页面独立滚动状态
    ///
    /// ExpectedBehavior: hierarchy-multi-scroll.json
    /// Spec reference: simulation-baseline.md §4.1, Scenario 3
    /// </summary>
    [Fact]
    public void Hierarchy_MultiScrollTraversal_AllScrollablePagesVisited()
    {
        // Arrange
        var root = CreateHierarchyRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.advanced-settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Multi-Scroll Traversal",
            PlanId: "hierarchy-multi-scroll-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateHierarchyEngine(plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var fixture = AdvancedSettingsFixture;
        var expected = LoadHierarchyExpectedBehavior("hierarchy-multi-scroll.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("hierarchy-multi-scroll", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ── Scenario 4: Scroll + Deep Back ───────────────────────────────────

    /// <summary>
    /// 滚动后深层返回 — 滚动app_list后3步返回home。
    ///
    /// 验证点：
    ///   - 滚动状态保持
    ///   - 3个back操作成功
    ///   - 成功返回Level 0
    ///
    /// ExpectedBehavior: hierarchy-scroll-deep-back.json
    /// Spec reference: simulation-baseline.md §4.1, Scenario 4
    /// </summary>
    [Fact]
    public void Hierarchy_ScrollThenDeepBack_PreservesState()
    {
        // Arrange
        var root = CreateHierarchyRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.advanced-settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Scroll and Deep Back",
            PlanId: "hierarchy-scroll-deep-back-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateHierarchyEngine(plan);

        // Act
        var result = engine.Run();

        // Assert — ExpectedBehavior-driven verification
        var fixture = AdvancedSettingsFixture;
        var expected = LoadHierarchyExpectedBehavior("hierarchy-scroll-deep-back.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("hierarchy-scroll-deep-back", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }
}
