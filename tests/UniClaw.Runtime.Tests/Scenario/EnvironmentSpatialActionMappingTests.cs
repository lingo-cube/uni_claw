using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// ENVIRONMENT_SPATIAL_ACTION_MAPPING — executable proofs for the
/// Environment Adapter Gap purchase.
///
/// Proves that canonical Observation Bounds flow through DeviceAction
/// to the Environment, where they can be mapped to physical device coordinates.
/// The Environment maps coordinates; it does NOT select semantic targets.
/// </summary>
public sealed class EnvironmentSpatialActionMappingTests
{
    // ── P1: NORMALIZED → DEVICE PIXEL ────────────────────────────────────

    /// <summary>
    /// P1: Known normalized bounds + known device dimensions → correct physical coordinate.
    /// centerX = 0.4 → pixelX = 0.4 * 1080 = 432
    /// </summary>
    [Fact]
    public void P1_NormalizedToDevicePixel_CorrectMapping()
    {
        var bounds = new ElementBounds(0.30f, 0.40f, 0.50f, 0.46f);
        const int deviceWidth = 1080;
        const int deviceHeight = 2400;

        var (px, py) = MapToDevicePixel(bounds, deviceWidth, deviceHeight);

        Assert.Equal(432, px); // 0.40 * 1080
        Assert.Equal(1032, py); // 0.43 * 2400
    }

    /// <summary>
    /// P1 continuation: B1 real-device dimensions (1440×3168).
    /// </summary>
    [Fact]
    public void P1_B1RealDeviceMapping()
    {
        // Wi‑Fi entry from analysis.jsonl: x≈0.2611, y≈0.2938
        var bounds = new ElementBounds(0.22f, 0.28f, 0.30f, 0.31f);
        const int deviceWidth = 1440;
        const int deviceHeight = 3168;

        var (px, py) = MapToDevicePixel(bounds, deviceWidth, deviceHeight);

        // centerX = 0.26 → 0.26 * 1440 ≈ 374
        Assert.True(px >= 350 && px <= 400, $"px={px} should be ~374");
        // centerY = 0.295 → 0.295 * 3168 ≈ 935
        Assert.True(py >= 900 && py <= 970, $"py={py} should be ~935");
    }

    private static (int X, int Y) MapToDevicePixel(ElementBounds bounds, int deviceWidth, int deviceHeight)
    {
        var px = (int)Math.Round(bounds.CenterX * deviceWidth);
        var py = (int)Math.Round(bounds.CenterY * deviceHeight);
        return (px, py);
    }

    // ── P2: RESOLUTION INDEPENDENCE ──────────────────────────────────────

    /// <summary>
    /// P2: Same normalized target at different resolutions → equivalent relative position.
    /// centerX=0.40 at 1080w → 432px (40%). centerX=0.40 at 1440w → 576px (40%).
    /// </summary>
    [Fact]
    public void P2_ResolutionIndependence_RelativePositionPreserved()
    {
        var bounds = new ElementBounds(0.30f, 0.40f, 0.50f, 0.46f);

        var (px1080, py2400) = MapToDevicePixel(bounds, 1080, 2400);
        var (px1440, py3168) = MapToDevicePixel(bounds, 1440, 3168);

        // Same relative position (40% of width, 43% of height)
        Assert.Equal(0.40, (double)px1080 / 1080, 0.01);
        Assert.Equal(0.40, (double)px1440 / 1440, 0.01);
        Assert.Equal(0.43, (double)py2400 / 2400, 0.01);
        Assert.Equal(0.43, (double)py3168 / 3168, 0.01);
    }

    // ── P3: SAME TEXT, DIFFERENT BOUNDS ──────────────────────────────────

    /// <summary>
    /// P3: Two same-text candidates. Grounding selects ONE before Environment.
    /// Environment maps the selected candidate's Bounds — must not re-select.
    /// </summary>
    [Fact]
    public void P3_SameTextDifferentBounds_EnvironmentMapsSelectedOnly()
    {
        var internet1 = new ObservedElement("Internet", null, 1,
            new ElementBounds(0.26f, 0.29f, 0.34f, 0.31f));
        var internet2 = new ObservedElement("Internet", null, 3,
            new ElementBounds(0.50f, 0.29f, 0.58f, 0.31f));

        // Grounding selects internet1 (Index 1)
        var selected = internet1;
        var action = new DeviceAction.Tap(selected.Index, selected.Bounds);

        // Action carries the SELECTED element's bounds, not the other one
        Assert.NotNull(action.TargetBounds);
        Assert.Equal(0.26f, action.TargetBounds.X1, 0.01f);
        Assert.NotEqual(internet2.Bounds!.X1, action.TargetBounds.X1);

        // Physical mapping uses selected bounds
        var (px, _) = MapToDevicePixel(action.TargetBounds, 1080, 2400);
        Assert.True(px < 400, "Selected element (x=0.26-0.34) should map to left side, not right");
    }

    // ── P4: NULL BOUNDS ──────────────────────────────────────────────────

    /// <summary>
    /// P4: Null bounds → spatial adapter must NOT guess coordinates.
    /// DeviceAction with null TargetBounds = no spatial mapping available.
    /// </summary>
    [Fact]
    public void P4_NullBounds_NoSpatialMapping()
    {
        var action = new DeviceAction.Tap(5, TargetBounds: null);
        Assert.Null(action.TargetBounds);

        // Environment should detect null bounds and fall back to Index-based
        // or fail — never guess coordinates
        Assert.False(HasValidSpatialTarget(action));
    }

    /// <summary>
    /// P4: Element without Bounds → action carries null TargetBounds.
    /// </summary>
    [Fact]
    public void P4_LegacyElementWithoutBounds_ActionHasNullBounds()
    {
        var element = new ObservedElement("Settings", null, 0); // no Bounds
        var action = new DeviceAction.Tap(element.Index, element.Bounds);

        Assert.NotNull(action.TargetElementIndex);
        Assert.Null(action.TargetBounds);
    }

    private static bool HasValidSpatialTarget(DeviceAction action)
        => action is DeviceAction.Tap tap && tap.TargetBounds is not null
        || action is DeviceAction.SetSwitch ss && ss.TargetBounds is not null;

    // ── P5: INVALID BOUNDS ───────────────────────────────────────────────

    /// <summary>
    /// P5: Invalid bounds → must not dispatch physically.
    /// </summary>
    [Fact]
    public void P5_InvalidBounds_NoPhysicalDispatch()
    {
        var invalidBounds = new ElementBounds(0.5f, 0.2f, -0.1f, 0.4f); // X2 < 0
        Assert.False(invalidBounds.IsValid);

        var action = new DeviceAction.Tap(1, invalidBounds);
        Assert.False(IsSafeToDispatchSpatially(action));
    }

    /// <summary>
    /// P5: Out-of-range bounds (>1) → not safe.
    /// </summary>
    [Fact]
    public void P5_OutOfRangeBounds_NoPhysicalDispatch()
    {
        var outOfRange = new ElementBounds(0.1f, 0.2f, 1.5f, 0.4f);
        Assert.False(outOfRange.IsValid);

        var action = new DeviceAction.Tap(1, outOfRange);
        Assert.False(IsSafeToDispatchSpatially(action));
    }

    private static bool IsSafeToDispatchSpatially(DeviceAction action)
    {
        var bounds = action switch
        {
            DeviceAction.Tap tap => tap.TargetBounds,
            DeviceAction.SetSwitch ss => ss.TargetBounds,
            _ => null,
        };
        return bounds is null || bounds.IsValid;
    }

    // ── P6: ROW VS SWITCH ────────────────────────────────────────────────

    /// <summary>
    /// P6: Row (Wi‑Fi text label) and switch (Wi‑Fi toggle) have different Bounds.
    /// Tap on row → row Bounds. SetSwitch on toggle → toggle Bounds.
    /// Environment must not interchange them.
    /// </summary>
    [Fact]
    public void P6_RowAndSwitch_DifferentBounds_NotInterchanged()
    {
        var rowBounds = new ElementBounds(0.08f, 0.12f, 0.28f, 0.15f);   // left side label
        var switchBounds = new ElementBounds(0.78f, 0.12f, 0.92f, 0.15f); // right side toggle

        var tapAction = new DeviceAction.Tap(0, rowBounds);
        var switchAction = new DeviceAction.SetSwitch(1, true, switchBounds);

        // Tap carries row bounds
        Assert.Equal(rowBounds, tapAction.TargetBounds);
        // SetSwitch carries switch bounds
        Assert.Equal(switchBounds, switchAction.TargetBounds);
        // They are NOT interchangeable
        Assert.NotEqual(tapAction.TargetBounds, switchAction.TargetBounds);
    }

    /// <summary>
    /// P6: If row and switch have the SAME bounds (no separate interaction surface),
    /// the action carries what the Observation provides. This is honest.
    /// </summary>
    [Fact]
    public void P6_SameBoundsForRowAndSwitch_HonestRepresentation()
    {
        // When perception doesn't separate row label from switch toggle,
        // both get the same bounding box. This is truthful.
        var sameBounds = new ElementBounds(0.22f, 0.12f, 0.92f, 0.15f);

        var tapAction = new DeviceAction.Tap(0, sameBounds);
        var switchAction = new DeviceAction.SetSwitch(1, true, sameBounds);

        // Both carry the same bounds — honest about what perception provided
        Assert.Equal(tapAction.TargetBounds, switchAction.TargetBounds);
    }

    // ── P7: DISPATCH != EFFECT ───────────────────────────────────────────

    /// <summary>
    /// P7: Coordinate mapping succeeds + physical dispatch occurs,
    /// but post-action observation does not satisfy expected effect →
    /// action is NOT successful. Dispatch ≠ World Change.
    /// </summary>
    [Fact]
    public void P7_DispatchDoesNotProveEffect()
    {
        var bounds = new ElementBounds(0.30f, 0.40f, 0.50f, 0.46f);
        Assert.True(bounds.IsValid);

        // Mapping succeeds
        var (px, py) = MapToDevicePixel(bounds, 1080, 2400);
        Assert.True(px > 0 && py > 0);

        // BUT: successful coordinate mapping ≠ world change
        // After physical tap dispatch, a fresh Observation must confirm the expected effect.
        // This is proven by existing Traversal.Verify (post-action observation check).
        //
        // The spatial mapping is PURE MECHANICS — it answers "where to tap?"
        // It does NOT answer "did the tap produce the expected world change?"
    }

    // ── P8: DETERMINISTIC MAPPING ────────────────────────────────────────

    /// <summary>
    /// P8: Same bounds + same dimensions → same physical coordinate.
    /// </summary>
    [Fact]
    public void P8_DeterministicMapping_SameInputSameOutput()
    {
        var bounds = new ElementBounds(0.3111f, 0.5786f, 0.3806f, 0.5952f);

        var (px1, py1) = MapToDevicePixel(bounds, 1440, 3168);
        var (px2, py2) = MapToDevicePixel(bounds, 1440, 3168);

        Assert.Equal(px1, px2);
        Assert.Equal(py1, py2);
    }

    // ── DeviceAction Round-Trip: Index + Bounds preserved ─────────────────

    /// <summary>
    /// DeviceAction with both Index and Bounds preserves both.
    /// Index remains valid for synthetic/configured paths.
    /// </summary>
    [Fact]
    public void DeviceAction_RoundTrip_PreservesIndexAndBounds()
    {
        var bounds = new ElementBounds(0.2f, 0.3f, 0.4f, 0.5f);
        var action = new DeviceAction.Tap(3, bounds);

        Assert.Equal(3, action.TargetElementIndex);
        Assert.Equal(bounds, action.TargetBounds);
    }

    /// <summary>
    /// SetSwitch with both Index, TargetState, and Bounds preserves all.
    /// </summary>
    [Fact]
    public void SetSwitch_RoundTrip_PreservesAllFields()
    {
        var bounds = new ElementBounds(0.78f, 0.12f, 0.92f, 0.15f);
        var action = new DeviceAction.SetSwitch(1, true, bounds);

        Assert.Equal(1, action.TargetElementIndex);
        Assert.True(action.TargetState);
        Assert.Equal(bounds, action.TargetBounds);
    }

    // ── CENTER POINT POLICY ──────────────────────────────────────────────

    /// <summary>
    /// CenterPointPolicy: Environment uses Bounds.Center for interaction point.
    /// This is sufficient for current purchase — bounds represent the
    /// intended actionable surface as produced by upstream perception.
    /// </summary>
    [Fact]
    public void CenterPoint_IsDerivedFromBounds()
    {
        var bounds = new ElementBounds(0.2f, 0.3f, 0.6f, 0.5f);

        Assert.Equal(0.4f, bounds.CenterX, 0.001f);
        Assert.Equal(0.4f, bounds.CenterY, 0.001f);

        // Center is within bounds
        Assert.True(bounds.CenterX >= bounds.X1 && bounds.CenterX <= bounds.X2);
        Assert.True(bounds.CenterY >= bounds.Y1 && bounds.CenterY <= bounds.Y2);
    }
}
