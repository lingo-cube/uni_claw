using System.Collections.Immutable;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

/// <summary>Immutable result of one external semantic interpretation attempt.</summary>
public sealed record SemanticCapabilityEvaluationBatch
{
    /// <summary>Evidence admitted by the Runtime boundary.</summary>
    public ImmutableArray<SemanticEvidenceV2Envelope> Accepted { get; }
    /// <summary>Evidence rejected by the Runtime boundary.</summary>
    public ImmutableArray<SemanticEvidenceV2Envelope> Rejected { get; }
    /// <summary>Admission failure reasons aligned with rejected evidence.</summary>
    public ImmutableArray<SemanticEvidenceAdmissionFailure> RejectionReasons { get; }
    /// <summary>Whether a capability was configured for this evaluation.</summary>
    public bool IsCapabilityAvailable { get; }

    internal SemanticCapabilityEvaluationBatch(
        IEnumerable<SemanticEvidenceV2Envelope> accepted,
        IEnumerable<SemanticEvidenceV2Envelope> rejected,
        IEnumerable<SemanticEvidenceAdmissionFailure> reasons,
        bool isCapabilityAvailable)
    {
        Accepted = accepted.ToImmutableArray();
        Rejected = rejected.ToImmutableArray();
        RejectionReasons = reasons.ToImmutableArray();
        IsCapabilityAvailable = isCapabilityAvailable;
    }

    /// <summary>Primary evidence suitable as a later authorization input.</summary>
    public ImmutableArray<SemanticEvidenceV2Envelope> EligibleForAuthorizationInput =>
        Accepted.Where(e => e.Provenance.Tier == SemanticSourceTier.Primary).ToImmutableArray();

    /// <summary>Auxiliary evidence retained only for corroboration.</summary>
    public ImmutableArray<SemanticEvidenceV2Envelope> AuxiliaryCorroboration =>
        Accepted.Where(e => e.Provenance.Tier == SemanticSourceTier.Auxiliary).ToImmutableArray();
}

/// <summary>
/// Runtime-owned adapter for an optional external semantic capability.
/// It has no action, lifecycle, or Agent callback surface.
/// </summary>
public sealed class SemanticCapabilityRuntime
{
    private readonly IExternalSemanticCapability? _capability;

    /// <summary>Creates a Runtime consumer for an optional external capability.</summary>
    public SemanticCapabilityRuntime(IExternalSemanticCapability? capability = null) => _capability = capability;

    /// <summary>Interprets and fail-closed admits one immutable observation context.</summary>
    public async ValueTask<SemanticCapabilityEvaluationBatch> EvaluateAsync(
        ExternalSemanticCapabilityContext capabilityContext,
        SemanticObservationReference currentObservation,
        IEnumerable<SemanticSourceMetadata> sources,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capabilityContext);
        ArgumentNullException.ThrowIfNull(currentObservation);
        ArgumentNullException.ThrowIfNull(sources);
        cancellationToken.ThrowIfCancellationRequested();

        if (_capability is null)
            return Empty(false);

        var sourceArray = sources.ToImmutableArray();
        var admission = new SemanticEvidenceAdmissionContext(
            _capability.Manifest, currentObservation, now, SemanticSourceTier.Auxiliary, sourceArray, capabilityContext.Facts);

        ImmutableArray<SemanticEvidenceV2Envelope> interpreted;
        try
        {
            interpreted = await _capability.InterpretAsync(capabilityContext, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Empty(true, SemanticEvidenceAdmissionFailure.InvalidCandidate);
        }

        var accepted = ImmutableArray.CreateBuilder<SemanticEvidenceV2Envelope>();
        var rejected = ImmutableArray.CreateBuilder<SemanticEvidenceV2Envelope>();
        var reasons = ImmutableArray.CreateBuilder<SemanticEvidenceAdmissionFailure>();
        foreach (var evidence in interpreted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsKnownCandidate(evidence))
            {
                rejected.Add(evidence);
                reasons.Add(SemanticEvidenceAdmissionFailure.InvalidCandidate);
                continue;
            }

            var result = SemanticEvidenceV2Admission.Admit(evidence, admission);
            if (result.Accepted) accepted.Add(evidence);
            else
            {
                rejected.Add(evidence);
                reasons.Add(result.Failure ?? SemanticEvidenceAdmissionFailure.InvalidCandidate);
            }
        }

        return new SemanticCapabilityEvaluationBatch(accepted, rejected, reasons, true);
    }

    private static bool IsKnownCandidate(SemanticEvidenceV2Envelope evidence) =>
        evidence?.Candidate is ContainerIdentityCandidateEvidence or
            ElementAffordanceCandidateEvidence or ContainerRelationCandidateEvidence;

    private static SemanticCapabilityEvaluationBatch Empty(
        bool available, SemanticEvidenceAdmissionFailure? reason = null) =>
        new(
            ImmutableArray<SemanticEvidenceV2Envelope>.Empty,
            ImmutableArray<SemanticEvidenceV2Envelope>.Empty,
            reason is null ? ImmutableArray<SemanticEvidenceAdmissionFailure>.Empty : ImmutableArray.Create(reason.Value),
            available);
}
