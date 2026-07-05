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
    private static StateFixture LoadTwoPageFixture()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "two-page-app.json");
        // Fallback: try relative path
        if (!File.Exists(jsonPath))
            jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "two-page-app.json");
        if (!File.Exists(jsonPath))
            jsonPath = Path.Combine("..", "..", "..", "Fixtures", "two-page-app.json");
        return StateFixture.FromJson(File.ReadAllText(jsonPath));
    }

    /// <summary>
    /// Helper: builds a StepContext with StatefulMock* services.
    /// </summary>
    private static StepContext BuildStepContext(
        TraversalRuntimeContext ctx,
        TraversalFSM fsm,
        StatefulMockVisionService vision,
        StatefulMockActionExecutor action,
        SimpleNodeRegistry nodeRegistry)
    {
        return new StepContext(
            Context: ctx,
            StateMachine: fsm,
            Vision: vision,
            Action: action,
            ChildMgr: new DynamicChildManager(),
            NodeRegistry: nodeRegistry,
            Trace: new TraceCoordinator(),
            SnapshotMgr: new PageSnapshotManager(),
            Stack: new NodeStackAdapter(ctx, nodeRegistry)
        );
    }

    [Fact]
    public void TwoPageLinearTraversal_HomeToSettingsAndBack()
    {
        var fixture = LoadTwoPageFixture();
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var nodeRegistry = new SimpleNodeRegistry();

        // Build traversal nodes
        var settingsNode = new TraversalNode("btn_settings", "Settings", NodeType.LeafAction,
            new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.9))),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var backNode = new TraversalNode("btn_back", "Back", NodeType.LeafAction,
            new Operation(OperationType.Back),
            new ChildrenStrategy(ChildrenStrategyType.None));

        nodeRegistry.Register(settingsNode);
        nodeRegistry.Register(backNode);

        var rootNode = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings" }));

        var ctx = new TraversalRuntimeContext("e2e-001");
        ctx.NodeStack.Push(rootNode);
        ctx.CurrentFrame = rootNode;

        var fsm = new TraversalFSM(ctx);
        var stepCtx = BuildStepContext(ctx, fsm, vision, action, nodeRegistry);

        // Step through: NodeSelect → PreconditionCheck → Execute → ResultVerify → Branch
        // NodeSelect (stack not empty → PreconditionCheck)
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        // PreconditionCheck → Execute
        fsm.TransitionTo(TraversalState.Execute);

        // Execute root node (NoAction) → skip
        var result = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);

        // Push the child node for settings
        ctx.NodeStack.Push(settingsNode);
        ctx.CurrentFrame = settingsNode;
        ctx.AddVisitedChild("root", "btn_settings");

        // Drive to Execute for settings node
        // ResultVerify → Branch → NodeSelect → PreconditionCheck → Execute
        fsm.TransitionTo(TraversalState.Branch);
        fsm.TransitionTo(TraversalState.NodeSelect);
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        // Execute settings click
        result = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal("settings", vision.CurrentPageId); // 页面已切换到 settings
        Assert.Single(action.GetHistory());
        Assert.Equal("tap", action.GetHistory()[0].Action);

        // Press back
        ctx.NodeStack.Push(backNode);
        ctx.CurrentFrame = backNode;
        // ResultVerify → Branch → NodeSelect → PreconditionCheck → Execute
        fsm.TransitionTo(TraversalState.Branch);
        fsm.TransitionTo(TraversalState.NodeSelect);
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        result = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal("home", vision.CurrentPageId); // 已返回 home
        Assert.Equal(2, action.GetHistory().Count);
        Assert.Equal("back", action.GetHistory()[1].Action);
    }

    /// <summary>
    /// Helper: drives FSM from current state to Execute, then calls Step(ctx).
    /// Valid path: ResultVerify → Branch → NodeSelect → PreconditionCheck → Execute
    /// </summary>
    private static TraversalState DriveToExecuteAndStep(TraversalFSM fsm, StepContext stepCtx)
    {
        // If already in Execute, just step
        if (fsm.CurrentState == TraversalState.Execute)
            return fsm.Step(stepCtx);

        // Drive from ResultVerify to Execute
        if (fsm.CurrentState == TraversalState.ResultVerify)
        {
            fsm.TransitionTo(TraversalState.Branch);
            fsm.TransitionTo(TraversalState.NodeSelect);
        }
        if (fsm.CurrentState == TraversalState.NodeSelect)
            fsm.TransitionTo(TraversalState.PreconditionCheck);
        if (fsm.CurrentState == TraversalState.PreconditionCheck)
            fsm.TransitionTo(TraversalState.Execute);

        return fsm.Step(stepCtx);
    }

    [Fact]
    public void ThreeLevelTraversal_HomeToWifiAndBack()
    {
        // Fixture: home → settings → wifi-settings
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p
                .Name("HomeScreen")
                .Button("btn_settings", "Settings", 0.5, 0.9))
            .Page("settings", p => p
                .Name("SettingsScreen")
                .Button("btn_wifi", "Wi-Fi Settings", 0.5, 0.5)
                .BackButton("btn_back_s", 0.05, 0.05))
            .Page("wifi", p => p
                .Name("WiFiScreen")
                .Switch("sw_enable", "Enable Wi-Fi", 0.8, 0.3)
                .BackButton("btn_back_w", 0.05, 0.05))
            .Transition(t => t.Id("t1").Click("btn_settings").From("home").To("settings"))
            .Transition(t => t.Id("t2").Click("btn_wifi").From("settings").To("wifi"))
            .Transition(t => t.Id("t3").Click("btn_back_s").From("settings").To("home"))
            .Transition(t => t.Id("t4").Click("btn_back_w").From("wifi").To("settings"))
            .Build();

        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var nodeRegistry = new SimpleNodeRegistry();

        // Build nodes
        var settingsNode = new TraversalNode("btn_settings", "Settings", NodeType.LeafAction,
            new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.9))),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var wifiNode = new TraversalNode("btn_wifi", "Wi-Fi", NodeType.LeafAction,
            new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5))),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var backSettingsNode = new TraversalNode("btn_back_s", "Back", NodeType.LeafAction,
            new Operation(OperationType.Back),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var backWifiNode = new TraversalNode("btn_back_w", "Back", NodeType.LeafAction,
            new Operation(OperationType.Back),
            new ChildrenStrategy(ChildrenStrategyType.None));

        nodeRegistry.Register(settingsNode);
        nodeRegistry.Register(wifiNode);
        nodeRegistry.Register(backSettingsNode);
        nodeRegistry.Register(backWifiNode);

        var rootNode = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: new List<string> { "btn_settings" }));

        var ctx = new TraversalRuntimeContext("e2e-003");
        ctx.NodeStack.Push(rootNode);
        ctx.CurrentFrame = rootNode;

        var fsm = new TraversalFSM(ctx);
        var stepCtx = BuildStepContext(ctx, fsm, vision, action, nodeRegistry);
        // NodeSelect → PreconditionCheck → Execute
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        // === Level 1: Execute root (NoAction) ===
        var result = DriveToExecuteAndStep(fsm, stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);

        // === Level 2: Click Settings → go to settings page ===
        ctx.NodeStack.Push(settingsNode);
        ctx.CurrentFrame = settingsNode;
        ctx.AddVisitedChild("root", "btn_settings");

        result = DriveToExecuteAndStep(fsm, stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal("settings", vision.CurrentPageId);
        Assert.Equal(1, vision.NavigationDepth);

        // === Level 3: Click Wi-Fi → go to wifi page ===
        ctx.NodeStack.Push(wifiNode);
        ctx.CurrentFrame = wifiNode;
        ctx.AddVisitedChild("settings", "btn_wifi");

        result = DriveToExecuteAndStep(fsm, stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal("wifi", vision.CurrentPageId);
        Assert.Equal(2, vision.NavigationDepth); // home → settings → wifi

        // === Back: wifi → settings ===
        ctx.NodeStack.Push(backWifiNode);
        ctx.CurrentFrame = backWifiNode;

        result = DriveToExecuteAndStep(fsm, stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal("settings", vision.CurrentPageId); // NavigateBack: wifi → settings
        Assert.Equal(1, vision.NavigationDepth);

        // === Back: settings → home ===
        ctx.NodeStack.Push(backSettingsNode);
        ctx.CurrentFrame = backSettingsNode;

        result = DriveToExecuteAndStep(fsm, stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Equal("home", vision.CurrentPageId); // NavigateBack: settings → home
        Assert.Equal(0, vision.NavigationDepth);

        // Verify action history
        Assert.Equal(4, action.GetHistory().Count);
        Assert.Equal("tap", action.GetHistory()[0].Action);  // click Settings
        Assert.Equal("tap", action.GetHistory()[1].Action);  // click Wi-Fi
        Assert.Equal("back", action.GetHistory()[2].Action); // back from wifi
        Assert.Equal("back", action.GetHistory()[3].Action); // back from settings
    }

    [Fact]
    public void EmptyAreaTap_ReturnsResultVerify()
    {
        var fixture = LoadTwoPageFixture();
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var nodeRegistry = new SimpleNodeRegistry();

        // Node targeting empty area (no element at 0.9, 0.9)
        var emptyNode = new TraversalNode("empty_tap", "Empty", NodeType.LeafAction,
            new Operation(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(0.9, 0.9))),
            new ChildrenStrategy(ChildrenStrategyType.None));
        nodeRegistry.Register(emptyNode);

        var ctx = new TraversalRuntimeContext("e2e-002");
        ctx.NodeStack.Push(emptyNode);
        ctx.CurrentFrame = emptyNode;

        var fsm = new TraversalFSM(ctx);
        var stepCtx = BuildStepContext(ctx, fsm, vision, action, nodeRegistry);

        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);

        var result = fsm.Step(stepCtx);

        // TapAsync returns false (no element) → ResultVerify, NOT ErrorHandling
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(action.GetHistory());
        Assert.False(action.GetHistory()[0].Success);
    }
}
