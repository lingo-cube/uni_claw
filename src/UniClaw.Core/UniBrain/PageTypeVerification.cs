using System.Collections.Immutable;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// PageTypeVerification — 页面类型验证结果。
/// 从 AI/IAIStrategyAdvisor.cs 迁入 UniBrain/。
/// 字段更新: ActualType → string? (非必填), Reasoning → string?, Mismatch → MismatchDetails?。
/// </summary>
public sealed record class PageTypeVerification(
    bool IsMatch,
    double Confidence,
    string? ActualType = null,
    string? Reasoning = null,
    MismatchDetails? Mismatch = null,
    Suggestion? Suggestion = null);

/// <summary>
/// MismatchDetails — 不匹配详情。
/// 对齐 Python missing_items / unexpected_items / type_conflict。
/// 旧 C#: ExpectedType, ActualElements, MissingElements。
/// 新 C#: MissingItems, UnexpectedItems, TypeConflict (对齐 Python 字段名)。
/// </summary>
public sealed record class MismatchDetails(
    ImmutableArray<string> MissingItems,
    ImmutableArray<string> UnexpectedItems,
    string? TypeConflict = null);

/// <summary>
/// Suggestion — 建议。
/// 对齐 Python: action, target, reason。
/// Target 从 object? → string? (类型安全)。
/// </summary>
public sealed record class Suggestion(
    string Action,
    string? Target = null,
    string? Reason = null);
