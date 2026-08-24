using System.Collections.Immutable;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.Model;

/// <summary>Closed UniAgent-authored objective vocabulary for one bounded strategy.</summary>
public enum StrategyObjectiveKind
{
    /// <summary>Explore every discoverable branch inside the declared scope.</summary>
    ExploreScope = 1,

    /// <summary>Inspect all discovered items matching one typed semantic criterion.</summary>
    InspectMatchesWithinScope = 2,
}

/// <summary>Closed traversal approach declared by UniAgent.</summary>
public enum ExplorationIntent
{
    /// <summary>Discover and verify exhaustive coverage inside the declared scope.</summary>
    ExhaustiveWithinScope = 1,

    /// <summary>Discover the scope and inspect every item matching the typed criterion.</summary>
    InspectMatchesWithinScope = 2,
}

/// <summary>Effects that UniAgent can explicitly forbid for the bounded strategy.</summary>
public enum StrategyProhibitedEffect
{
    /// <summary>No state-changing interaction is permitted.</summary>
    StateMutation = 1,

    /// <summary>No traversal beyond the declared semantic scope is permitted.</summary>
    ExternalBoundaryCrossing = 2,
}

/// <summary>Evidence requirement declared by the strategy; never a completion fact.</summary>
public enum StrategyCompletionKind
{
    /// <summary>Agent evidence must prove exhaustive coverage within the scope.</summary>
    ExhaustiveCoverageWithinScope = 1,

    /// <summary>Agent evidence must prove every discovered typed match was inspected.</summary>
    AllDiscoveredMatchesInspected = 2,
}

/// <summary>Runtime-local adaptation classes that UniAgent may permit.</summary>
public enum StrategyAdaptationKind
{
    /// <summary>Reconcile the current hypothesis against fresh WorldBelief.</summary>
    ReconcileBelief = 1,

    /// <summary>Re-ground a semantic target without changing the strategy boundary.</summary>
    RegroundSemanticTarget = 2,

    /// <summary>Reorder already-authorized pending work inside the declared scope.</summary>
    ReorderPendingWork = 3,

    /// <summary>Revise the runtime-local execution hypothesis.</summary>
    ReviseExecutionHypothesis = 4,
}

/// <summary>
/// Stable typed reference to a composition-provided semantic capability. It carries
/// identity and version only: never code, a delegate, a selector, or user prose.
/// </summary>
public sealed record SemanticCriterionRef
{
    /// <summary>Create one typed semantic criterion reference.</summary>
    public SemanticCriterionRef(string capabilityId, string criterionId, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(criterionId);
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));

        CapabilityId = capabilityId;
        CriterionId = criterionId;
        Version = version;
    }

    /// <summary>Composition-owned capability identity.</summary>
    public string CapabilityId { get; }

    /// <summary>Criterion identity interpreted only by the matching capability.</summary>
    public string CriterionId { get; }

    /// <summary>Required capability contract version.</summary>
    public int Version { get; }
}

/// <summary>Typed objective authored by UniAgent; RuntimeAgent never creates it.</summary>
public sealed record StrategyObjective
{
    /// <summary>Create a typed objective.</summary>
    public StrategyObjective(StrategyObjectiveKind kind, SemanticCriterionRef? criterion = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        Kind = kind;
        Criterion = criterion;
    }

    /// <summary>Closed objective kind.</summary>
    public StrategyObjectiveKind Kind { get; }

    /// <summary>Typed criterion required only by criterion-directed objectives.</summary>
    public SemanticCriterionRef? Criterion { get; }
}

/// <summary>Finite application and semantic boundary for one strategy.</summary>
public sealed record StrategyScope
{
    /// <summary>Create a finite strategy scope.</summary>
    public StrategyScope(string applicationIdentity, string semanticRoot, int maximumDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticRoot);
        if (maximumDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));

        ApplicationIdentity = applicationIdentity;
        SemanticRoot = semanticRoot;
        MaximumDepth = maximumDepth;
    }

    /// <summary>Declared application identity.</summary>
    public string ApplicationIdentity { get; }

    /// <summary>Declared semantic root.</summary>
    public string SemanticRoot { get; }

    /// <summary>Finite semantic traversal depth.</summary>
    public int MaximumDepth { get; }
}

/// <summary>Immutable safety and interaction boundary authored by UniAgent.</summary>
public sealed record StrategyConstraintSet
{
    /// <summary>Create a closed constraint set.</summary>
    public StrategyConstraintSet(
        ImmutableHashSet<TypeLevelElementCategory> allowedInteractionCategories,
        ImmutableHashSet<StrategyProhibitedEffect> prohibitedEffects)
    {
        ArgumentNullException.ThrowIfNull(allowedInteractionCategories);
        ArgumentNullException.ThrowIfNull(prohibitedEffects);
        if (allowedInteractionCategories.IsEmpty)
            throw new ArgumentException("At least one allowed interaction category is required.", nameof(allowedInteractionCategories));
        if (allowedInteractionCategories.Any(category => !Enum.IsDefined(category)))
            throw new ArgumentOutOfRangeException(nameof(allowedInteractionCategories));
        if (prohibitedEffects.Any(effect => !Enum.IsDefined(effect)))
            throw new ArgumentOutOfRangeException(nameof(prohibitedEffects));

        AllowedInteractionCategories = allowedInteractionCategories.ToImmutableHashSet();
        ProhibitedEffects = prohibitedEffects.ToImmutableHashSet();
    }

    /// <summary>Interaction categories Agent may later consider for authorization.</summary>
    public ImmutableHashSet<TypeLevelElementCategory> AllowedInteractionCategories { get; }

    /// <summary>Explicitly forbidden effect classes.</summary>
    public ImmutableHashSet<StrategyProhibitedEffect> ProhibitedEffects { get; }
}

/// <summary>Strategy completion evidence requirement; it cannot assert satisfaction.</summary>
public sealed record StrategyCompletionCriteria
{
    /// <summary>Create a typed completion evidence requirement.</summary>
    public StrategyCompletionCriteria(StrategyCompletionKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
    }

    /// <summary>Required evidence semantics.</summary>
    public StrategyCompletionKind Kind { get; }
}

/// <summary>Closed set of runtime-local adaptation permissions.</summary>
public sealed record StrategyAdaptationBoundary
{
    /// <summary>Create an immutable adaptation boundary.</summary>
    public StrategyAdaptationBoundary(ImmutableHashSet<StrategyAdaptationKind> allowedAdaptations)
    {
        ArgumentNullException.ThrowIfNull(allowedAdaptations);
        if (allowedAdaptations.Any(adaptation => !Enum.IsDefined(adaptation)))
            throw new ArgumentOutOfRangeException(nameof(allowedAdaptations));
        AllowedAdaptations = allowedAdaptations.ToImmutableHashSet();
    }

    /// <summary>Runtime-local operations permitted by UniAgent.</summary>
    public ImmutableHashSet<StrategyAdaptationKind> AllowedAdaptations { get; }

    /// <summary>Whether one runtime-local adaptation class is permitted.</summary>
    public bool Allows(StrategyAdaptationKind adaptation) => AllowedAdaptations.Contains(adaptation);
}

/// <summary>
/// Immutable, typed, UniAgent-authored bounded strategy. It has no route, action,
/// selector, executable callback, mutable plan, or completion fact.
/// </summary>
public sealed record StrategyDirective
{
    /// <summary>Create one bounded strategy contract.</summary>
    public StrategyDirective(
        string strategyId,
        int contractVersion,
        StrategyObjective objective,
        StrategyScope scope,
        ExplorationIntent exploration,
        StrategyConstraintSet constraints,
        StrategyCompletionCriteria completion,
        StrategyAdaptationBoundary adaptation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(adaptation);
        if (!Enum.IsDefined(exploration))
            throw new ArgumentOutOfRangeException(nameof(exploration));

        StrategyId = strategyId;
        ContractVersion = contractVersion;
        Objective = objective;
        Scope = scope;
        Exploration = exploration;
        Constraints = constraints;
        Completion = completion;
        Adaptation = adaptation;
    }

    /// <summary>UniAgent-owned idempotency identity.</summary>
    public string StrategyId { get; }

    /// <summary>Strategy Contract version requested by UniAgent.</summary>
    public int ContractVersion { get; }

    /// <summary>Immutable typed objective.</summary>
    public StrategyObjective Objective { get; }

    /// <summary>Immutable finite semantic scope.</summary>
    public StrategyScope Scope { get; }

    /// <summary>Immutable abstract exploration approach.</summary>
    public ExplorationIntent Exploration { get; }

    /// <summary>Immutable safety and interaction constraints.</summary>
    public StrategyConstraintSet Constraints { get; }

    /// <summary>Immutable GoalEvidence requirement.</summary>
    public StrategyCompletionCriteria Completion { get; }

    /// <summary>Immutable runtime-local adaptation permissions.</summary>
    public StrategyAdaptationBoundary Adaptation { get; }
}
