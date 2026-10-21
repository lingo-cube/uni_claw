using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Runtime;

/// <summary>
/// RFS-001 / P24·P26：internal run driver 可拉取的外部输入 cases。typed、
/// 一次性消费；不是 per-cycle 指令，不携带 expected owner state。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public abstract record RunDriverInput
{
    /// <summary>一轮外部观察（initial=External / post-action=PostActionEffectFlow）。</summary>
    public sealed record Observation(IReadOnlyList<ObservationProposal> Proposals) : RunDriverInput;

    /// <summary>宿主/用户 cancel（有界 lifecycle 输入；正式协议仍 Deferred ⑯）。</summary>
    public sealed record Cancel(string Reason, DateTimeOffset VirtualTime) : RunDriverInput;

    /// <summary>stimulus 消费纪律拒绝（context 失配等）——fail closed 信号。</summary>
    public sealed record Unexpected(string Reason) : RunDriverInput;

    /// <summary>
    /// Host/Owner interruption signal. It is consumed at the next safe
    /// decision boundary; it never authorizes an Effect by itself.
    /// </summary>
    public sealed record Interruption(InterruptionSignal Signal) : RunDriverInput;
}

/// <summary>Typed, owner-produced signal that requests a new consultation.</summary>
public sealed record InterruptionSignal(
    string SignalKey,
    string Kind,
    long EventSeq,
    string? Target = null,
    string? Reason = null,
    Heartbeat? Heartbeat = null,
    IReadOnlyList<ContextReferenceEntry>? ContextReferenceIndex = null,
    CasePacket? CasePacket = null);

/// <summary>
/// Kernel-to-Owner seam for the zero-Effect Skip outcome. The Kernel does not
/// reference the coverage ledger; the Host/Owner decides how to append the
/// TargetCoverageMarked fact.
/// </summary>
public sealed record TargetCoverageMarkRequest(
    string Target,
    string DecisionId,
    string Reason,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>
/// 观察深度档位（PER-009 mechanism ①，台账 #20 A 方案）。Focused = Tier 1
/// 定向复查；Deep 留给 Tier 2 change（枚举成员扩展非破坏，届时按 D6 触发）。
/// </summary>
public enum ObservationDepth
{
    /// <summary>常规周期观察（快档常开）。</summary>
    Normal,

    /// <summary>定向复查（悬案聚焦：Subjects 必非空）。</summary>
    Focused,

    /// <summary>Slow capability route hint；不携带 logical profile 或模型细节。</summary>
    Slow,
}

/// <summary>
/// 观察指令：driver 当前期望的观察方向（context + 深度 + 聚焦 subjects）。
/// PER-009 A 方案（台账 #20 用户裁决）：聚焦复查经 Kernel 驱动面传导——
/// Observation Control 权威保持在 Control Loop（baseline §14），双 Host
/// 生而同构（§24.8）。Focused 要求 Subjects 非空；Slow 只作路线提示，
/// 不把 logical profile、provider/model 或 EvidenceContext 带入该 surface。
/// </summary>
public sealed record ObservationDirective(
    ObservationContext Context,
    ObservationDepth Depth = ObservationDepth.Normal,
    IReadOnlyList<string>? Subjects = null,
    ObservationNeed? Need = null,
    TimeSpan? RemainingBudget = null);

/// <summary>
/// internal run driver 的最小 concrete 外部 seam（delegate 形态；非冻结公共
/// Interface——buyer 证据待 Phase 6）。Simulation Host 与 Product Host 各自
/// 提供 adapter：录制 replay feed / live acquisition + live UniAgent realization。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed class RunDriverInputs
{
    /// <summary>
    /// 拉取下一外部输入；参数 = 观察指令（context + 深度 + 聚焦 subjects——
    /// 串行验证屏障与消费纪律的执行面；Focused 为 PER-009 聚焦复查通道）。
    /// null = 无可用输入（合法等待）。
    /// </summary>
    public required Func<ObservationDirective, RunDriverInput?> NextInput { get; init; }

    /// <summary>Non-observation lifecycle poll, consumed only at safe driver
    /// boundaries; cannot dequeue or synthesize an observation or authorize effects.</summary>
    public Func<RunDriverInput.Cancel?>? PollCancellation { get; init; }

    /// <summary>P25 UniAgent consultation seam（唯一调用方 = internal driver）。</summary>
    public required Func<AgentDecisionContext, AgentDecision?> ConsultAgent { get; init; }

    /// <summary>Optional Owner callback for AgentDecision.Skip.</summary>
    public Action<TargetCoverageMarkRequest>? MarkTargetCoverage { get; set; }

    /// <summary>P25 durable seam; null uses an in-process journal for legacy/test composition.</summary>
    public IConsultationJournal? ConsultationJournal { get; init; }

    /// <summary>Owner-produced read-only progress facts for the shared execution overview.</summary>
    public Func<ExecutionOverviewProjection?>? ProjectExecutionOverview { get; init; }

    /// <summary>Optional task-evidence barrier. A non-null reason withholds
    /// completion proof; it can never grant completion or authorize effects.</summary>
    public Func<string?>? CheckTaskEvidenceBarrier { get; init; }

    /// <summary>Owner's read-only current candidate projection for a named plan source.</summary>
    public Func<string, PlanCandidateSet?>? ProjectPlanCandidates { get; init; }

    /// <summary>Names and descriptions of candidate sources available to the Agent.</summary>
    public IReadOnlyDictionary<string, string>? AvailablePlanSources { get; init; }

    /// <summary>Read-only current owner condition, optionally evaluated for the current semantic candidate.</summary>
    public Func<string, PlanCandidate?, PlanConditionResult?>? EvaluatePlanCondition { get; init; }

    /// <summary>Declared condition names and descriptions available to the Agent.</summary>
    public IReadOnlyDictionary<string, string>? AvailablePlanConditions { get; init; }

    /// <summary>World-derived, current-revision traversal reference view.</summary>
    public Func<string, TraversalObservation?>? ProjectTraversal { get; init; }

    /// <summary>Task binding validation for the existing typed inspection protocol.</summary>
    public Func<PlanLanguageHandler, bool>? CanBindPlanLanguage { get; init; }

    /// <summary>Explicit cross-domain typed inspection adapter; no registry dispatch.</summary>
    public Func<PlanLanguageHandler, PlanHandlerInput, Finding?>? InspectPlanLanguage { get; init; }

    /// <summary>Asynchronous typed adapter for the same existing inspection protocol.</summary>
    public Func<PlanLanguageHandler, PlanHandlerInput, CancellationToken, ValueTask<Finding?>>? InspectPlanLanguageAsync { get; init; }

    /// <summary>Maximum wait for one read-only typed inspection. Late results
    /// have no Owner write authority and are not accepted after this deadline.</summary>
    public TimeSpan PlanHandlerTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Control subquota; never replenishes Effect, consultation or plan budgets.</summary>
    public TimeSpan ObservationRecoveryTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public int MaxObservationRecoveryRequests { get; init; } = 4;

    /// <summary>Control Loop Recovery receives execution facts; strategy remains there.</summary>
    public Action<TraversalRecoveryFacts>? ObserveTraversalRecovery { get; init; }

    /// <summary>Per-step current-fact guard. This constrains execution and grants no Effect authority.</summary>
    public Func<AgentActionStep, AgentDecisionContext, PlanStepGuardResult>? GuardStep { get; init; }
}
