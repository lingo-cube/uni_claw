using System.Collections.Frozen;

namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Canonical Evidence Record：admission 通过后的不可变依据记录。
/// EvidenceId = 内容确定性哈希（相同 canonical semantic content → 相同
/// id，支撑幂等去重；ING-006 D5：Kind / ObservationContext / relevant
/// provenance 的语义变化不得复用同一 id）。
/// append-oriented：可被后续记录 supersede / 降权，内容不可就地改写。
/// kind 与 ObservationContext 随记录携带（协议 P3）。
/// </summary>
public sealed record EvidenceRecord(
    string EvidenceId,
    ObservationClaim Claim,
    IngressKind Kind,
    ObservationContext Context,
    Provenance Provenance)
{
    /// <summary>Immediate derived parents, empty for direct source evidence.</summary>
    public IReadOnlyList<string> DerivedFromEvidenceIds { get; init; } = Array.Empty<string>();

    /// <summary>Deduplicated direct-source leaf basis for derived evidence.</summary>
    public IReadOnlySet<string> TransitiveEvidenceBasis { get; init; } =
        FrozenSet<string>.Empty;

    /// <summary>Fusion rule identity, when this record is derived.</summary>
    public string? FusionRule { get; init; }

    /// <summary>Fusion rule version, when this record is derived.</summary>
    public string? FusionVersion { get; init; }
}
