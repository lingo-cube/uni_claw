using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// WI-NORM-B — boundary-row tolerance for source-equivalence normalization.
///
/// When a strict unique suffix-prefix overlap between two adjacent scrolling
/// viewports fails, the normalizer progressively relaxes by skipping the FIRST
/// and/or LAST row of the incoming window (a viewport-truncated boundary row)
/// and re-attempting the exact-Ordinal overlap. Skipped rows are:
///   - never added to the union (they will be captured fully in a later frame),
///   - never participate in signature comparison,
///   - always recorded explicitly as <c>boundary-truncated</c> evidence
///     (skips are never silent).
///
/// Strict matching is always attempted first; relaxation is a downgrade, not a
/// replacement, so clean-overlap behavior is byte-for-byte unchanged.
///
/// Test observations follow the <c>SourceRoleStabilityTests</c> pattern:
/// StructuredElementEvidence rows (clickable LinearLayout, exact-Ordinal
/// signature RawText|Class|ResourceId|ContentDescription).
/// </summary>
public sealed class BoundaryToleranceTests
{
    private const string RowClass = "android.widget.LinearLayout";
    private const string RowId = "opaque:id/row";

    private static StructuredElementEvidence Row(string title)
        => new(Class: RowClass, ResourceId: RowId, Clickable: true, Checkable: false,
            Checked: false, Enabled: true, Focusable: true,
            Bounds: new ElementBounds(0, 0, 1, 0.1f), RawText: title);

    private static Observation Obs(long seq, params string[] titles)
        => new([], "opaque", seq)
        {
            StructuredElements = titles.Select(Row).ToImmutableArray(),
        };

    private static string Sig(string title) => $"{title}|{RowClass}|{RowId}|";

    // ── Acceptance: strict match unchanged (regression) ──────────────────────

    [Fact]
    public void StrictOverlap_CleanWindows_NoBoundaryRecords()
    {
        // Two windows with a clean suffix-prefix overlap: behavior must be
        // identical to the pre-tolerance normalizer — resolved, no relaxation,
        // no boundary-truncation records.
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "Item 02", "Item 03", "Item 04");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Empty(result.BoundaryTruncations);
        Assert.Equal(4, result.UniqueSourceSignatures.Length);
        // Overlap evidence records the two SAME_SOURCE mappings (Item 02, Item 03).
        Assert.Equal(2, result.EquivalenceEvidence.Length);
        Assert.All(result.EquivalenceEvidence, e =>
            Assert.Equal(SourceEquivalenceKind.SameSource, e.Kind));
    }

    // ── Acceptance: top boundary skip rescues the match ──────────────────────

    [Fact]
    public void TopBoundarySkip_RescuesMatch_AndRecordsSkippedRow()
    {
        // Window B's first row (top of the incoming viewport) is a truncated
        // subtitle instead of the expected title, so strict overlap fails.
        // Skipping B[0] exposes the real overlap [Item 02, Item 03]; the match
        // succeeds and B[0] is recorded as boundary-truncated.
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "TruncatedSubtitle", "Item 02", "Item 03", "Item 04");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(0, result.UnresolvedCount);

        var record = Assert.Single(result.BoundaryTruncations);
        Assert.Equal(2, record.WindowSequence);
        Assert.Equal(0, record.SkippedIndex);                                   // top row
        Assert.Equal(Sig("TruncatedSubtitle"), record.SkippedSignature);
        Assert.Equal("boundary-truncated", record.Reason);

        // Skipped row is NOT in the union.
        Assert.DoesNotContain(result.UniqueSourceSignatures,
            s => s.StartsWith("TruncatedSubtitle|", StringComparison.Ordinal));
        Assert.Equal(4, result.UniqueSourceSignatures.Length);                  // Item 01..04
    }

    // ── Acceptance: skipped rows not in union ────────────────────────────────

    [Fact]
    public void SkippedRow_NotInUnion_AndNotInOverlapEvidence()
    {
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "TruncatedSubtitle", "Item 02", "Item 03", "Item 04");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        // Union contains only the real sources.
        Assert.DoesNotContain(result.UniqueSourceSignatures,
            s => s.StartsWith("TruncatedSubtitle|", StringComparison.Ordinal));
        // No overlap evidence entry references the skipped index 0 of window 2.
        Assert.DoesNotContain(result.EquivalenceEvidence,
            e => e.SecondOccurrenceIdentity.StartsWith("2:0", StringComparison.Ordinal));
    }

    // ── Acceptance: no match even after skipping → Unresolved ────────────────

    [Fact]
    public void NoMatchEvenAfterSkipping_StaysUnresolved()
    {
        // Completely unrelated windows: no suffix-prefix overlap exists even
        // after every boundary relaxation is attempted.
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "Alpha", "Beta", "Gamma");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.False(result.IsResolved);
        Assert.Equal(1, result.UnresolvedCount);
        // No skips are recorded when relaxation did not rescue the match.
        Assert.Empty(result.BoundaryTruncations);
    }

    // ── Acceptance: deterministic (same input → same output twice) ───────────

    [Fact]
    public void BoundaryRelaxation_IsDeterministic()
    {
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "TruncatedSubtitle", "Item 02", "Item 03", "Item 04");
        var input = ImmutableArray.Create(a, b);

        var r1 = SourceEquivalenceNormalizer.Normalize(input);
        var r2 = SourceEquivalenceNormalizer.Normalize(input);

        Assert.Equal(r1.UniqueSourceSignatures, r2.UniqueSourceSignatures);
        Assert.Equal(r1.EquivalenceEvidence, r2.EquivalenceEvidence);
        Assert.Equal(r1.BoundaryTruncations, r2.BoundaryTruncations);
        Assert.Equal(r1.IsResolved, r2.IsResolved);
    }

    // ── Acceptance: chain — top-skip records across multiple pairs ───────────
    // This is the reachable form of the "skip-first + skip-last combination":
    // each adjacent pair can contribute at most one boundary record, and under
    // the exact-Ordinal suffix-prefix model only a TOP-boundary (skip-first)
    // skip can ever rescue a failed strict match (see the architectural finding
    // documented in <see cref="BottomBoundarySkip_IsUnreachableForRescue"/>).

    [Fact]
    public void Chain_TopSkipOnMultiplePairs_RecordsEachSkip()
    {
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "GarbledTop1", "Item 02", "Item 03", "Item 04");
        var c = Obs(3, "GarbledTop2", "Item 03", "Item 04", "Item 05");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b, c));

        Assert.True(result.IsResolved);
        Assert.Equal(5, result.UniqueSourceSignatures.Length);                  // Item 01..05
        Assert.Equal(2, result.BoundaryTruncations.Length);
        Assert.All(result.BoundaryTruncations, t => Assert.Equal(0, t.SkippedIndex));
        Assert.Equal(2, result.BoundaryTruncations[0].WindowSequence);
        Assert.Equal(3, result.BoundaryTruncations[1].WindowSequence);
        Assert.All(result.BoundaryTruncations, t =>
            Assert.Equal("boundary-truncated", t.Reason));
    }

    // ── Architectural finding: bottom-boundary skip is unreachable for rescue ─
    //
    // The WorkItem lists "skip next[:-1] (bottom boundary)" and "skip
    // next[1:-1] (both)" as relaxation steps. Under the exact-Ordinal
    // SUFFIX(current)-vs-PREFIX(next) overlap used by FindUniqueSuffixPrefixOverlap,
    // a relaxation that drops a SUFFIX row of `next` cannot turn a failed strict
    // match into a success: next[:-1] is a PREFIX of next, so for any L,
    //   overlap(current, next[:-1]) at L  =>  prefix(next[:-1], L) == prefix(next, L)
    //                                       =>  overlap(current, next) at L
    //                                       =>  strict would have succeeded.
    // Hence when strict FAILS, next[:-1] also has no overlap, and next[1:-1] (a
    // prefix of next[1:]) can only succeed when next[1:] (skip-first) already
    // succeeds — so skip-both is never the winner. Only skip-FIRST (dropping
    // next's PREFIX row) can expose a match, which is exactly the realistic
    // top-truncation case in the semantic brief ("title scrolled out, only
    // subtitle left").
    //
    // These two tests pin that reality so the finding is executable and any
    // future change to the overlap directionality is caught.

    [Fact]
    public void BottomBoundarySkip_IsUnreachableForRescue_GarbledBottomBeyondOverlap_StrictSucceeds()
    {
        // A garbled BOTTOM row that is NOT part of the overlap is simply new
        // territory; strict prefix matching ignores it and succeeds. No
        // relaxation is triggered, so no boundary record is produced.
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "Item 02", "Item 03", "GarbledBottom");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Empty(result.BoundaryTruncations);                               // strict succeeded
        // The garbled bottom row enters the union as a (wrong) new source — it
        // does not break the match, and is therefore not a skip candidate.
        Assert.Contains(result.UniqueSourceSignatures,
            s => s.StartsWith("GarbledBottom|", StringComparison.Ordinal));
    }

    [Fact]
    public void BottomBoundarySkip_IsUnreachableForRescue_GarbledBottomInOverlap_AnchorMergeResolves()
    {
        // A garbled BOTTOM row that IS the overlapping element: skipping it
        // (next[:-1]) leaves a prefix that still does not match current's
        // suffix, because the overlap was supposed to run THROUGH that row.
        // Boundary-skip cannot rescue. LEADER NOTE (2026-08-29): the 3rd-tier
        // anchor merge NOW resolves this case (anchor "Item 02" + inserted
        // "Item 03-diff"). This is CORRECT behavior for the normalizer working
        // in isolation — the Python row stabilizer handles garbled text
        // upstream (mapping "Item 03-diff" → "Item 03"), making this a pure
        // repeat in production (0 insertions → anchor merge correctly rejects
        // → Unresolved). The normalizer's job is gap-filling, not text repair.
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "Item 02", "Item 03-diff");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        // Anchor merge resolves: "Item 02" anchors, "Item 03-diff" inserts.
        Assert.True(result.IsResolved);
        // Boundary truncation did NOT occur (skip-last didn't rescue; the
        // anchor merge tier handled it instead).
        Assert.Empty(result.BoundaryTruncations);
        // Verify the anchor merge was the resolution tier.
        Assert.Single(result.AnchorMerges);
    }

    // ── Acceptance: forbidden — skipping a middle row never happens ──────────
    // (Indirectly guaranteed: relaxation only ever proposes index 0 and/or the
    // last index; proven by every record above carrying SkippedIndex 0 or last.)

    [Fact]
    public void Relaxation_NeverSkipsMiddleRow()
    {
        // Construct a case where only the top row differs; the skipped index
        // must be 0 (a boundary), never an interior index.
        var a = Obs(1, "Item 01", "Item 02", "Item 03", "Item 04");
        var b = Obs(2, "Truncated", "Item 02", "Item 03", "Item 04", "Item 05");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        var record = Assert.Single(result.BoundaryTruncations);
        Assert.True(record.SkippedIndex == 0
            || record.SkippedIndex == 4,                                         // only first or last
            $"Relaxation skipped an interior index {record.SkippedIndex}.");
    }
}
