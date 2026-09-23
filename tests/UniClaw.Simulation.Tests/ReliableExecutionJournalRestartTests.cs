using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// CORE-013 Slice 4 — 产品可靠执行源（真实文件 journal）跨「进程死亡」
/// 恢复场景（CORE-012 计划 §4 Slice 4）。
///
/// 仿真协议：Host A = Compose(+journal) + Admit + Activate（+DriveOnce，
/// 真实产品路径 dispatch）→ 丢弃 host 与 journal 实例（不 Dispose——
/// 模拟进程死亡；FileShare 允许重开）→ Host B = 新组合 + 新 journal
/// 实例重开同一路径 → 无 Attempt ID 恢复发现。
///
/// 场景映射（vs 仿真契约 §6）：S4/S6/S7/S8/S10/S11/S12 在此经产品 seam
/// 证明；S5（已提交未发送）与 S9（本地提交重试保持原 Attempt）的产品
/// Dispatch 内无故障注入点——S5 由 journal 纯 prepare + torn 尾帧测试、
/// S9 由执行源契约 double 测试证明（ReliableExecutionJournalTests /
/// EffectBoundaryExecutionSourceTests）。
///
/// S6 的故障注入 = 追加故障 journal 包装器（AppendReceipt 抛 IO）：
/// 这正是产品「driver 已调用后追加失败不吞 delivery、记录停留
/// pending-unknown」路径（CORE-012 计划 §3.1）的端到端验证。
/// </summary>
public sealed class ReliableExecutionJournalRestartTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "uniclaw-exec-restart-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp 清理尽力 */ }
    }

    private string JournalPath => Path.Combine(_dir, "journal.jsonl");

    /// <summary>S6 注入点：driver 已调用后的追加故障（其余全部委托真 journal）。</summary>
    private sealed class AppendFaultingJournal : IReliableExecutionSource
    {
        private readonly IReliableExecutionSource _inner;

        public AppendFaultingJournal(IReliableExecutionSource inner) => _inner = inner;

        public ExecutionCommit CommitPrepare(ExecutionRegistration registration) =>
            _inner.CommitPrepare(registration);
        public IReadOnlyList<PendingExecution> DiscoverPending() => _inner.DiscoverPending();
        public ExecutionAttemptView? GetAttempt(string attemptId) => _inner.GetAttempt(attemptId);
        public void AppendSubmission(string attemptId, string note) => _inner.AppendSubmission(attemptId, note);
        public void AppendReceipt(string attemptId, EffectReceipt? receipt) =>
            throw new IOException("append-fault-after-driver");
        public void AppendLateFeedback(string? attemptId, ExecutionFeedback feedback) =>
            _inner.AppendLateFeedback(attemptId, feedback);
        public string LinkRetry(string attemptId) => _inner.LinkRetry(attemptId);
        public ExecutionLink LinkCompensation(string attemptId) => _inner.LinkCompensation(attemptId);
    }

    private static SimulationHost Boot(TraceArm arm, IReliableExecutionSource source)
    {
        var bundle = GoldenScenarioBundles.TwoStepMissingMiddleEvidence();
        var host = SimulationHost.Compose(
            bundle, new RunOptions { TraceArm = arm }, null, source);
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        return host;
    }

    // ---- S4：未提交任何执行记录的「崩溃」→ 无可发送 Attempt ----

    [Fact]
    public void S04_NoDispatchBeforeTerminate_JournalEmpty_NothingDiscoverable()
    {
        var hostA = Boot(TraceArm.Enabled, new FileExecutionJournal(JournalPath));

        // 模拟进程死亡：直接丢弃（不 Dispose、不 Drive）
        hostA = null!;
        GC.Collect();

        using var journalB = new FileExecutionJournal(JournalPath);
        var hostB = Boot(TraceArm.Enabled, journalB);

        Assert.Equal(0, journalB.AttemptCount);
        Assert.Empty(journalB.DiscoverPending()); // 无可发送 Attempt，不得解释为成功/失败/已发送
        Assert.Equal(0, hostB.EffectDeliveryCount);
    }

    // ---- S7：Receipt 已追加 → 重启可还原、脱离未决（不重复投递） ----

    [Fact]
    public void S07_CompletedDispatch_ReceiptRestorable_AfterRestart_NotPending()
    {
        var journalA = new FileExecutionJournal(JournalPath);
        var hostA = Boot(TraceArm.Enabled, journalA);
        var drive = hostA.DriveOnce();
        Assert.Equal(RunDriveStatus.WaitingForInput, drive.Status);
        Assert.Equal(1, hostA.EffectDeliveryCount);
        var productReceiptId = hostA.Facts.EffectReceipts.Single().ReceiptId;

        hostA = null!;
        journalA = null!;
        GC.Collect();

        using var journalB = new FileExecutionJournal(JournalPath);
        var hostB = Boot(TraceArm.Enabled, journalB);

        // 确认 Receipt → 不在未决集合：重启不得经 pending 路径重复投递
        Assert.Empty(journalB.DiscoverPending());
        var restored = journalB.GetAttempt("attempt-1");
        Assert.NotNull(restored);
        Assert.Equal(ExecutionAttemptStatus.Completed, restored!.Status);
        var entry = Assert.Single(restored.Entries, e => e.Kind == ExecutionEntryKind.Receipt);
        Assert.Equal(productReceiptId, entry.Receipt!.ReceiptId); // 产品 receipt 关联原样还原
        Assert.Equal("DeterministicEffectDriver", restored.Registration.ExecutorId);
        Assert.Equal("tap", restored.Registration.EffectClass);
        Assert.Equal(0, hostB.EffectDeliveryCount);
    }

    // ---- S6：driver 已调用、追加故障 → 记录停留未决未知，重启可发现 ----

    [Fact]
    public void S06_AppendFaultAfterDriver_AttemptStaysPendingUnknown_AfterRestart()
    {
        var journalA = new FileExecutionJournal(JournalPath);
        var hostA = Boot(TraceArm.Enabled, new AppendFaultingJournal(journalA));

        // dispatch 正常完成：driver 已调用、kernel 收到 receipt（追加故障被
        // boundary 吞掉，不吞 delivery——CORE-012 计划 §3.1 语义）
        var drive = hostA.DriveOnce();
        Assert.Equal(RunDriveStatus.WaitingForInput, drive.Status);
        Assert.Equal(1, hostA.EffectDeliveryCount);
        Assert.Single(hostA.Facts.EffectReceipts);

        hostA = null!;
        journalA = null!;
        GC.Collect();

        using var journalB = new FileExecutionJournal(JournalPath);
        var hostB = Boot(TraceArm.Enabled, journalB);

        // 原 Attempt 保持未知：submission 已落、无 Receipt、非成功非失败
        var pending = Assert.Single(journalB.DiscoverPending());
        Assert.Equal("attempt-1", pending.AttemptId);
        Assert.Equal(ExecutionAttemptStatus.Dispatched, pending.Status);
        var restored = journalB.GetAttempt("attempt-1")!;
        Assert.DoesNotContain(restored.Entries, e => e.Kind == ExecutionEntryKind.Receipt);

        // 不自动新投递：Host B 零 driver 调用、零新 Attempt
        Assert.Equal(0, hostB.EffectDeliveryCount);
        Assert.Equal(1, journalB.AttemptCount);

        // Host B 协调动作：显式登记「仍无 Receipt」→ 仍未决，不伪造失败
        journalB.AppendReceipt("attempt-1", null);
        var declared = journalB.GetAttempt("attempt-1")!;
        Assert.Equal(ExecutionAttemptStatus.UnknownOutcome, declared.Status);
        Assert.Single(journalB.DiscoverPending());
    }

    // ---- S12：Trace 两臂（关闭/写入失败）不影响 journal 恢复结果 ----

    [Fact]
    public void S12_TraceDisabled_RecoveryUnaffected() =>
        S12_RecoveryUnaffectedByTraceArm(TraceArm.Disabled);

    [Fact]
    public void S12_TraceRecorderFailing_RecoveryUnaffected() =>
        S12_RecoveryUnaffectedByTraceArm(TraceArm.FailingRecorder);

    private void S12_RecoveryUnaffectedByTraceArm(TraceArm arm)
    {
        var journalA = new FileExecutionJournal(JournalPath);
        var hostA = Boot(arm, journalA);
        Assert.Equal(RunDriveStatus.WaitingForInput, hostA.DriveOnce().Status);
        var productReceiptId = hostA.Facts.EffectReceipts.Single().ReceiptId;
        hostA = null!;
        journalA = null!;
        GC.Collect();

        using var journalB = new FileExecutionJournal(JournalPath);
        var hostB = Boot(arm, journalB);

        Assert.Empty(journalB.DiscoverPending());
        var restored = journalB.GetAttempt("attempt-1")!;
        Assert.Equal(ExecutionAttemptStatus.Completed, restored.Status);
        Assert.Equal(productReceiptId,
            restored.Entries.Single(e => e.Kind == ExecutionEntryKind.Receipt).Receipt!.ReceiptId);
        Assert.Equal(0, hostB.EffectDeliveryCount);
    }

    // ---- S8：Host B 协调——迟到反馈追加不覆盖、待关联可再重启存活 ----

    [Fact]
    public void S08_HostBCoordination_LateFeedbackAppended_HeldFeedbackDurable()
    {
        using (var journalA = new FileExecutionJournal(JournalPath))
        {
            var hostA = Boot(TraceArm.Enabled, journalA);
            Assert.Equal(RunDriveStatus.WaitingForInput, hostA.DriveOnce().Status);
            hostA = null!;
        }

        using var journalB = new FileExecutionJournal(JournalPath);
        var before = journalB.GetAttempt("attempt-1")!;
        journalB.AppendLateFeedback("attempt-1", new ExecutionFeedback("fb-late", "late-observer"));
        journalB.AppendLateFeedback(null, new ExecutionFeedback("fb-lost", "correlation-lost"));

        var after = journalB.GetAttempt("attempt-1")!;
        Assert.Equal(before.Registration, after.Registration); // 准备集合不被覆盖
        Assert.Equal(before.Entries.Count + 1, after.Entries.Count);

        // 再一次「重启」：协调动作的记录同样持久
        using var journalC = new FileExecutionJournal(JournalPath);
        var reopened = journalC.GetAttempt("attempt-1")!;
        Assert.Contains(reopened.Entries, e =>
            e.Kind == ExecutionEntryKind.LateFeedback && e.Feedback!.FeedbackId == "fb-late");
        var held = Assert.Single(journalC.UnassociatedFeedback);
        Assert.Equal("fb-lost", held.FeedbackId);
    }

    // ---- S10/S11：Host B 协调——外部重试与补偿关联持久 ----

    [Fact]
    public void S10_S11_HostBCoordination_RetryAndCompensationLinksDurable()
    {
        using (var journalA = new FileExecutionJournal(JournalPath))
        {
            var hostA = Boot(TraceArm.Enabled, journalA);
            Assert.Equal(RunDriveStatus.WaitingForInput, hostA.DriveOnce().Status);
            hostA = null!;
        }

        string retryId;
        ExecutionLink compensation;
        using (var journalB = new FileExecutionJournal(JournalPath))
        {
            retryId = journalB.LinkRetry("attempt-1");
            compensation = journalB.LinkCompensation("attempt-1");
        }

        using var journalC = new FileExecutionJournal(JournalPath);
        var original = journalC.GetAttempt("attempt-1")!;
        var retry = journalC.GetAttempt(retryId)!;
        var compensating = journalC.GetAttempt(compensation.NewAttemptId)!;

        Assert.Equal(ExecutionAttemptStatus.Completed, original.Status); // 原 Attempt 不被改写
        Assert.Equal(original.Registration.EffectRef, retry.Registration.EffectRef); // 同一逻辑 Effect
        Assert.Equal("attempt-1", retry.Registration.RetryOf); // S10 重试关系保留
        Assert.Equal(ExecutionAttemptStatus.CommittedPending, retry.Status);
        Assert.NotEqual(original.Registration.EffectRef, compensation.NewEffectRef); // S11 新 Effect
        Assert.Equal(original.Registration.EffectRef, compensating.Registration.Compensates);
        Assert.Null(compensating.Registration.RetryOf); // 补偿不是重试
    }
}
