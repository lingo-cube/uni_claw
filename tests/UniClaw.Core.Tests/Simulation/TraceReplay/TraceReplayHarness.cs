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
/// FSM 仿真验证 harness — 核心是可配置的 vision + action，不绑死 trace replay。
///
/// 三种用法:
///   1. TraceReplayHarness.FromRunDir(runDir) → 用 analysis.jsonl 回放
///   2. TraceReplayHarness.FromRunDir(runDir).WithVision(mockVision) → 自定义 vision
///   3. new TraceReplayHarness(plan, analyses, reason, actions) → 全手动
/// </summary>
public sealed class TraceReplayHarness
{
    private readonly ImmutableArray<PageAnalysis> _analyses;
    private readonly TraversalPlan _plan;
    private readonly string _expectedReason;
    private readonly int _expectedActions;
    private readonly string _runId;
    private Func<IPageAnalyzer> _visionFactory;
    private Func<IPageAnalyzer, IActionExecutor> _actionFactory;

    public string ExpectedReason => _expectedReason;
    public int ExpectedActions => _expectedActions;
    public string RunId => _runId;

    public TraceReplayHarness(
        string runId, ImmutableArray<PageAnalysis> analyses, TraversalPlan plan,
        string expectedReason, int expectedActions)
    {
        _runId = runId; _analyses = analyses; _plan = plan;
        _expectedReason = expectedReason; _expectedActions = expectedActions;
        _visionFactory = () => new TraceReplayVisionService(analyses);
        _actionFactory = v => new TraceReplayActionExecutor((TraceReplayVisionService)v, analyses);
    }

    // ── Factory ────────────────────────────────────────

    /// <summary>从 run 目录构建 — 默认 trace replay vision</summary>
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

    // ── Configuration (fluent) ─────────────────────────

    /// <summary>替换 vision — 可注入 StatefulMockVisionService 等</summary>
    public TraceReplayHarness WithVision(IPageAnalyzer vision)
    { _visionFactory = () => vision; return this; }

    /// <summary>替换 action executor</summary>
    public TraceReplayHarness WithAction(Func<IPageAnalyzer, IActionExecutor> factory)
    { _actionFactory = factory; return this; }

    // ── Execution ──────────────────────────────────────

    public Task<TraversalResult> RunAsync(CancellationToken ct = default)
        => RunWithPlanAsync(_plan, ct);

    public Task<TraversalResult> RunWithPlanAsync(TraversalPlan modifiedPlan, CancellationToken ct = default)
    {
        var vision = _visionFactory();
        var action = _actionFactory(vision);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var engine = new TraversalEngine(modifiedPlan, brain, new DefaultScreenStateProvider(), action);
        return engine.RunAsync(ct);
    }

    // ── Diagnostics ────────────────────────────────────

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
        lines.Add("Action history:");
        foreach (var a in result.ActionHistory)
            lines.Add($"  [{a.Timestamp:HH:mm:ss}] {a.Action} success={a.Success} params={string.Join(",", a.Parameters.Select(kv => $"{kv.Key}={kv.Value}"))}");
        return string.Join("\n", lines);
    }

    /// <summary>深度诊断: 提取子帧嵌套深度</summary>
    public static int MaxSubframeDepth(TraversalResult result)
        => result.VisitedPages.Select(p => p.Split("_subframe").Length - 1).Max();
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
