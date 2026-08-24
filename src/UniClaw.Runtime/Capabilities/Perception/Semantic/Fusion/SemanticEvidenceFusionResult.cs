using System.Collections.Immutable;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;

/// <summary>Stable reason codes for a rejected SemanticEvidence value.</summary>
public static class SemanticEvidenceRejectionReason
{
    /// <summary>The evidence observation sequence does not match the current observation.</summary>
    public const string StaleObservationSequence = "STALE_OBSERVATION_SEQUENCE";

    /// <summary>The evidence has expired (ValidUntil passed).</summary>
    public const string StaleExpired = "STALE_EXPIRED";

    /// <summary>The evidence scope is invalid.</summary>
    public const string InvalidScope = "INVALID_SCOPE";

    /// <summary>A referenced Observation / Trace id does not exist in the admission context.</summary>
    public const string MissingReference = "MISSING_REFERENCE";

    /// <summary>The evidence carries an unsupported reference kind (e.g. Fact, which Semantic cannot create).</summary>
    public const string UnsupportedReferenceKind = "UNSUPPORTED_REFERENCE_KIND";

    /// <summary>The evidence failed structural compatibility validation.</summary>
    public const string Incompatible = "INCOMPATIBLE";
}

/// <summary>
/// A rejection of one SemanticEvidence value with a stable reason code.
/// A rejection is a validation outcome, not a belief/action decision.
/// </summary>
public sealed record SemanticEvidenceRejection
{
    /// <summary>The rejected evidence id.</summary>
    public string EvidenceId { get; }

    /// <summary>Stable rejection reason (see <see cref="SemanticEvidenceRejectionReason"/>).</summary>
    public string Reason { get; }

    /// <summary>Optional human/audit-readable detail.</summary>
    public string? Details { get; }

    /// <summary>Creates a rejection.</summary>
    public SemanticEvidenceRejection(string evidenceId, string reason, string? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EvidenceId = evidenceId;
        Reason = reason;
        Details = details;
    }
}

/// <summary>
/// The confidence weight of one accepted SemanticEvidence value. This is an
/// Evidence Weight, NEVER Truth (falsifier F4). Runtime Belief decides whether to
/// form a Belief; this type only carries the weight.
/// </summary>
public sealed record SemanticEvidenceWeight
{
    /// <summary>The accepted evidence id.</summary>
    public string EvidenceId { get; }

    /// <summary>The confidence expressed as a weight, in [0,1].</summary>
    public double Weight { get; }

    /// <summary>Creates an evidence weight.</summary>
    public SemanticEvidenceWeight(string evidenceId, double weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId);
        if (weight is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weight), weight, "Weight must be within [0, 1].");
        }
        EvidenceId = evidenceId;
        Weight = weight;
    }
}

/// <summary>
/// Output of Runtime Evidence Fusion. It contains only validated evidence and
/// weights — NEVER an Action, Goal decision, Plan, or World mutation. Fact /
/// Belief Update is owned by the Runtime belief system, not by this result.
/// </summary>
public sealed record ValidatedSemanticEvidenceResult
{
    /// <summary>Evidence accepted by the validation pipeline.</summary>
    public ImmutableArray<SemanticEvidence> AcceptedEvidence { get; }

    /// <summary>Evidence rejected by the validation pipeline.</summary>
    public ImmutableArray<SemanticEvidence> RejectedEvidence { get; }

    /// <summary>Rejection reasons aligned with <see cref="RejectedEvidence"/> admission failures.</summary>
    public ImmutableArray<SemanticEvidenceRejection> ValidationReasons { get; }

    /// <summary>Confidence weights for the accepted evidence.</summary>
    public ImmutableArray<SemanticEvidenceWeight> ConfidenceWeights { get; }

    /// <summary>Creates a validated fusion result.</summary>
    public ValidatedSemanticEvidenceResult(
        ImmutableArray<SemanticEvidence> acceptedEvidence,
        ImmutableArray<SemanticEvidence> rejectedEvidence,
        ImmutableArray<SemanticEvidenceRejection> validationReasons,
        ImmutableArray<SemanticEvidenceWeight> confidenceWeights)
    {
        AcceptedEvidence = acceptedEvidence;
        RejectedEvidence = rejectedEvidence;
        ValidationReasons = validationReasons;
        ConfidenceWeights = confidenceWeights;
    }

    /// <summary>An empty fusion result (no evidence in, no evidence out).</summary>
    public static ValidatedSemanticEvidenceResult Empty { get; } =
        new(
            ImmutableArray<SemanticEvidence>.Empty,
            ImmutableArray<SemanticEvidence>.Empty,
            ImmutableArray<SemanticEvidenceRejection>.Empty,
            ImmutableArray<SemanticEvidenceWeight>.Empty);
}
