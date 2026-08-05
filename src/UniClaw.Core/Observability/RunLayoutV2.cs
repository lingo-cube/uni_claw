namespace UniClaw.Core.Observability;

/// <summary>
/// V2 run-layout constants and pure path functions. Shared by Host (write side)
/// and TraceTool (read side). Layout version is decoupled from trace.jsonl line format.
/// </summary>
public static class RunLayoutV2
{
    /// <summary>Current layout version written to manifest.schemaVersion.</summary>
    public const string CurrentSchemaVersion = "2";

    // ── Storage-space directory names (under run root) ──

    /// <summary>Event-stream space directory name.</summary>
    public const string TraceDir = "trace";

    /// <summary>Asset-space directory name.</summary>
    public const string AssetsDir = "assets";

    /// <summary>Step screenshots/analysis subdirectory name.</summary>
    public const string StepsDir = "steps";

    // ── File names (within their spaces) ──

    public const string TraceFileName = "trace.jsonl";
    public const string RunLogFileName = "run.log";
    public const string AnalysisSnapshotsFileName = "analysis.jsonl";
    public const string CriteriaFileName = "criteria.json";
    public const string ManifestFileName = "manifest.json";
    public const string ResultFileName = "result.json";
    public const string IssuesFileName = "issues.jsonl";

    // ── Pure path functions (no I/O, no state) ──

    /// <summary>Full trace file path: {runDir}/trace/{runId}/trace.jsonl.</summary>
    public static string TraceFilePath(string runDir, string runId) =>
        Path.Combine(runDir, TraceDir, runId, TraceFileName);

    /// <summary>Full run log file path: {runDir}/trace/{runId}/run.log.</summary>
    public static string RunLogFilePath(string runDir, string runId) =>
        Path.Combine(runDir, TraceDir, runId, RunLogFileName);

    /// <summary>Relative run log path: trace/{runId}/run.log (for result.json).</summary>
    public static string RunLogRelativePath(string runId) =>
        $"{TraceDir}/{runId}/{RunLogFileName}";

    /// <summary>Asset space root for a run: {runDir}/assets/{runId}/.</summary>
    public static string AssetSpaceRoot(string runDir, string runId) =>
        Path.Combine(runDir, AssetsDir, runId);

    /// <summary>Step directory within asset space: assets/{runId}/steps/{n:D4}/.</summary>
    public static string StepDir(string runDir, string runId, int stepNumber) =>
        Path.Combine(runDir, AssetsDir, runId, StepsDir, $"{stepNumber:D4}");

    /// <summary>Before/after screenshot path: assets/{runId}/steps/{n:D4}/before.png.</summary>
    public static string ScreenshotPath(string runDir, string runId, int stepNumber, string suffix) =>
        Path.Combine(StepDir(runDir, runId, stepNumber), $"{suffix}.png");

    /// <summary>Step analysis path: assets/{runId}/steps/{n:D4}/analysis.json.</summary>
    public static string StepAnalysisPath(string runDir, string runId, int stepNumber) =>
        Path.Combine(StepDir(runDir, runId, stepNumber), "analysis.json");

    /// <summary>Analysis snapshots path: assets/{runId}/analysis.jsonl.</summary>
    public static string AnalysisSnapshotsPath(string runDir, string runId) =>
        Path.Combine(AssetSpaceRoot(runDir, runId), AnalysisSnapshotsFileName);

    /// <summary>
    /// Vision evidence file name. seq guards ai.call retry overwrite.
    /// Path is relative — runId is injected at assembly.
    /// </summary>
    public static string VisionEvidenceFileName(string stepSpanId, int seq = 0) =>
        seq > 0
            ? $"vision-evidence-{stepSpanId}-{seq}.json"
            : $"vision-evidence-{stepSpanId}.json";

    /// <summary>Criteria file path at run root.</summary>
    public static string CriteriaPath(string runDir) =>
        Path.Combine(runDir, CriteriaFileName);

    /// <summary>Version check: is the schema version known to this reader?</summary>
    public static bool IsKnownVersion(string schemaVersion) =>
        schemaVersion is "1" or "2";

    /// <summary>Old-tool refusal message for unsupported versions.</summary>
    public static string UnsupportedVersionMessage(string found) =>
        $"Unsupported run layout version {found} — upgrade the analyzer. Supported versions: 1, 2.";
}
