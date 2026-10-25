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
/// 4. ADR-0055：原生 WriteAsync/FlushAsync 确认；结果原两帧合法合批，准备独立提交。
/// 5. 本实现只产生 Success（已 Flush）/ Failure（IO 异常）；Unknown 留给
///    其他传输实现（CORE-013 决策 2）。
///
/// journal 身份 = 文件路径（组合作用域）；AttemptId 由本源铸造为全局
/// 帧序号（重启续号，不冲突）。单线程 delivery 路径调用，无并发契约。
/// </summary>
public sealed class FileExecutionJournal : IAsyncReliableExecutionSource, IDisposable, IAsyncDisposable
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
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly int _maxBatchBytes;
    private bool _faulted, _disposed;
    /// <summary>Actual managed calls, not physical disk syscalls.</summary>
    public long ManagedWriteCalls { get; private set; }
    public long ManagedFlushCalls { get; private set; }
    public long FrameBytesSubmitted { get; private set; }
    public int CommittedBatches { get; private set; }
    public int PeakBatchRecords { get; private set; }
    public int PeakBatchBytes { get; private set; }
    private int _seq;
    private int _retryLinks;
    private int _compensationLinks;

    public FileExecutionJournal(string filePath, int maxBatchBytes = 16 * 1024 * 1024)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("journal 文件路径不能为空", nameof(filePath));
        if (maxBatchBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBatchBytes));
        _maxBatchBytes = maxBatchBytes;
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        _stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
        try { Replay(); } catch { _stream.Dispose(); throw; }
    }

    /// <summary>当前持久 Attempt 数（「不自动新建」断言面）。</summary>
    public int AttemptCount => _attempts.Count;

    /// <summary>关联不明确的迟到反馈（保留待关联，不猜测归属）。</summary>
    public IReadOnlyList<ExecutionFeedback> UnassociatedFeedback => _unassociated;

    public ExecutionCommit CommitPrepare(ExecutionRegistration registration) => CommitPrepareAsync(registration).AsTask().GetAwaiter().GetResult();
    public async ValueTask<ExecutionCommit> CommitPrepareAsync(ExecutionRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        EnterWrite(cancellationToken);
        try
        {
            if (_faulted || _disposed) return new(ExecutionCommitOutcome.Failure, null);
            if (registration.AttemptId is { } existingId && _attempts.TryGetValue(existingId, out var existing)
                && existing.Status != ExecutionAttemptStatus.CommitUnknown) return new(ExecutionCommitOutcome.Failure, null);
            var id = registration.AttemptId ?? $"attempt-{_seq + 1}";
            var frame = Create("prepare", _seq + 1, id, registration with { AttemptId = id });
            return await TryWriteFramesAsync([frame]).ConfigureAwait(false)
                ? new(ExecutionCommitOutcome.Success, id) : new(ExecutionCommitOutcome.Failure, null);
        }
        finally { _writeGate.Release(); }
    }

    public IReadOnlyList<PendingExecution> DiscoverPending() => _attempts.Values
        .Where(a => a.Status is ExecutionAttemptStatus.CommittedPending or ExecutionAttemptStatus.CommitUnknown
            or ExecutionAttemptStatus.Dispatched or ExecutionAttemptStatus.UnknownOutcome)
        .Select(a => new PendingExecution(a.Registration.AttemptId!, a.Registration.EffectRef, a.Status)).ToArray();
    public ExecutionAttemptView? GetAttempt(string attemptId) => _attempts.TryGetValue(attemptId, out var attempt)
        ? new(attempt.Registration, attempt.Status, attempt.Entries.ToArray()) : null;

    public void AppendSubmission(string attemptId, string note) => AppendSubmissionAsync(attemptId, note).AsTask().GetAwaiter().GetResult();
    public ValueTask AppendSubmissionAsync(string attemptId, string note, CancellationToken cancellationToken = default) =>
        AppendAsync(() => { Require(attemptId); return [Create("submission", _seq + 1, attemptId, note: note)]; }, cancellationToken);
    public void AppendReceipt(string attemptId, EffectReceipt? receipt) => AppendReceiptAsync(attemptId, receipt).AsTask().GetAwaiter().GetResult();
    public ValueTask AppendReceiptAsync(string attemptId, EffectReceipt? receipt, CancellationToken cancellationToken = default) =>
        AppendAsync(() => { Require(attemptId); return [ReceiptFrame(_seq + 1, attemptId, receipt)]; }, cancellationToken);
    public ValueTask AppendResultAsync(string attemptId, string submissionNote, EffectReceipt receipt, CancellationToken cancellationToken = default) =>
        AppendAsync(() => { Require(attemptId); return [Create("submission", _seq + 1, attemptId, note: submissionNote), ReceiptFrame(_seq + 2, attemptId, receipt)]; }, cancellationToken);
    public void AppendLateFeedback(string? attemptId, ExecutionFeedback feedback) => AppendLateFeedbackAsync(attemptId, feedback).AsTask().GetAwaiter().GetResult();
    public ValueTask AppendLateFeedbackAsync(string? attemptId, ExecutionFeedback feedback, CancellationToken cancellationToken = default) =>
        AppendAsync(() => { ArgumentNullException.ThrowIfNull(feedback); return [Create("feedback", _seq + 1,
            attemptId is not null && _attempts.ContainsKey(attemptId) ? attemptId : null, note: feedback.Note, feedback: feedback.FeedbackId)]; }, cancellationToken);

    public string LinkRetry(string attemptId) => LinkRetryAsync(attemptId).AsTask().GetAwaiter().GetResult();
    public async ValueTask<string> LinkRetryAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        string id = "";
        await AppendAsync(() =>
        {
            var original = Require(attemptId); id = $"{attemptId}#r{_retryLinks + 1}";
            return [Create("link", _seq + 1, id, original.Registration with { AttemptId = id, RetryOf = attemptId, Compensates = null }, link: "retry")];
        }, cancellationToken).ConfigureAwait(false);
        return id;
    }
    public ExecutionLink LinkCompensation(string attemptId) => LinkCompensationAsync(attemptId).AsTask().GetAwaiter().GetResult();
    public async ValueTask<ExecutionLink> LinkCompensationAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        string id = "", effect = "";
        await AppendAsync(() =>
        {
            var original = Require(attemptId); id = $"{attemptId}#c{_compensationLinks + 1}";
            effect = $"{original.Registration.EffectRef}::comp{_compensationLinks + 1}";
            return [Create("link", _seq + 1, id, original.Registration with { AttemptId = id, EffectRef = effect, RetryOf = null,
                Compensates = original.Registration.EffectRef }, link: "compensation")];
        }, cancellationToken).ConfigureAwait(false);
        return new(id, effect);
    }

    private static JournalFrame Create(string kind, int sequence, string? id, ExecutionRegistration? r = null,
        string? note = null, EffectReceipt? receipt = null, string? feedback = null, string? link = null) =>
        new(kind, sequence, id, r?.EffectRef, r?.IntentId, r?.BindingId, r?.EffectClass, r?.TargetSubject, r?.TargetValue,
            r?.RevisionId, r?.RevisionNumber ?? 0, r?.ExecutorId, r?.AdmissionNote, r?.RetryOf, r?.Compensates,
            note, receipt?.ReceiptId, receipt?.Outcome.ToString(), receipt?.Report, receipt?.DispatchedAt.ToString("O"), receipt?.Reason, feedback, link);
    private static JournalFrame ReceiptFrame(int sequence, string id, EffectReceipt? receipt) =>
        Create("receipt", sequence, id, note: receipt?.ReceiptId ?? "no-receipt:pending", receipt: receipt);

    private void EnterWrite(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_writeGate.Wait(0)) throw new InvalidOperationException("execution-write-in-progress");
    }
    private async ValueTask AppendAsync(Func<JournalFrame[]> prepare, CancellationToken cancellationToken)
    {
        EnterWrite(cancellationToken);
        try
        {
            if (_faulted || _disposed) throw new IOException("execution-journal-faulted");
            if (!await TryWriteFramesAsync(prepare()).ConfigureAwait(false)) throw new IOException("execution-journal-write-failed");
        }
        finally { _writeGate.Release(); }
    }
    private async ValueTask<bool> TryWriteFramesAsync(JournalFrame[] frames)
    {
        try
        {
            if (frames.Length is < 1 or > 2) throw new InvalidDataException("execution-batch-limit");
            using var buffer = new MemoryStream();
            foreach (var frame in frames)
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(frame, Json);
                if (payload.Length + 4L > _maxBatchBytes - buffer.Length) throw new InvalidDataException("execution-batch-exceeds-byte-limit");
                var header = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
                buffer.Write(header); buffer.Write(payload);
            }
            var bytes = buffer.ToArray();
            PeakBatchRecords = Math.Max(PeakBatchRecords, frames.Length); PeakBatchBytes = Math.Max(PeakBatchBytes, bytes.Length);
            ManagedWriteCalls++; FrameBytesSubmitted += bytes.Length;
            await _stream.WriteAsync(bytes).ConfigureAwait(false);
            ManagedFlushCalls++; await _stream.FlushAsync().ConfigureAwait(false);
            foreach (var frame in frames) Apply(frame);
            CommittedBatches++; return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidDataException)
        { _faulted = true; return false; }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try { if (!_disposed) { _disposed = true; await _stream.DisposeAsync().ConfigureAwait(false); } }
        finally { _writeGate.Release(); }
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
