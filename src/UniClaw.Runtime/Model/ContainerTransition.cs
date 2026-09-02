using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>Closed evidence vocabulary for a Container transition.</summary>
public enum ContainerTransitionKind
{
    SAME_CONTAINER,
    ENTER_CHILD,
    VERIFIED_RETURN_TO_ACTIVE_PARENT,
    PREMATURE_RETURN_TO_ACTIVE_PARENT,
    KNOWN_NON_PARENT_TRANSITION,
    EXTERNAL_EXIT,
    UNKNOWN_TRANSITION,
}

/// <summary>Closed vocabulary describing the permitted state effect of a transition.</summary>
public enum ContainerTransitionDisposition
{
    OBSERVED_AND_EXECUTION_ADVANCED,
    OBSERVED_AND_EXECUTION_RESUMED,
    OBSERVED_EXECUTION_PRESERVED,
    NO_COMMIT_FAIL_CLOSED,
}

/// <summary>
/// Closed intents that may carry a prepared branch-progress replacement into
/// the Agent reconciliation seam.  The seam validates each intent against
/// the corresponding transition and evidence references; callers cannot
/// authorize arbitrary replacement with a boolean.
/// </summary>
public enum ContainerProgressReplacementIntent
{
    /// <summary>No branch-progress replacement is permitted.</summary>
    None,
    /// <summary>Verified child return carries exact completed-sibling evidence.</summary>
    VerifiedChildReturn,
    /// <summary>External boundary observation carries one exact pending obligation.</summary>
    ExternalBoundaryObserved,
    /// <summary>External boundary return carries one exact verified disposition.</summary>
    ExternalBoundaryReturned,
}

/// <summary>
/// Immutable typed evidence for one fresh Container-location observation.
/// It describes evidence and the already-permitted state effect only; it does
/// not authorize action, recovery, route selection, completion, or re-entry.
/// </summary>
public sealed record ContainerTransition
{
    public ContainerTransition(
        string transitionRef,
        string? fromObservedLocation,
        string? toObservedLocation,
        string? activeExecutionContainer,
        string? activeParentAtObservation,
        string freshObservationRef,
        string? completenessRef,
        ContainerTransitionKind kind,
        ContainerTransitionDisposition disposition,
        string? evidenceRef = null,
        string? assetRef = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(freshObservationRef);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(disposition)) throw new ArgumentOutOfRangeException(nameof(disposition));

        TransitionRef = transitionRef;
        FromObservedLocation = fromObservedLocation;
        ToObservedLocation = toObservedLocation;
        ActiveExecutionContainer = activeExecutionContainer;
        ActiveParentAtObservation = activeParentAtObservation;
        FreshObservationRef = freshObservationRef;
        CompletenessRef = completenessRef;
        Kind = kind;
        Disposition = disposition;
        EvidenceRef = evidenceRef;
        AssetRef = assetRef;
    }

    /// <summary>Deterministic reference derived from RunId and FreshObservationRef.</summary>
    public string TransitionRef { get; }

    /// <summary>Previously accepted observed location; null means unavailable/Unknown.</summary>
    public string? FromObservedLocation { get; }

    /// <summary>Fresh observed location; null means Unknown.</summary>
    public string? ToObservedLocation { get; }

    /// <summary>Execution obligation before this evidence was classified.</summary>
    public string? ActiveExecutionContainer { get; }

    /// <summary>Immediate active parent identity, when the execution path has one.</summary>
    public string? ActiveParentAtObservation { get; }

    /// <summary>Reference to the fresh observation, never an embedded observation body.</summary>
    public string FreshObservationRef { get; }

    /// <summary>Reference to existing completeness evidence; completeness is never copied.</summary>
    public string? CompletenessRef { get; }

    public ContainerTransitionKind Kind { get; }
    public ContainerTransitionDisposition Disposition { get; }

    /// <summary>Optional logical evidence-chain reference.</summary>
    public string? EvidenceRef { get; }

    /// <summary>Optional capture asset reference; no asset body is embedded.</summary>
    public string? AssetRef { get; }

    /// <summary>Explicitly reports an absent capture asset rather than inventing one.</summary>
    public bool IsAssetMissing => string.IsNullOrWhiteSpace(AssetRef);

    /// <summary>Compatibility aliases used by read-only consumers.</summary>
    public string? PreviousObservedLocation => FromObservedLocation;
    public string? CurrentObservedLocation => ToObservedLocation;
    public string? ActiveParent => ActiveParentAtObservation;

    /// <summary>Derive a stable reference without creating mutable identity state.</summary>
    public static string DeriveTransitionRef(string runId, string freshObservationRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(freshObservationRef);
        return $"{runId}:container-transition:{freshObservationRef}";
    }
}

/// <summary>Inputs to the pure ContainerTransition classifier.</summary>
public sealed record ContainerTransitionClassificationInput
{
    public string RunId { get; init; } = string.Empty;
    public string? FromObservedLocation { get; init; }
    public string? ToObservedLocation { get; init; }
    public string? ActiveExecutionContainer { get; init; }
    public string? ActiveParentAtObservation { get; init; }
    public bool IsVerifiedReturn { get; init; }
    public bool IsAuthorizedChildEntry { get; init; }
    public bool IsExternalExit { get; init; }
    public string FreshObservationRef { get; init; } = string.Empty;
    public string? CompletenessRef { get; init; }
    public string? EvidenceRef { get; init; }
    public string? AssetRef { get; init; }
}

/// <summary>Result of pure preparation; failure carries no live-state mutation.</summary>
public sealed record ContainerTransitionPreparation
{
    private ContainerTransitionPreparation(bool canCommit, ContainerTransition transition, string? failureReason)
    {
        CanCommit = canCommit;
        Transition = transition;
        FailureReason = failureReason;
    }

    public bool CanCommit { get; }
    public ContainerTransition Transition { get; }
    public string? FailureReason { get; }

    public static ContainerTransitionPreparation Accepted(ContainerTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return new(true, transition, null);
    }

    public static ContainerTransitionPreparation Rejected(ContainerTransition transition, string reason)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(false, transition, reason);
    }
}

/// <summary>
/// Stateless, deterministic evidence classifier. It never chooses an action,
/// recovery, route, completion result, or re-entry policy.
/// </summary>
public static class ContainerTransitionClassifier
{
    public static ContainerTransition Classify(ContainerTransitionClassificationInput input)
        => Prepare(input).Transition;

    public static ContainerTransitionPreparation Prepare(ContainerTransitionClassificationInput? input)
    {
        if (input is null)
            return Rejected("classification input is unavailable");

        if (string.IsNullOrWhiteSpace(input.RunId)
            || string.IsNullOrWhiteSpace(input.FreshObservationRef)
            || string.IsNullOrWhiteSpace(input.ActiveExecutionContainer))
        {
            return Rejected("run, fresh observation, and active execution references are required");
        }

        var policyFlags = (input.IsVerifiedReturn ? 1 : 0)
            + (input.IsAuthorizedChildEntry ? 1 : 0)
            + (input.IsExternalExit ? 1 : 0);
        if (policyFlags > 1)
            return Rejected("transition policy flags are contradictory");

        if (input.IsVerifiedReturn
            && (string.IsNullOrWhiteSpace(input.ActiveParentAtObservation)
                || !string.Equals(input.ToObservedLocation, input.ActiveParentAtObservation, StringComparison.Ordinal)))
        {
            return Rejected("verified return requires an exact active parent destination");
        }

        if (input.IsAuthorizedChildEntry
            && (string.IsNullOrWhiteSpace(input.ToObservedLocation)
                || string.Equals(input.ToObservedLocation, input.ActiveExecutionContainer, StringComparison.Ordinal)
                || string.Equals(input.ToObservedLocation, input.ActiveParentAtObservation, StringComparison.Ordinal)))
        {
            return Rejected("authorized child entry requires a distinct known child destination");
        }

        var kind = ClassifyKind(input);
        var disposition = kind switch
        {
            ContainerTransitionKind.ENTER_CHILD => ContainerTransitionDisposition.OBSERVED_AND_EXECUTION_ADVANCED,
            ContainerTransitionKind.VERIFIED_RETURN_TO_ACTIVE_PARENT => ContainerTransitionDisposition.OBSERVED_AND_EXECUTION_RESUMED,
            _ => ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED,
        };
        var transition = new ContainerTransition(
            ContainerTransition.DeriveTransitionRef(input.RunId, input.FreshObservationRef),
            input.FromObservedLocation,
            input.ToObservedLocation,
            input.ActiveExecutionContainer,
            input.ActiveParentAtObservation,
            input.FreshObservationRef,
            input.CompletenessRef,
            kind,
            disposition,
            input.EvidenceRef,
            input.AssetRef);
        return ContainerTransitionPreparation.Accepted(transition);
    }

    private static ContainerTransitionKind ClassifyKind(ContainerTransitionClassificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsExternalExit)
            return ContainerTransitionKind.EXTERNAL_EXIT;
        if (input.ToObservedLocation is null)
            return ContainerTransitionKind.UNKNOWN_TRANSITION;
        if (string.Equals(input.ToObservedLocation, input.ActiveExecutionContainer, StringComparison.Ordinal))
            return ContainerTransitionKind.SAME_CONTAINER;
        if (string.Equals(input.ToObservedLocation, input.ActiveParentAtObservation, StringComparison.Ordinal))
        {
            return input.IsVerifiedReturn
                ? ContainerTransitionKind.VERIFIED_RETURN_TO_ACTIVE_PARENT
                : ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT;
        }
        if (input.IsAuthorizedChildEntry)
            return ContainerTransitionKind.ENTER_CHILD;
        return ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION;
    }

    private static ContainerTransitionPreparation Rejected(string reason)
    {
        // Rejected preparations must remain visibly synthetic; never turn an
        // absent identity into a valid-looking run or observation reference.
        const string safeRunId = "rejected:invalid-input";
        const string safeObservation = "rejected:invalid-input";
        var transition = new ContainerTransition(
            ContainerTransition.DeriveTransitionRef(safeRunId, safeObservation),
            null, null, null, null, safeObservation, null,
            ContainerTransitionKind.UNKNOWN_TRANSITION,
            ContainerTransitionDisposition.NO_COMMIT_FAIL_CLOSED);
        return ContainerTransitionPreparation.Rejected(transition, reason);
    }
}

/// <summary>
/// Availability of the deliberately non-retained Fast assessment in the
/// public Runtime read model.
/// NEW_SYMBOL_JUSTIFICATION: the read seam must distinguish an absent V2
/// state from a present state whose Fast assessment is intentionally not
/// retained; a bool or false assessment would lose that distinction.
/// </summary>
public enum ContainerFastAssessmentAvailability
{
    /// <summary>No V2 state exists from which an assessment could be projected.</summary>
    Unavailable,
    /// <summary>V2 state exists, but the production path does not retain Fast assessment values.</summary>
    NotRetained,
}

/// <summary>Authority-free immutable read projection of transition evidence.</summary>
public sealed record ContainerTransitionReadModel
{
    /// <summary>Gets the observed semantic location projected from the compatibility belief.</summary>
    public string? CurrentObservedLocation { get; init; }
    /// <summary>Gets the independent active execution obligation location.</summary>
    public string? ActiveExecutionContainer { get; init; }
    /// <summary>Gets the ordered immutable execution ancestor path.</summary>
    public ImmutableArray<string> ActiveAncestorPath { get; init; } = [];
    /// <summary>Gets the latest legacy transition projected from append-only history.</summary>
    public ContainerTransition? LatestTransition { get; init; }
    /// <summary>Gets the current V2 node reference, when a current V2 Container exists.</summary>
    public ContainerNodeRef? CurrentNodeRef { get; init; }
    /// <summary>Gets the current V2 Slice reference, when a current V2 Container exists.</summary>
    public ContainerSliceRef? CurrentSliceRef { get; init; }
    /// <summary>Gets the source node reference from the current path-relative entry context.</summary>
    public ContainerNodeRef? EntrySourceNodeRef { get; init; }
    /// <summary>Gets the occurrence reference that established the current entry context.</summary>
    public TransitionOccurrenceRef? EntryTransitionOccurrenceRef { get; init; }
    /// <summary>Gets the optional relation reference from the current entry context.</summary>
    public ContainerRelationRef? EntryRelationRef { get; init; }
    /// <summary>Gets the latest immutable V2 transition occurrence, independent of legacy history.</summary>
    public ContainerTransitionOccurrence? LatestTransitionOccurrence { get; init; }
    /// <summary>Gets the accepted V2 evidence revision, when V2 state is available.</summary>
    public SemanticEvidenceRevision? EvidenceRevision { get; init; }
    /// <summary>Gets whether a V2 aggregate state was available for this projection.</summary>
    public bool IsV2StateAvailable { get; init; }
    /// <summary>Gets whether Fast assessment is unavailable or intentionally not retained.</summary>
    public ContainerFastAssessmentAvailability FastAssessmentAvailability { get; init; }
    /// <summary>Gets the existing completeness evidence reference from the legacy projection.</summary>
    public string? CompletenessRef { get; init; }
    /// <summary>Gets the existing logical evidence-chain reference.</summary>
    public string? EvidenceRef { get; init; }
    /// <summary>Gets the optional capture asset reference.</summary>
    public string? AssetRef { get; init; }
    /// <summary>Gets whether the latest legacy transition has no capture asset.</summary>
    public bool IsAssetMissing { get; init; }
    /// <summary>Gets immutable diagnostics for unavailable or missing read evidence.</summary>
    public ImmutableArray<string> Diagnostics { get; init; } = [];

    public static ContainerTransitionReadModel Unavailable(string diagnostic = "Container context is unavailable.")
        => new() { Diagnostics = [diagnostic] };

    public static ContainerTransitionReadModel From(
        string? observedLocation,
        string? activeExecutionContainer,
        IEnumerable<string>? activeAncestorPath,
        IEnumerable<ContainerTransition>? transitions)
        => From(observedLocation, activeExecutionContainer, activeAncestorPath, transitions, null);

    /// <summary>
    /// Projects legacy transition evidence together with the optional sole V2
    /// aggregate.  V2 values are copied from immutable state; no live handle,
    /// Graph, provider, action, or mutable owner is exposed.
    /// </summary>
    public static ContainerTransitionReadModel From(
        string? observedLocation,
        string? activeExecutionContainer,
        IEnumerable<string>? activeAncestorPath,
        IEnumerable<ContainerTransition>? transitions,
        ContainerRuntimeV2State? v2State)
    {
        var latest = transitions?.LastOrDefault();
        var path = activeAncestorPath is null
            ? default
            : activeAncestorPath.ToImmutableArray();
        var diagnosticsBuilder = ImmutableArray.CreateBuilder<string>();
        if (path.IsDefault)
            diagnosticsBuilder.Add("Active ancestor path unavailable: no structured path snapshot.");
        if (latest is null)
            diagnosticsBuilder.Add("Latest container transition unavailable: no structured transition event.");
        else if (latest.IsAssetMissing)
            diagnosticsBuilder.Add("MISSING_ASSET");
        var model = new ContainerTransitionReadModel
        {
            CurrentObservedLocation = observedLocation,
            ActiveExecutionContainer = activeExecutionContainer,
            ActiveAncestorPath = path,
            LatestTransition = latest,
            CompletenessRef = latest?.CompletenessRef,
            EvidenceRef = latest?.EvidenceRef,
            AssetRef = latest?.AssetRef,
            IsAssetMissing = latest?.IsAssetMissing ?? false,
            Diagnostics = diagnosticsBuilder.ToImmutable(),
        };
        if (v2State is null)
        {
            return model with
            {
                FastAssessmentAvailability = ContainerFastAssessmentAvailability.Unavailable,
            };
        }

        var current = v2State.CurrentContainer;
        var entry = current?.EntryContext;
        return model with
        {
            CurrentNodeRef = current?.NodeRef,
            CurrentSliceRef = current?.CurrentSliceRef,
            EntrySourceNodeRef = entry?.SourceNodeRef,
            EntryTransitionOccurrenceRef = entry?.EntryTransitionOccurrenceRef,
            EntryRelationRef = entry?.EntryRelationRef,
            LatestTransitionOccurrence = v2State.TransitionOccurrences.LastOrDefault(),
            EvidenceRevision = v2State.EvidenceRevision,
            IsV2StateAvailable = true,
            FastAssessmentAvailability = ContainerFastAssessmentAvailability.NotRetained,
        };
    }

    /// <summary>Derive typed events from the immutable DecisionRecord journal; free-form text is ignored.</summary>
    public static ContainerTransitionReadModel From(
        string? observedLocation,
        string? activeExecutionContainer,
        IEnumerable<string>? activeAncestorPath,
        IEnumerable<DecisionRecord>? history)
        => From(observedLocation, activeExecutionContainer, activeAncestorPath, history, null);

    /// <summary>Projects a DecisionRecord journal and optional immutable V2 state.</summary>
    public static ContainerTransitionReadModel From(
        string? observedLocation,
        string? activeExecutionContainer,
        IEnumerable<string>? activeAncestorPath,
        IEnumerable<DecisionRecord>? history,
        ContainerRuntimeV2State? v2State)
        => From(
            observedLocation,
            activeExecutionContainer,
            activeAncestorPath,
            history?
                .Select(entry => entry.ContainerTransition)
                .Where(transition => transition is not null)
                .Cast<ContainerTransition>(),
            v2State);
}

/// <summary>Immutable result of bounded scroll-stability confirmation.</summary>
public sealed record ScrollStabilityResult
{
    private ScrollStabilityResult(Observation? confirmedObservation, ScrollStabilityClassification? classification, string? detail)
    {
        ConfirmedObservation = confirmedObservation;
        FailureClassification = classification;
        FailureDetail = detail;
    }

    public Observation? ConfirmedObservation { get; }
    public ScrollStabilityClassification? FailureClassification { get; }
    public string? FailureDetail { get; }
    public bool IsConfirmed => ConfirmedObservation is not null;

    public static ScrollStabilityResult Confirmed(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new(observation, null, null);
    }

    public static ScrollStabilityResult Failed(ScrollStabilityClassification classification, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return new(null, classification, detail);
    }
}

/// <summary>Non-location quiescence outcome vocabulary used by ScrollStabilityResult.</summary>
public enum ScrollStabilityClassification
{
    Stable,
    CountMismatch,
    ReorderOrSignatureMismatch,
    PositionDrift,
    DuplicateAmbiguity,
    ReobserveFailed,
    LeftContainer,
}
