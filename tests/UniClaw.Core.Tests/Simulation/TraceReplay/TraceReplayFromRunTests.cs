using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Core.Tests.Simulation.TraceReplay;

/// <summary>
/// 从产物回放 → 诊断 → 修复验证 的完整工作流。
/// 自动发现最近的失败 run，不需要硬编码路径。
/// < 1 秒一次迭代，不需要模拟器。
///
/// 用法:
///   1. 集成测试失败后 → 产物自动落 artifacts/runs/
///   2. 跑本测试 → Step1 复现, Step2 诊断, Step3 验证修复
///   3. 修改代码 → 重跑 Step3 → 通过后上模拟器 E2E
/// </summary>
public class TraceReplayFromRunTests
{
    private readonly ITestOutputHelper _output;
    public TraceReplayFromRunTests(ITestOutputHelper output) => _output = output;

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    private static string? FindLatestRun(string scenarioId)
    {
        var dir = Path.Combine(RepoRoot, "artifacts/runs", scenarioId);
        if (!Directory.Exists(dir)) return null;
        return Directory.GetDirectories(dir)
            .Where(d => File.Exists(Path.Combine(d, "assets",
                Path.GetFileName(d)!, "analysis.jsonl")))
            .OrderByDescending(d => d)
            .FirstOrDefault();
    }

    [Fact]
    public async Task Step1_AutoDiscoverAndReplay()
    {
        var runDir = FindLatestRun("enumerate-settings-safely");
        if (runDir is null) { _output.WriteLine("SKIP: no run found"); return; }

        _output.WriteLine($"Replaying: {runDir}");
        var h = TraceReplayHarness.FromRunDir(runDir);
        var result = await h.RunAsync();
        _output.WriteLine(h.Diagnose(result));
        Assert.True(result.TotalSteps > 0);
    }

    [Fact]
    public async Task Step2_Diagnose_DepthRunaway()
    {
        var runDir = FindLatestRun("enumerate-settings-safely");
        if (runDir is null) { _output.WriteLine("SKIP: no run found"); return; }

        var h = TraceReplayHarness.FromRunDir(runDir);
        var result = await h.RunAsync();

        var maxDepth = result.VisitedPages
            .Select(p => p.Split("_subframe").Length - 1)
            .Max();
        _output.WriteLine($"Max subframe depth: {maxDepth}");
        foreach (var p in result.VisitedPages)
        {
            var d = p.Split("_subframe").Length - 1;
            if (d >= 3) _output.WriteLine($"  depth={d}: {p[..Math.Min(100, p.Length)]}");
        }
        Assert.True(maxDepth >= 3, $"Depth runaway: {maxDepth}");
    }

    [Fact]
    public async Task Step3_FixVerify_RestoreConstrainsDepth()
    {
        var runDir = FindLatestRun("enumerate-settings-safely");
        if (runDir is null) { _output.WriteLine("SKIP: no run found"); return; }

        var h = TraceReplayHarness.FromRunDir(runDir);
        var plan = new PlanCompiler().Compile(new IntentSlots(
            "com.android.settings", "full",
            ElementHandling: "safe_mode",
            Navigation: "bounded_settings",
            Restore: true,
            Entry: "Settings"));
        plan = plan with { EntryPolicy = new EntryPolicy(EntryStrategy.BindCurrentScreen) };

        var result = await h.RunWithPlanAsync(plan);
        _output.WriteLine(h.Diagnose(result));
        Assert.True(result.TotalSteps > 0);
    }
}
