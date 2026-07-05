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

    // ── Runner Tests ───────────────────────────────────

    [Fact]
    public void Runner_EmptyNodeTree_CompletesImmediately()
    {
        var fixture = TwoPageFixture();
        var registry = new SimpleNodeRegistry();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));

        var runner = new SimulationRunner(fixture, root, registry);
        var result = runner.Run();

        Assert.True(result.Success);
        Assert.Equal(SimulationResult.Reasons.AllVisited, result.CompletionReason);
        Assert.True(result.TotalSteps <= 5); // root NoAction → complete
    }

    [Fact]
    public void Runner_TwoPage_CompletesAllVisited()
    {
        var fixture = TwoPageFixture();
        var registry = new SimpleNodeRegistry();
        registry.Register(Leaf("btn_settings", ClickAt(0.5, 0.9)));

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings" }));

        var runner = new SimulationRunner(fixture, root, registry);
        var result = runner.Run();

        Assert.True(result.Success);
        Assert.Equal(SimulationResult.Reasons.AllVisited, result.CompletionReason);
        Assert.NotEmpty(result.ActionHistory);
        Assert.Contains("home", result.VisitedPages);
    }

    [Fact]
    public void Runner_MaxStepsExceeded()
    {
        var fixture = TwoPageFixture();
        var registry = new SimpleNodeRegistry();
        registry.Register(Leaf("btn_settings", ClickAt(0.5, 0.9)));
        registry.Register(Leaf("btn_settings2", ClickAt(0.5, 0.9)));
        registry.Register(Leaf("btn_settings3", ClickAt(0.5, 0.9)));

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings", "btn_settings2", "btn_settings3" }));

        var runner = new SimulationRunner(fixture, root, registry,
            new SimulationConfig { MaxSteps = 1 });

        var result = runner.Run();

        Assert.False(result.Success);
        Assert.Equal(SimulationResult.Reasons.MaxSteps, result.CompletionReason);
        Assert.Equal(1, result.TotalSteps);
    }

    [Fact]
    public void Runner_VisitedPages_TracksInOrder()
    {
        var fixture = TwoPageFixture();
        var registry = new SimpleNodeRegistry();
        registry.Register(Leaf("btn_settings", ClickAt(0.5, 0.9)));

        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings" }));

        var runner = new SimulationRunner(fixture, root, registry);
        var result = runner.Run();

        Assert.True(result.Success);
        Assert.True(result.VisitedPages.Length >= 1);
        // First visited page should be the initial page
        Assert.Equal("home", result.VisitedPages[0]);
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
        var registry = new SimpleNodeRegistry();

        // Wi-Fi path + Profile path
        registry.Register(Leaf("btn_settings", ClickAt(0.5, 0.7)));
        registry.Register(Leaf("btn_wifi", ClickAt(0.5, 0.3)));
        registry.Register(Leaf("btn_back_w", new Operation(OperationType.Back)));
        registry.Register(Leaf("btn_back_s", new Operation(OperationType.Back)));
        registry.Register(Leaf("btn_profile", ClickAt(0.5, 0.8)));
        registry.Register(Leaf("btn_back_p", new Operation(OperationType.Back)));
        registry.Register(Leaf("sw_wifi", ClickAt(0.8, 0.3)));

        var root = new TraversalNode("root", "Settings App", NodeType.Screen,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> {
                    "btn_settings", "btn_wifi", "btn_back_w", "btn_back_s",
                    "btn_profile", "btn_back_p", "sw_wifi"
                }));

        var runner = new SimulationRunner(fixture, root, registry);
        var result = runner.Run();

        // 正常完成
        Assert.True(result.Success);
        Assert.Equal(SimulationResult.Reasons.AllVisited, result.CompletionReason);

        // 起始页
        Assert.Equal("home", result.VisitedPages[0]);

        // 核心页面被访问
        Assert.Contains("settings", result.VisitedPages);
        Assert.Contains("wifi", result.VisitedPages);
        Assert.Contains("profile", result.VisitedPages);

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
        var registry = new SimpleNodeRegistry();

        // Only register the Wi-Fi path nodes
        registry.Register(Leaf("btn_settings", ClickAt(0.5, 0.7)));
        registry.Register(Leaf("btn_wifi", ClickAt(0.5, 0.3)));
        registry.Register(Leaf("btn_back_w", new Operation(OperationType.Back)));
        registry.Register(Leaf("btn_back_s", new Operation(OperationType.Back)));

        var root = new TraversalNode("root", "WiFi Path", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> {
                    "btn_settings", "btn_wifi", "btn_back_w", "btn_back_s"
                }));

        var runner = new SimulationRunner(fixture, root, registry);
        var result = runner.Run();

        Assert.True(result.Success);

        // Wi-Fi 路径: home → settings → wifi → settings → home
        Assert.Equal(5, result.VisitedPages.Length);
        Assert.Equal(new[] { "home", "settings", "wifi", "settings", "home" },
            result.VisitedPages);

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
        var nodeRegistry = new SimpleNodeRegistry();

        var emptyNode = new TraversalNode("empty_tap", "Empty", NodeType.LeafAction,
            new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.9, 0.9))),
            new ChildrenStrategy(ChildrenStrategyType.None));
        nodeRegistry.Register(emptyNode);

        var ctx = new TraversalRuntimeContext("e2e-002");
        ctx.NodeStack.Push(emptyNode);
        ctx.CurrentFrame = emptyNode;

        var fsm = new TraversalFSM(ctx);
        var stepCtx = new StepContext(ctx, fsm, vision, action,
            null!, nodeRegistry, null!, null!, null!);

        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(action.GetHistory());
        Assert.False(action.GetHistory()[0].Success);
    }
}
