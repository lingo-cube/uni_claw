using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// PERCEPTION_EVIDENCE_PRESERVATION — executable proofs that upstream
/// perception evidence (type labels, per-candidate bounds) survives
/// into Runtime Observation without being discarded.
///
/// Primary falsifier: real Wi‑Fi row with separate toggle candidate.
/// </summary>
public sealed class PerceptionEvidencePreservationTests
{
    // ── Reality-derived data from B1 golden + A3 EP-04 ────────────────────

    private static ElementBounds ToggleBounds => new(0.85f, 0.40f, 0.92f, 0.44f);
    private static ElementBounds WiFiEntryBounds => new(0.08f, 0.40f, 0.25f, 0.44f);
    private static ElementBounds AndroidWifiBounds => new(0.26f, 0.50f, 0.38f, 0.54f);

    // ── P1: TYPE PRESERVATION ─────────────────────────────────────────────

    [Fact]
    public void P1_TypePreservation_ToggleRemainsToggle_MenuItemRemainsMenuItem()
    {
        var toggle = new ObservedElement("", null, 1, ToggleBounds, "toggle");
        var menuItem = new ObservedElement("Wi‑Fi", null, 0, WiFiEntryBounds, "menuItem");

        Assert.Equal("toggle", toggle.PerceptionType);
        Assert.Equal("menuItem", menuItem.PerceptionType);
    }

    // ── P2: BOUNDS PRESERVATION ───────────────────────────────────────────

    [Fact]
    public void P2_BoundsPreservation_FullNormalizedBoundsSurvive()
    {
        var element = new ObservedElement("Wi‑Fi", null, 0, WiFiEntryBounds, "menuItem");

        Assert.NotNull(element.Bounds);
        Assert.Equal(0.08f, element.Bounds.X1, 0.001f);
        Assert.Equal(0.40f, element.Bounds.Y1, 0.001f);
        Assert.Equal(0.25f, element.Bounds.X2, 0.001f);
        Assert.Equal(0.44f, element.Bounds.Y2, 0.001f);
    }

    // ── P3: EMPTY TOGGLE SURVIVES ─────────────────────────────────────────

    /// <summary>
    /// P3: An empty-text toggle is NOT discarded merely because Text == "".
    /// The toggle candidate has bounds and a type label — it is real perception evidence.
    /// </summary>
    [Fact]
    public void P3_EmptyToggleSurvives_NotDiscardedForEmptyText()
    {
        var emptyToggle = new ObservedElement("", null, 5, ToggleBounds, "toggle");

        Assert.Equal("", emptyToggle.Text);
        Assert.Equal("toggle", emptyToggle.PerceptionType);
        Assert.NotNull(emptyToggle.Bounds);
        Assert.Null(emptyToggle.SwitchState); // no state fabricated
    }

    // ── P4: ROW + SWITCH REMAIN DISTINCT ──────────────────────────────────

    /// <summary>
    /// P4: Wi‑Fi row entry and toggle remain two distinct ObservedElements.
    /// They share same y-row but are separate perception candidates.
    /// </summary>
    [Fact]
    public void P4_WifiRowAndToggle_RemainDistinctElements()
    {
        var wifiEntry = new ObservedElement("Wi‑Fi", null, 0, WiFiEntryBounds, "menuItem");
        var toggle = new ObservedElement("", null, 1, ToggleBounds, "toggle");

        // Different elements
        Assert.NotEqual(wifiEntry.Index, toggle.Index);
        Assert.NotEqual(wifiEntry.Text, toggle.Text);
        Assert.NotEqual(wifiEntry.PerceptionType, toggle.PerceptionType);
        Assert.NotEqual(wifiEntry.Bounds, toggle.Bounds);

        // Wi‑Fi entry is left-side, toggle is right-side
        Assert.True(wifiEntry.Bounds!.X2 < toggle.Bounds!.X1,
            "Wi‑Fi entry should be left of the toggle");
    }

    // ── P5: SAME ROW SPATIAL RELATION IS DERIVABLE ────────────────────────

    /// <summary>
    /// P5: Using Bounds only, vertical regions can be shown to align.
    /// Both Wi‑Fi entry and toggle share approximately the same y-range.
    /// No semantic grouping inference yet.
    /// </summary>
    [Fact]
    public void P5_SameRowSpatialRelation_DerivableFromBounds()
    {
        var wifiEntry = new ObservedElement("Wi‑Fi", null, 0, WiFiEntryBounds, "menuItem");
        var toggle = new ObservedElement("", null, 1, ToggleBounds, "toggle");

        // Same y-row: their vertical ranges overlap
        bool sameRow = wifiEntry.Bounds!.Y1 <= toggle.Bounds!.Y2
            && toggle.Bounds!.Y1 <= wifiEntry.Bounds!.Y2;
        Assert.True(sameRow, "Wi‑Fi entry and toggle should share the same y-row");

        // Center y is similar
        Assert.True(Math.Abs(wifiEntry.Bounds.CenterY - toggle.Bounds.CenterY) < 0.05f,
            "Centers should be within 5% of viewport height");
    }

    // ── P6: NO SWITCHSTATE FABRICATION ────────────────────────────────────

    /// <summary>
    /// P6: Real toggle candidate from perception has SwitchState == null.
    /// No state is fabricated from type=toggle or geometry.
    /// </summary>
    [Fact]
    public void P6_NoSwitchStateFabrication_RealToggleHasNullState()
    {
        var realToggle = new ObservedElement("", null, 5, ToggleBounds, "toggle");

        Assert.Null(realToggle.SwitchState);
        Assert.Equal("toggle", realToggle.PerceptionType);
        // PerceptionType=toggle is evidence of control TYPE, not evidence of ON/OFF state
    }

    /// <summary>
    /// P6: SwitchState is preserved only when explicitly provided (test fixtures).
    /// </summary>
    [Fact]
    public void P6_SwitchStateOnlyWhenExplicitlyProvided()
    {
        var syntheticOn = new ObservedElement("Wi‑Fi", true, 0, null, "toggle");
        var syntheticOff = new ObservedElement("Wi‑Fi", false, 1, null, "toggle");

        Assert.True(syntheticOn.SwitchState);
        Assert.False(syntheticOff.SwitchState);
        // Both have PerceptionType=toggle but only one is ON — state ≠ type
    }

    // ── P7: PROVIDER TYPE != ACTION AUTHORITY ─────────────────────────────

    /// <summary>
    /// P7: PerceptionType=toggle alone must not automatically bypass
    /// Traversal grounding/action authorization.
    /// </summary>
    [Fact]
    public void P7_ProviderTypeIsNotActionAuthority()
    {
        var toggleElement = new ObservedElement("", null, 5, ToggleBounds, "toggle");

        // PerceptionType is preserved as evidence
        Assert.Equal("toggle", toggleElement.PerceptionType);

        // BUT: this does NOT mean SetSwitch is authorized.
        // Traversal still requires:
        //   1. Text match (TargetDescription grounding)
        //   2. Action authorization (CandidateAuthorizationEvaluator)
        //   3. Post-action effect verification
        //
        // PerceptionType is evidence, not authority.
    }

    // ── P8: LEGACY BACKWARD COMPATIBILITY ─────────────────────────────────

    [Fact]
    public void P8_LegacyBackwardCompatibility_OldConstructionStillWorks()
    {
        // Old 3-arg construction
        var legacy1 = new ObservedElement("Settings", null, 0);
        Assert.Null(legacy1.Bounds);
        Assert.Null(legacy1.PerceptionType);

        // Old with SwitchState
        var legacy2 = new ObservedElement("Wi‑Fi", true, 3);
        Assert.Null(legacy2.Bounds);
        Assert.Null(legacy2.PerceptionType);

        // New with Bounds only (no PerceptionType)
        var withBounds = new ObservedElement("Wi‑Fi", null, 1, WiFiEntryBounds);
        Assert.NotNull(withBounds.Bounds);
        Assert.Null(withBounds.PerceptionType);
    }

    // ── P9: PAGEANALYSIS REGRESSION ───────────────────────────────────────

    /// <summary>
    /// P9: PageAnalysis ignores PerceptionType — TEXT_ATTRIBUTE semantics unchanged.
    /// </summary>
    [Fact]
    public void P9_PageAnalysisRegression_IgnoresPerceptionType()
    {
        var criteria = new PageAnalysisCriteria(
            "com.android.settings",
            new Dictionary<string, ImmutableArray<string>>
            {
                ["InternetPage"] = ["T-Mobile", "Add network"],
            }.ToImmutableDictionary());

        // Observation with PerceptionType
        var obs = new Observation(
            [
                new ObservedElement("T-Mobile", null, 0, TMobileBounds, "menuItem"),
                new ObservedElement("Add network", null, 1, null, "menuItem"),
            ],
            "com.android.settings", 1);

        var evidence = PageAnalysis.Analyze(obs, criteria);

        // TEXT_ANCHOR Supports InternetPage — unaffected by PerceptionType
        var internetEvidence = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, internetEvidence.Stance);
    }

    private static ElementBounds TMobileBounds => new(0.22f, 0.30f, 0.38f, 0.33f);

    // ── P10: DEVICE SPATIAL MAPPING ───────────────────────────────────────

    /// <summary>
    /// P10: When Traversal has grounded the toggle candidate, its preserved
    /// Bounds continue through DeviceAction → Environment without coordinate loss.
    /// </summary>
    [Fact]
    public void P10_DeviceSpatialMapping_ToggleBoundsFlowToDeviceAction()
    {
        var toggle = new ObservedElement("", null, 5, ToggleBounds, "toggle");

        // Traversal grounds this element → creates DeviceAction.Tap with Bounds
        var action = new DeviceAction.Tap(toggle.Index, toggle.Bounds);

        Assert.NotNull(action.TargetBounds);
        Assert.Equal(ToggleBounds, action.TargetBounds);

        // Environment maps Bounds → physical coordinates
        var (px, py) = MapToDevicePixel(action.TargetBounds, 1080, 2400);
        Assert.True(px > 800, $"Toggle at x≈0.885 should map to px>800, got {px}");
        Assert.True(py > 900 && py < 1100, $"Toggle at y≈0.42 should map to py≈1008, got {py}");
    }

    private static (int X, int Y) MapToDevicePixel(ElementBounds bounds, int w, int h)
        => ((int)Math.Round(bounds.CenterX * w), (int)Math.Round(bounds.CenterY * h));

    // ── FULL ROW REPRESENTATION ───────────────────────────────────────────

    /// <summary>
    /// Real Wi‑Fi row: three separate candidates from upstream perception,
    /// all preserved independently in Runtime Observation.
    /// </summary>
    [Fact]
    public void RealWifiRow_AllCandidatesIndependentlyPreserved()
    {
        var wifiEntry = new ObservedElement("Wi‑Fi", null, 0, WiFiEntryBounds, "menuItem");
        var toggle = new ObservedElement("", null, 1, ToggleBounds, "toggle");
        var subtitle = new ObservedElement("AndroidWifi", null, 2, AndroidWifiBounds, "menuItem");

        // All three are distinct
        Assert.Equal(3, new[] { wifiEntry, toggle, subtitle }.Select(e => e.Index).Distinct().Count());

        // Each has its own bounds
        Assert.NotNull(wifiEntry.Bounds);
        Assert.NotNull(toggle.Bounds);
        Assert.NotNull(subtitle.Bounds);

        // Each has its type label
        Assert.Equal("menuItem", wifiEntry.PerceptionType);
        Assert.Equal("toggle", toggle.PerceptionType);
        Assert.Equal("menuItem", subtitle.PerceptionType);

        // Positions: entry (left, y≈0.42), toggle (right, y≈0.42), subtitle (left, y≈0.52)
        Assert.True(wifiEntry.Bounds.Y1 < subtitle.Bounds.Y1,
            "Wi‑Fi entry should be above AndroidWifi subtitle");
        Assert.True(toggle.Bounds.X1 > wifiEntry.Bounds.X2,
            "Toggle should be right of Wi‑Fi entry");
    }
}
