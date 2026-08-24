using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Semantic.Settings;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class ExternalSettingsSemanticCapabilityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Primary_vision_facts_produce_typed_candidates_with_provenance()
    {
        var capability = new SettingsSemanticCapability();
        var context = Context(
            new SemanticObservationFact("row-1", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "Navigate up"));

        var result = await capability.InterpretAsync(context);

        var evidence = Assert.Single(result);
        Assert.IsType<ContainerRelationCandidateEvidence>(evidence.Candidate);
        Assert.Equal(ContainerRelationKind.ReturnToParent, ((ContainerRelationCandidateEvidence)evidence.Candidate).RelationKind);
        Assert.Equal(SemanticSourceTier.Primary, evidence.Provenance.Tier);
        Assert.Equal("vision", evidence.Provenance.SourceId);
        Assert.Equal("row-1", ((ContainerRelationCandidateEvidence)evidence.Candidate).RelatedOccurrenceId);
    }

    [Fact]
    public async Task Auxiliary_only_fails_closed()
    {
        var context = Context(new SemanticObservationFact("row-1", SemanticObservationFactKind.Text,
            "adb", SemanticSourceTier.Auxiliary, "capture-1", 1, "frame-1", rawText: "Settings"));

        Assert.Empty(await new SettingsSemanticCapability().InterpretAsync(context));
    }

    [Fact]
    public async Task Unknown_locale_fails_closed()
    {
        var context = Context(new SemanticObservationFact("row-1", SemanticObservationFactKind.Text,
            "vision", SemanticSourceTier.Primary, "capture-1", 1, "frame-1", rawText: "Settings"));

        Assert.Empty(await new SettingsSemanticCapability("xx-XX").InterpretAsync(context));
    }

    [Fact]
    public async Task Package_emits_only_manifest_bound_symbols()
    {
        var context = Context(
            new SemanticObservationFact("container-1", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "Settings"),
            new SemanticObservationFact("search-1", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "Search"),
            new SemanticObservationFact("row-1", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "Network", rawClassName: "android.widget.LinearLayout",
                rawContentDescription: "summary"));

        var capability = new SettingsSemanticCapability();
        var result = await capability.InterpretAsync(context);

        Assert.Equal(3, result.Length);
        Assert.All(result, envelope => Assert.True(capability.Manifest.Contains(envelope.Meaning)));
        Assert.Equal(ElementAffordanceKind.LocalControl,
            Assert.IsType<ElementAffordanceCandidateEvidence>(result[1].Candidate).AffordanceKind);
        Assert.Equal(ElementAffordanceKind.NavigationCandidate,
            Assert.IsType<ElementAffordanceCandidateEvidence>(result[2].Candidate).AffordanceKind);
        Assert.DoesNotContain(result.SelectMany(e => e.GetType().GetProperties()), p =>
            p.Name is "DeviceAction" or "Route" or "Selector" or "GoalEvidence");
    }

    [Fact]
    public async Task Auxiliary_support_retains_primary_occurrence_and_provenance()
    {
        var primary = new SemanticObservationFact("vision-search", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Search");
        var auxiliary = new SemanticObservationFact("adb-search-container", SemanticObservationFactKind.ClassName, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawClassName: "LinearLayout",
            clickable: true, parentOccurrenceId: "vision-search");

        var evidence = Assert.Single(await new SettingsSemanticCapability().InterpretAsync(Context(primary, auxiliary)));
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal("vision-search", affordance.OccurrenceId);
        Assert.Equal(SemanticSourceTier.Primary, evidence.Provenance.Tier);
        Assert.Equal("vision", evidence.Provenance.SourceId);
    }

    [Fact]
    public async Task Vision_only_and_vision_plus_auxiliary_emit_same_primary_authority()
    {
        var primary = new SemanticObservationFact("vision-row", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network",
            rawClassName: "android.widget.LinearLayout", rawContentDescription: "summary");
        var auxiliary = new SemanticObservationFact("adb-row", SemanticObservationFactKind.ClassName, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawText: "Network", clickable: true);

        var capability = new SettingsSemanticCapability();
        var visionOnly = Assert.Single(await capability.InterpretAsync(Context(primary)));
        var corroborated = Assert.Single(await capability.InterpretAsync(Context(primary, auxiliary)));

        Assert.Equal(visionOnly.Candidate, corroborated.Candidate);
        Assert.Equal(SemanticSourceTier.Primary, corroborated.Provenance.Tier);
        Assert.Equal("vision-row", ((ElementAffordanceCandidateEvidence)corroborated.Candidate).OccurrenceId);
    }

    [Fact]
    public async Task Vision_menu_item_provider_type_emits_navigation_candidate()
    {
        var primary = new SemanticObservationFact("vision-menu-item", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network",
            rawProviderType: "menu_item", bounds: new SemanticNormalizedBounds(0, 0, 1, .1f));

        var evidence = Assert.Single(await new SettingsSemanticCapability().InterpretAsync(Context(primary)));
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal(ElementAffordanceKind.NavigationCandidate, affordance.AffordanceKind);
        Assert.Equal("vision-menu-item", affordance.OccurrenceId);
        Assert.Equal(SemanticSourceTier.Primary, evidence.Provenance.Tier);
    }

    [Fact]
    public async Task Ambiguous_auxiliary_support_does_not_suppress_primary_candidate()
    {
        var primary = new SemanticObservationFact("vision-search", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Search");
        var aux1 = new SemanticObservationFact("adb-search-1", SemanticObservationFactKind.ClassName, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawText: "Search", clickable: true);
        var aux2 = new SemanticObservationFact("adb-search-2", SemanticObservationFactKind.ClassName, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawText: "Search", clickable: true);

        var evidence = Assert.Single(await new SettingsSemanticCapability().InterpretAsync(Context(primary, aux1, aux2)));
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal("vision-search", affordance.OccurrenceId);
        Assert.Equal(SemanticSourceTier.Primary, evidence.Provenance.Tier);
    }

    [Fact]
    public async Task Primary_toggle_fact_emits_local_control_at_visual_occurrence()
    {
        var primary = new SemanticObservationFact("vision-toggle", SemanticObservationFactKind.BooleanState, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network",
            rawProviderType: "toggle", primitiveState: true);

        var evidence = Assert.Single(await new SettingsSemanticCapability().InterpretAsync(Context(primary)));
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal(ElementAffordanceKind.LocalControl, affordance.AffordanceKind);
        Assert.Equal("vision-toggle", affordance.OccurrenceId);
        Assert.Equal(SemanticSourceTier.Primary, evidence.Provenance.Tier);
    }

    [Fact]
    public async Task Text_and_state_facts_for_one_toggle_occurrence_emit_one_candidate()
    {
        var text = new SemanticObservationFact("vision-toggle", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-text", 1, "frame-1", rawText: "Network");
        var state = new SemanticObservationFact("vision-toggle", SemanticObservationFactKind.BooleanState, "vision",
            SemanticSourceTier.Primary, "vision-state", 1, "frame-1", primitiveState: true,
            rawProviderType: "toggle");

        var result = await new SettingsSemanticCapability().InterpretAsync(Context(text, state));

        var evidence = Assert.Single(result);
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal(ElementAffordanceKind.LocalControl, affordance.AffordanceKind);
        Assert.Equal("vision-toggle", affordance.OccurrenceId);
    }

    [Fact]
    public async Task Projector_runtime_and_analyzer_accept_primary_menu_item()
    {
        var observation = new Observation(
            [new ObservedElement("Network", null, 0, new ElementBounds(.1f, .1f, .8f, .2f), "menu_item")],
            "fixture", 7)
        {
            Sources = [new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, 7,
                "frame-7", 100, 100, "vision", "vision")]
        };
        var projected = SemanticObservationFactProjector.Project(observation);
        var batch = await new SemanticCapabilityRuntime(new SettingsSemanticCapability()).EvaluateAsync(
            projected, projected.Observation, projected.Sources, DateTimeOffset.UnixEpoch);

        var accepted = Assert.Single(batch.EligibleForAuthorizationInput);
        var analyzed = InteractionAffordanceAnalyzer.Analyze(
            observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) });
        var affordance = Assert.Single(analyzed);
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, affordance.Classification);
        Assert.True(affordance.EligibleForAuthorization);
        Assert.Equal(accepted.Candidate, batch.Accepted[0].Candidate);
    }

    private static ExternalSemanticCapabilityContext Context(params SemanticObservationFact[] facts) =>
        new(new SemanticObservationReference("observation:1", 1, "frame-1"),
            new[]
            {
                new SemanticSourceMetadata("vision", SemanticSourceTier.Primary, true, "frame-1"),
                new SemanticSourceMetadata("adb", SemanticSourceTier.Auxiliary, true, "frame-1"),
            }, facts: facts);
}
