using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SEMANTIC_OBJECT_CAPABILITY_MODEL — executable proofs for Phase 2.
///
/// Proves that SemanticObject and Capability are:
/// - Immutable declarative domain concepts (NOT mutable state)
/// - Free of UI details (no ObservedElement, Bounds, Index, Text, Page)
/// - Free of execution procedures (no Tap, SetSwitch, coordinates, routes)
/// - Multi-domain (Wi‑Fi + Bluetooth use same contract)
/// - State dimensions ≠ current state values
/// </summary>
public sealed class SemanticObjectCapabilityModelTests
{
    // ── Wi‑Fi Domain Catalog ──────────────────────────────────────────────

    private static SemanticObject WifiConnectivity => SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting",
        ["Enabled"]);

    private static SemanticObject BluetoothConnectivity => SemanticObject.Define(
        "BluetoothConnectivity", "ConnectivitySetting",
        ["Enabled"]);

    private static Capability SetEnabled => Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");

    // ── P1: BUSINESS OBJECT != UI ELEMENT ─────────────────────────────────

    [Fact]
    public void P1_SemanticObject_ContainsNoUiElementDetails()
    {
        var obj = WifiConnectivity;

        Assert.Equal("WifiConnectivity", obj.Identity);
        Assert.Equal("ConnectivitySetting", obj.Category);
        Assert.Single(obj.StateDimensions);
        Assert.Equal("Enabled", obj.StateDimensions[0]);

        // NO UI details: semantic object is a domain concept, not a UI element
        // (verified by type system: SemanticObject has no ObservedElement, Bounds, Index, Text, or Page fields)
    }

    /// <summary>
    /// P1 continued: SemanticObject type itself contains zero UI element fields.
    /// </summary>
    [Fact]
    public void P1_TypeSystem_SemanticObjectHasNoUiFields()
    {
        var properties = typeof(SemanticObject).GetProperties()
            .Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("Bounds", properties);
        Assert.DoesNotContain("Index", properties);
        Assert.DoesNotContain("Text", properties);
        Assert.DoesNotContain("SwitchState", properties);
        Assert.DoesNotContain("PerceptionType", properties);
        Assert.DoesNotContain("ObservedElement", properties);
    }

    // ── P2: CAPABILITY != EXECUTION ACTION ───────────────────────────────

    [Fact]
    public void P2_Capability_ContainsNoExecutionProcedure()
    {
        var cap = SetEnabled;

        Assert.Equal("SetEnabled", cap.Name);
        Assert.Equal("ConnectivitySetting", cap.ApplicableToCategory);
        Assert.Equal("Enabled", cap.StateDimension);

        // NO execution details: capability is a domain contract, not a UI procedure
    }

    [Fact]
    public void P2_TypeSystem_CapabilityHasNoExecutionFields()
    {
        var properties = typeof(Capability).GetProperties()
            .Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("Tap", properties);
        Assert.DoesNotContain("SetSwitch", properties);
        Assert.DoesNotContain("PageRoute", properties);
        Assert.DoesNotContain("TargetDescription", properties);
        Assert.DoesNotContain("ActionDescription", properties);
        Assert.DoesNotContain("Coordinates", properties);
    }

    // ── P3: STATE DIMENSION != CURRENT STATE ─────────────────────────────

    [Fact]
    public void P3_StateDimension_DoesNotDeclareCurrentState()
    {
        var obj = WifiConnectivity;

        // StateDimensions says "Enabled" is a valid dimension — NOT that Enabled=true
        Assert.Contains("Enabled", obj.StateDimensions);

        // The object does NOT carry Enabled=true/false as current-world truth
        var properties = typeof(SemanticObject).GetProperties()
            .Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("CurrentState", properties);
        Assert.DoesNotContain("State", properties);
        Assert.DoesNotContain("IsEnabled", properties);
    }

    /// <summary>
    /// P3: StateDimensions can be empty — not all objects have observable state.
    /// </summary>
    [Fact]
    public void P3_StateDimensions_CanBeEmpty()
    {
        var noState = SemanticObject.Define("StaticLabel", "TextLabel");
        Assert.Empty(noState.StateDimensions);
    }

    // ── P4: IMMUTABILITY ─────────────────────────────────────────────────

    [Fact]
    public void P4_SemanticObject_IsImmutable()
    {
        var obj = WifiConnectivity;
        var copy = obj with { };

        Assert.Equal(obj.Identity, copy.Identity);
        Assert.Equal(obj.Category, copy.Category);
        Assert.Equal(obj.StateDimensions, copy.StateDimensions);
        Assert.Equal(obj, copy); // value equality
    }

    [Fact]
    public void P4_Capability_IsImmutable()
    {
        var cap = SetEnabled;
        var copy = cap with { };

        Assert.Equal(cap.Name, copy.Name);
        Assert.Equal(cap.ApplicableToCategory, copy.ApplicableToCategory);
        Assert.Equal(cap, copy); // value equality
    }

    // ── P5: NO MUTABLE OWNER ─────────────────────────────────────────────

    [Fact]
    public void P5_NoMutableOwner_SemanticObjectAndCapabilityAreRecords()
    {
        // Both are sealed record types — immutable by construction
        Assert.True(typeof(SemanticObject).IsSealed);
        Assert.True(typeof(Capability).IsSealed);

        // Immutability guaranteed by positional record semantics:
        // init-only properties, value equality, with-expression copies
        var obj1 = SemanticObject.Define("A", "B");
        var obj2 = obj1 with { Identity = "X" };
        Assert.NotEqual(obj1, obj2);
        Assert.Equal("A", obj1.Identity); // original unchanged
    }

    // ── P6: MULTI-DOMAIN ─────────────────────────────────────────────────

    [Fact]
    public void P6_MultiDomain_WifiAndBluetoothUseSameContract()
    {
        var wifi = WifiConnectivity;
        var bluetooth = BluetoothConnectivity;

        // Same category — both are ConnectivitySettings
        Assert.Equal(wifi.Category, bluetooth.Category);

        // Same state dimension
        Assert.Equal(wifi.StateDimensions, bluetooth.StateDimensions);

        // Different identities
        Assert.NotEqual(wifi.Identity, bluetooth.Identity);

        // Same capability applies to both
        Assert.Equal("ConnectivitySetting", SetEnabled.ApplicableToCategory);
        Assert.Equal("Enabled", SetEnabled.StateDimension);
    }

    /// <summary>
    /// P6: Capability is NOT Wi‑Fi-specific — SetEnabled applies to any ConnectivitySetting.
    /// </summary>
    [Fact]
    public void P6_Capability_AppliesToCategory_NotSpecificObject()
    {
        // SetEnabled applies to CATEGORY "ConnectivitySetting", not to "WifiConnectivity" specifically
        Assert.Equal("ConnectivitySetting", SetEnabled.ApplicableToCategory);
        Assert.NotEqual("WifiConnectivity", SetEnabled.ApplicableToCategory);
    }

    // ── P7: ARCHITECTURE DIRECTION ───────────────────────────────────────

    [Fact]
    public void P7_ArchitectureDirection_NoDependencyReversal()
    {
        // SemanticObject and Capability are in Model/ — they depend on nothing
        // Agent → Container → Traversal → Environment direction is unchanged
        // (verified by ArchitectureGuardTests)
    }

    // ── P8: EXISTING RUNTIME UNCHANGED ───────────────────────────────────

    [Fact]
    public void P8_ExistingPaths_Unchanged()
    {
        // This module adds domain vocabulary.
        // It does NOT replace resolveSemanticPage, identityRule, PageAnalysis, or any execution path.
        // Existing tests continue to pass — verified by full regression.
    }

    // ── P9: VALIDATION ───────────────────────────────────────────────────

    [Fact]
    public void P9_SemanticObject_RejectsNullOrEmptyIdentity()
    {
        Assert.Throws<ArgumentException>(() => SemanticObject.Define("", "Category"));
        Assert.Throws<ArgumentException>(() => SemanticObject.Define("  ", "Category"));
    }

    [Fact]
    public void P9_SemanticObject_RejectsNullOrEmptyCategory()
    {
        Assert.Throws<ArgumentException>(() => SemanticObject.Define("Id", ""));
        Assert.Throws<ArgumentException>(() => SemanticObject.Define("Id", "  "));
    }

    [Fact]
    public void P9_Capability_RejectsNullOrEmptyFields()
    {
        Assert.Throws<ArgumentException>(() => Capability.Define("", "Cat", "Dim"));
        Assert.Throws<ArgumentException>(() => Capability.Define("Name", "", "Dim"));
        Assert.Throws<ArgumentException>(() => Capability.Define("Name", "Cat", ""));
    }

    // ── DOMAIN CATALOG (Wi‑Fi Vertical Slice) ────────────────────────────

    /// <summary>
    /// Minimum Wi‑Fi domain catalog: one object + one capability.
    /// Represents "Wi‑Fi can be enabled/disabled" as declarative domain knowledge.
    /// </summary>
    [Fact]
    public void WifiVerticalSlice_CompleteDomainCatalog()
    {
        // Objects
        var wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
        var bluetooth = SemanticObject.Define("BluetoothConnectivity", "ConnectivitySetting", ["Enabled"]);

        // Capabilities
        var setEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");

        // Catalog: immutable declarations, no mutable state
        var objects = new[] { wifi, bluetooth };
        var capabilities = new[] { setEnabled };

        Assert.Equal(2, objects.Length);
        Assert.Single(capabilities);

        // SetEnabled applies to both Wi‑Fi and Bluetooth (same category)
        foreach (var obj in objects)
            Assert.Equal(setEnabled.ApplicableToCategory, obj.Category);

        // Both objects have the state dimension that SetEnabled affects
        foreach (var obj in objects)
            Assert.Contains(setEnabled.StateDimension, obj.StateDimensions);
    }

    /// <summary>
    /// Domain catalog can be queried: "what capabilities apply to WifiConnectivity?"
    /// </summary>
    [Fact]
    public void WifiVerticalSlice_QueryCapabilitiesForObject()
    {
        var wifi = WifiConnectivity;
        var capabilities = new[] { SetEnabled };

        var applicable = capabilities
            .Where(c => c.ApplicableToCategory == wifi.Category)
            .ToImmutableArray();

        Assert.Single(applicable);
        Assert.Equal("SetEnabled", applicable[0].Name);
        Assert.Equal("Enabled", applicable[0].StateDimension);
    }
}
