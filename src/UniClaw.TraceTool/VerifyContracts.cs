using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool;

/// <summary>
/// Deterministic conclusion of a verification rule: the cause of the outcome,
/// a confidence label ("high"/"medium"/"low"), the 1-based step number of the
/// failing step (null when not applicable), and a human-readable summary.
/// </summary>
public sealed record class VerifyVerdict(
    string Cause,
    string Confidence,
    int? FailingStep,
    string Summary);

/// <summary>
/// A single piece of evidence collected while reaching a <see cref="VerifyVerdict"/>.
/// <see cref="StepNumber"/> is the 1-based step the evidence refers to (null when
/// the evidence is run-scoped rather than step-scoped).
/// </summary>
public sealed record class VerifyEvidence(
    string Type,
    string? StepNumber,
    string Description);

/// <summary>
/// Artifact paths attached to a <see cref="VerifyResult"/> so consumers can open
/// the underlying evidence without re-discovering the run layout. Screenshot
/// paths are step-relative; <see cref="TracePath"/> points at the run's trace
/// file. Empty collections mean the artifacts are unknown, not that they do not
/// exist.
/// </summary>
public sealed record class VerifyArtifactPaths(
    IReadOnlyList<string> ScreenshotPaths,
    string? TracePath);

/// <summary>
/// Complete verification result for a single run: the run identity, a status
/// of "success", "failure" or "evidence_missing", the winning verdict, the
/// evidence collected, and the artifact paths.
/// </summary>
public sealed record class VerifyResult(
    string RunId,
    string Status,
    VerifyVerdict Verdict,
    IReadOnlyList<VerifyEvidence> Evidence,
    VerifyArtifactPaths ArtifactPaths);

/// <summary>
/// A single row from analysis.jsonl — matches the AnalysisSnapshot format written
/// by AnalysisWritingDecorator. Each row is one page analysis with its detected items.
/// </summary>
public sealed class AnalysisRow
{
    public string AnalyzedAt { get; init; } = "";
    public int ItemCount { get; init; }
    public bool HasScroll { get; init; }
    public bool IsEndOfList { get; init; }
    public bool IsPopup { get; init; }
    public string[] Level1MenuNames { get; init; } = [];
    public AnalysisItemDto[] Items { get; init; } = [];
}

/// <summary>Single detected item within an analysis row (name / type / normalised coordinate / expected action).</summary>
public sealed class AnalysisItemDto
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public string ExpectedAction { get; init; } = "";
}
