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
public sealed record ConsultationProgress(
    int RoundsUsed,
    int StepsDispatched,
    int StepsVerified);

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
