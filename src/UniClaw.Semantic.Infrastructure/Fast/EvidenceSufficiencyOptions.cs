using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Evidence Sufficiency options (SEMANTIC_SAFETY_HARDENING_APPLY, mechanism B).
///
/// Purpose: distinguish GENERIC evidence (text / toggle / button / settings /
/// system words, bare element-type labels) from IDENTITY-DISCRIMINATIVE evidence
/// (exclusive per-identity anchors + device-state structural signals). A claim
/// requires sufficient discriminative evidence; otherwise ABSTAIN.
///
/// Everything is versioned / configurable / profile-bound; no magic numbers in
/// code, no case-id special cases. Generic tokens and per-identity anchors are
/// IDENTITY SEMANTIC KNOWLEDGE (derived from the tuning corpora and captured
/// real-trace vocabulary — they describe Container Identity, not failure cases).
/// </summary>
public sealed record EvidenceSufficiencyOptions
{
    /// <summary>Whether evidence sufficiency is applied.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Minimum total evidence score (text + structural + anchors weighted).</summary>
    public int MinEvidenceScore { get; init; } = 2;

    /// <summary>Minimum number of non-generic text fragments (generic words alone are insufficient).</summary>
    public int MinNonGenericText { get; init; } = 1;

    /// <summary>Minimum total discriminative signal (exclusive anchors + switch signals).</summary>
    public int MinDiscriminativeSignal { get; init; } = 1;

    /// <summary>When true, an observation with ZERO text fragments is always insufficient (near-empty).</summary>
    public bool RequireTextEvidence { get; init; } = true;

    /// <summary>Tokens treated as GENERIC_UI_SIMILARITY (not identity proof).</summary>
    public ImmutableHashSet<string> GenericTokens { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// Exclusive identity anchors: exact observed text fragment → identity.
    /// Fragments here are identity-discriminative for that container.
    /// </summary>
    public IReadOnlyDictionary<string, ImmutableHashSet<string>> PerIdentityAnchors { get; init; }
        = new Dictionary<string, ImmutableHashSet<string>>();
}