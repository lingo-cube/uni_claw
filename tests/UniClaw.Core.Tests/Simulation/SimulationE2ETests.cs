using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Simulation;

public class SimulationE2ETests
{
    // ── Helpers ────────────────────────────────────────

    private static TraversalNode Leaf(string id, Operation op)
        => new(id, id, NodeType.LeafAction, op, new ChildrenStrategy(ChildrenStrategyType.None));

    private static Operation ClickAt(double x, double y)
        => new(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(x, y)));

    private static StateFixture TwoPageFixture() => new StateFixtureBuilder()
        .Page("home", p => p
            .Name("HomeScreen")
            .Button("btn_settings", "Settings", 0.5, 0.9))
        .Page("settings", p => p
            .Name("SettingsScreen")
            .BackButton("btn_back", 0.05, 0.05))
        .Transition(t => t.Id("go").Click("btn_settings").From("home").To("settings"))
        .Transition(t => t.Id("back").Click("btn_back").From("settings").To("home"))
        .Build();

    /// <summary>Helper: create TraversalEngine from fixture + root + child nodes</summary>
    private static TraversalEngine CreateEngine(
        StateFixture fixture,
        TraversalNode root,
        Dictionary<string, TraversalNode> nodes,
        TraversalEngineConfig? config = null)
    {
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);

        var plan = new TraversalPlan(
            EntryApp: "test_app",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan",
            PlanId: "test-001",
            RootNode: root,
            StaticNodes: nodes);

        return new TraversalEngine(plan, vision, action, config);
    }

    // ── TraversalEngine Tests ──────────────────────────

    [Fact]
    public void Runner_EmptyNodeTree_CompletesImmediately()
    {
        var fixture = TwoPageFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));

        var engine = CreateEngine(fixture, root, new Dictionary<string, TraversalNode>());
        var result = engine.Run();

        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
        Assert.True(result.TotalSteps <= 5); // root NoAction → complete
    }

    [Fact]
    public void Runner_TwoPage_CompletesAllVisited()
    {
        var fixture = TwoPageFixture();
        var nodes = new Dictionary<string, TraversalNode>
        {
            ["btn_settings"] = Leaf("btn_settings", ClickAt(0.5, 0.9)),
        };

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings" }));

        var engine = CreateEngine(fixture, root, nodes);
        var result = engine.Run();

        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
        Assert.NotEmpty(result.ActionHistory);
        Assert.NotEmpty(result.VisitedPages);  // Node IDs visited during traversal
    }

    [Fact]
    public void Runner_MaxStepsExceeded()
    {
        var fixture = TwoPageFixture();
        var nodes = new Dictionary<string, TraversalNode>
        {
            ["btn_settings"] = Leaf("btn_settings", ClickAt(0.5, 0.9)),
            ["btn_settings2"] = Leaf("btn_settings2", ClickAt(0.5, 0.9)),
            ["btn_settings3"] = Leaf("btn_settings3", ClickAt(0.5, 0.9)),
        };

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings", "btn_settings2", "btn_settings3" }));

        var engine = CreateEngine(fixture, root, nodes,
            new TraversalEngineConfig { MaxSteps = 1 });
        var result = engine.Run();

        Assert.False(result.Success);
        Assert.Equal(TraversalResult.Reasons.MaxSteps, result.CompletionReason);
        Assert.Equal(1, result.TotalSteps);
    }

    [Fact]
    public void Runner_VisitedPages_TracksInOrder()
    {
        var fixture = TwoPageFixture();
        var nodes = new Dictionary<string, TraversalNode>
        {
            ["btn_settings"] = Leaf("btn_settings", ClickAt(0.5, 0.9)),
        };

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings" }));

        var engine = CreateEngine(fixture, root, nodes);
        var result = engine.Run();

        Assert.True(result.Success);
        Assert.True(result.VisitedPages.Length >= 1);
        // First visited page should be root node
        Assert.Equal("root", result.VisitedPages[0]);
    }

    // ── Complete Simulation Example ─────────────────────

    /// <summary>
    /// 4 页面的模拟设置应用：
    ///   home ──click Settings──▶ settings ──click Wi-Fi──▶ wifi ──back──▶ settings ──back──▶ home
    ///                             settings ──click Profile──▶ profile ──back──▶ home
    ///
    /// 页面元素类型：button, switch, back_button, readonly
    /// </summary>
    private static StateFixture SettingsAppFixture() => new StateFixtureBuilder()
        .Page("home", p => p
            .Name("HomeScreen")
            .Button("btn_settings", "Settings", 0.5, 0.7)
            .Button("btn_profile", "Profile", 0.5, 0.8))
        .Page("settings", p => p
            .Name("SettingsScreen")
            .Button("btn_wifi", "Wi-Fi", 0.5, 0.3)
            .Button("btn_bluetooth", "Bluetooth", 0.5, 0.4)
            .Button("btn_display", "Display", 0.5, 0.5)
            .BackButton("btn_back_s", 0.05, 0.05))
        .Page("wifi", p => p
            .Name("WiFiScreen")
            .Switch("sw_wifi", "Enable Wi-Fi", 0.8, 0.3)
            .Readonly("txt_network", "Current: HomeWiFi", 0.5, 0.5)
            .BackButton("btn_back_w", 0.05, 0.05))
        .Page("profile", p => p
            .Name("ProfileScreen")
            .Readonly("txt_name", "User: Alice", 0.5, 0.3)
            .Readonly("txt_email", "alice@example.com", 0.5, 0.4)
            .BackButton("btn_back_p", 0.05, 0.05))
        .Transition(t => t.Id("t1").Click("btn_settings").From("home").To("settings"))
        .Transition(t => t.Id("t2").Click("btn_profile").From("home").To("profile"))
        .Transition(t => t.Id("t3").Click("btn_wifi").From("settings").To("wifi"))
        .Transition(t => t.Id("t4").Click("btn_back_s").From("settings").To("home"))
        .Transition(t => t.Id("t5").Click("btn_back_w").From("wifi").To("settings"))
        .Transition(t => t.Id("t6").Click("btn_back_p").From("profile").To("home"))
        .Build();

    [Fact]
    public void SettingsApp_CompleteTraversal_AllPathsVisited()
    {
        var fixture = SettingsAppFixture();
        var nodes = new Dictionary<string, TraversalNode>
        {
            ["btn_settings"] = Leaf("btn_settings", ClickAt(0.5, 0.7)),
            ["btn_wifi"] = Leaf("btn_wifi", ClickAt(0.5, 0.3)),
            ["btn_back_w"] = Leaf("btn_back_w", new Operation(OperationType.Back)),
            ["btn_back_s"] = Leaf("btn_back_s", new Operation(OperationType.Back)),
            ["btn_profile"] = Leaf("btn_profile", ClickAt(0.5, 0.8)),
            ["btn_back_p"] = Leaf("btn_back_p", new Operation(OperationType.Back)),
            ["sw_wifi"] = Leaf("sw_wifi", ClickAt(0.8, 0.3)),
        };

        var root = new TraversalNode("root", "Settings App", NodeType.Screen,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> {
                    "btn_settings", "btn_wifi", "btn_back_w", "btn_back_s",
                    "btn_profile", "btn_back_p", "sw_wifi"
                }));

        var engine = CreateEngine(fixture, root, nodes);
        var result = engine.Run();

        // 正常完成
        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);

        // Visited pages (node IDs) include root and child nodes
        Assert.Contains("root", result.VisitedPages);

        // 核心节点被访问
        Assert.Contains("btn_settings", result.VisitedPages);
        Assert.Contains("btn_wifi", result.VisitedPages);

        // 多种操作类型
        var actions = result.ActionHistory;
        Assert.Contains(actions, a => a.Action == "tap" && a.Success);
        Assert.Contains(actions, a => a.Action == "back" && a.Success);
        Assert.True(actions.Length >= 5);
    }

    [Fact]
    public void SettingsApp_WiFiPath_VisitsCorrectPages()
    {
        var fixture = SettingsAppFixture();
        var nodes = new Dictionary<string, TraversalNode>
        {
            ["btn_settings"] = Leaf("btn_settings", ClickAt(0.5, 0.7)),
            ["btn_wifi"] = Leaf("btn_wifi", ClickAt(0.5, 0.3)),
            ["btn_back_w"] = Leaf("btn_back_w", new Operation(OperationType.Back)),
            ["btn_back_s"] = Leaf("btn_back_s", new Operation(OperationType.Back)),
        };

        var root = new TraversalNode("root", "WiFi Path", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> {
                    "btn_settings", "btn_wifi", "btn_back_w", "btn_back_s"
                }));

        var engine = CreateEngine(fixture, root, nodes);
        var result = engine.Run();

        Assert.True(result.Success);

        // Wi-Fi 路径 visited nodes (node IDs, not mock page IDs)
        Assert.Contains("root", result.VisitedPages);
        Assert.Contains("btn_settings", result.VisitedPages);
        Assert.Contains("btn_wifi", result.VisitedPages);

        // 2 次 click + 2 次 back
        Assert.Equal(4, result.ActionHistory.Length);
        Assert.Equal("tap", result.ActionHistory[0].Action);   // click Settings
        Assert.Equal("tap", result.ActionHistory[1].Action);   // click Wi-Fi
        Assert.Equal("back", result.ActionHistory[2].Action);  // back from wifi
        Assert.Equal("back", result.ActionHistory[3].Action);  // back from settings
    }

    // ── Manual Handler Tests (kept for fine-grained handler verification) ──

    [Fact]
    public void EmptyAreaTap_ReturnsResultVerify()
    {
        var fixture = TwoPageFixture();
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);

        var emptyNode = new TraversalNode("empty_tap", "Empty", NodeType.LeafAction,
            new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.9, 0.9))),
            new ChildrenStrategy(ChildrenStrategyType.None));

        var nodes = new Dictionary<string, TraversalNode>
        {
            ["empty_tap"] = emptyNode,
        };

        var ctx = new TraversalRuntimeContext("e2e-002");
        ctx.NodeStack.Push(emptyNode);
        ctx.SetCurrentFrame(emptyNode);

        var fsm = new TraversalFSM(ctx);
        var registry = new DictionaryNodeRegistry();
        registry.Register(emptyNode);
        var stepCtx = new StepContext(ctx, fsm, vision, action,
            null!, registry, null!, null!, null!);

        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(action.GetHistory());
        Assert.False(action.GetHistory()[0].Success);
    }
}
