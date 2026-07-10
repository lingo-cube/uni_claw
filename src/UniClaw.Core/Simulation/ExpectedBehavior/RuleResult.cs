namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 单条规则验证结果。
/// </summary>
/// <param name="RuleId">规则标识 (如 "completion", "page_coverage", "collision_proof:ON")</param>
/// <param name="Passed">是否通过 (numeric_anchor 的 Passed 是 informational)</param>
/// <param name="Message">验证消息 (成功为 PASS 描述, 失败为差异说明)</param>
/// <param name="Actual">实际值 (可选, 失败时展示具体数值)</param>
public sealed record class RuleResult(
    string RuleId,
    bool Passed,
    string Message,
    string? Actual = null);
