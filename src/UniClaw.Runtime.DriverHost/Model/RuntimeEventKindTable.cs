using System.Collections.Immutable;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Frozen audited classification table for all 18 RuntimeEventKinds
/// (design.md §2). This table IS the contract for what the projection may
/// emit: a kind is emitted only when <see cref="RuntimeEventKindMetadata.EmittableInSlice"/>
/// is true AND a truthful source instance exists at projection time.
/// </summary>
public static class RuntimeEventKindTable
{
    public static IReadOnlyList<RuntimeEventKindMetadata> All { get; } =
    [
        new(RuntimeEventKind.ObservationProduced, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true, null),
        new(RuntimeEventKind.ContainerReconciled, RuntimeEventSourceClassification.DerivableFromExistingSpan, true, null),
        new(RuntimeEventKind.BindingUpdated, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, false,
            "ObjectBindings delta requires active Container internals — not on Agent public surface."),
        new(RuntimeEventKind.StateBeliefUpdated, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, false,
            "ObjectStateBeliefs delta requires active Container internals — not on Agent public surface."),
        new(RuntimeEventKind.DecisionProposed, RuntimeEventSourceClassification.RequiresNewRuntimeSemanticEmission, false,
            "C-class — out of scope for dsh-kernel-read-only-observability."),
        new(RuntimeEventKind.DecisionAccepted, RuntimeEventSourceClassification.RequiresNewRuntimeSemanticEmission, false,
            "C-class — out of scope for dsh-kernel-read-only-observability."),
        new(RuntimeEventKind.ActionAuthorized, RuntimeEventSourceClassification.RequiresNewRuntimeSemanticEmission, false,
            "C-class — out of scope for dsh-kernel-read-only-observability."),
        new(RuntimeEventKind.ActionDispatched, RuntimeEventSourceClassification.DerivableFromExistingSpanAndPublicReadModel, true, null),
        new(RuntimeEventKind.PostActionObserved, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, false,
            "Traversal journal PostActionObservation is not on Agent public surface."),
        new(RuntimeEventKind.VerificationCompleted, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, false,
            "Traversal journal step Result is not on Agent public surface."),
        new(RuntimeEventKind.NavigationDecision, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true, null),
        new(RuntimeEventKind.ViewportExplorationDecision, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true, null),
        new(RuntimeEventKind.TrapRaised, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true, null),
        new(RuntimeEventKind.RecoveryStarted, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true, null),
        new(RuntimeEventKind.RecoveryVerified, RuntimeEventSourceClassification.RequiresNewRuntimeSemanticEmission, false,
            "C-class — out of scope for dsh-kernel-read-only-observability."),
        new(RuntimeEventKind.GoalEvidenceProduced, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true,
            "Partial only: State=Completed + Reason; full GoalEvidence not on Agent public surface."),
        new(RuntimeEventKind.RunCompleted, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true, null),
        new(RuntimeEventKind.RunFailed, RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel, true, null),
    ];

    public static RuntimeEventKindMetadata For(RuntimeEventKind kind)
        => All.First(m => m.Kind == kind);
}
