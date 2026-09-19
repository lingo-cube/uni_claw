using System.Text;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// CORE-013 Slice 1 — 文件 append-only journal 的执行源契约测试，
/// 重点证明 CORE-012 Q3 的提交边界限定：
/// 1. CommitResult=success ⇔ 帧完整写入并 Flush（进程死亡后可重读）；
/// 2. torn 尾帧 = 从未提交（截尾忽略，非损坏恢复）；
/// 3. 提交路径同步、无异步缓冲；
/// 4. 无 Attempt ID 的未决发现 + append-only 历史。
/// journal v1 只产生 Success/Failure（Unknown 留给其他传输实现，
/// 三值面与 S9 同 Attempt 重试语义在 EffectBoundaryExecutionSourceTests
/// 的 double 层证明）。
/// </summary>
public sealed class ReliableExecutionJournalTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), "uniclaw-exec-journal-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_file());
        try { Directory.Delete(Path.GetDirectoryName(_path)!, true); } catch { /* temp 清理尽力 */ }
    }

    private string _file() => Path.Combine(_path, "journal.jsonl");

    private static ExecutionRegistration Registration(
        string attemptRef, string effectRef = "effect-x", string subject = "switch.wifi") =>
        new(AttemptId: null, EffectRef: effectRef, IntentId: $"intent-{attemptRef}",
            BindingId: $"bind-{attemptRef}", EffectClass: "tap", TargetSubject: subject,
            TargetValue: "true", RevisionId: "rev-1", RevisionNumber: 1,
            ExecutorId: "DeterministicEffectDriver", AdmissionNote: "admissible:checks:10");

    [Fact]
    public void CommitSuccess_IsFlushedAndRecoverable_AfterTerminateWithoutDispose()
    {
        string attemptId;
        var (journalA, _) = Committed(out attemptId);

        // 模拟进程死亡：不 Dispose，直接丢弃实例（句柄存活，FileShare 允许重开）
        journalA = null!;
        GC.Collect();

        using var journalB = new FileExecutionJournal(_file());
        var restored = journalB.GetAttempt(attemptId);
        Assert.NotNull(restored);
        Assert.Equal("effect-x", restored!.Registration.EffectRef);
        Assert.Equal("bind-a1", restored.Registration.BindingId);
        Assert.Equal(ExecutionAttemptStatus.Completed, restored.Status);
        var receipt = Assert.Single(restored.Entries, e => e.Kind == ExecutionEntryKind.Receipt);
        Assert.Equal("receipt-1", receipt.Receipt!.ReceiptId);
        Assert.Empty(journalB.DiscoverPending()); // 确认 Receipt → 非未决
    }

    [Fact]
    public void TornTrailingFrame_IsNeverCommitted_AndReplayContinues()
    {
        // 纯 prepare + torn 尾帧（无 submission——该 Attempt 处于已提交未发送）
        using var journalA = new FileExecutionJournal(_file());
        var commit = journalA.CommitPrepare(Registration("a1"));
        Assert.Equal(ExecutionCommitOutcome.Success, commit.Outcome);
        var attemptId = commit.AttemptId!;
        journalA.Dispose();

        // 手工追加 torn 尾帧：长度头声明 999 字节，实际只写 5 字节
        using (var raw = new FileStream(_file(), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            raw.Write(BitConverter.GetBytes(999).Reverse().ToArray(), 0, 4);
            raw.Write(new byte[] { 1, 2, 3, 4, 5 }, 0, 5);
            raw.Flush();
        }

        using var journalB = new FileExecutionJournal(_file());
        var restored = journalB.GetAttempt(attemptId);
        Assert.NotNull(restored);
        Assert.Equal(ExecutionAttemptStatus.CommittedPending, restored!.Status); // 无伪造结果
        var pending = Assert.Single(journalB.DiscoverPending());
        Assert.Equal(attemptId, pending.AttemptId);

        // 后续提交继续可用（截尾不损坏 journal）
        var next = journalB.CommitPrepare(Registration("a2"));
        Assert.Equal(ExecutionCommitOutcome.Success, next.Outcome);
        Assert.Equal(2, journalB.DiscoverPending().Count);
    }

    [Fact]
    public void DiscoverPending_TracksStatusTransitions_WithoutAttemptId()
    {
        using var journal = new FileExecutionJournal(_file());
        var first = journal.CommitPrepare(Registration("p1"));
        var second = journal.CommitPrepare(Registration("p2"));
        Assert.Equal(ExecutionCommitOutcome.Success, first.Outcome);
        Assert.Equal(ExecutionCommitOutcome.Success, second.Outcome);
        Assert.Equal(2, journal.DiscoverPending().Count);

        journal.AppendSubmission(first.AttemptId!, "driver-called");
        journal.AppendReceipt(first.AttemptId!, null); // 显式无 Receipt：未决保持
        var pendingFirst = journal.DiscoverPending().Single(p => p.AttemptId == first.AttemptId);
        Assert.Equal(ExecutionAttemptStatus.UnknownOutcome, pendingFirst.Status);

        journal.AppendReceipt(second.AttemptId!, Receipt("receipt-2"));
        var onlyFirst = Assert.Single(journal.DiscoverPending());
        Assert.Equal(first.AttemptId, onlyFirst.AttemptId);
        Assert.Equal(ExecutionAttemptStatus.Completed, journal.GetAttempt(second.AttemptId!)!.Status);
    }

    [Fact]
    public void AppendOnly_HistoryGrows_RegistrationNeverOverwritten()
    {
        using var journal = new FileExecutionJournal(_file());
        var commit = journal.CommitPrepare(Registration("h1"));
        var before = journal.GetAttempt(commit.AttemptId!)!;

        journal.AppendSubmission(commit.AttemptId!, "driver-called");
        journal.AppendReceipt(commit.AttemptId!, Receipt("receipt-9"));
        journal.AppendLateFeedback(commit.AttemptId!, new ExecutionFeedback("fb-1", "late-observer"));

        var after = journal.GetAttempt(commit.AttemptId!)!;
        Assert.Equal(before.Registration, after.Registration);
        Assert.Equal(3, after.Entries.Count);
        Assert.Equal(new[] { ExecutionEntryKind.Submission, ExecutionEntryKind.Receipt, ExecutionEntryKind.LateFeedback },
            after.Entries.Select(e => e.Kind).ToArray());
    }

    [Fact]
    public void LateFeedback_WithUnresolvableCorrelation_IsHeldAndDurable()
    {
        string attemptId;
        var (journalA, _) = Committed(out attemptId, deliverReceipt: false);

        journalA.AppendLateFeedback(null, new ExecutionFeedback("fb-lost", "correlation-lost"));
        journalA = null!;
        GC.Collect();

        using var journalB = new FileExecutionJournal(_file());
        var held = Assert.Single(journalB.UnassociatedFeedback);
        Assert.Equal("fb-lost", held.FeedbackId);
        Assert.Single(journalB.GetAttempt(attemptId)!.Entries); // 未误挂
    }

    [Fact]
    public void LinkRetryAndCompensation_AreDurableAcrossReopen()
    {
        string attemptId;
        var (journalA, _) = Committed(out attemptId);
        var retryId = journalA.LinkRetry(attemptId);
        var compensation = journalA.LinkCompensation(attemptId);
        journalA = null!;
        GC.Collect();

        using var journalB = new FileExecutionJournal(_file());
        var original = journalB.GetAttempt(attemptId)!;
        var retry = journalB.GetAttempt(retryId)!;
        var compensating = journalB.GetAttempt(compensation.NewAttemptId)!;

        Assert.Equal(ExecutionAttemptStatus.Completed, original.Status); // 原记录不被改写
        Assert.Equal(original.Registration.EffectRef, retry.Registration.EffectRef); // 同一逻辑 Effect
        Assert.Equal(attemptId, retry.Registration.RetryOf);
        Assert.Equal(ExecutionAttemptStatus.CommittedPending, retry.Status);
        Assert.Equal(compensation.NewEffectRef, compensating.Registration.EffectRef); // 新 Effect
        Assert.NotEqual(original.Registration.EffectRef, compensation.NewEffectRef);
        Assert.Equal(original.Registration.EffectRef, compensating.Registration.Compensates);
        // 未决 = retry + compensation（原 Attempt 已 Completed，不可再投递）
        Assert.Equal(2, journalB.DiscoverPending().Count);
        Assert.Contains(journalB.DiscoverPending(), p => p.AttemptId == retryId);
        Assert.Contains(journalB.DiscoverPending(), p => p.AttemptId == compensation.NewAttemptId);
    }

    [Fact]
    public void CommitAfterDispose_ReportsFailure_NothingDurable()
    {
        using (var journal = new FileExecutionJournal(_file()))
        {
            journal.Dispose();
            var commit = journal.CommitPrepare(Registration("d1"));
            Assert.Equal(ExecutionCommitOutcome.Failure, commit.Outcome);
            Assert.Null(commit.AttemptId);
        }

        using var reopened = new FileExecutionJournal(_file());
        Assert.Empty(reopened.DiscoverPending());
        Assert.Equal(0, reopened.AttemptCount);
    }

    [Fact]
    public void ExplicitAttemptId_CollisionWithCommitted_IsRejected()
    {
        using var journal = new FileExecutionJournal(_file());
        var first = journal.CommitPrepare(Registration("c1"));
        Assert.Equal(ExecutionCommitOutcome.Success, first.Outcome);

        var collision = journal.CommitPrepare(Registration("c2") with { AttemptId = first.AttemptId });
        Assert.Equal(ExecutionCommitOutcome.Failure, collision.Outcome);
        Assert.Equal(1, journal.AttemptCount);
    }

    // ---- helpers ----

    private (FileExecutionJournal Journal, ExecutionCommit Commit) Committed(
        out string attemptId, bool deliverReceipt = true)
    {
        var journal = new FileExecutionJournal(_file());
        var commit = journal.CommitPrepare(Registration("a1"));
        Assert.Equal(ExecutionCommitOutcome.Success, commit.Outcome);
        attemptId = commit.AttemptId!;
        journal.AppendSubmission(attemptId, "driver-called");
        if (deliverReceipt)
            journal.AppendReceipt(attemptId, Receipt("receipt-1"));
        return (journal, commit);
    }

    private static EffectReceipt Receipt(string receiptId) => new(
        receiptId, "intent-a1", "bind-a1", "switch.wifi", "rev-1", 1,
        DispatchOutcome.DeliveryCompleted, "sim:delivered",
        new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero));
}
