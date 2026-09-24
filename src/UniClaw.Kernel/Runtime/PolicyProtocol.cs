namespace UniClaw.Kernel.Runtime;

/// <summary>
/// RUN-005 §3 — 一切 Policy 谓词/守卫的机械求值格（closed typed AST 的唯一
/// 求值词汇）。三态语义（M1 认识论词汇落地）：
/// <list type="bullet">
/// <item><see cref="Satisfied"/>：证据在场、无冲突、命题成立。</item>
/// <item><see cref="Violated"/>：证据在场、无冲突、命题不成立。</item>
/// <item><see cref="Unknown"/>：证据缺席或冲突（SR-022 族：conflicted claim
/// 永不满足终止）。Unknown 一律 fail closed 回 decision boundary，<b>不得</b>
/// 被当成 false/Satisfied 偷偷降级。</item>
/// </list>
/// Guard warm-up ≠ Unknown（§8 v0.3 澄清：首样本/窗口未满属 Satisfied 侧）。
/// </summary>
public enum PolicyTruth
{
    /// <summary>证据在场且命题成立。</summary>
    Satisfied,

    /// <summary>证据在场且命题不成立。</summary>
    Violated,

    /// <summary>证据缺席/冲突，无法合法求值——fail closed。</summary>
    Unknown,
}

/// <summary>
/// RUN-005 §2 — L2 advisory policy proposal（FROZEN v0.3）。bounded
/// contingent advisory decision package：Agent 给规则，Kernel 机械展开；
/// ≠ workflow/program/script/effect batch/driver macro。v0.2 裁决：无
/// PolicyScope、无 PolicyFallback、无 MaxRounds——scope 语义 = semantic
/// lease（§4），一切 invalidation 唯一出口 = 零新 effect 回 decision
/// boundary（§7）。
/// </summary>
/// <param name="PolicyId">同 run 内唯一（V6f）。</param>
/// <param name="Match">合取门控（非空，V6b）；Satisfied 才物化单步。</param>
/// <param name="ActionTemplate">自带语义目标——Kernel 零猜测，「从 match
/// 猜 target」的代码路径结构性不存在（§12）。</param>
/// <param name="Termination">合取（非空，V6b）；Satisfied → policy 成功出口。</param>
/// <param name="Guards">可空；逐个 tri-state 求值，Violated/Unknown →
/// invalidation（§5 步骤 4）。</param>
/// <param name="MaxApplications">唯一局部预算（V6c：0 &lt; MaxApplications
/// ≤ StepsRemaining；不得扩大合同步数预算）。</param>
public sealed record PolicyProposal(
    string PolicyId,
    IReadOnlyList<PolicyPredicate> Match,
    PolicyActionTemplate ActionTemplate,
    IReadOnlyList<PolicyPredicate> Termination,
    IReadOnlyList<PolicyGuard> Guards,
    int MaxApplications,
    string? Justification);

/// <summary>
/// RUN-005 §2（v0.3.1 修订）— closed typed predicate AST。v1 词汇冻结为两员
///（ClaimEquals/ClaimInSet）；禁止 arbitrary expression / script / free-form
/// predicate / general DSL / dynamic code。求值一律 <see cref="PolicyTruth"/>
/// 三态（§3 推导表）。派生类型不在封闭词汇内 → V6a reject（fail closed）。
/// 【v0.3.1 删除 ElementExists】原「看见 → Satisfied / 没看见 → Unknown」与
/// Termination Unknown → invalidation 的出口语义矛盾（无法表达「继续执行，
/// 直到目标元素出现」——首轮未出现即退出）；「元素没出现」只有在
/// observation coverage 足够时才能合法判定为 false，而 v1 无 coverage/
/// completeness semantics。不为绕过矛盾把 not-observed 改判 Violated——
/// 整员删除，DEFER（buyer = coverage-aware traversal / termination）。
/// </summary>
public abstract record PolicyPredicate
{
    /// <summary>claim 存在且 Value == <paramref name="Value"/> 且 !InConflict → Satisfied；claim 存在且异值且 !InConflict → Violated；subject 缺席或 InConflict → Unknown。</summary>
    public sealed record ClaimEquals(string Subject, string Value) : PolicyPredicate;

    /// <summary>
    /// 有界方向域（F8(o)/F3(o)）：claim 值 ∈ <paramref name="Values"/>（如
    /// temp ∈ {24,23,22,21}——19/25 出集即停）。存在、!InConflict、Value∈Values
    /// → Satisfied；存在、!InConflict、Value∉Values → Violated；缺席/冲突 →
    /// Unknown。v1 不加 GreaterThan/LessThan/数值 range DSL（buyer 未到）。
    /// </summary>
    public sealed record ClaimInSet(string Subject, IReadOnlyList<string> Values) : PolicyPredicate;
}

/// <summary>
/// RUN-005 §2 — 自带语义目标的动作模板（= <see cref="AgentActionStep"/>
/// 同形，F2(o)）。TargetRole 是 typed target 信息的必填位：物化单步 =
/// AgentActionStep(TargetRole, …, EffectClass, DesiredState)，目标来自
/// 模板，Kernel 不得从 Match 猜 target。
/// </summary>
public sealed record PolicyActionTemplate(
    string TargetRole,
    string? TargetDescriptor,
    string EffectClass,
    string? DesiredState);

/// <summary>
/// RUN-005 §2 — closed typed guard（kind 封闭 + typed inputs + tri-state
/// 求值）。v1 单守卫词汇：ObservationUnchanged。Guard warm-up ≠
/// <see cref="PolicyTruth.Unknown"/>（§8）。
/// </summary>
public abstract record PolicyGuard
{
    /// <summary>
    /// 「连续 n 轮 subject 值不变则触发」。Satisfied = 连续未变计数 &lt; n
    ///（含 warm-up：首样本前/窗口未满）；Violated = 连续 n 轮值不变；
    /// Unknown = 本轮 subject 值 Unknown（缺席/冲突）。历史比较 operand =
    /// GuardCursor（§8），非 current World truth。
    /// </summary>
    public sealed record ObservationUnchanged(string Subject, int AfterRounds) : PolicyGuard;
}

/// <summary>
/// RUN-005 §5/§7 — PolicyInvalidated 的 typed cause 词汇（Owner 决策 1：
/// 统一承载一切 policy 级 invalidation，只是 NeedDecision 的 typed cause，
/// 不建新状态机；M2 原文不净化）。token（协议 reason 字符串形态）：
/// bounds-exhausted / no-match / match-unknown / guard-violated /
/// guard-unknown / lease-invalidated / termination-unprovable /
/// control-non-act。Slice B 消费；Slice A 先落词汇。
/// </summary>
public enum PolicyInvalidationReason
{
    /// <summary>ApplicationsUsed ≥ MaxApplications（bounds-exhausted）。</summary>
    BoundsExhausted,

    /// <summary>Match 合取 Violated（no-match）。</summary>
    NoMatch,

    /// <summary>Match 合取 Unknown（match-unknown）。</summary>
    MatchUnknown,

    /// <summary>某 Guard Violated（guard-violated）。</summary>
    GuardViolated,

    /// <summary>某 Guard Unknown（guard-unknown；无 Fallback 分支——同回 decision boundary）。</summary>
    GuardUnknown,

    /// <summary>semantic lease 失效（lease-invalidated；§4：容器身份变化/单根不持存——content 变化 ≠ 失效）。</summary>
    LeaseInvalidated,

    /// <summary>Termination 合取 Unknown（termination-unprovable）。</summary>
    TerminationUnprovable,

    /// <summary>SelectIntent 返回 plain-Observe 且 Termination 未满足（control-non-act，F7(b)：消灭静默楔死）。</summary>
    ControlNonAct,
}
