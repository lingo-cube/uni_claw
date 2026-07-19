using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.ExpectedBehavior;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Baseline;

/// <summary>
/// Simulation Baseline Tests — 2 core baseline scenarios (C-11 E2E regression guard).
/// Phase D: ExpectedBehavior-driven verification (contract-driven, not hardcoded values).
/// Spec reference: docs/system/layers/simulation-baseline.md §1.1 + §1.2
/// </summary>
[Collection("Baseline Tests")]
public class SimulationBaselineTests
{
    private readonly BaselineReportCollector _collector;

    /// <summary>
    /// Constructor accepting the collection fixture.
    /// </summary>
    public SimulationBaselineTests(BaselineTestsFixture fixture)
    {
        _collector = fixture.Collector;
    }

    // ── Shared 7-page Settings App Fixture ──────────────────

    /// <summary>
    /// 7-page Settings App fixture matching Python simulation baseline.
    /// Pages: home, wifi, bluetooth, display, storage, storage_internal, storage_external.
    /// 11 transitions: 4 forward (home→sub), 4 back (sub→home), 2 sub-page forward, 2 sub-page back.
    /// Spec reference: simulation-baseline.md §1.0
    /// </summary>
    private static StateFixture SettingsAppFixture7Pages() => new StateFixtureBuilder()
        .Page("home", p => p
            .Name("Settings")
            .Button("menu_wifi", "Wi-Fi", 0.50, 0.13)
            .Button("menu_bluetooth", "Bluetooth", 0.50, 0.22)
            .Button("menu_display", "Display", 0.50, 0.31)
            .Button("menu_storage", "Storage", 0.50, 0.40)
            .Button("menu_battery", "Battery", 0.50, 0.50)
            .Button("menu_apps", "Apps", 0.50, 0.59))
        .Page("wifi", p => p
            .Name("Wi-Fi")
            .Switch("wifi_switch", "ON", 0.90, 0.07)
            .Button("network_1", "HomeNetwork", 0.50, 0.15)
            .Button("network_2", "OfficeWiFi", 0.50, 0.24)
            .Button("network_3", "GuestNetwork", 0.50, 0.33)
            .BackButton("btn_back_w", 0.05, 0.05))
        .Page("bluetooth", p => p
            .Name("Bluetooth")
            .Switch("bluetooth_switch", "ON", 0.90, 0.07)
            .Button("device_1", "Headphones Pro", 0.50, 0.15)
            .Button("device_2", "Speaker Mini", 0.50, 0.24)
            .BackButton("btn_back_bt", 0.05, 0.05))
        .Page("display", p => p
            .Name("Display")
            .Switch("brightness", "Brightness level", 0.50, 0.13)  // slider mapped as switch in mock
            .Button("wallpaper", "Wallpaper", 0.50, 0.22)
            .Switch("dark_mode", "Dark mode", 0.50, 0.31)
            .BackButton("btn_back_d", 0.05, 0.05))
        .Page("storage", p => p
            .Name("Storage")
            .Button("internal_storage", "Internal Storage", 0.50, 0.14)
            .Button("external_storage", "SD Card", 0.50, 0.25)
            .BackButton("btn_back_s", 0.05, 0.05))
        .Page("storage_internal", p => p
            .Name("Internal Storage")
            .Readonly("apps_usage", "Apps: 25GB", 0.50, 0.12)
            .Readonly("media_usage", "Media: 15GB", 0.50, 0.17)
            .Readonly("system_usage", "System: 5GB", 0.50, 0.22)
            .BackButton("btn_back_si", 0.05, 0.05))
        .Page("storage_external", p => p
            .Name("SD Card")
            .Readonly("photos_usage", "Photos: 1.5GB", 0.50, 0.12)
            .Readonly("videos_usage", "Videos: 500MB", 0.50, 0.17)
            .BackButton("btn_back_se", 0.05, 0.05))
        .Transition(t => t.Id("home_to_wifi").Click("menu_wifi").From("home").To("wifi"))
        .Transition(t => t.Id("home_to_bt").Click("menu_bluetooth").From("home").To("bluetooth"))
        .Transition(t => t.Id("home_to_d").Click("menu_display").From("home").To("display"))
        .Transition(t => t.Id("home_to_s").Click("menu_storage").From("home").To("storage"))
        .Transition(t => t.Id("wifi_back").Click("btn_back_w").From("wifi").To("home"))
        .Transition(t => t.Id("bt_back").Click("btn_back_bt").From("bluetooth").To("home"))
        .Transition(t => t.Id("d_back").Click("btn_back_d").From("display").To("home"))
        .Transition(t => t.Id("s_back").Click("btn_back_s").From("storage").To("home"))
        .Transition(t => t.Id("s_to_si").Click("internal_storage").From("storage").To("storage_internal"))
        .Transition(t => t.Id("s_to_se").Click("external_storage").From("storage").To("storage_external"))
        .Transition(t => t.Id("si_back").Click("btn_back_si").From("storage_internal").To("storage"))
        .Transition(t => t.Id("se_back").Click("btn_back_se").From("storage_external").To("storage"))
        .Build();

    // ── Shared DynamicMatch Root Node ────────────────────

    /// <summary>
    /// DynamicMatch root node shared by both baseline scenarios.
    /// menu_rule matches buttons (MenuItemType.Button) for navigation items.
    /// switch_rule matches switches (MenuItemType.Switch) for toggleable elements.
    /// ContainerHandler determines completion via 5-priority chain; FallbackDecider handles nav-subframe AutoEscape.
    /// Note: C# uses "button"/"switch" (MenuItemType string values), not "menu_item" (Python concept).
    /// </summary>
    private static TraversalNode CreateDynamicMatchRoot() => new TraversalNode(
        NodeId: "root",
        Name: "Settings App",
        NodeType: NodeType.Container,
        Operation: new Operation(OperationType.NoAction),
        ChildrenStrategy: new ChildrenStrategy(
            ChildrenStrategyType.DynamicMatch,
            DynamicRules: new Dictionary<string, DynamicRule>
            {
                ["menu_rule"] = new DynamicRule(
                    RuleId: "menu_rule",
                    MatchCondition: new MatchCondition(Type: "button"),
                    ChildTemplate: "menu_container",
                    Action: MatchAction.GenerateChild),
                ["switch_rule"] = new DynamicRule(
                    RuleId: "switch_rule",
                    MatchCondition: new MatchCondition(Type: "switch"),
                    ChildTemplate: "switch_leaf",
                    Action: MatchAction.GenerateChild),
            }));

    // ── CreateEngine Helper ──────────────────────────────

    /// <summary>Helper: create TraversalEngine from fixture + plan</summary>
    private static TraversalEngine CreateEngine(StateFixture fixture, TraversalPlan plan)
    {
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        return new TraversalEngine(plan, vision, action);
    }

    // ── Expected Behavior Helper ──────────────────────────

    /// <summary>
    /// Helper: load ExpectedBehavior from JSON, expand auto_derive sentinels with fixture,
    /// and auto-derive Mode from the plan's CompletionPolicy (settings scenarios have no scroll screen).
    /// </summary>
    private static ExpectedBehavior LoadExpectedBehavior(string jsonFileName, StateFixture fixture, TraversalPlan plan)
    {
        var basePath = Path.Combine("Baseline", "Fixtures", "expected", jsonFileName);
        var expected = ExpectedBehavior.FromJson(basePath);
        return expected.WithFixtureDerivation(fixture, plan.CompletionPolicy);
    }

    // ── Scenario 1: Full Traversal (§1.1) ──────────────────

    /// <summary>
    /// Settings 全量遍历 — CompletionPolicy=null (natural completion).
    /// Phase D: ExpectedBehavior-driven verification (contract-driven).
    /// ExpectedBehavior: FromJson + WithFixtureDerivation → Verify → Assert.True(report.AllPassed).
    /// </summary>
    [Fact]
    public async Task SettingsApp_FullTraversal_AllVisited()
    {
        var fixture = SettingsAppFixture7Pages();
        var root = CreateDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Safe Full Traversal",
            PlanId: "settings-full-traversal-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

        var engine = CreateEngine(fixture, plan);
        var result = await engine.RunAsync();

        // ExpectedBehavior contract-driven verification
        var expected = LoadExpectedBehavior("settings-full-traversal.json", fixture, plan);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("settings-full-traversal", expected, result, report);
    }

    // ── Scenario 2: Target Search (§1.2) ──────────────────

    /// <summary>
    /// Settings 目标搜索 — CompletionPolicy=TargetFound "Dark mode" Exact MarkAndStop.
    /// Phase D: ExpectedBehavior-driven verification (contract-driven).
    /// page_coverage.required 手写 (Wi-Fi, Bluetooth, Display),
    /// Forbidden=["Storage","Internal Storage","SD Card"] (early termination proof).
    /// </summary>
    [Fact]
    public async Task SettingsApp_TargetSearch_StopsAtDarkMode()
    {
        var fixture = SettingsAppFixture7Pages();
        var root = CreateDynamicMatchRoot();

        var plan = new TraversalPlan(
            EntryApp: "com.example.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "Target Search - Dark Mode",
            PlanId: "settings-target-search-v1",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>(),
            CompletionPolicy: new CompletionPolicy(
                Type: CompletionPolicyType.TargetFound,
                TargetName: "Dark mode",
                MatchMode: MatchMode.Exact,
                ActionOnFound: TargetFoundAction.MarkAndStop));

        var engine = CreateEngine(fixture, plan);
        var result = await engine.RunAsync();

        // ExpectedBehavior contract-driven verification
        var expected = LoadExpectedBehavior("settings-target-search.json", fixture, plan);
        var report = expected.Verify(result);

        Assert.True(report.AllPassed, report.Summary);

        _collector.Add("settings-target-search", expected, result, report);
    }
}
