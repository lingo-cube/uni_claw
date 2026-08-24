using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

/// <summary>
/// Optional observation enrichment decorator. The external capability receives
/// typed observation facts only; it never receives the inner environment or an action.
/// </summary>
public sealed class SemanticCapabilityEnvironment : IEnvironment
{
    private readonly IEnvironment _inner;
    private readonly SemanticCapabilityRuntime _runtime;
    private readonly Func<Observation, ExternalSemanticCapabilityContext> _project;
    private readonly Func<DateTimeOffset> _clock;

    /// <param name="inner">Raw environment to decorate.</param>
    /// <param name="runtime">Runtime-owned evidence admission consumer.</param>
    /// <param name="projector">Pure observation-to-context projector.</param>
    /// <param name="clock">Clock used for evidence freshness evaluation.</param>
    public SemanticCapabilityEnvironment(
        IEnvironment inner,
        SemanticCapabilityRuntime runtime,
        Func<Observation, ExternalSemanticCapabilityContext>? projector = null,
        Func<DateTimeOffset>? clock = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _project = projector ?? (observation => SemanticObservationFactProjector.Project(observation));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Observes raw state and optionally returns an enriched immutable copy.</summary>
    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        var raw = await _inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var context = _project(raw);
            if (context.Observation.Sequence != raw.SequenceNumber)
                throw new InvalidOperationException("Projected context is stale for the raw observation.");
            var sources = raw.Sources.Select(source => new SemanticSourceMetadata(
                source.SourceId,
                source.Tier == ObservationSourceTier.PrimaryVision ? SemanticSourceTier.Primary : SemanticSourceTier.Auxiliary,
                source.Available,
                source.FrameReference)).ToArray();
            if (sources.Length == 0)
                sources = context.Sources.ToArray();
            else if (sources.Any(source => !context.Sources.Any(candidate =>
                         string.Equals(candidate.SourceId, source.SourceId, StringComparison.Ordinal) &&
                         candidate.Tier == source.Tier && candidate.Available == source.Available &&
                         string.Equals(candidate.FrameId, source.FrameId, StringComparison.Ordinal))))
                throw new InvalidOperationException("Projected context sources do not match the raw observation.");
            var current = context.Observation;
            var batch = await _runtime.EvaluateAsync(context, current, sources, _clock(), cancellationToken)
                .ConfigureAwait(false);
            return raw with
            {
                AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Semantic enrichment is optional and cannot make valid raw observation unavailable.
            return raw with { AdmittedSemanticEvidence = AdmittedSemanticEvidenceSnapshot.Empty };
        }
    }

    /// <summary>Delegates the action unchanged to the inner environment.</summary>
    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken) =>
        _inner.ExecuteAsync(action, cancellationToken);
}
