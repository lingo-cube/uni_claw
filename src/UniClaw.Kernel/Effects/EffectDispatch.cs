using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Effects;

/// <summary>
/// DeliveryTarget — driver-executable 的目标地址（DSE-002 / EB lowering seam
/// 产物；Human 裁决 2026-09-09）。五层名词链：TargetDescriptor（找谁）→
/// ObservationOccurrence（看到谁）→ CanonicalBinding（作用谁）→
/// **DeliveryTarget（去哪执行）** → DispatchRequest（送什么）。
/// OccurrenceReference 仅溯源（receipt / attempt / trace 关联），
/// **永不参与执行**——invariant：Reference identity ≠ executable locator。
/// Spatial = SpatialLocator（归一化 bounds + frame，规则 A 构造执法）；
/// Native = NativeLocator（平台原生键，DSE-003 / ego-browser buyer）——
/// 两者并存 = 同一已授权 target 的不同 delivery material，driver 固定
/// 消费自身支持集、永不挑选/fallback（规则 B/C：delivery form 由支持集
/// 声明决定）。字符串通道（Deferred ⑮）locatorless 包装，executable
/// 判定归 driver（规则 B）。
/// </summary>
public sealed record DeliveryTarget(
    string OccurrenceReference,
    SpatialLocator? Spatial = null,
    NativeLocator? Native = null,
    UniClaw.Kernel.Perception.CoordinateSpace? Space = null);

/// <summary>
/// P14 Dispatch Request — Effect Boundary 发往 Capability Plane 的 bounded
/// command（DSE-001 收窄 + DSE-002 DeliveryTarget）。只表达「在哪个
/// bounded target 上做什么 effect」，由 EB 从 CanonicalBinding 唯一派生
/// （lowering 在 EB 内；driver 只做物理翻译，不重新 grounding、不选择
/// 语义目标、不自行恢复——Human 裁决规则 C）；不携带 IntentId /
/// BindingId / judgment / authorization 语义——Capability Plane 对授权态
/// 结构性零感知（协议基线 P14 Forbidden Use）。
/// </summary>
/// <param name="Target">DeliveryTarget：UI 通道 = occurrence ref + locator（EB lowering 唯一派生）；字符串通道 locatorless 包装。</param>
/// <param name="EffectClass">Effect semantics（来自 CanonicalBinding.EffectClass）。</param>
/// <param name="Parameters">Typed parameters 通道（当前沿载 TargetValue；typed family 扩展 = 未来 buyer）。</param>
/// <param name="RevisionId">Revision anchor（binding 依据的 WorldBelief revision）。</param>
public sealed record DispatchRequest(
    DeliveryTarget Target,
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

/// DSE-001 词汇 / RVR-002 F2：未确认完成 = DeliveryFailed ∨ UnknownOutcome。
/// Assurance 失败记忆与 ControlLoop recovery 共用的判定语义，单点维护
/// （两处消费点不得再手写双态字面量）。
public static class DispatchOutcomePredicates
{
    public static bool IsUnconfirmedOutcome(this DispatchOutcome outcome) =>
        outcome is DispatchOutcome.DeliveryFailed or DispatchOutcome.UnknownOutcome;
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
