using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 Phase 1 trace 三臂与 semantic digest 验收：
/// 同 bundle 跨运行 digest 恒等（确定性 replay）；Trace on/off/failure
/// 三臂 canonical 输出等价（ADR-0013）；未封存 trace 不可导入（sealed-only 门，
/// D18 Derive 面）。完整 derive → re-drive 流程见 ImportReDriveTests。
/// </summary>
public sealed class TraceArmsAndDigestTests
{
    /// <summary>
    /// 同 bundle 两次独立运行 → semantic digest 逐字节相同且非空；
    /// 不同 bundle（S2 vs S1）→ digest 不同（digest 有区分度）。
    /// </summary>
    [Fact]
    public void SameBundle_TwoRuns_SemanticDigestIdentical()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        var first = ScenarioRunner.Run(bundle).Report;
        var second = ScenarioRunner.Run(bundle).Report;

        Assert.NotEmpty(first.SemanticDigest);
        Assert.NotEmpty(second.SemanticDigest);
        Assert.Equal(first.SemanticDigest, second.SemanticDigest);

        var other = ScenarioRunner.Run(GoldenScenarioBundles.AlreadyOnZeroEffect()).Report;
        Assert.NotEqual(first.SemanticDigest, other.SemanticDigest);
    }

    /// <summary>
    /// ADR-0013 三臂等价：Trace Enabled / Disabled / FailingRecorder 下
    /// 验收全部通过，且 semantic digest 完全一致——trace 开关与 recorder
    /// 故障均不改变 Runtime 语义输出。
    /// </summary>
    [Fact]
    public void Trace_ThreeArms_CanonicalOutputEquivalent()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        var reports = new[]
        {
            ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Enabled }).Report,
            ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Disabled }).Report,
            ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.FailingRecorder }).Report,
        };

        foreach (var report in reports)
            Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));

        Assert.NotEmpty(reports[0].SemanticDigest);
        Assert.Equal(reports[0].SemanticDigest, reports[1].SemanticDigest);
        Assert.Equal(reports[0].SemanticDigest, reports[2].SemanticDigest);
    }

    /// <summary>
    /// sealed-only 导入门（D18 Derive 面）：(a) 等待中的 run（无 outcome
    /// emission）在 Enabled 臂封存 → RecorderTerminal=Quarantined → Derive
    /// fail closed；(b) 完成的 run 封存 → Finalized → Derive 产出可重跑
    /// bundle。仅在 Enabled 臂上使用 TraceScope（FailingRecorder 臂为 null）。
    /// </summary>
    [Fact]
    public void UnsealedTrace_CannotBeImported_OnlySealedArtifactPasses()
    {
        // (a) S4：WaitingForInput（无 outcome emission）→ Quarantined
        var waiting = ScenarioRunner.Run(
            GoldenScenarioBundles.MissingPostActionStimulus(),
            new RunOptions { TraceArm = TraceArm.Enabled });
        Assert.NotNull(waiting.Host.TraceScope);
        var quarantined = waiting.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(UniClaw.Kernel.Trace.RecorderTerminal.Quarantined, quarantined.RecorderTerminal);
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.Derive(GoldenScenarioBundles.MissingPostActionStimulus(), quarantined));

        // (b) S1：Completed（outcome 已发射）→ Finalized → 可派生
        var completed = ScenarioRunner.Run(
            GoldenScenarioBundles.WifiToggleOffToOn(),
            new RunOptions { TraceArm = TraceArm.Enabled });
        Assert.NotNull(completed.Host.TraceScope);
        var sealedArtifact = completed.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(UniClaw.Kernel.Trace.RecorderTerminal.Finalized, sealedArtifact.RecorderTerminal);
        var imported = ScenarioImporter.Derive(GoldenScenarioBundles.WifiToggleOffToOn(), sealedArtifact);
        Assert.NotEmpty(imported.Bundle.Stimuli);
        Assert.NotEmpty(imported.DerivationLog);
    }
}
