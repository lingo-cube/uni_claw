using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class SemanticCapabilityRuntimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static SemanticObservationReference Observation => new("obs-1", 1, "frame-1");
    private static SemanticSourceMetadata Vision => new("vision", SemanticSourceTier.Primary, true, "frame-1");
    private static SemanticSourceMetadata Adb => new("adb", SemanticSourceTier.Auxiliary, true, "frame-1");
    private static SemanticCapabilityManifest Manifest => new("cap", "1", ["container"]);

    [Fact]
    public async Task NoCapabilityProducesEmptyBatch()
    {
        var result = await new SemanticCapabilityRuntime().EvaluateAsync(Context(), Observation, [Vision], Now);
        Assert.Empty(result.Accepted);
        Assert.Empty(result.EligibleForAuthorizationInput);
    }

    [Fact]
    public async Task InvalidCandidateIsRejectedByAdmission()
    {
        var envelope = Envelope(Vision, validUntil: Now.AddMinutes(1));
        var capability = new StubCapability(Manifest, [envelope with { ProtocolVersion = "unknown" }]);
        var result = await new SemanticCapabilityRuntime(capability).EvaluateAsync(Context(), Observation, [Vision], Now);
        Assert.Empty(result.Accepted);
        Assert.Single(result.Rejected);
    }

    [Fact]
    public async Task AuxiliaryEvidenceIsPreservedButNotEligible()
    {
        var capability = new StubCapability(Manifest, [Envelope(Adb, validUntil: Now.AddMinutes(1))]);
        var result = await new SemanticCapabilityRuntime(capability).EvaluateAsync(Context(Adb), Observation, [Adb], Now);
        Assert.Single(result.AuxiliaryCorroboration);
        Assert.Empty(result.EligibleForAuthorizationInput);
    }

    [Fact]
    public async Task PrimaryEvidenceIsEligible()
    {
        var capability = new StubCapability(Manifest, [Envelope(Vision, validUntil: Now.AddMinutes(1))]);
        var result = await new SemanticCapabilityRuntime(capability).EvaluateAsync(Context(), Observation, [Vision], Now);
        Assert.Single(result.EligibleForAuthorizationInput);
    }

    [Fact]
    public async Task CapabilityExceptionFailsClosed()
    {
        var capability = new StubCapability(Manifest, null, throwOnInterpret: true);
        var result = await new SemanticCapabilityRuntime(capability).EvaluateAsync(Context(), Observation, [Vision], Now);
        Assert.Empty(result.Accepted);
        Assert.Empty(result.Rejected);
        Assert.NotEmpty(result.RejectionReasons);
    }

    [Fact]
    public async Task CancellationDoesNotProduceMutation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new SemanticCapabilityRuntime().EvaluateAsync(Context(), Observation, [Vision], Now, cts.Token).AsTask());
    }

    private static ExternalSemanticCapabilityContext Context(SemanticSourceMetadata? source = null) =>
        new(Observation, [source ?? Vision]);

    private static SemanticEvidenceV2Envelope Envelope(SemanticSourceMetadata source, DateTimeOffset validUntil) =>
        new("e1", new ContainerIdentityCandidateEvidence(
            new SemanticSymbolReference("cap", "1", "container"), Observation,
            new SemanticScopeReference("obs"),
            new SemanticProvenance(source.SourceId, source.Tier, "capture-1", Now, source.FrameId),
            .9, Now, validUntil));

    private sealed class StubCapability(
        SemanticCapabilityManifest manifest,
        ImmutableArray<SemanticEvidenceV2Envelope>? evidence,
        bool throwOnInterpret = false) : IExternalSemanticCapability
    {
        public SemanticCapabilityManifest Manifest { get; } = manifest;
        public ValueTask<ImmutableArray<SemanticEvidenceV2Envelope>> InterpretAsync(
            ExternalSemanticCapabilityContext context, CancellationToken cancellationToken = default) =>
            throwOnInterpret ? throw new InvalidOperationException("test") : ValueTask.FromResult(evidence ?? []);
    }
}
