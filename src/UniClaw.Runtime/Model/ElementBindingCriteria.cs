using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Caller-provided binding recognition KNOWLEDGE — NOT a binding verdict.
///
/// Provides the minimum signals needed for BindingAnalysis to produce
/// SemanticEvidence about which ObservedElements may bind to which
/// SemanticObjects.
///
/// This is KNOWLEDGE (what to look for), not VERDICT (what binds to what).
/// Follows the PageAnalysisCriteria pattern.
/// </summary>
/// <param name="KnownObjects">SemanticObjects that may be present in the observation.</param>
/// <param name="ObjectTextAnchors">Object identity → opaque text anchor to match.</param>
/// <param name="ObjectControlTypes">Object identity → PerceptionType of an associated control.</param>
public sealed record ElementBindingCriteria(
    ImmutableArray<SemanticObject> KnownObjects,
    ImmutableDictionary<string, string>? ObjectTextAnchors = null,
    ImmutableDictionary<string, string>? ObjectControlTypes = null)
{
    /// <summary>Empty criteria — no binding knowledge provided.</summary>
    public static ElementBindingCriteria Empty { get; } = new([], null, null);
}
