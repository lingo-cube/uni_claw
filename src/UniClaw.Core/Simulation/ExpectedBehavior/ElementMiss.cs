namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// exact 模式下对单个「应遍历但未遍历」元素的显式豁免 (C-11 schema 变更)。
/// 与旧 ratio 的「隐式放宽」语义对立: 每个豁免必须附 <see cref="Reason"/>,
/// 并记入 docs/system/decisions/log.md, 是「证明」纪律的强制点 (设计 §6.3)。
/// </summary>
/// <param name="Id">被豁免的元素 ID (与 Required 集合、ActionHistory element_id 同一命名空间, 精确等值)</param>
/// <param name="Reason">豁免理由 (具体、可审计, 如 "duplicate-dedup at scroll boundary")</param>
public sealed record class ElementMiss(
    string Id,
    string Reason);
