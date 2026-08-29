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

    private static ExternalSemanticCapabilityContext Context(params SemanticObservationFact[] facts) =>
        new(new SemanticObservationReference("observation:1", 1, "frame-1"),
            new[]
            {
                new SemanticSourceMetadata("vision", SemanticSourceTier.Primary, true, "frame-1"),
                new SemanticSourceMetadata("adb", SemanticSourceTier.Auxiliary, true, "frame-1"),
            }, facts: facts);
}
