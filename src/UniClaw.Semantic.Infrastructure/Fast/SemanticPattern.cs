using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Legacy canonical semantic pattern (prototype representation seed).
/// Retained for compatibility with the reference/test matcher path and the
/// held-out benchmark tooling; the prototype OWNER is
/// <see cref="IContainerIdentityPrototypeStore"/>.
/// </summary>
public sealed record SemanticPattern(
    string IdentityCandidate,
    string PatternReference,
    ImmutableArray<string> TextFragments,
    ImmutableArray<string> ElementTypes,
    ImmutableArray<string> StructuralFeatures);