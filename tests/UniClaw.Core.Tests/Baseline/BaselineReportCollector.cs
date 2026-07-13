using UniClaw.Core.Simulation.ExpectedBehavior;
using UniClaw.Core.Simulation.Scroll;
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
    /// </summary>
    public BaselineReportCollector()
    {
        _reports = new List<BaselineReport>();
        _reportsDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Baseline",
            "reports");
    }

    /// <summary>
    /// Adds a test result to the collector.
    /// </summary>
    /// <param name="scenario">Scenario identifier (matches expected JSON filename)</param>
    /// <param name="expected">Expected behavior definition</param>
    /// <param name="result">Actual traversal result</param>
    /// <param name="report">Verification report from ExpectedBehavior.Verify</param>
    /// <param name="executor">Optional scroll mock action executor for scroll metrics</param>
    /// <param name="vision">Optional scroll mock vision service for scroll metrics</param>
    public void Add(
        string scenario,
        ExpectedBehavior expected,
        TraversalResult result,
        VerificationReport report,
        ScrollableMockActionExecutor? executor = null,
        ScrollableMockVisionService? vision = null)
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
    /// Builds actual NumericAnchor from traversal result and optional mock services.
    /// </summary>
    private NumericAnchor BuildActualNumeric(
        TraversalResult result,
        ScrollableMockActionExecutor? executor,
        ScrollableMockVisionService? vision)
    {
        // 基础指标（保持不变）
        var totalSteps = result.TotalSteps;
        var visitedPagesCount = result.VisitedPages.Length;
        var actionHistoryCount = result.ActionHistory.Length;
        var elapsedSecondsMax = result.ElapsedSeconds;

        // 滚动指标：从 ScrollHistory 计算
        int scrollCount = 0, scrollUpCount = 0;
        double scrollDistance = 0.0, finalProgress = 0.0;

        if (executor != null && vision != null)
        {
            var scrollHistory = executor.ScrollHistory;
            var currentPageId = vision.CurrentPageId;

            // 从滚动历史计算指标
            scrollCount = scrollHistory.Count(s => s.Action == ScrollActionType.ScrollDown);
            scrollUpCount = scrollHistory.Count(s => s.Action == ScrollActionType.ScrollUp);
            finalProgress = vision.GetScrollProgress(currentPageId);

            // 计算总滚动距离
            if (scrollHistory.Length > 0)
            {
                var firstScroll = scrollHistory[0];
                var lastScroll = scrollHistory[^1];
                scrollDistance = lastScroll.AfterProgress - firstScroll.BeforeProgress;
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
            JumpDetected: 0,        // Phase 3
            JumpRecovered: 0,       // Phase 3
            FinalProgress: finalProgress,
            AdaptiveStepIncreases: 0); // Phase 3
    }

    /// <summary>
    /// Writes all collected reports to JSON and Markdown.
    /// Called by the fixture when the test collection completes.
    /// </summary>
    public void WriteAll()
    {
        BaselineReportWriter.WriteAll(_reportsDir, _reports);
    }
}
