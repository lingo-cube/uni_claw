using UniClaw.Agent.Evaluation;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// TRW-001 — sealed artifact 生命周期验收（state.md Acceptance 5 / 6）：
/// 未 seal（Quarantined）/ integrity 失败（篡改 .json / 缺失 .sha256）→
/// DeriveFromPersisted fail closed；async sealed artifact → 派生 stimuli →
/// re-drive 复现任一既有场景语义（RFS-001 ImportReDrive 模式复用）。
/// </summary>
public sealed class AsyncImportRedriveTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "uniclaw-trw001-" + Guid.NewGuid().ToString("N"));

    private static string CorrelationOf(MinimalScenarioBundle bundle) => "sim:" + bundle.ScenarioId;

    /// <summary>
    /// Acceptance 5：(a) 等待中 run（无 outcome emission）→ Quarantined →
    /// DeriveFromPersisted fail closed；(b) 篡改磁盘 .trace.json → integrity
    /// 失配 fail closed；(c) 删除 .trace.sha256 → fail closed。
    /// </summary>
    [Fact]
    public void UnsealedOrBadIntegrity_ImportFailsClosed()
    {
        // (a) S4：WaitingForInput → Quarantined → 持久产物虽在，导入门关死
        var waitingBundle = GoldenScenarioBundles.MissingPostActionStimulus();
        var waitingDir = TempDir();
        var waiting = ScenarioRunner.Run(waitingBundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(waitingDir),
        });
        var quarantined = waiting.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(RecorderTerminal.Quarantined, quarantined.RecorderTerminal);
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.DeriveFromPersisted(waitingBundle, waitingDir, CorrelationOf(waitingBundle)));

        // (b) S1：seal 后篡改 .trace.json（改写 RunId 字段）→ 重算 hash 失配
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var dir = TempDir();
        var run = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(dir),
        });
        Assert.True(run.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, run.Report));
        var sealedArtifact = run.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(RecorderTerminal.Finalized, sealedArtifact.RecorderTerminal);

        var name = SealedTraceStore.Sanitize(CorrelationOf(bundle));
        var jsonPath = Path.Combine(dir, name + ".trace.json");
        var hashPath = Path.Combine(dir, name + ".trace.sha256");
        var tampered = File.ReadAllText(jsonPath).Replace(
            CorrelationOf(bundle), "sim:tampered-run-id", StringComparison.Ordinal);
        Assert.NotEqual(File.ReadAllText(jsonPath), tampered);
        File.WriteAllText(jsonPath, tampered);

        Assert.Throws<SealedTraceIntegrityException>(() => SealedTraceStore.LoadVerified(dir, CorrelationOf(bundle)));
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.DeriveFromPersisted(bundle, dir, CorrelationOf(bundle)));

        // (c) 删除 .sha256 → 完整性无法证明 → fail closed
        File.Delete(hashPath);
        Assert.Throws<SealedTraceIntegrityException>(() => SealedTraceStore.LoadVerified(dir, CorrelationOf(bundle)));
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.DeriveFromPersisted(bundle, dir, CorrelationOf(bundle)));
    }

    /// <summary>
    /// Acceptance 6：S1 async 臂 sealed artifact → DeriveFromPersisted（经
    /// 磁盘 integrity 复核）→ derived bundle → re-drive → Completed /
    /// Completion / 1 effect / 2 consultations（RUN-004 E1 再咨询）/
    /// GoalSatisfied。
    /// </summary>
    [Fact]
    public void AsyncSealedArtifact_Imports_AndReDrives()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var dir = TempDir();

        var source = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(dir),
        });
        Assert.True(source.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, source.Report));
        var artifact = source.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(RecorderTerminal.Finalized, artifact.RecorderTerminal);

        var imported = ScenarioImporter.DeriveFromPersisted(bundle, dir, CorrelationOf(bundle));
        Assert.Equal(2, imported.Bundle.Stimuli.Count);
        imported.Bundle.Verify();

        var reDrive = ScenarioRunner.Run(imported.Bundle);
        var report = reDrive.Report;
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(imported.Bundle, report));
        Assert.Equal(RunDriveStatus.Completed.ToString(), report.RunDriveStatus);
        Assert.Equal(TerminalClassification.Completion, report.Outcome!.Classification);
        Assert.Equal(1, report.EffectDeliveries);
        // RUN-004 E1（causal：d53f3331/ba4b5e9b）：提案耗尽 → StepVerified
        // 再咨询恰一次（同 ImportReDriveTests 归因；bundle 期望已为 2）。
        Assert.Equal(2, report.AgentConsultations);
        Assert.Equal(GoalSatisfaction.Satisfied, report.GoalEvaluation!.Satisfaction);

        // 源/重跑语义等价（同 ImportReDriveTests 断言面）
        Assert.Equal(source.Report.Outcome!.Classification, report.Outcome.Classification);
        Assert.Equal(source.Report.EffectDeliveries, report.EffectDeliveries);
        Assert.Equal(source.Report.AgentConsultations, report.AgentConsultations);
        Assert.Equal(source.Report.GoalEvaluation!.Satisfaction, report.GoalEvaluation.Satisfaction);
    }

    /// <summary>
    /// Review 修法 1/2：emission marker 已入队（入口关闭前），但 consumer
    /// 闸门卡住 flush 直至 DrainTimeout（500ms）⇒ Finalize 返回 Quarantined
    /// （drain 超时诊断 + 未刷盘 accounting 诊断），journal 快照不含未刷盘的
    /// emission（诊断可见）；finalize 后释放闸门 consumer 追平且不崩溃；
    /// 产物经 DeriveFromPersisted 拒绝（Quarantined 门）。
    /// 确定性：ManualResetEventSlim 闸门 + 条件 SpinUntil，无 sleep-race。
    /// </summary>
    [Fact]
    public void EmissionEnqueued_FlushStalledPastDrainTimeout_NotImportable()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var dir = TempDir();
        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        var writer = new AsyncFileTraceWriter(
            new RunCorrelation("sim:" + bundle.ScenarioId),
            new AsyncTraceWriterOptions(dir, DrainTimeout: TimeSpan.FromMilliseconds(500),
                BeforeBatchFlushGate: batchIndex =>
                {
                    if (batchIndex == 0)
                    {
                        entered.Set();
                        release.Wait();
                    }
                }));
        var references = new[] { new TraceReference(TraceReferenceKind.Run, "sim:" + bundle.ScenarioId) };
        var scope = writer.StartOperation(TraceCatalog.PerceptionObserve, null, references);
        scope.Complete(StructuralOutcome.Completed);
        Assert.True(entered.Wait(5000), "consumer 应进入 batch-0 flush 闸门（确定性停摆）");

        // emission 在入口关闭前进入 channel；consumer 卡在 flush 闸门
        writer.MarkOutcomeEmitted();
        var artifact = writer.Finalize();
        Assert.Equal(RecorderTerminal.Quarantined, artifact.RecorderTerminal);
        Assert.Contains(artifact.RecorderDiagnostics, d => d.Reason == "drain-timeout");
        Assert.Contains(artifact.RecorderDiagnostics, d =>
            d.Reason.StartsWith("drain-timeout-unflushed:", StringComparison.Ordinal));
        // journal 只含已刷盘记录 ⇒ 未刷盘 emission 不在快照（诊断可见）
        Assert.Contains(artifact.RecorderDiagnostics, d => d.Reason == "runtime-outcome-emission-not-observed");

        // finalize 之后才释放闸门：consumer 追平、不崩溃（恢复性 cancel 不引入故障）
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => writer.ConsumedRecords >= 1, TimeSpan.FromSeconds(30)),
            "释放闸门后 consumer 应刷盘 batch-0");
        Assert.False(writer.IsFaulted);

        // Quarantined 门：导入拒绝
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.DeriveFromPersisted(bundle, dir, CorrelationOf(bundle)));
    }

    /// <summary>
    /// Review 修法 3：注入 SealPersistFault ⇒ Finalize 不抛出，返回内存
    /// CaptureFailed 产物（seal-persist-failed 诊断）；磁盘不留可导入 seal
    /// 对（final 两个文件与 .tmp 残留均清理）；LoadVerified /
    /// DeriveFromPersisted 均 fail closed；run 本身验收不受影响。
    /// </summary>
    [Fact]
    public void SealPersistFailure_NoThrow_NotImportable()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var dir = TempDir();

        var run = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(dir,
                SealPersistFault: _ => new IOException("injected-seal-persist-failure")),
        });
        Assert.True(run.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, run.Report));

        var artifact = run.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(RecorderTerminal.CaptureFailed, artifact.RecorderTerminal);
        Assert.Contains(artifact.RecorderDiagnostics, d =>
            d.Reason.StartsWith("seal-persist-failed:", StringComparison.Ordinal));

        var name = SealedTraceStore.Sanitize(CorrelationOf(bundle));
        var jsonPath = Path.Combine(dir, name + ".trace.json");
        var hashPath = Path.Combine(dir, name + ".trace.sha256");
        Assert.False(File.Exists(jsonPath), "seal 持久化失败后不得残留 .trace.json");
        Assert.False(File.Exists(hashPath), "seal 持久化失败后不得残留 .trace.sha256");
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));

        Assert.Throws<SealedTraceIntegrityException>(() => SealedTraceStore.LoadVerified(dir, CorrelationOf(bundle)));
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.DeriveFromPersisted(bundle, dir, CorrelationOf(bundle)));
    }
}
