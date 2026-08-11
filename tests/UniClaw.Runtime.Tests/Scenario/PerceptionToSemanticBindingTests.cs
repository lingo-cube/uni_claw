using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// PERCEPTION_TO_SEMANTIC_BINDING — executable proofs for Phase 3.
///
/// Proves that BindingAnalysis produces binding evidence from Observation signals
/// (Text + PerceptionType + spatial relation) and Container holds the reconciled
/// ObjectBinding state.
/// </summary>
public sealed class PerceptionToSemanticBindingTests
{
    // ── Wi‑Fi Domain Catalog (from Phase 2) ───────────────────────────────

    private static SemanticObject WifiConnectivity => SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);

    private static SemanticObject BluetoothConnectivity => SemanticObject.Define(
        "BluetoothConnectivity", "ConnectivitySetting", ["Enabled"]);

    // ── Wi‑Fi InternetPage Observation (RealitySeeded data) ───────────────

    private static Observation InternetPageWithWifiRow()
    {
        var toggleBounds = new ElementBounds(0.85f, 0.40f, 0.92f, 0.44f);
        var wifiBounds = new ElementBounds(0.08f, 0.40f, 0.25f, 0.44f);
        var androidWifiBounds = new ElementBounds(0.26f, 0.50f, 0.38f, 0.54f);

        return new Observation(
            [
                new ObservedElement("Internet", null, 0),
                new ObservedElement("T-Mobile", null, 1),
                new ObservedElement("", false, 2, new ElementBounds(0.87f, 0.30f, 0.92f, 0.34f), "toggle"), // Mobile data toggle
                new ObservedElement("Wi‑Fi", null, 3, wifiBounds, "menuItem"),                    // Wi‑Fi entry
                new ObservedElement("AndroidWifi", null, 4, androidWifiBounds, "menuItem"),        // SSID subtitle
                new ObservedElement("", null, 5, toggleBounds, "toggle"),                          // Wi‑Fi toggle
                new ObservedElement("Add network", null, 6),
                new ObservedElement("Wi-Fi doesn't turn backon automatically", null, 7),
            ],
            "com.android.settings", 1);
    }

    private static ElementBindingCriteria WifiBindingCriteria()
    {
        var wifi = WifiConnectivity;
        return new ElementBindingCriteria(
            KnownObjects: [wifi],
            ObjectTextAnchors: new Dictionary<string, string> { ["WifiConnectivity"] = "Wi‑Fi" }.ToImmutableDictionary(),
            ObjectControlTypes: new Dictionary<string, string> { ["WifiConnectivity"] = "toggle" }.ToImmutableDictionary());
    }

    // ── P1: WIFI ROW + TOGGLE ────────────────────────────────────────────

    [Fact]
    public void P1_WifiRowAndToggle_ProducesBindingEvidence()
    {
        var obs = InternetPageWithWifiRow();
        var criteria = WifiBindingCriteria();
        var evidence = BindingAnalysis.Analyze(obs, criteria);

        // TEXT_IDENTITY: supports WifiConnectivity binding (element "Wi‑Fi" found)
        var textEvidence = evidence.Where(e => e.Evidence.Source == "TEXT_IDENTITY" && e.Evidence.Claim == "binds to WifiConnectivity").ToImmutableArray();
        Assert.NotEmpty(textEvidence);
        Assert.Contains(textEvidence, e => e.Evidence.Stance == SemanticEvidenceStance.Supports);

        // SPATIAL_RELATION: supports WifiConnectivity binding (Wi‑Fi + toggle on same row)
        var spatialEvidence = evidence.Where(e => e.Evidence.Source == "SPATIAL_RELATION" && e.Evidence.Claim == "binds to WifiConnectivity").ToImmutableArray();
        Assert.NotEmpty(spatialEvidence);
        Assert.Contains(spatialEvidence, e => e.Evidence.Stance == SemanticEvidenceStance.Supports);

        // Reconcile into bindings
        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);
        Assert.Single(bindings);
        Assert.Equal("WifiConnectivity", bindings[0].ObjectIdentity);
        Assert.Equal(2, bindings[0].ElementIndices.Length); // Wi‑Fi entry + toggle
        Assert.Contains(3, bindings[0].ElementIndices); // Wi‑Fi entry
        Assert.Contains(5, bindings[0].ElementIndices); // toggle
    }

    // ── P2: WRONG ROW TOGGLE ─────────────────────────────────────────────

    [Fact]
    public void P2_WrongRowToggle_DoesNotBindToWifiConnectivity()
    {
        // Toggle aligned with T-Mobile, not with Wi‑Fi
        var obs = new Observation(
            [
                new ObservedElement("T-Mobile", null, 0, new ElementBounds(0.08f, 0.30f, 0.25f, 0.34f), "menuItem"),
                new ObservedElement("", null, 1, new ElementBounds(0.85f, 0.30f, 0.92f, 0.34f), "toggle"), // same row as T-Mobile
                new ObservedElement("Wi‑Fi", null, 2, new ElementBounds(0.08f, 0.50f, 0.25f, 0.54f), "menuItem"), // different row
            ],
            "com.android.settings", 1);

        var criteria = WifiBindingCriteria();
        var evidence = BindingAnalysis.Analyze(obs, criteria);

        // TEXT_IDENTITY supports binding (Wi‑Fi text found)
        var textEvidence = evidence.Where(e => e.Evidence.Source == "TEXT_IDENTITY" && e.Evidence.Claim == "binds to WifiConnectivity").ToImmutableArray();
        Assert.Contains(textEvidence, e => e.Evidence.Stance == SemanticEvidenceStance.Supports);

        // SPATIAL_RELATION: toggle at y=0.30 is NOT same-row as Wi‑Fi at y=0.50
        var spatialEvidence = evidence.Where(e => e.Evidence.Source == "SPATIAL_RELATION" && e.Evidence.Claim == "binds to WifiConnectivity").ToImmutableArray();
        Assert.Empty(spatialEvidence);

        // Binding: only Wi‑Fi text element, no toggle (wrong row)
        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);
        Assert.Single(bindings);
        Assert.Single(bindings[0].ElementIndices); // only Wi‑Fi text, no toggle
        Assert.Equal(2, bindings[0].ElementIndices[0]);
    }

    // ── P3: UNRELATED WIFI TEXT ──────────────────────────────────────────

    [Fact]
    public void P3_UnrelatedWifiText_DoesNotBecomeBinding()
    {
        // "Wi-Fi doesn't turn back on automatically" contains "Wi‑Fi" — substring trap
        // Exact match required
        var obs = new Observation(
            [
                new ObservedElement("Wi-Fi doesn't turn backon automatically", null, 0), // NOT "Wi‑Fi"
            ],
            "com.android.settings", 1);

        var criteria = WifiBindingCriteria(); // text anchor = "Wi‑Fi"
        var evidence = BindingAnalysis.Analyze(obs, criteria);

        // TEXT_IDENTITY: Insufficient (no exact match for "Wi‑Fi")
        var textEvidence = evidence.Single(e => e.Evidence.Source == "TEXT_IDENTITY");
        Assert.Equal(SemanticEvidenceStance.Insufficient, textEvidence.Evidence.Stance);

        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);
        Assert.Empty(bindings); // no binding
    }

    // ── P4: DUPLICATE TEXT ───────────────────────────────────────────────

    [Fact]
    public void P4_DuplicateText_SpatialContextDistinguishes()
    {
        // Two "Internet" elements: one at left, one at right
        var obs = new Observation(
            [
                new ObservedElement("Internet", null, 0, new ElementBounds(0.08f, 0.30f, 0.25f, 0.34f), "menuItem"),
                new ObservedElement("Internet", null, 1, new ElementBounds(0.50f, 0.30f, 0.67f, 0.34f), "menuItem"),
                new ObservedElement("", null, 2, new ElementBounds(0.85f, 0.30f, 0.92f, 0.34f), "toggle"), // same row as BOTH
            ],
            "com.android.settings", 1);

        var obj = SemanticObject.Define("InternetEntry", "NavigableContainer");
        var criteria = new ElementBindingCriteria(
            [obj],
            new Dictionary<string, string> { ["InternetEntry"] = "Internet" }.ToImmutableDictionary(),
            new Dictionary<string, string> { ["InternetEntry"] = "toggle" }.ToImmutableDictionary());

        var evidence = BindingAnalysis.Analyze(obs, criteria);
        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);

        Assert.Single(bindings);
        // Both "Internet" elements + toggle produce binding evidence
        Assert.True(bindings[0].ElementIndices.Length >= 2,
            $"Expected >=2 bound elements (both Internet duplicates), got {bindings[0].ElementIndices.Length}");
    }

    // ── P5: EMPTY TOGGLE ─────────────────────────────────────────────────

    [Fact]
    public void P5_EmptyToggle_VisibleToBinding()
    {
        var obs = InternetPageWithWifiRow();
        var criteria = WifiBindingCriteria();
        var evidence = BindingAnalysis.Analyze(obs, criteria);

        // Toggle at Index 5 has Text="" — but it IS found by PerceptionType="toggle"
        var spatialEvidence = evidence.Where(e => e.Evidence.Source == "SPATIAL_RELATION" && e.Evidence.Claim == "binds to WifiConnectivity").ToImmutableArray();
        Assert.NotEmpty(spatialEvidence);

        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);
        Assert.Contains(5, bindings[0].ElementIndices); // empty toggle is bound
    }

    // ── P6: NO TOGGLE ────────────────────────────────────────────────────

    [Fact]
    public void P6_NoToggle_PartialBinding_NoFabrication()
    {
        // Wi‑Fi text exists but no toggle in observation
        var obs = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0, new ElementBounds(0.08f, 0.40f, 0.25f, 0.44f), "menuItem"),
            ],
            "com.android.settings", 1);

        var criteria = WifiBindingCriteria();
        var evidence = BindingAnalysis.Analyze(obs, criteria);

        // TEXT_IDENTITY supports binding
        var textEvidence = evidence.Single(e => e.Evidence.Source == "TEXT_IDENTITY");
        Assert.Equal(SemanticEvidenceStance.Supports, textEvidence.Evidence.Stance);

        // NO SPATIAL_RELATION (no toggle found)
        var spatialEvidence = evidence.Where(e => e.Evidence.Source == "SPATIAL_RELATION").ToImmutableArray();
        Assert.Empty(spatialEvidence);

        // Binding: only Wi‑Fi element, no fabricated toggle
        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);
        Assert.Single(bindings);
        Assert.Single(bindings[0].ElementIndices); // only Wi‑Fi text
    }

    // ── P7: NO STATE FABRICATION ─────────────────────────────────────────

    [Fact]
    public void P7_NoStateFabrication_BindingDoesNotDeclareEnabled()
    {
        var obs = InternetPageWithWifiRow();
        var criteria = WifiBindingCriteria();
        var evidence = BindingAnalysis.Analyze(obs, criteria);
        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);

        Assert.Single(bindings);
        var binding = bindings[0];

        // ObjectBinding has NO state fields — no Enabled=true/false
        var props = typeof(ObjectBinding).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("State", props);
        Assert.DoesNotContain("Enabled", props);
        Assert.DoesNotContain("SwitchState", props);

        // ObjectBinding only has: ObjectIdentity, ElementIndices, EvidenceBasis
        Assert.Equal("WifiConnectivity", binding.ObjectIdentity);
        Assert.NotEmpty(binding.ElementIndices);
        Assert.NotEmpty(binding.EvidenceBasis);
    }

    // ── P8: OBSERVATION REFRESH ──────────────────────────────────────────

    [Fact]
    public void P8_ObservationRefresh_BindingUpdatesWithFreshIndices()
    {
        // Observation N: Wi‑Fi at Index 3, toggle at Index 5
        var obs1 = InternetPageWithWifiRow();
        var criteria = WifiBindingCriteria();

        // Container: simulate binding update
        var container = new RuntimeContainer("InternetPage", _ => true,
            (_, _, _) => throw new InvalidOperationException("not used"));

        var evidence1 = BindingAnalysis.Analyze(obs1, criteria);
        var bindings1 = BindingReconciler.Reconcile(evidence1, criteria.KnownObjects);
        container.UpdateBindings(bindings1);

        Assert.Contains(3, container.ObjectBindings[0].ElementIndices);
        Assert.Contains(5, container.ObjectBindings[0].ElementIndices);

        // Observation N+1: same Wi‑Fi row but at DIFFERENT indices (scroll/reorder)
        var obs2 = new Observation(
            [
                new ObservedElement("Settings", null, 0),
                new ObservedElement("Wi‑Fi", null, 1, new ElementBounds(0.08f, 0.40f, 0.25f, 0.44f), "menuItem"), // now Index 1
                new ObservedElement("", null, 2, new ElementBounds(0.85f, 0.40f, 0.92f, 0.44f), "toggle"), // now Index 2
            ],
            "com.android.settings", 2);

        var evidence2 = BindingAnalysis.Analyze(obs2, criteria);
        var bindings2 = BindingReconciler.Reconcile(evidence2, criteria.KnownObjects);
        container.UpdateBindings(bindings2);

        // Binding refreshed with NEW indices
        Assert.Contains(1, container.ObjectBindings[0].ElementIndices);
        Assert.Contains(2, container.ObjectBindings[0].ElementIndices);
        Assert.DoesNotContain(3, container.ObjectBindings[0].ElementIndices); // old index gone
    }

    // ── P9: UNKNOWN OBJECT ───────────────────────────────────────────────

    [Fact]
    public void P9_UnknownObject_NoBinding()
    {
        // Observation has no Bluetooth-related elements
        var obs = InternetPageWithWifiRow();

        var bluetooth = BluetoothConnectivity;
        var criteria = new ElementBindingCriteria(
            [bluetooth],
            new Dictionary<string, string> { ["BluetoothConnectivity"] = "Bluetooth" }.ToImmutableDictionary());

        var evidence = BindingAnalysis.Analyze(obs, criteria);

        // TEXT_IDENTITY: Insufficient
        var textEvidence = evidence.Single(e => e.Evidence.Source == "TEXT_IDENTITY");
        Assert.Equal(SemanticEvidenceStance.Insufficient, textEvidence.Evidence.Stance);

        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);
        Assert.Empty(bindings); // no binding for unknown object
    }

    // ── P10: MULTI-DOMAIN ────────────────────────────────────────────────

    [Fact]
    public void P10_MultiDomain_BluetoothUsesSameContract()
    {
        var obs = new Observation(
            [
                new ObservedElement("Bluetooth", null, 0, new ElementBounds(0.08f, 0.55f, 0.25f, 0.59f), "menuItem"),
                new ObservedElement("", null, 1, new ElementBounds(0.85f, 0.55f, 0.92f, 0.59f), "toggle"),
            ],
            "com.android.settings", 1);

        var criteria = new ElementBindingCriteria(
            [BluetoothConnectivity],
            new Dictionary<string, string> { ["BluetoothConnectivity"] = "Bluetooth" }.ToImmutableDictionary(),
            new Dictionary<string, string> { ["BluetoothConnectivity"] = "toggle" }.ToImmutableDictionary());

        var evidence = BindingAnalysis.Analyze(obs, criteria);
        var bindings = BindingReconciler.Reconcile(evidence, criteria.KnownObjects);

        Assert.Single(bindings);
        Assert.Equal("BluetoothConnectivity", bindings[0].ObjectIdentity);
        Assert.Equal(2, bindings[0].ElementIndices.Length);
    }

    // ── CONTAINER BINDING LIFECYCLE ──────────────────────────────────────

    [Fact]
    public void Container_BindResetsObjectBindings()
    {
        var container = new RuntimeContainer("InternetPage", _ => true,
            (_, _, _) => throw new InvalidOperationException("not used"));

        // Set bindings
        var binding = new ObjectBinding("WifiConnectivity", [3, 5], "TEXT_IDENTITY+SPATIAL_RELATION");
        container.UpdateBindings([binding]);
        Assert.Single(container.ObjectBindings);

        // Bind resets
        container.Bind(new Observation([], "com.android.settings", 1));
        Assert.Empty(container.ObjectBindings);
    }

    // ── SAME ROW PREDICATE ───────────────────────────────────────────────

    [Fact]
    public void SameRow_VerticalOverlap_ReturnsTrue()
    {
        var a = new ElementBounds(0.08f, 0.40f, 0.25f, 0.44f);
        var b = new ElementBounds(0.85f, 0.40f, 0.92f, 0.44f);
        Assert.True(BindingAnalysis.SameRow(a, b));
    }

    [Fact]
    public void SameRow_DifferentRows_ReturnsFalse()
    {
        var a = new ElementBounds(0.08f, 0.30f, 0.25f, 0.34f);
        var b = new ElementBounds(0.85f, 0.50f, 0.92f, 0.54f);
        Assert.False(BindingAnalysis.SameRow(a, b));
    }

    [Fact]
    public void SameRow_AdjacentRows_ReturnsFalse()
    {
        var a = new ElementBounds(0.08f, 0.40f, 0.25f, 0.44f);
        var b = new ElementBounds(0.85f, 0.45f, 0.92f, 0.49f); // just below
        Assert.False(BindingAnalysis.SameRow(a, b));
    }

    // ── STATELESS ────────────────────────────────────────────────────────

    [Fact]
    public void BindingAnalysis_IsStateless_SameInputSameOutput()
    {
        var obs = InternetPageWithWifiRow();
        var criteria = WifiBindingCriteria();

        var r1 = BindingAnalysis.Analyze(obs, criteria);
        var r2 = BindingAnalysis.Analyze(obs, criteria);

        Assert.Equal(r1.Length, r2.Length);
        for (int i = 0; i < r1.Length; i++)
            Assert.Equal(r1[i], r2[i]);
    }
}
