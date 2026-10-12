using System.Collections.Frozen;

namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Canonical Evidence Record：admission 通过后的不可变依据记录。
/// EvidenceId = 内容确定性哈希（相同 canonical semantic content → 相同
/// id，支撑幂等去重；ING-006 D5：Kind / ObservationContext / relevant
/// provenance 的语义变化不得复用同一 id）。
/// append-oriented：可被后续记录 supersede / 降权，内容不可就地改写。
/// kind 与 ObservationContext 随记录携带（协议 P3）；Origin（ADR-0048 /
/// PROTOCOL-FIX-001）为账本从 Context 机械推导的 canonical 来源性质
/// （External | PostActionSelf），自报不参与——origin = f(Context)，故
/// EvidenceId 不追加独立 origin 输入（值域独立扩展时须同步纳入并迁移）。
/// </summary>
public sealed record EvidenceRecord(
    string EvidenceId,
    ObservationClaim Claim,
    IngressKind Kind,
    ObservationContext Context,
    Provenance Provenance,
    ObservationOrigin Origin = ObservationOrigin.External)
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
