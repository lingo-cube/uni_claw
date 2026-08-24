using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;

/// <summary>
/// Minimal wiring seam: an ISemanticProvider (default NoOpSemanticProvider)
/// resolves SemanticEvidence, then feeds it into ISemanticEvidenceFusion.
/// This is NOT wired into Agent decision logic; it is a standalone construction
/// helper so Runtime may create a provider and route evidence into fusion.
/// It never produces Action / Goal / Plan / World mutation and never creates
/// Fact — the Runtime belief system owns Fact / Belief Update.
/// </summary>
public sealed class SemanticEvidenceFusionPipeline
{
    private readonly ISemanticProvider _provider;
    private readonly ISemanticEvidenceFusion _fusion;

    /// <summary>Creates the pipeline with a provider (default NoOp) and fusion (default).</summary>
    public SemanticEvidenceFusionPipeline(
        ISemanticProvider? provider = null,
        ISemanticEvidenceFusion? fusion = null)
    {
        _provider = provider ?? new NoOpSemanticProvider();
        _fusion = fusion ?? new SemanticEvidenceFusion();
    }

    /// <summary>Resolves provider evidence and fuses it with the given input.</summary>
    public async Task<ValidatedSemanticEvidenceResult> ResolveAndFuseAsync(
        SemanticEvidenceFusionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var context = new ObservationContext(
            input.CurrentObservation,
            input.ExistingBelief?.SemanticPage);

        var providerEvidence = await _provider.ResolveAsync(context, cancellationToken);

        var enriched = new SemanticEvidenceFusionInput(
            currentObservation: input.CurrentObservation,
            visionEvidence: input.VisionEvidence,
            semanticEvidence: input.SemanticEvidence.AddRange(providerEvidence),
            containerHistory: input.ContainerHistory,
            existingBelief: input.ExistingBelief,
            knownObservationSequences: input.KnownObservationSequences,
            knownTraceIds: input.KnownTraceIds);

        return _fusion.Fuse(enriched);
    }
}
