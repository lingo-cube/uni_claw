using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.Simulation.TraceReplay;

/// <summary>
/// 从真实 run 产物提取的仿真 fixture 工厂。
/// run: 20260805T052309367Z-1bc7a25ea6384e3
/// scenario: enumerate-settings-safely, provider=local, mode=direct
/// outcome: max_steps (120), settings_home_not_restored
///
/// 页面数据来源: assets/{runId}/analysis.jsonl
/// 动作来源: trace/{runId}/run.log
/// 计划来源: plan.json (safe_mode, depth=2, restore=false, 4 DynamicRules)
/// </summary>
internal static class EnumerateFixtures
{
    /// <summary>
    /// DFS 回退重入场景 — 还原 analysis.jsonl row 0→10→16→28→36 的时序:
    ///   Settings (16 items) → Network &amp; internet (21) → Internet (14) → back → Internet again (loop)
    /// </summary>
    public static StateFixture DfsRevisitLoop()
    {
        return new StateFixtureBuilder()
            .Page("settings", p => p
                .Name("Settings")
                .Element("qsearch", e => e.Type("input").Text("QSearch settings").At(0.5, 0.28))
                .Element("network_internet", e => e.Type("menu_item").Text("Network & internet").At(0.38, 0.40))
                .Element("connected_devices", e => e.Type("menu_item").Text("Connected devices").At(0.38, 0.54))
                .Element("bluetooth_pairing", e => e.Type("menu_item").Text("Bluetooth, pairing").At(0.31, 0.58))
                .Element("apps", e => e.Type("menu_item").Text("Apps").At(0.23, 0.68))
                .Element("recent_apps", e => e.Type("menu_item").Text("Recent apps,default apps").At(0.37, 0.72))
                .Element("notifications", e => e.Type("menu_item").Text("Notifications").At(0.32, 0.81))
                .Element("notif_history", e => e.Type("menu_item").Text("Notification history, conversations").At(0.43, 0.85))
                .BackButton("btn_back", 0.05, 0.05))
            .Page("network_internet", p => p
                .Name("Network & internet")
                .Element("ni_internet", e => e.Type("menu_item").Text("Internet").At(0.5, 0.15))
                .Element("ni_sims", e => e.Type("menu_item").Text("SIMs").At(0.5, 0.22))
                .BackButton("ni_back", 0.05, 0.05))
            .Page("internet", p => p
                .Name("Internet")
                .Element("int_wifi", e => e.Type("menu_item").Text("Wi‑Fi").At(0.5, 0.15))
                .Element("int_mobile", e => e.Type("menu_item").Text("T-Mobile").At(0.5, 0.22))
                .Switch("int_switch", "Mobile data", 0.85, 0.28)
                .BackButton("int_back", 0.05, 0.05))
            .Transition(t => t.Id("t_net").Click("network_internet").From("settings").To("network_internet"))
            .Transition(t => t.Id("t_int").Click("ni_internet").From("network_internet").To("internet"))
            .Transition(t => t.Id("t_int2").Click("ni_internet").From("internet").To("internet"))
            .Transition(t => t.Id("t_back").Click("int_back").From("internet").To("network_internet"))
            .Transition(t => t.Id("t_back_ni").Click("ni_back").From("network_internet").To("settings"))
            .Build();
    }

    /// <summary>
    /// 搜索框误标 — 还原搜索框被 YOLO 识别为 menuItem 的场景。
    /// </summary>
    public static StateFixture SearchBoxStuck()
    {
        return new StateFixtureBuilder()
            .Page("settings_search", p => p
                .Name("Settings")
                .Element("qsearch_menuitem", e => e.Type("menu_item").Text("Q Search settings").At(0.5, 0.28))
                .Element("network_internet", e => e.Type("menu_item").Text("Network & internet").At(0.38, 0.40))
                .Element("connected_devices", e => e.Type("menu_item").Text("Connected devices").At(0.38, 0.54))
                .BackButton("btn_back", 0.05, 0.05))
            .Page("search_ui", p => p
                .Name("Search")
                .Element("search_input", e => e.Type("input").Text("Search settings").At(0.5, 0.10))
                .Element("search_result", e => e.Type("menu_item").Text("Wi‑Fi").At(0.5, 0.30))
                .BackButton("search_back", 0.05, 0.05))
            .Transition(t => t.Id("t_search").Click("qsearch_menuitem").From("settings_search").To("search_ui"))
            .Transition(t => t.Id("t_search_stay").Click("search_result").From("search_ui").To("search_ui"))
            .Transition(t => t.Id("t_search_back").Click("search_back").From("search_ui").To("settings_search"))
            .Build();
    }

    /// <summary>
    /// 构建与真实 run 一致的 enumerate 计划 (safe_mode, depth=2).
    /// </summary>
    public static TraversalPlan CreateEnumeratePlan()
    {
        var rootRules = new Dictionary<string, DynamicRule>(StringComparer.Ordinal)
        {
            ["menu_container"] = new(
                "menu_container",
                new MatchCondition(Type: "menu_item", TextMatchMode: TextMatchMode.Contains),
                "menu_container",
                MatchAction.GenerateChild),
            ["switch_leaf"] = new(
                "switch_leaf",
                new MatchCondition(Type: "switch", TextMatchMode: TextMatchMode.Contains),
                "switch_leaf",
                MatchAction.GenerateChild),
            ["leaf_action"] = new(
                "leaf_action",
                new MatchCondition(Type: "button", TextMatchMode: TextMatchMode.Contains),
                "leaf_action",
                MatchAction.GenerateChild),
        };

        var rootNode = new TraversalNode(
            "root", "Settings", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, DynamicRules: rootRules, MaxChildren: 100));

        return new TraversalPlan(
            EntryApp: "com.android.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "com.android.settings_full",
            PlanId: "test-enumerate",
            RootNode: rootNode,
            TemplateRegistry: "safe_mode",
            CompletionPolicy: new CompletionPolicy(
                Type: CompletionPolicyType.Exhaustive,
                MatchMode: MatchMode.Contains,
                ActionOnFound: TargetFoundAction.MarkAndStop));
    }

    // ── helpers ──

    public static TraversalEngine CreateEngine(StateFixture fixture, TraversalPlan plan)
    {
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        return new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action);
    }
}
