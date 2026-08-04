using System.Text.Json;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool;

/// <summary>
/// Rebuilt verification input for the verify engine, loaded from a run directory.
/// Carries the run itself plus the raw evidence the rules need: verification
/// criteria, last analysis row, and engine facts from result.json.
/// </summary>
public sealed class VerificationInput
{
    public TraceRun Run { get; init; } = null!;
    public VerificationCriteria? Criteria { get; init; }
    public AnalysisRow? LastAnalysisRow { get; init; }  // last row only (what verify needs)
    public string? CompletionReason { get; init; }  // from result.json
    public bool TargetActionExecuted { get; init; }  // derived: completionReason==target_found && actionsSucceeded>0
    public int ActionsSucceeded { get; init; }
    public int ActionsAttempted { get; init; }
}

// AnalysisRow (analysis.jsonl row model) lives in VerifyContracts.cs — same
// namespace, shared with verification rules (VerifyEngine/LocateOneItemRule).

public static class RunEvidenceLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Load a run directory and rebuild verification input.
    /// Dispatches by manifest.schemaVersion for V1/V2 asset paths.
    /// </summary>
    public static async Task<VerificationInput> LoadAsync(
        string runDir,
        CancellationToken ct = default)
    {
        // 1. Load the TraceRun (delegates to TraceRunLoader — handles V1/V2 dispatch)
        var run = await TraceRunLoader.LoadAsync(runDir, ct);

        // 2. Read criteria.json (at the run root in both V1 and V2)
        VerificationCriteria? criteria = null;
        var criteriaPath = Path.Combine(runDir, "criteria.json");
        if (File.Exists(criteriaPath))
        {
            var json = await File.ReadAllTextAsync(criteriaPath, ct);
            criteria = JsonSerializer.Deserialize<VerificationCriteria>(json, JsonOptions);
        }

        // 3. Read analysis.jsonl rows — V2 → assets/{runId}/analysis.jsonl, V1 → run root.
        //    Verify consumes only the last row (the final page analysis).
        var schemaVersion = run.Manifest?.SchemaVersion ?? "1";
        var analysisPath = schemaVersion == "2"
            ? Path.Combine(runDir, "assets", run.RunId, "analysis.jsonl")
            : Path.Combine(runDir, "analysis.jsonl");

        AnalysisRow? lastRow = null;
        if (File.Exists(analysisPath))
        {
            var lines = await File.ReadAllLinesAsync(analysisPath, ct);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                try
                {
                    var row = JsonSerializer.Deserialize<AnalysisRow>(line, JsonOptions);
                    if (row is not null)
                        lastRow = row;
                }
                catch (JsonException)
                {
                    // Malformed line — skip.
                }
            }
        }

        // 4. Engine facts from result.json
        var completionReason = run.Result?.CompletionReason;
        var actionsSucceeded = run.Result?.ActionsSucceeded ?? 0;
        var actionsAttempted = run.Result?.ActionsAttempted ?? 0;
        var targetActionExecuted = completionReason == "target_found" && actionsSucceeded > 0;

        return new VerificationInput
        {
            Run = run,
            Criteria = criteria,
            LastAnalysisRow = lastRow,
            CompletionReason = completionReason,
            TargetActionExecuted = targetActionExecuted,
            ActionsSucceeded = actionsSucceeded,
            ActionsAttempted = actionsAttempted,
        };
    }
}
