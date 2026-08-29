using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// Round-level summary of the binding-created dispatch policy (category →
/// handling map; spec "PlanDelta contract": dispatch policy is one of the eight
/// freedoms). <c>StrategyDirective</c> itself does NOT carry dispatch policy —
/// the binding creates one per strategy — so a DispatchPolicy PlanDelta is
/// declared and validated against the round's previous/next summaries instead
/// (a DispatchPolicy change requires BOTH summaries present AND different).
/// Equality is CONTENT equality over the category→handling pairs, never
/// dictionary reference equality, so a summary built from an equal policy always
/// compares equal. Validation artifact only; never a Runtime input.
/// </summary>
public sealed record DispatchPolicySummary
{
    /// <summary>Create one immutable dispatch-policy summary (≥1 category mapping).</summary>
    public DispatchPolicySummary(ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> categoryHandling)
    {
        ArgumentNullException.ThrowIfNull(categoryHandling);
        if (categoryHandling.IsEmpty)
            throw new ArgumentException("A dispatch policy summary must carry at least one category mapping.", nameof(categoryHandling));
        if (categoryHandling.Values.Any(handling => !Enum.IsDefined(handling)))
            throw new ArgumentOutOfRangeException(nameof(categoryHandling));

        CategoryHandling = categoryHandling;
    }

    /// <summary>Category → handling mapping summarized for this round.</summary>
    public ImmutableDictionary<TypeLevelElementCategory, TypeLevelHandling> CategoryHandling { get; }

    /// <summary>Content equality over the category→handling pairs.</summary>
    public bool Equals(DispatchPolicySummary? other)
        => other is not null
            && CategoryHandling.Count == other.CategoryHandling.Count
            && CategoryHandling.All(pair =>
                other.CategoryHandling.TryGetValue(pair.Key, out var handling) && handling == pair.Value);

    /// <summary>Content-based hash over the sorted category→handling pairs.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var pair in CategoryHandling.OrderBy(pair => (int)pair.Key))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }
}