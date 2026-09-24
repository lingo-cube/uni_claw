using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// S1 端到端 smoke：golden happy-path bundle 经 Simulation Host 全真实 L2
/// 组合。SIM-003 G8：bundle 经 ScenarioLibrary.Load 解析（certified JSON
/// 投影），golden 值断言由 AcceptancePassed 承载；保留断言均为期望模型
/// 之外的架构不变量（Type-B）。
/// </summary>
public sealed class SimulationHostSmokeTests
{
    [Fact, Trait("Scenario", "SCN-SMOKE-001")]
    public void S1_HappyPath_EndToEnd_Accepts()
    {
        var bundle = ScenarioLibrary.Load("SCN-SMOKE-001").Bundle;
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(bundle, report) + "; reason=" + report.Reason);
        Assert.Empty(report.AgentViolations);

        // Type-B：metrics 纪律——ScriptedUniAgent 无 live model，显式 N/A
        // 而非伪造 0（metrics 面不变量，不在期望六字段模型内）
        Assert.StartsWith("N/A", report.Metrics.ModelCalls, StringComparison.Ordinal);
    }
}
