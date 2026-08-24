using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Semantic.Settings;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// Scenario-only adapter for deterministic Settings fixtures. Fixtures provide
/// primary visual elements explicitly; structured facts are optional
/// corroboration and are never promoted to primary.
/// </summary>
public sealed class SettingsSemanticCapabilityTestEnvironment : IEnvironment
{
    private readonly SemanticCapabilityEnvironment _inner;

    public SettingsSemanticCapabilityTestEnvironment(IEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _inner = new SemanticCapabilityEnvironment(
            new SourceStampingEnvironment(environment),
            new SemanticCapabilityRuntime(new SettingsSemanticCapability()),
            Project);
    }

    public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
        _inner.ObserveAsync(cancellationToken);

    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken) =>
        _inner.ExecuteAsync(action, cancellationToken);

    private static ExternalSemanticCapabilityContext Project(Observation observation)
        => SemanticObservationFactProjector.Project(observation);

    private sealed class SourceStampingEnvironment(IEnvironment inner) : IEnvironment
    {
        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var raw = await inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
            var frame = $"fixture-frame-{raw.SequenceNumber}";
            return raw with { Sources = ImmutableArray.Create(
                new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, raw.SequenceNumber,
                    frame, 1080, 1920, "fixture-vision", "fixture-vision"),
                new ObservationSourceMetadata(ObservationSourceTier.AuxiliaryStructured,
                    !raw.StructuredElements.IsDefaultOrEmpty, raw.SequenceNumber, frame,
                    1080, 1920, "fixture-structured", "fixture-structured")) };
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(action, cancellationToken);
    }
}
