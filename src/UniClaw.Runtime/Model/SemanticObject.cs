using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Immutable declarative domain concept — NOT mutable runtime state.
///
/// A SemanticObject describes WHAT exists in the UI domain, independent of any
/// specific Observation. It is a domain concept (like "Wi‑Fi Switch" or
/// specific Observation. It is a domain concept, not a UI element
/// (no Bounds, Index, Text, Page).
///
/// SemanticObject is NOT duplicated per Container. Container will later own
/// the mutable BINDING state (which ObservedElement currently instantiates
/// which SemanticObject) — this type is only the declarative definition.
///
/// StateDimensions declare what state MAY be observed (e.g., "Enabled") —
/// they do NOT declare current state values. Current state belongs to the
/// belief/binding layers (future phases).
///
/// Freeze: SemanticObject ≠ ObservedElement. Domain identity ≠ UI location.
/// </summary>
/// <param name="Identity">Unique domain identity.</param>
/// <param name="Category">Domain category.</param>
/// <param name="StateDimensions">Observable state dimensions. Empty if none.</param>
public sealed record SemanticObject(string Identity, string Category, ImmutableArray<string> StateDimensions)
{
    /// <summary>Creates a validated SemanticObject.</summary>
    public static SemanticObject Define(string identity, string category, ImmutableArray<string>? stateDimensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return new SemanticObject(identity, category, stateDimensions ?? []);
    }

    /// <summary>Convenience: creates a SemanticObject with no state dimensions.</summary>
    public static SemanticObject Define(string identity, string category)
        => Define(identity, category, null);
}
