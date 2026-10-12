namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Observation Origin（协议 P2/P3；ADR-0048）：观察的来源性质——外部观察 vs
/// post-action effect flow 的自产观察。与 ProducerIdentity（谁生产）、
/// ObservationContext（哪个流程上下文）分开。封闭二值，扩值需协议 + 代码双改。
///
/// 判定权（ADR-0048，PROTOCOL-FIX-001）：由 Evidence Ledger 在 admission 时从
/// Context **机械推导**——Context=PostActionEffectFlow ⇒ PostActionSelf；其余 ⇒
/// External。禁止以时间邻近推导（协议通则 8：时间相邻 ≠ 因果）；推导不出按保守
/// 方向归 External（误归外部 = 拒绝满足 MaterialEffect，fail-closed）。
/// ObservationProposal 上的生产者自报 Origin 仅作诊断，不参与判定、不进入
/// EvidenceRecord 与 EvidenceId。
/// </summary>
public enum ObservationOrigin
{
    /// <summary>外部观察（非 post-action effect flow 自产）。</summary>
    External,

    /// <summary>post-action effect flow 的自产观察（系统发起的动作后观察）。</summary>
    PostActionSelf,
}
