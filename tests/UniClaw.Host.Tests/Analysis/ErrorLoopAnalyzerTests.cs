using UniClaw.Core.Observability;
using UniClaw.Host.Analysis;
using Xunit;

namespace UniClaw.Host.Tests.Analysis;

/// <summary>
/// Acceptance tests for ErrorLoopAnalyzer (trace-span-observability 9.15): error-loop
/// verdicts derived from the span tree — 5+ consecutive engine.step spans whose children
/// are ALL entry.skipped (stuck_in_error_loop, 0.9) and entry.skipped exceeding
/// entry.visited × 4 (skip_rate_too_high, 0.7). No baseline dependency — always operates.
/// </summary>
public sealed class ErrorLoopAnalyzerTests
{
    private static (InMemoryTraceService Service, InMemoryTraceStorage Storage) CreateTrace()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceService(storage), storage);
    }

    [Fact]
    public async Task FiveConsecutiveAllSkippedSteps_ReturnsStuckInErrorLoop()
    {
        // 5 consecutive engine.step spans, each with ONLY entry.skipped children
        // (no entry.visited among them) → stuck_in_error_loop at 0.9.
        var (service, storage) = CreateTrace();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        for (var i = 0; i < 5; i++)
        {
            storage.OpenSpan(SpanTypes.EngineStep, $"step {i}", $"s{i}", "run",
                start.AddSeconds(i + 1), null, null);
            storage.OpenSpan(SpanTypes.EntrySkipped, $"skip {i}", $"sk{i}", $"s{i}",
                start.AddSeconds(i + 1.1), null, null);
        }

        var verdict = Assert.IsType<CompletionVerdict>(
            await new ErrorLoopAnalyzer(null).EvaluateAsync(service));
        Assert.True(verdict.ShouldTerminate);
        Assert.Contains("stuck_in_error_loop", verdict.Reason);
        Assert.Equal(0.9, verdict.Confidence);
    }

    [Fact]
    public async Task SkipRateTooHigh_ReturnsTerminate()
    {
        // Single step with 1 entry.visited + 5 entry.skipped children:
        // skipped (5) > visited (1) × 4 → skip_rate_too_high at 0.7.
        // Consecutive-run rule does not fire: the step's children are not all skipped.
        var (service, storage) = CreateTrace();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1), null, null);
        storage.OpenSpan(SpanTypes.EntryVisited, "Network", "v1", "s1", start.AddSeconds(2), null, null);
        for (var i = 0; i < 5; i++)
            storage.OpenSpan(SpanTypes.EntrySkipped, $"skip {i}", $"sk{i}", "s1",
                start.AddSeconds(2.1 + i * 0.01), null, null);

        var verdict = Assert.IsType<CompletionVerdict>(
            await new ErrorLoopAnalyzer(null).EvaluateAsync(service));
        Assert.True(verdict.ShouldTerminate);
        Assert.Contains("skip_rate_too_high", verdict.Reason);
        Assert.Equal(0.7, verdict.Confidence);
    }

    [Fact]
    public async Task NormalRun_ReturnsObserve()
    {
        // Healthy run: two steps with visited > skipped each → neither rule fires.
        var (service, storage) = CreateTrace();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        for (var i = 0; i < 2; i++)
        {
            storage.OpenSpan(SpanTypes.EngineStep, $"step {i}", $"s{i}", "run",
                start.AddSeconds(i + 1), null, null);
            storage.OpenSpan(SpanTypes.EntryVisited, $"visited {i} a", $"v{i}a", $"s{i}",
                start.AddSeconds(i + 1.1), null, null);
            storage.OpenSpan(SpanTypes.EntryVisited, $"visited {i} b", $"v{i}b", $"s{i}",
                start.AddSeconds(i + 1.2), null, null);
            storage.OpenSpan(SpanTypes.EntrySkipped, $"skip {i}", $"sk{i}", $"s{i}",
                start.AddSeconds(i + 1.3), null, null);
        }

        var verdict = Assert.IsType<CompletionVerdict>(
            await new ErrorLoopAnalyzer(null).EvaluateAsync(service));
        Assert.False(verdict.ShouldTerminate);
        Assert.Equal(0.0, verdict.Confidence);
        Assert.Contains("observe", verdict.Reason);
        Assert.DoesNotContain("error_loop", verdict.Reason);
    }
}
