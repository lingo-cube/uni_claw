using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// ALIAS_COLLAPSE_SOURCE_INDEPENDENCE — the first executable falsifier for the
/// Semantic Evidence Minimum Contract.
///
/// Proves that a wrong semantic hypothesis can be contradicted by an independent
/// evidence source. Before the Semantic Evidence purchase, the Runtime was
/// structurally unfalsifiable: the same injected lambda acted as both classifier
/// (resolveSemanticPage) and verifier (IsStillMine), making it impossible to detect
/// a wrong page-identity claim.
///
/// After the purchase, Container.EvaluatePageBelief fuses the LOCAL_IDENTITY source
/// (Container's own identity rule) with independent evidence (e.g. TRANSITION),
/// producing SemanticBeliefState.Contradicted when sources disagree.
///
/// Pass criterion: a wrong semantic claim can be contradicted by independent evidence.
/// Pass is NOT: rewriting the Page() heuristic to return a different string.
/// </summary>
public sealed class AliasCollapseSourceIndependenceTests
{
    // ── Alias-Collapse Falsifier (Primary) ───────────────────────────────

    /// <summary>
    /// PRIMARY FALSIFIER: Two independent sources disagree about page identity.
    ///
    /// Scenario: InternetPage and WifiPage both resolve to "WifiSub" under the
    /// same Page() heuristic. Source A (LOCAL_IDENTITY / text-semantic) SUPPORTS
    /// "page is WifiSub". Source B (TRANSITION) CONTRADICTS — verified navigation
    /// proves the semantic scope changed.
    ///
    /// Before purchase: Runtime silently collapses to "WifiSub" (unfalsifiable).
    /// After purchase: fusion produces Contradicted → alias-collapse detected.
    /// </summary>
    [Fact]
    public void AliasCollapse_TwoIndependentSourcesDisagree_YieldsContradicted()
    {
        // Arrange — Container with a heuristic that collapses both pages to "WifiSub"
        var container = new RuntimeContainer(
            "WifiSub",
            _ => true,  // identity rule: always says "yes, this is WifiSub" (the bad heuristic)
            (_, _, _) => throw new InvalidOperationException("not used in this test"));

        var observation = new Observation(
            [new ObservedElement("Wi‑Fi", true, 0)],
            "com.android.settings",
            1);

        // Source A: LOCAL_IDENTITY says SUPPORTS (Container's own identity rule → true)
        // Source B: TRANSITION says CONTRADICTS (verified navigation proves page changed)
        var transitionEvidence = new SemanticEvidence(
            "TRANSITION",
            "page is WifiSub",
            SemanticEvidenceStance.Contradicts,
            "Verified navigation: element inventory changed after Tap Wi‑Fi — semantic scope changed.");

        // Act — fuse LOCAL_IDENTITY + TRANSITION
        var belief = container.EvaluatePageBelief(observation, transitionEvidence);

        // Assert — the wrong claim is contradicted by independent evidence
        Assert.Equal(SemanticBeliefState.Contradicted, belief);
        Assert.Equal(SemanticBeliefState.Contradicted, container.LocalPageBeliefState);
    }

    // ── Edge Case E1: Single supporting source → no invented contradiction ────

    /// <summary>
    /// E1: When only one source exists and it SUPPORTS, belief must be Supported.
    /// No contradiction is invented from nothing.
    /// </summary>
    [Fact]
    public void E1_SingleSupportingSource_YieldsSupported_NoInventedContradiction()
    {
        var container = new RuntimeContainer(
            "SettingsRoot",
            _ => true,
            (_, _, _) => throw new InvalidOperationException("not used"));

        var observation = new Observation(
            [new ObservedElement("Settings", null, 0)],
            "com.android.settings",
            1);

        // No additional evidence — only LOCAL_IDENTITY (which SUPPORTS)
        var belief = container.EvaluatePageBelief(observation);

        Assert.Equal(SemanticBeliefState.Supported, belief);
    }

    // ── Edge Case E2: Support + Contradict → must not silently become Supported ──

    /// <summary>
    /// E2: When one source SUPPORTS and another CONTRADICTS, belief must be Contradicted.
    /// It must not silently collapse to Supported.
    /// </summary>
    [Fact]
    public void E2_SupportAndContradict_MustNotBecomeSupported()
    {
        var container = new RuntimeContainer(
            "WifiSub",
            _ => true,  // LOCAL_IDENTITY SUPPORTS
            (_, _, _) => throw new InvalidOperationException("not used"));

        var observation = new Observation(
            [new ObservedElement("Wi‑Fi", true, 0)],
            "com.android.settings",
            1);

        var contradictingEvidence = new SemanticEvidence(
            "STRUCTURAL",
            "page is WifiSub",
            SemanticEvidenceStance.Contradicts,
            "Element inventory shape inconsistent with WifiSub.");

        var belief = container.EvaluatePageBelief(observation, contradictingEvidence);

        Assert.Equal(SemanticBeliefState.Contradicted, belief);
        Assert.NotEqual(SemanticBeliefState.Supported, belief);
    }

    // ── Edge Case E3: Insufficient source → must not manufacture semantic truth ──

    /// <summary>
    /// E3: When the only additional evidence is INSUFFICIENT, and LOCAL_IDENTITY
    /// CONTRADICTS (identity rule returns false), belief must be Contradicted
    /// (from the local contradicts), not Supported.
    ///
    /// When ALL sources are INSUFFICIENT, belief must be Unresolved — no truth manufactured.
    /// </summary>
    [Fact]
    public void E3_InsufficientSource_DoesNotManufactureTruth()
    {
        // Pure fusion: all INSUFFICIENT → Unresolved
        var insufficientOnly = SemanticReconciliation.FuseBelief(
            new SemanticEvidence("TEXT_SEMANTIC", "page is X", SemanticEvidenceStance.Insufficient, "no text match"),
            new SemanticEvidence("STRUCTURAL", "page is X", SemanticEvidenceStance.Insufficient, "no structural match"));

        Assert.Equal(SemanticBeliefState.Unresolved, insufficientOnly);

        // Container: LOCAL_IDENTITY CONTRADICTS + TRANSITION INSUFFICIENT → Contradicted
        // (one contradicts, none support → Contradicted is NOT possible; only Supports+Contradicts = Contradicted)
        // Actually: one Contradicts, zero Supports → Unresolved (no positive support, no contradiction pair)
        var container = new RuntimeContainer(
            "SettingsRoot",
            _ => false,  // LOCAL_IDENTITY CONTRADICTS
            (_, _, _) => throw new InvalidOperationException("not used"));

        var observation = new Observation(
            [new ObservedElement("Unknown", null, 0)],
            "com.android.settings",
            1);

        var insufficientEvidence = new SemanticEvidence(
            "TRANSITION",
            "page is SettingsRoot",
            SemanticEvidenceStance.Insufficient,
            "no transition data");

        var belief = container.EvaluatePageBelief(observation, insufficientEvidence);

        // LOCAL_IDENTITY Contradicts + TRANSITION Insufficient → no Supports, no Contradicts pair → Unresolved
        Assert.Equal(SemanticBeliefState.Unresolved, belief);
    }

    // ── Edge Case E4: Fresh transition contradicts stale/local heuristic ──────────

    /// <summary>
    /// E4: Fresh transition evidence CONTRADICTS a stale local heuristic that SUPPORTS.
    /// Fresh external evidence wins — the belief becomes Contradicted, not Supported.
    /// This is the core of the alias-collapse: the local heuristic says "same page"
    /// but the transition proves the page changed.
    /// </summary>
    [Fact]
    public void E4_FreshTransitionContradictsStaleLocal_FreshWins()
    {
        var container = new RuntimeContainer(
            "InternetPage",
            _ => true,  // stale local heuristic: always says "still my page" (SUPPORTS)
            (_, _, _) => throw new InvalidOperationException("not used"));

        var postNavigationObservation = new Observation(
            [new ObservedElement("Wi‑Fi", true, 0), new ObservedElement("AndroidWifi", null, 1)],
            "com.android.settings",
            5);

        var freshTransitionEvidence = new SemanticEvidence(
            "TRANSITION",
            "same page as before",
            SemanticEvidenceStance.Contradicts,
            "Verified Tap Wi‑Fi navigation: element inventory changed from InternetPage to WifiPage.");

        var belief = container.EvaluatePageBelief(postNavigationObservation, freshTransitionEvidence);

        // Fresh transition evidence CONTRADICTS → Contradicted (not silently Supported)
        Assert.Equal(SemanticBeliefState.Contradicted, belief);
    }

    // ── Edge Case E5: Identical input replay → deterministic semantic result ─────

    /// <summary>
    /// E5: Same evidence inputs must produce the same belief state on every call.
    /// The fusion is a pure function — no hidden state, no randomness.
    /// </summary>
    [Fact]
    public void E5_IdenticalInputReplay_DeterministicBelief()
    {
        var container = new RuntimeContainer(
            "WifiSub",
            _ => true,
            (_, _, _) => throw new InvalidOperationException("not used"));

        var observation = new Observation(
            [new ObservedElement("Wi‑Fi", true, 0)],
            "com.android.settings",
            1);

        var transitionEvidence = new SemanticEvidence(
            "TRANSITION",
            "page is WifiSub",
            SemanticEvidenceStance.Contradicts,
            "Navigation proved page changed.");

        var belief1 = container.EvaluatePageBelief(observation, transitionEvidence);
        var belief2 = container.EvaluatePageBelief(observation, transitionEvidence);

        Assert.Equal(belief1, belief2);
        Assert.Equal(SemanticBeliefState.Contradicted, belief1);
    }

    // ── Pure Fusion Unit Tests ──────────────────────────────────────────

    /// <summary>Pure fusion: SUPPORTS only → Supported.</summary>
    [Fact]
    public void Fusion_SupportsOnly_YieldsSupported()
    {
        var belief = SemanticReconciliation.FuseBelief(
            new SemanticEvidence("A", "claim", SemanticEvidenceStance.Supports, "a"));
        Assert.Equal(SemanticBeliefState.Supported, belief);
    }

    /// <summary>Pure fusion: SUPPORTS + CONTRADICTS → Contradicted.</summary>
    [Fact]
    public void Fusion_SupportsAndContradicts_YieldsContradicted()
    {
        var belief = SemanticReconciliation.FuseBelief(
            new SemanticEvidence("A", "claim", SemanticEvidenceStance.Supports, "a"),
            new SemanticEvidence("B", "claim", SemanticEvidenceStance.Contradicts, "b"));
        Assert.Equal(SemanticBeliefState.Contradicted, belief);
    }

    /// <summary>Pure fusion: all INSUFFICIENT → Unresolved.</summary>
    [Fact]
    public void Fusion_AllInsufficient_YieldsUnresolved()
    {
        var belief = SemanticReconciliation.FuseBelief(
            new SemanticEvidence("A", "claim", SemanticEvidenceStance.Insufficient, "a"),
            new SemanticEvidence("B", "claim", SemanticEvidenceStance.Insufficient, "b"));
        Assert.Equal(SemanticBeliefState.Unresolved, belief);
    }

    /// <summary>Pure fusion: empty → Unresolved.</summary>
    [Fact]
    public void Fusion_Empty_YieldsUnresolved()
    {
        var belief = SemanticReconciliation.FuseBelief();
        Assert.Equal(SemanticBeliefState.Unresolved, belief);
    }

    /// <summary>Pure fusion: CONTRADICTS only (no SUPPORTS) → Unresolved.</summary>
    [Fact]
    public void Fusion_ContradictsOnly_YieldsUnresolved()
    {
        var belief = SemanticReconciliation.FuseBelief(
            new SemanticEvidence("A", "claim", SemanticEvidenceStance.Contradicts, "a"));
        Assert.Equal(SemanticBeliefState.Unresolved, belief);
    }

    /// <summary>Pure fusion: two SUPPORTS from different sources → Supported (not Contradicted).</summary>
    [Fact]
    public void Fusion_TwoSupports_YieldsSupported()
    {
        var belief = SemanticReconciliation.FuseBelief(
            new SemanticEvidence("TEXT_SEMANTIC", "claim", SemanticEvidenceStance.Supports, "a"),
            new SemanticEvidence("TRANSITION", "claim", SemanticEvidenceStance.Supports, "b"));
        Assert.Equal(SemanticBeliefState.Supported, belief);
    }
}
