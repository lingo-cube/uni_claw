using UniClaw.Agent.Evaluation;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 D18 — REAL importer：sealed trace artifact（FastPerception 经
/// TraceReferenceKind.Artifact 引用每帧 RawArtifact）→ derived stimuli →
/// 重跑 derived bundle。负面：Quarantined artifact / 跨场景 correlation
/// 失配 → fail closed。
/// </summary>
public sealed class ImportReDriveTests
{
    [Fact]
    public void SealedS1Trace_DerivesTwoStimuli_ReDriveCompletes_EquivalentSemantics()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        // 源运行（Enabled 臂：perception observe span 引用每帧 artifact）
        var source = ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Enabled });
        Assert.True(source.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, source.Report));
        var artifact = source.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(UniClaw.Kernel.Trace.RecorderTerminal.Finalized, artifact.RecorderTerminal);

        // Derive：两条 stimulus（initial + post-action——两个 artifact 均被
        // trace 引用）按 trace 顺序重版本化
        var imported = ScenarioImporter.Derive(bundle, artifact);
        Assert.Equal(2, imported.Bundle.Stimuli.Count);
        Assert.Equal("wifi-off-to-on-imported", imported.Bundle.ScenarioId);
        Assert.Equal("golden-wifi-off-to-on-import", imported.Bundle.BundleId);
        Assert.Equal("v2", imported.Bundle.ScenarioVersion);
        Assert.Equal(
            new[] { "import-1-obs-1-initial", "import-2-obs-2-post" },
            imported.Bundle.Stimuli.Select(s => s.StimulusId).ToArray());
        Assert.Equal(2, imported.DerivationLog.Count);
        imported.Bundle.Verify();

        // Re-drive：重跑 derived bundle → Completed / Completion /
        // 1 effect / 2 consultations / GoalSatisfied
        var reDrive = ScenarioRunner.Run(imported.Bundle);
        var report = reDrive.Report;
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(imported.Bundle, report));
        Assert.Equal(RunDriveStatus.Completed.ToString(), report.RunDriveStatus);
        Assert.Equal(TerminalClassification.Completion, report.Outcome!.Classification);
        Assert.Equal(1, report.EffectDeliveries);
        // RUN-004 E1（causal：d53f3331/ba4b5e9b）：提案耗尽 → NeedDecision
        //（StepVerified）再咨询恰一次（ScriptedUniAgent 多轮分支答 NoAction
        // → TerminalEvaluation）。旧期望 1 = 单轮协议时代残留；bundle 侧
        // ExpectedAgentConsultations 已随 ba4b5e9b 升 2，此处硬编码同步。
        Assert.Equal(2, report.AgentConsultations);
        Assert.Equal(GoalSatisfaction.Satisfied, report.GoalEvaluation!.Satisfaction);

        // 源/重跑 outcome 语义等价（digest 字符串因 stimulus id 版本化必然
        // 不同——断言等价的验收语义与分类，而非逐字节 digest）
        Assert.Equal(source.Report.Outcome!.Classification, report.Outcome!.Classification);
        Assert.Equal(source.Report.EffectDeliveries, report.EffectDeliveries);
        Assert.Equal(source.Report.AgentConsultations, report.AgentConsultations);
        Assert.Equal(source.Report.GoalEvaluation!.Satisfaction, report.GoalEvaluation!.Satisfaction);

        // derived bundle 自身的 digest 稳定性：两次重跑 digest 恒等
        var reDrive2 = ScenarioRunner.Run(imported.Bundle);
        Assert.Equal(report.SemanticDigest, reDrive2.Report.SemanticDigest);
    }

    /// <summary>Quarantined artifact（等待中 run 封存）→ Derive fail closed。</summary>
    [Fact]
    public void QuarantinedArtifact_DeriveFailsClosed()
    {
        var waiting = ScenarioRunner.Run(
            GoldenScenarioBundles.MissingPostActionStimulus(),
            new RunOptions { TraceArm = TraceArm.Enabled });
        var quarantined = waiting.Host.TraceScope!.FinalizeArtifact();
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.Derive(GoldenScenarioBundles.MissingPostActionStimulus(), quarantined));
    }

    /// <summary>跨场景 artifact（S2 的 sealed trace 对 S1 bundle derive）→ correlation mismatch。</summary>
    [Fact]
    public void ArtifactFromDifferentScenario_DeriveFailsClosed_CorrelationMismatch()
    {
        var s2 = ScenarioRunner.Run(
            GoldenScenarioBundles.AlreadyOnZeroEffect(),
            new RunOptions { TraceArm = TraceArm.Enabled });
        var s2Artifact = s2.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(UniClaw.Kernel.Trace.RecorderTerminal.Finalized, s2Artifact.RecorderTerminal);

        var ex = Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.Derive(GoldenScenarioBundles.WifiToggleOffToOn(), s2Artifact));
        Assert.Contains("run-correlation-mismatch", ex.Message, StringComparison.Ordinal);
    }
}
