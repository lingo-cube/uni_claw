using System.Collections.Frozen;

namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Canonical Evidence Record：admission 通过后的不可变依据记录。
/// EvidenceId = 内容确定性哈希（相同 canonical semantic content → 相同
/// id，支撑幂等去重；ING-006 D5：Kind / ObservationContext / relevant
/// provenance 的语义变化不得复用同一 id）。
/// append-oriented：可被后续记录 supersede / 降权，内容不可就地改写。
/// Kind / Context 随记录携带；Origin（ADR-0048）由账本核验原实际投递的
/// 有效关联后派生，与 Context 正交，自报不参与。新 identity v2 明确纳入
/// 实际 Origin 与稳定 AttemptReference；旧 v1 历史保持原编号与原字段只读。
/// 历史关联值不携带 live token，也不授权新入证、current World 或执行。
/// </summary>
public sealed record EvidenceRecord(
    string EvidenceId,
    ObservationClaim Claim,
    IngressKind Kind,
    ObservationContext Context,
    Provenance Provenance,
    ObservationOrigin Origin = ObservationOrigin.External)
{
    public int IdentityVersion { get; init; } = 1;
    public AttemptReference? AttemptCorrelation { get; init; }

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
