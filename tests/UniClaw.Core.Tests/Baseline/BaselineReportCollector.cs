using UniClaw.Core.Simulation.ExpectedBehavior;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Baseline;

/// <summary>
/// xUnit collection fixture for baseline tests.
/// Creates a BaselineReportCollector instance and writes reports when disposed.
/// </summary>
[CollectionDefinition("Baseline Tests")]
public sealed class BaselineTestsFixture : ICollectionFixture<BaselineTestsFixture>, IDisposable
{
    /// <summary>
    /// Gets the collector instance for adding test results.
    /// </summary>
    public BaselineReportCollector Collector { get; }

    public BaselineTestsFixture()
    {
        Collector = new BaselineReportCollector();
    }

    public void Dispose()
    {
        Collector.WriteAll();
    }
}

/// <summary>
/// Collector for baseline test results.
/// Stores results and writes them to JSON/Markdown reports.
/// </summary>
public sealed class BaselineReportCollector
{
    private readonly List<BaselineReport> _reports;
    private readonly string _reportsDir;

    /// <summary>
    /// Creates a new BaselineReportCollector.
    /// Reports are written to tests/UniClaw.Core.Tests/Baseline/reports/
    /// (source tree, not bin output).
    /// </summary>
    public BaselineReportCollector()
    {
        _reports = new List<BaselineReport>();
        var sourceRoot = FindSourceRoot();
        _reportsDir = Path.Combine(
            sourceRoot,
            "tests", "UniClaw.Core.Tests", "Baseline", "reports");
    }

    /// <summary>
    /// Walk up from test bin directory to find solution root.
    /// </summary>
    private static string FindSourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "src", "UniClaw.Core.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Adds a test result to the collector.
    /// </summary>
    /// <param name="scenario">Scenario identifier (matches expected JSON filename)</param>
    /// <param name="expected">Expected behavior definition</param>
    /// <param name="result">Actual traversal result</param>
    /// <param name="report">Verification report from ExpectedBehavior.Verify</param>
    /// <param name="executor">Optional action executor — scroll metrics derived from its ActionHistory swipe records</param>
    /// <param name="vision">Optional vision provider — FinalProgress from its viewport</param>
    public void Add(
        string scenario,
        ExpectedBehavior expected,
        TraversalResult result,
        VerificationReport report,
        IActionExecutor? executor = null,
        IVisionProvider? vision = null)
    {
        var actualNumeric = BuildActualNumeric(result, executor, vision);
        var baselineReport = new BaselineReport(
            Scenario: scenario,
            Timestamp: DateTime.UtcNow,
            AllPassed: report.AllPassed,
            Details: report.Details,
            ExpectedNumeric: expected.NumericAnchor,
            ActualNumeric: actualNumeric);
        _reports.Add(baselineReport);
    }

    /// <summary>
    /// Builds actual NumericAnchor from traversal result + optional services.
    /// 滚动指标从 <see cref="IActionExecutor.GetHistory"/> 的 swipe ActionRecord 按方向统计,
    /// FinalProgress 取自 <see cref="IVisionProvider"/> 视口 (baseline-scroll-metrics)。
    /// </summary>
    private NumericAnchor BuildActualNumeric(
        TraversalResult result,
        IActionExecutor? executor,
        IVisionProvider? vision)
    {
        // 基础指标（保持不变）
        var totalSteps = result.TotalSteps;
        var visitedPagesCount = result.VisitedPages.Length;
        var actionHistoryCount = result.ActionHistory.Length;
        var elapsedSecondsMax = result.ElapsedSeconds;

        // 滚动指标：从 ActionHistory (swipe records) 计算; executor 或 vision 为空 → 全 0
        int scrollCount = 0, scrollUpCount = 0;
        double scrollDistance = 0.0, finalProgress = 0.0;

        if (executor != null && vision != null)
        {
            var swipes = executor.GetHistory()
                .Where(r => r.Action == "swipe")
                .ToList();

            scrollCount = swipes.Count(r => IsDirection(r, "down"));
            scrollUpCount = swipes.Count(r => IsDirection(r, "up"));
            finalProgress = vision.GetScrollProgress();

            // 滚动距离 = 末次 after-progress − 首次 before-progress (mock 视口)
            if (swipes.Count > 0)
            {
                var first = swipes[0];
                var last = swipes[^1];
                scrollDistance = ToDouble(last.Parameters, "after_progress")
                               - ToDouble(first.Parameters, "before_progress");
            }
        }

        return new NumericAnchor(
            TotalSteps: totalSteps,
            VisitedPagesCount: visitedPagesCount,
            ActionHistoryCount: actionHistoryCount,
            ElapsedSecondsMax: elapsedSecondsMax,
            ScrollCount: scrollCount,
            ScrollDistance: scrollDistance,
            ScrollUpCount: scrollUpCount,
            FinalProgress: finalProgress);
    }

    private static bool IsDirection(ActionRecord record, string direction)
        => record.Parameters.TryGetValue("direction", out var d) && d is string s && s == direction;

    private static double ToDouble(Dictionary<string, object> parameters, string key)
        => parameters.TryGetValue(key, out var v) && v is double d ? d : 0.0;

    /// <summary>
    /// Writes all collected reports to JSON and Markdown.
    /// Called by the fixture when the test collection completes.
    /// </summary>
    public void WriteAll()
    {
        BaselineReportWriter.WriteAll(_reportsDir, _reports);
    }
}
