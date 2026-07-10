using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 验证报告汇总 (D-E2: 返回 VerificationReport, 测试代码 Assert.True(report.AllPassed, report.Summary))。
/// AllPassed 排除 numeric_anchor (informational, 非 CI-blocking)。
/// </summary>
/// <param name="AllPassed">所有非-informational 规则是否通过</param>
/// <param name="Summary">人可读的 pass/fail 摘要</param>
/// <param name="Details">逐条规则验证结果</param>
public sealed record class VerificationReport(
    bool AllPassed,
    string Summary,
    ImmutableArray<RuleResult> Details);
