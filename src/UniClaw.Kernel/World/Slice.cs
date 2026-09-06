namespace UniClaw.Kernel.World;

/// <summary>
/// Slice = scoped immutable projection of a WorldBelief revision（Target §12.2，
/// 不变量 20）。只声明 source revision 与 scope；不回写 WorldBelief。
/// 有效性由 source revision currency 派生判定（World Model 侧，ADR-0010）；
/// freshness 充分性属消费侧 Freshness Judgment，不是 Slice 自身有效性的
/// 组成。不存在显式 invalidation event。
/// </summary>
public sealed record Slice(
    string SourceRevisionId,
    string Scope,
    FreshnessBasis FreshnessBasis,
    IReadOnlyDictionary<string, string> Projection);
