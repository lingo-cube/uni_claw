using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Core.Tests.Simulation.TraceReplay;

/// <summary>
/// Android Settings enumerate 场景的永久仿真回归测试。
/// 基于 API 35 emulator (uniclaw-lite-api35) 真实 Settings 页面结构。
///
/// 场景: enumerate-settings-safely, safe_mode plan, maxDepth=2.
/// 真实 bug: DFS DynamicMatch 子帧生成不受 maxDepth 约束 → 深度失控.
/// 修复后: 引擎停在 depth=2 不进入子页面.
/// </summary>
public class SettingsEnumerateRegression
{
    private readonly ITestOutputHelper _output;
    public SettingsEnumerateRegression(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// 模拟 API 35 Settings 的 4 层嵌套结构:
    ///   Settings → Network & internet → Internet → Wi‑Fi → Advanced
    ///   预期 maxDepth=2 时只遍历到 Internet, 不进入 Wi‑Fi.
    /// </summary>
    private static StateFixture Api35Settings()
    {
        return new StateFixtureBuilder()
            // ── Settings 主页 ──
            .Page("settings", p => p
                .Name("Settings")
                .Element("qsearch", e => e.Type("input").Text("QSearch settings").At(0.5, 0.28))
                .Element("network", e => e.Type("menu_item").Text("Network & internet").At(0.38, 0.40))
                .Element("connected", e => e.Type("menu_item").Text("Connected devices").At(0.38, 0.54))
                .Element("apps", e => e.Type("menu_item").Text("Apps").At(0.23, 0.68))
                .Element("notifications", e => e.Type("menu_item").Text("Notifications").At(0.32, 0.81))
                .BackButton("s_back", 0.05, 0.05))
            // ── Network & internet (depth=1) ──
            .Page("network", p => p
                .Name("Network & internet")
                .Element("internet", e => e.Type("menu_item").Text("Internet").At(0.5, 0.15))
                .Element("sims", e => e.Type("menu_item").Text("SIMs").At(0.5, 0.22))
                .Element("airplane", e => e.Type("menu_item").Text("Airplane mode").At(0.5, 0.29))
                .Element("hotspot", e => e.Type("menu_item").Text("Hotspot & tethering").At(0.5, 0.36))
                .Element("data_saver", e => e.Type("menu_item").Text("Data Saver").At(0.5, 0.43))
                .Element("vpn", e => e.Type("menu_item").Text("VPN").At(0.5, 0.50))
                .BackButton("n_back", 0.05, 0.05))
            // ── Internet (depth=2) ──
            .Page("internet", p => p
                .Name("Internet")
                .Element("wifi", e => e.Type("menu_item").Text("Wi‑Fi").At(0.5, 0.15))
                .Element("tmobile", e => e.Type("menu_item").Text("T-Mobile").At(0.5, 0.22))
                .Switch("mobile_data", "Mobile data", 0.85, 0.28)
                .BackButton("i_back", 0.05, 0.05))
            // ── Wi‑Fi (depth=3, 不应到达) ──
            .Page("wifi", p => p
                .Name("Wi‑Fi")
                .Element("advanced", e => e.Type("menu_item").Text("Advanced").At(0.5, 0.3))
                .BackButton("w_back", 0.05, 0.05))
            // ── 子页面 (每个都有独立 back) ──
            .Page("sims", p => p.Name("SIMs").BackButton("sims_back", 0.05, 0.05))
            .Page("airplane", p => p.Name("Airplane mode").BackButton("ap_back", 0.05, 0.05))
            .Page("hotspot", p => p.Name("Hotspot & tethering").BackButton("hs_back", 0.05, 0.05))
            .Page("data_saver", p => p.Name("Data Saver").BackButton("ds_back", 0.05, 0.05))
            .Page("vpn", p => p.Name("VPN").BackButton("vpn_back", 0.05, 0.05))
            // ── 跳转 ──
            .Transition(t => t.Id("t1").Click("network").From("settings").To("network"))
            .Transition(t => t.Id("t2").Click("internet").From("network").To("internet"))
            .Transition(t => t.Id("t3").Click("wifi").From("internet").To("wifi"))
            .Transition(t => t.Id("t4").Click("advanced").From("wifi").To("wifi"))
            .Transition(t => t.Id("ts").Click("sims").From("network").To("sims"))
            .Transition(t => t.Id("ta").Click("airplane").From("network").To("airplane"))
            .Transition(t => t.Id("th").Click("hotspot").From("network").To("hotspot"))
            .Transition(t => t.Id("td").Click("data_saver").From("network").To("data_saver"))
            .Transition(t => t.Id("tv").Click("vpn").From("network").To("vpn"))
            // back transitions
            .Transition(t => t.Id("b1").Click("s_back").From("settings").To("settings"))
            .Transition(t => t.Id("b2").Click("n_back").From("network").To("settings"))
            .Transition(t => t.Id("b3").Click("i_back").From("internet").To("network"))
            .Transition(t => t.Id("b4").Click("w_back").From("wifi").To("internet"))
            .Transition(t => t.Id("bs").Click("sims_back").From("sims").To("network"))
            .Transition(t => t.Id("ba").Click("ap_back").From("airplane").To("network"))
            .Transition(t => t.Id("bh").Click("hs_back").From("hotspot").To("network"))
            .Transition(t => t.Id("bd").Click("ds_back").From("data_saver").To("network"))
            .Transition(t => t.Id("bv").Click("vpn_back").From("vpn").To("network"))
            .Build();
    }

    [Fact(DisplayName = "Settings enumerate: maxDepth=2 → 不进入 Wi‑Fi (depth=3)")]
    public async Task Enumerate_StopsAtDepth2()
    {
        var fixture = Api35Settings();
        var rules = new Dictionary<string, DynamicRule>(StringComparer.Ordinal)
        {
            ["menu_container"] = new("menu_container",
                new MatchCondition(Type: "menu_item", TextMatchMode: TextMatchMode.Contains),
                "menu_container", MatchAction.GenerateChild),
        };
        var root = new TraversalNode("root", "Settings", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, DynamicRules: rules, MaxChildren: 100));
        var plan = new TraversalPlan(
            EntryApp: "com.android.settings",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            RootNode: root,
            TemplateRegistry: "safe_mode",
            CompletionPolicy: new CompletionPolicy(
                Type: CompletionPolicyType.Exhaustive,
                MatchMode: MatchMode.Contains,
                ActionOnFound: TargetFoundAction.MarkAndStop),
            IntentSlots: new IntentSlots("com.android.settings", "full", Depth: 2));

        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var engine = new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action);

        var result = await engine.RunAsync(CancellationToken.None);

        // 诊断
        _output.WriteLine($"Completion: {result.CompletionReason} Steps: {result.TotalSteps}");
        var depth3plus = result.VisitedPages
            .Where(p => p.Contains("wifi") || p.Contains("advanced") || p.Contains("Wi-Fi"))
            .ToList();
        if (depth3plus.Any())
        {
            _output.WriteLine($"DEPTH VIOLATION: visited depth=3+ pages:");
            foreach (var p in depth3plus) _output.WriteLine($"  ❌ {p}");
        }

        // 核心断言: 不应访问 Wi‑Fi (depth=3)
        Assert.Empty(depth3plus);

        // 应访问 Settings 首页的 menu_item
        Assert.Contains(result.VisitedPages, p => p.Contains("network", StringComparison.OrdinalIgnoreCase));
        // 应访问 Network 的 menu_item (depth=1)
        Assert.Contains(result.VisitedPages, p => p.Contains("internet", StringComparison.OrdinalIgnoreCase));
    }
}
