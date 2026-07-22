using System.Collections.Immutable;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// SafetyScreeningResult — 安全筛选结果。
/// 从 AI/IAIStrategyAdvisor.cs 迁入 UniBrain/。
/// Evaluations 从 List → ImmutableArray (不可变设计)。
/// </summary>
public sealed record class SafetyScreeningResult(
    ImmutableArray<SafetyEvaluation> Evaluations,
    PageLevelGuidance? PageLevelGuidance = null);

/// <summary>
/// SafetyEvaluation — 安全评估。
/// 字段精简: 移除 ContextDependency + TaskRelevance (YAGNI, Python 未对齐)。
/// </summary>
public sealed record class SafetyEvaluation(
    string Name,
    SafetyTag SafetyTag,
    double Confidence,
    string? Reason = null);

/// <summary>
/// SafetyTag — 安全标签枚举。
/// 4 值锁定 (Safe, Caution, Skip, Unknown)，新增/删除需 constitution change flow。
/// 从 AI/IAIStrategyAdvisor.cs 迁入 UniBrain/。
/// </summary>
public enum SafetyTag
{
    /// <summary>安全</summary>
    Safe,

    /// <summary>谨慎</summary>
    Caution,

    /// <summary>跳过</summary>
    Skip,

    /// <summary>未知</summary>
    Unknown
}

/// <summary>
/// PageLevelGuidance — 页面级安全指导。
/// RecommendedMaxParallel 从 int? → int (有默认值 1)。
/// </summary>
public sealed record class PageLevelGuidance(
    bool OverallSafeToProceed,
    int RecommendedMaxParallel = 1);
