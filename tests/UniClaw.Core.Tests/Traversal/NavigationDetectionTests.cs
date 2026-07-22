using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// 导航检测单元测试 — 验证 TryHandleNavigation 行为。
/// </summary>
public class NavigationDetectionTests
{
    private readonly ITestOutputHelper _output;

    public NavigationDetectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 最简两分支导航: hub→listA 然后 hub→listB, 无滚动。
    /// listA/listB 各有 1 个按钮(非 back_button), 验证两边都被访问。
    /// </summary>
    [Fact]
    public async Task TwoBranchNoScroll_BothBranchesVisited()
    {
        // Arrange: hub with to_A, to_B → listA, listB (each with 1 item, no scroll)
        var fixture = new StateFixtureBuilder()
            .Page("hub", p => p
                .Name("Hub")
                .Button("to_A", "Go A", 0.50, 0.30)
                .Button("to_B", "Go B", 0.50, 0.50))
            .Page("listA", p => p
                .Name("List A")
                .Button("item_a", "Item A", 0.50, 0.30))
            .Page("listB", p => p
                .Name("List B")
                .Button("item_b", "Item B", 0.50, 0.30))
            .Transition(t => t.Id("t_a").Click("to_A").From("hub").To("listA"))
            .Transition(t => t.Id("t_b").Click("to_B").From("hub").To("listB"))
            .Build();

        var root = new TraversalNode(
            NodeId: "root",
            Name: "Hub",
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
                }));

        var plan = new TraversalPlan(
            EntryApp: "test",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Test",
            PlanId: "test-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var screen = new SimulatedScreen(fixture);
        var vision = new ScrollableMockVisionService(screen);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var action = new ScrollableMockActionExecutor(screen);
        var engine = new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action);

        // Act
        var result = await engine.RunAsync();

        // Debug
        _output.WriteLine($"TotalSteps: {result.TotalSteps}, Reason: {result.CompletionReason}");
        _output.WriteLine($"Visited: {string.Join(", ", result.VisitedPages)}");
        _output.WriteLine($"Actions: {string.Join(", ", result.ActionHistory.Select(a => a.Action))}");

        // Assert: both branches traversed
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);

        // Both navigation buttons visited
        var visited = result.VisitedPages.ToHashSet();
        bool hasGoA = visited.Any(n => n.Contains("Go A"));
        bool hasGoB = visited.Any(n => n.Contains("Go B"));
        Assert.True(hasGoA, $"Expected 'Go A' visited. Visited: [{string.Join(", ", visited)}]");
        Assert.True(hasGoB, $"Expected 'Go B' visited. Visited: [{string.Join(", ", visited)}]");

        // Action sequence: tap(to_A), back, tap(to_B), back
        var actions = result.ActionHistory.Select(a => a.Action).ToList();
        Assert.Contains("back", actions);
        Assert.True(actions.Count(a => a == "tap") >= 2, $"Expected at least 2 taps, got {actions.Count(a => a == "tap")}");
    }
}
