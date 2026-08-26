using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.ValidationHarness.Fixtures;

/// <summary>Fixture semantic roles the harness decorator stamps onto elements.</summary>
public enum FixtureSemanticRole
{
    /// <summary>A bound navigable container candidate (expand-and-traverse).</summary>
    NavigationCandidate,

    /// <summary>The unique labelled parent-return control of a container.</summary>
    ParentReturnControl,

    /// <summary>An interactive-but-local control (mutates page state only; never navigates).</summary>
    LocalControl,

    /// <summary>Not an interaction target (titles, state lines, OCR noise). Classifying
    /// non-targets explicitly is REQUIRED for completeness proofs: an element with NO
    /// admitted role stays UNKNOWN and blocks container completeness (fail closed).</summary>
    NonInteractive,
}

/// <summary>
/// Harness-local observation decorator (modeled on the Scenario test fake
/// <c>SemanticCapabilityTestEnvironment</c>, written against production Runtime
/// types only): stamps the two fixture observation sources and runs the fixture
/// capability so the observation carries admitted typed semantic evidence —
/// exactly the input the existing open-world traversal consumes. It holds no
/// Runtime authority and emits only passive typed candidate evidence.
/// </summary>
public sealed class FixtureSemanticEnvironment : IEnvironment
{
    private readonly IEnvironment _inner;
    private readonly SemanticCapabilityRuntime _runtime;
    private readonly Func<ObservedElement, FixtureSemanticRole?> _classifier;
    private Observation _currentObservation = null!;

    /// <summary>The observation currently being interpreted by the fixture
    /// capability (set in ObserveAsync before evaluation) — the context-aware
    /// classifier reads it for page-dependent roles.</summary>
    public Observation CurrentObservation => _currentObservation;

    private readonly Func<Observation, ObservedElement, int, FixtureSemanticRole?>? _contextClassifier;

    public FixtureSemanticEnvironment(IEnvironment inner, Func<ObservedElement, FixtureSemanticRole?> classifier)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _runtime = new SemanticCapabilityRuntime(new FixtureCapability(this));
    }

    /// <summary>
    /// Context-aware construction (mirrors the Scenario test fake): the
    /// classifier receives the whole observation — page-aware roles such as
    /// "the root title is NOT a parent-return control on the root page" are
    /// expressible. Required for viewport-exploration completeness on pages
    /// whose title text equals the child pages' return-control label.
    /// </summary>
    public FixtureSemanticEnvironment(
        IEnvironment inner,
        Func<Observation, ObservedElement, int, FixtureSemanticRole?> contextClassifier)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _classifier = _ => null;
        _contextClassifier = contextClassifier ?? throw new ArgumentNullException(nameof(contextClassifier));
        _runtime = new SemanticCapabilityRuntime(new FixtureCapability(this));
    }

    /// <summary>Raw underlying world (fixture surface; not a Runtime surface).</summary>
    public IEnvironment Inner => _inner;

    /// <summary>Fixture world action history when the inner environment is the
    /// deterministic world; empty otherwise.</summary>
    public IReadOnlyList<DeviceAction> ActionHistory =>
        (_inner as ValidationFixtureWorld)?.ActionHistory ?? Array.Empty<DeviceAction>();

    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        var raw = await _inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
        var frame = $"fixture-frame-{raw.SequenceNumber}";
        var stamped = raw with
        {
            Sources = ImmutableArray.Create(
                new ObservationSourceMetadata(
                    ObservationSourceTier.PrimaryVision, true, raw.SequenceNumber, frame, 1080, 1920,
                    "fixture-vision", "fixture-vision"),
                new ObservationSourceMetadata(
                    ObservationSourceTier.AuxiliaryStructured, !raw.StructuredElements.IsDefaultOrEmpty,
                    raw.SequenceNumber, frame, 1080, 1920, "fixture-structured", "fixture-structured")),
        };
        _currentObservation = stamped;
        var context = SemanticObservationFactProjector.Project(stamped);
        var batch = await _runtime.EvaluateAsync(
            context,
            context.Observation,
            context.Sources,
            DateTimeOffset.UnixEpoch,
            cancellationToken).ConfigureAwait(false);
        var finalObs = stamped with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) };
        if (System.Environment.GetEnvironmentVariable("TIERB_DEBUG_EVIDENCE") is not null)
        {
            var summary = string.Join(",",
                InteractionAffordanceAnalyzer.Analyze(finalObs)
                    .Where(a => a.EligibleForAuthorization)
                    .Select(a => $"{a.Classification}({(finalObs.Elements[a.CanonicalOccurrence.Reference.ElementIndex].Text ?? "?")[..Math.Min(14, (finalObs.Elements[a.CanonicalOccurrence.Reference.ElementIndex].Text ?? "?").Length)]})"));
            Console.Error.WriteLine($"[evidence] seq={raw.SequenceNumber} page='{ResolvePageHint(finalObs)}' admitted-interactive=[{summary}] rejected={batch.Rejected.Length} reasons=[{string.Join(",", batch.RejectionReasons)}] envelopes=[{string.Join(",", batch.Rejected.Select(e => e.Candidate.GetType().Name))}]");
        }
        return finalObs;
    }

    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken) =>
        _inner.ExecuteAsync(action, cancellationToken);

    private string ResolvePageHint(Observation observation)
    {
        try { return RealityFixtureStrategyBinding.ResolvePage(observation) ?? "?"; }
        catch { return "?"; }
    }

    private sealed class FixtureCapability : IExternalSemanticCapability
    {
        private readonly FixtureSemanticEnvironment _owner;

        public FixtureCapability(FixtureSemanticEnvironment owner) => _owner = owner;

        public SemanticCapabilityManifest Manifest { get; } = new(
            "fixture.semantic",
            "1",
            ["fixture.navigation", "fixture.parent-return", "fixture.non-interactive", "fixture.local-control"]);

        public ValueTask<ImmutableArray<SemanticEvidenceV2Envelope>> InterpretAsync(
            ExternalSemanticCapabilityContext context, CancellationToken cancellationToken = default)
        {
            var output = ImmutableArray.CreateBuilder<SemanticEvidenceV2Envelope>();
            var index = 0;
            foreach (var fact in context.Facts.Where(fact =>
                         fact.SourceTier == SemanticSourceTier.Primary && fact.Kind == SemanticObservationFactKind.Text))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = new ObservedElement(fact.RawText ?? string.Empty, null, index, null, fact.RawProviderType);
                var role = _owner._contextClassifier is { } ctx
                    ? ctx(_owner.CurrentObservation, element, index)
                    : _owner._classifier(element);
                index++;
                if (role is null)
                    continue;

                var meaning = new SemanticSymbolReference(
                    Manifest.ManifestId,
                    Manifest.Version,
                    role switch
                    {
                        FixtureSemanticRole.ParentReturnControl => "fixture.parent-return",
                        FixtureSemanticRole.NonInteractive => "fixture.non-interactive",
                        FixtureSemanticRole.LocalControl => "fixture.local-control",
                        _ => "fixture.navigation",
                    });
                var observation = context.Observation;
                var scope = new SemanticScopeReference($"occurrence:{fact.OccurrenceId}");
                var provenance = new SemanticProvenance(
                    fact.SourceId, fact.SourceTier, fact.ProvenanceId, DateTimeOffset.UnixEpoch, fact.FrameId);
                SemanticCandidateEvidence candidate = role switch
                {
                    FixtureSemanticRole.ParentReturnControl => new ContainerRelationCandidateEvidence(
                        fact.OccurrenceId,
                        ContainerRelationKind.ReturnToParent,
                        meaning,
                        meaning,
                        observation,
                        scope,
                        provenance,
                        confidence: 0.9,
                        DateTimeOffset.UnixEpoch,
                        DateTimeOffset.MaxValue),
                    FixtureSemanticRole.NonInteractive => new ElementAffordanceCandidateEvidence(
                        fact.OccurrenceId,
                        ElementAffordanceKind.NonInteractive,
                        meaning,
                        observation,
                        scope,
                        provenance,
                        confidence: 0.9,
                        DateTimeOffset.UnixEpoch,
                        DateTimeOffset.MaxValue),
                    FixtureSemanticRole.LocalControl => new ElementAffordanceCandidateEvidence(
                        fact.OccurrenceId,
                        ElementAffordanceKind.LocalControl,
                        meaning,
                        observation,
                        scope,
                        provenance,
                        confidence: 0.9,
                        DateTimeOffset.UnixEpoch,
                        DateTimeOffset.MaxValue),
                    _ => new ElementAffordanceCandidateEvidence(
                        fact.OccurrenceId,
                        ElementAffordanceKind.NavigationCandidate,
                        meaning,
                        observation,
                        scope,
                        provenance,
                        confidence: 0.9,
                        DateTimeOffset.UnixEpoch,
                        DateTimeOffset.MaxValue),
                };
                output.Add(new SemanticEvidenceV2Envelope($"fixture:{fact.OccurrenceId}", candidate));
            }

            return ValueTask.FromResult(output.ToImmutable());
        }
    }
}