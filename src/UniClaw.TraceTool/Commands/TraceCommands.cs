using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Rendering;
using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool.Commands;

/// <summary>
/// Stable exit-code contract for the trace CLI (trace-analyzer-cli spec §exit-codes).
/// Analyze family: 0 = success; 1 = diff detected behavioral differences; 2 = usage
/// error / run dir not found; 3 = empty trace (no spans).
/// verify/watch: 0 = verified · 1 = not_verified · 2 = usage/dir error · 3 = evidence
/// missing (trace-based-validation spec §exit-codes). <see cref="NotVerified"/> shares
/// value 1 with <see cref="DiffDetected"/>; <see cref="EmptyTrace"/> doubles as the
/// evidence-missing code.
/// </summary>
public static class TraceExitCodes
{
    public const int Success = 0;
    public const int DiffDetected = 1;
    public const int NotVerified = 1;  // verify/watch: verdict was not_verified
    public const int UsageError = 2;
    public const int EmptyTrace = 3;   // analyze: empty trace · verify: evidence missing
}

/// <summary>
/// Command handlers for the trace CLI. Each handler owns its full execution path:
/// validate options, load the run, compute, render. All handlers return a
/// <see cref="TraceExitCodes"/> value; Program.cs only wires System.CommandLine options
/// to these methods (beta4 has no global options and no Task&lt;int&gt; handler overloads,
/// so exit codes flow through InvocationContext.ExitCode).
/// </summary>
public static class TraceCommands
{
    // ── Shared JSON contract (Task 5.1) ──────────────────────────────

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Emit the machine-readable contract: a single JSON document on stdout wrapped with
    /// schemaVersion ("1"). Logs and warnings go to stderr only — stdout carries the
    /// document and nothing else.
    /// </summary>
    public static void WriteJson(object data)
    {
        var wrapped = new { schemaVersion = "1", data };
        Console.WriteLine(JsonSerializer.Serialize(wrapped, JsonWriteOptions));
    }

    private static void Error(string message) =>
        Console.Error.WriteLine($"trace: error: {message}");

    private static void Warn(string message) =>
        Console.Error.WriteLine($"trace: warning: {message}");

    /// <summary>
    /// Table output auto-switches to JSON when stdout is not a TTY (piped/redirected):
    /// non-TTY output must be decoration-free (trace-analyzer-cli spec §non-tty).
    /// </summary>
    private static bool UseJson(string format) =>
        string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
        || Console.IsOutputRedirected;

    /// <summary>
    /// Load a run directory for --run commands, mapping failures onto the exit-code
    /// contract: missing directory or load failure → UsageError (2); zero spans in the
    /// trace → EmptyTrace (3). Returns (run, Success) on the happy path.
    /// </summary>
    private static async Task<(TraceRun? Run, int ExitCode)> LoadRunAsync(string runDir)
    {
        if (!Directory.Exists(runDir))
        {
            Error($"run directory not found: {runDir}");
            return (null, TraceExitCodes.UsageError);
        }

        TraceRun run;
        try
        {
            run = await TraceRunLoader.LoadAsync(runDir);
        }
        catch (Exception ex)
        {
            Error($"failed to load run '{runDir}': {ex.Message}");
            return (null, TraceExitCodes.UsageError);
        }

        if (run.Trace.GetAllSpans().Count == 0)
        {
            Error($"no spans found in trace (empty trace): {runDir}");
            return (null, TraceExitCodes.EmptyTrace);
        }

        return (run, TraceExitCodes.Success);
    }

    // ── Task 4.1: list ──────────────────────────────────────────────

    /// <summary>
    /// Scan a directory tree for run directories (dirs containing manifest.json),
    /// apply --status / --task-id filters, sort by timestamp descending, and print at
    /// most --limit rows as a table or JSON document.
    /// </summary>
    public static async Task<int> ListAsync(
        string? dir,
        string? status,
        string? taskId,
        int limit,
        string format)
    {
        var root = Path.GetFullPath(dir ?? "artifacts/runs");
        if (!Directory.Exists(root))
        {
            Error($"run directory not found: {root}");
            return TraceExitCodes.UsageError;
        }

        var entries = new List<RunEntry>();
        try
        {
            foreach (var manifestPath in Directory.EnumerateFiles(
                         root, "manifest.json", SearchOption.AllDirectories))
            {
                var runDir = Path.GetDirectoryName(manifestPath);
                if (runDir == null)
                    continue;
                var entry = ReadRunEntry(runDir);
                if (entry == null)
                    continue;
                if (status != null
                    && !string.Equals(entry.Status, status, StringComparison.Ordinal))
                    continue;
                if (taskId != null
                    && !string.Equals(entry.TaskId, taskId, StringComparison.Ordinal))
                    continue;
                entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Error($"failed to scan '{root}': {ex.Message}");
            return TraceExitCodes.UsageError;
        }

        var runs = entries
            .OrderByDescending(e => e.Timestamp ?? DateTimeOffset.MinValue)
            .Take(Math.Max(0, limit))
            .ToList();

        if (UseJson(format))
        {
            WriteJson(new { runs });
            return TraceExitCodes.Success;
        }

        var table = new Table();
        table.AddColumn("RunId");
        table.AddColumn("Scenario");
        table.AddColumn("Status");
        table.AddColumn("Duration (ms)");
        table.AddColumn("TaskId");
        table.AddColumn("Timestamp");
        foreach (var run in runs)
        {
            table.AddRow(
                run.RunId,
                run.ScenarioId,
                run.Status,
                run.DurationMs.ToString(),
                run.TaskId ?? "-",
                run.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
        }
        AnsiConsole.Write(table);
        return TraceExitCodes.Success;
    }

    /// <summary>Lightweight run entry for list output (parse errors → stderr warning, skip).</summary>
    private sealed record class RunEntry(
        string RunId,
        string ScenarioId,
        string Status,
        long DurationMs,
        string? TaskId,
        DateTimeOffset? Timestamp);

    /// <summary>
    /// Read manifest.json + result.json via JsonDocument (lenient: a missing or
    /// unparseable file degrades to "unknown" / warning instead of aborting the scan).
    /// </summary>
    private static RunEntry? ReadRunEntry(string runDir)
    {
        var manifest = ReadJsonObject(Path.Combine(runDir, "manifest.json"));
        var result = ReadJsonObject(Path.Combine(runDir, "result.json"));
        if (manifest == null && result == null)
            return null;

        string? GetProp(JsonElement? element, string name) =>
            element is { } e
            && e.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        var runId = GetProp(manifest, "runId")
            ?? GetProp(result, "runId")
            ?? Path.GetFileName(runDir);
        var scenarioId = GetProp(manifest, "scenarioId") ?? "unknown";
        var status = GetProp(result, "status") ?? "unknown";
        var durationMs = result is { } resultElement
            && resultElement.TryGetProperty("durationMs", out var durationValue)
            && durationValue.ValueKind == JsonValueKind.Number
                ? durationValue.GetInt64()
                : 0;
        var taskId = GetProp(manifest, "taskId");
        var timestamp = ParseTimestamp(GetProp(manifest, "startedAt"))
            ?? ParseTimestamp(GetProp(result, "updatedAt"));

        return new RunEntry(runId, scenarioId, status, durationMs, taskId, timestamp);
    }

    private static JsonElement? ReadJsonObject(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            Warn($"skipping unreadable {path}: {ex.Message}");
            return null;
        }
    }

    private static DateTimeOffset? ParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var timestamp)
            ? timestamp
            : null;

    // ── Task 4.2: timeline ──────────────────────────────────────────

    /// <summary>
    /// Render the engine.step timeline with per-step AI call counts (slow steps
    /// highlighted beyond --threshold) plus the AI latency distribution per capability
    /// (min / avg / p50 / p95 / max).
    /// </summary>
    public static async Task<int> TimelineAsync(
        string? runDir,
        int threshold,
        string format)
    {
        if (string.IsNullOrWhiteSpace(runDir))
        {
            Error("--run is required.");
            return TraceExitCodes.UsageError;
        }

        var (run, exitCode) = await LoadRunAsync(runDir);
        if (run == null)
            return exitCode;

        var aiCalls = run.Trace.GetAICalls();
        var stepRows = run.Trace
            .GetSpansByType(SpanTypes.EngineStep)
            .Select((span, index) => new StepRow(
                index + 1,
                span.SpanName,
                span.DurationMs,
                span.Status,
                aiCalls.Count(call =>
                    call.Context?.StepSpanId == span.SpanId
                    || call.Context?.StepNumber == index + 1)))
            .ToList();
        var aiLatency = aiCalls
            .GroupBy(call => call.Capability)
            .Select(group => new AiLatencyEntry(group.Key, group.Select(call => call.LatencyMs)))
            .OrderBy(entry => entry.Capability, StringComparer.Ordinal)
            .ToList();

        if (UseJson(format))
        {
            WriteJson(new { runId = run.RunId, status = run.Status, steps = stepRows, aiLatency });
            return TraceExitCodes.Success;
        }

        var stepTable = new Table { Title = new TableTitle("Step Timeline") };
        stepTable.AddColumn("Step");
        stepTable.AddColumn("Name");
        stepTable.AddColumn("Duration (ms)");
        stepTable.AddColumn("Status");
        stepTable.AddColumn("AI Calls");
        foreach (var row in stepRows)
        {
            IRenderable durationCell = threshold > 0 && row.DurationMs > threshold
                ? new Markup($"[red bold]{row.DurationMs:F0}[/]")
                : new Text($"{row.DurationMs:F0}");
            stepTable.AddRow(
                new Text(row.StepNumber.ToString()),
                new Text(row.Name ?? "-"),
                durationCell,
                new Text(row.Status),
                new Text(row.AiCallCount.ToString()));
        }
        AnsiConsole.Write(stepTable);

        var aiTable = new Table { Title = new TableTitle("AI Latency Distribution") };
        aiTable.AddColumn("Capability");
        aiTable.AddColumn("Calls");
        aiTable.AddColumn("Min (ms)");
        aiTable.AddColumn("Avg (ms)");
        aiTable.AddColumn("P50 (ms)");
        aiTable.AddColumn("P95 (ms)");
        aiTable.AddColumn("Max (ms)");
        foreach (var entry in aiLatency)
        {
            aiTable.AddRow(
                entry.Capability,
                entry.Count.ToString(),
                entry.Min.ToString("F1"),
                entry.Avg.ToString("F1"),
                entry.P50.ToString("F1"),
                entry.P95.ToString("F1"),
                entry.Max.ToString("F1"));
        }
        AnsiConsole.Write(aiTable);
        return TraceExitCodes.Success;
    }

    /// <summary>One timeline row: an engine.step span plus its AI call count.</summary>
    public sealed record class StepRow(
        int StepNumber,
        string? Name,
        double DurationMs,
        string Status,
        int AiCallCount);

    /// <summary>AI latency distribution for one capability (min / avg / p50 / p95 / max).</summary>
    public sealed record class AiLatencyEntry
    {
        public string Capability { get; }
        public int Count { get; }
        public double Min { get; }
        public double Avg { get; }
        public double P50 { get; }
        public double P95 { get; }
        public double Max { get; }

        public AiLatencyEntry(string capability, IEnumerable<double> latencies)
        {
            Capability = capability;
            var sorted = latencies.OrderBy(value => value).ToArray();
            Count = sorted.Length;
            Min = sorted.Length == 0 ? 0 : sorted[0];
            Max = sorted.Length == 0 ? 0 : sorted[^1];
            Avg = sorted.Length == 0 ? 0 : sorted.Average();
            P50 = Percentile(sorted, 50);
            P95 = Percentile(sorted, 95);
        }

        /// <summary>Nearest-rank percentile over pre-sorted values.</summary>
        private static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 0)
                return 0;
            var rank = Math.Max(1, (int)Math.Ceiling(p / 100.0 * sorted.Length));
            return sorted[Math.Min(rank, sorted.Length) - 1];
        }
    }

    // ── Task 4.3: diagnose ──────────────────────────────────────────

    /// <summary>
    /// Run the rule engine and print the verdict, bounded evidence (≤ 5 entries),
    /// suggestions, and artifact paths. JSON output carries the full DiagnoseResult:
    /// runId, status, run context, verdict, evidence, suggestions, artifactPaths.
    /// </summary>
    public static async Task<int> DiagnoseAsync(string? runDir, string format)
    {
        if (string.IsNullOrWhiteSpace(runDir))
        {
            Error("--run is required.");
            return TraceExitCodes.UsageError;
        }

        var (run, exitCode) = await LoadRunAsync(runDir);
        if (run == null)
            return exitCode;

        var diagnosis = await DiagnoseEngine.DiagnoseAsync(run);

        if (UseJson(format))
        {
            WriteJson(diagnosis);
            return TraceExitCodes.Success;
        }

        AnsiConsole.MarkupLine(
            $"[bold]Run:[/] {Markup.Escape(diagnosis.RunId)}    "
            + $"[bold]Status:[/] {Markup.Escape(diagnosis.Status)}");
        AnsiConsole.MarkupLine(
            $"[bold]Verdict:[/] cause={Markup.Escape(diagnosis.Verdict.Cause)}  "
            + $"confidence={Markup.Escape(diagnosis.Verdict.Confidence)}");
        if (!string.IsNullOrEmpty(diagnosis.Verdict.FailingStep))
            AnsiConsole.MarkupLine($"[bold]Failing step:[/] {Markup.Escape(diagnosis.Verdict.FailingStep)}");
        AnsiConsole.MarkupLine($"[bold]Summary:[/] {Markup.Escape(diagnosis.Verdict.Summary)}");

        var evidenceTable = new Table { Title = new TableTitle("Evidence") };
        evidenceTable.AddColumn("Type");
        evidenceTable.AddColumn("Step");
        evidenceTable.AddColumn("Description");
        foreach (var evidence in diagnosis.Evidence)
            evidenceTable.AddRow(evidence.Type, evidence.StepNumber ?? "-", evidence.Description);
        AnsiConsole.Write(evidenceTable);

        if (diagnosis.Suggestions.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Suggestions:[/]");
            foreach (var suggestion in diagnosis.Suggestions)
                AnsiConsole.MarkupLine($"  - {Markup.Escape(suggestion)}");
        }

        AnsiConsole.MarkupLine("[bold]Artifacts:[/]");
        if (diagnosis.ArtifactPaths.ManifestPath is { } manifestPath)
            AnsiConsole.MarkupLine($"  manifest:  {Markup.Escape(manifestPath)}");
        if (diagnosis.ArtifactPaths.ResultPath is { } resultPath)
            AnsiConsole.MarkupLine($"  result:    {Markup.Escape(resultPath)}");
        AnsiConsole.MarkupLine($"  trace:     {Markup.Escape(diagnosis.ArtifactPaths.TracePath)}");
        foreach (var screenshot in diagnosis.ArtifactPaths.ScreenshotPaths)
            AnsiConsole.MarkupLine($"  screenshot: {Markup.Escape(screenshot)}");
        return TraceExitCodes.Success;
    }

    // ── Task 4.4: diff ──────────────────────────────────────────────

    /// <summary>
    /// Compare two runs via RunDiffer: step diffs, metric diffs, AI comparison, and a
    /// one-line conclusion. Behavioral differences → exit code 1.
    /// </summary>
    public static async Task<int> DiffAsync(
        string? runADir,
        string? runBDir,
        string format)
    {
        if (string.IsNullOrWhiteSpace(runADir))
        {
            Error("--run-a is required.");
            return TraceExitCodes.UsageError;
        }
        if (string.IsNullOrWhiteSpace(runBDir))
        {
            Error("--run-b is required.");
            return TraceExitCodes.UsageError;
        }

        var (runA, exitA) = await LoadRunAsync(runADir);
        if (runA == null)
            return exitA;
        var (runB, exitB) = await LoadRunAsync(runBDir);
        if (runB == null)
            return exitB;

        var diff = RunDiffer.Diff(runA, runB);

        if (UseJson(format))
        {
            WriteJson(diff);
            return diff.HasDifferences ? TraceExitCodes.DiffDetected : TraceExitCodes.Success;
        }

        var stepTable = new Table { Title = new TableTitle("Step Diffs") };
        stepTable.AddColumn("Step");
        stepTable.AddColumn("In A");
        stepTable.AddColumn("In B");
        stepTable.AddColumn("Difference");
        foreach (var stepDiff in diff.StepDiffs)
        {
            stepTable.AddRow(
                stepDiff.StepLabel,
                stepDiff.PresentInA ? "yes" : "-",
                stepDiff.PresentInB ? "yes" : "-",
                stepDiff.Difference ?? "-");
        }
        AnsiConsole.Write(stepTable);

        var metricTable = new Table { Title = new TableTitle("Metric Diffs") };
        metricTable.AddColumn("Metric");
        metricTable.AddColumn("A");
        metricTable.AddColumn("B");
        metricTable.AddColumn("Delta");
        foreach (var metricDiff in diff.MetricDiffs)
        {
            metricTable.AddRow(
                metricDiff.Metric,
                metricDiff.ValueA.ToString(),
                metricDiff.ValueB.ToString(),
                $"{metricDiff.Delta:+0;-0;0}");
        }
        AnsiConsole.Write(metricTable);

        var aiTable = new Table { Title = new TableTitle("AI Comparison") };
        aiTable.AddColumn("Capability");
        aiTable.AddColumn("Avg A (ms)");
        aiTable.AddColumn("Avg B (ms)");
        aiTable.AddColumn("Delta (ms)");
        aiTable.AddColumn("Count A");
        aiTable.AddColumn("Count B");
        foreach (var comparison in diff.AiComparisons)
        {
            aiTable.AddRow(
                comparison.Capability,
                comparison.AvgLatencyA.ToString("F1"),
                comparison.AvgLatencyB.ToString("F1"),
                comparison.DeltaMs.ToString("F1"),
                comparison.CountA.ToString(),
                comparison.CountB.ToString());
        }
        AnsiConsole.Write(aiTable);

        AnsiConsole.MarkupLine(diff.HasDifferences
            ? $"[yellow]{Markup.Escape(diff.Conclusion)}[/]"
            : $"[green]{Markup.Escape(diff.Conclusion)}[/]");
        return diff.HasDifferences ? TraceExitCodes.DiffDetected : TraceExitCodes.Success;
    }

    // ── Task 4.5: report ────────────────────────────────────────────

    /// <summary>
    /// Export a run as a Markdown report (run summary, timeline table, diagnosis
    /// summary, embedded Mermaid sequence diagram of engine.step spans) or as JSON.
    /// Writes to --out when given, otherwise to stdout.
    /// </summary>
    public static async Task<int> ReportAsync(string? runDir, string format, string? outPath)
    {
        if (string.IsNullOrWhiteSpace(runDir))
        {
            Error("--run is required.");
            return TraceExitCodes.UsageError;
        }

        var (run, exitCode) = await LoadRunAsync(runDir);
        if (run == null)
            return exitCode;

        var steps = run.Trace.GetSpansByType(SpanTypes.EngineStep);
        var diagnosis = await DiagnoseEngine.DiagnoseAsync(run);

        string content;
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var data = new
            {
                runId = run.RunId,
                status = run.Status,
                scenarioId = run.ScenarioId,
                taskId = run.TaskId,
                purpose = run.Purpose,
                durationMs = run.DurationMs,
                steps = steps.Select((span, index) => new StepRow(
                    index + 1, span.SpanName, span.DurationMs, span.Status, 0)).ToList(),
                diagnosis,
            };
            content = JsonSerializer.Serialize(
                new { schemaVersion = "1", data }, JsonWriteOptions) + Environment.NewLine;
        }
        else
        {
            content = BuildMarkdownReport(run, steps, diagnosis);
        }

        return WriteReportOutput(outPath, content);
    }

    private static int WriteReportOutput(string? outPath, string content)
    {
        if (string.IsNullOrWhiteSpace(outPath))
        {
            Console.Write(content);
            return TraceExitCodes.Success;
        }

        try
        {
            var fullPath = Path.GetFullPath(outPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, content);
            Console.Error.WriteLine($"trace: report written to {fullPath}");
            return TraceExitCodes.Success;
        }
        catch (Exception ex)
        {
            Error($"failed to write report '{outPath}': {ex.Message}");
            return TraceExitCodes.UsageError;
        }
    }

    private static string BuildMarkdownReport(
        TraceRun run,
        IReadOnlyList<TraceSpan> steps,
        DiagnoseResult diagnosis)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Trace Report — {run.RunId}");
        builder.AppendLine();
        builder.AppendLine("## Run Summary");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Status | {EscapeCell(run.Status)} |");
        builder.AppendLine($"| Scenario | {EscapeCell(run.ScenarioId)} |");
        builder.AppendLine($"| Task | {EscapeCell(run.TaskId)} |");
        builder.AppendLine($"| Purpose | {EscapeCell(run.Purpose)} |");
        builder.AppendLine($"| Device | {EscapeCell(run.DeviceSerial)} |");
        builder.AppendLine($"| Provider | {EscapeCell(run.ProviderId)} |");
        builder.AppendLine($"| App Package | {EscapeCell(run.AppPackage)} |");
        builder.AppendLine($"| Duration | {run.DurationMs} ms |");
        builder.AppendLine();
        builder.AppendLine("## Timeline");
        builder.AppendLine();
        builder.AppendLine("| Step | Name | Duration (ms) | Status |");
        builder.AppendLine("|---|---|---|---|");
        for (var i = 0; i < steps.Count; i++)
        {
            var span = steps[i];
            builder.AppendLine(
                $"| {i + 1} | {EscapeCell(span.SpanName)} | {span.DurationMs:F0} | {EscapeCell(span.Status)} |");
        }
        builder.AppendLine();
        builder.AppendLine("## Diagnosis Summary");
        builder.AppendLine();
        builder.AppendLine($"- **Cause**: {diagnosis.Verdict.Cause}");
        if (!string.IsNullOrEmpty(diagnosis.Verdict.FailingStep))
            builder.AppendLine($"- **Failing step**: {diagnosis.Verdict.FailingStep}");
        builder.AppendLine($"- **Summary**: {diagnosis.Verdict.Summary}");
        builder.AppendLine($"- **Confidence**: {diagnosis.Verdict.Confidence}");
        if (diagnosis.Suggestions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Suggestions");
            builder.AppendLine();
            foreach (var suggestion in diagnosis.Suggestions)
                builder.AppendLine($"- {suggestion}");
        }
        builder.AppendLine();
        builder.AppendLine("## Sequence Diagram");
        builder.AppendLine();
        builder.AppendLine("```mermaid");
        builder.AppendLine("sequenceDiagram");
        builder.AppendLine("    participant Engine as Engine");
        builder.AppendLine("    participant AI as AI Provider");
        for (var i = 0; i < steps.Count; i++)
        {
            var span = steps[i];
            builder.AppendLine(
                $"    Engine->>AI: step {i + 1}: {EscapeMermaid(span.SpanName)} ({span.Status})");
            builder.AppendLine($"    AI-->>Engine: {span.DurationMs:F0}ms");
        }
        builder.AppendLine("```");
        builder.AppendLine();
        return builder.ToString();
    }

    private static string EscapeCell(string? value) =>
        value?.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ") ?? "-";

    private static string EscapeMermaid(string? value) =>
        value?.Replace("\"", "'", StringComparison.Ordinal).ReplaceLineEndings(" ") ?? "-";

    // ── Task 3.5: verify / watch ────────────────────────────────────

    /// <summary>
    /// Verify a single run or batch of runs. For --run: verify one run (any status).
    /// For --dir: batch-verify pending runs only (idempotent — the status is re-read
    /// before writeback, so already-final runs are never overwritten).
    /// Exit: 0=verified · 1=not_verified · 2=usage/dir · 3=evidence_missing.
    /// </summary>
    public static async Task<int> VerifyAsync(
        string? runDir,
        string? dir,
        string? status,
        string? taskId,
        string format)
    {
        if (!string.IsNullOrWhiteSpace(runDir))
        {
            return await VerifyOneAsync(runDir, format);
        }

        if (!string.IsNullOrWhiteSpace(dir))
        {
            return await VerifyDirAsync(dir, status, taskId, format);
        }

        Error("either --run or --dir is required.");
        return TraceExitCodes.UsageError;
    }

    private static async Task<int> VerifyOneAsync(string runDir, string format)
    {
        if (!Directory.Exists(runDir))
        {
            Error($"run directory not found: {runDir}");
            return TraceExitCodes.UsageError;
        }

        VerificationInput input;
        try
        {
            input = await RunEvidenceLoader.LoadAsync(runDir);
        }
        catch (Exception ex)
        {
            Error($"failed to load run '{runDir}': {ex.Message}");
            return TraceExitCodes.UsageError;
        }

        var criteria = input.Criteria;
        var context = new VerificationContext
        {
            RunId = input.Run.RunId,
            Criteria = criteria,
            LastAnalysisRow = input.LastAnalysisRow,
            CompletionReason = input.CompletionReason,
            TargetActionExecuted = input.TargetActionExecuted,
            ExpectedPageIdentities = criteria?.ExpectedPageIdentities ?? [],
            Trace = (ITraceEventQuery)input.Run.Trace,
            Issues = input.Run.Issues,
        };

        var result = VerifyEngine.Verify(context);

        // Write back only if status was pending_verification (idempotent: the status
        // is re-read inside WriteBackResultAsync, so batch runs never clobber a final
        // verdict written by another process).
        if (string.Equals(input.Run.Status, "pending_verification", StringComparison.Ordinal))
        {
            await WriteBackResultAsync(runDir, result);
        }

        // Output
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(result);
        }
        else
        {
            PrintVerdict(result);
        }

        return result.Status switch
        {
            "success" => TraceExitCodes.Success,
            "failure" => TraceExitCodes.NotVerified,
            "evidence_missing" => TraceExitCodes.EmptyTrace,
            _ => TraceExitCodes.UsageError,
        };
    }

    /// <summary>
    /// Write the verify verdict back into result.json (status + completionReason).
    /// Only writes while the on-disk status is still pending_verification, and the
    /// write is atomic (tmp file + move) so a crash never leaves a torn result.json.
    /// </summary>
    private static async Task WriteBackResultAsync(string runDir, VerifyResult result)
    {
        var resultPath = Path.Combine(runDir, "result.json");

        // Re-read status before writeback (batch idempotency / multi-process guard).
        if (File.Exists(resultPath))
        {
            var currentJson = await File.ReadAllTextAsync(resultPath);
            using var doc = JsonDocument.Parse(currentJson);
            var currentStatus = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (!string.Equals(currentStatus, "pending_verification", StringComparison.Ordinal))
                return; // Already finalized — don't overwrite
        }

        // Preserve all existing fields, update the verdict fields.
        var resultData = File.Exists(resultPath)
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                await File.ReadAllTextAsync(resultPath))
            : [];
        var updated = new Dictionary<string, object>(resultData!
            .ToDictionary(kv => kv.Key, kv => (object)kv.Value))
        {
            ["status"] = result.Status,
            ["completionReason"] = result.Verdict.Cause,
        };
        var json = JsonSerializer.Serialize(updated, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });

        // Atomic write: tmp → move.
        var tmpPath = $"{resultPath}.tmp-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tmpPath, json);
        File.Move(tmpPath, resultPath, overwrite: true);
    }

    /// <summary>
    /// Batch-verify the run directories directly under a root directory. Only runs
    /// whose result.json status matches --status (default "pending", prefix-matching
    /// pending_verification) and whose manifest taskId matches --task-id are re-verified;
    /// already-final runs are left untouched (spec: batch-verify pending only).
    /// </summary>
    private static async Task<int> VerifyDirAsync(string dir, string? statusFilter, string? taskId, string format)
    {
        if (!Directory.Exists(dir))
        {
            Error($"directory not found: {dir}");
            return TraceExitCodes.UsageError;
        }

        var overallExit = TraceExitCodes.Success;

        foreach (var subdir in Directory.GetDirectories(dir))
        {
            var entry = ReadRunEntry(subdir);
            if (entry == null)
                continue; // Not a run directory (no manifest.json / result.json)

            if (statusFilter != null
                && !entry.Status.StartsWith(statusFilter, StringComparison.Ordinal))
                continue;
            if (taskId != null
                && !string.Equals(entry.TaskId, taskId, StringComparison.Ordinal))
                continue;

            var exit = await VerifyOneAsync(subdir, format);
            if (exit != TraceExitCodes.Success && overallExit == TraceExitCodes.Success)
                overallExit = exit;
        }

        return overallExit;
    }

    /// <summary>
    /// Watch a specific run-id: locate by leaf directory name, poll for a final
    /// result.json status (pending_verification first and foremost — the final state
    /// that guarantees assets are complete), auto-verify, and exit with verify's
    /// exit code. Polling also stops on any other final status (success/failure/…),
    /// which verifies report-only without writeback instead of hanging forever.
    /// </summary>
    public static async Task<int> WatchAsync(string runId, string dir, int intervalMs)
    {
        if (!Directory.Exists(dir))
        {
            Error($"directory not found: {dir}");
            return TraceExitCodes.UsageError;
        }

        // Locate run: leaf directory name == runId
        var matches = Directory.GetDirectories(dir)
            .Where(d => string.Equals(Path.GetFileName(d), runId, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 0)
        {
            Error($"run-id '{runId}' not found under '{dir}'.");
            return TraceExitCodes.UsageError;
        }

        if (matches.Length > 1)
        {
            Error($"multiple directories match run-id '{runId}'. Use an explicit --run path.");
            return TraceExitCodes.UsageError;
        }

        var runDir = matches[0];

        // Poll until a final status appears ("running" or a missing result.json means
        // the run is still executing).
        while (true)
        {
            var resultPath = Path.Combine(runDir, "result.json");
            if (File.Exists(resultPath))
            {
                var json = await File.ReadAllTextAsync(resultPath);
                using var doc = JsonDocument.Parse(json);
                var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
                if (status != null
                    && !string.Equals(status, "running", StringComparison.Ordinal))
                    break;
            }

            await Task.Delay(intervalMs);
        }

        // Auto-verify
        return await VerifyOneAsync(runDir, "json");
    }

    private static void PrintVerdict(VerifyResult result)
    {
        Console.WriteLine($"Run:     {result.RunId}");
        Console.WriteLine($"Status:  {result.Status}");
        Console.WriteLine($"Verdict: {result.Verdict.Cause} ({result.Verdict.Confidence})");
        Console.WriteLine($"Summary: {result.Verdict.Summary}");
    }
}
