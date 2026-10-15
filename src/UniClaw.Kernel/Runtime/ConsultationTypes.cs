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
    ElementEpistemic Epistemic,
    // Semantic state is part of the advisory projection so the agent can
    // avoid a non-idempotent tap when the current checked state is already
    // satisfied. Switch values derive from the existing typed checked resolver;
    // missing/unsupported evidence stays null. Canonical authority is unchanged.
    string? State = null);

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

/// <summary>
/// PROTOCOL-FIX-003（ADR-0050 D3）— P25 续授提案（大脑侧提议的续授量）。
/// 语义：请为本 Run 追加的步数/轮次额度；规则侧校验 已授 + 提案 ≤ HardCap，
/// 拒绝不构成决定失败（留痕后按无续授继续）。
/// </summary>
public sealed record BudgetRenewalRequest(
    int AdditionalSteps,
    int AdditionalRounds,
    string? Justification = null);

/// <summary>
/// PROTOCOL-FIX-003（ADR-0050 §2）— 续授窗口投影（决策上下文携带）：
/// 窗口是否开启 + 硬顶/已授/阈值（大脑据以形成提案）。null（上下文缺省）=
/// policy 未配置续授，通道整体关闭。
/// </summary>
public sealed record RenewalWindow(
    bool Open,
    int HardCapSteps,
    int HardCapRounds,
    int GrantedSteps,
    int GrantedRounds,
    int ThresholdSteps,
    int ThresholdRounds);

/// <summary>
/// PROTOCOL-FIX-003（ADR-0050 §3 轻记账）— 续授留痕（P25 决策 id、量、
/// 依据 + 授/拒结果）。driver ephemeral 记账（不进 canonical Run State，
/// 不变量 36 精神）；只读审计投影。
/// </summary>
public sealed record BudgetRenewalRecord(
    string DecisionId,
    int AdditionalSteps,
    int AdditionalRounds,
    string? Justification,
    bool Granted,
    string? RejectionReason);

/// <summary>完成自证载荷（裁决⑧：核验后信 + 人为终极裁定）。</summary>
public sealed record CompletionEvidence(
    string Basis,
    IReadOnlyList<string> Checklist);

/// <summary>
/// AGT-019 M2（Q8 裁决）：观察深度三档——封闭枚举，越贵越晚用。
/// Normal=常规摘要观察；Focused=定向聚焦复查（Subject 靶向）；Deep=
/// 深看（局部视图全量）。v1 语义：档位入 trace/journal 留痕，观察拉取
/// 机制复用既有 Defer 链（不新建观察系统）；预算执法不变（V5 静态界
/// MaxRounds≤4 与咨询预算合同同时生效、取最严）。
/// RUN-003：公开组合缝；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public enum ObserveDepth
{
    Normal,
    Focused,
    Deep,
}

/// <summary>Defer 的有界观察请求（SR-067/068）。
/// AGT-019 M2 参数化（RUN-004 D7 ObserveSpec 形状扩展，V5 上限执法
/// 不变）：Depth 深度档（缺省 Normal = 现行为）；Purpose 目的陈述
/// （可选；提供则非空白且 ≤256——上下文经济学有界）。</summary>
public sealed record ObserveSpec(
    string? Subject,
    int MaxRounds,
    ObserveDepth Depth = ObserveDepth.Normal,
    string? Purpose = null);

/// <summary>当前屏摘要（协议 §2.1 Screen 字段）。</summary>
public sealed record ScreenSummary(
    string ContainerId,
    string? Signature);
