using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Input to <see cref="IVectorSemanticIndex"/>. Contains only perception-level
/// features: visible element summary, element types, text fragments, and
/// structural features. It never contains Goal, Action, Expected State, or
/// Planner Context.
/// </summary>
public sealed record ContainerSemanticQuery
{
    /// <summary>The visible elements observed in the current frame.</summary>
    public ImmutableArray<ObservedElement> VisibleElements { get; }

    /// <summary>Element type labels (e.g. menu_item, toggle).</summary>
    public ImmutableArray<string> ElementTypes { get; }

    /// <summary>Non-empty text fragments from visible elements.</summary>
    public ImmutableArray<string> TextFragments { get; }

    /// <summary>Structural features (e.g. type:/switch: markers).</summary>
    public ImmutableArray<string> StructuralFeatures { get; }

    /// <summary>Creates a container semantic query.</summary>
    public ContainerSemanticQuery(
        ImmutableArray<ObservedElement> visibleElements,
        ImmutableArray<string>? elementTypes = null,
        ImmutableArray<string>? textFragments = null,
        ImmutableArray<string>? structuralFeatures = null)
    {
        VisibleElements = visibleElements;
        ElementTypes = elementTypes ?? ImmutableArray<string>.Empty;
        TextFragments = textFragments ?? ImmutableArray<string>.Empty;
        StructuralFeatures = structuralFeatures ?? ImmutableArray<string>.Empty;
    }
}
