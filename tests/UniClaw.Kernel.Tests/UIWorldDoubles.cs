using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// UIW-001 测试 doubles —— 确定性 AssociationStrategy 与观察构造器。
/// Strategy 是 UIWorld 内部 seam（UWM-009 §11/§30）：double 只从
/// (previous revision, current evidence, P22 prior) 输入推导，零隐藏状态。
/// </summary>
internal static class UIWorldDoubles
{
    public static readonly DateTimeOffset T0 = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset T1 = new(2026, 9, 7, 10, 5, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset T2 = new(2026, 9, 7, 10, 10, 0, TimeSpan.Zero);

    /// <summary>container 观察约定 subject（测试域约定，非协议）。</summary>
    public const string Observed = "ui.container.observed";

    public static ObservationProposal Observation(string value, DateTimeOffset t) =>
        new(new ObservationClaim(Observed, value), IngressKind.Observation, ObservationContext.External,
            new Provenance("provider.scripts", t, "scope:ui.container", new[] { "raw://capture", "encode:v1" }));

    public static ObservationProposal SubjectObservation(string subject, string value, DateTimeOffset t) =>
        new(new ObservationClaim(subject, value), IngressKind.Observation, ObservationContext.External,
            new Provenance("provider.scripts", t, $"scope:{subject}", new[] { "raw://capture", "encode:v1" }));

    public static UniKernel NewKernel(IAssociationStrategy strategy, params string[] extraScope)
    {
        var scope = new HashSet<string> { Observed };
        foreach (var s in extraScope) scope.Add(s);
        return new UniKernel(new EvidenceLedger(), new WorldModel(scope, strategy));
    }

    /// <summary>脚本化 New proposal（support 指向当前真实 EvidenceId）。</summary>
    public static Func<AssociationInput, AssociationProposal> ScriptedNew(string reason) => i => new AssociationProposal(
        AssociationDispositionKind.New, MatchedContainerId: null,
        new[] { new AssociationCandidate("(new)", new[] { i.Current.EvidenceId }, Array.Empty<string>()) },
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);

    /// <summary>脚本化 Insufficient proposal。</summary>
    public static Func<AssociationInput, AssociationProposal> ScriptedInsufficient(string reason) => _ => new AssociationProposal(
        AssociationDispositionKind.Insufficient, MatchedContainerId: null,
        Candidates: Array.Empty<AssociationCandidate>(),
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);
}

/// <summary>
/// 正路径 double：从 previous revision 的 signature claims + 当前 claim + P22
/// prior 确定性推导。前缀语义（测试域约定）：glimpse: = 判别信息不足；
/// ambiguous: = 多解释；contradicts: = 语义反证；其余 value = signature 精确匹配。
/// prior 只影响 plausibility（navigate→New 更可信；scroll 下未见 signature →
/// Insufficient），signature 匹配始终由 evidence 决定 Matched（prior ≠ truth）。
/// </summary>
internal sealed class SignatureAssociationStrategy : IAssociationStrategy
{
    public AssociationProposal Propose(AssociationInput input)
    {
        var sig = input.Current.Claim.Value;
        var evId = input.Current.EvidenceId;

        if (input.Previous is null)
            return NewProposal(evId, "first-observation");

        if (sig.StartsWith("glimpse:", StringComparison.Ordinal))
            return InsufficientProposal("undiscriminating-observation");

        if (sig.StartsWith("contradicts:", StringComparison.Ordinal))
            return InsufficientProposal("semantic-contradiction");

        if (sig.StartsWith("ambiguous:", StringComparison.Ordinal) && input.Previous.Containers.Count >= 2)
        {
            var ids = input.Previous.Containers.Select(c => c.Identity.ContainerId).Take(2).ToList();
            return new AssociationProposal(
                AssociationDispositionKind.Ambiguous, MatchedContainerId: null,
                ids.Select(id => new AssociationCandidate(id, new[] { evId }, Array.Empty<string>())).ToArray(),
                Relations: Array.Empty<ProposedRelation>(), Reason: "multiple-plausible-identities");
        }

        var matches = input.Previous.Containers
            .Where(c => input.Previous.WorldState.TryGetValue(
                WorldModel.SignatureSubjectPrefix + c.Identity.ContainerId, out var claim)
                && claim.Value == sig)
            .Select(c => c.Identity.ContainerId)
            .ToList();

        if (matches.Count == 1)
            return new AssociationProposal(
                AssociationDispositionKind.Matched, matches[0],
                new[] { new AssociationCandidate(matches[0], new[] { evId }, Array.Empty<string>()) },
                Array.Empty<ProposedRelation>(), "signature-match");

        if (input.Transition is { TransitionKind: "navigate" })
            return NewProposal(evId, "navigate-prior-different-container");

        return input.Transition is { TransitionKind: "scroll" }
            ? InsufficientProposal("scrolled-unseen-signature")
            : NewProposal(evId, "unseen-signature");
    }

    private static AssociationProposal NewProposal(string evId, string reason) => new(
        AssociationDispositionKind.New, MatchedContainerId: null,
        new[] { new AssociationCandidate("(new)", new[] { evId }, Array.Empty<string>()) },
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);

    private static AssociationProposal InsufficientProposal(string reason) => new(
        AssociationDispositionKind.Insufficient, MatchedContainerId: null,
        Candidates: Array.Empty<AssociationCandidate>(),
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);
}

/// <summary>
/// 脚本 double：按序弹出脚本生成的 proposal（脚本收到 AssociationInput，
/// 可引用 previous containers 的真实 minted id 与当前 EvidenceId）。
/// 用途：测 WorldModel Authority gates 的 backstop——劣质/恶意 strategy
/// 不得越过协议边界。记录收到的输入。
/// </summary>
internal sealed class ScriptedAssociationStrategy : IAssociationStrategy
{
    private readonly Queue<Func<AssociationInput, AssociationProposal>> _queue;
    public List<AssociationInput> Seen { get; } = new();

    public ScriptedAssociationStrategy(
        params Func<AssociationInput, AssociationProposal>[] scripts) =>
        _queue = new Queue<Func<AssociationInput, AssociationProposal>>(scripts);

    public AssociationProposal Propose(AssociationInput input)
    {
        Seen.Add(input);
        return _queue.Count > 0
            ? _queue.Dequeue()(input)
            : new AssociationProposal(AssociationDispositionKind.Insufficient, null,
                Array.Empty<AssociationCandidate>(), Array.Empty<ProposedRelation>(), "script-exhausted");
    }
}
