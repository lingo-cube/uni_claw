using System.Text.Json;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 deterministic association double（UIWorldGroundingSeamTests
/// SeedContainerAssociationStrategy 同款）：首条 evidence 提议 New root
/// container（evidence-backed，经 Authority gates），之后一律 Insufficient
/// ——场景世界恒单 root，DeriveSlice/ActViaCurrentGrounding 可用。
/// </summary>
public sealed class SeedingAssociationStrategy : IAssociationStrategy
{
    public AssociationProposal Propose(AssociationInput input) => input.Previous is null
        ? new AssociationProposal(
            AssociationDispositionKind.New, MatchedContainerId: null,
            new[] { new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()) },
            Relations: Array.Empty<ProposedRelation>(), Reason: "seed-root-container")
        : new AssociationProposal(
            AssociationDispositionKind.Insufficient, MatchedContainerId: null,
            Candidates: Array.Empty<AssociationCandidate>(),
            Relations: Array.Empty<ProposedRelation>(), Reason: "seed-once");
}

/// <summary>
/// RFS-001 deterministic occurrence derivation double：live.frame claim
/// （ScenarioPerceptionAdapter 的 join 产物）→ ProposedOccurrence 集合
/// （role/state/归一化 bounds，frame = AdbEffectDriver.SupportedFrame）。
/// 非 live.frame record（如 reviewed state claim）→ 携带 previous occurrence
/// 景观原样重提议：bundle stimulus 中 claim proposal 在 live.frame 之后
/// 入证，revision-local 替换语义下若返回空集会清空接地景观（确定性：
/// 同输入同输出——previous revision 是 seam 的合法输入）。
/// </summary>
public sealed class ReplayFrameObservationStrategy : IUiObservationStrategy
{
    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Claim.Subject != "live.frame"
            || !record.Claim.Value.StartsWith("{\"detects\"", StringComparison.Ordinal))
            return CarryOver(previous);

        var owner = previous is { Containers.Count: 1 }
            ? previous.Containers[0].Identity.ContainerId
            : null;
        using var document = JsonDocument.Parse(record.Claim.Value);
        var occurrences = new List<ProposedOccurrence>();
        foreach (var entry in document.RootElement.GetProperty("detects").EnumerateArray())
        {
            var cls = entry.GetProperty("cls").GetString() ?? throw new InvalidOperationException(
                "live.frame detects 条目缺 cls（fail closed）");
            var role = entry.TryGetProperty("role", out var roleProperty) && roleProperty.GetString() is { Length: > 0 } roleValue
                ? roleValue
                : cls;
            string? state = entry.TryGetProperty("st", out var stateProperty)
                ? stateProperty.GetString()
                : null;
            var bounds = entry.GetProperty("b").EnumerateArray()
                .Select(e => e.GetDouble()).ToArray();
            occurrences.Add(new ProposedOccurrence(
                owner, role, SemanticDescriptor: null, State: state,
                Locator: new SpatialLocator(bounds[0], bounds[1], bounds[2], bounds[3],
                    AdbEffectDriver.SupportedFrame),
                Native: null));
        }
        return occurrences;
    }

    /// <summary>previous occurrence 景观原样重提议（owner 重挂在当前唯一 root）。</summary>
    private static IReadOnlyList<ProposedOccurrence> CarryOver(WorldBeliefRevision? previous)
    {
        if (previous?.Occurrences is not { Count: > 0 } carried)
            return Array.Empty<ProposedOccurrence>();
        var owner = previous!.Containers is { Count: 1 } containers
            ? containers[0].Identity.ContainerId
            : null;
        return carried
            .Select(o => new ProposedOccurrence(
                o.OwningContainerId ?? owner, o.Role, o.SemanticDescriptor,
                o.State, o.Locator, o.Native))
            .ToList();
    }
}
