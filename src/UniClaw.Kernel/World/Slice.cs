namespace UniClaw.Kernel.World;

/// <summary>
/// Slice = scoped immutable projection of a WorldBelief revision（Target §12.2，
/// 不变量 20）。只声明 source revision 与 scope；不回写 WorldBelief。
/// 有效性由 source revision 与 freshness 派生判定，不存在显式 invalidation event。
/// </summary>
public sealed record Slice(
    string SourceRevisionId,
    string Scope,
    Freshness Freshness,
    IReadOnlyDictionary<string, string> Projection);
