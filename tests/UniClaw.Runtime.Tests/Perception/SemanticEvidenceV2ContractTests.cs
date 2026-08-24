using Xunit;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class SemanticEvidenceV2ContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_typed_candidate_is_admitted()
    {
        var manifest = Manifest();
        var observation = Observation();
        var candidate = Candidate(observation, manifest);
        var result = SemanticEvidenceV2Admission.Admit(
            new SemanticEvidenceV2Envelope("e-1", candidate),
            Context(manifest, observation, SemanticSourceTier.Primary));

        Assert.True(result.Accepted);
        Assert.NotNull(result.Evidence);
        Assert.Equal(SemanticEvidenceV2Protocol.Version, result.Evidence!.ProtocolVersion);
    }

    [Theory]
    [InlineData("semantic-evidence-v1", SemanticEvidenceAdmissionFailure.UnsupportedProtocol)]
    [InlineData("wrong-manifest", SemanticEvidenceAdmissionFailure.ManifestMismatch)]
    public void Unsupported_protocol_or_manifest_fails_closed(
        string mode, SemanticEvidenceAdmissionFailure expected)
    {
        var manifest = Manifest();
        var observation = Observation();
        var candidate = Candidate(observation, manifest);
        var envelope = new SemanticEvidenceV2Envelope("e-1", candidate);
        if (mode == "semantic-evidence-v1")
            envelope = envelope with { ProtocolVersion = mode };
        else
            envelope = envelope with { Meaning = new SemanticSymbolReference("other", "1", "container") };

        var result = SemanticEvidenceV2Admission.Admit(
            envelope, Context(manifest, observation, SemanticSourceTier.Primary));

        Assert.False(result.Accepted);
        Assert.Equal(expected, result.Failure);
    }

    [Fact]
    public void Auxiliary_source_is_preserved_and_can_be_limited()
    {
        var manifest = Manifest();
        var observation = Observation();
        var auxiliary = Candidate(observation, manifest, SemanticSourceTier.Auxiliary);
        var result = SemanticEvidenceV2Admission.Admit(
            new SemanticEvidenceV2Envelope("e-1", auxiliary),
            Context(manifest, observation, SemanticSourceTier.Primary));

        Assert.False(result.Accepted);
        Assert.Equal(SemanticEvidenceAdmissionFailure.InvalidSourceTier, result.Failure);
        Assert.Equal(SemanticSourceTier.Auxiliary, auxiliary.Provenance.Tier);
    }

    [Fact]
    public void Coverage_requirement_is_not_candidate_evidence()
    {
        var requirement = new CoverageRequirementEvidence(
            "coverage-1", new SemanticSymbolReference("pkg", "1", "criterion"),
            new SemanticScopeReference("scope"), new[] { SemanticEvidenceKind.ContainerIdentity });

        Assert.Equal(SemanticEvidenceV2Protocol.Version, requirement.ProtocolVersion);
        Assert.DoesNotContain(SemanticEvidenceKind.ContainerRelation, requirement.RequiredEvidenceKinds);
    }

    [Fact]
    public void Observation_and_frame_mismatch_fail_closed()
    {
        var manifest = Manifest();
        var candidate = Candidate(Observation(), manifest);
        var current = new SemanticObservationReference("obs-2", 2, "frame-2");
        var result = SemanticEvidenceV2Admission.Admit(
            new SemanticEvidenceV2Envelope("e-1", candidate),
            Context(manifest, current, SemanticSourceTier.Primary));

        Assert.False(result.Accepted);
        Assert.Equal(SemanticEvidenceAdmissionFailure.StaleObservation, result.Failure);
    }

    [Fact]
    public void V2_contract_has_no_authority_bearing_members()
    {
        var forbidden = new[] { "DeviceAction", "Selector", "Route", "Coordinate", "Branch", "Completion", "GoalEvidence", "Callback", "RunState", "Fsm" };
        var types = typeof(SemanticEvidenceV2Protocol).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(SemanticEvidenceV2Protocol).Namespace);
        var names = types.SelectMany(type => type.GetProperties().Select(property => property.Name)
                .Concat(type.GetFields().Select(field => field.Name)))
            .ToArray();
        Assert.DoesNotContain(names, name => forbidden.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Facts_are_immutable_and_correlated_to_current_observation()
    {
        var observation = Observation();
        var source = new SemanticSourceMetadata("vision", SemanticSourceTier.Primary, true, observation.FrameId);
        var fact = new SemanticObservationFact(
            "occ-1", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
            "capture-1", observation.Sequence, observation.FrameId, rawText: "opaque");
        var context = new ExternalSemanticCapabilityContext(observation, new[] { source }, facts: new[] { fact });

        Assert.Single(context.Facts);
        Assert.Equal("occ-1", context.Facts[0].OccurrenceId);
        Assert.IsAssignableFrom<IReadOnlyCollection<SemanticObservationFact>>(context.Facts);
        Assert.Equal("occ-parent", new SemanticObservationFact(
            "occ-2", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
            "capture-2", observation.Sequence, observation.FrameId, parentOccurrenceId: "occ-parent").ParentOccurrenceId);
        Assert.DoesNotContain("ParentOccurrenceId", typeof(ContainerIdentityCandidateEvidence).GetProperties().Select(p => p.Name));
    }

    [Theory]
    [InlineData(false, SemanticSourceTier.Primary, 1, "frame-1")]
    [InlineData(true, SemanticSourceTier.Auxiliary, 1, "frame-1")]
    [InlineData(true, SemanticSourceTier.Primary, 2, "frame-1")]
    [InlineData(true, SemanticSourceTier.Primary, 1, "foreign-frame")]
    public void Foreign_stale_or_unavailable_facts_fail_closed(
        bool available, SemanticSourceTier tier, long sequence, string frame)
    {
        var observation = Observation();
        var source = new SemanticSourceMetadata("vision", SemanticSourceTier.Primary, available, observation.FrameId);
        var fact = new SemanticObservationFact(
            "occ-1", SemanticObservationFactKind.Text, "vision", tier,
            "capture-1", sequence, frame, rawText: "opaque");

        Assert.Throws<ArgumentException>(() =>
            new ExternalSemanticCapabilityContext(observation, new[] { source }, facts: new[] { fact }));
    }

    [Fact]
    public void Vision_is_primary_and_auxiliary_source_remains_auxiliary()
    {
        var observation = Observation();
        var sources = new[]
        {
            new SemanticSourceMetadata("vision", SemanticSourceTier.Primary, true, observation.FrameId),
            new SemanticSourceMetadata("hierarchy", SemanticSourceTier.Auxiliary, true, observation.FrameId),
        };
        var facts = new[]
        {
            new SemanticObservationFact("v", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary, "c1", 1, "frame-1", rawText: "v"),
            new SemanticObservationFact("a", SemanticObservationFactKind.ClassName, "hierarchy", SemanticSourceTier.Auxiliary, "c2", 1, "frame-1", rawClassName: "opaque"),
        };

        var context = new ExternalSemanticCapabilityContext(observation, sources, facts: facts);

        Assert.Equal(SemanticSourceTier.Primary, context.Facts[0].SourceTier);
        Assert.Equal(SemanticSourceTier.Auxiliary, context.Facts[1].SourceTier);
    }

    [Fact]
    public void V1_semantic_contract_remains_frozen_and_is_not_auto_converted()
    {
        var v1Kinds = Enum.GetNames<UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidenceKind>();
        Assert.Equal(new[] { "ContainerIdentity" }, v1Kinds);

        var v1Properties = typeof(UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence)
            .GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("AffordanceKind", v1Properties);
        Assert.DoesNotContain("RelationKind", v1Properties);

        var v2Types = typeof(SemanticEvidenceV2Protocol).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(SemanticEvidenceV2Protocol).Namespace);
        var v1EvidenceType = typeof(UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence);
        var publicParameters = v2Types.SelectMany(type => type.GetMethods())
            .Where(method => method.IsPublic)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType);
        Assert.DoesNotContain(v1EvidenceType, publicParameters);
        Assert.DoesNotContain(v2Types.SelectMany(type => type.GetMethods()).Where(method => method.IsPublic),
            method => method.Name.Contains("Convert", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Translate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Candidate_occurrence_must_resolve_to_matching_source_fact()
    {
        var observation = Observation();
        var manifest = Manifest();
        var source = new SemanticSourceMetadata("source", SemanticSourceTier.Primary, true, observation.FrameId);
        var fact = new SemanticObservationFact("occ-1", SemanticObservationFactKind.Text, "source", SemanticSourceTier.Primary, "capture", 1, "frame-1", rawText: "opaque");
        var candidate = new ElementAffordanceCandidateEvidence("foreign-occurrence", ElementAffordanceKind.LocalControl,
            new SemanticSymbolReference(manifest.ManifestId, manifest.Version, "affordance"), observation,
            new SemanticScopeReference("scope"), new SemanticProvenance("source", SemanticSourceTier.Primary, "capture", Now, "frame-1"), .8, Now, Now.AddMinutes(1));

        var result = SemanticEvidenceV2Admission.Admit(new SemanticEvidenceV2Envelope("e", candidate),
            new SemanticEvidenceAdmissionContext(manifest, observation, Now, SemanticSourceTier.Primary, [source], [fact]));

        Assert.False(result.Accepted);
        Assert.Equal(SemanticEvidenceAdmissionFailure.InvalidProvenance, result.Failure);
    }

    private static SemanticCapabilityManifest Manifest() =>
        new("pkg", "1", new[] { "container", "affordance", "relation", "criterion" });

    private static SemanticObservationReference Observation() =>
        new("obs-1", 1, "frame-1");

    private static SemanticEvidenceAdmissionContext Context(
        SemanticCapabilityManifest manifest, SemanticObservationReference observation,
        SemanticSourceTier tier) => new(
            manifest, observation, Now, tier,
            new[] { new SemanticSourceMetadata("source", tier, true, observation.FrameId) });

    private static ContainerIdentityCandidateEvidence Candidate(
        SemanticObservationReference observation, SemanticCapabilityManifest manifest,
        SemanticSourceTier tier = SemanticSourceTier.Primary)
    {
        return new ContainerIdentityCandidateEvidence(
            new SemanticSymbolReference(manifest.ManifestId, manifest.Version, "container"),
            observation,
            new SemanticScopeReference("scope-1"),
            new SemanticProvenance("source", tier, "capture-1", Now, observation.FrameId),
            0.8,
            Now,
            Now.AddMinutes(1));
    }
}
