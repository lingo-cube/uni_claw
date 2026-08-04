using UniClaw.Core.Observability;
using UniClaw.Host.Analysis;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool;

public sealed record class DiagnoseVerdict(
    string Cause,
    string? FailingStep,
    string Summary,
    string Confidence);

public sealed record class DiagnoseEvidence(
    string Type,
    string Description,
    string? StepNumber);

public sealed record class DiagnoseResult(
    string RunId,
    string Status,
    RunContext Run,
    DiagnoseVerdict Verdict,
    IReadOnlyList<DiagnoseEvidence> Evidence,
    IReadOnlyList<string> Suggestions,
    ArtifactPaths ArtifactPaths);

public sealed record class RunContext(
    string RunId,
    string TaskId,
    string Purpose,
    RunSystemInfo? System,
    RunMachineInfo? Machine);

public sealed record class ArtifactPaths(
    string? ManifestPath,
    string? ResultPath,
    string TracePath,
    IReadOnlyList<string> ScreenshotPaths);

public static class DiagnoseEngine
{
    private const int MaxEvidence = 5;

    /// <summary>
    /// Rule engine shared by the `diagnose` command and the TUI (design D7 — one
    /// conclusion source). TraceTool adds only aggregation rules; the error-loop
    /// detection itself is delegated to the Host <see cref="ErrorLoopAnalyzer"/>
    /// (design D3), evaluated offline over the replayed span tree with a null
    /// recorder — pure detection, no span emission.
    /// </summary>
    public static async Task<DiagnoseResult> DiagnoseAsync(
        TraceRun run,
        CancellationToken ct = default)
    {
        var trace = run.Trace;
        var result = run.Result;

        // Build run context
        var context = new RunContext(
            run.RunId, run.TaskId, run.Purpose,
            run.SystemInfo, run.MachineInfo);

        // Gather evidence
        var evidence = new List<DiagnoseEvidence>();
        var suggestions = new List<string>();

        // Check for errors
        var errors = trace.GetErrors();
        if (errors.Count > 0)
        {
            foreach (var err in errors.Take(MaxEvidence))
            {
                evidence.Add(new DiagnoseEvidence(
                    "error", $"{err.ErrorType}: {err.ErrorMessage}",
                    err.Context?.StepNumber?.ToString()));
            }
        }

        // Check for AI call failures
        var aiCalls = trace.GetAICalls();
        var failedAiCalls = aiCalls.Where(c => !c.Success).ToList();
        if (failedAiCalls.Count > 0)
        {
            var byCapability = failedAiCalls
                .GroupBy(c => c.Capability)
                .Select(g => $"{g.Key}: {g.Count()} failures");
            evidence.Add(new DiagnoseEvidence(
                "ai_call_failures",
                string.Join("; ", byCapability),
                null));
            suggestions.Add("Check AI provider credentials and rate limits.");
        }

        // Check for timeline gaps (large gaps between steps)
        var steps = trace.GetSpansByType(SpanTypes.EngineStep);
        if (steps.Count >= 2)
        {
            for (var i = 1; i < steps.Count; i++)
            {
                var previousEnd = steps[i - 1].EndTime;
                if (previousEnd.HasValue
                    && steps[i].StartTime > previousEnd.Value.AddSeconds(30))
                {
                    evidence.Add(new DiagnoseEvidence(
                        "timeline_gap",
                        $"Large gap between step {i} and step {i + 1}",
                        (i + 1).ToString()));
                }
            }
        }

        // Host analyzer reuse (design D3): ErrorLoopAnalyzer over the replayed span
        // tree. A null recorder yields verdicts without emitting analyze.error_loop
        // spans — the offline read-only mode. Only terminating error-loop verdicts
        // (stuck_in_error_loop / skip_rate_too_high) override the cause; "observe"
        // and null (analyzer failure) leave the result.json completionReason intact.
        var errorLoopVerdict = await new ErrorLoopAnalyzer(null)
            .EvaluateAsync(trace, ct);

        // Determine cause from result
        var cause = result?.CompletionReason ?? "unknown";
        var status = result?.Status ?? "unknown";
        string? failingStep = null;

        if (errorLoopVerdict is { ShouldTerminate: true } verdict
            && verdict.Reason is "stuck_in_error_loop" or "skip_rate_too_high")
        {
            cause = "error_loop_stuck";
            var (longestRun, lastConsecutiveStep) =
                LongestConsecutiveAllSkippedRun(trace, steps);

            if (verdict.Reason == "stuck_in_error_loop")
            {
                failingStep = $"Step at index {lastConsecutiveStep}";
                evidence.Add(new DiagnoseEvidence(
                    "error_loop",
                    $"Error loop: {longestRun} consecutive all-skipped steps",
                    lastConsecutiveStep.ToString()));
            }
            else
            {
                var skipped = trace.GetSpansByType(SpanTypes.EntrySkipped).Count;
                var visited = trace.GetSpansByType(SpanTypes.EntryVisited).Count;
                var lastSkippedStep = LastSkippedStepIndex(trace, steps);
                if (lastSkippedStep > 0)
                    failingStep = $"Step at index {lastSkippedStep}";
                evidence.Add(new DiagnoseEvidence(
                    "error_loop",
                    $"Error loop: skipped={skipped} vs visited={visited} "
                    + $"(rate > {ErrorLoopAnalyzer.SkipRateMultiplier}x)",
                    lastSkippedStep > 0 ? lastSkippedStep.ToString() : null));
            }
        }

        // VerificationAnalyzer classification from run artifacts (design D3):
        // issue fingerprints recorded in result.json are passed through as evidence.
        // trace-issue-evidence (D-3): the Host never backfills result.json's
        // issueFingerprints (D-192 writes the real reason into issues.jsonl
        // instead), so when the result carries no fingerprints we fall back to
        // the first issue record with a usable fingerprint. Non-empty result
        // fingerprints win — issues are never duplicated.
        if (result is not null && !result.IssueFingerprints.IsDefaultOrEmpty)
        {
            evidence.Add(new DiagnoseEvidence(
                "issue_fingerprints",
                string.Join("; ", result.IssueFingerprints),
                null));
        }
        else if (run.Issues.Count > 0)
        {
            var issue = run.Issues.FirstOrDefault(
                i => !string.IsNullOrWhiteSpace(i.Fingerprint));
            if (issue is not null)
            {
                evidence.Add(new DiagnoseEvidence(
                    "issue_fingerprints",
                    $"issues.jsonl: {issue.Fingerprint} — {issue.Summary}",
                    null));
            }
        }

        var summary = status switch
        {
            "success" => "Run completed successfully.",
            "failure" => $"Run failed: {cause}",
            "incomplete" => "Run did not complete.",
            "blocked" => "Run was blocked.",
            _ => $"Run status: {status}"
        };

        var confidence = evidence.Count > 0 ? "medium" : "low";

        // Find failing step if any
        if (failingStep == null)
        {
            var lastStep = steps.LastOrDefault();
            if (lastStep != null && status != "success")
                failingStep = $"Step at index {steps.Count}";
        }

        var verdictResult = new DiagnoseVerdict(cause, failingStep, summary, confidence);

        // Build artifact paths
        var artifactPaths = new ArtifactPaths(
            Path.Combine(run.RunDir, "manifest.json"),
            Path.Combine(run.RunDir, "result.json"),
            Path.Combine(run.RunDir, result?.TracePath ?? "trace/trace.jsonl"),
            run.StepAssets
                .Where(s => s.HasScreenshotBefore || s.HasScreenshotAfter)
                .Select(s => s.Directory)
                .ToList());

        // Limit evidence
        var boundedEvidence = evidence.Take(MaxEvidence).ToList();

        return new DiagnoseResult(
            run.RunId, status, context, verdictResult, boundedEvidence,
            suggestions, artifactPaths);
    }

    /// <summary>
    /// Length of the longest run of consecutive engine.step spans (in insertion
    /// order) whose children are ALL entry.skipped, plus the 1-based index of the
    /// last step in that run. Mirrors ErrorLoopAnalyzer's stuck rule — used only to
    /// position the failing step and describe the evidence, never to decide the verdict.
    /// </summary>
    private static (int LongestRun, int LastStepIndex) LongestConsecutiveAllSkippedRun(
        ITraceQuery trace,
        IReadOnlyList<TraceSpan> steps)
    {
        var longest = 0;
        var lastStepIndex = 0;
        var current = 0;

        for (var i = 0; i < steps.Count; i++)
        {
            var children = trace.GetChildSpans(steps[i].SpanId);
            var allChildrenSkipped = children.Count > 0
                && children.All(c => c.SpanType == SpanTypes.EntrySkipped);

            current = allChildrenSkipped ? current + 1 : 0;
            if (current > longest)
            {
                longest = current;
                lastStepIndex = i + 1;
            }
        }

        return (longest, lastStepIndex);
    }

    /// <summary>
    /// 1-based index of the last engine.step span that has at least one
    /// entry.skipped child; 0 when no step skipped anything.
    /// </summary>
    private static int LastSkippedStepIndex(
        ITraceQuery trace,
        IReadOnlyList<TraceSpan> steps)
    {
        for (var i = steps.Count - 1; i >= 0; i--)
        {
            if (trace.GetChildSpans(steps[i].SpanId)
                .Any(c => c.SpanType == SpanTypes.EntrySkipped))
            {
                return i + 1;
            }
        }

        return 0;
    }
}
