namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Observation Origin（协议 P2/P3；ADR-0048）：观察的来源性质——外部观察 vs
/// post-action effect flow 的自产观察。与 ProducerIdentity（谁生产）、
/// ObservationContext（哪个流程上下文）分开。封闭二值，扩值需协议 + 代码双改。
///
/// 判定权归 Evidence Ledger：只认原 Effect Boundary 的有效、同用途关联。
/// Context、自报、producer 名称及时间邻近均不产生关联；无关联保守 External。
/// </summary>
public enum ObservationOrigin
{
    /// <summary>外部观察（非 post-action effect flow 自产）。</summary>
    External,

    /// <summary>post-action effect flow 的自产观察（系统发起的动作后观察）。</summary>
    PostActionSelf,
}
