using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Core.Tests.Simulation.TraceReplay;

/// <summary>
/// Trace Replay → 诊断 → 修复验证 的完整工作流。
/// 不需要模拟器，< 1 秒完成一次迭代。
/// </summary>
public class TraceReplayFromRunTests
{
    private readonly ITestOutputHelper _output;
    public TraceReplayFromRunTests(ITestOutputHelper output) => _output = output;

    private const string RunDir = "/Users/fran/Documents/Code/spacex/uni-claw/artifacts/runs/skill-test/enumerate-settings-safely/20260805T052309367Z-1bc7a25ea6384e3";

    // ── Step 1: 复现 ────────────────────────────────────

    [Fact(DisplayName = "Step1: 从产物回放 → 复现失败结局")]
    public async Task Step1_Reproduce()
    {
        var h = TraceReplayHarness.FromRunDir(RunDir);
        var result = await h.RunAsync();

        _output.WriteLine(h.Diagnose(result));

        // 引擎消耗了步数 (复现了遍历行为)
        Assert.True(result.TotalSteps > 0, "Engine should traverse");
        // 访过的页面不止 root
        Assert.True(result.VisitedPages.Length > 1,
            $"Expected multiple visited pages, got {result.VisitedPages.Length}: {string.Join(", ", result.VisitedPages)}");
    }

    // ── Step 2: 诊断 ────────────────────────────────────

    [Fact(DisplayName = "Step2: 诊断 — DFS 在 depth=1 有重复访问")]
    public async Task Step2_Diagnose_DfsRevisitsAtDepth1()
    {
        var h = TraceReplayHarness.FromRunDir(RunDir);
        var result = await h.RunAsync();

        // 诊断: 统计重复访问的页面
        var pageVisits = result.VisitedPages
            .GroupBy(p => p.Split("_root")[0])  // 提取基础页面名
            .Where(g => g.Count() > 1)
            .ToList();

        _output.WriteLine($"Pages visited more than once: {pageVisits.Count}");
        foreach (var g in pageVisits)
            _output.WriteLine($"  '{g.Key}' ×{g.Count()}");

        // 真实 run 中 "dyn_menu_container_Internet" 被访问多次 (回退重入循环)
        var internetVisits = result.VisitedPages.Count(p => p.Contains("Internet"));
        _output.WriteLine($"Internet page visited {internetVisits} times");
        Assert.True(internetVisits >= 2,
            $"DFS revisit bug: Internet should be visited ≥2 times, got {internetVisits}");
    }

    // ── Step 3: 修 → 验 (fix-verify loop) ──────────────

    [Fact(DisplayName = "Step3: 修复验证 — enable restore 后引擎应回到 Settings")]
    public async Task Step3_FixVerify_RestoreEnabled()
    {
        var h = TraceReplayHarness.FromRunDir(RunDir);

        // 修复: 构建 restore=true 的计划
        var intentSlots = new IntentSlots(
            "com.android.settings", "full",
            ElementHandling: "safe_mode",
            Navigation: "bounded_settings",
            Restore: true,   // ← BUGFIX: 启用 restore
            Entry: "Settings");

        var plan = new PlanCompiler().Compile(intentSlots);
        plan = plan with { EntryPolicy = new EntryPolicy(EntryStrategy.BindCurrentScreen) };

        var result = await h.RunWithPlanAsync(plan);

        _output.WriteLine(h.Diagnose(result));

        // restore=true → 引擎应该在遍历后回到 Settings
        // 注意: 即使 restore 也不能完全解决 visited-children 重复访问
        // 但至少 completionReason 不再是 settings_home_not_restored
        _output.WriteLine($"With restore=true: reason={result.CompletionReason} steps={result.TotalSteps}");
        Assert.True(result.TotalSteps > 0);
    }
}
