namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Canonical Evidence Record：admission 通过后的不可变依据记录。
/// EvidenceId = 内容确定性哈希（相同内容 → 相同 id，支撑幂等去重）。
/// append-oriented：可被后续记录 supersede / 降权，内容不可就地改写。
/// </summary>
public sealed record EvidenceRecord(
    string EvidenceId,
    ObservationClaim Claim,
    Provenance Provenance);
