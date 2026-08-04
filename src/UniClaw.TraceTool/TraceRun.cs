using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool;

/// <summary>
/// Single aggregate entry point for a run directory. Combines manifest, result,
/// trace query, step assets, and issue records (issues.jsonl). All subcommands
/// read through this type only.
/// </summary>
public sealed class TraceRun
{
    public string RunDir { get; }
    public RunManifest? Manifest { get; }
    public RunResult? Result { get; }
    public ITraceQuery Trace { get; }
    public IReadOnlyList<StepAsset> StepAssets { get; }

    /// <summary>
    /// D-192 issue records appended by the Host run (issues.jsonl). Empty when
    /// the run directory carries no issues.jsonl — never null.
    /// </summary>
    public IReadOnlyList<RunIssue> Issues { get; }

    public TraceRun(
        string runDir,
        RunManifest? manifest,
        RunResult? result,
        ITraceQuery trace,
        IReadOnlyList<StepAsset> stepAssets,
        IReadOnlyList<RunIssue> issues)
    {
        RunDir = runDir;
        Manifest = manifest;
        Result = result;
        Trace = trace;
        StepAssets = stepAssets;
        Issues = issues;
    }

    // ── Metadata helpers — missing → "unknown" ──────────────

    public string RunId => Manifest?.RunId ?? Result?.RunId ?? Path.GetFileName(RunDir);
    public string Status => Result?.Status ?? "unknown";
    public string TaskId => Manifest?.TaskId ?? "unknown";
    public string Purpose => Manifest?.Purpose ?? "unknown";
    public RunSystemInfo? SystemInfo => Manifest?.SystemInfo;
    public RunMachineInfo? MachineInfo => Manifest?.MachineInfo;
    public long DurationMs => Result?.DurationMs ?? 0;
    public string ScenarioId => Manifest?.ScenarioId ?? "unknown";
    public string DeviceSerial => Manifest?.DeviceSerial ?? "unknown";
    public string ProviderId => Manifest?.ProviderId ?? "unknown";
    public string AppPackage => Manifest?.AppPackage ?? "unknown";
}

/// <summary>
/// Lightweight reference to a step directory (steps/D4/).
/// Assets are lazily loaded on first access.
/// </summary>
public sealed class StepAsset
{
    public string Directory { get; }
    public int StepNumber { get; }

    public StepAsset(string directory)
    {
        Directory = directory;
        var dirName = Path.GetFileName(directory);
        // Parse "0001" → 1 (unparsable names keep StepNumber 0)
        if (int.TryParse(dirName, out var num))
            StepNumber = num;
    }

    public string ScreenshotBeforePath => System.IO.Path.Combine(Directory, "before.png");
    public string ScreenshotAfterPath => System.IO.Path.Combine(Directory, "after.png");
    public string AnalysisPath => System.IO.Path.Combine(Directory, "analysis.json");
    public string VerificationPath => System.IO.Path.Combine(Directory, "verification.json");

    public bool HasScreenshotBefore => File.Exists(ScreenshotBeforePath);
    public bool HasScreenshotAfter => File.Exists(ScreenshotAfterPath);
}
