using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class SemanticCapabilityEnvironmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly SemanticObservationReference ObservationRef = new("obs", 1, "frame");
    private static readonly SemanticSourceMetadata Source = new("vision", SemanticSourceTier.Primary, true, "frame");
    private static readonly SemanticCapabilityManifest Manifest = new("cap", "1", ["container"]);

    [Fact]
    public async Task ObserveEnrichesPrimaryEvidenceAndKeepsSnapshotsIsolated()
    {
        var first = new Observation(ImmutableArray<ObservedElement>.Empty, null, 1);
        var second = first with { SequenceNumber = 2 };
        var environment = new StubEnvironment(first, second);
        var decorator = Create(environment, [Envelope(Source)]);

        var a = await decorator.ObserveAsync(CancellationToken.None);
        var b = await decorator.ObserveAsync(CancellationToken.None);

        Assert.Single(a.AdmittedSemanticEvidence.Evidence);
        Assert.Single(a.AdmittedSemanticEvidence.EligibleForAuthorizationInput);
        Assert.Empty(b.AdmittedSemanticEvidence.Evidence);
        Assert.NotSame(a.AdmittedSemanticEvidence, b.AdmittedSemanticEvidence);
    }

    [Fact]
    public async Task AuxiliaryEvidenceIsRetainedButNotEligible()
    {
        var auxiliary = new SemanticSourceMetadata("adb", SemanticSourceTier.Auxiliary, true, "frame");
        var raw = new Observation(ImmutableArray<ObservedElement>.Empty, null, 1);
        var decorator = Create(new StubEnvironment(raw), [Envelope(auxiliary)], projector: observation => Project(observation, auxiliary));
        var result = await decorator.ObserveAsync(CancellationToken.None);
        Assert.Single(result.AdmittedSemanticEvidence.Evidence);
        Assert.Empty(result.AdmittedSemanticEvidence.EligibleForAuthorizationInput);
    }

    [Fact]
    public async Task NoCapabilityLeavesRawObservationUnenriched()
    {
        var raw = new Observation(ImmutableArray<ObservedElement>.Empty, "app", 1);
        var result = await new SemanticCapabilityEnvironment(new StubEnvironment(raw), new SemanticCapabilityRuntime(), Project).ObserveAsync(CancellationToken.None);
        Assert.Equal(raw, result with { AdmittedSemanticEvidence = AdmittedSemanticEvidenceSnapshot.Empty });
        Assert.Empty(result.AdmittedSemanticEvidence.Evidence);
    }

    [Fact]
    public async Task BadCapabilityDoesNotBlockRawObservation()
    {
        var raw = new Observation(ImmutableArray<ObservedElement>.Empty, "app", 1);
        var result = await Create(new StubEnvironment(raw), null, throws: true).ObserveAsync(CancellationToken.None);
        Assert.Equal("app", result.ForegroundApplication);
        Assert.Empty(result.AdmittedSemanticEvidence.Evidence);
    }

    [Fact]
    public async Task ExecutePassesThroughExactlyOnce()
    {
        var raw = new Observation(ImmutableArray<ObservedElement>.Empty, null, 1);
        var environment = new StubEnvironment(raw);
        var decorator = Create(environment, null);
        var action = new DeviceAction.ScrollForward();
        await decorator.ExecuteAsync(action, CancellationToken.None);
        Assert.Same(action, environment.LastAction);
        Assert.Equal(1, environment.ExecuteCount);
    }

    private static SemanticCapabilityEnvironment Create(StubEnvironment environment, ImmutableArray<SemanticEvidenceV2Envelope>? evidence, bool throws = false, Func<Observation, ExternalSemanticCapabilityContext>? projector = null)
    {
        IExternalSemanticCapability? capability = evidence is null && !throws ? null : new StubCapability(evidence ?? [], throws);
        return new SemanticCapabilityEnvironment(environment, new SemanticCapabilityRuntime(capability), projector ?? Project, () => Now);
    }

    private static ExternalSemanticCapabilityContext Project(Observation observation) => Project(observation, Source);

    private static ExternalSemanticCapabilityContext Project(Observation observation, SemanticSourceMetadata source) =>
        new(new SemanticObservationReference(
            observation.SequenceNumber == 1 ? "obs" : $"obs-{observation.SequenceNumber}",
            observation.SequenceNumber,
            source.FrameId), [source]);

    private static SemanticEvidenceV2Envelope Envelope(SemanticSourceMetadata source) =>
        new("e1", new ContainerIdentityCandidateEvidence(new SemanticSymbolReference("cap", "1", "container"), ObservationRef,
            new SemanticScopeReference("scope"), new SemanticProvenance(source.SourceId, source.Tier, "capture", Now, source.FrameId), .9, Now, Now.AddMinutes(1)));

    private sealed class StubCapability(ImmutableArray<SemanticEvidenceV2Envelope> evidence, bool throws) : IExternalSemanticCapability
    {
        public SemanticCapabilityManifest Manifest { get; } = SemanticCapabilityEnvironmentTests.Manifest;
        public ValueTask<ImmutableArray<SemanticEvidenceV2Envelope>> InterpretAsync(ExternalSemanticCapabilityContext context, CancellationToken cancellationToken = default) =>
            throws ? throw new InvalidOperationException("failure") : ValueTask.FromResult(evidence);
    }

    private sealed class StubEnvironment(params Observation[] observations) : IEnvironment
    {
        private readonly Queue<Observation> _observations = new(observations);
        public DeviceAction? LastAction { get; private set; }
        public int ExecuteCount { get; private set; }
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => Task.FromResult(_observations.Count == 0 ? observations[^1] : _observations.Dequeue());
        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            LastAction = action;
            ExecuteCount++;
            return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, null, null));
        }
    }
}
