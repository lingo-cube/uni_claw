using System.Collections.Immutable;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

/// <summary>Immutable Runtime enrichment; primary evidence is the only authorization input.</summary>
public sealed record AdmittedSemanticEvidenceSnapshot
{
    /// <summary>Empty immutable enrichment.</summary>
    public static AdmittedSemanticEvidenceSnapshot Empty { get; } =
        new(ImmutableArray<SemanticEvidenceV2Envelope>.Empty);

    /// <summary>All admitted evidence, including auxiliary corroboration.</summary>
    public ImmutableArray<SemanticEvidenceV2Envelope> Evidence { get; }

    /// <summary>Admitted primary evidence eligible for later authorization input.</summary>
    public ImmutableArray<SemanticEvidenceV2Envelope> EligibleForAuthorizationInput =>
        Evidence.Where(e => e.Provenance.Tier == SemanticSourceTier.Primary).ToImmutableArray();

    /// <summary>Creates an immutable snapshot from already-admitted envelopes.</summary>
    public AdmittedSemanticEvidenceSnapshot(IEnumerable<SemanticEvidenceV2Envelope> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Evidence = evidence.ToImmutableArray();
    }
}
