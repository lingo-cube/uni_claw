namespace UniClaw.Runtime.Model;

/// <summary>
/// Three-way equivalence between two navigation source occurrences within the
/// same Container exploration.
/// </summary>
public enum SourceEquivalenceKind
{
    /// <summary>Two occurrences were proven to represent the same source.</summary>
    SameSource,
    /// <summary>Two occurrences were proven to represent different sources.</summary>
    DifferentSource,
    /// <summary>Available evidence could not determine equivalence.</summary>
    Unknown,
}

/// <summary>
/// Agent-run-local immutable evidence that two occurrences are proven same,
/// different, or unresolved as one logical navigation source.
/// </summary>
/// <param name="FirstOccurrenceIdentity">First occurrence identity.</param>
/// <param name="SecondOccurrenceIdentity">Second occurrence identity.</param>
/// <param name="Kind">Equivalence result.</param>
/// <param name="Reason">Deterministic reason.</param>
public sealed record SourceEquivalenceEvidence(
    string FirstOccurrenceIdentity,
    string SecondOccurrenceIdentity,
    SourceEquivalenceKind Kind,
    string Reason);
