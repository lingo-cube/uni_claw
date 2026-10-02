namespace UniClaw.Kernel.Runtime;

/// <summary>
/// RFS-001 / P25：Kernel internal driver 只在语义 decision boundary 请求
/// UniAgent。Phase 1 最小词汇：单一 InitialPlanning 边界（initial observation
/// 之后、首个现实 Effect 之前）。词汇封闭 = tracer 假设，非冻结协议。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public enum AgentDecisionPhase
{
    /// <summary>初始观察完成后的全局方案边界。</summary>
    InitialPlanning,

    /// <summary>一支提案全部步验证完（=协议 T2 ProposalExhausted，词表映射）。</summary>
    StepVerified,

    /// <summary>提案中途被接地/门拒绝。</summary>
    StepRejected,

    /// <summary>提案中途验证失败。</summary>
    VerificationFailed,

    /// <summary>Defer 等待预算耗尽（最后再问一次）。</summary>
    DeferRoundsExhausted,

    /// <summary>
    /// RUN-005 §7（Owner 决策 1）：policy 级 invalidation——bounds/no-match/
    /// match-unknown/guard-violated/guard-unknown/lease-invalidated/
    /// termination-unprovable/control-non-act 的统一 typed cause（原文经
    /// FailureReason 携带，M2 不净化）。NeedDecision 的再咨询边界（T5 谱系），
    /// 不建新状态机；invalidation 出口恒零新 Effect。
    /// </summary>
    PolicyInvalidated,
}

/// <summary>
/// P25 有界 Decision Context（最小 concrete 载荷；字段集 = TRACER_HYPOTHESIS，
/// 非 Interface 冻结）。只携带 goal-level 决策所需摘要，不下发 canonical store
/// 全量副本；CurrentWorldClaims 是 obligation 相关 subject 的只读投影。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record AgentDecisionContext(
    string DecisionId,
    string RunId,
    string ContractVersion,
    string Objective,
    IReadOnlySet<string> AllowedEffects,
    IReadOnlyDictionary<string, ClaimSummary> CurrentWorldClaims,
    IReadOnlyList<AgentObligationView> PendingObligations,
    AgentDecisionPhase Phase,
    string? FailureReason = null,
    int? FailedStepIndex = null,
    ScreenSummary? Screen = null,
    IReadOnlyList<ElementSummary>? Elements = null,
    ConsultationProgress? Progress = null,
    ConsultationBudget? BudgetRemaining = null);

/// <summary>
/// obligation 的非权威摘要视图（P7 派生投影，只读）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record AgentObligationView(
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
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record AgentActionProposal(
    string DecisionId,
    IReadOnlyList<AgentActionStep> Steps,
    string? Justification);

/// <summary>
/// proposal 中的单个有序步骤（RFS-001 D21/D22）。每步携带完整的目标表达
/// （TargetRole/TargetDescriptor/EffectClass/DesiredState），由 driver 逐串行
/// 采纳为 TargetSpec；步骤间无依赖表达（非 DAG）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record AgentActionStep(
    string TargetRole,
    string? TargetDescriptor,
    string EffectClass,
    string? DesiredState);

/// <summary>
/// 显式「无需行动」proposal（decision outcome，不是 NoOp effect）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record AgentNoActionProposal(
    string DecisionId,
    string Justification,
    CompletionEvidence? Completion = null);

/// <summary>
/// UniAgent 决策返回的封闭 union；seam 返回 null = 显式 no-response。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public abstract record AgentDecision
{
    public sealed record Act(AgentActionProposal Proposal) : AgentDecision;

    public sealed record NoAction(AgentNoActionProposal Proposal) : AgentDecision;

    /// <summary>
    /// RUN-004：再观察一轮（有界，SR-067/068）。DecisionId = 咨询关联号
    /// （终局收口裁决：每次 consultation response 都回带同号——Act/NoAction/
    /// Defer 同律 D2「防串话」；真实 UniAgent/HTTP transport 依赖该稳定关联）。
    /// </summary>
    public sealed record Defer(string DecisionId, ObserveSpec Spec) : AgentDecision;

    /// <summary>
    /// RUN-005 Slice A（FROZEN v0.3 §2）：L2 policy proposal——Agent 给规则，
    /// Kernel 机械展开（Slice B 落地展开循环）。DecisionId 回带同 V1/V6e
    /// 三态同律（D2 防串话适用于每一次 consultation response）；与 PolicyId
    /// （同 run 内 policy 唯一性，V6f）正交。DecisionId 放置在 union 成员上
    ///（Defer 先例）——FROZEN §2 sketch 未显式画出该参数，但 §9 V6e 与
    /// 基线 D2 要求其存在，此处为两条款的合并落形，非词汇扩展。
    /// </summary>
    public sealed record Policy(string DecisionId, PolicyProposal Proposal) : AgentDecision;

    /// <summary>
    /// AGT-009（§11）：advisory 抽象遍历计划——高层策略进入既有执行链的
    /// 载荷，不是第二控制器。DecisionId 同律回带（D2 防串话；Policy 先例）。
    /// Kernel 每次最多采纳一个 ActItem，且每个 ActItem 都独立走既有
    /// Grounding → Assurance → Effect Gate → dispatch → post-action
    /// verification 全链；ObserveItem 零 Effect；ControlItem 只允许
    /// Reobserve / Replan / Stop 的有界转移（Replan 消耗当前 Run 的全局
    /// 咨询预算，不重新获得新预算）。
    /// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
    /// </summary>
    public sealed record Plan(string DecisionId, AgentPlanProposal Proposal) : AgentDecision;
}

/// <summary>
/// AGT-009 — Plan 流程控制词汇（ControlItem 唯一 Kind；closed enum）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public enum PlanControlKind
{
    /// <summary>有界重新观察：废弃剩余计划，拉取一轮外部观察后回决策边界。</summary>
    Reobserve,

    /// <summary>重新规划：废弃剩余计划，回决策边界（消耗全局咨询预算）。</summary>
    Replan,

    /// <summary>安全终止：废弃剩余计划，零新 Effect 进入终局评估。</summary>
    Stop,
}

/// <summary>
/// AGT-009 — advisory 遍历计划项（closed union，§11 已定形状）。只携带
/// 语义目标与意图：结构上不存在坐标、occurrence、过期 selector 或直接
/// Effect 授权字段（「不能包含」由类型形状执法，非运行时过滤）。closed
/// union 之外的派生类型由 driver 以 plan:unknown-item fail closed 拒绝。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public abstract record PlanItem
{
    /// <summary>
    /// 单步语义动作：语义目标（角色/描述）+ effect class + 可选期望终态。
    /// 形状与 AgentActionStep 对齐（driver 逐项物化为单步，复用 Act 链）。
    /// </summary>
    public sealed record ActItem(
        string TargetRole,
        string? TargetDescriptor,
        string EffectClass,
        string? DesiredState) : PlanItem;

    /// <summary>
    /// 记录或补证（record-only）：零 Effect，也不能单独宣告任务完成
    ///（Settings record-only 结果由既有 coverage ledger 记录）。
    /// Subject 非 null = 定向补证（聚焦复查通道）。
    /// </summary>
    public sealed record ObserveItem(string? Subject) : PlanItem;

    /// <summary>流程控制：Reobserve / Replan / Stop（有界转移）。</summary>
    public sealed record ControlItem(PlanControlKind Kind, string? Reason) : PlanItem;
}

/// <summary>
/// AGT-009 — advisory 遍历计划载荷：有界有序 PlanItem 列表（非 DAG），
/// 步数上限与预算由 KernelRunDriver 机械执法（V7）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed record AgentPlanProposal(
    IReadOnlyList<PlanItem> Items,
    string? Justification);
