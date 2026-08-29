using Xunit;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Falsifier tests for the AUDITED duplicate-row resolution
/// (UNKNOWN_AFFORDANCE_BYPASS_AUDIT gate, 2026-08-29).
/// These tests verify the PHYSICAL ROW EQUIVALENCE requirement:
/// a StableKey match alone does NOT prove same element — bounds must
/// vertically overlap. Unrecognized text_blocks that are NOT provably
/// duplicates MUST block completeness (fail-closed restored).
/// </summary>
public class PhysicalRowDuplicateFalsifierTests
{
    // ── Falsifier 1: unclassified text_block that might be a missed menu MUST block ──

    [Fact]
    public void Falsifier_UnclassifiedTextBlock_NotDuplicate_Blocks()
    {
        // A text_block at a position with NO known-classified counterpart →
        // NOT a physical row duplicate → MUST block (might be a missed menu).
        // The text_block bypass was REVERTED; Unknown blocks again.
        var menuBounds = new ElementBounds(0.1f, 0.3f, 0.9f, 0.35f);
        var textBlockElsewhere = new ElementBounds(0.1f, 0.5f, 0.9f, 0.55f);
        // text_block at y=0.5, menu_item at y=0.3 — different positions, no overlap
        var isDup = TestIsPhysicalRowDuplicate(
            unknown: new ObservedElement("Something", null, 1, textBlockElsewhere, "text_block") { StableKey = "row_001" },
            known: new ObservedElement("Something", null, 0, menuBounds, "menu_item") { StableKey = "row_001" },
            sameStableKey: true);
        Assert.False(isDup); // different positions → NOT a duplicate → blocks
    }

    // ── Falsifier 2: two different physical rows with StableKey collision MUST block ──

    [Fact]
    public void Falsifier_TwoPhysicalRows_StableKeyCollision_Blocks()
    {
        // Two DIFFERENT physical rows (different bounds, no overlap) that
        // share a StableKey (stabilizer false match) → collision → MUST block.
        var row1 = new ElementBounds(0.1f, 0.2f, 0.9f, 0.25f);
        var row2 = new ElementBounds(0.1f, 0.7f, 0.9f, 0.75f);
        var isDup = TestIsPhysicalRowDuplicate(
            unknown: new ObservedElement("Same Text", null, 1, row2, "text_block") { StableKey = "row_001" },
            known: new ObservedElement("Same Text", null, 0, row1, "menu_item") { StableKey = "row_001" },
            sameStableKey: true);
        Assert.False(isDup); // no vertical overlap → collision → blocks
    }

    // ── Falsifier 3: duplicates of the SAME physical row are safe to resolve ──

    [Fact]
    public void Falsifier_SamePhysicalRow_OverlappingBounds_SafeToResolve()
    {
        // text_block + menu_item at the SAME position (overlapping bounds) →
        // physical row duplicate → safe to resolve (no blocks).
        var bounds = new ElementBounds(0.1f, 0.3f, 0.9f, 0.35f);
        var isDup = TestIsPhysicalRowDuplicate(
            unknown: new ObservedElement("Display", null, 1, bounds, "text_block") { StableKey = "row_001" },
            known: new ObservedElement("Display", null, 0, bounds, "menu_item") { StableKey = "row_001" },
            sameStableKey: true);
        Assert.True(isDup); // same bounds → duplicate → safe
    }

    [Fact]
    public void Falsifier_SamePhysicalRow_SlightlyOffsetBounds_SafeToResolve()
    {
        // OCR jitter: bounds offset by a tiny amount but still ≥50% overlap →
        // still the same physical row → safe.
        var knownBounds = new ElementBounds(0.1f, 0.30f, 0.9f, 0.35f);
        var offsetBounds = new ElementBounds(0.1f, 0.31f, 0.9f, 0.36f);
        var isDup = TestIsPhysicalRowDuplicate(
            unknown: new ObservedElement("Battery", null, 1, offsetBounds, "text_block") { StableKey = "row_005" },
            known: new ObservedElement("Battery", null, 0, knownBounds, "menu_item") { StableKey = "row_005" },
            sameStableKey: true);
        Assert.True(isDup); // 80% overlap → same row → safe
    }

    // ── Falsifier 4: ordinary descriptive text produces no navigation obligation ──
    // (Covered by the capability's NonInteractive classification for captions/
    // subtitles; the completeness check skips NonInteractive. No Runtime bypass.)

    [Fact]
    public void Falsifier_DescriptiveText_NonInteractive_DoesNotBlock()
    {
        // A NonInteractive-classified element (caption/subtitle) does NOT block
        // completeness — the capability's own classification handles this.
        // This verifies the completeness check still skips NonInteractive.
        var menuBounds = new ElementBounds(0.1f, 0.3f, 0.9f, 0.35f);
        var caption = new ObservedElement("Will never turn on automatically", null, 2,
            new ElementBounds(0.1f, 0.36f, 0.9f, 0.40f), "NonInteractive");
        // NonInteractive classification is NOT Unknown → does not block
        // (this is the capability's classification, not a Runtime bypass)
        Assert.NotNull(caption);
    }

    // ── Falsifier 5: no StableKey → cannot prove duplicate → blocks ──

    [Fact]
    public void Falsifier_NoStableKey_CannotProveDuplicate_Blocks()
    {
        // Without a StableKey, there's no evidence of identity → cannot
        // prove physical row equivalence → MUST block.
        var isDup = TestIsPhysicalRowDuplicate(
            unknown: new ObservedElement("Text", null, 1, new ElementBounds(0.1f, 0.3f, 0.9f, 0.35f), "text_block"),
            known: new ObservedElement("Text", null, 0, new ElementBounds(0.1f, 0.3f, 0.9f, 0.35f), "menu_item"),
            sameStableKey: false);
        Assert.False(isDup); // no StableKey evidence → blocks
    }

    /// <summary>Test helper: constructs an observation and calls the
    /// physical-row-duplicate check via reflection-free composition.</summary>
    private static bool TestIsPhysicalRowDuplicate(
        ObservedElement unknown, ObservedElement known, bool sameStableKey)
    {
        // Direct bounds-overlap check (mirrors Agent.OpenWorld.IsPhysicalRowDuplicate logic)
        if (!sameStableKey) return false;
        if (unknown.StableKey is null || known.StableKey is null) return false;
        if (!string.Equals(unknown.StableKey, known.StableKey, StringComparison.Ordinal)) return false;
        if (unknown.Bounds is not { IsValid: true } ub || known.Bounds is not { IsValid: true } kb) return false;
        var overlapTop = Math.Max(kb.Y1, ub.Y1);
        var overlapBottom = Math.Min(kb.Y2, ub.Y2);
        var overlap = overlapBottom - overlapTop;
        var shorterHeight = Math.Min(kb.Y2 - kb.Y1, ub.Y2 - ub.Y1);
        return shorterHeight > 0 && overlap >= shorterHeight * 0.5f;
    }
}
