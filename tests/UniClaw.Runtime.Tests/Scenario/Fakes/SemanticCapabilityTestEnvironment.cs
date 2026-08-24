using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

public enum FixtureSemanticRole { NavigationCandidate, ParentReturnControl, LocalControl, NonInteractive }

/// <summary>
/// Shared fixture decorator helpers for the Semantic Evidence Protocol V2
/// test pipeline. These emit only passive typed candidate evidence.
/// </summary>
public static class FixtureCapabilityDecorators
{
    /// <summary>Classifies PerceptionType "toggle" elements as LOCAL_CONTROL.</summary>
    public static SemanticCapabilityTestEnvironment WithToggleLocalControl(this IEnvironment environment) =>
        new(environment, element =>
            string.Equals(element.PerceptionType, "toggle", StringComparison.Ordinal)
                ? FixtureSemanticRole.LocalControl
                : null);

    /// <summary>Classifies every text-bearing element as a NAVIGATION_CANDIDATE.</summary>
    public static SemanticCapabilityTestEnvironment WithAllTextNavigation(this IEnvironment environment) =>
        new(environment, element =>
            string.IsNullOrWhiteSpace(element.Text) ? null : FixtureSemanticRole.NavigationCandidate);
}

/// <summary>Test-only decorator that stamps explicit fixture elements as primary visual evidence.</summary>
public sealed class SemanticCapabilityTestEnvironment : IEnvironment
{
    private readonly IEnvironment _inner;
    private readonly SemanticCapabilityRuntime _runtime;
    private readonly Func<ObservedElement, FixtureSemanticRole?> _classifier;
    private readonly Func<Observation, ObservedElement, int, FixtureSemanticRole?>? _contextClassifier;
    private Observation _currentObservation = null!;

    /// <summary>Full observation currently being interpreted by the fixture capability.</summary>
    internal Observation CurrentObservation => _currentObservation;

    public SemanticCapabilityTestEnvironment(IEnvironment inner, Func<ObservedElement, FixtureSemanticRole?> classifier)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _runtime = new SemanticCapabilityRuntime(new FixtureCapability(this));
    }

    /// <summary>Context-aware test-only binding for scenarios where the same
    /// label has different roles on different pages. This remains outside
    /// production and emits only passive typed evidence.</summary>
    public SemanticCapabilityTestEnvironment(
        IEnvironment inner,
        Func<Observation, ObservedElement, int, FixtureSemanticRole?> classifier)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _contextClassifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _classifier = _ => null;
        _runtime = new SemanticCapabilityRuntime(new FixtureCapability(this));
    }

    public IEnvironment Inner => _inner;
    public IReadOnlyList<DeviceAction> ActionHistory => (_inner as ScriptedEnvironment)?.ActionHistory ?? Array.Empty<DeviceAction>();
    public IReadOnlyList<Observation> ObservationHistory => (_inner as ScriptedEnvironment)?.ObservationHistory ?? Array.Empty<Observation>();

    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        var raw = await _inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
        var frame = $"fixture-frame-{raw.SequenceNumber}";
        var stamped = raw with { Sources = ImmutableArray.Create(
            new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, raw.SequenceNumber, frame, 1080, 1920,
                "fixture-vision", "fixture-vision"),
            new ObservationSourceMetadata(ObservationSourceTier.AuxiliaryStructured, !raw.StructuredElements.IsDefaultOrEmpty,
                raw.SequenceNumber, frame, 1080, 1920, "fixture-structured", "fixture-structured")) };
        _currentObservation = stamped;
        var context = SemanticObservationFactProjector.Project(stamped);
        var batch = await _runtime.EvaluateAsync(context, context.Observation, context.Sources,
            DateTimeOffset.UnixEpoch, cancellationToken).ConfigureAwait(false);
        return stamped with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) };
    }

    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken) => _inner.ExecuteAsync(action, cancellationToken);

    private sealed class FixtureCapability : IExternalSemanticCapability
    {
        private readonly SemanticCapabilityTestEnvironment _owner;

        public FixtureCapability(SemanticCapabilityTestEnvironment owner) => _owner = owner;

        public SemanticCapabilityManifest Manifest { get; } = new("fixture.semantic", "1", ["fixture.navigation", "fixture.parent-return", "fixture.local-control", "fixture.non-interactive"]);

        public ValueTask<ImmutableArray<SemanticEvidenceV2Envelope>> InterpretAsync(ExternalSemanticCapabilityContext context, CancellationToken cancellationToken = default)
        {
            var output = ImmutableArray.CreateBuilder<SemanticEvidenceV2Envelope>();
            var index = 0;
            foreach (var fact in context.Facts.Where(f => f.SourceTier == SemanticSourceTier.Primary && f.Kind == SemanticObservationFactKind.Text))
            {
                var element = new ObservedElement(fact.RawText ?? string.Empty, null, index, null, fact.RawProviderType);
                var role = _owner._contextClassifier is { } contextClassifier
                    ? contextClassifier(_owner.CurrentObservation, element, index)
                    : _owner._classifier(element);
                index++;
                if (role is null) continue;
                var meaning = new SemanticSymbolReference(Manifest.ManifestId, Manifest.Version, role switch
                {
                    FixtureSemanticRole.ParentReturnControl => "fixture.parent-return",
                    FixtureSemanticRole.LocalControl => "fixture.local-control",
                    FixtureSemanticRole.NonInteractive => "fixture.non-interactive",
                    _ => "fixture.navigation",
                });
                var observation = context.Observation;
                var scope = new SemanticScopeReference($"occurrence:{fact.OccurrenceId}");
                var provenance = new SemanticProvenance(fact.SourceId, fact.SourceTier, fact.ProvenanceId, DateTimeOffset.UnixEpoch, fact.FrameId);
                SemanticCandidateEvidence candidate = role switch
                {
                    FixtureSemanticRole.ParentReturnControl =>
                        new ContainerRelationCandidateEvidence(fact.OccurrenceId, ContainerRelationKind.ReturnToParent, meaning, meaning, observation, scope, provenance, .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue),
                    FixtureSemanticRole.LocalControl =>
                        new ElementAffordanceCandidateEvidence(fact.OccurrenceId, ElementAffordanceKind.LocalControl, meaning, observation, scope, provenance, .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue),
                    FixtureSemanticRole.NonInteractive =>
                        new ElementAffordanceCandidateEvidence(fact.OccurrenceId, ElementAffordanceKind.NonInteractive, meaning, observation, scope, provenance, .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue),
                    _ =>
                        new ElementAffordanceCandidateEvidence(fact.OccurrenceId, ElementAffordanceKind.NavigationCandidate, meaning, observation, scope, provenance, .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue),
                };
                output.Add(new SemanticEvidenceV2Envelope($"fixture:{fact.OccurrenceId}", candidate));
            }
            return ValueTask.FromResult(output.ToImmutable());
        }
    }
}
