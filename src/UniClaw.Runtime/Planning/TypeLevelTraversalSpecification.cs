using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Immutable caller-side description of an open-world traversal boundary.
/// It is neither a concrete <c>Plan</c> nor a future route, observation, or completion receipt.
/// </summary>
public sealed record TypeLevelTraversalSpecification
{
    /// <summary>Creates a validated type-level traversal specification.</summary>
    public TypeLevelTraversalSpecification(
        TypeLevelTaskScope scope,
        ImmutableHashSet<TypeLevelElementCategory> targetCategories,
        int maximumDepth,
        TypeLevelSafetyBoundary safety,
        TypeLevelCompletionRequirement completion,
        TypeLevelEntryBoundary entry,
        TypeLevelDispatchPolicy? dispatchPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(targetCategories);
        ArgumentNullException.ThrowIfNull(safety);
        ArgumentNullException.ThrowIfNull(entry);

        if (targetCategories.IsEmpty)
            throw new ArgumentException("Target categories must not be empty.", nameof(targetCategories));
        if (targetCategories.Any(category => !Enum.IsDefined(category)))
            throw new ArgumentOutOfRangeException(nameof(targetCategories));
        if (maximumDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        if (completion != TypeLevelCompletionRequirement.ExhaustiveWithinScope)
            throw new ArgumentOutOfRangeException(nameof(completion));

        Scope = scope;
        TargetCategories = new TypeLevelSafetyBoundary(targetCategories).AllowedInteractionCategories;
        MaximumDepth = maximumDepth;
        Safety = safety;
        Completion = completion;
        Entry = entry;
        DispatchPolicy = dispatchPolicy;
    }

    /// <summary>Declared task scope boundary.</summary>
    public TypeLevelTaskScope Scope { get; }
    /// <summary>Declared categories that may be targeted during later traversal.</summary>
    public ImmutableHashSet<TypeLevelElementCategory> TargetCategories { get; }
    /// <summary>Declared upper bound on semantic traversal depth.</summary>
    public int MaximumDepth { get; }
    /// <summary>Declared forbidden-interaction boundary.</summary>
    public TypeLevelSafetyBoundary Safety { get; }
    /// <summary>Declared caller completion requirement.</summary>
    public TypeLevelCompletionRequirement Completion { get; }
    /// <summary>Declared application and semantic entry boundary.</summary>
    public TypeLevelEntryBoundary Entry { get; }
    /// <summary>Optional caller-authorized category→handling dispatch policy; absent = navigation-only (Tap).</summary>
    public TypeLevelDispatchPolicy? DispatchPolicy { get; }
}

/// <summary>Caller-declared application and semantic root; it is not discovered work inventory.</summary>
public sealed record TypeLevelTaskScope
{
    /// <summary>Creates a validated task scope.</summary>
    public TypeLevelTaskScope(string applicationIdentity, string semanticRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticRoot);
        ApplicationIdentity = applicationIdentity;
        SemanticRoot = semanticRoot;
    }

    /// <summary>Application identity declared by the caller.</summary>
    public string ApplicationIdentity { get; }
    /// <summary>Semantic root declared by the caller.</summary>
    public string SemanticRoot { get; }
}

/// <summary>Bounded element-category vocabulary for the type-level traversal scenario.</summary>
public enum TypeLevelElementCategory
{
    /// <summary>A navigable semantic container.</summary>
    NavigableContainer = 1,
    /// <summary>A semantic control capable of changing state.</summary>
    StateChangingControl = 2,
}

/// <summary>Caller-declared interaction boundary; it preserves no authority to perform an interaction.</summary>
public sealed record TypeLevelSafetyBoundary
{
    private static readonly ImmutableHashSet<TypeLevelElementCategory> NavigableContainers =
        ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer);
    private static readonly ImmutableHashSet<TypeLevelElementCategory> StateChangingControls =
        ImmutableHashSet.Create(TypeLevelElementCategory.StateChangingControl);
    private static readonly ImmutableHashSet<TypeLevelElementCategory> AllElementCategories =
        ImmutableHashSet.Create(
            TypeLevelElementCategory.NavigableContainer,
            TypeLevelElementCategory.StateChangingControl);

    /// <summary>Creates a validated allowed-interaction category boundary.</summary>
    public TypeLevelSafetyBoundary(ImmutableHashSet<TypeLevelElementCategory> allowedInteractionCategories)
    {
        ArgumentNullException.ThrowIfNull(allowedInteractionCategories);

        if (allowedInteractionCategories.IsEmpty)
        {
            throw new ArgumentException("Allowed interaction categories must not be empty.", nameof(allowedInteractionCategories));
        }

        if (allowedInteractionCategories.Any(category => !Enum.IsDefined(category)))
        {
            throw new ArgumentOutOfRangeException(nameof(allowedInteractionCategories));
        }

        AllowedInteractionCategories = allowedInteractionCategories.Count == 1
            ? allowedInteractionCategories.Contains(TypeLevelElementCategory.NavigableContainer)
                ? NavigableContainers
                : StateChangingControls
            : AllElementCategories;
    }

    /// <summary>Categories allowed by the caller's boundary.</summary>
    public ImmutableHashSet<TypeLevelElementCategory> AllowedInteractionCategories { get; }
}

/// <summary>Caller completion requirement only; it neither measures nor proves completion.</summary>
public enum TypeLevelCompletionRequirement
{
    /// <summary>Require exhaustive coverage only within the declared scope.</summary>
    ExhaustiveWithinScope = 1,
}

/// <summary>Caller-declared application and semantic starting boundary.</summary>
public sealed record TypeLevelEntryBoundary
{
    /// <summary>Creates a validated entry boundary.</summary>
    public TypeLevelEntryBoundary(string applicationIdentity, string expectedSemanticEntry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSemanticEntry);
        ApplicationIdentity = applicationIdentity;
        ExpectedSemanticEntry = expectedSemanticEntry;
    }

    /// <summary>Application identity at which traversal is expected to start.</summary>
    public string ApplicationIdentity { get; }
    /// <summary>Expected semantic starting boundary.</summary>
    public string ExpectedSemanticEntry { get; }
}

// TypeLevelHandling and TypeLevelDispatchPolicy moved to UniClaw.Runtime.Model namespace
// (src/UniClaw.Runtime/Model/TypeLevelDispatchPolicy.cs) — the Agent references them at runtime.
