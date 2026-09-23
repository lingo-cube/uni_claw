using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// SIM-002 G1：从 V0Runtime 拆出的产品能力——World Model 观察推导策略。
/// 属产品侧（IUiObservationStrategy 的 Host realization），非仿真专用。
/// </summary>
public sealed class FrameOccurrenceStrategy : IUiObservationStrategy
{
    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
    {
        // 检测类 claim（ui.detect.*.class）→ 产出 occurrence 提案
        if (!record.Claim.Subject.StartsWith("ui.detect.", StringComparison.Ordinal))
            return Array.Empty<ProposedOccurrence>();

        var role = record.Claim.Subject["ui.detect.".Length..][..^".class".Length];
        return new[]
        {
            new ProposedOccurrence(
                Role: role,
                SemanticDescriptor: null,
                OwningContainerId: null,
                EvidenceId: record.EvidenceId),
        };
    }
}
