using UniClaw.Kernel.World;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Runtime;

/// <summary>Four original committed historical endpoints. No current World or execution authority.</summary>
public sealed record RunWorldRecoveryPoint(RunEvidenceRecoveryPoint RunResourcesAndEvidence, WorldHistoryPosition WorldHistory);

/// <summary>Keep independent original histories visible even when no common point can be certified.</summary>
public sealed record RunWorldRecoveryRead(RunEvidenceRecoveryRead RunResourcesAndEvidence, WorldHistoryRead? World,
    RunWorldRecoveryPoint? Point, bool HistoriesConsistent, string? Reason)
{
    public bool ContinuationSupported => false;
}

public static partial class RunResourceAccounting
{
    /// <summary>Compare only complete original readonly endpoints, without selecting an older prefix or judging World again.</summary>
    public static RunWorldRecoveryRead InspectWorldRecovery(RunModel run, RunResourceRead resources,
        EvidenceLedger evidence, WorldHistoryRead world)
    {
        var three = InspectEvidenceRecovery(run, resources, evidence);
        var reason = !three.HistoriesConsistent ? three.Reason
            : resources.WorldHistoryPosition is null ? "resource-world-link-missing"
            : world.Position != resources.WorldHistoryPosition ? "resource-world-position-mismatch"
            : world.RunRecordingId != run.HistoryPosition!.RecordingId
                || world.EvidenceRecordingId != evidence.HistoryPosition!.RecordingId ? "world-owner-source-mismatch"
            : null;
        return new(three, world, reason is null ? new(three.Point!, world.Position) : null, reason is null, reason);
    }
}
