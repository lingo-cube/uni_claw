namespace UniClaw.Kernel.Evidence;

/// <summary>观察声明：某 producer 断言某 subject 具有某 value。</summary>
public sealed record ObservationClaim(string Subject, string Value);

/// <summary>
/// Ingress semantic kind（协议 P2）：输入候选是什么性质的证据。
/// 已验证二成员，不声明 exhaustive；新 kind 需协议 + 代码双改。
/// </summary>
public enum IngressKind
{
    /// <summary>对世界状态的观察声明。</summary>
    Observation,

    /// <summary>对一次 dispatch attempt 的投递报告（不是 world-state evidence）。</summary>
    AttemptReport,
}

/// <summary>
/// Observation Context（ING-006 D3）：观察在什么流程上下文中产生——
/// 表达「为什么 / 在哪个流程被观察」，不表达谁生产（ProducerIdentity），
/// 也不表达真实性（Deferred ⑦）。
/// </summary>
public enum ObservationContext
{
    /// <summary>常规外部观察流程。</summary>
    External,

    /// <summary>post-action effect flow：因某次 dispatch 而发起的效果观察。</summary>
    PostActionEffectFlow,
}

/// <summary>
/// 观察输入候选（ING-006 前名 ObservationRecord——与 baseline §11.2 的
/// canonical "Observation Record" 一词两义，rename 消除，见协议 P2
/// Terminology note）。这是 admission 的 proposal，不是 canonical
/// Evidence Record。Provenance 为 null 或字段不完整 → admission
/// fail-closed。
/// </summary>
public sealed record ObservationProposal(
    ObservationClaim Claim,
    IngressKind Kind,
    ObservationContext Context,
    Provenance? Provenance);
