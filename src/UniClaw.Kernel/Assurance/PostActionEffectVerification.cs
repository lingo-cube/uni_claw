using UniClaw.Kernel.Control;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

/// <summary>
/// 一次现实 Effect 的 post-action verification judgment。它是 Assurance
/// authority 的短生命周期产物，不是 Outcome Proof，也不进入 Run/World truth。
/// AGT-005：升为 public（Host 覆盖遍历 runner 的逐步 trace 消费面——
/// 白名单经 KernelRuntimeSurfaceWhitelistTests 显式修订）。
/// </summary>
public sealed record PostActionEffectVerification(
    string RevisionId,
    TargetSpec Target,
    bool IsVerified,
    IReadOnlyList<AssuranceCheck> Checks,
    string? RejectionReason);

/// <summary>
/// StateBecomes — 状态型期望谓词（ADR-0049 D8）：subject 应处于 value 态。
/// value 域 = 目标 occurrence presentation state（经 CheckedSemantics 严格
/// 映射比较）；保留字 "present" = 可观察在位（恰一匹配 occurrence）——
/// navigation transition 合法移除目标时由
/// <c>post-action-navigation-transition</c> 证明豁免。词汇经协议变更扩展。
/// </summary>
public sealed record StateBecomes(string Subject, string Value)
{
    public static StateBecomes Create(string subject, string value)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("subject is required", nameof(subject));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("value is required", nameof(value));
        return new StateBecomes(subject, value);
    }
}

/// <summary>
/// ChangePredicate — 变化型期望谓词封闭词汇（ADR-0049 D8；通则 4：锁类别
/// 不声明 exhaustive；扩展走协议变更，本 change 不做）。
/// </summary>
public enum ChangePredicate
{
    /// <summary>可见 occurrence 内容集发生变化。</summary>
    ContentChanged,

    /// <summary>路由保持不变。</summary>
    RouteStable,
}

/// <summary>
/// ExpectedEffect — post-action 验证的显式期望载荷（ADR-0049 D8 /
/// PROTOCOL-FIX-002）：两族封闭谓词 = 判定的对照物（非身份连续）。挂
/// authoring/意图侧投影：不进 CanonicalBinding、不进 ExecutionContract、
/// 不进 P14（D1）——不是第二套 effect 权威。空谓词集（两族皆空）= 运行期
/// 拒绝（A3：验证永远有显式对照物）。
/// </summary>
public sealed record ExpectedEffect
{
    public IReadOnlyList<StateBecomes> States { get; }

    public IReadOnlyList<ChangePredicate> Changes { get; }

    public ExpectedEffect(
        IReadOnlyList<StateBecomes> states,
        IReadOnlyList<ChangePredicate> changes)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(changes);
        if (states.Count == 0 && changes.Count == 0)
            throw new ArgumentException(
                "expected effect requires at least one predicate (empty predicate set is rejected, PROTOCOL-FIX-002 A3)");
        States = states;
        Changes = changes;
    }

    /// <summary>单状态型期望的便捷构造。</summary>
    public static ExpectedEffect OfState(string subject, string value) =>
        new([StateBecomes.Create(subject, value)], []);

    /// <summary>纯变化型期望的便捷构造。</summary>
    public static ExpectedEffect OfChanges(params ChangePredicate[] predicates) =>
        new([], predicates.Length == 0
            ? throw new ArgumentException("at least one change predicate is required", nameof(predicates))
            : predicates);
}

/// <summary>
/// 变化型谓词的观测证据（internal；非期望）：pre-dispatch 与 post-action
/// 信念的机械派生事实，由 Kernel 编排侧组装（UniKernel），Assurance 只做
/// 谓词求值。期望本身在 <see cref="ExpectedEffect"/>——期望与观测分离
/// （PROTOCOL-FIX-002：滚动三布尔迁移后删除，此处仅是求值输入）。
/// </summary>
/// <param name="RouteBefore">pre-dispatch 路由 claim（null = 证据缺席）。</param>
/// <param name="RouteAfter">post-action 路由 claim（null = 证据缺席）。</param>
/// <param name="VisibleContentChanged">可见 occurrence (role, descriptor) 集
/// 合不对称证据（null = 证据缺席，不判满足，fail closed）。</param>
internal sealed record ChangeEvidence(
    string? RouteBefore,
    string? RouteAfter,
    bool? VisibleContentChanged);

/// <summary>
/// Kernel 编排给 Assurance 的最小验证输入：本轮 P2/P3 处理结果、当前 scoped
/// Slice 与显式期望载荷。Assurance 负责判断是否足以清偿下一次现实 Effect 前
/// 的验证屏障。<paramref name="Expected"/> 必填（A3：无期望载荷的验证输入 =
/// 编译失败）。PROTOCOL-FIX-002：原特设滚动三布尔（pre/post 路由对 +
/// 内容变化位）迁移为
/// <see cref="ChangePredicate.RouteStable"/> ∧
/// <see cref="ChangePredicate.ContentChanged"/> 实例后删除；其观测事实收敛
/// 进 <see cref="ChangeEvidence"/>。
/// </summary>
internal sealed record PostActionEffectVerificationInput(
    TargetSpec Target,
    Slice Slice,
    IReadOnlyList<KernelResult> ProcessedObservations,
    ExpectedEffect Expected,
    bool TargetDisappearedAfterRouteChange = false,
    ChangeEvidence? Change = null);
