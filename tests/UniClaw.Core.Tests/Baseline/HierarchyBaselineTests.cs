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

    // ── Shared 4-Level Hierarchy Fixture (12 pages + 3 scrollable) ──────────────────

    /// <summary>
    /// 4-level Advanced Settings fixture for deep hierarchy baseline testing.
    /// Level 0: home (6 menu items)
    /// Level 1: network, apps, privacy, storage (4 pages)
    /// Level 2: wifi, bluetooth, data_usage, installed_apps, running_apps, permissions, location_history (7 pages)
    /// Level 3: network_list, app_list, perm_list (3 scrollable pages via ScrollDataStore)
    /// Total: 12 static pages + 3 scrollable pages
    /// Spec reference: simulation-baseline.md §4.1
    /// </summary>
    private static StateFixture AdvancedSettingsFixture() => new StateFixtureBuilder()
        // Level 0: home
        .Page("home", p => p
            .Name("Advanced Settings")
            .Button("menu_network", "Network", 0.50, 0.13)
            .Button("menu_apps", "Apps", 0.50, 0.22)
            .Button("menu_privacy", "Privacy", 0.50, 0.31)
            .Button("menu_storage", "Storage", 0.50, 0.40)
            .Button("menu_security", "Security", 0.50, 0.50)
            .Button("menu_about", "About", 0.50, 0.59))
        // Level 1: network (→ Level 2: wifi, bluetooth, data_usage)
        .Page("network", p => p
            .Name("Network")
            .Button("menu_wifi", "Wi-Fi", 0.50, 0.15)
            .Button("menu_bluetooth", "Bluetooth", 0.50, 0.24)
            .Button("menu_data_usage", "Data Usage", 0.50, 0.33)
            .BackButton("btn_back_network", 0.05, 0.05))
        // Level 2: wifi (→ Level 3: network_list scrollable)
        .Page("wifi", p => p
            .Name("Wi-Fi")
            .Switch("wifi_switch", "ON", 0.90, 0.07)
            .Button("network_list_btn", "Network List", 0.50, 0.20)
            .BackButton("btn_back_wifi", 0.05, 0.05))
        // Level 2: bluetooth
        .Page("bluetooth", p => p
            .Name("Bluetooth")
            .Switch("bluetooth_switch", "ON", 0.90, 0.07)
            .Button("device_1", "Headphones Pro", 0.50, 0.20)
            .Button("device_2", "Speaker Mini", 0.50, 0.30)
            .BackButton("btn_back_bluetooth", 0.05, 0.05))
        // Level 2: data_usage (→ Level 3: usage_details)
        .Page("data_usage", p => p
            .Name("Data Usage")
            .Button("usage_details", "Usage Details", 0.50, 0.15)
            .BackButton("btn_back_data_usage", 0.05, 0.05))
        // Level 1: apps (→ Level 2: installed_apps, running_apps)
        .Page("apps", p => p
            .Name("Apps")
            .Button("menu_installed", "Installed Apps", 0.50, 0.15)
            .Button("menu_running", "Running Apps", 0.50, 0.24)
            .BackButton("btn_back_apps", 0.05, 0.05))
        // Level 2: installed_apps (→ Level 3: app_list scrollable)
        .Page("installed_apps", p => p
            .Name("Installed Apps")
            .Button("app_list_btn", "App List", 0.50, 0.15)
            .BackButton("btn_back_installed", 0.05, 0.05))
        // Level 2: running_apps
        .Page("running_apps", p => p
            .Name("Running Apps")
            .Button("app_1", "App 1", 0.50, 0.15)
            .Button("app_2", "App 2", 0.50, 0.24)
            .BackButton("btn_back_running", 0.05, 0.05))
        // Level 1: privacy (→ Level 2: permissions, location_history)
        .Page("privacy", p => p
            .Name("Privacy")
            .Button("menu_permissions", "Permissions", 0.50, 0.15)
            .Button("menu_location", "Location History", 0.50, 0.24)
            .BackButton("btn_back_privacy", 0.05, 0.05))
        // Level 2: permissions (→ Level 3: perm_list scrollable)
        .Page("permissions", p => p
            .Name("Permissions")
            .Button("perm_list_btn", "Permission List", 0.50, 0.15)
            .BackButton("btn_back_permissions", 0.05, 0.05))
        // Level 2: location_history (→ Level 3: history_log)
        .Page("location_history", p => p
            .Name("Location History")
            .Button("history_log", "History Log", 0.50, 0.15)
            .BackButton("btn_back_location", 0.05, 0.05))
        // Level 1: storage
        .Page("storage", p => p
            .Name("Storage")
            .Button("internal_storage", "Internal Storage", 0.50, 0.15)
            .Button("external_storage", "SD Card", 0.50, 0.24)
            .BackButton("btn_back_storage", 0.05, 0.05))
        // Level 3: usage_details (static content)
        .Page("usage_details", p => p
            .Name("Usage Details")
            .Readonly("mobile_data", "Mobile: 15GB", 0.50, 0.12)
            .Readonly("wifi_data", "Wi-Fi: 45GB", 0.50, 0.17)
            .Readonly("roaming", "Roaming: 2GB", 0.50, 0.22)
            .Readonly("hotspot", "Hotspot: 5GB", 0.50, 0.27)
            .Readonly("total", "Total: 67GB", 0.50, 0.32)
            .BackButton("btn_back_usage_details", 0.05, 0.05))
        // Level 3: history_log (static content)
        .Page("history_log", p => p
            .Name("History Log")
            .Readonly("log_1", "Location 1 - Today", 0.50, 0.12)
            .Readonly("log_2", "Location 2 - Yesterday", 0.50, 0.17)
            .Readonly("log_3", "Location 3 - 2 days ago", 0.50, 0.22)
            .Readonly("log_4", "Location 4 - Last week", 0.50, 0.27)
            .Readonly("log_5", "Location 5 - Last month", 0.50, 0.32)
            .BackButton("btn_back_history", 0.05, 0.05))
        // Level 3: network_list (scrollable - 25 items)
        .Page("network_list", p => p
            .Name("Network List")
            .BackButton("btn_back_network_list", 0.05, 0.05))
        // Level 3: app_list (scrollable - 30 items)
        .Page("app_list", p => p
            .Name("App List")
            .BackButton("btn_back_app_list", 0.05, 0.05))
        // Level 3: perm_list (scrollable - 20 items)
        .Page("perm_list", p => p
            .Name("Permission List")
            .BackButton("btn_back_perm_list", 0.05, 0.05))
        // Transitions: home → Level 1
        .Transition(t => t.Id("home_to_network").Click("menu_network").From("home").To("network"))
        .Transition(t => t.Id("home_to_apps").Click("menu_apps").From("home").To("apps"))
        .Transition(t => t.Id("home_to_privacy").Click("menu_privacy").From("home").To("privacy"))
        .Transition(t => t.Id("home_to_storage").Click("menu_storage").From("home").To("storage"))
        // Transitions: network → Level 2
        .Transition(t => t.Id("network_to_wifi").Click("menu_wifi").From("network").To("wifi"))
        .Transition(t => t.Id("network_to_bluetooth").Click("menu_bluetooth").From("network").To("bluetooth"))
        .Transition(t => t.Id("network_to_data_usage").Click("menu_data_usage").From("network").To("data_usage"))
        // Transitions: wifi → Level 3
        .Transition(t => t.Id("wifi_to_network_list").Click("network_list_btn").From("wifi").To("network_list"))
        // Transitions: data_usage → Level 3
        .Transition(t => t.Id("data_usage_to_details").Click("usage_details").From("data_usage").To("usage_details"))
        // Transitions: apps → Level 2
        .Transition(t => t.Id("apps_to_installed").Click("menu_installed").From("apps").To("installed_apps"))
        .Transition(t => t.Id("apps_to_running").Click("menu_running").From("apps").To("running_apps"))
        // Transitions: installed_apps → Level 3
        .Transition(t => t.Id("installed_to_app_list").Click("app_list_btn").From("installed_apps").To("app_list"))
        // Transitions: privacy → Level 2
        .Transition(t => t.Id("privacy_to_permissions").Click("menu_permissions").From("privacy").To("permissions"))
        .Transition(t => t.Id("privacy_to_location").Click("menu_location").From("privacy").To("location_history"))
        // Transitions: permissions → Level 3
        .Transition(t => t.Id("permissions_to_perm_list").Click("perm_list_btn").From("permissions").To("perm_list"))
        // Transitions: location_history → Level 3
        .Transition(t => t.Id("location_to_history").Click("history_log").From("location_history").To("history_log"))
        // Transitions: storage → Level 2
        .Transition(t => t.Id("storage_to_internal").Click("internal_storage").From("storage").To("storage"))  // Reuse storage page name
        .Transition(t => t.Id("storage_to_external").Click("external_storage").From("storage").To("storage"))  // Reuse storage page name
        // Transitions: Level 3 → Level 2 (back buttons from scrollable pages)
        .Transition(t => t.Id("network_list_back").Click("btn_back_network_list").From("network_list").To("wifi"))
        .Transition(t => t.Id("app_list_back").Click("btn_back_app_list").From("app_list").To("installed_apps"))
        .Transition(t => t.Id("perm_list_back").Click("btn_back_perm_list").From("perm_list").To("permissions"))
        // Transitions: Level 2 → Level 1 (back buttons)
        .Transition(t => t.Id("wifi_back").Click("btn_back_wifi").From("wifi").To("network"))
        .Transition(t => t.Id("bluetooth_back").Click("btn_back_bluetooth").From("bluetooth").To("network"))
        .Transition(t => t.Id("data_usage_back").Click("btn_back_data_usage").From("data_usage").To("network"))
        .Transition(t => t.Id("usage_details_back").Click("btn_back_usage_details").From("usage_details").To("data_usage"))
        .Transition(t => t.Id("installed_back").Click("btn_back_installed").From("installed_apps").To("apps"))
        .Transition(t => t.Id("running_back").Click("btn_back_running").From("running_apps").To("apps"))
        .Transition(t => t.Id("permissions_back").Click("btn_back_permissions").From("permissions").To("privacy"))
        .Transition(t => t.Id("location_back").Click("btn_back_location").From("location_history").To("privacy"))
        .Transition(t => t.Id("history_log_back").Click("btn_back_history").From("history_log").To("location_history"))
        // Transitions: Level 1 → Level 0 (back buttons)
        .Transition(t => t.Id("network_back").Click("btn_back_network").From("network").To("home"))
        .Transition(t => t.Id("apps_back").Click("btn_back_apps").From("apps").To("home"))
        .Transition(t => t.Id("privacy_back").Click("btn_back_privacy").From("privacy").To("home"))
        .Transition(t => t.Id("storage_back").Click("btn_back_storage").From("storage").To("home"))
        .Build();

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
        var fixture = AdvancedSettingsFixture();
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
        var fixture = AdvancedSettingsFixture();
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
        var fixture = AdvancedSettingsFixture();
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
        var fixture = AdvancedSettingsFixture();
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
        var fixture = AdvancedSettingsFixture();
        var expected = LoadHierarchyExpectedBehavior("hierarchy-scroll-deep-back.json", fixture);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("hierarchy-scroll-deep-back", expected, result, report,
            executor: (ScrollableMockActionExecutor)engine.ActionExecutor,
            vision: (ScrollableMockVisionService)engine.VisionProvider);
    }
}
