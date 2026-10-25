using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    public ValueTask<RelevanceJudgment> JudgeRelevanceAsync(EvidenceRecord record, CancellationToken cancellationToken = default) =>
        RecordOperationAsync("relevance", new { record.EvidenceId }, () => JudgeRelevanceCore(record), cancellationToken);

    public ValueTask<WorldBeliefRevision> ReconcileAsync(EvidenceRecord record, RelevanceJudgment judgment,
        TransitionContext? transitionContext = null, CancellationToken cancellationToken = default) =>
        RecordOperationAsync("reconcile", new { record.EvidenceId, judgment, transitionContext },
            () => ReconcileCore(record, judgment, transitionContext), cancellationToken);

    public ValueTask<DemandHandle> RegisterContinuityDemandAsync(ContinuityDemand demand, CancellationToken cancellationToken = default) =>
        RecordOperationAsync("demand-register", demand, () => RegisterContinuityDemandCore(demand), cancellationToken);

    public async ValueTask RevokeContinuityDemandAsync(string demandId, CancellationToken cancellationToken = default) =>
        _ = await RecordOperationAsync("demand-revoke", new { demandId }, () => { RevokeContinuityDemandCore(demandId); return true; }, cancellationToken).ConfigureAwait(false);

    public ValueTask<ContinuityResolution> ResolveContinuityAsync(DemandHandle handle, CancellationToken cancellationToken = default) =>
        RecordOperationAsync("continuity", handle, () => ResolveContinuityCore(handle), cancellationToken);

    internal ValueTask<bool> RecordObservedNavigationAsync(ObservedNavigation navigation) =>
        RecordOperationAsync("navigation", navigation, () => RecordObservedNavigationCore(navigation));

    internal ValueTask<bool> RecordObservedViewportTransitionAsync(ObservedViewportTransition observation) =>
        RecordOperationAsync("viewport", observation, () => RecordObservedViewportTransitionCore(observation));

    internal ValueTask<WorldBeliefRevision?> ResolveConflictAsync(string subject, string resolvedValue, string tier,
        string? overruledProducer, string basis, DateTimeOffset resolvedAt) =>
        RecordOperationAsync("conflict-resolution", new { subject, resolvedValue, tier, overruledProducer, basis, resolvedAt },
            () => ResolveConflictCore(subject, resolvedValue, tier, overruledProducer, basis, resolvedAt));
}
