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

    /// <summary>
    /// Minimal single-page fixture for windowed+jump termination testing.
    /// </summary>
    private static StateFixture JumpListFixture() => new StateFixtureBuilder()
        .Page("jump_list", p => p.Name("Jump List"))
        .Build();

    // ── Scroll Content for Long List Scenarios (PagedItemGenerator configs) ────

    /// <summary>Long list content: 30 items, pageSize 8, dense (fillRatio 1.0).</summary>
    private static IScrollContentSource LongListContent() =>
        new PagedItemGenerator(totalCount: 30, pageSize: 8, fillRatio: 1.0, namePrefix: "Item_");

    /// <summary>Sparse long list content: 25 items, pageSize 8, sparse (fillRatio 0.5).</summary>
    private static IScrollContentSource SparseLongListContent() =>
        new PagedItemGenerator(totalCount: 25, pageSize: 8, fillRatio: 0.5, namePrefix: "SparseItem_");

    /// <summary>Dense long list content: 20 items, pageSize 8, dense (fillRatio 1.0).</summary>
    private static IScrollContentSource DenseLongListContent() =>
        new PagedItemGenerator(totalCount: 20, pageSize: 8, fillRatio: 1.0, namePrefix: "DenseItem_");

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

    /// <summary>Helper: wrap a shared SimulatedScreen into a TraversalEngine.</summary>
    private static TraversalEngine CreateLongListEngine(SimulatedScreen screen, TraversalPlan plan)
    {
        var vision = new ScrollableMockVisionService(screen);
        var action = new ScrollableMockActionExecutor(screen);
        return new TraversalEngine(plan, vision, action);
    }

    // ── Expected Behavior Helper ────────────────────────────────────────────

    /// <summary>
    /// Helper: load ExpectedBehavior from JSON for long list scenarios, expanding auto_derive
    /// from fixture chrome ∪ scroll universe, and auto-deriving Mode from the plan's CompletionPolicy.
    /// </summary>
    private static ExpectedBehavior LoadLongListExpectedBehavior(
        string jsonFileName, StateFixture fixture, SimulatedScreen screen, TraversalPlan plan)
    {
        var basePath = Path.Combine("Baseline", "Fixtures", "expected", "long-list", jsonFileName);
        var expected = ExpectedBehavior.FromJson(basePath);
        return expected.WithDerivation(fixture, screen, plan.CompletionPolicy);
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
    public async Task LongList_FullTraversal_AllItemsVisited()
    {
        // Arrange
        var fixture = LongListFixture();
        var content = LongListContent();
        var root = CreateLongListRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.long-list",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Long List Full Traversal - 30 Items",
            PlanId: "long-list-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var screen = new SimulatedScreen(fixture).WithScrollablePage("long_list", content);
        var engine = CreateLongListEngine(screen, plan);

        // Act
        var result = await engine.RunAsync();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadLongListExpectedBehavior("long-list-full-traversal.json", fixture, screen, plan);
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
    public async Task SparseList_FullTraversal_JumpRecoveryWorks()
    {
        // Arrange
        var fixture = SparseLongListFixture();
        var content = SparseLongListContent();
        var root = CreateLongListRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.sparse-list",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Sparse List Full Traversal - 25 Items",
            PlanId: "sparse-list-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var screen = new SimulatedScreen(fixture).WithScrollablePage("sparse_long_list", content);
        var engine = CreateLongListEngine(screen, plan);

        // Act
        var result = await engine.RunAsync();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadLongListExpectedBehavior("sparse-list-full-traversal.json", fixture, screen, plan);
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
    public async Task DenseList_FullTraversal_AdaptiveStepIncreases()
    {
        // Arrange
        var fixture = DenseLongListFixture();
        var content = DenseLongListContent();
        var root = CreateLongListRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.dense-list",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Dense List Full Traversal - 20 Items",
            PlanId: "dense-list-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var screen = new SimulatedScreen(fixture).WithScrollablePage("dense_long_list", content);
        var engine = CreateLongListEngine(screen, plan);

        // Act
        var result = await engine.RunAsync();

        // Assert — ExpectedBehavior-driven verification
        var expected = LoadLongListExpectedBehavior("dense-list-full-traversal.json", fixture, screen, plan);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("dense-list-full-traversal", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }

    // ── Scenario 4: Windowed + Jump termination (seen-set diff terminates despite jumps) ──

    /// <summary>
    /// 窗口跳跃终止 — Windowed+Jump profile (swipe 过冲跳页, 部分元素永不出现) 下,
    /// 验证 scroll loop 仍能终止 (不无限循环): seen-set 差分到底检测在跳跃下仍成立。
    ///
    /// 验证点:
    ///   - 遍历完成 (all_visited), 不无限循环
    ///   - 到底 (finalProgress = 1.0)
    ///
    /// ExpectedBehavior: long-list-jump-termination.json
    /// </summary>
    [Fact]
    public async Task JumpList_WindowedWithJump_ScrollLoopTerminates()
    {
        // Arrange — windowed + 过冲因子 2.0 (每次 swipe 跳 2 页)
        var fixture = JumpListFixture();
        var content = new PagedItemGenerator(totalCount: 30, pageSize: 8, fillRatio: 1.0, namePrefix: "Jump_");
        var profile = ScrollBehaviorProfile.PagedWithJump(ScrollJump.Overshoot(2.0));
        var root = CreateLongListRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.jump-list",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Windowed+Jump Termination",
            PlanId: "long-list-jump-termination-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var screen = new SimulatedScreen(fixture, profile).WithScrollablePage("jump_list", content);
        var engine = CreateLongListEngine(screen, plan);

        // Act
        var result = await engine.RunAsync();

        // Assert — loop terminated, all_visited, reached bottom
        var expected = LoadLongListExpectedBehavior("long-list-jump-termination.json", fixture, screen, plan);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);
        Assert.NotEqual(TraversalResult.Reasons.MaxSteps, result.CompletionReason); // 未因步数上限而停 (真到底)

        _collector.Add("long-list-jump-termination", expected, result, report,
            executor: engine.ActionExecutor, vision: engine.VisionProvider);
    }
}
