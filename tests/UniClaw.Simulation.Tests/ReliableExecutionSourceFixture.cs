using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// 可靠提交结果三态（CORE-010 仿真契约 §4 PrepareCommit 语义）。
/// </summary>
public enum ReliableCommitOutcome
{
    Success,
    Failure,
    Unknown,
}

/// <summary>
/// 确定性 crash cut point（CORE-010 计划 §5 / 仿真契约 §5）。
/// None = 不注入崩溃（完整序列）。
/// </summary>
public enum CrashCutPoint
{
    None,

    /// <summary>准备尚未可靠提交：恢复后不得出现可发送 Attempt。</summary>
    BeforePrepareCommit,

    /// <summary>准备已提交、尚未调用 driver：恢复后必须发现未决 Attempt。</summary>
    AfterPrepareCommitBeforeDriver,

    /// <summary>driver 已调用、Receipt 尚未追加：原 Attempt 保持未知。</summary>
    AfterDriverBeforeReceipt,

    /// <summary>Receipt 已追加并关联：重启后不得重复创建同一外部 Attempt。</summary>
    AfterReceiptAppend,
}

/// <summary>
/// Attempt 在可靠执行源中的状态。pending 判定（FindPending 语义）=
/// 已越过 commit-success 但无确认 Receipt：CommittedPending /
/// CommitUnknown / Dispatched / UnknownOutcome。Registered（未提交）与
/// CommitFailed、Completed（含确定性失败回执）不是未决。
/// </summary>
public enum ReliableAttemptStatus
{
    /// <summary>提交成功，尚未发送。</summary>
    CommittedPending,

    /// <summary>提交结果未知（记录已落，需协调）。</summary>
    CommitUnknown,

    /// <summary>提交确定性失败（不可发送）。</summary>
    CommitFailed,

    /// <summary>driver 已调用，尚无 Receipt（未知语义）。</summary>
    Dispatched,

    /// <summary>显式无 Receipt / Receipt 未确认：保持未决，不伪造失败。</summary>
    UnknownOutcome,

    /// <summary>已追加确定性 Receipt（完成或确定性失败，Attempt 已决）。</summary>
    Completed,
}

/// <summary>
/// 准备集合（仿真契约 §4 GetAttempt 还原面）：Effect 引用、Attempt 标识、
/// 固定请求（CanonicalBinding：target/effectClass/revision）、固定依据
/// （RevisionId）、准入依据（AssuranceJudgment）与执行端标识。全部为
/// 不可变值记录——fixture 跨 Host 存活不依赖任何 Host 内存组合。
/// </summary>
public sealed record ExecutionPreparation(
    string RunId,
    string AttemptId,
    string EffectRef,
    string ExecutorId,
    CanonicalBinding Binding,
    AssuranceJudgment Judgment,
    string? RetryOf = null,
    string? Compensates = null);

/// <summary>迟到反馈（关联不明确时由 fixture 保留待关联）。</summary>
public sealed record AttemptFeedback(string FeedbackId, string Note);

/// <summary>追加条目类型（append-only 历史）。</summary>
public enum AttemptEntryKind
{
    Submission,
    Receipt,
    LateFeedback,
}

/// <summary>单条追加历史（不覆盖任何既有记录）。</summary>
public sealed record AttemptEntry(
    AttemptEntryKind Kind,
    int Sequence,
    string Note,
    EffectReceipt? Receipt = null,
    AttemptFeedback? Feedback = null);

/// <summary>未决发现结果（不需要 Attempt ID）。</summary>
public sealed record PendingDiscovery(
    string RunId,
    string AttemptId,
    string EffectRef,
    ReliableAttemptStatus Status);

/// <summary>GetAttempt 还原面：准备集合 + 当前状态 + append-only 历史。</summary>
public sealed record RestoredAttempt(
    ExecutionPreparation Preparation,
    ReliableAttemptStatus Status,
    IReadOnlyList<AttemptEntry> Entries);

/// <summary>LinkCompensation 产物：新 Effect + 新 Attempt。</summary>
public sealed record CompensationLink(string NewAttemptId, string NewEffectRef);

/// <summary>
/// Guarded dispatch 序列观察面：停在哪个 cut point、提交结果、 grounding
/// 留痕（Binding/Judgment/Gate/Receipt）与拒绝原因。
/// </summary>
public sealed record GuardedDispatchResult(
    CrashCutPoint StoppedAt,
    bool Crashed,
    ReliableCommitOutcome? Commit,
    string? AttemptId,
    string? RefusalReason,
    BindingDecision? Binding = null,
    AssuranceJudgment? Judgment = null,
    GateDecision? Gate = null,
    EffectReceipt? Receipt = null);

/// <summary>
/// CORE-011 — 测试侧 ReliableExecutionSourceFixture（仿真契约 §4 行为
/// 契约的受控实现；授权边界内，永不进入产品程序集）。
///
/// 仿真「未来 Runtime 在 EffectBoundary.Dispatch gate 通过、driver 调用
/// 之前接入的可靠执行源」：同一 fixture 实例由 SimulationHost.Compose
/// 交给 Host A 与 Host B（契约 §3），crash = 在命名 cut point 停机并丢弃
/// Host A 内存组合后，恢复发现只读本 fixture。fixture 与 Trace、
/// AsyncPerceptionTracer、摘要及 Host 内存对象相互独立（契约 §2）。
///
/// 本类型是验收工具，不是产品事实源；其名称不是已批准公共接口。
/// </summary>
public sealed class ReliableExecutionSourceFixture
{
    private sealed class AttemptState(
        ExecutionPreparation preparation, ReliableAttemptStatus status, List<AttemptEntry> entries)
    {
        public ExecutionPreparation Preparation { get; } = preparation;
        public ReliableAttemptStatus Status { get; set; } = status;
        public List<AttemptEntry> Entries { get; } = entries;
    }

    private readonly Dictionary<string, AttemptState> _attempts = new(StringComparer.Ordinal);
    private readonly List<AttemptFeedback> _unassociated = new();
    private int _sequence;
    private int _retryCounter;
    private int _compensationCounter;

    /// <summary>
    /// 测试对下一次 PrepareCommit / RetryCommit 的确定性控制（默认 Success）。
    /// 持续有效直到再次设置——S1/S2/S3 的受控注入点。
    /// </summary>
    public ReliableCommitOutcome NextCommitOutcome { get; set; } = ReliableCommitOutcome.Success;

    /// <summary>当前持久 Attempt 数（「不自动新建」断言面）。</summary>
    public int AttemptCount => _attempts.Count;

    /// <summary>关联不明确的迟到反馈（保留待关联，不猜测归属）。</summary>
    public IReadOnlyList<AttemptFeedback> UnassociatedFeedback => _unassociated;

    /// <summary>
    /// PrepareCommit（契约 §4）：提交完整准备集合，返回 success / failure /
    /// unknown（由 <see cref="NextCommitOutcome"/> 确定性决定）。Success →
    /// CommittedPending（未决）；Failure → CommitFailed（不可发送）；
    /// Unknown → CommitUnknown（未决，需协调）。重复 AttemptId → fail closed。
    /// </summary>
    public ReliableCommitOutcome PrepareCommit(ExecutionPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (_attempts.ContainsKey(preparation.AttemptId))
            throw new InvalidOperationException($"duplicate attempt id: {preparation.AttemptId}");

        var outcome = NextCommitOutcome;
        var status = outcome switch
        {
            ReliableCommitOutcome.Success => ReliableAttemptStatus.CommittedPending,
            ReliableCommitOutcome.Failure => ReliableAttemptStatus.CommitFailed,
            ReliableCommitOutcome.Unknown => ReliableAttemptStatus.CommitUnknown,
            _ => throw new ArgumentOutOfRangeException(),
        };
        _attempts[preparation.AttemptId] = new AttemptState(preparation, status, new List<AttemptEntry>());
        return outcome;
    }

    /// <summary>
    /// 本地提交重试（S9）：仅对 CommitUnknown 的原 Attempt 重试提交——
    /// 保持原 AttemptId，不新建 Attempt、不增加外部投递。
    /// </summary>
    public ReliableCommitOutcome RetryCommit(string attemptId)
    {
        var attempt = Require(attemptId);
        if (attempt.Status != ReliableAttemptStatus.CommitUnknown)
            throw new InvalidOperationException(
                $"local commit retry 只适用于 CommitUnknown，当前 {attempt.Status}");

        var outcome = NextCommitOutcome;
        attempt.Status = outcome switch
        {
            ReliableCommitOutcome.Success => ReliableAttemptStatus.CommittedPending,
            ReliableCommitOutcome.Failure => ReliableAttemptStatus.CommitFailed,
            ReliableCommitOutcome.Unknown => ReliableAttemptStatus.CommitUnknown,
            _ => throw new ArgumentOutOfRangeException(),
        };
        return outcome;
    }

    /// <summary>
    /// FindPending（契约 §4）：不依赖 Attempt ID，按声明范围（RunId；
    /// null = 全部）枚举未决记录。未决 = 已越过 commit-success 但无确认
    /// Receipt；Registered（从未提交）不存在于本 fixture，CommitFailed 与
    /// 已确认 Completed 不可发现——这是 S4「无可发送」与 S7「不重复投递」
    /// 的结构保证。
    /// </summary>
    public IReadOnlyList<PendingDiscovery> FindPending(string? runIdScope = null)
    {
        IEnumerable<AttemptState> selection = _attempts.Values;
        if (runIdScope is not null)
            selection = selection.Where(a => a.Preparation.RunId == runIdScope);
        return selection
            .Where(a => a.Status is ReliableAttemptStatus.CommittedPending
                or ReliableAttemptStatus.CommitUnknown
                or ReliableAttemptStatus.Dispatched
                or ReliableAttemptStatus.UnknownOutcome)
            .Select(a => new PendingDiscovery(
                a.Preparation.RunId, a.Preparation.AttemptId, a.Preparation.EffectRef, a.Status))
            .ToList();
    }

    /// <summary>GetAttempt（契约 §4）：按稳定关联还原完整准备集合与历史。</summary>
    public RestoredAttempt? GetAttempt(string attemptId) =>
        _attempts.TryGetValue(attemptId, out var attempt)
            ? new RestoredAttempt(attempt.Preparation, attempt.Status, attempt.Entries.ToList())
            : null;

    /// <summary>
    /// AppendSubmission（契约 §4）：追加实际提交过程，不覆盖准备记录。
    /// submission = driver 已被调用的留痕 → 状态迁移到 Dispatched
    /// （未知语义，待 Receipt 确认）。
    /// </summary>
    public void AppendSubmission(string attemptId, string note)
    {
        var attempt = Require(attemptId);
        attempt.Status = ReliableAttemptStatus.Dispatched;
        attempt.Entries.Add(new AttemptEntry(
            AttemptEntryKind.Submission, ++_sequence, note));
    }

    /// <summary>
    /// AppendReceipt（契约 §4）：追加 Receipt，或以 null 显式登记「无
    /// Receipt 未决」。确认完成 → Completed（脱离未决集合）；未确认
    /// （UnknownOutcome receipt 或 null）→ UnknownOutcome（保持未决，
    /// 不伪造失败）。
    /// </summary>
    public void AppendReceipt(string attemptId, EffectReceipt? receipt)
    {
        var attempt = Require(attemptId);
        var confirmed = receipt is not null
            && receipt.Outcome == DispatchOutcome.DeliveryCompleted;
        attempt.Status = confirmed
            ? ReliableAttemptStatus.Completed
            : ReliableAttemptStatus.UnknownOutcome;
        attempt.Entries.Add(new AttemptEntry(
            AttemptEntryKind.Receipt, ++_sequence,
            receipt?.ReceiptId ?? "no-receipt:pending", receipt));
    }

    /// <summary>
    /// AppendLateFeedback（契约 §4）：迟到反馈追加到原 Attempt，不覆盖
    /// 历史；关联不明确（null / 未知 AttemptId）→ 保留待关联。
    /// </summary>
    public void AppendLateFeedback(string? attemptId, AttemptFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        if (attemptId is not null && _attempts.TryGetValue(attemptId, out var attempt))
            attempt.Entries.Add(new AttemptEntry(
                AttemptEntryKind.LateFeedback, ++_sequence, feedback.FeedbackId, Feedback: feedback));
        else
            _unassociated.Add(feedback);
    }

    /// <summary>
    /// LinkRetry（S10）：同一逻辑 Effect 的外部重试 = 新 Attempt（新
    /// AttemptId，同 EffectRef），重试关系保留在 RetryOf。
    /// </summary>
    public string LinkRetry(string attemptId)
    {
        var original = Require(attemptId);
        var retryId = $"{attemptId}#r{++_retryCounter}";
        var preparation = original.Preparation with
        {
            AttemptId = retryId,
            RetryOf = attemptId,
            Compensates = null,
        };
        _attempts[retryId] = new AttemptState(
            preparation, ReliableAttemptStatus.CommittedPending, new List<AttemptEntry>());
        return retryId;
    }

    /// <summary>
    /// LinkCompensation（S11）：补偿 = 新 Effect + 新 Attempt，关联被补偿
    /// Effect（Compensates）；不是重试（RetryOf = null）。
    /// </summary>
    public CompensationLink LinkCompensation(string attemptId)
    {
        var original = Require(attemptId);
        var newEffectRef = $"{original.Preparation.EffectRef}::comp{++_compensationCounter}";
        var newAttemptId = $"{attemptId}#c{_compensationCounter}";
        var preparation = original.Preparation with
        {
            AttemptId = newAttemptId,
            EffectRef = newEffectRef,
            RetryOf = null,
            Compensates = original.Preparation.EffectRef,
        };
        _attempts[newAttemptId] = new AttemptState(
            preparation, ReliableAttemptStatus.CommittedPending, new List<AttemptEntry>());
        return new CompensationLink(newAttemptId, newEffectRef);
    }

    /// <summary>
    /// Guarded dispatch 序列（CORE-010 计划 §3 Runtime seam 的测试侧仿真）：
    /// 复刻 UniKernel.Act 目标序 Bind→Judge→（PrepareCommit）→gate/driver→
    /// （Append），把可靠执行源插在 gate 通过、driver 调用之前。真实投递
    /// 仍经产品 EffectBoundary.Dispatch seam（不绕过 gate、不直呼 driver）。
    ///
    /// 提交未明确 Success 时绝不越过 driver 调用边界（S2/S3）；
    /// <paramref name="crashAt"/> 命名的 cut point 处确定性停机（返回
    /// Crashed=true，序列中止——模拟进程崩溃，测试随即丢弃 Host A）。
    /// SimulationHost 为 internal 组合根，故本方法 internal（同程序集测试面）。
    /// </summary>
    internal GuardedDispatchResult DispatchGuarded(
        SimulationHost host,
        string attemptId,
        string effectRef,
        string effectClass,
        string targetSubject,
        string targetValue,
        CrashCutPoint crashAt = CrashCutPoint.None)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!ReferenceEquals(host.ExecutionSource, this))
            throw new InvalidOperationException(
                "host 未注入本 fixture（SimulationHost.Compose 需传入同一 execution source）");

        // 1) 真实 grounding：WorldModel 派生 BindingView → EffectBoundary.Bind
        var bindingView = host.WorldCore.DeriveBindingView(targetSubject);
        var intent = new ControlIntent(
            $"intent-{attemptId}", ControlIntentKind.Act, effectClass, targetSubject,
            bindingView.RevisionId);
        var candidate = new CandidateBinding(targetSubject, targetValue, bindingView.RevisionId);
        var decision = host.EffectBoundaryCore.Bind(intent, candidate, bindingView);
        if (decision.Canonical is not { } canonical)
            return new GuardedDispatchResult(
                crashAt, false, null, attemptId, $"binding-rejected:{decision.RejectionReason}", decision);

        // 2) 真实准入：RuntimeAssurance.Judge（三元组 correlation）
        var judgment = host.AssuranceCore.Judge(
            intent, canonical, host.KernelCore.RunView!,
            host.WorldCore.DeriveActionAssuranceView(targetSubject));
        if (!judgment.IsAdmissible)
            return new GuardedDispatchResult(
                crashAt, false, null, attemptId, $"judgment-rejected:{judgment.RejectionReason}",
                decision, judgment);

        // 3) 固定并发布准备集合（运行内存；崩溃于此 = 无可发送 Attempt）
        var preparation = new ExecutionPreparation(
            host.KernelCore.RunId!, attemptId, effectRef,
            host.EffectDriver.GetType().Name, canonical, judgment);
        if (crashAt == CrashCutPoint.BeforePrepareCommit)
            return new GuardedDispatchResult(
                CrashCutPoint.BeforePrepareCommit, true, null, attemptId, null, decision, judgment);

        // 4) 可靠提交：未明确 Success 不越过 driver 边界
        var commit = PrepareCommit(preparation);
        if (commit != ReliableCommitOutcome.Success)
            return new GuardedDispatchResult(
                crashAt, false, commit, attemptId, $"commit-blocked:{commit}", decision, judgment);
        if (crashAt == CrashCutPoint.AfterPrepareCommitBeforeDriver)
            return new GuardedDispatchResult(
                CrashCutPoint.AfterPrepareCommitBeforeDriver, true, commit, attemptId,
                null, decision, judgment);

        // 5) 真实投递：EffectBoundary gate + driver + Receipt（产品 seam）。
        //    gate 拒绝 = driver 未被调用：Attempt 保持 CommittedPending
        //    （已提交、未发送、未决——恢复发现形状同 cut2），不标 Dispatched。
        var (gate, receipt) = host.EffectBoundaryCore.Dispatch(canonical, judgment, bindingView);
        if (!gate.Allowed)
            return new GuardedDispatchResult(
                crashAt, false, commit, attemptId, $"gate-rejected:{gate.Reason}",
                decision, judgment, gate);
        AppendSubmission(attemptId, "driver-called:gate:allowed");
        if (crashAt == CrashCutPoint.AfterDriverBeforeReceipt)
            return new GuardedDispatchResult(
                CrashCutPoint.AfterDriverBeforeReceipt, true, commit, attemptId,
                null, decision, judgment, gate);

        // 6) Receipt 关联（追加，不覆盖）
        AppendReceipt(attemptId, receipt);
        if (crashAt == CrashCutPoint.AfterReceiptAppend)
            return new GuardedDispatchResult(
                CrashCutPoint.AfterReceiptAppend, true, commit, attemptId,
                null, decision, judgment, gate, receipt);
        return new GuardedDispatchResult(
            CrashCutPoint.None, false, commit, attemptId, null, decision, judgment, gate, receipt);
    }

    private AttemptState Require(string attemptId) =>
        _attempts.TryGetValue(attemptId, out var attempt)
            ? attempt
            : throw new InvalidOperationException($"unknown attempt id: {attemptId}");
}
