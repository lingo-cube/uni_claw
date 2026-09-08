using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// UIW-003 测试 doubles —— 确定性 observation / continuity seams 与 demand
/// 构造器。value 约定（测试域，非协议）：occurrence value = "role:descriptor"
/// 片段，多 occurrence 用 '+' 连接；descriptor 前缀 "contradicts:" = 语义反证。
/// </summary>
internal static class UIWorldContinuityDoubles
{
    /// <summary>observation + continuity 双 seam kernel（无 association → 无 containers，
    /// occurrence 的 OwningContainerId 默认 null，避开 container 级联）。</summary>
    public static (UniKernel Kernel, WorldModel World) NewKernel(
        IUiObservationStrategy observation,
        IContinuityStrategy continuity,
        params string[] extraScope)
    {
        var scope = new HashSet<string> { UIWorldDoubles.Observed };
        foreach (var s in extraScope) scope.Add(s);
        var world = new WorldModel(scope, associationStrategy: null, observation, continuity);
        return (new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance), world);
    }

    /// <summary>EffectTargetCommitment demand 构造器（测试域）。</summary>
    public static ContinuityDemand Demand(string demandId, string role,
        string? descriptor = null, string? anchor = null, string? anchorRevision = null,
        string? itemId = null) => new(
        demandId, ContinuityDemandSourceKind.EffectTargetCommitment,
        OwningContainerId: null, role, descriptor, anchor, anchorRevision, itemId);
}

/// <summary>
/// 正路径 observation double：从 evidence claim value 确定性派生 occurrences。
/// "role:descriptor" 片段以 '+' 连接；固定 owning container（默认 null）。
/// </summary>
internal sealed class RoleObservationStrategy(string? owningContainerId = null) : IUiObservationStrategy
{
    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
        record.Claim.Value.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment =>
            {
                var parts = segment.Split(':', 2);
                return new ProposedOccurrence(owningContainerId, parts[0],
                    parts.Length == 2 ? parts[1] : null);
            })
            .ToArray();
}

/// <summary>零派生 double（S8：无候选 occurrence 路径）。</summary>
internal sealed class EmptyObservationStrategy : IUiObservationStrategy
{
    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
        Array.Empty<ProposedOccurrence>();
}

/// <summary>
/// 正路径 continuity double：按 role 在 candidate occurrences 中确定性判别。
/// 约定（测试域）：descriptor 前缀 "contradicts:" → Contradicted（support =
/// 该 occurrence 的 evidence basis）；既有 item 匹配优先 demand.LogicalItemId，
/// 否则按 role + Established 回退（descriptor 精确匹配优先；descriptor 缺失时
/// role 匹配回退——状态/文本/值变化不终止 continuity，ADR-0015）；0 匹配 →
/// Insufficient；多匹配 → Ambiguous（Identity never creates information）。
/// </summary>
internal sealed class RoleContinuityStrategy : IContinuityStrategy
{
    public ContinuityProposal Propose(ContinuityAdjudicationInput input)
    {
        var demand = input.Demand;
        var roleMatches = input.Candidates.Where(c => c.Role == demand.Role).ToList();

        var contradiction = roleMatches.FirstOrDefault(c =>
            c.SemanticDescriptor?.StartsWith("contradicts:", StringComparison.Ordinal) == true);
        if (contradiction is not null)
            return new ContinuityProposal(
                ContinuityProposedOutcomeKind.Contradicted,
                contradiction.OccurrenceId, demand.LogicalItemId,
                contradiction.SupportingEvidenceIds.ToArray(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "semantic-contradiction");

        var item = demand.LogicalItemId is not null
            ? input.ExistingItems.FirstOrDefault(i => i.LogicalItemId == demand.LogicalItemId
                && i.Lifecycle == LogicalItemLifecycle.Established)
            : input.ExistingItems.FirstOrDefault(i => i.Role == demand.Role
                && i.Lifecycle == LogicalItemLifecycle.Established);

        var descriptorMatches = roleMatches
            .Where(c => c.SemanticDescriptor == demand.SemanticDescriptor).ToList();
        var chosen = descriptorMatches.Count > 0 ? descriptorMatches : roleMatches;
        if (chosen.Count == 0)
            return new ContinuityProposal(
                ContinuityProposedOutcomeKind.Insufficient, null, null,
                Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "no-role-matching-candidate");
        if (chosen.Count > 1)
            return new ContinuityProposal(
                ContinuityProposedOutcomeKind.Ambiguous, null, null,
                Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "multiple-plausible-referents");

        var occurrence = chosen[0];
        var support = occurrence.SupportingEvidenceIds.ToArray();
        return item is null
            ? new ContinuityProposal(
                ContinuityProposedOutcomeKind.ReferenceEstablished,
                occurrence.OccurrenceId, MatchedLogicalItemId: null,
                support, Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "single-role-match-mint")
            : new ContinuityProposal(
                ContinuityProposedOutcomeKind.SameReferent,
                occurrence.OccurrenceId, item.LogicalItemId,
                support, Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "single-role-match-continuity");
    }
}

/// <summary>
/// 脚本 double：按序弹出脚本生成的 proposal（脚本收到 ContinuityAdjudicationInput，
/// 可引用真实 occurrence id / item id / evidence refs）。记录收到的输入。
/// </summary>
internal sealed class ScriptedContinuityStrategy : IContinuityStrategy
{
    private readonly Queue<Func<ContinuityAdjudicationInput, ContinuityProposal>> _queue;
    public List<ContinuityAdjudicationInput> Seen { get; } = new();

    public ScriptedContinuityStrategy(
        params Func<ContinuityAdjudicationInput, ContinuityProposal>[] scripts) =>
        _queue = new Queue<Func<ContinuityAdjudicationInput, ContinuityProposal>>(scripts);

    public ContinuityProposal Propose(ContinuityAdjudicationInput input)
    {
        Seen.Add(input);
        return _queue.Count > 0
            ? _queue.Dequeue()(input)
            : new ContinuityProposal(
                ContinuityProposedOutcomeKind.Insufficient, null, null,
                Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<ProposedTermination>(), "script-exhausted");
    }
}
