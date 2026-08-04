using UniClaw.Core.Observability;

namespace UniClaw.TraceTool;

public sealed record class StepDiff(
    string StepLabel,
    bool PresentInA,
    bool PresentInB,
    string? Difference);

public sealed record class MetricDiff(
    string Metric,
    long ValueA,
    long ValueB,
    long Delta);

public sealed record class AiComparison(
    string Capability,
    double AvgLatencyA,
    double AvgLatencyB,
    double DeltaMs,
    int CountA,
    int CountB);

public sealed record class RunDiff(
    IReadOnlyList<StepDiff> StepDiffs,
    IReadOnlyList<MetricDiff> MetricDiffs,
    IReadOnlyList<AiComparison> AiComparisons,
    string Conclusion,
    bool HasDifferences);

public static class RunDiffer
{
    public static RunDiff Diff(TraceRun runA, TraceRun runB)
    {
        var stepDiffs = DiffSteps(runA, runB);
        var metricDiffs = DiffMetrics(runA, runB);
        var aiComparisons = DiffAI(runA, runB);
        var hasDifferences = stepDiffs.Count > 0 || metricDiffs.Count > 0 || aiComparisons.Count > 0;
        var conclusion = BuildConclusion(hasDifferences, runA, runB);

        return new RunDiff(stepDiffs, metricDiffs, aiComparisons, conclusion, hasDifferences);
    }

    private static IReadOnlyList<StepDiff> DiffSteps(TraceRun runA, TraceRun runB)
    {
        var diffs = new List<StepDiff>();
        var stepsA = runA.Trace.GetSpansByType(SpanTypes.EngineStep);
        var stepsB = runB.Trace.GetSpansByType(SpanTypes.EngineStep);

        var maxSteps = Math.Max(stepsA.Count, stepsB.Count);
        for (var i = 0; i < maxSteps; i++)
        {
            var a = i < stepsA.Count ? stepsA[i] : null;
            var b = i < stepsB.Count ? stepsB[i] : null;

            if (a == null && b != null)
                diffs.Add(new StepDiff($"Step {i + 1}", false, true, "Added in B"));
            else if (a != null && b == null)
                diffs.Add(new StepDiff($"Step {i + 1}", true, false, "Removed in B"));
            else if (a != null && b != null && a.Status != b.Status)
                diffs.Add(new StepDiff($"Step {i + 1}", true, true, $"Status: {a.Status} → {b.Status}"));
        }

        return diffs;
    }

    private static IReadOnlyList<MetricDiff> DiffMetrics(TraceRun runA, TraceRun runB)
    {
        var diffs = new List<MetricDiff>();
        if (runA.Result == null || runB.Result == null) return diffs;

        var rA = runA.Result;
        var rB = runB.Result;

        AddMetric("Steps Consumed", rA.StepsConsumed, rB.StepsConsumed);
        AddMetric("Scrolls Consumed", rA.ScrollsConsumed, rB.ScrollsConsumed);
        AddMetric("Actions Attempted", rA.ActionsAttempted, rB.ActionsAttempted);
        AddMetric("Actions Succeeded", rA.ActionsSucceeded, rB.ActionsSucceeded);
        AddMetric("Duration (ms)", rA.DurationMs, rB.DurationMs);

        void AddMetric(string name, long va, long vb)
        {
            if (va != vb)
                diffs.Add(new MetricDiff(name, va, vb, vb - va));
        }

        return diffs;
    }

    private static IReadOnlyList<AiComparison> DiffAI(TraceRun runA, TraceRun runB)
    {
        var aiCallsA = runA.Trace.GetAICalls();
        var aiCallsB = runB.Trace.GetAICalls();

        var capabilities = aiCallsA.Select(c => c.Capability)
            .Concat(aiCallsB.Select(c => c.Capability))
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal);

        var comparisons = new List<AiComparison>();
        foreach (var capability in capabilities)
        {
            var callsA = aiCallsA.Where(c => c.Capability == capability).ToList();
            var callsB = aiCallsB.Where(c => c.Capability == capability).ToList();

            var avgA = callsA.Count > 0 ? callsA.Average(c => c.LatencyMs) : 0;
            var avgB = callsB.Count > 0 ? callsB.Average(c => c.LatencyMs) : 0;

            // Only actual differences are diffs (mirrors DiffSteps / DiffMetrics):
            // an identical capability (same average, same call counts) must not
            // flip RunDiff.HasDifferences and the CLI's exit-code contract.
            if (avgA != avgB || callsA.Count != callsB.Count)
            {
                comparisons.Add(new AiComparison(
                    capability, avgA, avgB, avgB - avgA, callsA.Count, callsB.Count));
            }
        }

        return comparisons;
    }

    private static string BuildConclusion(bool hasDifferences, TraceRun runA, TraceRun runB)
    {
        if (!hasDifferences)
            return "No behavioral differences detected between runs.";

        var statusA = runA.Status;
        var statusB = runB.Status;
        if (statusA == "success" && statusB != "success")
            return $"Regression: run A was {statusA}, run B is {statusB}.";
        if (statusA != "success" && statusB == "success")
            return $"Improvement: run A was {statusA}, run B is {statusB}.";

        return "Behavioral differences detected between runs.";
    }
}
