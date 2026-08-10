using System.Collections.Immutable;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.Model;

/// <summary>Caller-authorized handling behavior for a discovered element category.</summary>
public enum TypeLevelHandling
{
    /// <summary>Enter the container and traverse its subtree.</summary>
    EnterAndTraverse = 1,
    /// <summary>Inspect the element without state change.</summary>
    Inspect = 2,
    /// <summary>Set the element to the desired state.</summary>
    SetDesiredState = 3,
    /// <summary>Interaction with this category is forbidden.</summary>
    Forbidden = 4,
}

/// <summary>
/// Immutable caller-authorized mapping from element category to handling behavior.
/// The Agent matches discovered element categories against this policy at runtime.
/// </summary>
public sealed record TypeLevelDispatchPolicy
{
    /// <summary>Creates a validated dispatch policy.</summary>
    public TypeLevelDispatchPolicy(ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> categoryHandling)
    {
        ArgumentNullException.ThrowIfNull(categoryHandling);
        if (categoryHandling.IsEmpty)
            throw new ArgumentException("Dispatch policy must contain at least one category mapping.", nameof(categoryHandling));
        if (categoryHandling.Values.Any(h => !Enum.IsDefined(h)))
            throw new ArgumentOutOfRangeException(nameof(categoryHandling));
        CategoryHandling = categoryHandling;
    }

    /// <summary>Category → handling mapping declared by the caller.</summary>
    public ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> CategoryHandling { get; }

    /// <summary>Resolves the authorized handling for a category, or null if the category is not in the policy.</summary>
    public TypeLevelHandling? Resolve(TypeLevelElementCategory category)
        => CategoryHandling.TryGetValue(category, out var handling) ? handling : null;
}
