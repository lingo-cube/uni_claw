using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Repository-audited RunSnapshot field classification (design.md §4).
/// </summary>
public enum SnapshotFieldClassification
{
    /// <summary>Truthfully projected from the Agent public read model (Agent.State, Belief, LastTrap).</summary>
    DirectPublicProjection,

    /// <summary>Derived read model — visibly identified as derived, never presented as canonical Kernel state.</summary>
    DerivedReadModel,

    /// <summary>Absent — no truthful source on the current public surface; never invented.</summary>
    NotCurrentlyAvailable,
}

/// <summary>
/// One classified RunSnapshot field. Value is null when the field is
/// not currently available. <see cref="IsPartial"/> marks partial evidence
/// (e.g. goal evidence with State+Reason but no SourceObservationSequence).
/// </summary>
public sealed record SnapshotField<T>(
    T? Value,
    SnapshotFieldClassification Classification,
    string TruthSource,
    bool IsPartial = false)
{
    /// <summary>Creates a direct public projection field.</summary>
    public static SnapshotField<T> Direct(T? value, string source)
        => new(value, SnapshotFieldClassification.DirectPublicProjection, source);

    /// <summary>Creates a derived read-model field.</summary>
    public static SnapshotField<T> Derived(T? value, string source)
        => new(value, SnapshotFieldClassification.DerivedReadModel, source);

    /// <summary>Creates an unavailable field.</summary>
    public static SnapshotField<T> Unavailable(string source)
        => new(default, SnapshotFieldClassification.NotCurrentlyAvailable, source);

    /// <summary>Creates an unavailable field retaining partial evidence.</summary>
    public static SnapshotField<T> UnavailablePartial(T? value, string source)
        => new(value, SnapshotFieldClassification.NotCurrentlyAvailable, source, IsPartial: true);
}

/// <summary>Goal summary derived from the RunSemanticGoal span tag (DERIVED_READ_MODEL).</summary>
public sealed record GoalSummary(string Goal);

/// <summary>Latest decision-shaped trace event (DERIVED_READ_MODEL).</summary>
public sealed record DecisionSummary(string? Reason, string? ActionId, string? StepId, string? ContainerId);

/// <summary>Latest dispatched action trace event (DERIVED_READ_MODEL).</summary>
public sealed record ActionSummary(string ActionId, string? StepId, string? ContainerId, string ActionDescription);

/// <summary>Latest recovery trace event (DERIVED_READ_MODEL).</summary>
public sealed record RecoverySummary(string RecoveryId, string? Reason, string? ContainerId, string? StepId);

/// <summary>Partial goal evidence summary — full GoalEvidence is NOT on the Agent public surface.</summary>
public sealed record GoalEvidenceSummary(bool Satisfied, string? Reason, long? SourceObservationSequence, bool IsPartial);

/// <summary>
/// Read-only projection of Kernel-owned run state (design.md §4 / §6).
/// The consumer NEVER becomes a second mutable owner; no mutable references
/// are exposed; every field retains its audited classification.
/// </summary>
public sealed record RunSnapshot
{
    /// <summary>Run identity.</summary>
    public string RunId { get; init; } = "";

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.State.</summary>
    public SnapshotField<RunState> RunState { get; init; } = SnapshotField<RunState>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.Belief.SemanticPage.</summary>
    public SnapshotField<string?> CurrentSemanticPage { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.CurrentObservedLocation.</summary>
    public SnapshotField<string?> CurrentObservedLocation { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.ActiveExecutionContainer.</summary>
    public SnapshotField<string?> ActiveExecutionContainer { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — immutable active ancestor path copy.</summary>
    public SnapshotField<ImmutableArray<string>> ActiveAncestorPath { get; init; } = SnapshotField<ImmutableArray<string>>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.CurrentNodeRef (V2 physical current).</summary>
    public SnapshotField<ContainerNodeRef?> CurrentContainerNodeRef { get; init; } = SnapshotField<ContainerNodeRef?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.CurrentSliceRef (V2 fresh current Slice).</summary>
    public SnapshotField<ContainerSliceRef?> CurrentSliceRef { get; init; } = SnapshotField<ContainerSliceRef?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.EntrySourceNodeRef (path-relative entry source).</summary>
    public SnapshotField<ContainerNodeRef?> EntrySourceNodeRef { get; init; } = SnapshotField<ContainerNodeRef?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.EntryTransitionOccurrenceRef (path-relative entry occurrence).</summary>
    public SnapshotField<TransitionOccurrenceRef?> EntryTransitionOccurrenceRef { get; init; } = SnapshotField<TransitionOccurrenceRef?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.EntryRelationRef (optional path-relative entry relation).</summary>
    public SnapshotField<ContainerRelationRef?> EntryRelationRef { get; init; } = SnapshotField<ContainerRelationRef?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.LatestTransitionOccurrence (latest immutable V2 occurrence).</summary>
    public SnapshotField<ContainerTransitionOccurrence?> LatestTransitionOccurrence { get; init; } = SnapshotField<ContainerTransitionOccurrence?>.Unavailable("No snapshot data.");

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.ContainerContext.EvidenceRevision (accepted V2 evidence revision).</summary>
    public SnapshotField<SemanticEvidenceRevision?> EvidenceRevision { get; init; } = SnapshotField<SemanticEvidenceRevision?>.Unavailable("No snapshot data.");

    /// <summary>NOT_CURRENTLY_AVAILABLE — no latest Fast assessment value on the public surface; the production path deliberately retains no mutable latest Fast slot.</summary>
    public SnapshotField<ContainerFastAssessmentAvailability?> FastAssessmentAvailability { get; init; } = SnapshotField<ContainerFastAssessmentAvailability?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — latest typed transition from immutable history.</summary>
    public SnapshotField<ContainerTransition?> LatestObservedTransition { get; init; } = SnapshotField<ContainerTransition?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — existing completeness evidence reference.</summary>
    public SnapshotField<string?> ContainerCompletenessRef { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — existing transition evidence reference.</summary>
    public SnapshotField<string?> ContainerEvidenceRef { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — catalog observation ref correlated from FreshObservationRef.</summary>
    public SnapshotField<string?> ContainerCatalogEvidenceRef { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — correlated capture asset reference.</summary>
    public SnapshotField<string?> ContainerAssetRef { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — all correlated capture assets, logical refs only.</summary>
    public SnapshotField<ImmutableArray<string>> ContainerAssetRefs { get; init; } = SnapshotField<ImmutableArray<string>>.Unavailable("No snapshot data.");

    /// <summary>Compatibility names for the structured Container evidence refs.</summary>
    public SnapshotField<string?> CompletenessRef => ContainerCompletenessRef;
    /// <summary>Compatibility alias for ContainerEvidenceRef.</summary>
    public SnapshotField<string?> EvidenceRef => ContainerEvidenceRef;
    /// <summary>Compatibility alias for ContainerAssetRef.</summary>
    public SnapshotField<string?> AssetRef => ContainerAssetRef;

    /// <summary>DIRECT_PUBLIC_PROJECTION — Agent.LastTrap.</summary>
    public SnapshotField<Trap?> ActiveTrap { get; init; } = SnapshotField<Trap?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — RunSemanticGoal span tag "goal".</summary>
    public SnapshotField<GoalSummary?> CurrentGoal { get; init; } = SnapshotField<GoalSummary?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — latest TraceEvent(Reason/ActionId).</summary>
    public SnapshotField<DecisionSummary?> LastDecision { get; init; } = SnapshotField<DecisionSummary?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — latest TraceEvent(ActionId,Action).</summary>
    public SnapshotField<ActionSummary?> LastAction { get; init; } = SnapshotField<ActionSummary?>.Unavailable("No snapshot data.");

    /// <summary>DERIVED_READ_MODEL — latest TraceEvent(RecoveryId).</summary>
    public SnapshotField<RecoverySummary?> RecoveryState { get; init; } = SnapshotField<RecoverySummary?>.Unavailable("No snapshot data.");

    /// <summary>NOT_CURRENTLY_AVAILABLE (partial) — State=Completed + Reason only.</summary>
    public SnapshotField<GoalEvidenceSummary?> LatestGoalEvidence { get; init; } = SnapshotField<GoalEvidenceSummary?>.Unavailable("No snapshot data.");

    /// <summary>NOT_CURRENTLY_AVAILABLE — active Container private.</summary>
    public SnapshotField<long?> CurrentObservationSequence { get; init; } = SnapshotField<long?>.Unavailable("No snapshot data.");

    /// <summary>NOT_CURRENTLY_AVAILABLE — active Container private.</summary>
    public SnapshotField<string?> CurrentContainerSummary { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>NOT_CURRENTLY_AVAILABLE — Container.ObjectBindings not on public surface.</summary>
    public SnapshotField<string?> BindingsSummary { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>NOT_CURRENTLY_AVAILABLE — Container.ObjectStateBeliefs not on public surface.</summary>
    public SnapshotField<string?> StateBeliefsSummary { get; init; } = SnapshotField<string?>.Unavailable("No snapshot data.");

    /// <summary>Projection diagnostics (gaps, mismatch warnings) — never runtime authority.</summary>
    public ImmutableArray<string> Diagnostics { get; init; } = [];

    /// <summary>A run with no registered projection data — every field remains NotCurrentlyAvailable.</summary>
    public static RunSnapshot Unknown(string runId, string diagnostic)
        => new() { RunId = runId, Diagnostics = [diagnostic] };
}
