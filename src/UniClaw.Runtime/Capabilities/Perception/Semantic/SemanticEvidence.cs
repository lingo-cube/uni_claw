using System.Collections.Immutable;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic;

/// <summary>
/// The semantic kind a <see cref="SemanticEvidence"/> value addresses.
/// Phase 1 supports only <see cref="ContainerIdentity"/> (Container Identity
/// Recovery). Future kinds (<c>ElementMeaning</c>, <c>Relation</c>) are reserved
/// in the contract and MUST NOT be implemented in Phase 1.
/// </summary>
public enum SemanticEvidenceKind
{
    /// <summary>A container identity candidate (Phase 1).</summary>
    ContainerIdentity = 1,
}

/// <summary>
/// The observation context scope a <see cref="SemanticEvidence"/> value refers to.
/// MUST distinguish CurrentObservation / CurrentContainer / HistoricalContext.
/// </summary>
public enum SemanticEvidenceScope
{
    /// <summary>The evidence is scoped to the current observation only.</summary>
    CurrentObservation = 1,

    /// <summary>The evidence is scoped to the current container.</summary>
    CurrentContainer = 2,

    /// <summary>The evidence is scoped to historical context.</summary>
    HistoricalContext = 3,
}

/// <summary>
/// A reference from a <see cref="SemanticEvidence"/> value to another record.
/// Observation and Trace references are supported; Fact references are reserved
/// until the Runtime belief system produces Facts.
/// </summary>
public sealed record SemanticEvidenceReference
{
    /// <summary>The referenced record kind, e.g. "Observation", "Trace", "Fact".</summary>
    public string Kind { get; }

    /// <summary>Opaque reference id within the referenced record category.</summary>
    public string ReferenceId { get; }

    /// <summary>Creates a semantic evidence reference.</summary>
    public SemanticEvidenceReference(string kind, string referenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        Kind = kind;
        ReferenceId = referenceId;
    }
}

/// <summary>
/// The only output of the Semantic Perception Layer. It is EVIDENCE, never a
/// Fact and never a Decision. Semantic does NOT produce Fact; Runtime Validation
/// produces Fact / Belief Update.
/// </summary>
public sealed record SemanticEvidence
{
    /// <summary>Unique identity of this evidence value.</summary>
    public string EvidenceId { get; }

    /// <summary>Version of this evidence value (part of identity).</summary>
    public string Version { get; }

    /// <summary>Which Semantic channel produced this evidence (e.g. FAST / SLOW).</summary>
    public string Source { get; }

    /// <summary>The semantic kind this evidence addresses (Phase 1: ContainerIdentity).</summary>
    public SemanticEvidenceKind Kind { get; }

    /// <summary>The semantic candidate hypothesis. Candidate, not Fact.</summary>
    public string Candidate { get; }

    /// <summary>Confidence in the candidate, in [0,1].</summary>
    public double Confidence { get; }

    /// <summary>The observation context scope this evidence refers to.</summary>
    public SemanticEvidenceScope Scope { get; }

    /// <summary>The observation sequence this evidence is based on (freshness).</summary>
    public long ObservationSequence { get; }

    /// <summary>When this evidence was created (freshness / timestamp).</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Optional expiry of this evidence (freshness). Null = no explicit expiry.</summary>
    public DateTimeOffset? ValidUntil { get; }

    /// <summary>Optional references to Observation / Trace / (future) Fact records.</summary>
    public ImmutableArray<SemanticEvidenceReference> References { get; init; }
        = ImmutableArray<SemanticEvidenceReference>.Empty;

    /// <summary>Creates one SemanticEvidence value.</summary>
    public SemanticEvidence(
        string evidenceId,
        string version,
        string source,
        SemanticEvidenceKind kind,
        string candidate,
        double confidence,
        SemanticEvidenceScope scope,
        long observationSequence,
        DateTimeOffset createdAt,
        DateTimeOffset? validUntil = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);
        if (confidence is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence), confidence, "Confidence must be within [0, 1].");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(observationSequence);

        EvidenceId = evidenceId;
        Version = version;
        Source = source;
        Kind = kind;
        Candidate = candidate;
        Confidence = confidence;
        Scope = scope;
        ObservationSequence = observationSequence;
        CreatedAt = createdAt;
        ValidUntil = validUntil;
    }
}
