namespace UniClaw.Kernel.Effects.ExecutionSource;

/// <summary>
/// 可靠提交结果三值（CORE-009 契约 / CORE-012 Q3）。Success = 记录已到达
/// 实现声明的故障范围内可恢复读取的提交边界；仅写入内存、发起异步写或
/// 依赖未定义缓冲的实现不得报 Success。
/// </summary>
public enum ExecutionCommitOutcome
{
    Success,
    Failure,
    Unknown,
}

/// <summary>
/// Attempt 在执行源中的状态。未决（DiscoverPending 语义）= 已越过
/// commit-success 但无确认 Receipt：CommittedPending / CommitUnknown /
/// Dispatched / UnknownOutcome。
/// </summary>
public enum ExecutionAttemptStatus
{
    /// <summary>提交成功，尚未发送。</summary>
    CommittedPending,

    /// <summary>提交结果未知（记录已落，需协调；重试保持原 Attempt）。</summary>
    CommitUnknown,

    /// <summary>提交确定性失败（不可发送）。</summary>
    CommitFailed,

    /// <summary>driver 已被调用，尚无 Receipt（未知语义）。</summary>
    Dispatched,

    /// <summary>显式无 Receipt / Receipt 未确认：保持未决，不伪造失败。</summary>
    UnknownOutcome,

    /// <summary>已追加确定性 Receipt（完成或确定性失败，Attempt 已决）。</summary>
    Completed,
}

/// <summary>追加条目类型（append-only 历史）。</summary>
public enum ExecutionEntryKind
{
    Submission,
    Receipt,
    LateFeedback,
}

/// <summary>
/// 准备集合（CORE-009 契约：Effect / Attempt / 请求 / Binding / 固定依据 /
/// 执行端 / 准入依据的 correlation 原语）。只存不可变关联事实——不重铸
/// CanonicalBinding、不重判 admissibility（CORE-012 Q4 冻结边界）。
/// AttemptId：null = 由执行源铸造；非 null 且对应 CommitUnknown 记录 =
/// 本地提交重试（保持原 Attempt，CORE-010 计划 S9）。
/// </summary>
public sealed record ExecutionRegistration(
    string? AttemptId,
    string EffectRef,
    string IntentId,
    string BindingId,
    string EffectClass,
    string TargetSubject,
    string? TargetValue,
    string RevisionId,
    int RevisionNumber,
    string ExecutorId,
    string? AdmissionNote,
    string? RetryOf = null,
    string? Compensates = null);

/// <summary>CommitPrepare 结果：三值 outcome + 成功时的稳定 AttemptId。</summary>
public sealed record ExecutionCommit(ExecutionCommitOutcome Outcome, string? AttemptId);

/// <summary>无 Attempt ID 的未决发现结果。</summary>
public sealed record PendingExecution(
    string AttemptId, string EffectRef, ExecutionAttemptStatus Status);

/// <summary>迟到反馈（关联不明确时由执行源保留待关联）。</summary>
public sealed record ExecutionFeedback(string FeedbackId, string Note);

/// <summary>Receipt 的只读投影（journal 序列化形态；非 EffectReceipt 重铸）。</summary>
public sealed record ExecutionReceiptView(
    string ReceiptId, string Outcome, string Report, string CompletedAt, string? Reason);

/// <summary>单条追加历史。</summary>
public sealed record ExecutionJournalEntry(
    ExecutionEntryKind Kind,
    int Sequence,
    string Note,
    ExecutionReceiptView? Receipt = null,
    ExecutionFeedback? Feedback = null);

/// <summary>GetAttempt 还原面：准备集合 + 状态 + append-only 历史。</summary>
public sealed record ExecutionAttemptView(
    ExecutionRegistration Registration,
    ExecutionAttemptStatus Status,
    IReadOnlyList<ExecutionJournalEntry> Entries);

/// <summary>LinkCompensation 产物：新 Effect + 新 Attempt。</summary>
public sealed record ExecutionLink(string NewAttemptId, string NewEffectRef);

/// <summary>
/// 可靠执行源（CORE-012 Q4 批准的最小行为面；名称为实现轮命名，不是
/// 冻结公共 API 词汇）。五条冻结边界：只存 correlation 与不可变记录；
/// append-only；不重铸 CanonicalBinding / 不重判 admissibility；与
/// IRunTrace 零依赖零供给；Discovery 只读，重试/补偿/重观察决策归
/// Control/Agent 流。
/// 实现约定：本接口的写入方法在单线程 delivery 路径上调用（无并发契约）。
/// </summary>
public interface IReliableExecutionSource
{
    /// <summary>
    /// 提交完整准备集合，返回三值结果。Success 必须表示记录已到达实现
    /// 声明故障范围内可恢复读取的提交边界（CORE-012 Q3 限定）。
    /// </summary>
    ExecutionCommit CommitPrepare(ExecutionRegistration registration);

    /// <summary>不依赖 Attempt ID 枚举未决记录（声明范围 = 本执行源实例）。</summary>
    IReadOnlyList<PendingExecution> DiscoverPending();

    /// <summary>按稳定关联还原准备集合与 append-only 历史（只读投影）。</summary>
    ExecutionAttemptView? GetAttempt(string attemptId);

    /// <summary>追加实际提交过程（driver 已被调用的留痕），不覆盖准备记录。</summary>
    void AppendSubmission(string attemptId, string note);

    /// <summary>
    /// 追加 Receipt，或以 null 显式登记「无 Receipt 未决」。未确认结果
    /// 保持未决，不伪造失败。
    /// </summary>
    void AppendReceipt(string attemptId, EffectReceipt? receipt);

    /// <summary>迟到反馈追加到原 Attempt；关联不明确（null/未知 id）→ 保留待关联。</summary>
    void AppendLateFeedback(string? attemptId, ExecutionFeedback feedback);

    /// <summary>同一逻辑 Effect 的外部重试 = 新 Attempt（保留 RetryOf 关联）。</summary>
    string LinkRetry(string attemptId);

    /// <summary>补偿 = 新 Effect + 新 Attempt（Compensates 关联被补偿 Effect）。</summary>
    ExecutionLink LinkCompensation(string attemptId);
}
