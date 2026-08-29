using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// WI-ANCHOR — neighbor-anchored merge as the third-tier fallback for
/// source-equivalence normalization.
///
/// The normalizer's merge precedence is:
///   1. Strict unique suffix-prefix overlap (unchanged behavior).
///   2. Boundary-row relaxation: skip first/last/both rows (unchanged behavior).
///   3. NEW — neighbor-anchored merge (this WorkItem).
///   4. Unresolved (fail-closed).
///
/// Neighbor-anchored merge finds window rows that EXACTLY (Ordinal) match rows
/// already in the accumulated union and uses them as anchors. Non-anchor window
/// rows are inserted between their nearest surrounding anchors. Existing union
/// elements are never deleted or reordered; at least one anchor is required, and
/// zero anchors keep the result fail-closed (Unresolved). Comparison stays
/// exact-Ordinal — no fuzzy matching is introduced.
///
/// Test observations follow the <c>BoundaryToleranceTests</c> /
/// <c>SourceRoleStabilityTests</c> pattern: StructuredElementEvidence rows
/// (clickable LinearLayout, exact-Ordinal signature
/// RawText|Class|ResourceId|ContentDescription).
/// </summary>
public sealed class AnchorMergeTests
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

    private static ImmutableArray<string> Sigs(params string[] titles)
        => titles.Select(Sig).ToImmutableArray();

    // ── 1. Basic insertion: D inserted between C and E ───────────────────────
    // Acceptance: union=[A,B,C,E] window=[C,D,E,F] → [A,B,C,D,E,F].

    [Fact]
    public void BasicInsertion_InsertsRowBetweenAnchors()
    {
        // Window 1 builds union [A,B,C,E] (strict overlap A,B,C,E is not chained
        // here because there is only ONE window so far; union == window 1). Then
        // window 2 = [C,D,E,F]: strict suffix-prefix overlap of [A,B,C,E] vs
        // [C,D,E,F] finds no suffix of union matching a prefix of window, and
        // boundary relaxation (skip first/last/both) also fails because D sits
        // in the middle. Anchor merge: C and E are anchors; D inserts between
        // them; F appends after E.
        var a = Obs(1, "A", "B", "C", "E");
        var b = Obs(2, "C", "D", "E", "F");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(0, result.UnresolvedCount);
        Assert.Equal(Sigs("A", "B", "C", "D", "E", "F"), result.UniqueSourceSignatures);

        // One anchor-merge record for window 2; no boundary truncations.
        var merge = Assert.Single(result.AnchorMerges);
        Assert.Equal(2, merge.WindowSequence);
        Assert.Equal("anchor-merge", merge.Reason);
        Assert.True(merge.AnchorCount >= 2, $"expected >=2 anchors, got {merge.AnchorCount}");
        // D and F were inserted (F appends after E since E is its below... actually
        // F has anchor E above it, so F inserts after E).
        Assert.Contains(Sig("D"), merge.InsertedSignatures);
        Assert.Contains(Sig("F"), merge.InsertedSignatures);
        Assert.Empty(result.BoundaryTruncations);
    }

    // ── 2. No anchors → Unresolved (fail-closed) ─────────────────────────────
    // Acceptance: union=[A,B,C] window=[X,Y,Z] → Unresolved (no anchors).

    [Fact]
    public void NoAnchors_StaysUnresolved()
    {
        var a = Obs(1, "A", "B", "C");
        var b = Obs(2, "X", "Y", "Z");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.False(result.IsResolved);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.Empty(result.AnchorMerges);
        Assert.Empty(result.BoundaryTruncations);
    }

    // ── 3. All anchors (subset window): narrowing rejects pure repeat ────────
    // LEADER NARROWING (2026-08-29): anchor merge requires ≥1 inserted row.
    // Pure-repeat/subset windows (0 insertions) are rejected to preserve the
    // REVISIT_COMPLETENESS_FRESHNESS_PRESSURE contract (backward views stay
    // Unresolved by anchor merge). The strict suffix-prefix path handles this
    // trivial overlap case as before.

    [Fact]
    public void AllAnchors_SubsetWindow_NarrowingRejects_StaysUnresolved()
    {
        var a = Obs(1, "A", "B", "C", "D");
        var b = Obs(2, "B", "C");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        // Pure subset/repeat (0 insertions, no new info): all three tiers
        // correctly reject → Unresolved. Strict suffix-prefix fails ([C,D]≠[B,C]),
        // boundary skip fails, anchor merge narrowed to reject pure repeats.
        // This preserves REVISIT_COMPLETENESS_FRESHNESS_PRESSURE: a window
        // with no new rows provides no new information for completeness.
        Assert.False(result.IsResolved);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.Empty(result.BoundaryTruncations);
        Assert.True(result.AnchorMerges.IsDefaultOrEmpty);
    }

    // ── 4. Insert at start: A inserted before B ──────────────────────────────
    // Acceptance: union=[B,C,D] window=[A,B,C] → [A,B,C,D].

    [Fact]
    public void InsertAtStart_InsertsBeforeFirstAnchor()
    {
        var a = Obs(1, "B", "C", "D");
        var b = Obs(2, "A", "B", "C");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(Sigs("A", "B", "C", "D"), result.UniqueSourceSignatures);
        var merge = Assert.Single(result.AnchorMerges);
        Assert.Contains(Sig("A"), merge.InsertedSignatures);
    }

    // ── 5. Insert at end: D appended after C ─────────────────────────────────
    // Acceptance: union=[A,B,C] window=[B,C,D] → [A,B,C,D].
    // NOTE: this window admits a clean strict suffix-prefix overlap (suffix
    // [B,C] == prefix [B,C]), so the STRICT tier handles it and D is appended
    // normally. The test pins the required OUTCOME ([A,B,C,D], resolved) rather
    // than forcing the anchor-merge tier — anchor merge is a fallback, never the
    // preferred path when strict succeeds.

    [Fact]
    public void InsertAtEnd_ResultEndsWithD_AndResolved()
    {
        var a = Obs(1, "A", "B", "C");
        var b = Obs(2, "B", "C", "D");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(Sigs("A", "B", "C", "D"), result.UniqueSourceSignatures);
        // D is present at the tail of the union.
        Assert.Equal(Sig("D"), result.UniqueSourceSignatures[^1]);
    }

    // ── 6. Multiple insertions interleaved with anchors ──────────────────────
    // Acceptance: union=[A,C,E,G] window=[A,B,C,D,E,F,G] → all matched/inserted.

    [Fact]
    public void MultipleInsertions_InterleavedWithAnchors()
    {
        var a = Obs(1, "A", "C", "E", "G");
        var b = Obs(2, "A", "B", "C", "D", "E", "F", "G");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(
            Sigs("A", "B", "C", "D", "E", "F", "G"),
            result.UniqueSourceSignatures);
        var merge = Assert.Single(result.AnchorMerges);
        // Anchors: A, C, E, G (4).
        Assert.Equal(4, merge.AnchorCount);
        // Inserted: B, D, F.
        Assert.Equal(
            new[] { Sig("B"), Sig("D"), Sig("F") }.OrderBy(s => s, StringComparer.Ordinal),
            merge.InsertedSignatures.OrderBy(s => s, StringComparer.Ordinal));
    }

    // ── 7. Strict path still preferred (regression) ──────────────────────────
    // A clean suffix-prefix overlap must use the STRICT path, not anchor merge.

    [Fact]
    public void StrictOverlap_CleanWindows_UsesStrictPathNotAnchorMerge()
    {
        var a = Obs(1, "Item 01", "Item 02", "Item 03");
        var b = Obs(2, "Item 02", "Item 03", "Item 04");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(0, result.UnresolvedCount);
        // Strict path produces overlap evidence, no anchor merges, no truncations.
        Assert.Empty(result.AnchorMerges);
        Assert.Empty(result.BoundaryTruncations);
        Assert.Equal(4, result.UniqueSourceSignatures.Length);
        Assert.Equal(2, result.EquivalenceEvidence.Length);
        Assert.All(result.EquivalenceEvidence, e =>
            Assert.Equal(SourceEquivalenceKind.SameSource, e.Kind));
        // Strict-path evidence reason is the overlap reason, NOT the anchor reason.
        Assert.All(result.EquivalenceEvidence, e =>
            Assert.DoesNotContain("Anchor", e.Reason, StringComparison.Ordinal));
    }

    // ── 8. Determinism: same input → same output twice ───────────────────────
    // NOTE: AnchorMergeRecord carries an ImmutableArray<string> field; record
    // equality compares ImmutableArray by reference, not contents, so the merge
    // records are compared field-by-field here (the sequences themselves are
    // compared by contents via Assert.Equal on the ImmutableArray<string>).

    [Fact]
    public void AnchorMerge_IsDeterministic()
    {
        var a = Obs(1, "A", "B", "C", "E");
        var b = Obs(2, "C", "D", "E", "F");
        var input = ImmutableArray.Create(a, b);

        var r1 = SourceEquivalenceNormalizer.Normalize(input);
        var r2 = SourceEquivalenceNormalizer.Normalize(input);

        Assert.True(r1.IsResolved && r2.IsResolved);
        Assert.Equal(r1.UniqueSourceSignatures, r2.UniqueSourceSignatures);
        Assert.Equal(r1.EquivalenceEvidence, r2.EquivalenceEvidence);
        Assert.Equal(r1.BoundaryTruncations, r2.BoundaryTruncations);
        // Anchor merges: compare scalar fields + inserted-signature contents.
        Assert.Equal(r1.AnchorMerges.Length, r2.AnchorMerges.Length);
        for (int i = 0; i < r1.AnchorMerges.Length; i++)
        {
            var m1 = r1.AnchorMerges[i];
            var m2 = r2.AnchorMerges[i];
            Assert.Equal(m1.WindowSequence, m2.WindowSequence);
            Assert.Equal(m1.AnchorCount, m2.AnchorCount);
            Assert.Equal(m1.Reason, m2.Reason);
            Assert.Equal(m1.InsertedSignatures, m2.InsertedSignatures);
        }
    }

    // ── Guardrail: existing union elements never deleted or reordered ─────────
    // The union prefix [A,B,C,E] must remain a contiguous prefix of the result
    // (in original order), with inserted rows slotted between anchors only.

    [Fact]
    public void AnchorMerge_PreservesExistingUnionOrderAndElements()
    {
        var a = Obs(1, "A", "B", "C", "E");
        var b = Obs(2, "C", "D", "E", "F");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        var sigs = result.UniqueSourceSignatures;
        // Original union rows A,B,C,E must appear in the result in that exact
        // relative order (no reordering, no deletion).
        var originalOrder = Sigs("A", "B", "C", "E");
        int cursor = 0;
        foreach (var expected in originalOrder)
        {
            int idx = -1;
            for (int j = cursor; j < sigs.Length; j++)
            {
                if (string.Equals(sigs[j], expected, StringComparison.Ordinal))
                {
                    idx = j;
                    break;
                }
            }
            Assert.True(idx >= 0,
                $"Original union element {expected} was deleted or reordered (cursor={cursor}).");
            cursor = idx + 1;
        }
    }

    // ── Guardrail: anchor merge produces SAME_SOURCE equivalence evidence ─────
    // Each anchor yields an equivalence evidence entry linking the prior-window
    // union occurrence to the current-window anchor occurrence.

    [Fact]
    public void AnchorMerge_RecordsSameSourceEvidenceForAnchors()
    {
        var a = Obs(1, "A", "B", "C", "E");
        var b = Obs(2, "C", "D", "E", "F");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        // Anchors C and E each produce one SAME_SOURCE evidence entry.
        var anchorEvidence = result.EquivalenceEvidence
            .Where(e => e.Reason.Contains("Anchor", StringComparison.Ordinal))
            .ToList();
        Assert.True(anchorEvidence.Count >= 2,
            $"expected >=2 anchor evidence entries, got {anchorEvidence.Count}");
        Assert.All(anchorEvidence, e =>
            Assert.Equal(SourceEquivalenceKind.SameSource, e.Kind));
        Assert.All(anchorEvidence, e =>
            Assert.StartsWith("2:", e.SecondOccurrenceIdentity, StringComparison.Ordinal));
    }

    // ── 9. WI-FIX: multi-row insertion between SAME anchor pair keeps window order ─
    // Bug: when multiple new rows go between the SAME pair of anchors, each
    // row's insertAt was computed from already-shifted anchor indices, so a
    // later window row could land BEFORE an earlier one (reversed order).
    // Fix: track lastInsertPos and force each subsequent insertion at or after
    // it, so insertion order == window order.
    //
    // Acceptance (WI-FIX): union=[A,E] window=[A,B,C,D,E] → [A,B,C,D,E]
    // (NOT a reversed order like [A,D,C,B,E]). B,C,D all go between the single
    // anchor pair A,E, so this is the minimal case that triggers the bug.

    [Fact]
    public void MultiRowSameAnchorPair_PreservesWindowOrder()
    {
        var a = Obs(1, "A", "E");
        var b = Obs(2, "A", "B", "C", "D", "E");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(Sigs("A", "B", "C", "D", "E"), result.UniqueSourceSignatures);
        var merge = Assert.Single(result.AnchorMerges);
        // Anchors: A, E (2). Inserted: B, C, D — in window order.
        Assert.Equal(2, merge.AnchorCount);
        Assert.Equal(
            new[] { Sig("B"), Sig("C"), Sig("D") },
            merge.InsertedSignatures);
    }

    // ── 10. WI-FIX: explicit acceptance case (different anchor pairs) ─────────
    // Acceptance (WI-FIX): union=[A,C,E] window=[A,B,C,D,E] → [A,B,C,D,E].
    // B lands between A,C and D between C,E (different pairs), so this case
    // already ordered correctly pre-fix; pinned here as a regression guard that
    // the monotonic guard did not disturb the already-correct path.

    [Fact]
    public void MultiRowDifferentAnchorPairs_PreservesWindowOrder()
    {
        var a = Obs(1, "A", "C", "E");
        var b = Obs(2, "A", "B", "C", "D", "E");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        Assert.Equal(Sigs("A", "B", "C", "D", "E"), result.UniqueSourceSignatures);
    }

    // ── 11. WI-FIX: B explicitly before D (the named anti-regression) ──────────
    // The WorkItem names the forbidden outcome [A,D,B,C,E]. This pins that B
    // appears before D in the result regardless of anchor-pair structure.

    [Fact]
    public void MultiRowInsertion_BeforeIsBeforeD_NotReversed()
    {
        var a = Obs(1, "A", "C", "E");
        var b = Obs(2, "A", "B", "C", "D", "E");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(a, b));

        Assert.True(result.IsResolved);
        var sigs = result.UniqueSourceSignatures;
        int bIdx = IndexOf(sigs, Sig("B"));
        int dIdx = IndexOf(sigs, Sig("D"));
        Assert.True(bIdx >= 0 && dIdx >= 0, "B and D must both be present.");
        Assert.True(bIdx < dIdx, $"B must appear before D (bIdx={bIdx}, dIdx={dIdx}).");
    }

    // ── SOURCE_NORMALIZATION_ANCHOR_ADJACENT_CONFIRMATION_REPAIR_GATE ───────
    // An anchor merge may preserve an earlier signature between the latest
    // window's anchors. The accumulated union then intentionally contains both
    // signatures, so the latest accepted window is not necessarily its suffix.
    // One immediately adjacent, exact repeat of that latest window is the
    // settled confirmation for the anchor-resolved observation, not a general
    // revisit. It must add no source and must retain exact SAME_SOURCE evidence.

    [Fact]
    public void AnchorMerge_ImmediateExactConfirmation_ResolvesWithoutGrowingUnion()
    {
        var before = Obs(25, "A", "B", "C", "PriorRole", "D");
        var mergedWindow = Obs(28, "B", "C", "CurrentRole", "D", "E");
        var confirmation = Obs(31, "B", "C", "CurrentRole", "D", "E");

        var anchorOnly = SourceEquivalenceNormalizer.Normalize(
            ImmutableArray.Create(before, mergedWindow));
        var result = SourceEquivalenceNormalizer.Normalize(
            ImmutableArray.Create(before, mergedWindow, confirmation));

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

    [Fact]
    public void AnchorMerge_NonAdjacentRepeat_DoesNotReuseConfirmationException()
    {
        var before = Obs(25, "A", "B", "C", "PriorRole", "D");
        var mergedWindow = Obs(28, "B", "C", "CurrentRole", "D", "E");
        var immediateConfirmation = Obs(31, "B", "C", "CurrentRole", "D", "E");
        var laterRepeat = Obs(34, "B", "C", "CurrentRole", "D", "E");

        var result = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(
            before, mergedWindow, immediateConfirmation, laterRepeat));

        Assert.False(result.IsResolved);
        Assert.Equal(1, result.UnresolvedCount);
        Assert.Empty(result.AnchorMerges);
    }

    private static int IndexOf(ImmutableArray<string> sigs, string value)
    {
        for (int i = 0; i < sigs.Length; i++)
            if (string.Equals(sigs[i], value, StringComparison.Ordinal))
                return i;
        return -1;
    }
}
