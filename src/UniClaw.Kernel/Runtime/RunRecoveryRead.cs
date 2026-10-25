using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Runtime;

/// <summary>Exact committed versions of the two original histories, for read-only comparison.</summary>
public sealed record RunRecoveryPoint(RunHistoryPosition RunHistory, string ResourceRecordingId, int ResourceSequence);

/// <summary>Historical Run/Runtime comparison only. World, Control, Plan and current Evidence are not restored.</summary>
public sealed record RunRecoveryRead(RunModel? Run, RunResourceRead Resources, RunRecoveryPoint? Point,
    bool StateAndResourcesConsistent, string? Reason)
{
    public bool ContinuationSupported => false;
}

public static partial class RunResourceAccounting
{
    /// <summary>Compare original Owner versions, without inferring steps from Run progress or judging Outcome again.</summary>
    public static RunRecoveryRead InspectRecovery(RunModel run, RunResourceRead resources)
    {
        var reason = !run.IsReadOnly ? "run-history-not-read-only"
            : !resources.AccountingComplete ? resources.Reason ?? "resource-history-incomplete"
            : !resources.CommittedEndpointVerified ? "resource-endpoint-unverified"
            : run.HistoryPosition is null || resources.RunHistoryPosition is null ? "run-resource-link-missing"
            : run.RunId != resources.Identity?.RunId ? "run-resource-identity-mismatch"
            : run.HistoryPosition != resources.RunHistoryPosition ? "run-resource-position-mismatch"
            : run.IsTerminal != resources.Confirmed!.Terminal ? "run-resource-terminal-mismatch"
            : !Guid.TryParseExact(resources.RecordingId, "N", out _) || resources.LastSequence < 1 ? "resource-position-unavailable"
            : null;
        return new(run, resources, reason is null ? new(run.HistoryPosition!, resources.RecordingId!, resources.LastSequence) : null,
            reason is null, reason);
    }
}
