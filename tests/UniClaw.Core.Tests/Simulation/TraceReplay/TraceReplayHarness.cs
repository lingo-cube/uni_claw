using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Tests.Simulation.TraceReplay;

/// <summary>
/// 从真实 run 产物直接构建仿真引擎的可复用 harness。
/// 不手写 fixture — 直接读 analysis.jsonl + plan.json + result.json。
/// </summary>
public sealed class TraceReplayHarness
{
    private readonly ImmutableArray<PageAnalysis> _analyses;
    private readonly TraversalPlan _plan;
    private readonly string _expectedReason;
    private readonly int _expectedActions;
    private readonly string _runId;

    public string ExpectedReason => _expectedReason;
    public int ExpectedActions => _expectedActions;
    public string RunId => _runId;

    private TraceReplayHarness(
        string runId, ImmutableArray<PageAnalysis> analyses, TraversalPlan plan,
        string expectedReason, int expectedActions)
    { _runId = runId; _analyses = analyses; _plan = plan; _expectedReason = expectedReason; _expectedActions = expectedActions; }

    /// <summary>从 run 目录构建 harness</summary>
    public static TraceReplayHarness FromRunDir(string runDir)
    {
        var planPath = Path.Combine(runDir, "plan.json");
        var plan = JsonSerializer.Deserialize<TraversalPlan>(
            File.ReadAllText(planPath), DomainJsonOptions.Default)
            ?? throw new InvalidOperationException("Failed to deserialize plan.json");
        plan = plan with { EntryPolicy = new EntryPolicy(EntryStrategy.BindCurrentScreen) };

        using var resultDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, "result.json")));
        var root = resultDoc.RootElement;
        var expectedReason = root.GetProperty("completionReason").GetString() ?? "unknown";
        var expectedActions = 0;
        if (root.TryGetProperty("actionsAttempted", out var aa)) expectedActions = aa.GetInt32();
        var runId = root.GetProperty("runId").GetString() ?? "";

        var analysisPath = Path.Combine(runDir, "assets", runId, "analysis.jsonl");
        var analyses = File.ReadAllLines(analysisPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<PageAnalysis>(l, DomainJsonOptions.Default))
            .Where(a => a is not null).Select(a => a!).ToImmutableArray();

        return new TraceReplayHarness(runId, analyses, plan, expectedReason, expectedActions);
    }

    public Task<TraversalResult> RunAsync(CancellationToken ct = default)
    {
        var vision = new TraceReplayVisionService(_analyses);
        var action = new TraceReplayActionExecutor(vision, _analyses);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var engine = new TraversalEngine(_plan, brain, new DefaultScreenStateProvider(), action);
        return engine.RunAsync(ct);
    }

    /// <summary>用修改后的 plan 运行 — 验证修复</summary>
    public Task<TraversalResult> RunWithPlanAsync(TraversalPlan modifiedPlan, CancellationToken ct = default)
    {
        var vision = new TraceReplayVisionService(_analyses);
        var action = new TraceReplayActionExecutor(vision, _analyses);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var engine = new TraversalEngine(modifiedPlan, brain, new DefaultScreenStateProvider(), action);
        return engine.RunAsync(ct);
    }

    /// <summary>诊断输出: 回放后打印引擎行为摘要</summary>
    public string Diagnose(TraversalResult result)
    {
        var lines = new List<string>
        {
            $"RunId: {_runId}",
            $"Expected: {_expectedReason} | Actual: {result.CompletionReason} | Steps: {result.TotalSteps}",
            $"Actions: {result.ActionHistory.Length} (expected ~{_expectedActions})",
            $"Visited pages ({result.VisitedPages.Length}):"
        };
        foreach (var p in result.VisitedPages)
            lines.Add($"  - {p}");
        lines.Add($"Action history:");
        foreach (var a in result.ActionHistory)
            lines.Add($"  [{a.Timestamp:HH:mm:ss}] {a.Action} success={a.Success} params={string.Join(",", a.Parameters.Select(kv => $"{kv.Key}={kv.Value}"))}");
        return string.Join("\n", lines);
    }
}

/// <summary>按 analysis.jsonl 时序回放页面分析</summary>
public sealed class TraceReplayVisionService : IPageAnalyzer
{
    private readonly ImmutableArray<PageAnalysis> _analyses;
    private int _index;
    public int CurrentIndex => _index;
    public int TotalFrames => _analyses.Length;

    public TraceReplayVisionService(ImmutableArray<PageAnalysis> analyses) { _analyses = analyses; }

    public void AdvanceIndex() { if (_index < _analyses.Length) _index++; }

    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        if (_analyses.IsEmpty) return Task.FromResult<PageAnalysis?>(null);
        var a = _index < _analyses.Length ? _analyses[_index] : _analyses[^1];
        if (_index < _analyses.Length) _index++;
        return Task.FromResult<PageAnalysis?>(a);
    }
    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => Task.FromResult<AppEntryPoint?>(null);
    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis, string expectedType, string? expectedPageName = null, CancellationToken ct = default)
        => Task.FromResult(new PageTypeVerification(true, 1.0, expectedType));
}

/// <summary>记录动作不执行真实 I/O</summary>
public sealed class TraceReplayActionExecutor : IActionExecutor
{
    private readonly TraceReplayVisionService _vision;
    private readonly ImmutableArray<PageAnalysis> _analyses;
    private readonly List<ActionRecord> _history = new();

    public TraceReplayActionExecutor(TraceReplayVisionService vision, ImmutableArray<PageAnalysis> analyses)
    { _vision = vision; _analyses = analyses; }

    public Task<bool> TapAsync(double x, double y, CancellationToken ct = default)
    { Record("click", x, y); return Task.FromResult(true); }
    public Task<bool> PressBackAsync(CancellationToken ct = default)
    { Record("back", 0, 0); return Task.FromResult(true); }
    public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int durationMs = 400, CancellationToken ct = default)
    { Record("scroll", sx, sy); AdvanceIfPageChanged(); return Task.FromResult(true); }
    public Task<bool> InputTextAsync(string text, CancellationToken ct = default)
    { Record("input", 0, 0); return Task.FromResult(true); }
    public Task<bool> LongPressAsync(double x, double y, int durationMs = 800, CancellationToken ct = default)
    { Record("long_press", x, y); return Task.FromResult(true); }
    public Task WaitAsync(int milliseconds, CancellationToken ct = default) => Task.CompletedTask;

    public List<ActionRecord> GetHistory() => _history;
    public Task<bool> IsScreenOnAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> LaunchAppAsync(string packageName, CancellationToken ct = default) => Task.FromResult(true);
    public Task<string?> GetCurrentActivityAsync(CancellationToken ct = default) => Task.FromResult<string?>("com.android.settings");
    public Task<bool> GoBackAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> GoHomeAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<string> ExecuteShellAsync(string command, CancellationToken ct = default) => Task.FromResult("ok");

    private void Record(string action, double x, double y)
        => _history.Add(new ActionRecord(action, DateTimeOffset.UtcNow,
            new Dictionary<string, object> { ["x"] = x, ["y"] = y }, true));

    private void AdvanceIfPageChanged()
    {
        var idx = _vision.CurrentIndex;
        if (idx >= _analyses.Length - 1) return;
        var cur = _analyses[idx].Items.FirstOrDefault()?.Name;
        var nxt = _analyses[idx + 1].Items.FirstOrDefault()?.Name;
        if (cur != nxt) _vision.AdvanceIndex();
    }
}
