namespace UniClaw.Core.UniBrain;

/// <summary>
/// ContainerInference — 容器推断结果。
/// 从 AI/IAIStrategyAdvisor.cs 迁入 UniBrain/。
/// 字段不变: ContainerType, Confidence, MatchedTemplate。
/// </summary>
public sealed record class ContainerInference(
    string ContainerType,
    double Confidence,
    string? MatchedTemplate = null);
