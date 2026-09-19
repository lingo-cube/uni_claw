using UniClaw.Kernel.Trace;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// TRW-001 — 真实异步 Trace writer 验收（state.md Acceptance 1–4 / 7 / 9 的
/// 行为面）：四臂 canonical 等价；writer 减速不进入 kernel critical path；
/// 队列满显式丢弃可诊断且 canonical output 不变；消费侧崩溃 →
/// CaptureFailed 且 canonical output 不变；artifact 内容确定性。
/// 确定性纪律（D7）：一切后台 flush 观察只经 FinalizeArtifact 的 bounded
/// drain，无 sleep-race。
/// </summary>
public sealed class AsyncTraceWriterTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "uniclaw-trw001-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Acceptance 2：sync Enabled / AsyncFile / Disabled / FailingRecorder
    /// 四臂下验收全部通过，semantic digest 完全一致——异步持久化不改变
    /// Runtime 语义输出（ADR-0013）。
    /// </summary>
    [Fact]
    public void AsyncArm_CanonicalEquivalent_FourArms()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        var reports = new[]
        {
            ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Enabled }).Report,
            ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.AsyncFile }).Report,
            ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Disabled }).Report,
            ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.FailingRecorder }).Report,
        };

        foreach (var report in reports)
            Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));

        Assert.NotEmpty(reports[0].SemanticDigest);
        Assert.Equal(reports[0].SemanticDigest, reports[1].SemanticDigest);
        Assert.Equal(reports[0].SemanticDigest, reports[2].SemanticDigest);
        Assert.Equal(reports[0].SemanticDigest, reports[3].SemanticDigest);
    }

    /// <summary>
    /// Acceptance 1：writer 注入 30ms/批减速时，kernel critical path 不被
    /// 放大（slow 与 fast 异步运行的 CriticalPathLatencyMs 同量级）；
    /// FinalizeArtifact 的 bounded drain 之后 consumer 完全追上（ConsumedRecords
    /// 覆盖全部 span/event/emission 记录）且未故障。
    /// </summary>
    [Fact]
    public void SlowWriter_DoesNotBlockRuntime()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        var slow = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(TempDir(), BatchDelay: _ => Task.Delay(30)),
        });
        var fast = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(TempDir()),
        });

        Assert.True(slow.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, slow.Report));
        Assert.True(fast.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, fast.Report));
        Assert.True(
            slow.Report.Metrics.CriticalPathLatencyMs <= fast.Report.Metrics.CriticalPathLatencyMs * 2 + 50,
            $"writer 减速放大了 critical path: slow={slow.Report.Metrics.CriticalPathLatencyMs}ms "
            + $"fast={fast.Report.Metrics.CriticalPathLatencyMs}ms");

        // drain 完成性：journal 全量消费（span + event + emission marker）
        var writer = slow.Host.TraceWriter!;
        var artifact = slow.Host.TraceScope!.FinalizeArtifact();
        Assert.False(writer.IsFaulted);
        Assert.Equal(0, writer.DroppedCount);
        Assert.True(writer.ConsumedRecords >= artifact.Spans.Sum(s => 1 + s.Events.Length) + 1,
            $"consumer 未追上: consumed={writer.ConsumedRecords} "
            + $"expected>={artifact.Spans.Sum(s => 1 + s.Events.Length) + 1}");
        Assert.Equal(RecorderTerminal.Finalized, artifact.RecorderTerminal);
    }

    /// <summary>
    /// Acceptance 3：ChannelCapacity=2 + 20ms 批延迟 → 运行期队列必然溢出：
    /// DroppedCount > 0 且诊断含 capture-drop 记录；但 run 验收与 semantic
    /// digest 同 Disabled 臂完全一致（显式丢弃不改变 canonical output）。
    /// </summary>
    [Fact]
    public void QueueFull_ExplicitDrops_Diagnostics_CanonicalUnchanged()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        var asyncRun = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(TempDir(), ChannelCapacity: 2, BatchDelay: _ => Task.Delay(20)),
        });
        var disabled = ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Disabled });

        Assert.True(asyncRun.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, asyncRun.Report));
        Assert.True(disabled.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, disabled.Report));
        Assert.Equal(disabled.Report.SemanticDigest, asyncRun.Report.SemanticDigest);

        var writer = asyncRun.Host.TraceWriter!;
        var artifact = asyncRun.Host.TraceScope!.FinalizeArtifact();
        Assert.True(writer.DroppedCount > 0, "容量 2 + 20ms 批延迟下应发生显式丢弃");
        Assert.Contains(writer.Diagnostics, d => d.Reason == "capture-dropped");
        // review 收紧语义：DroppedCount > 0 ⇒ 不得 Finalized（降级 Quarantined）
        Assert.Equal(RecorderTerminal.Quarantined, artifact.RecorderTerminal);
        Assert.Contains(artifact.RecorderDiagnostics,
            d => d.Reason == $"dropped-records:{writer.DroppedCount}");
    }

    /// <summary>
    /// Review 修法 1：早期 record 被丢弃（consumer 闸门停摆 + 容量 2）后，
    /// 释放闸门让 emission marker 成功入队并刷盘、drain 完成、无故障——
    /// artifact 仍必须是 Quarantined（DroppedCount > 0 ⇒ 不得 Finalized），
    /// 且诊断显式命名丢弃计数；canonical 输出不受丢弃影响。
    /// 确定性：一切经 ManualResetEventSlim 闸门 / 条件 SpinUntil，无 sleep-race。
    /// </summary>
    [Fact]
    public void DroppedRecords_ThenSuccessfulEmission_NotFinalized()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var dir = TempDir();
        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        var writer = new AsyncFileTraceWriter(
            new RunCorrelation("sim:" + bundle.ScenarioId),
            new AsyncTraceWriterOptions(dir, ChannelCapacity: 2,
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

        // consumer 停摆期间注入 10 条 start record：容量 2 ⇒ 必然显式丢弃
        for (var i = 0; i < 10; i++)
            writer.StartOperation(TraceCatalog.PerceptionObserve, null, references);
        Assert.True(writer.DroppedCount > 0, "consumer 停摆 + 容量 2 下后续 enqueue 应显式丢弃");

        release.Set();
        // 12 条 record（2 初始 + 10 注入）全部落定（consumed ∪ dropped）
        Assert.True(
            SpinWait.SpinUntil(
                () => writer.ConsumedRecords + writer.DroppedCount == 12, TimeSpan.FromSeconds(30)),
            $"consumer 释放后未追平: consumed={writer.ConsumedRecords} dropped={writer.DroppedCount}");

        // 队列已空 ⇒ emission 必然成功入队；drain 完成无故障
        writer.MarkOutcomeEmitted();
        var artifact = writer.Finalize();
        Assert.Equal(RecorderTerminal.Quarantined, artifact.RecorderTerminal);
        Assert.Contains(artifact.RecorderDiagnostics, d => d.Reason == $"dropped-records:{writer.DroppedCount}");
        Assert.DoesNotContain(artifact.RecorderDiagnostics, d => d.Reason == "drain-timeout");
        Assert.DoesNotContain(artifact.RecorderDiagnostics, d => d.Reason == "runtime-outcome-emission-not-observed");

        // canonical run output 不受丢弃影响（独立两臂对照）
        var asyncRun = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(TempDir()),
        });
        var disabled = ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Disabled });
        Assert.True(asyncRun.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, asyncRun.Report));
        Assert.Equal(disabled.Report.SemanticDigest, asyncRun.Report.SemanticDigest);
    }

    /// <summary>
    /// Review 修法 3（诊断有界）：容量 1 + 停摆闸门产生大量丢弃
    /// （DroppedCount &gt; 诊断上限）⇒ artifact RecorderDiagnostics 条数有界
    /// （≤ 32），且有 capped 汇总指示（diagnostics-capped:total=N，N &gt; 32）。
    /// </summary>
    [Fact]
    public void HeavyDropping_DiagnosticsBounded()
    {
        var dir = TempDir();
        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        var writer = new AsyncFileTraceWriter(
            new RunCorrelation("run-heavy-drop"),
            new AsyncTraceWriterOptions(dir, ChannelCapacity: 1,
                BeforeBatchFlushGate: batchIndex =>
                {
                    if (batchIndex == 0)
                    {
                        entered.Set();
                        release.Wait();
                    }
                }));
        var references = new[] { new TraceReference(TraceReferenceKind.Run, "run-heavy-drop") };
        var scope = writer.StartOperation(TraceCatalog.PerceptionObserve, null, references);
        scope.Complete(StructuralOutcome.Completed);
        Assert.True(entered.Wait(5000), "consumer 应进入 batch-0 flush 闸门（确定性停摆）");

        for (var i = 0; i < 40; i++)
            writer.StartOperation(TraceCatalog.PerceptionObserve, null, references);
        Assert.True(writer.DroppedCount > 32, $"应产生超上限的大量丢弃: {writer.DroppedCount}");

        release.Set();
        var artifact = writer.Finalize();
        Assert.InRange(artifact.RecorderDiagnostics.Length, 1, 32);
        Assert.Contains(artifact.RecorderDiagnostics, d =>
            d.Reason.StartsWith("diagnostics-capped:total=", StringComparison.Ordinal));
        Assert.True(writer.CumulativeDiagnosticCount > 32,
            $"累计诊断应超过样本上限: {writer.CumulativeDiagnosticCount}");
        Assert.Equal(RecorderTerminal.Quarantined, artifact.RecorderTerminal);
    }

    /// <summary>
    /// Acceptance 4：消费侧崩溃注入（3 条后抛错）→ FinalizeArtifact 产出
    /// RecorderTerminal=CaptureFailed（该预留终态的首次真实触发路径）；
    /// run 本身照常 Completed / 验收通过，semantic digest 同 Disabled 臂
    /// （writer 故障域完全隔离于 canonical output）。
    /// </summary>
    [Fact]
    public void WriterCrash_CaptureFailed_CanonicalUnchanged()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        var crashed = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(
                TempDir(),
                FaultAfterRecords: consumed => consumed >= 3
                    ? new InvalidOperationException("injected-writer-crash")
                    : null),
        });
        var disabled = ScenarioRunner.Run(bundle, new RunOptions { TraceArm = TraceArm.Disabled });

        Assert.True(crashed.Report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, crashed.Report));
        Assert.Equal(disabled.Report.SemanticDigest, crashed.Report.SemanticDigest);

        var writer = crashed.Host.TraceWriter!;
        var artifact = crashed.Host.TraceScope!.FinalizeArtifact();
        Assert.True(writer.IsFaulted);
        Assert.Equal(RecorderTerminal.CaptureFailed, artifact.RecorderTerminal);
    }

    /// <summary>
    /// Acceptance 7：同一 bundle 两遍异步运行（独立临时目录）→ 两份 sealed
    /// artifact 的 canonical rendering SHA-256 逐字节相等（TraceModel 无
    /// timing 字段，天然确定性），且与各自磁盘 .sha256 一致。
    /// </summary>
    [Fact]
    public void ArtifactContent_Deterministic_TwoRuns()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var dir1 = TempDir();
        var dir2 = TempDir();

        var first = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(dir1),
        });
        var second = ScenarioRunner.Run(bundle, new RunOptions
        {
            TraceArm = TraceArm.AsyncFile,
            AsyncTrace = new AsyncTraceWriterOptions(dir2),
        });

        Assert.True(first.Report.AcceptancePassed);
        Assert.True(second.Report.AcceptancePassed);

        var artifact1 = first.Host.TraceScope!.FinalizeArtifact();
        var artifact2 = second.Host.TraceScope!.FinalizeArtifact();
        Assert.Equal(RecorderTerminal.Finalized, artifact1.RecorderTerminal);
        Assert.Equal(RecorderTerminal.Finalized, artifact2.RecorderTerminal);
        Assert.Equal(artifact1.IntegritySha256, artifact2.IntegritySha256);

        var name = SealedTraceStore.Sanitize("sim:" + bundle.ScenarioId);
        var hash1 = File.ReadAllText(Path.Combine(dir1, name + ".trace.sha256")).Trim();
        var hash2 = File.ReadAllText(Path.Combine(dir2, name + ".trace.sha256")).Trim();
        Assert.Equal(hash1, hash2);
        Assert.Equal(artifact1.IntegritySha256, hash1);
        Assert.Equal(artifact2.IntegritySha256, hash2);
    }
}
