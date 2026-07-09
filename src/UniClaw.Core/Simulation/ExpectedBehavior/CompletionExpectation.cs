using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 预期遍历完成状态 (D-E4: completion 维度)。
/// 对照 TraversalResult.Success + CompletionReason + FinalState。
/// </summary>
/// <param name="Success">预期是否成功完成</param>
/// <param name="Reason">预期完成原因（使用 TraversalResult.Reasons 常量值）</param>
/// <param name="FinalState">预期 FSM 终态名（可选）</param>
public sealed record class CompletionExpectation(
    bool Success,
    string Reason,
    string? FinalState = null);
