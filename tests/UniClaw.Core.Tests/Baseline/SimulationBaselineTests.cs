using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Baseline;

/// <summary>
/// Simulation Baseline Tests — 2 core baseline scenarios (C-11 E2E regression guard).
/// Phase B: range-based assertions (≥, Contains, DoesNotContain, >0).
/// Phase C: upgrade to exact values after C# runtime baseline confirmation.
/// Spec reference: docs/system/layers/simulation-baseline.md §1.1 + §1.2
/// </summary>
public class SimulationBaselineTests
{
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
    /// ExitCondition: AllChildrenVisited + AutoEscape (same as Python baseline).
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
            }),
        ExitCondition: new ExitCondition(
            ExitConditionType.AllChildrenVisited,
            Fallback: FallbackAction.AutoEscape));

    // ── CreateEngine Helper ──────────────────────────────

    /// <summary>Helper: create TraversalEngine from fixture + plan</summary>
    private static TraversalEngine CreateEngine(StateFixture fixture, TraversalPlan plan)
    {
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        return new TraversalEngine(plan, vision, action);
    }

    // ── Scenario 1: Full Traversal (§1.1) ──────────────────

    /// <summary>
    /// Settings 全量遍历 — CompletionPolicy=null (natural completion).
    /// Verifies DFS visits all pages and executes actual actions.
    /// Phase B assertions: range-based (≥, Contains, >0).
    /// </summary>
    [Fact]
    public void SettingsApp_FullTraversal_AllVisited()
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
        var result = engine.Run();

        // (1) Success + AllVisited
        Assert.True(result.Success,
            $"Expected Success=true, got CompletionReason={result.CompletionReason}");
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);

        // (2) VisitedPages count ≥ 7 (root + at least 6 menu containers)
        Assert.True(result.VisitedPages.Length >= 7,
            $"Expected VisitedPages.Length >= 7, got {result.VisitedPages.Length}: [{string.Join(", ", result.VisitedPages)}]");

        // (3) Contains root node
        Assert.Contains("root", result.VisitedPages);

        // (4) Contains Wi-Fi subtree (DFS visited Wi-Fi menu container)
        Assert.Contains(result.VisitedPages, p => p.Contains("Wi-Fi"));

        // (5) TotalSteps > 0 (actual execution happened)
        Assert.True(result.TotalSteps > 0, $"Expected TotalSteps > 0, got {result.TotalSteps}");

        // (6) ActionHistory has entries (actions were executed)
        Assert.True(result.ActionHistory.Length > 0,
            $"Expected ActionHistory.Length > 0, got {result.ActionHistory.Length}");
    }

    // ── Scenario 2: Target Search (§1.2) ──────────────────

    /// <summary>
    /// Settings 目标搜索 — CompletionPolicy=TargetFound "Dark mode" Exact MarkAndStop.
    /// Verifies DFS finds target and stops early (MARK_AND_STOP).
    /// Display subtree visited (DFS reached target), Storage subtree NOT visited (early termination proof).
    /// </summary>
    [Fact]
    public void SettingsApp_TargetSearch_StopsAtDarkMode()
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
        var result = engine.Run();

        // (1) Success + TargetFound
        Assert.True(result.Success,
            $"Expected Success=true, got CompletionReason={result.CompletionReason}, Error={result.Error?.Message}");
        Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);

        // (2) Display subtree visited — DFS traversed to Display where target resides
        Assert.Contains(result.VisitedPages, p => p.Contains("Display"));

        // (3) Storage subtree NOT visited — early termination proof (MARK_AND_STOP生效)
        Assert.DoesNotContain(result.VisitedPages, p => p.Contains("Storage"));

        // (4) Target search has fewer steps than full traversal (Phase B: just verify > 0)
        Assert.True(result.TotalSteps > 0, $"Expected TotalSteps > 0, got {result.TotalSteps}");

        // Phase C: add exact comparison with fullTraversal TotalSteps
        // and exact VisitedPages count assertion
    }
}
