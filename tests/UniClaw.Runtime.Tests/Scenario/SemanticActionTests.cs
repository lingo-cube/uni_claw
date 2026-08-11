using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SEMANTIC_ACTION — executable proofs for Phase 4.
///
/// Proves that SemanticAction is a domain-level desired effect (not a UI procedure),
/// Agent authorizes, Traversal lowers to ExecutionAction with safety rules.
/// </summary>
public sealed class SemanticActionTests
{
    private static SemanticObject WifiConnectivity => SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static SemanticObject BluetoothConnectivity => SemanticObject.Define(
        "BluetoothConnectivity", "ConnectivitySetting", ["Enabled"]);

    private static Capability SetEnabled => Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");

    // ── P1: SEMANTIC ACTION != EXECUTION ACTION ──────────────────────────

    [Fact]
    public void P1_SemanticAction_ContainsNoUiExecutionDetails()
    {
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        Assert.Equal("WifiConnectivity", action.ObjectIdentity);
        Assert.Equal("SetEnabled", action.CapabilityName);
        Assert.Equal("Enabled", action.StateDimension);
        Assert.True(action.DesiredValue);

        // Type system: SemanticAction has NO UI execution fields
        var props = typeof(SemanticAction).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Index", props);
        Assert.DoesNotContain("Bounds", props);
        Assert.DoesNotContain("Tap", props);
        Assert.DoesNotContain("SetSwitch", props);
        Assert.DoesNotContain("TargetDescription", props);
    }

    // ── P2: AGENT AUTHORITY ──────────────────────────────────────────────

    [Fact]
    public void P2_AgentAuthorizes_ValidAction_ReturnsNull()
    {
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);
        var result = RuntimeAgent.AuthorizeAction(action, WifiConnectivity, SetEnabled);
        Assert.Null(result); // authorized
    }

    [Fact]
    public void P2_AgentRejects_CategoryMismatch()
    {
        var nonConnectivity = SemanticObject.Define("StaticLabel", "TextLabel");
        var action = new SemanticAction("StaticLabel", "SetEnabled", "Enabled", true);
        var result = RuntimeAgent.AuthorizeAction(action, nonConnectivity, SetEnabled);
        Assert.IsType<SemanticActionResult.Invalid>(result);
    }

    // ── P3: CATEGORY APPLICABILITY ───────────────────────────────────────

    [Fact]
    public void P3_CapabilityRejects_IncompatibleObjectCategory()
    {
        var incompatible = SemanticObject.Define("SomeText", "TextLabel");
        var action = new SemanticAction("SomeText", "SetEnabled", "Enabled", true);
        var result = RuntimeAgent.AuthorizeAction(action, incompatible, SetEnabled);
        Assert.NotNull(result);
        Assert.IsType<SemanticActionResult.Invalid>(result);
    }

    // ── P4: STATE DIMENSION VALIDITY ─────────────────────────────────────

    [Fact]
    public void P4_Rejects_WrongStateDimension()
    {
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Brightness", true);
        var result = RuntimeAgent.AuthorizeAction(action, WifiConnectivity, SetEnabled);
        Assert.IsType<SemanticActionResult.Invalid>(result);
    }

    [Fact]
    public void P4_Rejects_DimensionNotDeclaredByObject()
    {
        var noStateObj = SemanticObject.Define("StaticLabel", "ConnectivitySetting"); // no state dims
        var action = new SemanticAction("StaticLabel", "SetEnabled", "Enabled", true);
        var result = RuntimeAgent.AuthorizeAction(action, noStateObj, SetEnabled);
        Assert.IsType<SemanticActionResult.Invalid>(result);
    }

    // ── P5: MULTI-ELEMENT BINDING — SELECTS TOGGLE, NOT TEXT ─────────────

    [Fact]
    public void P5_MultiElementBinding_SelectsToggleNotText()
    {
        var obs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0, new ElementBounds(0.08f, 0.40f, 0.25f, 0.44f), "menuItem"),
                new ObservedElement("", false, 1, new ElementBounds(0.85f, 0.40f, 0.92f, 0.44f), "toggle"),
            ],
            "com.android.settings", 1);

        var binding = new ObjectBinding("WifiConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);

        // Should dispatch SetSwitch targeting the TOGGLE (Index 1), not the text (Index 0)
        var dispatched = Assert.IsType<SemanticActionResult.Dispatched>(result);
        var ss = Assert.IsType<DeviceAction.SetSwitch>(dispatched.Action);
        Assert.Equal(1, ss.TargetElementIndex); // toggle, not Wi‑Fi text
        Assert.True(ss.TargetState);
    }

    // ── P6: EMPTY TOGGLE ─────────────────────────────────────────────────

    [Fact]
    public void P6_EmptyToggle_UsableThroughBinding()
    {
        var obs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0, null, "menuItem"),
                new ObservedElement("", false, 1, new ElementBounds(0.85f, 0.40f, 0.92f, 0.44f), "toggle"), // Text=""
            ],
            "com.android.settings", 1);

        var binding = new ObjectBinding("WifiConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);
        Assert.IsType<SemanticActionResult.Dispatched>(result);
    }

    // ── P7: STALE BINDING ────────────────────────────────────────────────

    [Fact]
    public void P7_StaleBinding_NoToggleAtOldIndex()
    {
        // Binding from old observation: Wi‑Fi at 0, toggle at 1
        var binding = new ObjectBinding("WifiConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");

        // Fresh observation: Wi‑Fi at 2, toggle at 3 (indices shifted)
        var freshObs = new Observation(
            [
                new ObservedElement("Settings", null, 0),
                new ObservedElement("Network", null, 1),
                new ObservedElement("Wi‑Fi", null, 2, null, "menuItem"),
                new ObservedElement("", false, 3, new ElementBounds(0.85f, 0.40f, 0.92f, 0.44f), "toggle"),
            ],
            "com.android.settings", 2);

        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);
        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, freshObs);

        // Old indices 0,1 point to "Settings" and "Network" — not toggle
        Assert.IsType<SemanticActionResult.Unresolved>(result);
    }

    // ── P8: AMBIGUOUS INTERACTION SURFACE ─────────────────────────────────

    [Fact]
    public void P8_Ambiguous_MultipleToggles_NoDispatch()
    {
        var obs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0, null, "menuItem"),
                new ObservedElement("", false, 1, null, "toggle"),
                new ObservedElement("", false, 2, null, "toggle"), // second toggle!
            ],
            "com.android.settings", 1);

        var binding = new ObjectBinding("WifiConnectivity", [0, 1, 2], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);
        Assert.IsType<SemanticActionResult.Unresolved>(result);
    }

    // ── P9: UNKNOWN STATE — NO BLIND TOGGLE ──────────────────────────────

    [Fact]
    public void P9_UnknownState_NoDispatch()
    {
        var obs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0, null, "menuItem"),
                new ObservedElement("", null, 1, null, "toggle"), // SwitchState=null!
            ],
            "com.android.settings", 1);

        var binding = new ObjectBinding("WifiConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);
        Assert.IsType<SemanticActionResult.StateUnknown>(result);
    }

    // ── P10: ALREADY SATISFIED — NO TOGGLE ───────────────────────────────

    [Fact]
    public void P10_AlreadySatisfied_NoToggleDispatch()
    {
        var obs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0, null, "menuItem"),
                new ObservedElement("", true, 1, null, "toggle"), // already ON
            ],
            "com.android.settings", 1);

        var binding = new ObjectBinding("WifiConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);
        Assert.IsType<SemanticActionResult.NoOp>(result);
    }

    // ── P11: KNOWN STATE CHANGE — DISPATCH SetSwitch ─────────────────────

    [Fact]
    public void P11_KnownOff_DesiredOn_DispatchesSetSwitch()
    {
        var toggleBounds = new ElementBounds(0.85f, 0.40f, 0.92f, 0.44f);
        var obs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0, null, "menuItem"),
                new ObservedElement("", false, 1, toggleBounds, "toggle"), // OFF
            ],
            "com.android.settings", 1);

        var binding = new ObjectBinding("WifiConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);

        var dispatched = Assert.IsType<SemanticActionResult.Dispatched>(result);
        var ss = Assert.IsType<DeviceAction.SetSwitch>(dispatched.Action);
        Assert.Equal(1, ss.TargetElementIndex);
        Assert.True(ss.TargetState);
        Assert.Equal(toggleBounds, ss.TargetBounds);
    }

    // ── P12: DISPATCH != EFFECT ──────────────────────────────────────────

    [Fact]
    public void P12_DispatchDoesNotProveEffect()
    {
        var obs = new Observation(
            [new ObservedElement("Wi‑Fi", null, 0, null, "menuItem"), new ObservedElement("", false, 1, null, "toggle")],
            "com.android.settings", 1);

        var binding = new ObjectBinding("WifiConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);
        Assert.IsType<SemanticActionResult.Dispatched>(result);

        // Dispatched ≠ world effect. A fresh Observation must verify the desired state.
        // This is proven by the SemanticActionResult type system:
        // Dispatched only means "an ExecutionAction was produced" — not "world changed."
    }

    // ── P13: SECOND DOMAIN — Bluetooth ───────────────────────────────────

    [Fact]
    public void P13_Bluetooth_UsesSameSemanticActionContract()
    {
        var obs = new Observation(
            [
                new ObservedElement("Bluetooth", null, 0, null, "menuItem"),
                new ObservedElement("", false, 1, null, "toggle"),
            ],
            "com.android.settings", 1);

        var binding = new ObjectBinding("BluetoothConnectivity", [0, 1], "TEXT_IDENTITY+SPATIAL_RELATION");
        var action = new SemanticAction("BluetoothConnectivity", "SetEnabled", "Enabled", true); // OFF→ON

        // Authorize
        var authResult = RuntimeAgent.AuthorizeAction(action, BluetoothConnectivity, SetEnabled);
        Assert.Null(authResult); // authorized

        // Lower
        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);
        var dispatched = Assert.IsType<SemanticActionResult.Dispatched>(result);
        var ss = Assert.IsType<DeviceAction.SetSwitch>(dispatched.Action);
        Assert.Equal(1, ss.TargetElementIndex);
        Assert.True(ss.TargetState); // turning ON from false
    }

    // ── BINDING MISMATCH ─────────────────────────────────────────────────

    [Fact]
    public void LowerAction_RejectsBindingForDifferentObject()
    {
        var obs = new Observation([new ObservedElement("Wi‑Fi", null, 0)], "app", 1);
        var binding = new ObjectBinding("BluetoothConnectivity", [0], "TEXT");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var result = UniClaw.Runtime.Traversal.Traversal.LowerAction(action, binding, obs);
        Assert.IsType<SemanticActionResult.Invalid>(result);
    }

    // ── SetEnabled is IDEMPOTENT ─────────────────────────────────────────

    [Fact]
    public void SetEnabled_IsIdempotent_NotBlindToggle()
    {
        // OFF → ON: dispatch
        var obsOff = new Observation(
            [new ObservedElement("", false, 0, null, "toggle")], "app", 1);
        var binding = new ObjectBinding("WifiConnectivity", [0], "TEXT_IDENTITY");
        var actionOn = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var r1 = UniClaw.Runtime.Traversal.Traversal.LowerAction(actionOn, binding, obsOff);
        Assert.IsType<SemanticActionResult.Dispatched>(r1);

        // ON → ON: already satisfied, no toggle
        var obsOn = new Observation(
            [new ObservedElement("", true, 0, null, "toggle")], "app", 1);
        var r2 = UniClaw.Runtime.Traversal.Traversal.LowerAction(actionOn, binding, obsOn);
        Assert.IsType<SemanticActionResult.NoOp>(r2);

        // ON → OFF: dispatch SetSwitch(false)
        var actionOff = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", false);
        var r3 = UniClaw.Runtime.Traversal.Traversal.LowerAction(actionOff, binding, obsOn);
        var d3 = Assert.IsType<SemanticActionResult.Dispatched>(r3);
        Assert.False(((DeviceAction.SetSwitch)d3.Action).TargetState);
    }

    // ── SemanticAction Immutability ──────────────────────────────────────

    [Fact]
    public void SemanticAction_IsImmutable()
    {
        var a1 = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);
        var a2 = a1 with { DesiredValue = false };
        Assert.True(a1.DesiredValue);
        Assert.False(a2.DesiredValue);
        Assert.NotEqual(a1, a2);
    }

    [Fact]
    public void SemanticAction_Validation_RejectsEmpty()
    {
        Assert.False(new SemanticAction("", "SetEnabled", "Enabled", true).IsValid);
        Assert.False(new SemanticAction("Wifi", "", "Enabled", true).IsValid);
        Assert.False(new SemanticAction("Wifi", "SetEnabled", "", true).IsValid);
    }
}
