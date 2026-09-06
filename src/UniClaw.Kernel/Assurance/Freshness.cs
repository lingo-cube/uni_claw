using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

/// <summary>
/// Consumption Requirement：一次具体消费（action judgment）对 belief
/// freshness 提出的要求（FRS-007 D5 / ADR-0010）。当前 realization 携带
/// target 与 effect 语义；字段集不随 realization 锁死——未来 buyer 可扩
/// scope / 参数化要求，但不得回填 producer 侧自报数据。
/// _Avoid_: freshness policy（算法 / 阈值义）、constraint（泛义）。
/// </summary>
public sealed record ConsumptionRequirement(
    string? TargetSubject,
    string? EffectClass);

/// <summary>
/// Freshness 评估输入：Freshness Basis × 本次消费 requirement 的关系两侧
/// （FRS-007 D5）。evaluator 只见窄输入——不消费全量 WorldBelief
/// （不扩大 P11 曝射面）。
/// </summary>
public sealed record FreshnessEvaluationInput(
    FreshnessBasis FreshnessBasis,
    string RevisionId,
    ConsumptionRequirement Requirement);

/// <summary>
/// Freshness Judgment 三态（FRS-007 D3 / ADR-0010）：是否足够支撑**当前
/// 消费**——不是世界对象绝对 Fresh/Stale。Sufficient=可过 freshness 关；
/// Insufficient=判定过且不满足；Unknown=判定输入不足。Insufficient 与
/// Unknown 都 fail-closed 且不得折叠（来源不同，词汇可区分）。
/// </summary>
public enum FreshnessSufficiency
{
    Sufficient,
    Insufficient,
    Unknown
}

/// <summary>
/// 一次 Freshness Judgment 的不可变产物（Assurance 消费时形成）：只对
/// 该次消费有效，不回写 belief、不形成第二 WorldBelief Authority，
/// 也不使 CanonicalBinding 派生 validity 失效——freshness 拒绝的是
/// 授权（ADR-0010）。
/// </summary>
public sealed record FreshnessJudgment(
    FreshnessSufficiency Sufficiency,
    string Reason);

/// <summary>
/// Deterministic freshness evaluator 缝（FRS-007 D5 / ADR-0010）：基于
/// Freshness Basis × Consumption Requirement 判定 sufficiency。阈值 /
/// 算法 / canonical clock = Deferred ④/⑪——本接口不携带时间权威；
/// 同输入必须同结果。未配置 = composition/configuration error
/// （RuntimeAssurance ctor fail-fast），不是 runtime Unknown。
/// </summary>
public interface IFreshnessEvaluator
{
    FreshnessJudgment Evaluate(FreshnessEvaluationInput input);
}
