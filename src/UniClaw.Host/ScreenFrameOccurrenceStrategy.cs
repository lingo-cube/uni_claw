using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// SIM-002 G1：内容帧 claim（screen.frame JSON，LivePerception 产出格式）
/// → occurrence 提案（owner = previous revision 的唯一根容器；非帧 claim
/// 不派生）。live 全真路径依赖本派生（tap 目标 bounds 来自 occurrence
/// locator），故属产品能力、随 G1 从仿真档独立成件。
/// 仿真侧孪生：Simulation Host 的 dev 帧源文件内同名策略（双 Host 不得
/// 互引，两份逻辑保持逐行同步——C1 double 保真度条件）。
/// </summary>
public sealed class ScreenFrameOccurrenceStrategy : IUiObservationStrategy
{
    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
    {
        if (record.Claim.Subject != "screen.frame")
            return Array.Empty<ProposedOccurrence>();

        using var document = JsonDocument.Parse(record.Claim.Value);
        var entry = document.RootElement;
        var bounds = entry.GetProperty("b").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var frameId = entry.TryGetProperty("f", out var f) ? f.GetString()! : "v0.frame";
        var owner = previous?.Containers.Count == 1
            ? previous.Containers[0].Identity.ContainerId
            : null;
        return new[]
        {
            new ProposedOccurrence(
                OwningContainerId: owner,
                Role: entry.GetProperty("role").GetString()!,
                SemanticDescriptor: null,
                State: entry.GetProperty("state").GetString(),
                Locator: new SpatialLocator(bounds[0], bounds[1], bounds[2], bounds[3], frameId)),
        };
    }
}
