namespace UniClaw.Kernel.Effects;

/// <summary>
/// P14 Dispatch Request — Effect Boundary 发往 Capability Plane 的 bounded
/// command（DSE-001：对 `Deliver(CanonicalBinding)` known authority leak 的
/// 收窄）。只表达「在哪个 bounded target 上做什么 effect」，由 EB 从
/// CanonicalBinding 唯一派生（lowering 在 EB 内，driver 不重新 grounding）；
/// 不携带 IntentId / BindingId / judgment / authorization 语义——Capability
/// Plane 对授权态结构性零感知（协议基线 P14 Forbidden Use）。
/// 字段集 = P14 minimal payload；spatial / physical-target anchor 待 World
/// 侧 spatial fact 出现真实 driver buyer 后另立 change（无源不造）。
/// </summary>
/// <param name="Target">Bounded target：UI 通道 = TargetOccurrenceId；非 UI 字符串通道 = TargetSubject（Deferred ⑮ 沿载）。</param>
/// <param name="EffectClass">Effect semantics（来自 CanonicalBinding.EffectClass）。</param>
/// <param name="Parameters">Typed parameters 通道（当前沿载 TargetValue；typed family 扩展 = 未来 buyer）。</param>
/// <param name="RevisionId">Revision anchor（binding 依据的 WorldBelief revision）。</param>
public sealed record DispatchRequest(
    string Target,
    string EffectClass,
    string? Parameters,
    string RevisionId);

/// <summary>
/// 机械投递结果三态（DSE-001 / 协议 Deferred ⑤ 闭合）：对**世界效果的认知状态**，
/// 不是成因分类。DeliveryCompleted ≠ world effect（不变量 33/34）；
/// UnknownOutcome = 投递结果不可判定（timeout 后 client 放弃等待、结果不可
/// 解读等——物理上 effect 可能已发生），唯一合法后继是 re-observe，
/// NEVER blind redispatch。成因细类（timeout-killed / device-offline /
/// result-uninterpretable / …）入 <see cref="DispatchResult.Reason"/>，不进
/// 共享协议词汇（协议通则 4：不声明 exhaustive closed set）。
/// </summary>
public enum DispatchOutcome
{
    /// <summary>下游确认投递完成（≠ Effect ≠ Verified Effect）。</summary>
    DeliveryCompleted,

    /// <summary>投递确定性失败（下游显式拒绝 / 传输确定否定，effect 未送达）。</summary>
    DeliveryFailed,

    /// <summary>投递结果未知（effect 可能已发生也可能未发生）——re-observe，永不盲补发。</summary>
    UnknownOutcome,
}

/// <summary>Driver 单次投递的完整结果（三态 outcome + 成因 diagnostic + 下游确认时间）。</summary>
/// <param name="Outcome">对世界效果的认知状态（三态）。</param>
/// <param name="Report">Provider report（描述性 provenance）。</param>
/// <param name="Reason">成因分类 diagnostic（string，不锁词汇；如 timeout-killed / device-offline / result-uninterpretable）。</param>
/// <param name="CompletedAt">下游确认时间（供 attempt evidence 溯源）。</param>
public sealed record DispatchResult(
    DispatchOutcome Outcome,
    string Report,
    DateTimeOffset CompletedAt,
    string? Reason = null);

/// <summary>
/// 机械投递缝（Target §16.2 Dispatch；P14）：只负责 delivery 并如实报告，
/// 不得自行 retry、recover、replan 或改变 strategy（不变量 27）。
/// DSE-001：入参收窄为 DispatchRequest——CanonicalBinding 不穿透本缝。
/// </summary>
public interface IEffectDriver
{
    DispatchResult Deliver(DispatchRequest request);
}

/// <summary>
/// Effect Gate 决定：只执行或拒绝既有 authorization judgment，不重新判断、
/// 不改变 target、不扩大 effect（不变量 26，验收 4）。
/// </summary>
public sealed record GateDecision(bool Allowed, string? Reason);

/// <summary>
/// Effect Receipt — dispatch 后的不可变留痕（Target §16.2，§17）。
/// 是 attempt evidence，不直接证明 Effect（不变量 33/34，验收 5）。
/// DSE-001：Outcome 三态化；Reason 沿载 DispatchResult 的成因 diagnostic
/// （末位默认 null，既有构造调用兼容——Plan 字段顺序微偏离，语义等价）。
/// </summary>
public sealed record EffectReceipt(
    string ReceiptId,
    string IntentId,
    string BindingId,
    string TargetSubject,
    string RevisionId,
    int RevisionNumber,
    DispatchOutcome Outcome,
    string Report,
    DateTimeOffset DispatchedAt,
    string? Reason = null);
