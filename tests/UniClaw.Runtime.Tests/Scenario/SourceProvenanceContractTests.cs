using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
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
        return new Observation([], "com.uniclaw.fixture", seq) { StructuredElements = structured };
    }

    private static StructuredElementEvidence Row(
        string title,
        string resourceId = "com.uniclaw.fixture:id/row_title",
        string @class = "android.widget.LinearLayout",
        bool checkable = false,
        bool hasSwitchChild = false,
        bool clickable = true)
        => new(@class, resourceId, clickable, checkable, false, true, true,
            new ElementBounds(0, 0, 1, 0.1f), title, null, hasSwitchChild, null, null);

    private static Observation Observe(long seq, params StructuredElementEvidence[] rows)
        => new([], "com.uniclaw.fixture", seq) { StructuredElements = rows.ToImmutableArray() };

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
        Assert.NotNull(result.SourceElementIndex);
        var raw = v1.StructuredElements[result.SourceElementIndex!.Value];
        Assert.Equal("Item 01", raw.TitleText);
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

        Assert.Equal(1, SourceEquivalenceNormalizer.OccurrencesOf(obs).Length); // only Navigation A
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
            new StructuredElementEvidence("android.widget.LinearLayout", null, false, false, false,
                true, true, new ElementBounds(0, 0, 1, 0.1f), null, null, false, null, null));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        Assert.Equal(1, SourceEquivalenceNormalizer.OccurrencesOf(obs).Length);
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

        // The validator only produces grounding status/reason/element index.
        // It never authorizes, completes, or creates GoalEvidence.
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, result.Status);
        Assert.True(result.GetType().GetProperties().All(p =>
            p.Name is nameof(SourceGroundingValidator.SourceGroundingResult.Status)
                or nameof(SourceGroundingValidator.SourceGroundingResult.Reason)
                or nameof(SourceGroundingValidator.SourceGroundingResult.SourceElementIndex)));
    }

    // ── PROV-13: duplicate title irrelevant ────────────────────────────────

    [Fact]
    public void PROV13_DuplicateTitle_NotMerged_WhenSignaturesDiffer()
    {
        // Same title, distinct resource-id -> distinct signatures -> distinct
        // logical sources; grounding to either is unambiguous.
        var obs = Observe(1,
            Row("Shared", "com.uniclaw.fixture:id/row_title"),
            Row("Shared", "com.uniclaw.fixture:id/row_title_alt"));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        Assert.True(normalization.IsResolved);
        Assert.Equal(2, normalization.UniqueSourceSignatures.Length);
        var g1 = SourceGroundingValidator.Validate(accepted, Grounding("Shared", 1, "nav:1"), normalization);
        var g2 = SourceGroundingValidator.Validate(accepted, Grounding("Shared", 1, "nav:2"), normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, g1.Status);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Valid, g2.Status);
        Assert.NotEqual(
            ResolvedLabel(SourceEquivalenceNormalizer.OccurrencesOf(obs)[0], normalization),
            ResolvedLabel(SourceEquivalenceNormalizer.OccurrencesOf(obs)[1], normalization));
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

    // ── PROV-2b: legacy dispatch resolution helper ──────────────────────────

    [Fact]
    public void PROV2b_DispatchResolution_UniqueOccurrence_Resolves()
    {
        var v2 = LoadScroll01("v2.xml", 2);
        var accepted = ImmutableArray.Create(v2);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);

        // "Item 02" is the first navigation occurrence (nav:1) in v2.
        var occurrence = SourceGroundingValidator.TryResolveOccurrenceForBranch(v2, "Item 02", normalization);
        Assert.NotNull(occurrence);
        Assert.Equal("nav:1", occurrence!.OccurrenceIdentity);
    }

    [Fact]
    public void PROV2b2_DispatchResolution_Ambiguous_ReturnsNull()
    {
        // Duplicate complete signatures -> normalization unresolved -> no resolution.
        var obs = Observe(1, Row("Shared"), Row("Item A"), Row("Shared"), Row("Item B"));
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);
        Assert.False(normalization.IsResolved);

        Assert.Null(SourceGroundingValidator.TryResolveOccurrenceForBranch(obs, "Shared", normalization));
}

}
