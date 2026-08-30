using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// SOURCE_NORMALIZER_LOGICAL_ORDER_RECONCILIATION_REPAIR_GATE — captured
/// falsifier, counterexample preservation, and the serialization-permutation
/// property for the logical-order projection in
/// <see cref="SourceEquivalenceNormalizer"/>.
///
/// Invariants under proof:
///   SERIALIZATION_ORDER != LOGICAL_UI_ORDER — upstream perception emits
///   observation.Elements in detector/fusion serialization order, which is not
///   guaranteed top-to-bottom UI order. The order-sensitive merge predicates
///   (suffix-prefix overlap, anchor monotonicity) protect LOGICAL forward order;
///   the projection aligns their operand with their intent.
///   BOUNDS_ORDERING != BOUNDS_IDENTITY — bounds order the projection ONLY;
///   SameSource identity remains the exact structured signature
///   (StableKey|PerceptionType for Vision). Bounds are never identity.
///
/// The projection is ORDER-ONLY: same signatures, same filters, same in-window
/// duplicate check, same union/completeness semantics; original
/// Observation/canonical occurrence order untouched.
/// </summary>
public sealed class LogicalOrderProjectionReconciliationTests
{
    private const string VisionSourceId = "vision";
    private const string RowClass = "android.widget.LinearLayout";
    private const string RowId = "opaque:id/row";

    private sealed record RowSpec(string StableKey, string Text, string Type, float Y1, float Y2);

    // ── Vision observation builder (mirrors ExplicitPrimary in the Scenario suite) ──

    private static Observation Vision(long sequence, params RowSpec[] rows)
    {
        var frame = $"frame-{sequence}";
        var elements = ImmutableArray.CreateRange(rows.Select((row, i) => new ObservedElement(
            row.Text, null, i, new ElementBounds(0f, row.Y1, 1f, row.Y2), row.Type)
        {
            StableKey = row.StableKey,
        }));
        var observation = new Observation(elements, "com.uniclaw.fixture", sequence)
        {
            Sources =
            [
                new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, sequence, frame, 100, 100, VisionSourceId, VisionSourceId),
            ],
        };
        var envelopes = ImmutableArray.CreateRange(rows.Select((row, i) =>
        {
            var occurrenceId = SemanticObservationFactProjector.CreateOccurrenceId(VisionSourceId, i.ToString());
            var reference = new SemanticObservationReference($"observation:{sequence}", sequence, frame);
            var candidate = new ElementAffordanceCandidateEvidence(
                occurrenceId,
                ElementAffordanceKind.NavigationCandidate,
                new SemanticSymbolReference("fixture", "1", "navigation"),
                reference,
                new SemanticScopeReference(occurrenceId),
                new SemanticProvenance(VisionSourceId, SemanticSourceTier.Primary, VisionSourceId, DateTimeOffset.UnixEpoch, frame),
                .9,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.MaxValue);
            return new SemanticEvidenceV2Envelope($"e:{occurrenceId}", candidate);
        }));
        return observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(envelopes) };
    }

    private static string Sig(string stableKey, string type) => $"{stableKey}|{type}||";

    // ── Structured observation builder (anchor/boundary suite pattern) ─────────

    private static StructuredElementEvidence Row(string title)
        => new(Class: RowClass, ResourceId: RowId, Clickable: true, Checkable: false,
            Checked: false, Enabled: true, Focusable: true,
            Bounds: new ElementBounds(0, 0, 1, 0.1f), RawText: title);

    private static Observation Obs(long seq, params string[] titles)
        => new([], "opaque", seq)
        {
            StructuredElements = titles.Select(Row).ToImmutableArray(),
        };

    private static string StructuredSig(string title) => $"{title}|{RowClass}|{RowId}|";

    // ══════════════════════════════════════════════════════════════════════════
    // 1. CAPTURED FALSIFIER: seq22 → seq25 (real repair run-1 geometry)
    //    Representation reorder only → must RESOLVE; union grows by 4 sources.
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Seq22ToSeq25_RepresentationReorderOnly_ResolvesWithFourNewSources()
    {
        // Canonical (admitted) signature arrays in the exact run-1 array order;
        // bounds are the REAL captured element bounds (CenterY dates from
        // /tmp/p26-projection-repair-r1-frames.json). row_010 (Display toolbar
        // band) is a partial-width, mid-array element in seq22 and a full-width
        // FIRST element in seq25 — pure perception serialization reorder, no
        // logical-order change.
        var seq22 = Vision(22,
            new RowSpec("row_020", "Brightness level", "text_block", 0.331875f, 0.373125f),
            new RowSpec("row_022", "Lock screen", "text_block", 0.475000f, 0.492500f),
            new RowSpec("row_023", "Screen timeout", "text_block", 0.561250f, 0.578750f),
            new RowSpec("row_010", "Display", "menu_item", 0.187500f, 0.226875f),
            new RowSpec("row_019", "Brightness", "menu_item", 0.282500f, 0.299375f),
            new RowSpec("row_020", "Brightness level", "menu_item", 0.333125f, 0.355625f),
            new RowSpec("row_021", "Lock display", "menu_item", 0.425625f, 0.441250f),
            new RowSpec("row_030", "Show all notification content", "menu_item", 0.501875f, 0.515625f),
            new RowSpec("row_022", "Lock screen", "NonInteractive", 0.473125f, 0.495000f),
            new RowSpec("row_023", "Screen timeout", "menu_item", 0.561250f, 0.601875f),
            new RowSpec("row_025", "Appearance", "menu_item", 0.653125f, 0.669375f),
            new RowSpec("row_031", "Dark theme", "menu_item", 0.703125f, 0.720625f),
            new RowSpec("row_027", "Display size and text", "menu_item", 0.788750f, 0.810625f),
            new RowSpec("row_028", "Color", "menu_item", 0.860625f, 0.871875f),
            new RowSpec("row_032", "Colors", "menu_item", 0.909375f, 0.927500f));
        var seq25 = Vision(25,
            new RowSpec("row_010", "Display", "menu_item", 0.062500f, 0.117500f),
            new RowSpec("row_020", "Brightness level", "text_block", 0.186875f, 0.207500f),
            new RowSpec("row_022", "Lock screen", "text_block", 0.329375f, 0.346250f),
            new RowSpec("row_023", "Screen timeout", "text_block", 0.415000f, 0.431875f),
            new RowSpec("row_027", "Display size and text", "text_block", 0.643125f, 0.664375f),
            new RowSpec("row_019", "Brightness", "menu_item", 0.136875f, 0.153125f),
            new RowSpec("row_020", "Brightness level", "menu_item", 0.186875f, 0.207500f),
            new RowSpec("row_021", "Lock display", "menu_item", 0.279375f, 0.295625f),
            new RowSpec("row_022", "Lock screen", "menu_item", 0.329375f, 0.346250f),
            new RowSpec("row_023", "Screen timeout", "menu_item", 0.415000f, 0.431875f),
            new RowSpec("row_025", "Appearance", "menu_item", 0.507500f, 0.523125f),
            new RowSpec("row_031", "Dark theme", "menu_item", 0.556875f, 0.574375f),
            new RowSpec("row_027", "Display size and text", "menu_item", 0.643125f, 0.664375f),
            new RowSpec("row_028", "Color", "menu_item", 0.713750f, 0.726875f),
            new RowSpec("row_032", "Colors", "menu_item", 0.763125f, 0.781250f),
            new RowSpec("row_035", "Color contrast", "menu_item", 0.828125f, 0.868750f),
            new RowSpec("row_034", "Other display controls", "menu_item", 0.920625f, 0.936875f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(seq22, seq25));

        // Pair 1 must resolve via the anchor tier with EXACTLY the 4 genuinely
        // new logical sources (row_022|menu_item, row_027|text_block,
        // row_035, row_034) and 13 anchors.
        Assert.True(result.IsResolved);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Empty(result.BoundaryTruncations);
        var merge = Assert.Single(result.AnchorMerges);
        Assert.Equal(13, merge.AnchorCount);
        Assert.Equal(4, merge.InsertedSignatures.Length);
        Assert.Contains(Sig("row_022", "menu_item"), merge.InsertedSignatures);
        Assert.Contains(Sig("row_027", "text_block"), merge.InsertedSignatures);
        Assert.Contains(Sig("row_035", "menu_item"), merge.InsertedSignatures);
        Assert.Contains(Sig("row_034", "menu_item"), merge.InsertedSignatures);
        Assert.Equal(19, result.UniqueSourceSignatures.Length); // 15 + 4
        // The full expected union (projected logical order for the shared rows).
        Assert.Equal(
            [
                Sig("row_010", "menu_item"), Sig("row_019", "menu_item"),
                Sig("row_020", "text_block"), Sig("row_020", "menu_item"),
                Sig("row_021", "menu_item"), Sig("row_022", "text_block"),
                Sig("row_022", "menu_item"), Sig("row_022", "NonInteractive"),
                Sig("row_030", "menu_item"), Sig("row_023", "text_block"),
                Sig("row_023", "menu_item"), Sig("row_025", "menu_item"),
                Sig("row_031", "menu_item"), Sig("row_027", "text_block"),
                Sig("row_027", "menu_item"), Sig("row_028", "menu_item"),
                Sig("row_032", "menu_item"), Sig("row_035", "menu_item"),
                Sig("row_034", "menu_item"),
            ],
            result.UniqueSourceSignatures);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. TRUE BACKWARD SCROLL → fail-closed (REVISIT_COMPLETENESS_FRESHNESS_PRESSURE)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TrueBackwardScroll_RevisitedRowsOnly_StaysUnresolved()
    {
        var forward = Vision(1,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f),
            new RowSpec("row_030", "Lock display", "menu_item", 0.30f, 0.32f),
            new RowSpec("row_040", "Screen timeout", "menu_item", 0.40f, 0.42f),
            new RowSpec("row_050", "Appearance", "menu_item", 0.50f, 0.52f));
        // Backward scroll view shows previously-seen rows ABOVE the current
        // viewport; zero genuinely new sources. Even after the logical-order
        // projection (rows sort spatially ascending), every row is an anchor and
        // the anchor tier's zero-insertion guard keeps it fail-closed.
        var backward = Vision(2,
            new RowSpec("row_030", "Lock display", "menu_item", 0.30f, 0.32f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f),
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(forward, backward));

        Assert.False(result.IsResolved);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.True(result.AnchorMerges.IsDefaultOrEmpty);
        Assert.Empty(result.BoundaryTruncations);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. PURE REPEAT claiming progress → fail-closed (anchor narrowing, no new rows)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PureRepeat_NoNewRows_StaysUnresolved()
    {
        var full = Vision(1,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f),
            new RowSpec("row_030", "Lock display", "menu_item", 0.30f, 0.32f),
            new RowSpec("row_040", "Screen timeout", "menu_item", 0.40f, 0.42f),
            new RowSpec("row_050", "Appearance", "menu_item", 0.50f, 0.52f));
        // A non-suffix slice of previously-seen rows carries NO new source: all
        // three tiers must reject (strict/boundary find no suffix-prefix match;
        // anchor tier finds 0 insertions).
        var repeat = Vision(2,
            new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f),
            new RowSpec("row_040", "Screen timeout", "menu_item", 0.40f, 0.42f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(full, repeat));

        Assert.False(result.IsResolved);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.True(result.AnchorMerges.IsDefaultOrEmpty);
        Assert.Empty(result.BoundaryTruncations);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. AMBIGUOUS same-Y / overlapping rows → fail-closed, never conflated
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SameYDupRows_SameSignature_StaysUnresolved()
    {
        // Two rows claiming the SAME StableKey + type + band yield the SAME
        // signature: in-frame ambiguity must fail closed (never merge by guess).
        var v1 = Vision(1,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f));
        var v2 = Vision(2,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.False(result.IsResolved);
        Assert.Equal(1, result.UnresolvedCount);
    }

    [Fact]
    public void SameYOverlappingDistinctRows_AreNotConflated()
    {
        // Distinct logical rows sharing the same Y band must stay DISTINCT
        // entries (band ordering never collapses or merges them; identity is
        // signature-only and count is preserved).
        var v1 = Vision(1,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.10f, 0.12f));
        var v2 = Vision(2,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.10f, 0.12f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.True(result.IsResolved);
        Assert.Equal(2, result.UniqueSourceSignatures.Length);
        Assert.Contains(Sig("row_010", "menu_item"), result.UniqueSourceSignatures);
        Assert.Contains(Sig("row_020", "menu_item"), result.UniqueSourceSignatures);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5. SAME TEXT, DIFFERENT logical rows → must NEVER merge
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SameText_DifferentLogicalRows_DoNotMerge()
    {
        // "Display" appears on two distinct logical rows (row_010 vs row_099).
        // Only the StableKey differs — text is never identity. Serialization of
        // the second frame is REVERSED; the projection must keep the two rows
        // distinct and pair each row with itself.
        var v1 = Vision(1,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_099", "Display", "menu_item", 0.20f, 0.22f));
        var v2 = Vision(2,
            new RowSpec("row_099", "Display", "menu_item", 0.20f, 0.22f),
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.True(result.IsResolved);
        Assert.Equal([Sig("row_010", "menu_item"), Sig("row_099", "menu_item")],
            result.UniqueSourceSignatures);
        Assert.Equal(2, result.EquivalenceEvidence.Length);
        Assert.All(result.EquivalenceEvidence, e =>
            Assert.Equal(SourceEquivalenceKind.SameSource, e.Kind));
        // Each row is paired with itself across the frames — no cross-row merging.
        Assert.Contains(result.EquivalenceEvidence,
            e => e.FirstOccurrenceIdentity == "1:0" && e.SecondOccurrenceIdentity == "2:0");
        Assert.Contains(result.EquivalenceEvidence,
            e => e.FirstOccurrenceIdentity == "1:1" && e.SecondOccurrenceIdentity == "2:1");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 6. DIFFERENT SCROLL OFFSETS, preserved logical order → deterministic
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DifferentScrollOffsets_PreservedLogicalOrder_Deterministic()
    {
        var baseFrame = Vision(1,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f),
            new RowSpec("row_030", "Lock display", "menu_item", 0.30f, 0.32f));
        // Same rows, every band shifted +0.15 (viewport translated DOWN): the
        // logical relative order is preserved; the result must be identical.
        var shifted = Vision(2,
            new RowSpec("row_010", "Display", "menu_item", 0.25f, 0.27f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.35f, 0.37f),
            new RowSpec("row_030", "Lock display", "menu_item", 0.45f, 0.47f));
        var unshifted = Vision(2,
            new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f),
            new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f),
            new RowSpec("row_030", "Lock display", "menu_item", 0.30f, 0.32f));

        var shiftedResult = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(baseFrame, shifted));
        var unshiftedResult = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(baseFrame, unshifted));

        Assert.True(shiftedResult.IsResolved);
        Assert.True(unshiftedResult.IsResolved);
        Assert.Equal(unshiftedResult.UniqueSourceSignatures, shiftedResult.UniqueSourceSignatures);
        Assert.Equal(
            [Sig("row_010", "menu_item"), Sig("row_020", "menu_item"), Sig("row_030", "menu_item")],
            shiftedResult.UniqueSourceSignatures);
        Assert.Equal(unshiftedResult.EquivalenceEvidence, shiftedResult.EquivalenceEvidence);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 7. DUPLICATE REPRESENTATIONS of the same row → count/identity unchanged
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DuplicateRepresentations_SameRow_CountAndIdentityUnchanged()
    {
        // The same logical row appears as BOTH menu_item and text_block
        // representations (dual-input). Grouping must NOT collapse them: source
        // count stays 2 and each signature is byte-identical (no bounds leak).
        var v1 = Vision(1,
            new RowSpec("row_020", "Brightness level", "text_block", 0.33f, 0.35f),
            new RowSpec("row_020", "Brightness level", "menu_item", 0.34f, 0.36f));
        var v2 = Vision(2,
            new RowSpec("row_020", "Brightness level", "text_block", 0.33f, 0.35f),
            new RowSpec("row_020", "Brightness level", "menu_item", 0.34f, 0.36f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.True(result.IsResolved);
        Assert.Equal(2, result.UniqueSourceSignatures.Length);
        Assert.Contains(Sig("row_020", "text_block"), result.UniqueSourceSignatures);
        Assert.Contains(Sig("row_020", "menu_item"), result.UniqueSourceSignatures);
        // Identity is byte-identical to the exact structured signature — no
        // bounds / coordinate fragment may leak into identity.
        Assert.All(result.UniqueSourceSignatures, s => Assert.Equal(4, s.Split('|').Length));
        // Projected logical order: both representations keep their original
        // element-index order within the shared row group (text_block idx0
        // before menu_item idx1) — grouping orders, never identifies.
        string[] expectedFragments = [Sig("row_020", "text_block"), Sig("row_020", "menu_item")];
        Assert.Equal(expectedFragments, result.UniqueSourceSignatures.ToArray());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 8b. BOUNDARY-PLAUSIBILITY GATE regression (QuiescenceAdmission S3/S11 shape):
    //     a GENUINE mid-page row surfaced at the projected head must NOT be
    //     boundary-truncated as viewport garbage — the anchor tier places it.
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MidPageNewRowAtProjectedHead_IsNotBoundaryTruncated()
    {
        // Pre-scroll viewport carries only the lower region (row_fill, center
        // 0.85). The post-scroll viewport reveals a genuine NEW row ABOVE it
        // (row_new, center 0.25) plus row_fill. Under the logical-order
        // projection row_new is the window head; it is NOT at the viewport top
        // band, so the boundary tier must NOT truncate it — the anchor tier
        // must insert it BEFORE row_fill (union grows upward, no source lost).
        var preScroll = Vision(1,
            new RowSpec("row_fill", "Fill row", "menu_item", 0.80f, 0.90f));
        var postScroll = Vision(2,
            new RowSpec("row_new", "New top row", "menu_item", 0.20f, 0.30f),
            new RowSpec("row_fill", "Fill row", "menu_item", 0.80f, 0.90f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(preScroll, postScroll));

        Assert.True(result.IsResolved);
        Assert.Empty(result.BoundaryTruncations); // row_new was never "truncated"
        Assert.Equal(
            [Sig("row_new", "menu_item"), Sig("row_fill", "menu_item")],
            result.UniqueSourceSignatures);
        Assert.Single(result.AnchorMerges);
    }

    [Fact]
    public void TopBandGarbageRow_StillBoundaryTruncated()
    {
        // A row whose band IS at the spatial top edge (center 0.05) remains a
        // plausible viewport truncation: the boundary tier keeps its legacy
        // skip behavior for genuine top-edge garbage.
        var preScroll = Vision(1,
            new RowSpec("row_01", "Item 01", "menu_item", 0.10f, 0.20f),
            new RowSpec("row_02", "Item 02", "menu_item", 0.30f, 0.40f),
            new RowSpec("row_03", "Item 03", "menu_item", 0.50f, 0.60f));
        var postScroll = Vision(2,
            new RowSpec("row_top", "TruncatedTitle", "menu_item", 0.0f, 0.10f),
            new RowSpec("row_02", "Item 02", "menu_item", 0.30f, 0.40f),
            new RowSpec("row_03", "Item 03", "menu_item", 0.50f, 0.60f),
            new RowSpec("row_04", "Item 04", "menu_item", 0.70f, 0.80f));

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(preScroll, postScroll));

        Assert.True(result.IsResolved);
        var record = Assert.Single(result.BoundaryTruncations);
        Assert.Equal(0, record.SkippedIndex);
        Assert.Equal("row_top|menu_item||", record.SkippedSignature);
        Assert.DoesNotContain(result.UniqueSourceSignatures,
            s => s.StartsWith("row_top|", StringComparison.Ordinal));
        Assert.Equal(4, result.UniqueSourceSignatures.Length); // Item 01..04
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 8. EXISTING anchor-adjacent confirmation repair stays GREEN
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AnchorAdjacent_ImmediateExactConfirmation_StillResolvesWithoutGrowingUnion()
    {
        var before = Obs(25, "A", "B", "C", "PriorRole", "D");
        var mergedWindow = Obs(28, "B", "C", "CurrentRole", "D", "E");
        var confirmation = Obs(31, "B", "C", "CurrentRole", "D", "E");

        var anchorOnly = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(before, mergedWindow));
        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(before, mergedWindow, confirmation));

        Assert.True(anchorOnly.IsResolved);
        Assert.True(result.IsResolved);
        Assert.Equal(anchorOnly.UniqueSourceSignatures, result.UniqueSourceSignatures);
        Assert.Equal(7, result.UniqueSourceSignatures.Length);
        Assert.Single(result.AnchorMerges);
        var confirmationEvidence = result.EquivalenceEvidence
            .Where(e => e.FirstOccurrenceIdentity.StartsWith("28:", StringComparison.Ordinal)
                && e.SecondOccurrenceIdentity.StartsWith("31:", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(5, confirmationEvidence.Length);
        Assert.All(confirmationEvidence, e =>
            Assert.Equal(SourceEquivalenceKind.SameSource, e.Kind));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 9. PERMUTATION PROPERTY (Stability Property Suite candidate)
    //    Same logical sources, only serialization permutation, geometry/identity
    //    evidence unchanged → normalization result must be identical.
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PermutationProperty_SerializationOrderDoesNotChangeResult()
    {
        var rowA = new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f);
        var rowB = new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f);
        var rowC = new RowSpec("row_030", "Lock display", "menu_item", 0.30f, 0.32f);
        var rowD = new RowSpec("row_040", "Screen timeout", "menu_item", 0.40f, 0.42f);
        var rowE = new RowSpec("row_050", "Appearance", "menu_item", 0.50f, 0.52f);

        var frameA = Vision(1, rowA, rowB, rowC, rowD, rowE);
        var frameB_Ordered = Vision(2, rowA, rowB, rowC, rowD, rowE);
        var frameB_Perm1 = Vision(2, rowC, rowA, rowE, rowB, rowD);
        var frameB_Perm2 = Vision(2, rowB, rowE, rowD, rowA, rowC);

        var ordered = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(frameA, frameB_Ordered));
        var perm1 = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(frameA, frameB_Perm1));
        var perm2 = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(frameA, frameB_Perm2));

        Assert.True(ordered.IsResolved);
        Assert.True(perm1.IsResolved);
        Assert.True(perm2.IsResolved);
        var expected = new[]
        {
            Sig("row_010", "menu_item"), Sig("row_020", "menu_item"),
            Sig("row_030", "menu_item"), Sig("row_040", "menu_item"),
            Sig("row_050", "menu_item"),
        };
        Assert.Equal(expected, ordered.UniqueSourceSignatures);
        Assert.Equal(expected, perm1.UniqueSourceSignatures);
        Assert.Equal(expected, perm2.UniqueSourceSignatures);
        Assert.Equal(perm1.UniqueSourceSignatures, perm2.UniqueSourceSignatures);
        Assert.Equal(perm1.EquivalenceEvidence, perm2.EquivalenceEvidence);
        Assert.Equal(5, perm1.EquivalenceEvidence.Length); // strict overlap must be the tier
        Assert.Empty(perm1.AnchorMerges);
        Assert.Empty(perm1.BoundaryTruncations);
    }

    [Fact]
    public void Projection_IsDeterministic()
    {
        var rowA = new RowSpec("row_010", "Display", "menu_item", 0.10f, 0.12f);
        var rowB = new RowSpec("row_020", "Brightness", "menu_item", 0.20f, 0.22f);
        var rowC = new RowSpec("row_030", "Lock display", "menu_item", 0.30f, 0.32f);
        var input = ImmutableArray.Create(Vision(1, rowA, rowB, rowC), Vision(2, rowC, rowA, rowB));

        var r1 = SourceEquivalenceNormalizer.Normalize(input);
        var r2 = SourceEquivalenceNormalizer.Normalize(input);

        Assert.Equal(r1.IsResolved, r2.IsResolved);
        Assert.Equal(r1.UniqueSourceSignatures, r2.UniqueSourceSignatures);
        Assert.Equal(r1.EquivalenceEvidence, r2.EquivalenceEvidence);
        Assert.Equal(r1.BoundaryTruncations, r2.BoundaryTruncations);
    }
}