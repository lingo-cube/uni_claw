using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.Runtime;

// ============================================================================
// RUN-005 Slice A — Policy runtime 机制面（internal：不进公开驱动面，白名单
// 零新增；ControlBeliefView 先例）。全部按 FROZEN v0.3 设计稿
// （plans/2026-09-24-run-005-l2-policy-runtime.md）落形：
//   §3   PolicyTruth 逐 primitive 推导表（PolicyEvaluation）
//   §4   semantic lease adoption 绑定规则（PolicyLease / PolicyLeaseRef）
//   §4.1 PolicyEvaluationView 独立最小投影（owner-derived contract）
//   §8   GuardCursor（warm-up ≠ PolicyTruth.Unknown）
//   §9   V6 形态校验（PolicyValidation）
// Slice B 消费本文件落地的求值/校验缝实现 PolicyExpand 运行循环。
// ============================================================================

/// <summary>
/// RUN-005 §4.1 — claim 侧最小投影（Value / Disposition / InConflict）。
/// PolicyTruth 求值只消费这三个字段；Disposition 词汇同
/// <see cref="ClaimSummary"/> 先例：conflicted ＞ revised（痕迹链非空）＞
/// established。
/// </summary>
internal sealed record PolicyClaimFact(string Value, string Disposition, bool InConflict);

/// <summary>
/// RUN-005 §4.1 — occurrence 侧最小投影（Role / Epistemic）。
/// v1 派生规则：presence ⇒ Observed（occurrence = admission-checked
/// accepted evidence 经 reconciliation 建立的 belief；「未见」以不在投影中
/// 表达）。Epistemic≠Observed 分支防御性保留：未来降级 occurrence 一律
/// Unknown（fail closed），不得偷换成 Satisfied。
/// </summary>
internal sealed record PolicyOccurrenceFact(string Role, ElementEpistemic Epistemic);

/// <summary>
/// RUN-005 §4.1（Owner 决策 2）— Policy 谓词/守卫求值的独立最小输入载体。
/// 契约（owner 原文语义）：owner-derived · read-only · ephemeral ·
/// fresh-derived（每轮即席派生）· not persisted · not recovery state ·
/// not authority。不绑定 <see cref="AgentDecisionContext"/>（避免未来
/// Agent context 裁剪影响 Policy runtime correctness）；Text 不进 v1。
/// 派生只读 World owner 的公开 belief aggregate（<see cref="WorldBeliefRevision"/>），
/// 不成为第二 truth；消费侧判定权（PolicyTruth 推导）在
/// <see cref="PolicyEvaluation"/>。
/// </summary>
internal sealed record PolicyEvaluationView(
    IReadOnlyDictionary<string, PolicyClaimFact> Claims,
    IReadOnlyList<PolicyOccurrenceFact> Occurrences)
{
    /// <summary>
    /// owner-derived 派生（每轮即席、用毕即弃）：claims 取 WorldState
    ///（conflicted ＞ revised ＞ established，同 driver DispositionOf 律），
    /// occurrences 取 revision-local Occurrences。无 belief 的 fresh 派生
    /// 由调用侧保证——本方法不接收 null world（无 belief = 无可求值输入，
    /// 属 Slice B lease/observation 前置，不在投影内兜底）。
    /// </summary>
    public static PolicyEvaluationView FromBelief(WorldBeliefRevision belief)
    {
        ArgumentNullException.ThrowIfNull(belief);
        var conflicted = belief.Conflicts
            .Select(c => c.Subject)
            .ToHashSet(StringComparer.Ordinal);
        var claims = belief.WorldState.ToDictionary(
            kv => kv.Key,
            kv => new PolicyClaimFact(
                kv.Value.Value,
                DispositionOf(kv.Key, kv.Value, conflicted),
                conflicted.Contains(kv.Key)),
            StringComparer.Ordinal);
        var occurrences = (belief.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Select(o => new PolicyOccurrenceFact(o.Role, ElementEpistemic.Observed))
            .ToArray();
        return new PolicyEvaluationView(claims, occurrences);
    }

    private static string DispositionOf(
        string subject, WorldClaim claim, IReadOnlySet<string> conflicted) =>
        conflicted.Contains(subject)
            ? "conflicted"
            : claim.SupersededEvidenceIds is not null ? "revised" : "established";
}

/// <summary>
/// RUN-005 §8 — GuardCursor（Slice B 运行态；Slice A 落类型）。历史比较
/// operand（非 current World truth）：
/// <list type="bullet">
/// <item><see cref="LastObservedValue"/>：上一份可比较样本（null = 首样本
/// 未到——warm-up）。</item>
/// <item><see cref="ConsecutiveUnchangedCount"/>：连续未变轮数（verified
/// application 后由 Slice B 更新）。</item>
/// </list>
/// 【v0.3 澄清】warm-up ≠ <see cref="PolicyTruth.Unknown"/>：第一份样本只
/// 初始化 cursor；样本不足属 warm-up（Satisfied 侧）；Unknown 只表示当前
/// 证据冲突/不足、无法合法求值。
/// </summary>
internal sealed record PolicyGuardCursor(
    string Subject,
    string? LastObservedValue,
    int ConsecutiveUnchangedCount)
{
    /// <summary>warm-up（首样本未到）——求值上映射为 Satisfied 侧，绝不映射 Unknown。</summary>
    public bool InWarmUp => LastObservedValue is null;
}

/// <summary>
/// RUN-005 §4 — semantic lease ref：Policy adoption 绑定的 active execution
/// lease 身份。v1 判据 = scope 语义：当前唯一 root container 的 canonical
/// 身份（容器身份 + 单根持存）。内容变化（滚动/可见元素变化/content
/// revision）≠ 失效——只有容器身份变化/单根不持存才失效（每轮校验归
/// Slice B；detection vocabulary / preemption / cross-process recovery
/// 维持 DEFER）。不是 canonical owner：纯派生只读引用。
/// </summary>
internal sealed record PolicyLeaseRef(string RootContainerId);

/// <summary>lease 派生结果：Lease 与 Rejection 恰一非空。</summary>
internal readonly record struct PolicyLeaseDerivation(PolicyLeaseRef? Lease, string? Rejection);

/// <summary>
/// RUN-005 §4 — adoption 所需的 lease identity 规则（机械封闭，无检测系统）：
/// <list type="bullet">
/// <item>无 belief / 零 root / 多 root → <c>no-active-lease</c>（无 active
/// execution lease → Policy reject）。</item>
/// <item>唯一 root 但 ContainerId 空白 → <c>lease-identity-invalid</c>
///（identity 不合法 → Policy reject；防御性——铸造算法不产空白 id）。</item>
/// <item>唯一 root + 合法身份 → 绑定（ref = ContainerId）。</item>
/// </list>
/// </summary>
internal static class PolicyLease
{
    /// <summary>从当前 belief 派生 active execution lease（纯函数、无副作用）。</summary>
    public static PolicyLeaseDerivation TryDerive(WorldBeliefRevision? current)
    {
        if (current?.Containers is not { Count: 1 } containers)
            return new PolicyLeaseDerivation(null, "no-active-lease");
        var containerId = containers[0].Identity.ContainerId;
        if (string.IsNullOrWhiteSpace(containerId))
            return new PolicyLeaseDerivation(null, "lease-identity-invalid");
        return new PolicyLeaseDerivation(new PolicyLeaseRef(containerId), null);
    }
}

/// <summary>
/// RUN-005 §3 — PolicyTruth 机械求值（closed typed AST 的唯一求值入口；
/// Kernel 封闭机械求值，零解释、零修复）。逐 primitive 推导表（原文语义）：
/// <list type="table">
/// <item>ClaimEquals(s,v)：存在 ∧ Value==v ∧ !InConflict → Satisfied；
/// 存在 ∧ Value≠v ∧ !InConflict → Violated；subject 缺席 / InConflict →
/// Unknown（conflicted claim 永不满足终止）。</item>
/// <item>ClaimInSet(s,vs)：存在 ∧ !InConflict ∧ Value∈vs → Satisfied；
/// 存在 ∧ !InConflict ∧ Value∉vs → Violated；同上 Unknown。</item>
/// <item>ElementExists(r)：本 revision occurrences 含 role=r ∧
/// Epistemic=Observed → Satisfied；未见 / Epistemic≠Observed → Unknown；
/// v1 永不 Violated（absence 不可证——bounds 耗尽兜底）。</item>
/// <item>ObservationUnchanged(s,n)：连续未变计数 &lt; n（含 warm-up）→
/// Satisfied；连续 n 轮不变 → Violated；本轮 subject 值 Unknown（缺席/
/// 冲突）→ Unknown。</item>
/// </list>
/// 合取语义：任一 Unknown → 整体 Unknown；任一 Violated → 整体 Violated；
/// 否则 Satisfied。未知 AST 节点 → Unknown（fail closed；V6a 已在入口
/// reject，此处为防御面）。
/// </summary>
internal static class PolicyEvaluation
{
    /// <summary>单谓词求值（§3 推导表）。</summary>
    public static PolicyTruth Evaluate(PolicyPredicate predicate, PolicyEvaluationView view)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(view);
        return predicate switch
        {
            PolicyPredicate.ClaimEquals p => EvaluateClaim(p.Subject, view) switch
            {
                { } fact => fact.Value == p.Value ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                null => PolicyTruth.Unknown,
            },
            PolicyPredicate.ClaimInSet p => EvaluateClaim(p.Subject, view) switch
            {
                { } fact => p.Values.Contains(fact.Value) ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                null => PolicyTruth.Unknown,
            },
            PolicyPredicate.ElementExists p => view.Occurrences.Any(o =>
                o.Role == p.Role && o.Epistemic == ElementEpistemic.Observed)
                ? PolicyTruth.Satisfied
                : PolicyTruth.Unknown,
            // 封闭词汇外的派生节点：无法合法求值 → Unknown（fail closed）
            _ => PolicyTruth.Unknown,
        };
    }

    /// <summary>合取求值（§3 合取语义：Unknown ＞ Violated ＞ Satisfied）。</summary>
    public static PolicyTruth EvaluateConjunction(
        IReadOnlyList<PolicyPredicate> predicates, PolicyEvaluationView view)
    {
        ArgumentNullException.ThrowIfNull(predicates);
        var anyUnknown = false;
        var anyViolated = false;
        foreach (var predicate in predicates)
        {
            switch (Evaluate(predicate, view))
            {
                case PolicyTruth.Unknown:
                    anyUnknown = true;
                    break;
                case PolicyTruth.Violated:
                    anyViolated = true;
                    break;
            }
        }
        if (anyUnknown) return PolicyTruth.Unknown;
        if (anyViolated) return PolicyTruth.Violated;
        return PolicyTruth.Satisfied;
    }

    /// <summary>单守卫求值（§3 推导表 + §8 warm-up 澄清）。</summary>
    public static PolicyTruth Evaluate(
        PolicyGuard guard, PolicyEvaluationView view, PolicyGuardCursor? cursor)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(view);
        return guard switch
        {
            PolicyGuard.ObservationUnchanged g => EvaluateObservationUnchanged(g, view, cursor),
            // 封闭词汇外的派生节点：无法合法求值 → Unknown（fail closed）
            _ => PolicyTruth.Unknown,
        };
    }

    /// <summary>
    /// 求值序：本轮 subject 值 Unknown（缺席/冲突）优先——当前证据冲突/不足
    /// 时无法合法求值；其次 warm-up（首样本前/窗口未满）→ Satisfied；
    /// 连续 n 轮不变 → Violated。
    /// </summary>
    private static PolicyTruth EvaluateObservationUnchanged(
        PolicyGuard.ObservationUnchanged guard, PolicyEvaluationView view, PolicyGuardCursor? cursor)
    {
        if (EvaluateClaim(guard.Subject, view) is null)
            return PolicyTruth.Unknown;
        // warm-up：首样本未到（cursor 缺席或未初始化）→ Satisfied，绝不 Unknown
        if (cursor is null || cursor.InWarmUp)
            return PolicyTruth.Satisfied;
        return cursor.ConsecutiveUnchangedCount >= guard.AfterRounds
            ? PolicyTruth.Violated
            : PolicyTruth.Satisfied;
    }

    /// <summary>可求值 claim fact：存在且 !InConflict；缺席或冲突 → null（= Unknown 侧）。</summary>
    private static PolicyClaimFact? EvaluateClaim(string subject, PolicyEvaluationView view) =>
        view.Claims.TryGetValue(subject, out var fact) && !fact.InConflict ? fact : null;
}

/// <summary>
/// RUN-005 §9 — V6 形态面校验（纯函数、fail closed、不修复 malformed
/// Policy）。V6e（DecisionId 回带）由 ConsultAgentV2 correlation 执法；
/// V6f（PolicyId 同 run 唯一）与 V6g（lease 可绑定）需要 run 状态，由
/// driver 组合执法（本类提供 V6f 判定规则）。返回 null = 通过；非 null =
/// 拒绝原因（M2 词汇，kebab）。
/// </summary>
internal static class PolicyValidation
{
    /// <summary>
    /// V6 形态校验（V6a-d + PolicyId 合法性 + V6c bounds）。stepsRemaining =
    /// MaxTotalSteps - _stepsDispatched（设计 §6；不得扩大合同步数预算）。
    /// </summary>
    public static string? ValidateProposal(
        PolicyProposal proposal, ExecutionContractView view, int stepsRemaining)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(view);

        // PolicyId 合法（V6f 的 operand）
        if (string.IsNullOrWhiteSpace(proposal.PolicyId))
            return "policy:invalid-policy-id";

        // V6b：Match / Termination 非空合取
        if (proposal.Match is not { Count: > 0 })
            return "policy:empty-match";
        if (proposal.Termination is not { Count: > 0 })
            return "policy:empty-termination";

        // V6a + 节点字段合法性（closed AST：未知节点 reject；已知节点 typed
        // 字段畸形同律 reject——与 V6d「TargetRole 非空」同类）
        foreach (var predicate in proposal.Match.Concat(proposal.Termination))
        {
            switch (predicate)
            {
                case PolicyPredicate.ClaimEquals p:
                    if (string.IsNullOrWhiteSpace(p.Subject) || string.IsNullOrWhiteSpace(p.Value))
                        return "policy:invalid-node";
                    break;
                case PolicyPredicate.ClaimInSet p:
                    if (string.IsNullOrWhiteSpace(p.Subject) || p.Values is not { Count: > 0 })
                        return "policy:invalid-node";
                    break;
                case PolicyPredicate.ElementExists p:
                    if (string.IsNullOrWhiteSpace(p.Role))
                        return "policy:invalid-node";
                    break;
                default:
                    return "policy:unknown-node";
            }
        }
        if (proposal.Guards is not null)
        {
            foreach (var guard in proposal.Guards)
            {
                switch (guard)
                {
                    case PolicyGuard.ObservationUnchanged g:
                        if (string.IsNullOrWhiteSpace(g.Subject) || g.AfterRounds <= 0)
                            return "policy:invalid-node";
                        break;
                    default:
                        return "policy:unknown-node";
                }
            }
        }

        // V6d：模板自带目标 + EffectClass ∈ AllowedEffects
        var template = proposal.ActionTemplate;
        if (string.IsNullOrWhiteSpace(template.TargetRole))
            return "policy:missing-target-role";
        if (string.IsNullOrWhiteSpace(template.EffectClass)
            || !view.AllowedEffects.Contains(template.EffectClass))
            return "policy:effect-class-not-allowed";

        // V6c：0 < MaxApplications ≤ StepsRemaining
        if (proposal.MaxApplications <= 0)
            return "policy:max-applications-invalid";
        if (proposal.MaxApplications > stepsRemaining)
            return "policy:max-applications-exceeds-steps";

        return null;
    }

    /// <summary>
    /// V6f 判定规则：PolicyId 同 run 内唯一。currentConsultNumber 与登记值
    /// 相同 = 同一次咨询的幂等重验（Act 校验失败先例：_adoptedDecision 保持、
    /// 重 Drive 重验）——不算重复；不同咨询携带同 id → 重复。
    /// </summary>
    public static bool IsDuplicatePolicyId(
        IReadOnlyDictionary<string, int> adoptedPolicyIds,
        string policyId,
        int currentConsultNumber) =>
        adoptedPolicyIds.TryGetValue(policyId, out var adoptedAt)
        && adoptedAt != currentConsultNumber;
}
