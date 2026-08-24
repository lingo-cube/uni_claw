using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// CALLER_SOURCE_PROVENANCE_CONTRACT — PROV-1..PROV-14.
///
/// Proves the Agent-owned grounding validator semantics on real fixture
/// viewport evidence (scroll01 v1/v2/v3) and structured rows:
///   caller branch -> NavigationSourceOccurrenceReference
///   -> Agent validates occurrence -> normalizer -> run-local logical source.
/// </summary>
public sealed class SourceProvenanceContractTests
{
    private const int Width = 1080;
    private const int Height = 1920;

    // ── helpers ────────────────────────────────────────────────────────────

    private static Observation LoadScroll01(string name, long seq)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Replay/Assets/scroll01", name);
        var xml = File.ReadAllText(path);
        var structured = AdbUiHierarchySource.Parse(xml, Width, Height);
        return Primary(new Observation([], "com.uniclaw.fixture", seq) { StructuredElements = structured });
    }

    /// <summary>
    /// Mirrors the structured rows as PRIMARY Vision elements with a stamped
    /// primary source and admitted evidence (rows → NavigationCandidate,
    /// checkable/switch rows → LocalControl, non-row controls → Unknown).
    /// ADB-only occurrences are never primary; the Vision channel is the
    /// grounding authority.
    /// </summary>
    private static Observation Primary(Observation raw)
    {
        var elements = raw.StructuredElements
            .Select((s, i) => new ObservedElement(s.RawText, null, i, s.Bounds,
                s.RawText is null
                    ? null
                    : s.Checkable == true
                        ? "switch"
                        : s.Class?.Contains("LinearLayout", StringComparison.Ordinal) == true
                            ? "menuItem"
                            : "button"))
            .ToImmutableArray();
        var stamped = raw with
        {
            Elements = elements,
            Sources = ImmutableArray.Create(new ObservationSourceMetadata(
                ObservationSourceTier.PrimaryVision, true, raw.SequenceNumber,
                $"frame-{raw.SequenceNumber}", 1080, 1920, "vision", "vision")),
        };
        var context = SemanticObservationFactProjector.Project(stamped);
        var manifest = new SemanticCapabilityManifest("fixture", "1", ["navigation", "local-control"]);
        var evidence = context.Facts
            .Where(f => f.SourceTier == SemanticSourceTier.Primary
                && f.Kind == SemanticObservationFactKind.Text
                && !string.IsNullOrWhiteSpace(f.RawText)
                && !string.Equals(f.RawProviderType, "button", StringComparison.Ordinal))
            .Select(f => new SemanticEvidenceV2Envelope(
                $"e:{f.OccurrenceId}",
                new ElementAffordanceCandidateEvidence(f.OccurrenceId,
                    string.Equals(f.RawProviderType, "switch", StringComparison.Ordinal)
                        ? ElementAffordanceKind.LocalControl
                        : ElementAffordanceKind.NavigationCandidate,
                    new SemanticSymbolReference(manifest.ManifestId, manifest.Version, "navigation"),
                    context.Observation, new SemanticScopeReference(f.OccurrenceId),
                    new SemanticProvenance(f.SourceId, SemanticSourceTier.Primary, f.ProvenanceId, DateTimeOffset.UnixEpoch, f.FrameId),
                    .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue)))
            .ToImmutableArray();
        return stamped with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(evidence) };
    }

    private static StructuredElementEvidence Row(
        string title,
        string resourceId = "com.uniclaw.fixture:id/row_title",
        string @class = "android.widget.LinearLayout",
        bool checkable = false,
        bool hasSwitchChild = false,
        bool clickable = true)
        => new(Class: @class, ResourceId: resourceId, Clickable: clickable, Checkable: checkable,
            Checked: false, Enabled: true, Focusable: true,
            Bounds: new ElementBounds(0, 0, 1, 0.1f), RawText: title);

    private static Observation Observe(long seq, params StructuredElementEvidence[] rows)
        => Primary(new Observation([], "com.uniclaw.fixture", seq) { StructuredElements = rows.ToImmutableArray() });

    private static BranchSourceGroundingEvidence Grounding(
        string branch, long seq, string occurrenceLocalId)
        => new(branch, new NavigationSourceOccurrenceReference(seq, occurrenceLocalId));

    private static string ResolvedLabel(NavigationSourceOccurrence occurrence, SourceNormalizationResult normalization)
        => SourceGroundingValidator.TryResolveLogicalSource(occurrence, normalization)!;

    // ── PROV-1: valid grounding ────────────────────────────────────────────

    [Fact]
    public void PROV1_ValidGrounding_Accepts()
    {
        var v1 = LoadScroll01("v1.xml", 1);
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);
        var grounding = Grounding("Item 01", 1, "nav:1");

        var result = SourceGroundingValidator.Validate(accepted, grounding, normalization);

        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, result.Status);
        Assert.NotNull(result.CanonicalOccurrence);
        Assert.True(result.CanonicalOccurrence!.PrimarySupport);
        var raw = v1.Elements[result.CanonicalOccurrence.Reference.ElementIndex];
        Assert.Equal("Item 01", raw.Text);
    }

    // ── PROV-2: equivalent viewport occurrence -> same logical source ──────

    [Fact]
    public void PROV2_EquivalentViewportOccurrence_SameLogicalSource()
    {
        var v1 = LoadScroll01("v1.xml", 1);
        var v2 = LoadScroll01("v2.xml", 2);
        var accepted = ImmutableArray.Create(v1, v2);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);
        Assert.True(normalization.IsResolved);

        // Item 02 appears as nav:2 in v1 and nav:1 in v2 (real scroll overlap).
        var g1 = SourceGroundingValidator.Validate(accepted, Grounding("Item 02", 1, "nav:2"), normalization);
        var g2 = SourceGroundingValidator.Validate(accepted, Grounding("Item 02", 2, "nav:1"), normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, g1.Status);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, g2.Status);

        var occ1 = SourceEquivalenceNormalizer.OccurrencesOf(v1).First(o => o.OccurrenceIdentity == "nav:2");
        var occ2 = SourceEquivalenceNormalizer.OccurrencesOf(v2).First(o => o.OccurrenceIdentity == "nav:1");
        Assert.Equal(ResolvedLabel(occ1, normalization), ResolvedLabel(occ2, normalization));
    }

    // ── PROV-3: caller omission rejected ───────────────────────────────────

    [Fact]
    public void PROV3_CallerOmission_Rejected()
    {
        var v1 = LoadScroll01("v1.xml", 1);
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        // Reference to an Observation that is NOT accepted (omission of scope).
        var foreign = Grounding("Item 01", 999, "nav:1");
        var result = SourceGroundingValidator.Validate(accepted, foreign, normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Invalid, result.Status);
    }

    // ── PROV-4: caller fabrication rejected ────────────────────────────────

    [Fact]
    public void PROV4_CallerFabrication_Rejected()
    {
        var v1 = LoadScroll01("v1.xml", 1);
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        var fake = Grounding("Item 01", 1, "nav:99"); // occurrence does not exist
        var result = SourceGroundingValidator.Validate(accepted, fake, normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Invalid, result.Status);
    }

    // ── PROV-5: foreign Container rejected ─────────────────────────────────

    [Fact]
    public void PROV5_ForeignContainer_Rejected()
    {
        var v1 = LoadScroll01("v1.xml", 1);
        var v2 = LoadScroll01("v2.xml", 2);
        // Current Container only accepts v1; grounding into v2 is foreign.
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        var foreign = Grounding("Item 02", 2, "nav:1");
        var result = SourceGroundingValidator.Validate(accepted, foreign, normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Invalid, result.Status);
        _ = v2;
    }

    // ── PROV-6: previous run rejected ──────────────────────────────────────

    [Fact]
    public void PROV6_PreviousRun_Rejected()
    {
        // Same mechanism as foreign container: a sequence from a previous run
        // is not in the current run's accepted set.
        var v1 = LoadScroll01("v1.xml", 1);
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        var stale = Grounding("Item 01", 0, "nav:1"); // seq 0 not accepted
        var result = SourceGroundingValidator.Validate(accepted, stale, normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Invalid, result.Status);
    }

    // ── PROV-7: LOCAL_CONTROL rejected ─────────────────────────────────────

    [Fact]
    public void PROV7_LocalControl_Rejected()
    {
        // A Switch row is LOCAL_CONTROL -> never an occurrence.
        var obs = Observe(1,
            Row("Navigation A"),
            Row("Local Switch", "com.uniclaw.fixture:id/local_switch", "android.widget.LinearLayout",
                checkable: true, hasSwitchChild: true));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        Assert.Single(SourceEquivalenceNormalizer.OccurrencesOf(obs)); // only Navigation A
        var grounding = Grounding("Local Switch", 1, "nav:2"); // does not exist as occurrence
        var result = SourceGroundingValidator.Validate(accepted, grounding, normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Invalid, result.Status);
    }

    // ── PROV-8: UNKNOWN rejected ───────────────────────────────────────────

    [Fact]
    public void PROV8_Unknown_Rejected()
    {
        // Non-clickable / title-less element -> Unknown affordance -> no occurrence.
        var obs = Observe(1,
            Row("Navigation A"),
            Row("", clickable: false));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        Assert.Single(SourceEquivalenceNormalizer.OccurrencesOf(obs));
        var grounding = Grounding("something", 1, "nav:2");
        var result = SourceGroundingValidator.Validate(accepted, grounding, normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Invalid, result.Status);
    }

    // ── PROV-9: ambiguous equivalence blocks ───────────────────────────────

    [Fact]
    public void PROV9_AmbiguousEquivalence_Blocks()
    {
        // Duplicate complete signatures (SCROLL-02 style) -> normalization
        // unresolved -> grounding is Unresolved (fail closed).
        var obs = Observe(1,
            Row("Shared"), Row("Item A"), Row("Shared"), Row("Item B"));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        Assert.False(normalization.IsResolved);
        var grounding = Grounding("Shared", 1, "nav:1");
        var result = SourceGroundingValidator.Validate(accepted, grounding, normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Unresolved, result.Status);
    }

    // ── PROV-10: duplicate branches grounding same source rejected ─────────

    [Fact]
    public void PROV10_DuplicateBranchesSameSource_Rejected()
    {
        var v1 = LoadScroll01("v1.xml", 1);
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        var first = SourceGroundingValidator.Validate(accepted, Grounding("Item 01", 1, "nav:1"), normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, first.Status);
        var claimed = ImmutableHashSet.Create(
            ResolvedLabel(SourceEquivalenceNormalizer.OccurrencesOf(v1).First(o => o.OccurrenceIdentity == "nav:1"),
                normalization));

        // A second branch claiming the SAME logical source must be rejected.
        var second = SourceGroundingValidator.Validate(
            accepted, Grounding("Item 01 again", 1, "nav:1"), normalization, claimed);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Invalid, second.Status);
    }

    // ── PROV-11: destination UNKNOWN still grounds ─────────────────────────

    [Fact]
    public void PROV11_DestinationUnknown_StillGrounds()
    {
        // Fixture rows carry no destination identity; grounding must not require it.
        var v1 = LoadScroll01("v1.xml", 1);
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        var result = SourceGroundingValidator.Validate(accepted, Grounding("Item 01", 1, "nav:1"), normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, result.Status);
    }

    // ── PROV-12: unauthorized source remains discovered, not completed ─────

    [Fact]
    public void PROV12_UnauthorizedSource_RemainsDiscoveredNotCompleted()
    {
        var v1 = LoadScroll01("v1.xml", 1);
        var accepted = ImmutableArray.Create(v1);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        var result = SourceGroundingValidator.Validate(accepted, Grounding("Item 01", 1, "nav:1"), normalization);

        // The validator only produces grounding status/reason/canonical occurrence.
        // It never authorizes, completes, or creates GoalEvidence.
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, result.Status);
        Assert.True(result.GetType().GetProperties().All(p =>
            p.Name is nameof(SourceGroundingValidator.SourceGroundingResult.Status)
                or nameof(SourceGroundingValidator.SourceGroundingResult.Reason)
                or nameof(SourceGroundingValidator.SourceGroundingResult.CanonicalOccurrence)));
    }

    // ── PROV-13: duplicate title never merged into one fabricated source ────

    [Fact]
    public void PROV13_DuplicateTitle_NotMerged_WhenSignaturesDiffer()
    {
        // The PRIMARY equivalence key (Text|PerceptionType) cannot distinguish
        // two same-text rows: the equivalence NEVER merges them into one
        // logical source — it fails closed as ambiguous (no signature
        // guessing). Distinct resource-ids do not fabricate distinct sources.
        var obs = Observe(1,
            Row("Shared", "com.uniclaw.fixture:id/row_title"),
            Row("Shared", "com.uniclaw.fixture:id/row_title_alt"));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        Assert.False(normalization.IsResolved);
    }

    // ── PROV-14: caller cannot assert equivalence ──────────────────────────

    [Fact]
    public void PROV14_CallerCannotAssertEquivalence()
    {
        // The grounding carrier has exactly two fields: branch identity +
        // occurrence reference. No equivalence claim is representable.
        var props = typeof(BranchSourceGroundingEvidence).GetProperties()
            .Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "BranchIdentity", "SourceOccurrenceReference" }, props);
    }

    // ── PROV-2b: canonical validator resolves only accepted occurrences ─────

    [Fact]
    public void PROV2b_DispatchResolution_UniqueOccurrence_Resolves()
    {
        var v2 = LoadScroll01("v2.xml", 2);
        var accepted = ImmutableArray.Create(v2);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        // "Item 02" is the first navigation occurrence (nav:1) in v2.
        var result = SourceGroundingValidator.Validate(accepted, Grounding("Item 02", 2, "nav:1"), normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, result.Status);
        var resolved = SourceEquivalenceNormalizer.OccurrencesOf(v2).First(o => o.OccurrenceIdentity == "nav:1");
        Assert.Equal(resolved.CanonicalOccurrence.OccurrenceId, result.CanonicalOccurrence!.OccurrenceId);
    }

    [Fact]
    public void PROV2b2_DispatchResolution_Ambiguous_ReturnsNull()
    {
        // Duplicate complete signatures -> normalization unresolved -> no resolution.
        var obs = Observe(1, Row("Shared"), Row("Item A"), Row("Shared"), Row("Item B"));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);
        Assert.False(normalization.IsResolved);

        var result = SourceGroundingValidator.Validate(accepted, Grounding("Shared", 1, "nav:1"), normalization);
        Assert.NotEqual(SourceGroundingValidator.SourceGroundingStatus.Valid, result.Status);
}

}
