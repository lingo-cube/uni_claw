using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Runtime;

/// <summary>RUN-004 §1.1：元素认识论状态（M1：词汇无分数）。</summary>
public enum ElementEpistemic
{
    /// <summary>双源（视觉+XML）一致确认。</summary>
    Observed,
    /// <summary>视觉单源（XML 缺席或未连接——诚实降级）。</summary>
    Partial,
    /// <summary>多候选连接（v1 保留不产生）。</summary>
    Ambiguous,
}

/// <summary>上下文元素摘要（occurrence × XML 增强投影）。</summary>
public sealed record ElementSummary(
    string Role,
    string? Text,
    string? Bounds,
    bool? Clickable,
    bool? Checkable,
    bool? Enabled,
    ElementEpistemic Epistemic);

/// <summary>claim 摘要（值 + 判别 + 冲突标记）。</summary>
public sealed record ClaimSummary(string Value, string Disposition, bool InConflict);

/// <summary>咨询进度（no-progress 判定的输入，SR-100）。</summary>
/// <param name="PolicyState">RUN-005 §8：policy 级结局投影（_pendingPolicyOutcome
/// 数据源；null = 上一咨询后无 policy 结局——非 policy 决策路径恒 null）。</param>
public sealed record ConsultationProgress(
    int RoundsUsed,
    int StepsDispatched,
    int StepsVerified,
    PolicyProgressState? PolicyState = null);

/// <summary>
/// RUN-005 §8 — Progress.PolicyState 投影（policy 摘要）：PolicyId ·
/// ApplicationsUsed · TerminationStatus（出口时刻的终止求值三态；成功出口
/// 恒 Satisfied）。只读派生事实，非执行态权威（权威 = driver ephemeral
/// PolicyState）。
/// </summary>
public sealed record PolicyProgressState(
    string PolicyId,
    int ApplicationsUsed,
    PolicyTruth TerminationStatus);

/// <summary>剩余预算（SR-102/049）。</summary>
public sealed record ConsultationBudget(int RoundsRemaining, int StepsRemaining);

/// <summary>完成自证载荷（裁决⑧：核验后信 + 人为终极裁定）。</summary>
public sealed record CompletionEvidence(
    string Basis,
    IReadOnlyList<string> Checklist);

/// <summary>Defer 的有界观察请求（SR-067/068）。</summary>
public sealed record ObserveSpec(
    string? Subject,
    int MaxRounds);

/// <summary>当前屏摘要（协议 §2.1 Screen 字段）。</summary>
public sealed record ScreenSummary(
    string ContainerId,
    string? Signature);
