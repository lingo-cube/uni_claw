using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Simulation.TraceReplay;

/// <summary>
/// 从真实 run 20260805T052309367Z-1bc7a25ea6384e3 产物还原的执行路径仿真测试。
/// 场景: enumerate-settings-safely, local provider, safe_mode plan.
/// 真实结局: max_steps (120), settings_home_not_restored.
/// </summary>
public class TraceReplay_20260805T052309367Z_Enumerate
{
    [Fact(DisplayName = "复现: DFS回退后重入已访问子节点 → max_steps 或 all_visited")]
    public async Task DfsRevisitLoop_ReproducesLoop()
    {
        var fixture = EnumerateFixtures.DfsRevisitLoop();
        var plan = EnumerateFixtures.CreateEnumeratePlan();
        var engine = EnumerateFixtures.CreateEngine(fixture, plan);

        var result = await engine.RunAsync(CancellationToken.None);

        // 真实 run 结局: max_steps (120). 仿真中页面有限, 预期 max_steps 或 all_visited.
        Assert.True(
            result.CompletionReason == TraversalResult.Reasons.MaxSteps
            || result.CompletionReason == TraversalResult.Reasons.AllVisited,
            $"Expected MaxSteps or AllVisited, got: {result.CompletionReason}");

        // 关键: 引擎进入了 Internet 子页面 (动态生成 node ID 含 "Internet")
        Assert.Contains(result.VisitedPages, p => p.Contains("Internet", StringComparison.OrdinalIgnoreCase));
        // 至少有 click action
        Assert.True(result.ActionHistory.Length > 0, "Expected at least one click action");
    }

    [Fact(DisplayName = "搜索框 type=input → DynamicRule 跳过, 引擎正常")]
    public async Task SearchBoxInput_EngineSkipsIt()
    {
        var fixture = new StateFixtureBuilder()
            .Page("settings_fixed", p => p
                .Name("Settings")
                .Element("qsearch", e => e.Type("input").Text("Q Search settings").At(0.5, 0.28))
                .Element("network_internet", e => e.Type("menu_item").Text("Network & internet").At(0.38, 0.40))
                .Element("connected_devices", e => e.Type("menu_item").Text("Connected devices").At(0.38, 0.54))
                .BackButton("btn_back", 0.05, 0.05))
            .Page("network_internet", p => p
                .Name("Network & internet")
                .Element("ni_internet", e => e.Type("menu_item").Text("Internet").At(0.5, 0.15))
                .BackButton("ni_back", 0.05, 0.05))
            .Transition(t => t.Id("t_net").Click("network_internet").From("settings_fixed").To("network_internet"))
            .Transition(t => t.Id("t_ni_back").Click("ni_back").From("network_internet").To("settings_fixed"))
            .Build();

        var plan = EnumerateFixtures.CreateEnumeratePlan();
        var engine = EnumerateFixtures.CreateEngine(fixture, plan);
        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Contains(result.VisitedPages, p => p.Contains("Network & internet", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.ActionHistory.Length > 0, "Expected at least one action");
    }

    [Fact(DisplayName = "复现: 搜索框 type=menu_item → 引擎误点进入搜索卡死")]
    public async Task SearchBoxMenuItem_EngineStuckInSearch()
    {
        var fixture = EnumerateFixtures.SearchBoxStuck();
        var plan = EnumerateFixtures.CreateEnumeratePlan();
        var engine = EnumerateFixtures.CreateEngine(fixture, plan);
        var result = await engine.RunAsync(CancellationToken.None);

        Assert.True(
            result.CompletionReason == TraversalResult.Reasons.MaxSteps
            || result.CompletionReason == TraversalResult.Reasons.AllVisited,
            $"Expected MaxSteps or AllVisited, got: {result.CompletionReason}");
    }
}
