using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.World.UiRealization;

/// <summary>
/// UIW-005 — 产品 container association realization（UWM-009 §10 seam 的
/// 第一个产品实现；HOST-001 前置。2026-09-20 人裁决 b：不造临时件，
/// 直接落正式零件）。
/// 判定规则（确定性；UWM-009 §16 识别算法/阈值不冻结——本 realization
/// 取逐字节精确匹配）：
/// - <see cref="AssociationDispositionKind.Insufficient"/>：claim value
///   空/空白（无可判别内容），**或 claim subject 不是屏幕身份 subject**
///   （D4：内容类 claim 不参与容器身份判别——否则每帧内容变化都铸新
///   容器，与驱动器单根容器约束冲突）；
/// - <see cref="AssociationDispositionKind.New"/>：Previous 无 container，
///   或没有任何 container 的 signature 与当前 claim value 相等；
/// - <see cref="AssociationDispositionKind.Matched"/>：恰有一个 container
///   的 signature（owner 约定：ui.container.signature.&lt;id&gt; =
///   establishing claim 原文，WorldModel 276 行）相等；
/// - <see cref="AssociationDispositionKind.Ambiguous"/>：多个相等——
///   本 realization 自身不可达（同屏重看会走 Matched 不重铸），仅跨
///   realization 混用时可判达；Ambiguous 分支保留（词汇完整 + fail-safe
///   交 Authority gates）。
/// 权衡存证：逐字节相等 ⇒ 屏幕内容任何变化（滚动/弹窗/条目顺序）=
/// 新屏幕身份（identity 变异经 AssociationDecision 留痕可追溯）。
/// 改进路径（归一化签名 / occurrence 重叠相似度）= 同 seam 的后续
/// realization 迭代，不改本类型的语义边界。
/// Authority gates（evidence backing / contradiction-blocks-matched /
/// prior-only blocked）在 WorldModel 边界强制；本类型无 authority。
/// </summary>
public sealed class ProductAssociationStrategy : IAssociationStrategy
{
    /// <summary>
    /// D4（2026-09-20 装配期修正）：只有该 subject 的 claim 携带屏幕身份
    /// 判别信息；其余 subject（内容流、状态流）→ Insufficient。v0 realization
    /// 约定，非协议冻结。
    /// </summary>
    public const string ScreenIdentitySubject = "ui.screen";

    public AssociationProposal Propose(AssociationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Current.Claim.Subject != ScreenIdentitySubject)
            return new AssociationProposal(
                AssociationDispositionKind.Insufficient,
                MatchedContainerId: null,
                Candidates: Array.Empty<AssociationCandidate>(),
                Relations: Array.Empty<ProposedRelation>(),
                Reason: "not-screen-identity-claim");

        var value = input.Current.Claim.Value;
        if (string.IsNullOrWhiteSpace(value))
            return new AssociationProposal(
                AssociationDispositionKind.Insufficient,
                MatchedContainerId: null,
                Candidates: Array.Empty<AssociationCandidate>(),
                Relations: Array.Empty<ProposedRelation>(),
                Reason: "non-discriminative-claim");

        if (input.Previous?.Containers is not { Count: > 0 } containers)
            return New(input, "no-previous-container");

        var matched = containers
            .Select(container => (
                Container: container,
                Signature: SignatureOf(input.Previous, container.Identity.ContainerId)))
            .Where(entry => entry.Signature == value)
            .ToList();

        if (matched.Count == 1)
            return new AssociationProposal(
                AssociationDispositionKind.Matched,
                MatchedContainerId: matched[0].Container.Identity.ContainerId,
                Candidates: new[]
                {
                    new AssociationCandidate(
                        matched[0].Container.Identity.ContainerId,
                        new[] { input.Current.EvidenceId },
                        Array.Empty<string>()),
                },
                Relations: Array.Empty<ProposedRelation>(),
                Reason: "signature-exact-match");

        if (matched.Count > 1)
            return new AssociationProposal(
                AssociationDispositionKind.Ambiguous,
                MatchedContainerId: null,
                Candidates: matched
                    .Select(entry => new AssociationCandidate(
                        entry.Container.Identity.ContainerId,
                        new[] { input.Current.EvidenceId },
                        Array.Empty<string>()))
                    .ToArray(),
                Relations: Array.Empty<ProposedRelation>(),
                Reason: "multiple-signature-matches");

        return New(input, "no-signature-match");
    }

    private static string? SignatureOf(WorldBeliefRevision previous, string containerId)
        => previous.WorldState.TryGetValue(
               WorldModel.SignatureSubjectPrefix + containerId,
               out var claim)
            ? claim.Value
            : null;

    private static AssociationProposal New(AssociationInput input, string reason)
        => new(
            AssociationDispositionKind.New,
            MatchedContainerId: null,
            Candidates: new[]
            {
                new AssociationCandidate(
                    "(new)",
                    new[] { input.Current.EvidenceId },
                    Array.Empty<string>()),
            },
            Relations: Array.Empty<ProposedRelation>(),
            Reason: reason);
}
