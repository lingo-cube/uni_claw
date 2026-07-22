using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// ContextDecisionResult — 上下文决策结果。
/// 对齐 Python ai_types.ContextDecisionResult 全字段。
/// 旧 C#: 4 字段 (Result, Action, Target(object?), Confidence)。
/// 新 C#: 7 字段 — +Params, +Reasoning, +SafetyVerified; Target → string?。
/// </summary>
public sealed record class ContextDecisionResult(
    DecisionResult Result,
    string? Action = null,
    string? Target = null,                          // ← string? (非 object?)
    ImmutableDictionary<string, object>? Params = null,  // ← 对齐 Python params
    string? Reasoning = null,                       // ← 对齐 Python reasoning
    double Confidence = 0.0,
    bool SafetyVerified = true);                    // ← 对齐 Python safety_verified
