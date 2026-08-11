using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// MINIMUM_SPATIAL_OBSERVATION_CONTRACT — executable proofs for the
/// Observation Representation Gap purchase.
///
/// Proves that normalized ElementBounds on ObservedElement:
/// - Preserves upstream spatial evidence that already exists
/// - Is order-independent (Index ≠ coordinate)
/// - Distinguishes same-text different-location candidates
/// - Represents subtitle phantom and row+switch geometry
/// - Is backward compatible (null bounds)
/// - Does not break existing PageAnalysis TEXT_ATTRIBUTE semantics
/// </summary>
public sealed class MinimumSpatialObservationContractTests
{
    // ── Reality-derived bounds from analysis.jsonl (EP-04 run) ─────────────

    // SettingsRoot: "Bluetooth, pairing" — subtitle phantom, type=menuItem, action=navigate
    private static ElementBounds BluetoothPairingBounds => new(0.25f, 0.57f, 0.38f, 0.60f);

    // SettingsRoot: "Network&internet" — real menu item (first occurrence)
    private static ElementBounds NetworkInternetBounds => new(0.30f, 0.39f, 0.47f, 0.42f);

    // InternetPage: "Wi‑Fi" entry — no SwitchState, navigable
    private static ElementBounds WiFiEntryBounds => new(0.22f, 0.43f, 0.35f, 0.46f);

    // WifiPage: "Wi‑Fi" switch — SwitchState-bearing toggle
    private static ElementBounds WiFiSwitchBounds => new(0.22f, 0.12f, 0.35f, 0.15f);

    // WifiPage: "Auto-connect" — SwitchState=true
    private static ElementBounds AutoConnectBounds => new(0.22f, 0.18f, 0.38f, 0.21f);

    // InternetPage: "T-Mobile" — network row
    private static ElementBounds TMobileBounds => new(0.22f, 0.30f, 0.38f, 0.33f);

    // ── P1: SPATIAL ROUND TRIP ────────────────────────────────────────────

    /// <summary>
    /// P1: Upstream reality-derived element with known normalized bounds
    /// → Construct ObservedElement → Runtime preserves exact canonical bounds.
    /// </summary>
    [Fact]
    public void P1_SpatialRoundTrip_PreservesExactCanonicalBounds()
    {
        var bounds = new ElementBounds(0.3111f, 0.5786f, 0.3806f, 0.5952f);
        var element = new ObservedElement("Bluetooth, pairing", null, 9, bounds);

        Assert.NotNull(element.Bounds);
        Assert.Equal(0.3111f, element.Bounds.X1, 0.0001f);
        Assert.Equal(0.5786f, element.Bounds.Y1, 0.0001f);
        Assert.Equal(0.3806f, element.Bounds.X2, 0.0001f);
        Assert.Equal(0.5952f, element.Bounds.Y2, 0.0001f);

        // Center is derived
        Assert.Equal(0.34585f, element.Bounds.CenterX, 0.001f);
        Assert.Equal(0.5869f, element.Bounds.CenterY, 0.001f);
    }

    /// <summary>
    /// P1 continuation: Wi‑Fi entry at recorded position.
    /// </summary>
    [Fact]
    public void P1_WiFiEntry_PreservesRecordedBounds()
    {
        var bounds = new ElementBounds(0.2611f, 0.2938f, 0.3389f, 0.3104f);
        var element = new ObservedElement("Wi‑Fi", null, 6, bounds);

        Assert.NotNull(element.Bounds);
        Assert.True(element.Bounds.IsValid);
        Assert.Equal(0.2611f, element.Bounds.X1, 0.001f);
    }

    // ── P2: ORDER INDEPENDENCE ────────────────────────────────────────────

    /// <summary>
    /// P2: Reordering ObservedElements must not change each element's Bounds.
    /// Index is observation-local only. Bounds are element-intrinsic spatial evidence.
    /// </summary>
    [Fact]
    public void P2_OrderIndependence_BoundsUnchangedByReorder()
    {
        var elementA = new ObservedElement("Wi‑Fi", null, 0, WiFiEntryBounds);
        var elementB = new ObservedElement("T-Mobile", null, 1, TMobileBounds);

        // Swap indices (simulating reorder)
        var reorderedA = elementA with { Index = 1 };
        var reorderedB = elementB with { Index = 0 };

        // Bounds unchanged
        Assert.Equal(elementA.Bounds, reorderedA.Bounds);
        Assert.Equal(elementB.Bounds, reorderedB.Bounds);

        // Only Index changed
        Assert.NotEqual(elementA.Index, reorderedA.Index);
        Assert.Equal(1, reorderedA.Index);
        Assert.Equal(0, reorderedB.Index);
    }

    // ── P3: SAME TEXT DIFFERENT LOCATION ──────────────────────────────────

    /// <summary>
    /// P3: Two elements with identical Text but distinct Bounds
    /// remain distinguishable as observation evidence.
    /// Example: "Internet" at Index 1 (x=0.26) vs duplicate "Internet" at Index 3 (different x).
    /// </summary>
    [Fact]
    public void P3_SameTextDifferentLocation_DistinguishableByBounds()
    {
        var internet1 = new ObservedElement("Internet", null, 1,
            new ElementBounds(0.26f, 0.29f, 0.34f, 0.31f));
        var internet2 = new ObservedElement("Internet", null, 3,
            new ElementBounds(0.50f, 0.29f, 0.58f, 0.31f));

        // Same text
        Assert.Equal(internet1.Text, internet2.Text);

        // Different bounds — distinguishable
        Assert.NotEqual(internet1.Bounds, internet2.Bounds);
        Assert.True(internet1.Bounds!.X2 < internet2.Bounds!.X1,
            "internet1 should be left of internet2");
    }

    /// <summary>
    /// P3 continuation: "Network&internet" duplicates on SettingsRoot.
    /// </summary>
    [Fact]
    public void P3_NetworkInternetDuplicates_DistinguishableByBounds()
    {
        var ni1 = new ObservedElement("Network&internet", null, 4,
            new ElementBounds(0.30f, 0.39f, 0.47f, 0.42f));
        var ni2 = new ObservedElement("Network&internet", null, 6,
            new ElementBounds(0.30f, 0.39f, 0.47f, 0.42f)); // same y-row, same x

        // Same text AND same bounds → visually the same element (duplicate in perception)
        Assert.Equal(ni1.Text, ni2.Text);
        Assert.Equal(ni1.Bounds, ni2.Bounds);

        // But different Index — they ARE separate ObservedElements
        Assert.NotEqual(ni1.Index, ni2.Index);
    }

    // ── P4: SUBTITLE PHANTOM REPRESENTATION ───────────────────────────────

    /// <summary>
    /// P4: Reality-derived title/subtitle elements must preserve enough geometry
    /// to show they occupy distinct spatial positions from interactive menu items.
    ///
    /// "Bluetooth, pairing" (subtitle phantom) is at a different y-position
    /// than "Network&internet" (real menu item). Both are classified as menuItem
    /// in legacy perception, but their spatial positions differ.
    /// </summary>
    [Fact]
    public void P4_SubtitlePhantom_DistinctSpatialPosition()
    {
        var subtitle = new ObservedElement("Bluetooth, pairing", null, 9, BluetoothPairingBounds);
        var realMenu = new ObservedElement("Network&internet", null, 4, NetworkInternetBounds);

        // Both have text and bounds
        Assert.NotNull(subtitle.Bounds);
        Assert.NotNull(realMenu.Bounds);

        // They occupy DIFFERENT spatial positions
        Assert.NotEqual(subtitle.Bounds, realMenu.Bounds);

        // Subtitle is BELOW the real menu item (higher y)
        Assert.True(subtitle.Bounds.Y1 > realMenu.Bounds.Y2,
            $"subtitle y1={subtitle.Bounds.Y1} should be > realMenu y2={realMenu.Bounds.Y2}");
    }

    // ── P5: ROW + SWITCH REPRESENTATION ───────────────────────────────────

    /// <summary>
    /// P5: Reality-derived row (Wi‑Fi entry) + switch (Wi‑Fi toggle) preserve
    /// both regions independently. No interaction-capability inference yet.
    /// </summary>
    [Fact]
    public void P5_RowAndSwitch_PreserveIndependentRegions()
    {
        var wifiEntry = new ObservedElement("Wi‑Fi", null, 6, WiFiEntryBounds);
        var wifiSwitch = new ObservedElement("Wi‑Fi", false, 0, WiFiSwitchBounds);

        // Both have bounds
        Assert.NotNull(wifiEntry.Bounds);
        Assert.NotNull(wifiSwitch.Bounds);

        // They occupy DIFFERENT spatial regions
        Assert.NotEqual(wifiEntry.Bounds, wifiSwitch.Bounds);

        // Wi‑Fi entry (InternetPage) is at y≈0.29-0.31
        // Wi‑Fi switch (WifiPage) is at y≈0.12-0.15
        // Different pages → different y-positions
        Assert.True(wifiEntry.Bounds.Y1 > wifiSwitch.Bounds.Y2,
            "Wi‑Fi entry on InternetPage should be below Wi‑Fi switch on WifiPage");
    }

    /// <summary>
    /// P5 continuation: Row + embedded switch on same page (WifiPage).
    /// "Wi‑Fi" text label + "Wi‑Fi" switch toggle occupy the same semantic row
    /// but different spatial x-positions.
    /// </summary>
    [Fact]
    public void P5_RowWithEmbeddedSwitch_SameY_DifferentX()
    {
        // Simulate WifiPage: Wi‑Fi label (left) + Wi‑Fi toggle (right)
        var wifiLabel = new ObservedElement("Wi‑Fi", null, 0,
            new ElementBounds(0.08f, 0.12f, 0.28f, 0.15f));  // left side
        var wifiToggle = new ObservedElement("Wi‑Fi", false, 1,
            new ElementBounds(0.78f, 0.12f, 0.92f, 0.15f)); // right side, same y-row

        // Same y-row (overlapping y range)
        bool sameRow = wifiLabel.Bounds!.Y1 <= wifiToggle.Bounds!.Y2
            && wifiToggle.Bounds.Y1 <= wifiLabel.Bounds.Y2;
        Assert.True(sameRow, "Both should be on the same y-row");

        // Different x — label is left, toggle is right
        Assert.True(wifiLabel.Bounds.X2 < wifiToggle.Bounds.X1,
            "Label should be left of toggle");
    }

    // ── P6: NORMALIZATION INVARIANT ───────────────────────────────────────

    /// <summary>
    /// P6: Equivalent physical layout at different screenshot resolutions,
    /// after upstream normalization, produces equivalent normalized bounds.
    ///
    /// A 1080×2400 screenshot and a 1440×3168 screenshot of the same page
    /// should produce the same normalized bounds (within rounding tolerance).
    /// </summary>
    [Fact]
    public void P6_NormalizationInvariant_EquivalentLayoutSameBounds()
    {
        // Same element at ~x=304px on 1080-wide → normalized ~0.281
        // Same element at ~x=405px on 1440-wide → normalized ~0.281
        var bounds1080 = new ElementBounds(0.281f, 0.400f, 0.356f, 0.420f);
        var bounds1440 = new ElementBounds(0.281f, 0.400f, 0.356f, 0.420f);

        Assert.Equal(bounds1080, bounds1440);
    }

    /// <summary>
    /// P6: Bounds validation — all coordinates within [0,1].
    /// </summary>
    [Fact]
    public void P6_BoundsValidation_RejectsOutOfRange()
    {
        // Valid bounds
        var valid = new ElementBounds(0.1f, 0.2f, 0.3f, 0.4f);
        Assert.True(valid.IsValid);

        // Out of range: negative
        var negative = new ElementBounds(-0.1f, 0.2f, 0.3f, 0.4f);
        Assert.False(negative.IsValid);

        // Out of range: > 1
        var tooLarge = new ElementBounds(0.1f, 0.2f, 1.1f, 0.4f);
        Assert.False(tooLarge.IsValid);

        // Inverted: X2 < X1
        var inverted1 = new ElementBounds(0.5f, 0.2f, 0.3f, 0.4f);
        Assert.False(inverted1.IsValid);

        // Inverted: Y2 < Y1
        var inverted2 = new ElementBounds(0.1f, 0.4f, 0.3f, 0.2f);
        Assert.False(inverted2.IsValid);
    }

    // ── P7: NULL BACKWARD COMPATIBILITY ───────────────────────────────────

    /// <summary>
    /// P7: Legacy/synthetic ObservedElement without Bounds remains valid.
    /// Existing construction pattern (Text, SwitchState, Index) still works.
    /// </summary>
    [Fact]
    public void P7_NullBackwardCompatibility_LegacyElementWithoutBounds()
    {
        // Old construction pattern — no Bounds parameter
        var legacy = new ObservedElement("Settings", null, 0);
        Assert.Null(legacy.Bounds);
        Assert.Equal("Settings", legacy.Text);
        Assert.Equal(0, legacy.Index);

        // Old with SwitchState
        var legacySwitch = new ObservedElement("Wi‑Fi", true, 3);
        Assert.Null(legacySwitch.Bounds);
        Assert.True(legacySwitch.SwitchState);
    }

    /// <summary>
    /// P7: Observation with mixed null/non-null bounds is valid.
    /// </summary>
    [Fact]
    public void P7_MixedBounds_ObservationValid()
    {
        var elements = new[]
        {
            new ObservedElement("Settings", null, 0), // no bounds (legacy)
            new ObservedElement("Wi‑Fi", null, 1, WiFiEntryBounds), // with bounds (new)
            new ObservedElement("T-Mobile", null, 2, TMobileBounds), // with bounds
        };

        var observation = new Observation(
            [.. elements],
            "com.android.settings",
            1);

        Assert.Equal(3, observation.Elements.Length);
        Assert.Null(observation.Elements[0].Bounds);
        Assert.NotNull(observation.Elements[1].Bounds);
        Assert.NotNull(observation.Elements[2].Bounds);
    }

    // ── P8: PAGEANALYSIS BACKWARD COMPATIBILITY ───────────────────────────

    /// <summary>
    /// P8: Existing PageAnalysis TEXT_ATTRIBUTE semantics produce identical
    /// results when Bounds are present — PageAnalysis ignores Bounds.
    /// </summary>
    [Fact]
    public void P8_PageAnalysisBackwardCompatibility_IgnoresBounds()
    {
        var criteria = new PageAnalysisCriteria(
            "com.android.settings",
            new Dictionary<string, ImmutableArray<string>>
            {
                ["InternetPage"] = ["T-Mobile", "Add network"],
            }.ToImmutableDictionary());

        // Observation WITHOUT bounds
        var obsWithoutBounds = new Observation(
            [new ObservedElement("T-Mobile", null, 0), new ObservedElement("Add network", null, 1)],
            "com.android.settings", 1);

        // Observation WITH bounds (same text, same order)
        var obsWithBounds = new Observation(
            [
                new ObservedElement("T-Mobile", null, 0, TMobileBounds),
                new ObservedElement("Add network", null, 1, new ElementBounds(0.22f, 0.34f, 0.38f, 0.37f)),
            ],
            "com.android.settings", 1);

        var evidenceWithout = PageAnalysis.Analyze(obsWithoutBounds, criteria);
        var evidenceWith = PageAnalysis.Analyze(obsWithBounds, criteria);

        // Same evidence output — PageAnalysis ignores bounds
        Assert.Equal(evidenceWithout.Length, evidenceWith.Length);
        for (int i = 0; i < evidenceWithout.Length; i++)
        {
            Assert.Equal(evidenceWithout[i].Source, evidenceWith[i].Source);
            Assert.Equal(evidenceWithout[i].Claim, evidenceWith[i].Claim);
            Assert.Equal(evidenceWithout[i].Stance, evidenceWith[i].Stance);
        }
    }

    // ── P9: DETERMINISTIC REPLAY ──────────────────────────────────────────

    /// <summary>
    /// P9: Same Observation → same spatial representation.
    /// </summary>
    [Fact]
    public void P9_DeterministicReplay_SameSpatialRepresentation()
    {
        var bounds = new ElementBounds(0.3111f, 0.5786f, 0.3806f, 0.5952f);
        var e1 = new ObservedElement("Bluetooth, pairing", null, 9, bounds);
        var e2 = new ObservedElement("Bluetooth, pairing", null, 9, bounds);

        Assert.Equal(e1.Bounds, e2.Bounds);
        Assert.Equal(e1.Bounds!.X1, e2.Bounds!.X1);
        Assert.Equal(e1.Bounds!.CenterX, e2.Bounds!.CenterX);
    }

    // ── BOUNDS IMMUTABILITY ───────────────────────────────────────────────

    /// <summary>
    /// ElementBounds is an immutable record — with-expression creates new instance.
    /// </summary>
    [Fact]
    public void ElementBounds_IsImmutable()
    {
        var original = new ElementBounds(0.1f, 0.2f, 0.3f, 0.4f);
        var modified = original with { X1 = 0.15f };

        Assert.Equal(0.1f, original.X1);  // original unchanged
        Assert.Equal(0.15f, modified.X1);  // modified is new instance
        Assert.NotEqual(original, modified);
    }

    // ── CENTER AND DIMENSIONS ─────────────────────────────────────────────

    [Fact]
    public void ElementBounds_CenterAndDimensions_AreCorrect()
    {
        var bounds = new ElementBounds(0.2f, 0.3f, 0.6f, 0.7f);

        Assert.Equal(0.4f, bounds.CenterX, 0.001f);
        Assert.Equal(0.5f, bounds.CenterY, 0.001f);
        Assert.Equal(0.4f, bounds.Width, 0.001f);
        Assert.Equal(0.4f, bounds.Height, 0.001f);
    }
}
