namespace UniClaw.Kernel.Run;

/// <summary>
/// 已接受 contract version 的 immutable canonical view（Target §13.2）。
/// 对当前 Run immutable；同 version 重复 admit 复用同一实例，不产生新 View
/// （验收 9）。
/// </summary>
public sealed record ExecutionContractView(
    string Version,
    string Objective,
    IReadOnlySet<string> Scope,
    IReadOnlySet<string> AllowedEffects,
    IReadOnlySet<string> ForbiddenEffects,
    IReadOnlyList<string> ProofCriteria);
