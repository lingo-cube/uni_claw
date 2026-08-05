namespace UniClaw.Core.Observability;

/// <summary>
/// A single asset submission to the trace pipeline. Producers submit only the
/// <see cref="RelativePath"/> — runId is injected at assembly, never known to producers.
/// </summary>
public sealed class AssetSubmission
{
    /// <summary>Classification category (maps to record_type "asset.*" values).</summary>
    public string Category { get; }

    /// <summary>Asset bytes to persist.</summary>
    public byte[] Bytes { get; }

    /// <summary>Run-relative path (no runId segment). e.g. "steps/0001/before.png".</summary>
    public string RelativePath { get; }

    /// <summary>
    /// When true, bytes are appended to the existing file (JSONL / sequential assets).
    /// When false (default), the file is overwritten atomically via tmp+move.
    /// </summary>
    public bool Append { get; }

    public AssetSubmission(string category, byte[] bytes, string relativePath, bool append = false)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(relativePath);
        Category = category;
        Bytes = bytes;
        RelativePath = relativePath;
        Append = append;
    }
}

/// <summary>Well-known asset submission categories (record_type "asset.*" values).</summary>
public static class AssetCategories
{
    public const string Screenshot = "asset.screenshot";
    public const string UiXml = "asset.ui_xml";
    public const string StepAnalysis = "asset.step_analysis";
    public const string AnalysisSnapshot = "asset.analysis_snapshot";
    public const string VisionEvidence = "asset.vision_evidence";
}
