using System.Diagnostics;
using System.Text;
using Terminal.Gui;
using UniClaw.Core.Observability;

namespace UniClaw.TraceTool;

/// <summary>
/// Interactive Terminal.Gui browser over a single run directory (trace-analyzer task 6).
/// Thin presentation layer only: data comes from TraceRun, conclusions from DiagnoseEngine —
/// the same rule engine the `diagnose` CLI command uses (design D7, one conclusion source).
/// </summary>
public static class InteractiveTui
{
    private const int SlowStepMs = 5000;

    public static async Task<int> RunAsync(string? runDir, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runDir))
        {
            await Console.Error.WriteLineAsync("trace: error: --run is required.");
            return 2;
        }

        // Task 6.3: refuse to start when there is no real terminal (TERM=dumb or non-TTY).
        if (Console.IsOutputRedirected
            || Environment.GetEnvironmentVariable("TERM") == "dumb")
        {
            await Console.Error.WriteLineAsync(
                "Interactive TUI requires a real terminal. TERM=dumb or non-TTY detected.");
            return 2;
        }

        // Load the run
        TraceRun run;
        try
        {
            run = await TraceRunLoader.LoadAsync(runDir, cancellationToken);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to load run: {ex.Message}");
            return 2;
        }

        // Get engine steps
        var steps = run.Trace.GetSpansByType(SpanTypes.EngineStep);
        if (steps.Count == 0)
        {
            await Console.Error.WriteLineAsync("No spans found in trace.");
            return 3;
        }

        // Run the TUI
        Application.Init();
        try
        {
            var top = Application.Top;

            // Create menu bar
            var menu = new MenuBar(new MenuBarItem[]
            {
                new MenuBarItem("_File", new MenuItem[]
                {
                    new MenuItem("_Quit", "Quit", () => Application.RequestStop(top)),
                }),
            });

            // Left panel: step list
            var stepList = new ListView
            {
                Width = Dim.Percent(40),
                Height = Dim.Fill(),
                AllowsMarking = false,
            };

            // Right panel: detail view
            var detailView = new TextView
            {
                X = Pos.Right(stepList),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = true,
            };

            // Status bar (Task 6.2: T timeline / R report / Enter screenshot / Q quit)
            var statusBar = new StatusBar(new StatusItem[]
            {
                new StatusItem(Key.Q, "~Q~uit", () => Application.RequestStop(top)),
                new StatusItem(Key.T, "~T~imeline", () => ShowTimeline(run)),
                new StatusItem(Key.R, "~R~eport", () => ShowDiagnosis(run, detailView)),
                new StatusItem(Key.Enter, "~Enter~ Screenshot", () => OpenScreenshot(run, stepList.SelectedItem)),
            });

            // Populate step list — slow steps (>5s) highlighted
            var stepLabels = steps.Select((s, i) =>
            {
                var duration = s.EndTime.HasValue ? $"{s.DurationMs:F0}ms" : "open";
                var marker = s.EndTime.HasValue && s.DurationMs > SlowStepMs ? " [SLOW]" : "";
                return $"Step {i + 1}: {duration} [{s.Status}]{marker}";
            }).ToList();
            stepList.SetSource(stepLabels);

            // Task 6.2: ↑↓ select, detail shown on selection change
            stepList.SelectedItemChanged += (args) =>
            {
                if (args.Item >= 0 && args.Item < steps.Count)
                    ShowStepDetail(steps[args.Item], run, detailView);
            };

            // Populate the detail panel for the initially selected step
            ShowStepDetail(steps[0], run, detailView);

            // Build the window
            var window = new Window("Trace Analyzer - Interactive");
            window.Add(stepList, detailView);
            top.Add(menu);
            top.Add(window);
            top.Add(statusBar);

            Application.Run();
        }
        finally
        {
            Application.Shutdown();
        }

        return 0;
    }

    private static void ShowStepDetail(TraceSpan step, TraceRun run, TextView detailView)
    {
        var text = new StringBuilder();
        text.AppendLine($"Step: {step.SpanName}");
        text.AppendLine($"Status: {step.Status}");
        text.AppendLine($"Duration: {(step.EndTime.HasValue ? $"{step.DurationMs:F0}ms" : "open")}");
        text.AppendLine($"Start: {step.StartTime:HH:mm:ss.fff}");
        text.AppendLine($"End: {(step.EndTime.HasValue ? step.EndTime.Value.ToString("HH:mm:ss.fff") : "-")}");
        text.AppendLine();

        // Find AI calls for this step: correlated by StepSpanId, falling back to StepNumber
        // (AI calls recorded before step-span linkage carry StepNumber only).
        var stepNumber = step.Context?.StepNumber;
        var aiCalls = run.Trace.GetAICalls()
            .Where(ai => ai.Context?.StepSpanId == step.SpanId
                || (ai.Context?.StepSpanId == null && ai.Context?.StepNumber == stepNumber))
            .ToList();

        if (aiCalls.Count > 0)
        {
            text.AppendLine("--- AI Calls ---");
            foreach (var ai in aiCalls)
            {
                text.AppendLine(
                    $"  {ai.Capability}: {(ai.Success ? "OK" : "FAIL")} {ai.LatencyMs:F0}ms ({ai.ProviderId})");
            }
        }
        else
        {
            text.AppendLine("No AI calls for this step.");
        }

        // Find screenshots for this step (Task 6.3 opens them via the system viewer)
        var stepAssets = run.StepAssets;
        var asset = stepNumber != null
            ? stepAssets.FirstOrDefault(a => a.StepNumber == stepNumber.Value)
            : null;
        if (asset != null && (asset.HasScreenshotBefore || asset.HasScreenshotAfter))
        {
            text.AppendLine();
            text.AppendLine("--- Assets ---");
            if (asset.HasScreenshotBefore) text.AppendLine($"  Before: {asset.ScreenshotBeforePath}");
            if (asset.HasScreenshotAfter) text.AppendLine($"  After: {asset.ScreenshotAfterPath}");
            text.AppendLine("  (Enter: open in system viewer)");
        }

        detailView.Text = text.ToString();
    }

    private static void ShowTimeline(TraceRun run)
    {
        var steps = run.Trace.GetSpansByType(SpanTypes.EngineStep);
        var text = new StringBuilder();
        text.AppendLine("Timeline:");
        foreach (var step in steps)
        {
            var duration = step.EndTime.HasValue ? $"{step.DurationMs:F0}ms" : "open";
            text.AppendLine($"  {step.SpanName}: {duration} [{step.Status}]");
        }
        MessageBox.Query("Timeline", text.ToString(), "OK");
    }

    private static void ShowDiagnosis(TraceRun run, TextView detailView)
    {
        // Task 6.4: same rule engine as the `diagnose` CLI command — one conclusion source.
        // Terminal.Gui callbacks are void, so block synchronously. DiagnoseAsync is pure
        // in-memory here (ErrorLoopAnalyzer runs with a null recorder — no IO, no
        // sync-context postbacks), so GetResult cannot deadlock.
        var result = DiagnoseEngine.DiagnoseAsync(run).GetAwaiter().GetResult();
        var text = new StringBuilder();
        text.AppendLine("Diagnosis:");
        text.AppendLine();
        text.AppendLine($"Cause: {result.Verdict.Cause}");
        text.AppendLine($"Summary: {result.Verdict.Summary}");
        text.AppendLine($"Confidence: {result.Verdict.Confidence}");
        if (result.Verdict.FailingStep != null)
            text.AppendLine($"Failing step: {result.Verdict.FailingStep}");

        if (result.Evidence.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("--- Evidence ---");
            foreach (var ev in result.Evidence)
                text.AppendLine($"  [{ev.Type}] {ev.Description}");
        }

        if (result.Suggestions.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("--- Suggestions ---");
            foreach (var suggestion in result.Suggestions)
                text.AppendLine($"  - {suggestion}");
        }

        detailView.Text = text.ToString();
    }

    /// <summary>
    /// Task 6.3: open the step's screenshot (after preferred, before fallback) with the
    /// system viewer. Non-fatal when the viewer cannot be launched.
    /// </summary>
    private static void OpenScreenshot(TraceRun run, int selectedIndex)
    {
        var steps = run.Trace.GetSpansByType(SpanTypes.EngineStep);
        if (selectedIndex < 0 || selectedIndex >= steps.Count)
            return;

        var stepNumber = steps[selectedIndex].Context?.StepNumber;
        var asset = stepNumber != null
            ? run.StepAssets.FirstOrDefault(a => a.StepNumber == stepNumber.Value)
            : null;
        if (asset == null)
            return;

        var path = asset.HasScreenshotAfter
            ? asset.ScreenshotAfterPath
            : asset.HasScreenshotBefore ? asset.ScreenshotBeforePath : null;
        if (path == null)
            return;

        // Open with system viewer
        try
        {
            if (OperatingSystem.IsMacOS())
                Process.Start("open", path);
            else if (OperatingSystem.IsLinux())
                Process.Start("xdg-open", path);
            else if (OperatingSystem.IsWindows())
                Process.Start("explorer", path);
        }
        catch
        {
            // Non-fatal — screenshot viewing is best-effort
        }
    }
}
