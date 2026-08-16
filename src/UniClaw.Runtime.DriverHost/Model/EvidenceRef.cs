using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Logical evidence kind (design.md §6 — EvidenceRef taxonomy).
/// </summary>
public enum EvidenceKind
{
    /// <summary>Screen capture artifact (e.g. replay asset frame).</summary>
    Screenshot,

    /// <summary>Perception output record (observation evidence).</summary>
    PerceptionOutput,

    /// <summary>Binding evidence (target resolution record).</summary>
    BindingEvidence,

    /// <summary>Action dispatch journal record.</summary>
    ActionJournal,

    /// <summary>Replay asset (deterministic scenario input).</summary>
    ReplayAsset,

    /// <summary>Trace fragment (observability span/event fragment).</summary>
    TraceFragment,
}

/// <summary>
/// Logical reference to Kernel-owned evidence (design.md §6).
/// <list type="bullet">
/// <item><see cref="Locator"/> is a LOGICAL key (e.g. <c>capture:&lt;session&gt;:record:&lt;order&gt;</c>) —
/// never a filesystem path, never a URL, never wire-format specific.</item>
/// <item><see cref="ContentIdentity"/> is content-based identity when available
/// (e.g. artifact ContentHash), never a storage location.</item>
/// <item><see cref="Maturity"/> is the audited AssetMaturity of the underlying capture.</item>
/// </list>
/// Resolution is a SEPARATE logical operation (GetEvidence); a ref never embeds content.
/// </summary>
public sealed record EvidenceRef
{
    public string EvidenceId { get; init; } = "";

    public EvidenceKind Kind { get; init; }

    /// <summary>Run identity the evidence belongs to.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Kernel-assigned observation sequence anchor when attributable; null = none.</summary>
    public long? ObservationSequence { get; init; }

    /// <summary>Content-based identity when available; null = none recorded.</summary>
    public string? ContentIdentity { get; init; }

    /// <summary>Audited capture maturity (Synthetic/RealitySeeded/RecordedReality/LiveCapture).</summary>
    public AssetMaturity Maturity { get; init; }

    /// <summary>Byte count when known; null = unknown.</summary>
    public int? SizeBytes { get; init; }

    /// <summary>LOGICAL locator key — never a filesystem path.</summary>
    public string Locator { get; init; } = "";
}

/// <summary>
/// Result of a GetEvidence logical resolution (design.md §6 / §7).
/// Resolution NEVER invents evidence: not found is a truthful diagnostic.
/// </summary>
public sealed record EvidenceResolution
{
    public bool Found { get; init; }

    /// <summary>Canonical ref (identity-equal to the requested logical evidence).</summary>
    public EvidenceRef? Ref { get; init; }

    /// <summary>Capture session the evidence resolves to; null = none.</summary>
    public string? CaptureSessionId { get; init; }

    /// <summary>Resolved capture record (metadata only — never embedded content).</summary>
    public CaptureRecord? Record { get; init; }

    /// <summary>Resolved capture artifact (metadata only — never embedded content).</summary>
    public CaptureArtifact? Artifact { get; init; }

    /// <summary>Truthful diagnostic when not found.</summary>
    public string? Diagnostic { get; init; }
}
