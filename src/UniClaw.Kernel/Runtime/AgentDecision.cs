namespace UniClaw.Kernel.Runtime;

/// <summary>
/// RFS-001 / P25：Kernel internal driver 只在语义 decision boundary 请求
/// UniAgent。Phase 1 最小词汇：单一 InitialPlanning 边界（initial observation
/// 之后、首个现实 Effect 之前）。词汇封闭 = tracer 假设，非冻结协议。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal enum AgentDecisionPhase
{
    /// <summary>初始观察完成后的全局方案边界。</summary>
    InitialPlanning,
}

/// <summary>
/// P25 有界 Decision Context（最小 concrete 载荷；字段集 = TRACER_HYPOTHESIS，
/// 非 Interface 冻结）。只携带 goal-level 决策所需摘要，不下发 canonical store
/// 全量副本；CurrentWorldClaims 是 obligation 相关 subject 的只读投影。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal sealed record AgentDecisionContext(
    string DecisionId,
    string RunId,
    string ContractVersion,
    string Objective,
    IReadOnlySet<string> AllowedEffects,
    IReadOnlyDictionary<string, string> CurrentWorldClaims,
    IReadOnlyList<AgentObligationView> PendingObligations,
    AgentDecisionPhase Phase);

/// <summary>
/// obligation 的非权威摘要视图（P7 派生投影，只读）。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal sealed record AgentObligationView(
    string ObligationId,
    string Kind,
    string Subject,
    string RequiredValue,
    bool Mandatory);

/// <summary>
/// P25 advisory proposal：goal/plan 级建议。不是 effect command，不含坐标 /
/// occurrence 引用（Kernel 仍须 fresh Grounding），不写任何 L2 owner state
/// （baseline §24.2）。有界有序 steps——非 DAG、非 Decision Package（Package
/// 仍 Phase 6）；每步独立完整链（每步各自 grounding → binding → assurance →
/// gate → dispatch，且串行受不变量 43 屏障约束）。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal sealed record AgentActionProposal(
    string DecisionId,
    IReadOnlyList<AgentActionStep> Steps,
    string? Justification);

/// <summary>
/// proposal 中的单个有序步骤（RFS-001 D21/D22）。每步携带完整的目标表达
/// （TargetRole/TargetDescriptor/EffectClass/DesiredState），由 driver 逐串行
/// 采纳为 TargetSpec；步骤间无依赖表达（非 DAG）。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal sealed record AgentActionStep(
    string TargetRole,
    string? TargetDescriptor,
    string EffectClass,
    string? DesiredState);

/// <summary>
/// 显式「无需行动」proposal（decision outcome，不是 NoOp effect）。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal sealed record AgentNoActionProposal(
    string DecisionId,
    string Justification);

/// <summary>
/// UniAgent 决策返回的封闭 union；seam 返回 null = 显式 no-response。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal abstract record AgentDecision
{
    public sealed record Act(AgentActionProposal Proposal) : AgentDecision;

    public sealed record NoAction(AgentNoActionProposal Proposal) : AgentDecision;
}
