using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// S1 端到端 smoke：golden happy-path bundle 经 Simulation Host 全真实 L2
/// 组合 → Completed / Completion / 1 effect / 1 consultation / Satisfied。
/// </summary>
public sealed class SimulationHostSmokeTests
{
    [Fact, Trait("Scenario", "SCN-SMOKE-001")]
    public void S1_HappyPath_EndToEnd_Accepts()
    {
        var execution = ScenarioRunner.Run(GoldenScenarioBundles.WifiToggleOffToOn());
        var report = execution.Report;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(
                GoldenScenarioBundles.WifiToggleOffToOn(), report) + "; reason=" + report.Reason);
        Assert.Equal("Completed", report.RunDriveStatus);
        Assert.NotNull(report.Outcome);
        Assert.Equal(UniClaw.Kernel.Run.TerminalClassification.Completion, report.Outcome!.Classification);
        Assert.Equal(1, report.EffectDeliveries);
        Assert.Equal(2, report.AgentConsultations); // RUN-004: E1
        Assert.Empty(report.AgentViolations);
        Assert.NotNull(report.GoalEvaluation);
        Assert.Equal(UniClaw.Agent.Evaluation.GoalSatisfaction.Satisfied, report.GoalEvaluation!.Satisfaction);
        Assert.StartsWith("N/A", report.Metrics.ModelCalls, StringComparison.Ordinal);
    }
}
