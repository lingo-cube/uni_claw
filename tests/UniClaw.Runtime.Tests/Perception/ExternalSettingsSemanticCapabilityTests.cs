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

        Assert.Equal(4, result.Length);
        Assert.All(result, envelope => Assert.True(capability.Manifest.Contains(envelope.Meaning)));
        Assert.Contains(result, envelope => envelope.Candidate is ContainerIdentityCandidateEvidence);
        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { AffordanceKind: ElementAffordanceKind.NonInteractive });
        Assert.Equal(ElementAffordanceKind.LocalControl,
            Assert.IsType<ElementAffordanceCandidateEvidence>(result[2].Candidate).AffordanceKind);
        Assert.Equal(ElementAffordanceKind.NavigationCandidate,
            Assert.IsType<ElementAffordanceCandidateEvidence>(result[3].Candidate).AffordanceKind);
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
    public async Task Overlapping_same_text_primary_box_is_noninteractive_duplicate_of_unique_menu_item()
    {
        var menu = new SemanticObservationFact("vision-menu", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Battery",
            rawProviderType: "menu_item", bounds: new SemanticNormalizedBounds(.15, .70, .30, .08));
        var duplicate = new SemanticObservationFact("vision-duplicate", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Battery",
            rawProviderType: "text_block", bounds: new SemanticNormalizedBounds(.10, .71, .35, .08));

        var result = await new SettingsSemanticCapability().InterpretAsync(Context(menu, duplicate));

        Assert.Equal(2, result.Length);
        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-menu", AffordanceKind: ElementAffordanceKind.NavigationCandidate });
        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    [Fact]
    public async Task Run6_false_positive_checkbox_duplicate_does_not_consume_navigation_row()
    {
        // Same physical row identity is represented by the same text and
        // overlapping bounds. The checkbox is a detector artifact, not an
        // independent control occurrence.
        var menu = new SemanticObservationFact("run-6-row-menu", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network & internet",
            rawProviderType: "menu_item", bounds: new SemanticNormalizedBounds(.05, .30, .75, .10));
        var falseCheckbox = new SemanticObservationFact("run-6-row-checkbox", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network & internet",
            rawProviderType: "checkbox", bounds: new SemanticNormalizedBounds(.72, .31, .18, .08));

        var result = await new SettingsSemanticCapability().InterpretAsync(Context(menu, falseCheckbox));

        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "run-6-row-menu", AffordanceKind: ElementAffordanceKind.NavigationCandidate });
        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "run-6-row-checkbox", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    [Fact]
    public async Task Run6_after_adapter_normalization_false_checkbox_does_not_consume_navigation_row()
    {
        // The adapter has already normalized the detector's checkbox label to
        // toggle. This is the exact downstream composition gate: canonical type
        // alone must not erase the same-row duplicate disposition.
        var menu = new SemanticObservationFact("run-6-canonical-menu", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network & internet",
            rawProviderType: "menu_item", bounds: new SemanticNormalizedBounds(.05, .30, .75, .10));
        var canonicalizedFalseCheckbox = new SemanticObservationFact("run-6-canonical-checkbox", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network & internet",
            rawProviderType: "toggle", bounds: new SemanticNormalizedBounds(.72, .31, .18, .08));

        var result = await new SettingsSemanticCapability().InterpretAsync(Context(menu, canonicalizedFalseCheckbox));

        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "run-6-canonical-menu", AffordanceKind: ElementAffordanceKind.NavigationCandidate });
        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "run-6-canonical-checkbox", AffordanceKind: ElementAffordanceKind.NonInteractive });
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
    public async Task Primary_switch_fact_emits_local_control_at_visual_occurrence()
    {
        var primary = new SemanticObservationFact("vision-switch", SemanticObservationFactKind.BooleanState, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network",
            rawProviderType: "switch", primitiveState: false);

        var evidence = Assert.Single(await new SettingsSemanticCapability().InterpretAsync(Context(primary)));
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal(ElementAffordanceKind.LocalControl, affordance.AffordanceKind);
        Assert.Equal("vision-switch", affordance.OccurrenceId);
    }

    [Fact]
    public async Task Primary_checkbox_fact_is_local_control_when_canonicalized_by_adapter()
    {
        // The capability consumes the adapter's canonical toggle vocabulary.
        var primary = new SemanticObservationFact("vision-checkbox", SemanticObservationFactKind.BooleanState, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network",
            rawProviderType: "toggle", primitiveState: true);

        var evidence = Assert.Single(await new SettingsSemanticCapability().InterpretAsync(Context(primary)));
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal(ElementAffordanceKind.LocalControl, affordance.AffordanceKind);
        Assert.Equal("vision-checkbox", affordance.OccurrenceId);
    }

    [Fact]
    public async Task Genuine_checkbox_child_with_control_evidence_remains_local_control()
    {
        var row = new SemanticObservationFact("genuine-row", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Network",
            rawProviderType: "menu_item", bounds: new SemanticNormalizedBounds(.05, .30, .75, .10));
        var checkboxChild = new SemanticObservationFact("genuine-checkbox-child", SemanticObservationFactKind.BooleanState, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "",
            rawProviderType: "toggle", primitiveState: true,
            bounds: new SemanticNormalizedBounds(.72, .31, .18, .08));

        var result = await new SettingsSemanticCapability().InterpretAsync(Context(row, checkboxChild));

        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "genuine-row", AffordanceKind: ElementAffordanceKind.NavigationCandidate });
        Assert.Contains(result, envelope => envelope.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "genuine-checkbox-child", AffordanceKind: ElementAffordanceKind.LocalControl });
    }

    [Fact]
    public async Task Vision_input_with_glyph_prefixed_search_settings_hint_emits_search_role()
    {
        var primary = new SemanticObservationFact("vision-search", SemanticObservationFactKind.Text, "vision",
            SemanticSourceTier.Primary, "vision-capture", 1, "frame-1", rawText: "Q Search settings",
            rawProviderType: "input", bounds: new SemanticNormalizedBounds(0, 0, 1, .1f));

        var evidence = Assert.Single(await new SettingsSemanticCapability().InterpretAsync(Context(primary)));
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(evidence.Candidate);
        Assert.Equal(ElementAffordanceKind.LocalControl, affordance.AffordanceKind);
        Assert.Equal("settings.search-role", affordance.Meaning.SymbolId);
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

    [Fact]
    public async Task Primary_container_title_is_noninteractive_for_completeness()
    {
        var observation = new Observation(
            [new ObservedElement("Settings", null, 0, new ElementBounds(.1f, .1f, .8f, .2f), "text_block")],
            "fixture", 8)
        {
            Sources = [new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, 8,
                "frame-8", 100, 100, "vision", "vision")]
        };
        var projected = SemanticObservationFactProjector.Project(observation);
        var batch = await new SemanticCapabilityRuntime(new SettingsSemanticCapability()).EvaluateAsync(
            projected, projected.Observation, projected.Sources, DateTimeOffset.UnixEpoch);

        var analyzed = InteractionAffordanceAnalyzer.Analyze(
            observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) });

        Assert.Equal(InteractionAffordanceKind.NonInteractive, Assert.Single(analyzed).Classification);
    }

    // ── E1 RED (E_SUBTITLE_ADMISSION_PATTERN gate): subtitle/description text ──
    // directly below a known preference row must classify as NonInteractive
    // (DESCRIPTION_OF_KNOWN_ROW), not Unknown.

    [Fact]
    public async Task Subtitle_below_known_preference_row_is_noninteractive()
    {
        // Row: "Battery" menu_item at y=[0.30,0.35], classified as preference-row
        // Subtitle: "74% - about 12h remaining" text_block at y=[0.36,0.40]
        // (immediately below, same column, different text)
        var context = Context(
            // Row facts (preference row with structured corroboration)
            new SemanticObservationFact("row-occ", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "Battery", rawProviderType: "menu_item"),
            new SemanticObservationFact("row-occ", SemanticObservationFactKind.Geometry, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1",
                bounds: new SemanticNormalizedBounds(0.06f, 0.30f, 0.44f, 0.05f)),
            // Subtitle facts (text below the row)
            new SemanticObservationFact("sub-occ", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "74% - about 12h remaining", rawProviderType: "text_block"),
            new SemanticObservationFact("sub-occ", SemanticObservationFactKind.Geometry, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1",
                bounds: new SemanticNormalizedBounds(0.06f, 0.36f, 0.30f, 0.04f)));

        var capability = new SettingsSemanticCapability();
        var result = await capability.InterpretAsync(context);

        // Find the subtitle evidence
        var subtitleEvidence = result.FirstOrDefault(e =>
            e.Candidate is ElementAffordanceCandidateEvidence a &&
            a.OccurrenceId == "sub-occ");
        Assert.NotNull(subtitleEvidence);
        var affordance = Assert.IsType<ElementAffordanceCandidateEvidence>(subtitleEvidence.Candidate);
        Assert.Equal(ElementAffordanceKind.NonInteractive, affordance.AffordanceKind);
    }

    [Fact]
    public async Task Next_menu_row_below_previous_row_is_NOT_subtitle()
    {
        // Two consecutive menu rows: the second must NOT be classified as
        // subtitle (it's a navigation candidate in its own right).
        var context = Context(
            new SemanticObservationFact("row-1", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "Battery", rawProviderType: "menu_item"),
            new SemanticObservationFact("row-1", SemanticObservationFactKind.Geometry, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1",
                bounds: new SemanticNormalizedBounds(0.06f, 0.30f, 0.44f, 0.05f)),
            new SemanticObservationFact("row-2", SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: "Storage", rawProviderType: "menu_item"),
            new SemanticObservationFact("row-2", SemanticObservationFactKind.Geometry, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1",
                bounds: new SemanticNormalizedBounds(0.06f, 0.40f, 0.44f, 0.05f)));

        var capability = new SettingsSemanticCapability();
        var result = await capability.InterpretAsync(context);

        // row-2 must NOT get NonInteractive from the subtitle pattern
        var row2Evidence = result.Where(e =>
            e.Candidate is ElementAffordanceCandidateEvidence a && a.OccurrenceId == "row-2");
        // It may get preference-row evidence (Pattern 6) but NOT NonInteractive-from-subtitle
        // (NonInteractive from Pattern 5 duplicate is fine but doesn't apply here)
        Assert.DoesNotContain(row2Evidence, e =>
            e.Candidate is ElementAffordanceCandidateEvidence a &&
            a.AffordanceKind == ElementAffordanceKind.NonInteractive);
    }

    // ── PER_OCCURRENCE_SEMANTIC_FAULT_CONTAINMENT (fault containment gate) ──
    // A malformed occurrence (zero-width bounds) must not destroy the semantic
    // evidence of independent valid occurrences in the same observation.

    [Fact]
    public async Task Malformed_occurrence_does_not_destroy_valid_occurrence_evidence()
    {
        // A: valid Settings title (Pattern 1 → NonInteractive)
        // B: valid Battery menu_item (Pattern 6 → NavigationCandidate)
        // C: MALFORMED element (width == 0) — real-emulator zero-width shape
        // D: valid Storage menu_item (Pattern 6 → NavigationCandidate)
        var observation = new Observation(
            [
                new ObservedElement("Settings", null, 0, new ElementBounds(.06f, .19f, .30f, .05f), "text_block"),
                new ObservedElement("Battery", null, 1, new ElementBounds(.06f, .35f, .30f, .05f), "menu_item"),
                new ObservedElement("StrayZeroWidth", null, 2, new ElementBounds(.50f, .50f, .50f, .55f), "text_block"), // X2 == X1 → width 0, IsValid passes
                new ObservedElement("Storage", null, 3, new ElementBounds(.06f, .55f, .30f, .05f), "menu_item"),
            ],
            "fixture", 42)
        {
            Sources = [new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, 42,
                "frame-42", 100, 100, "vision", "vision")]
        };

        // OLD behavior: Project(observation) throws ArgumentOutOfRangeException
        // → SemanticCapabilityEnvironment catch → whole-frame Empty
        // RED assertion: with the fix, projection succeeds (no throw),
        // and valid occurrences A/B/D get evidence; C is skipped (no geometry fact).
        var projected = SemanticObservationFactProjector.Project(observation); // must NOT throw
        var batch = await new SemanticCapabilityRuntime(new SettingsSemanticCapability()).EvaluateAsync(
            projected, projected.Observation, projected.Sources, DateTimeOffset.UnixEpoch);

        // A/B/D must have evidence despite C being malformed
        Assert.NotEmpty(batch.Accepted);

        var analyzed = InteractionAffordanceAnalyzer.Analyze(
            observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) });

        // A: Settings title → NonInteractive
        var settingsAffordance = analyzed.FirstOrDefault(a =>
            a.CanonicalOccurrence!.Reference.ElementIndex == 0);
        Assert.NotNull(settingsAffordance);
        Assert.Equal(InteractionAffordanceKind.NonInteractive, settingsAffordance.Classification);

        // B: Battery → NavigationCandidate (or at least not Unknown)
        var batteryAffordance = analyzed.FirstOrDefault(a =>
            a.CanonicalOccurrence!.Reference.ElementIndex == 1);
        Assert.NotNull(batteryAffordance);

        // D: Storage → not Unknown (valid occurrence preserved)
        var storageAffordance = analyzed.FirstOrDefault(a =>
            a.CanonicalOccurrence!.Reference.ElementIndex == 3);
        Assert.NotNull(storageAffordance);
    }

    [Fact]
    public async Task Settings_title_in_multi_element_observation_gets_evidence()
    {
        // Realistic shape: Settings + multiple menu items (like the real root page)
        var observation = new Observation(
            [
                new ObservedElement("Settings", null, 0, new ElementBounds(.06f, .19f, .30f, .05f), "text_block"),
                new ObservedElement("Network & internet", null, 1, new ElementBounds(.06f, .35f, .40f, .05f), "menu_item"),
                new ObservedElement("Battery", null, 2, new ElementBounds(.06f, .50f, .30f, .05f), "menu_item"),
            ],
            "com.android.settings", 2)
        {
            Sources = [new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, 2,
                "frame-2", 1080, 1920, "vision", "vision")]
        };

        var projected = SemanticObservationFactProjector.Project(observation);
        var batch = await new SemanticCapabilityRuntime(new SettingsSemanticCapability()).EvaluateAsync(
            projected, projected.Observation, projected.Sources, DateTimeOffset.UnixEpoch);

        var analyzed = InteractionAffordanceAnalyzer.Analyze(
            observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) });

        // Settings must be NonInteractive even with other elements present
        var settingsAffordance = analyzed.FirstOrDefault(a =>
            a.CanonicalOccurrence!.Reference.ElementIndex == 0);
        Assert.NotNull(settingsAffordance);
        Assert.Equal(InteractionAffordanceKind.NonInteractive, settingsAffordance.Classification);
    }

    // ════════════════════════════════════════════════════════════════════════
    // PATTERN_5_OCCURRENCE_GRANULARITY_REPAIR_GATE — production-granularity
    // buyer. The production projector emits ONE occurrence as MULTIPLE facts
    // (Text fact = RawText + Provider; ClassName fact; Geometry fact = Bounds).
    // Pattern-5 previously required a SINGLE fact carrying RawText + Bounds +
    // menu_item provider ⇒ peers==0 on every real frame (FACT_FRAGMENTATION).
    // These tests use projector-shaped fragmented facts; the legacy
    // mega-fact tests above remain as compatibility cases.
    // ════════════════════════════════════════════════════════════════════════

    private static SemanticObservationFact[] FragmentedOccurrence(
        string occurrenceId, string text, string provider, SemanticNormalizedBounds? bounds = null)
    {
        // Mirrors SemanticObservationFactProjector.AddVisionFacts exactly:
        // Text fact (RawText+RawProviderType) / ClassName fact / Geometry fact.
        var facts = new List<SemanticObservationFact>
        {
            new(occurrenceId, SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawText: text, rawProviderType: provider),
            new(occurrenceId, SemanticObservationFactKind.ClassName, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", rawClassName: provider),
        };
        if (bounds is not null)
            facts.Add(new(occurrenceId, SemanticObservationFactKind.Geometry, "vision", SemanticSourceTier.Primary,
                "capture-1", 1, "frame-1", bounds: bounds));
        return facts.ToArray();
    }

    private static SemanticObservationFact[] Fragmented(params SemanticObservationFact[][] occurrences) =>
        occurrences.SelectMany(facts => facts).ToArray();

    /// <summary>Gate counterexample A — exact production-granularity duplicate:
    /// same text-block occurrence + one menu_item occurrence, same text,
    /// existing-overlap predicate true, all facts projector-style fragmented →
    /// duplicate suppression must hit.</summary>
    [Fact]
    public async Task Production_granularity_duplicate_text_block_is_noninteractive_with_unique_menu_item_peer()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-menu", "Battery", "menu_item", new SemanticNormalizedBounds(.15, .70, .30, .08)),
            FragmentedOccurrence("vision-duplicate", "Battery", "text_block", new SemanticNormalizedBounds(.10, .71, .35, .08)))));

        Assert.Contains(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-menu", AffordanceKind: ElementAffordanceKind.NavigationCandidate });
        Assert.Contains(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample B — same text, no overlap → not a
    /// duplicate.</summary>
    [Fact]
    public async Task Same_text_no_overlap_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-menu", "Battery", "menu_item", new SemanticNormalizedBounds(.15, .70, .30, .08)),
            FragmentedOccurrence("vision-duplicate", "Battery", "text_block", new SemanticNormalizedBounds(.90, .88, .05, .04)))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample C — overlap, different text → not a
    /// duplicate.</summary>
    [Fact]
    public async Task Overlap_different_text_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-menu", "Battery", "menu_item", new SemanticNormalizedBounds(.15, .70, .30, .08)),
            FragmentedOccurrence("vision-duplicate", "Bluetooth", "text_block", new SemanticNormalizedBounds(.10, .71, .35, .08)))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample D — same text + overlap but peer provider
    /// is NOT menu_item → not a duplicate (a text_block peer never suppresses).</summary>
    [Fact]
    public async Task Peer_provider_not_menu_item_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-peer", "Battery", "text_block", new SemanticNormalizedBounds(.15, .70, .30, .08)),
            FragmentedOccurrence("vision-duplicate", "Battery", "text_block", new SemanticNormalizedBounds(.10, .71, .35, .08)))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample E — two possible menu_item peers →
    /// ambiguous, fail closed, NOT suppressed.</summary>
    [Fact]
    public async Task Two_menu_item_peers_ambiguous_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-menu-1", "Battery", "menu_item", new SemanticNormalizedBounds(.10, .70, .40, .06)),
            FragmentedOccurrence("vision-menu-2", "Battery", "menu_item", new SemanticNormalizedBounds(.05, .69, .45, .08)),
            FragmentedOccurrence("vision-duplicate", "Battery", "text_block", new SemanticNormalizedBounds(.12, .71, .35, .06)))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample F — missing geometry on the current
    /// occurrence → not a duplicate.</summary>
    [Fact]
    public async Task Missing_geometry_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-menu", "Battery", "menu_item", new SemanticNormalizedBounds(.15, .70, .30, .08)),
            FragmentedOccurrence("vision-duplicate", "Battery", "text_block")))); // no geometry fact

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample G — missing text → not a duplicate.</summary>
    [Fact]
    public async Task Missing_text_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-menu", "Battery", "menu_item", new SemanticNormalizedBounds(.15, .70, .30, .08)),
            FragmentedOccurrence("vision-duplicate", "", "text_block", new SemanticNormalizedBounds(.10, .71, .35, .08)))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample H — the current occurrence's OWN multiple
    /// facts must never count itself as a peer.</summary>
    [Fact]
    public async Task Own_fragmented_facts_never_count_as_peer()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(
            FragmentedOccurrence("vision-solo", "Battery", "text_block", new SemanticNormalizedBounds(.10, .71, .35, .08))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-solo", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Gate counterexample J — XML-only corroboration is NOT
    /// duplicate proof: a text_block with an auxiliary clickable row but NO
    /// menu_item primary peer must not become a NonInteractive duplicate
    /// (existing corroboration-driven promotion stays legal).</summary>
    [Fact]
    public async Task Xml_corroboration_alone_is_not_duplicate_proof()
    {
        var auxiliary = new SemanticObservationFact("adb-row", SemanticObservationFactKind.Text, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawText: "Battery",
            rawClassName: "android.widget.LinearLayout", clickable: true);
        var facts = Fragmented(
            FragmentedOccurrence("vision-duplicate", "Battery", "text_block", new SemanticNormalizedBounds(.10, .71, .35, .08)),
            new[] { auxiliary });

        var result = await new SettingsSemanticCapability().InterpretAsync(Context(facts));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-duplicate", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Regression — a genuinely interactive control (switch/toggle)
    /// that does not match any menu_item row is never suppressed and keeps
    /// its LocalControl verdict.</summary>
    [Fact]
    public async Task Interactive_switch_without_match_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(
            FragmentedOccurrence("vision-toggle", "Dark theme", "switch", new SemanticNormalizedBounds(.83, .55, .13, .04))));

        Assert.Contains(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-toggle", AffordanceKind: ElementAffordanceKind.LocalControl });
        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-toggle", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Regression — a genuine navigation row (menu_item with text)
    /// keeps its NavigationCandidate verdict under occurrence aggregation.</summary>
    [Fact]
    public async Task Genuine_navigation_row_still_navigation_candidate()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(
            FragmentedOccurrence("vision-menu", "Wi-Fi", "menu_item", new SemanticNormalizedBounds(.06, .30, .40, .05))));

        Assert.Contains(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-menu", AffordanceKind: ElementAffordanceKind.NavigationCandidate });
    }

    // ════════════════════════════════════════════════════════════════════════
    // ROW_BAND_SUB_ELEMENT pattern (bounded repair for the 'Not set'/'Will
    // never' residuals; real child-frame evidence r5 seq25). A text_block that
    // is CONTAINED inside a composed menu_item row band, or is that row's
    // immediate same-column caption (gap ≤ 0.8×rowHeight — quantization bound),
    // with DIFFERENT text, no interaction shape and no structural peer of its
    // own text → NONINTERACTIVE supporting sub-element (EXACTLY-ONE row).
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Buyer A — 'Not set' shape: sub-line fully contained in the
    /// 'Screen timeout' row band (r5 seq25 geometry, idx7 vs idx20).</summary>
    [Fact]
    public async Task Contained_sub_line_is_noninteractive_supporting_sub_element()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-row", "Screen timeout", "menu_item",
                new SemanticNormalizedBounds(.056944, .408125, .333334, .040625)),
            FragmentedOccurrence("vision-sub", "Not set", "text_block",
                new SemanticNormalizedBounds(.0625, .43625, .106944, .0125)))));

        Assert.Contains(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-sub", AffordanceKind: ElementAffordanceKind.NonInteractive });
        Assert.Contains(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-row", AffordanceKind: ElementAffordanceKind.NavigationCandidate });
    }

    /// <summary>Buyer B — 'Will never' shape: same-column caption directly
    /// below the 'Dark theme' row (r5 seq25 geometry; gap 0.010625 over the
    /// 0.6× quantization flake).</summary>
    [Fact]
    public async Task Immediate_caption_below_row_is_noninteractive_supporting_sub_element()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-row", "Dark theme", "menu_item",
                new SemanticNormalizedBounds(.0625, .55, .244444, .0175)),
            FragmentedOccurrence("vision-sub", "Will never turn on automatically", "text_block",
                new SemanticNormalizedBounds(.063889, .578125, .472222, .013125)))));

        Assert.Contains(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-sub", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Counterexample — a text_block with NO contained/below menu_item
    /// row stays unchanged (no suppression).</summary>
    [Fact]
    public async Task Text_block_without_nearby_row_unchanged()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(
            FragmentedOccurrence("vision-lone", "Standalone text", "text_block",
                new SemanticNormalizedBounds(.06, .80, .40, .03))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-lone", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Counterexample — TWO candidate rows (overlapping bands) →
    /// ambiguous, fail closed, no suppression.</summary>
    [Fact]
    public async Task Two_candidate_rows_ambiguous_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-row-1", "Row one", "menu_item",
                new SemanticNormalizedBounds(.05, .40, .40, .15)),
            FragmentedOccurrence("vision-row-2", "Row two", "menu_item",
                new SemanticNormalizedBounds(.06, .42, .38, .12)),
            FragmentedOccurrence("vision-sub", "Sub text", "text_block",
                new SemanticNormalizedBounds(.07, .44, .20, .03)))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-sub", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Counterexample — toggle/switch-shaped sub-line is never
    /// suppressed as a row sub-element.</summary>
    [Fact]
    public async Task Toggle_shaped_sub_line_not_suppressed()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-row", "Dark theme", "menu_item",
                new SemanticNormalizedBounds(.0625, .55, .244444, .0175)),
            FragmentedOccurrence("vision-sub", "Dark theme", "switch",
                new SemanticNormalizedBounds(.70, .55, .20, .04)))));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-sub", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Counterexample — a structural (XML) peer row carrying the
    /// sub-line's OWN text means a real interactive row: fail closed.</summary>
    [Fact]
    public async Task Structural_peer_of_own_text_not_suppressed()
    {
        var auxiliary = new SemanticObservationFact("adb-notset", SemanticObservationFactKind.Text, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawText: "Not set",
            rawClassName: "android.widget.LinearLayout", clickable: true);
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-row", "Screen timeout", "menu_item",
                new SemanticNormalizedBounds(.056944, .408125, .333334, .040625)),
            FragmentedOccurrence("vision-sub", "Not set", "text_block",
                new SemanticNormalizedBounds(.0625, .43625, .106944, .0125)),
            new[] { auxiliary })));

        Assert.DoesNotContain(result, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-sub", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    /// <summary>Counterexample — missing geometry or missing text never
    /// suppresses.</summary>
    [Fact]
    public async Task Missing_geometry_or_text_never_suppressed_as_sub_element()
    {
        var noBounds = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-row", "Dark theme", "menu_item",
                new SemanticNormalizedBounds(.0625, .55, .244444, .0175)),
            FragmentedOccurrence("vision-sub", "Will never turn on automatically", "text_block"))));
        Assert.DoesNotContain(noBounds, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-sub", AffordanceKind: ElementAffordanceKind.NonInteractive });

        var noText = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-row-2", "Dark theme", "menu_item",
                new SemanticNormalizedBounds(.0625, .55, .244444, .0175)),
            FragmentedOccurrence("vision-sub-2", "", "text_block",
                new SemanticNormalizedBounds(.063889, .578125, .472222, .013125)))));
        Assert.DoesNotContain(noText, e => e.Candidate is ElementAffordanceCandidateEvidence
            { OccurrenceId: "vision-sub-2", AffordanceKind: ElementAffordanceKind.NonInteractive });
    }

    // ════════════════════════════════════════════════════════════════════════
    // PARENT-RETURN position/context fallback (runH 'Parent-return candidate
    // is absent'): the toolbar back arrow is a TEXT-LESS top-band icon and the
    // campaign's structured tier carries no bounds → the cross-tier Correlate
    // bridge ('Navigate up' label) is broken. When the frame has an auxiliary
    // back-control label AND exactly ONE top-band icon, that icon is the
    // parent-return control. Unique-icon keeps ambiguity fail-closed.
    // ════════════════════════════════════════════════════════════════════════

    private static SemanticObservationFact NavigateUpAuxiliary() =>
        new("adb-up", SemanticObservationFactKind.Text, "adb", SemanticSourceTier.Auxiliary,
            "adb-capture", 1, "frame-1", rawText: "None",
            rawClassName: "android.widget.ImageButton", clickable: true,
            rawContentDescription: "Navigate up");

    /// <summary>Buyer — runH seq31 shape: unique top-band back icon + a
    /// 'Navigate up' auxiliary label (no bounds) → the icon is the
    /// parent-return control.</summary>
    [Fact]
    public async Task Unique_top_band_back_icon_is_parent_return_control()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-back", "", "icon", new SemanticNormalizedBounds(.048611, .07875, .041667, .018125)),
            FragmentedOccurrence("vision-row", "Lock display", "menu_item", new SemanticNormalizedBounds(.061111, .130625, .1875, .015625)),
            new[] { NavigateUpAuxiliary() })));

        Assert.Contains(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.ReturnToParent, RelatedOccurrenceId: "vision-back" });
    }

    /// <summary>Counterexample — no 'Navigate up' auxiliary label → the top
    /// icon is not classified as parent-return.</summary>
    [Fact]
    public async Task Top_band_icon_without_navigate_up_label_not_parent_return()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-back", "", "icon", new SemanticNormalizedBounds(.048611, .07875, .041667, .018125)),
            FragmentedOccurrence("vision-row", "Lock display", "menu_item", new SemanticNormalizedBounds(.061111, .130625, .1875, .015625)))));

        Assert.DoesNotContain(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.ReturnToParent, RelatedOccurrenceId: "vision-back" });
    }

    /// <summary>Counterexample — TWO top-band icons → ambiguous, neither is
    /// classified as parent-return.</summary>
    [Fact]
    public async Task Two_top_band_icons_ambiguous_not_parent_return()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-back-1", "", "icon", new SemanticNormalizedBounds(.048611, .07875, .041667, .018125)),
            FragmentedOccurrence("vision-back-2", "", "icon", new SemanticNormalizedBounds(.30, .07, .04, .02)),
            FragmentedOccurrence("vision-row", "Lock display", "menu_item", new SemanticNormalizedBounds(.061111, .130625, .1875, .015625)),
            new[] { NavigateUpAuxiliary() })));

        Assert.DoesNotContain(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.ReturnToParent, RelatedOccurrenceId: "vision-back-1" });
        Assert.DoesNotContain(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.ReturnToParent, RelatedOccurrenceId: "vision-back-2" });
    }

    /// <summary>Counterexample — a mid-page icon (outside the top band) with a
    /// 'Navigate up' label is NOT the back control.</summary>
    [Fact]
    public async Task Mid_page_icon_not_parent_return()
    {
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-icon", "", "icon", new SemanticNormalizedBounds(.05, .50, .04, .02)),
            FragmentedOccurrence("vision-row", "Lock display", "menu_item", new SemanticNormalizedBounds(.061111, .130625, .1875, .015625)),
            new[] { NavigateUpAuxiliary() })));

        Assert.DoesNotContain(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.ReturnToParent, RelatedOccurrenceId: "vision-icon" });
    }

    /// <summary>Buyer B (parent-role inheritance, runJ path): the text-less
    /// arrow icon is a VERIFIED CHILD of the structured back control (aux with
    /// real bounds containing the icon) — the composition branch must inherit
    /// the parent's return role instead of consuming the icon as a plain child
    /// (which would starve the return classification).</summary>
    [Fact]
    public async Task Icon_child_of_back_control_inherits_parent_return_role()
    {
        var navigAuxWithBounds = new SemanticObservationFact("adb-up-b", SemanticObservationFactKind.Text, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawText: "None",
            rawClassName: "android.widget.ImageButton", clickable: true,
            rawContentDescription: "Navigate up",
            bounds: new SemanticNormalizedBounds(0, .07083333, .13611111, .0765625));
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-back", "", "icon", new SemanticNormalizedBounds(.048611, .07875, .041667, .018125)),
            FragmentedOccurrence("vision-row", "Lock display", "menu_item", new SemanticNormalizedBounds(.061111, .130625, .1875, .015625)),
            new[] { navigAuxWithBounds })));

        Assert.Contains(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.ReturnToParent, RelatedOccurrenceId: "vision-back" });
    }

    /// <summary>Counterexample — an icon that is a verified child of a
    /// NON-back parent keeps ChildOf (no return-role inheritance for ordinary
    /// parents).</summary>
    [Fact]
    public async Task Icon_child_of_ordinary_parent_keeps_child_relation()
    {
        var ordinaryAux = new SemanticObservationFact("adb-row", SemanticObservationFactKind.Text, "adb",
            SemanticSourceTier.Auxiliary, "adb-capture", 1, "frame-1", rawText: "Lock display",
            rawClassName: "android.widget.LinearLayout", clickable: true,
            bounds: new SemanticNormalizedBounds(.05, .13, .30, .03),
            parentOccurrenceId: "adb-row-parent");
        var result = await new SettingsSemanticCapability().InterpretAsync(Context(Fragmented(
            FragmentedOccurrence("vision-icon", "", "icon", new SemanticNormalizedBounds(.10, .14, .03, .015)),
            FragmentedOccurrence("vision-row", "Lock display", "menu_item", new SemanticNormalizedBounds(.061111, .130625, .1875, .015625)),
            new[] { ordinaryAux })));

        Assert.Contains(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.Child, RelatedOccurrenceId: "vision-icon" });
        Assert.DoesNotContain(result, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.ReturnToParent, RelatedOccurrenceId: "vision-icon" });
    }

    private static ExternalSemanticCapabilityContext Context(params SemanticObservationFact[] facts) =>
        new(new SemanticObservationReference("observation:1", 1, "frame-1"),
            new[]
            {
                new SemanticSourceMetadata("vision", SemanticSourceTier.Primary, true, "frame-1"),
                new SemanticSourceMetadata("adb", SemanticSourceTier.Auxiliary, true, "frame-1"),
            }, facts: facts);
}
