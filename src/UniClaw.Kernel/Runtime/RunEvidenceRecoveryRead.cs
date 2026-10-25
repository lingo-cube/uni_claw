using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Runtime;

/// <summary>Exact original Run/Runtime/Evidence committed endpoints. Historical only.</summary>
public sealed record RunEvidenceRecoveryPoint(RunRecoveryPoint RunAndResources, EvidenceHistoryPosition EvidenceHistory);

/// <summary>Independent historical results survive a failed comparison. No current World or execution state is restored.</summary>
public sealed record RunEvidenceRecoveryRead(RunRecoveryRead RunAndResources, EvidenceLedger? Evidence,
    RunEvidenceRecoveryPoint? Point, bool HistoriesConsistent, string? Reason)
{
    public bool ContinuationSupported => false;
}

public static partial class RunResourceAccounting
{
    /// <summary>Compare original read-only Owners and the last original resource commit, never replaying admission or Outcome judgment.</summary>
    public static RunEvidenceRecoveryRead InspectEvidenceRecovery(RunModel run, RunResourceRead resources, EvidenceLedger evidence)
    {
        var pair = InspectRecovery(run, resources);
        var reason = !pair.StateAndResourcesConsistent ? pair.Reason
            : !evidence.IsReadOnly ? "evidence-history-not-read-only"
            : evidence.HistoryPosition is null || resources.EvidenceHistoryPosition is null ? "resource-evidence-link-missing"
            : evidence.HistoryPosition != resources.EvidenceHistoryPosition ? "resource-evidence-position-mismatch"
            : null;
        return new(pair, evidence, reason is null ? new(pair.Point!, evidence.HistoryPosition!) : null,
            reason is null, reason);
    }
}
