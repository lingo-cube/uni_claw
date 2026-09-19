using System.Text;
using System.Text.Json;

namespace UniClaw.Kernel.Effects.ExecutionSource;

/// <summary>
/// CORE-013 — 文件 append-only journal（CORE-012 Q3=B 默认实现）。
///
/// 提交边界操作定义（Q3 限定落地，CORE-012 计划 §2）：
/// 1. <see cref="CommitPrepare"/> 返回 Success ⇔ 帧完整写入并
///    <c>Flush()</c>（托管缓冲 → OS 文件系统）；进程死亡（含未 Dispose
///    终止）后同机重开同一路径可完整读取（FileShare 允许并存句柄）。
/// 2. 页缓存即达标：故障范围是单机、单进程崩溃/重启；fsync/OS 崩溃/
///    断电、磁盘损坏恢复均为显式 non-goal。
/// 3. torn 尾帧（长度头声明超出实际剩余字节）在重放时截尾忽略 = 该
///    提交从未成功——append-only 的正确语义，不是损坏恢复。
/// 4. 提交路径同步写 + Flush，无异步/批量/后台缓冲。
/// 5. 本实现只产生 Success（已 Flush）/ Failure（IO 异常）；Unknown 留给
///    其他传输实现（CORE-013 决策 2）。
///
/// journal 身份 = 文件路径（组合作用域）；AttemptId 由本源铸造为全局
/// 帧序号（重启续号，不冲突）。单线程 delivery 路径调用，无并发契约。
/// </summary>
public sealed class FileExecutionJournal : IReliableExecutionSource, IDisposable
{
    private sealed class AttemptRecord
    {
        public AttemptRecord(ExecutionRegistration registration, ExecutionAttemptStatus status) =>
            (Registration, Status) = (registration, status);

        public ExecutionRegistration Registration { get; }
        public ExecutionAttemptStatus Status { get; set; }
        public List<ExecutionJournalEntry> Entries { get; } = new();
    }

    /// <summary>journal 帧的序列化载体（k = 判别符；字段按 kind 取用）。</summary>
    private sealed record JournalFrame(
        string K, int Seq, string? AttemptId, string? EffectRef, string? IntentId,
        string? BindingId, string? EffectClass, string? TargetSubject, string? TargetValue,
        string? RevisionId, int RevisionNumber, string? ExecutorId, string? AdmissionNote,
        string? RetryOf, string? Compensates, string? Note, string? ReceiptId,
        string? Outcome, string? Report, string? CompletedAt, string? Reason,
        string? FeedbackId, string? LinkKind);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        IncludeFields = false,
    };

    private readonly FileStream _stream;
    private readonly Dictionary<string, AttemptRecord> _attempts = new(StringComparer.Ordinal);
    private readonly List<ExecutionFeedback> _unassociated = new();
    private int _seq;
    private int _retryLinks;
    private int _compensationLinks;

    public FileExecutionJournal(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("journal 文件路径不能为空", nameof(filePath));
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        _stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        Replay();
    }

    /// <summary>当前持久 Attempt 数（「不自动新建」断言面）。</summary>
    public int AttemptCount => _attempts.Count;

    /// <summary>关联不明确的迟到反馈（保留待关联，不猜测归属）。</summary>
    public IReadOnlyList<ExecutionFeedback> UnassociatedFeedback => _unassociated;

    public ExecutionCommit CommitPrepare(ExecutionRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (registration.AttemptId is { } explicitId)
        {
            // 已存在且 CommitUnknown = 本地提交重试（保持原 Attempt，S9）
            if (_attempts.TryGetValue(explicitId, out var existing))
                return existing.Status == ExecutionAttemptStatus.CommitUnknown
                    ? WritePrepare(registration with { }, outcomeOnFailure: ExecutionCommitOutcome.Failure)
                    : new ExecutionCommit(ExecutionCommitOutcome.Failure, null);
            return WritePrepare(registration with { }, outcomeOnFailure: ExecutionCommitOutcome.Failure);
        }
        return WritePrepare(registration, outcomeOnFailure: ExecutionCommitOutcome.Failure);
    }

    public IReadOnlyList<PendingExecution> DiscoverPending() =>
        _attempts.Values
            .Where(a => a.Status is ExecutionAttemptStatus.CommittedPending
                or ExecutionAttemptStatus.CommitUnknown
                or ExecutionAttemptStatus.Dispatched
                or ExecutionAttemptStatus.UnknownOutcome)
            .Select(a => new PendingExecution(
                a.Registration.AttemptId!, a.Registration.EffectRef, a.Status))
            .ToList();

    public ExecutionAttemptView? GetAttempt(string attemptId) =>
        _attempts.TryGetValue(attemptId, out var attempt)
            ? new ExecutionAttemptView(attempt.Registration, attempt.Status, attempt.Entries.ToList())
            : null;

    public void AppendSubmission(string attemptId, string note)
    {
        var attempt = Require(attemptId);
        WriteFrame(new JournalFrame("submission", ++_seq, attemptId, null, null, null, null, null, null,
            null, 0, null, null, null, null, note, null, null, null, null, null, null, null));
        attempt.Status = ExecutionAttemptStatus.Dispatched;
        attempt.Entries.Add(new ExecutionJournalEntry(ExecutionEntryKind.Submission, _seq, note));
    }

    public void AppendReceipt(string attemptId, EffectReceipt? receipt)
    {
        var attempt = Require(attemptId);
        ExecutionReceiptView? view = receipt is null
            ? null
            : new ExecutionReceiptView(
                receipt.ReceiptId, receipt.Outcome.ToString(), receipt.Report,
                receipt.DispatchedAt.ToString("O"), receipt.Reason);
        WriteFrame(new JournalFrame("receipt", ++_seq, attemptId, null, null, null, null, null, null,
            null, 0, null, null, null, null,
            view?.ReceiptId ?? "no-receipt:pending",
            view?.ReceiptId, view?.Outcome, view?.Report, view?.CompletedAt, view?.Reason,
            null, null));
        attempt.Status = receipt is not null && receipt.Outcome == DispatchOutcome.DeliveryCompleted
            ? ExecutionAttemptStatus.Completed
            : ExecutionAttemptStatus.UnknownOutcome;
        attempt.Entries.Add(new ExecutionJournalEntry(
            ExecutionEntryKind.Receipt, _seq, view?.ReceiptId ?? "no-receipt:pending", view));
    }

    public void AppendLateFeedback(string? attemptId, ExecutionFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        if (attemptId is not null && _attempts.TryGetValue(attemptId, out var attempt))
        {
            WriteFrame(new JournalFrame("feedback", ++_seq, attemptId, null, null, null, null, null, null,
                null, 0, null, null, null, null, feedback.Note, null, null, null, null, null,
                feedback.FeedbackId, null));
            attempt.Entries.Add(new ExecutionJournalEntry(
                ExecutionEntryKind.LateFeedback, _seq, feedback.FeedbackId, Feedback: feedback));
        }
        else
        {
            // 关联不明确：持久保留待关联（跨重启存活）
            WriteFrame(new JournalFrame("feedback", ++_seq, null, null, null, null, null, null, null,
                null, 0, null, null, null, null, feedback.Note, null, null, null, null, null,
                feedback.FeedbackId, null));
            _unassociated.Add(feedback);
        }
    }

    public string LinkRetry(string attemptId)
    {
        var original = Require(attemptId);
        var retryId = $"{attemptId}#r{++_retryLinks}";
        WriteLink(original, retryId, original.Registration.EffectRef,
            original.Registration with { AttemptId = retryId, RetryOf = attemptId, Compensates = null },
            "retry");
        return retryId;
    }

    public ExecutionLink LinkCompensation(string attemptId)
    {
        var original = Require(attemptId);
        var newEffectRef = $"{original.Registration.EffectRef}::comp{++_compensationLinks}";
        var newAttemptId = $"{attemptId}#c{_compensationLinks}";
        WriteLink(original, newAttemptId, newEffectRef,
            original.Registration with
            {
                AttemptId = newAttemptId, EffectRef = newEffectRef,
                RetryOf = null, Compensates = original.Registration.EffectRef,
            },
            "compensation");
        return new ExecutionLink(newAttemptId, newEffectRef);
    }

    public void Dispose() => _stream.Dispose();

    // ---- 内部：帧写入与重放 ----

    private ExecutionCommit WritePrepare(
        ExecutionRegistration registration, ExecutionCommitOutcome outcomeOnFailure)
    {
        var seq = _seq + 1;
        var attemptId = registration.AttemptId ?? $"attempt-{seq}";
        var registration_ = registration with { AttemptId = attemptId };
        var frame = new JournalFrame("prepare", seq, attemptId,
            registration_.EffectRef, registration_.IntentId, registration_.BindingId,
            registration_.EffectClass, registration_.TargetSubject, registration_.TargetValue,
            registration_.RevisionId, registration_.RevisionNumber, registration_.ExecutorId,
            registration_.AdmissionNote, registration_.RetryOf, registration_.Compensates,
            null, null, null, null, null, null, null, null);
        if (!TryWriteFrame(frame))
            return new ExecutionCommit(outcomeOnFailure, null);
        _seq = seq;
        _attempts[attemptId] = new AttemptRecord(registration_, ExecutionAttemptStatus.CommittedPending);
        return new ExecutionCommit(ExecutionCommitOutcome.Success, attemptId);
    }

    private void WriteLink(
        AttemptRecord original, string newAttemptId, string newEffectRef,
        ExecutionRegistration newRegistration, string linkKind)
    {
        var seq = _seq + 1;
        var frame = new JournalFrame("link", seq, newAttemptId,
            newRegistration.EffectRef, newRegistration.IntentId, newRegistration.BindingId,
            newRegistration.EffectClass, newRegistration.TargetSubject, newRegistration.TargetValue,
            newRegistration.RevisionId, newRegistration.RevisionNumber, newRegistration.ExecutorId,
            newRegistration.AdmissionNote, newRegistration.RetryOf, newRegistration.Compensates,
            null, null, null, null, null, null, null, linkKind);
        if (!TryWriteFrame(frame))
            throw new IOException($"journal 写入失败：link({linkKind}) {newAttemptId}");
        _seq = seq;
        _attempts[newAttemptId] = new AttemptRecord(newRegistration, ExecutionAttemptStatus.CommittedPending);
    }

    private void WriteFrame(JournalFrame frame)
    {
        if (!TryWriteFrame(frame))
            throw new IOException($"journal 写入失败：{frame.K} {frame.AttemptId}");
        _seq = frame.Seq;
    }

    /// <summary>同步写 + Flush（提交边界判定点）；IO 异常 → false（不更新内存态）。</summary>
    private bool TryWriteFrame(JournalFrame frame)
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(frame, Json);
            var header = BitConverter.GetBytes(payload.Length);
            if (BitConverter.IsLittleEndian)
                (header[0], header[1], header[2], header[3]) = (header[3], header[2], header[1], header[0]);
            _stream.Write(header, 0, 4);
            _stream.Write(payload, 0, payload.Length);
            _stream.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private void Replay()
    {
        _stream.Position = 0;
        using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
        long consumed = 0;
        while (_stream.Length - consumed >= 4)
        {
            var header = reader.ReadBytes(4);
            consumed += 4;
            var length = BitConverter.ToInt32(
                BitConverter.IsLittleEndian
                    ? new[] { header[3], header[2], header[1], header[0] }
                    : header, 0);
            if (length <= 0 || _stream.Length - consumed < length)
                break; // torn 尾帧：从未提交（截尾忽略，非损坏恢复）
            var payload = reader.ReadBytes(length);
            consumed += length;
            JournalFrame frame;
            try
            {
                frame = JsonSerializer.Deserialize<JournalFrame>(payload, Json)
                    ?? throw new IOException("journal 帧反序列化为 null");
            }
            catch (JsonException)
            {
                // 长度完整的帧体损坏 = 声明范围外故障：fail closed（显式失败，不静默截尾）
                throw new IOException("journal 帧损坏（超出单进程崩溃/重启声明范围）");
            }
            Apply(frame);
        }
        _stream.Position = _stream.Length;
    }

    private void Apply(JournalFrame frame)
    {
        _seq = Math.Max(_seq, frame.Seq);
        switch (frame.K)
        {
            case "prepare":
                _attempts[frame.AttemptId!] = new AttemptRecord(
                    ToRegistration(frame), ExecutionAttemptStatus.CommittedPending);
                break;
            case "submission":
            {
                var attempt = ReplayRequire(frame.AttemptId!);
                attempt.Status = ExecutionAttemptStatus.Dispatched;
                attempt.Entries.Add(new ExecutionJournalEntry(
                    ExecutionEntryKind.Submission, frame.Seq, frame.Note ?? ""));
                break;
            }
            case "receipt":
            {
                var attempt = ReplayRequire(frame.AttemptId!);
                var view = frame.ReceiptId is null
                    ? null
                    : new ExecutionReceiptView(
                        frame.ReceiptId, frame.Outcome ?? "", frame.Report ?? "",
                        frame.CompletedAt ?? "", frame.Reason);
                attempt.Status = frame.ReceiptId is not null
                    && frame.Outcome == DispatchOutcome.DeliveryCompleted.ToString()
                    ? ExecutionAttemptStatus.Completed
                    : ExecutionAttemptStatus.UnknownOutcome;
                attempt.Entries.Add(new ExecutionJournalEntry(
                    ExecutionEntryKind.Receipt, frame.Seq, frame.Note ?? frame.ReceiptId ?? "", view));
                break;
            }
            case "feedback":
            {
                if (frame.AttemptId is not null
                    && _attempts.TryGetValue(frame.AttemptId, out var feedbackTarget))
                    feedbackTarget.Entries.Add(new ExecutionJournalEntry(
                        ExecutionEntryKind.LateFeedback, frame.Seq, frame.FeedbackId ?? "",
                        Feedback: new ExecutionFeedback(frame.FeedbackId ?? "", frame.Note ?? "")));
                else
                    _unassociated.Add(new ExecutionFeedback(frame.FeedbackId ?? "", frame.Note ?? ""));
                break;
            }
            case "link":
                if (frame.LinkKind == "retry")
                    _retryLinks++;
                else
                    _compensationLinks++;
                _attempts[frame.AttemptId!] = new AttemptRecord(
                    ToRegistration(frame), ExecutionAttemptStatus.CommittedPending);
                break;
            default:
                throw new IOException($"未知 journal 帧类型: {frame.K}");
        }
    }

    private ExecutionRegistration ToRegistration(JournalFrame frame) => new(
        frame.AttemptId, frame.EffectRef ?? "", frame.IntentId ?? "", frame.BindingId ?? "",
        frame.EffectClass ?? "", frame.TargetSubject ?? "", frame.TargetValue,
        frame.RevisionId ?? "", frame.RevisionNumber, frame.ExecutorId ?? "",
        frame.AdmissionNote, frame.RetryOf, frame.Compensates);

    private AttemptRecord Require(string attemptId) =>
        _attempts.TryGetValue(attemptId, out var attempt)
            ? attempt
            : throw new InvalidOperationException($"unknown attempt id: {attemptId}");

    private AttemptRecord ReplayRequire(string attemptId) =>
        _attempts.TryGetValue(attemptId, out var attempt)
            ? attempt
            : throw new IOException($"journal 重放失序：帧引用未知 attempt {attemptId}");
}
