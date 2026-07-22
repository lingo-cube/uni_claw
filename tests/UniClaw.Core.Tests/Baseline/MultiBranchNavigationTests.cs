using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Baseline;

/// <summary>
/// 多分支导航覆盖测试 — 验证 DynamicMatch 父节点的所有兄弟导航分支都被遍历。
///
/// Bug: 当 hub 页有两个导航按钮 (to_A→listA, to_B→listB) 时,
/// 引擎只走第一个分支, 第二个分支的元素访问量为 0,
/// 却仍上报 CompletionReason=AllVisited。
///
/// Spec reference: openspec/changes/navigation-subpage-frames/specs/scroll-aware-traversal/spec.md
/// </summary>
[Collection("Baseline Tests")]
public class MultiBranchNavigationTests
{
    private readonly BaselineReportCollector _collector;

    public MultiBranchNavigationTests(BaselineTestsFixture fixture)
    {
        _collector = fixture.Collector;
    }

    // ── Fixture: Hub with two navigation branches (listA + listB, both scrollable) ──

    /// <summary>
    /// Hub 页含 to_A (→listA 可滚动 16 项) 和 to_B (→listB 可滚动 16 项)。
    /// listA/listB 无 back_button — 导航进入后无法返回 hub,
    /// 复现 "listB 0/16 但 CompletionReason=AllVisited" 的 bug。
    /// </summary>
    private static StateFixture HubTwoBranchFixture() => new StateFixtureBuilder()
        .Page("hub", p => p
            .Name("Hub")
            .Button("to_A", "Go to List A", 0.50, 0.30)
            .Button("to_B", "Go to List B", 0.50, 0.50))
        .Page("listA", p => p
            .Name("List A"))
        .Page("listB", p => p
            .Name("List B"))
        .Transition(t => t.Id("hub_to_listA").Click("to_A").From("hub").To("listA"))
        .Transition(t => t.Id("hub_to_listB").Click("to_B").From("hub").To("listB"))
        .Build();

    // ── Fixture: Deep navigation chain (root→page1→page2, each with scrollable content) ──

    /// <summary>
    /// root→page1→page2 深层导航链, 每层有可滚动内容。
    /// 用于验证 PressBack 逐层还原 + 深层兄弟覆盖。
    /// </summary>
    private static StateFixture DeepNavChainFixture() => new StateFixtureBuilder()
        .Page("root_page", p => p
            .Name("Root")
            .Button("to_page1", "Go to Page 1", 0.50, 0.40))
        .Page("page1", p => p
            .Name("Page 1")
            .Button("to_page2", "Go to Page 2", 0.50, 0.40)
            .BackButton("back_from_page1", 0.05, 0.05))
        .Page("page2", p => p
            .Name("Page 2")
            .BackButton("back_from_page2", 0.05, 0.05))
        .Transition(t => t.Id("root_to_page1").Click("to_page1").From("root_page").To("page1"))
        .Transition(t => t.Id("page1_to_page2").Click("to_page2").From("page1").To("page2"))
        .Transition(t => t.Id("page2_back").Click("back_from_page2").From("page2").To("page1"))
        .Transition(t => t.Id("page1_back").Click("back_from_page1").From("page1").To("root_page"))
        .Build();

    // ── Fixture: Non-scrollable control (hub→listA2/listB2, static items) ──

    /// <summary>
    /// 非滚动控制组: hub→listA2/listB2 各有 3 个静态 readonly 项。
    /// 无 back_button, 用于隔离 "是否与滚动无关"。
    /// </summary>
    private static StateFixture NonScrollableControlFixture() => new StateFixtureBuilder()
        .Page("hub_ns", p => p
            .Name("Hub NS")
            .Button("to_A2", "Go to List A2", 0.50, 0.30)
            .Button("to_B2", "Go to List B2", 0.50, 0.50))
        .Page("listA2", p => p
            .Name("List A2")
            .Readonly("A2_item_0", "Item A0", 0.50, 0.20)
            .Readonly("A2_item_1", "Item A1", 0.50, 0.35)
            .Readonly("A2_item_2", "Item A2", 0.50, 0.50))
        .Page("listB2", p => p
            .Name("List B2")
            .Readonly("B2_item_0", "Item B0", 0.50, 0.20)
            .Readonly("B2_item_1", "Item B1", 0.50, 0.35)
            .Readonly("B2_item_2", "Item B2", 0.50, 0.50))
        .Transition(t => t.Id("hub_ns_to_listA2").Click("to_A2").From("hub_ns").To("listA2"))
        .Transition(t => t.Id("hub_ns_to_listB2").Click("to_B2").From("hub_ns").To("listB2"))
        .Build();

    // ── DynamicMatch Root Node ────────────────────────────────────────────

    private static TraversalNode CreateDynamicMatchRoot(string nodeId = "root") => new TraversalNode(
        NodeId: nodeId,
        Name: "Hub Traversal",
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
                ["readonly_rule"] = new DynamicRule(
                    RuleId: "readonly_rule",
                    MatchCondition: new MatchCondition(Type: "readonly"),
                    ChildTemplate: "readonly_leaf",
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
            }));

    // ── Helper: Create engine ─────────────────────────────────────────────

    private static TraversalEngine CreateEngine(TraversalPlan plan, StateFixture fixture,
        params (string pageId, IScrollContentSource source)[] scrollablePages)
    {
        var screen = new SimulatedScreen(fixture);
        foreach (var (pageId, source) in scrollablePages)
            screen.WithScrollablePage(pageId, source);
        var vision = new ScrollableMockVisionService(screen);
        var action = new ScrollableMockActionExecutor(screen);
        return new TraversalEngine(plan, vision, vision, action);
    }

    // ── Scenario 1: Two-Branch Coverage (TDD: currently FAILS) ────────────

    /// <summary>
    /// 两分支导航覆盖: hub→listA + hub→listB, 两个可滚动列表。
    ///
    /// 当前行为 (BUG): listA 16/16, listB 0/16, CompletionReason=AllVisited (谎言)。
    /// 期望行为: listA 16/16, listB 16/16, CompletionReason=AllVisited (真实)。
    ///
    /// TDD: 此测试当前 FAIL — 确认缺口存在后再修代码。
    /// </summary>
    [Fact]
    public async Task TwoBranch_BothListsVisited()
    {
        // Arrange
        var root = CreateDynamicMatchRoot();
        var plan = new TraversalPlan(
            EntryApp: "com.example.hub",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Hub Two-Branch Traversal",
            PlanId: "hub-two-branch-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var fixture = HubTwoBranchFixture();
        var engine = CreateEngine(plan, fixture,
            ("listA", new PagedItemGenerator(totalCount: 16, pageSize: 4, fillRatio: 1.0, namePrefix: "A_")),
            ("listB", new PagedItemGenerator(totalCount: 16, pageSize: 4, fillRatio: 1.0, namePrefix: "B_")));

        // Act
        var result = await engine.RunAsync();

        // Assert: BOTH branches traversed
        // CompletionReason should be AllVisited
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);

        // Both navigation buttons should be in visited pages
        var visited = result.VisitedPages.ToHashSet();
        bool toAVisited = visited.Any(n => n.Contains("Go to List A"));
        bool toBVisited = visited.Any(n => n.Contains("Go to List B"));
        Assert.True(toAVisited, $"Expected to_A visited. Visited: [{string.Join(", ", visited)}]");
        Assert.True(toBVisited, $"Expected to_B visited. Visited: [{string.Join(", ", visited)}]");

        // Action sequence should show taps on both branches + PressBack between them
        var actions = result.ActionHistory.Select(a => a.Action).ToList();
        bool hasBack = actions.Contains("back");
        Assert.True(hasBack, $"Expected PressBack action. Actions: [{string.Join(", ", actions)}]");

        // Total steps should be reasonable (not maxed out)
        Assert.True(result.TotalSteps < 500, $"TotalSteps={result.TotalSteps} should be < 500");
    }

    // ── Scenario 2: Deep Navigation Chain ─────────────────────────────────

    /// <summary>
    /// 深层导航链: root→page1→page2, 每层有可滚动内容。
    ///
    /// 当前行为 (BUG): 只走第一个分支, 深层页可能丢失。
    /// 期望行为: page1 和 page2 的内容都被访问, PressBack 逐层还原。
    ///
    /// TDD: 此测试当前 FAIL。
    /// </summary>
    [Fact]
    public async Task DeepNavigation_AllLevelsVisited()
    {
        // Arrange
        var root = CreateDynamicMatchRoot();
        var plan = new TraversalPlan(
            EntryApp: "com.example.deepnav",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Deep Navigation Chain",
            PlanId: "deep-nav-chain-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var fixture = DeepNavChainFixture();
        var engine = CreateEngine(plan, fixture,
            ("page1", new PagedItemGenerator(totalCount: 8, pageSize: 4, fillRatio: 1.0, namePrefix: "P1_")),
            ("page2", new PagedItemGenerator(totalCount: 8, pageSize: 4, fillRatio: 1.0, namePrefix: "P2_")));

        // Act
        var result = await engine.RunAsync();

        // Assert: all levels reached
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);

        // Navigation buttons at each level should be visited
        var visited = result.VisitedPages.ToHashSet();
        bool page1BtnVisited = visited.Any(n => n.Contains("Go to Page 1"));
        bool page2BtnVisited = visited.Any(n => n.Contains("Go to Page 2"));
        Assert.True(page1BtnVisited, $"Expected 'Go to Page 1' visited. Visited: [{string.Join(", ", visited)}]");
        Assert.True(page2BtnVisited, $"Expected 'Go to Page 2' visited. Visited: [{string.Join(", ", visited)}]");

        // PressBack actions should be recorded
        bool hasPressBack = result.ActionHistory.Any(a =>
            a.Action == "back" && a.Success);
        Assert.True(hasPressBack, "Expected PressBack actions between pages");
    }

    // ── Scenario 3: Non-Scrollable Control ────────────────────────────────

    /// <summary>
    /// 非滚动控制组: hub→listA2/listB2, 各 3 个静态 readonly 项, 无滚动。
    ///
    /// 验证 bug 与滚动无关 — 即使没有滚动, 第二个分支也不被访问。
    ///
    /// TDD: 此测试当前 FAIL。
    /// </summary>
    [Fact]
    public async Task NonScrollableControl_BothBranchesVisited()
    {
        // Arrange
        var root = CreateDynamicMatchRoot();
        var plan = new TraversalPlan(
            EntryApp: "com.example.hubns",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Non-Scrollable Control",
            PlanId: "hub-nonscroll-control-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var fixture = NonScrollableControlFixture();
        var engine = CreateEngine(plan, fixture);

        // Act
        var result = await engine.RunAsync();

        // Assert: both branches traversed
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);

        var visited = result.VisitedPages.ToHashSet();
        bool toA2Visited = visited.Any(n => n.Contains("Go to List A2"));
        bool toB2Visited = visited.Any(n => n.Contains("Go to List B2"));
        Assert.True(toA2Visited, $"Expected to_A2 visited. Visited: [{string.Join(", ", visited)}]");
        Assert.True(toB2Visited, $"Expected to_B2 visited. Visited: [{string.Join(", ", visited)}]");
    }
}
