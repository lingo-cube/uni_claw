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

    /// <summary>回放所用的 analysis.jsonl 帧序列（trace 模式可用）。</summary>
    public ImmutableArray<PageAnalysis> Analyses => _analyses;

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

    /// <summary>
    /// 规范化动作名称: tap→click, swipe→scroll, input_text→input。
    /// TraceReplayActionExecutor 和 StatefulMockActionExecutor 使用不同命名约定。
    /// </summary>
    private static string NormalizeActionName(string raw) => raw switch
    {
        "tap" => "click",
        "swipe" => "scroll",
        "input_text" => "input",
        _ => raw,
    };

    // ── Visual Replay Export ──────────────────────────

    private static readonly JsonSerializerOptions s_exportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// 导出仿真回放数据为 JSON，供 sim-replay-viewer.py 生成可视化 HTML。
    /// fixture 可选（TraceReplayVisionService 模式下无 fixture，仅回放 action + trace）。
    /// </summary>
    public static void ExportReplayJson(TraversalResult result, StateFixture? fixture, string outputPath)
        => ExportReplayJson(result, fixture, analyses: null, outputPath);

    /// <summary>
    /// 导出仿真回放数据（trace 模式），从 analysis.jsonl 帧构建页面，
    /// 并从原始 run.log 提取真实动作序列（而非 replay 引擎的决策）。
    /// </summary>
    public void ExportReplayJson(TraversalResult result, string outputPath)
        => ExportReplayJson(result, runLogPath: null, outputPath);

    /// <summary>
    /// 导出仿真回放数据（trace 模式），读取原始 run.log 获取动作序列。
    /// </summary>
    public void ExportReplayJson(TraversalResult result, string? runLogPath, string outputPath)
    {
        // 从 analyses 构建 trace-mode 页面
        var pages = new Dictionary<string, object?>();
        var seenNames = new HashSet<string>();
        int pi = 0;
        foreach (var a in _analyses)
        {
            var first = a.Items.FirstOrDefault();
            var name = first?.Name ?? $"frame_{pi}";
            var pid = SanitizePageId(name);
            if (seenNames.Add(pid))
            {
                pages[pid] = new Dictionary<string, object?>
                {
                    ["pageName"] = name,
                    ["frameIndex"] = pi,
                    ["elements"] = a.Items.Select(item => new Dictionary<string, object?>
                    {
                        ["id"] = SanitizePageId(item.Name),
                        ["type"] = item.Type.ToString().ToLowerInvariant(),
                        ["text"] = item.Name ?? "",
                        ["x"] = item.Coordinate?.X ?? 0.5,
                        ["y"] = item.Coordinate?.Y ?? 0.5,
                    }).ToArray(),
                };
            }
            pi++;
        }

        // 从原始 run.log 提取动作（优先于 replay engine 的动作）
        var actionEntries = result.ActionHistory;
        var traceEntries = result.Trace;
        if (runLogPath is not null && File.Exists(runLogPath))
        {
            var (parsedActions, parsedTrace) = ParseRunLog(runLogPath, _analyses);
            if (parsedActions.Length > 0)
            {
                actionEntries = parsedActions;
                traceEntries = parsedTrace;
            }
        }

        var replay = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["runId"] = _runId,
            ["completionReason"] = result.CompletionReason,
            ["totalSteps"] = result.TotalSteps,
            ["elapsedSeconds"] = result.ElapsedSeconds,
            ["sourceMode"] = "trace",
            ["fixture"] = new Dictionary<string, object?>
            {
                ["initialPage"] = pages.Keys.FirstOrDefault() ?? "",
                ["pages"] = pages,
                ["transitions"] = Array.Empty<object>(),
            },
            ["actionHistory"] = BuildActionEntriesFromList(actionEntries),
            ["visitedPages"] = result.VisitedPages.ToArray(),
            ["trace"] = BuildTraceEntriesFromList(traceEntries),
            ["analysisFrameCount"] = _analyses.Length,
        };

        File.WriteAllText(outputPath,
            JsonSerializer.Serialize(replay, s_exportOptions));
    }

    /// <summary>从原始 run.log 解析动作序列和 FSM 转换。</summary>
    private static (ImmutableArray<ActionRecord> Actions, ImmutableArray<TraceRecord> Trace)
        ParseRunLog(string runLogPath, ImmutableArray<PageAnalysis> analyses)
    {
        var actions = new List<ActionRecord>();
        var traces = new List<TraceRecord>();
        var lines = File.ReadAllLines(runLogPath);

        // 正则: FSM From→To step=N
        var fsmRe = new Regex(@"FSM (\w+)→(\w+) step=(\d+)");
        // 正则: SafeActionExecutor: action=X result=Y
        var actionRe = new Regex(@"SafeActionExecutor: action=(\w+) result=(\w+)");

        int? lastStep = null;
        string? lastAction = null;
        string? lastResult = null;

        foreach (var line in lines)
        {
            var fm = fsmRe.Match(line);
            if (fm.Success)
            {
                var from = fm.Groups[1].Value;
                var to = fm.Groups[2].Value;
                var step = int.Parse(fm.Groups[3].Value);
                lastStep = step;

                // Attach any pending action to this step
                if (lastAction is not null)
                {
                    // 从 analysis 帧获取坐标 (step N → analysis index ≈ N-1)
                    int analysisIdx = Math.Min(step - 1, analyses.Length - 1);
                    var frame = analysisIdx >= 0 ? analyses[analysisIdx] : analyses[0];
                    var items = frame.Items;
                    // 取第一个 menu_item/button 的中心作为近似坐标
                    double x = 0.5, y = 0.3;
                    if (items.Length > 0)
                    {
                        // 尝试用 step 序号选不同元素
                        int elemIdx = (step - 1) % items.Length;
                        var item = items[elemIdx];
                        x = item.Coordinate?.X ?? 0.5;
                        y = item.Coordinate?.Y ?? 0.5;
                    }
                    actions.Add(new ActionRecord(
                        lastAction,
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object> { ["x"] = x, ["y"] = y },
                        lastResult == "ok"));
                    lastAction = null;
                    lastResult = null;
                }

                traces.Add(new TraceRecord(
                    StepNumber: step,
                    FromState: Enum.TryParse<TraversalState>(from, out var fs) ? fs : TraversalState.NodeSelect,
                    ToState: Enum.TryParse<TraversalState>(to, out var ts) ? ts : TraversalState.Execute,
                    CurrentNodeId: null, CurrentPageId: null,
                    ActionExecuted: lastAction ?? "",
                    ActionSuccess: lastResult == "ok",
                    ChildPushed: false, FrameCompleted: false));
                continue;
            }

            var am = actionRe.Match(line);
            if (am.Success)
            {
                lastAction = am.Groups[1].Value;
                lastResult = am.Groups[2].Value;
            }
        }

        return (actions.ToImmutableArray(), traces.ToImmutableArray());
    }

    private static object[] BuildActionEntriesFromList(IList<ActionRecord> actions) =>
        actions.Select(a =>
        {
            var entry = new Dictionary<string, object?>
            {
                ["action"] = NormalizeActionName(a.Action),
                ["timestamp"] = a.Timestamp.ToString("o"),
                ["success"] = a.Success,
            };
            if (a.Parameters.TryGetValue("x", out var xObj) && xObj is double x)
                entry["x"] = Math.Round(x, 4);
            if (a.Parameters.TryGetValue("y", out var yObj) && yObj is double y)
                entry["y"] = Math.Round(y, 4);
            if (a.Parameters.TryGetValue("element_id", out var elemObj) && elemObj is string elemId)
                entry["elementId"] = elemId;
            return (object)entry;
        }).ToArray();

    private static object[] BuildTraceEntriesFromList(IList<TraceRecord> traces) =>
        traces.Select(t => new Dictionary<string, object?>
        {
            ["stepNumber"] = t.StepNumber,
            ["fromState"] = t.FromState.ToString(),
            ["toState"] = t.ToState.ToString(),
            ["actionExecuted"] = t.ActionExecuted,
            ["actionSuccess"] = t.ActionSuccess,
            ["pageFrom"] = t.PageFrom,
            ["pageTo"] = t.PageTo,
        }).ToArray();

    /// <summary>
    /// 完整版导出: fixture + analyses 都提供时, analyses 页面可补充 fixture 未覆盖的帧。
    /// </summary>
    public static void ExportReplayJson(
        TraversalResult result, StateFixture? fixture,
        ImmutableArray<PageAnalysis>? analyses, string outputPath)
    {
        var replay = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["runId"] = result.TraceId ?? "",
            ["completionReason"] = result.CompletionReason,
            ["totalSteps"] = result.TotalSteps,
            ["elapsedSeconds"] = result.ElapsedSeconds,
            ["sourceMode"] = fixture != null ? "fixture" : "trace",
        };

        // Fixture (optional — only available with StatefulMockVisionService)
        if (fixture != null)
        {
            replay["fixture"] = BuildFixtureDict(fixture);
        }
        else if (analyses is { Length: > 0 })
        {
            replay["fixture"] = BuildFixtureFromAnalyses(analyses.Value);
        }

        replay["actionHistory"] = BuildActionEntries(result);
        replay["visitedPages"] = result.VisitedPages.ToArray();
        replay["trace"] = BuildTraceEntries(result);

        File.WriteAllText(outputPath,
            JsonSerializer.Serialize(replay, s_exportOptions));
    }

    // ── helpers ──────────────────────────────────

    private static object BuildFixtureDict(StateFixture f) =>
        new Dictionary<string, object?>
        {
            ["initialPage"] = f.InitialPage,
            ["pages"] = f.Pages.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)new Dictionary<string, object?>
                {
                    ["pageName"] = kvp.Value.PageName,
                    ["elements"] = kvp.Value.Elements.Select(e => new Dictionary<string, object?>
                    {
                        ["id"] = e.Id, ["type"] = e.Type,
                        ["text"] = e.Text, ["x"] = e.X, ["y"] = e.Y,
                    }).ToArray(),
                }),
            ["transitions"] = f.Transitions.Select(t => new Dictionary<string, object?>
            {
                ["id"] = t.Id, ["trigger"] = t.Trigger,
                ["fromPage"] = t.FromPage, ["toPage"] = t.ToPage,
                ["action"] = t.Action,
            }).ToArray(),
        };

    private static object BuildFixtureFromAnalyses(ImmutableArray<PageAnalysis> analyses)
    {
        var pages = new Dictionary<string, object?>();
        var seenNames = new HashSet<string>();
        for (int i = 0; i < analyses.Length; i++)
        {
            var a = analyses[i];
            var first = a.Items.FirstOrDefault();
            var name = first?.Name ?? $"frame_{i}";
            var pid = SanitizePageId(name);
            if (seenNames.Add(pid))
            {
                pages[pid] = new Dictionary<string, object?>
                {
                    ["pageName"] = name,
                    ["frameIndex"] = i,
                    ["elements"] = a.Items.Select(item => new Dictionary<string, object?>
                    {
                        ["id"] = SanitizePageId(item.Name),
                        ["type"] = item.Type.ToString().ToLowerInvariant(),
                        ["text"] = item.Name ?? "",
                        ["x"] = item.Coordinate?.X ?? 0.5,
                        ["y"] = item.Coordinate?.Y ?? 0.5,
                    }).ToArray(),
                };
            }
        }
        return new Dictionary<string, object?>
        {
            ["initialPage"] = pages.Keys.FirstOrDefault() ?? "",
            ["pages"] = pages,
            ["transitions"] = Array.Empty<object>(),
        };
    }

    private static object[] BuildActionEntries(TraversalResult result) =>
        result.ActionHistory.Select(a =>
        {
            var entry = new Dictionary<string, object?>
            {
                ["action"] = NormalizeActionName(a.Action),
                ["timestamp"] = a.Timestamp.ToString("o"),
                ["success"] = a.Success,
            };
            if (a.Parameters.TryGetValue("x", out var xObj) && xObj is double x)
                entry["x"] = Math.Round(x, 4);
            if (a.Parameters.TryGetValue("y", out var yObj) && yObj is double y)
                entry["y"] = Math.Round(y, 4);
            if (a.Parameters.TryGetValue("element_id", out var elemObj) && elemObj is string elemId)
                entry["elementId"] = elemId;
            return (object)entry;
        }).ToArray();

    private static object[] BuildTraceEntries(TraversalResult result) =>
        result.Trace.Select(t => new Dictionary<string, object?>
        {
            ["stepNumber"] = t.StepNumber,
            ["fromState"] = t.FromState.ToString(),
            ["toState"] = t.ToState.ToString(),
            ["actionExecuted"] = t.ActionExecuted,
            ["actionSuccess"] = t.ActionSuccess,
            ["pageFrom"] = t.PageFrom,
            ["pageTo"] = t.PageTo,
        }).ToArray();

    private static string SanitizePageId(string? name) =>
        (name ?? "unknown").ToLowerInvariant()
            .Replace(" & ", "_")
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("(", "").Replace(")", "")
            .Replace("'", "").Replace("\"", "");
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
