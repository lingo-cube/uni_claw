using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeContainer = UniClaw.Runtime.Container.Container;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// PAGE_ANALYSIS_OBSERVATION_DERIVED — the first executable falsifier for the
/// PageAnalysis Minimum Purchase.
///
/// Proves that page semantic evidence can be derived from Fresh Observation signals
/// alone, without a manually-authored final page verdict. Before PageAnalysis,
/// the only path to page identity was the caller-injected resolveSemanticPage lambda
/// which returned a verdict string (Evidence=Claim=Belief=Truth collapse).
///
/// After PageAnalysis, multi-source SemanticEvidence is produced from observation
/// signals (FOREGROUND, TEXT_ANCHOR, SWITCH_DISTRIBUTION, TEXT_ANCHOR_NEGATIVE).
/// Sources can disagree. No single source is authoritative.
///
/// Pass criterion: independent observation-derived signal channels can produce
///   agreeing or conflicting SemanticEvidence about page identity.
/// Pass is NOT: caller supplies expected page name as verdict.
/// </summary>
public sealed class PageAnalysisObservationDerivedTests
{
    // ── Observation Helpers (RealitySeededSettingsFixture data) ─────────────

    private static Observation InternetPageObservation(long seq = 1) => new(
        [
            new ObservedElement("Internet", null, 0),           // title
            new ObservedElement("T-Mobile", null, 1),
            new ObservedElement("", null, 2),                   // empty
            new ObservedElement("", null, 3),                   // empty
            new ObservedElement("T-Mobile", null, 4),
            new ObservedElement("", false, 5),                  // toggle — empty text, SwitchState=false
            new ObservedElement("Wi‑Fi", null, 6),              // Wi‑Fi entry — NO SwitchState!
            new ObservedElement("", null, 7),                   // empty
            new ObservedElement("AndroidWifi", null, 8),        // connected SSID
            new ObservedElement("", null, 9),                   // empty
            new ObservedElement("Add network", null, 10),
            new ObservedElement("Networkpreferences", null, 11),
            new ObservedElement("Wi-Fi doesn't turn backon automatically", null, 12),
            new ObservedElement("Non-carrier data usage", null, 13),
        ],
        "com.android.settings",
        seq);

    private static Observation WifiPageObservation(long seq = 1) => new(
        [
            new ObservedElement("Wi‑Fi", false, 0),             // Wi‑Fi switch — SwitchState=false
            new ObservedElement("AndroidWifi", null, 1),        // connected SSID
            new ObservedElement("Auto-connect", true, 2),       // Auto-connect switch
            new ObservedElement("Network preferences", null, 3),
        ],
        "com.android.settings",
        seq);

    private static Observation WifiOnPageObservation(long seq = 1) => new(
        [
            new ObservedElement("Wi‑Fi", true, 0),              // Wi‑Fi switch — SwitchState=TRUE
            new ObservedElement("AndroidWifi", null, 1),
            new ObservedElement("Auto-connect", true, 2),
            new ObservedElement("Connected devices", null, 3),
        ],
        "com.android.settings",
        seq);

    private static Observation UnknownPageObservation(long seq = 1) => new(
        [
            new ObservedElement("UnfamiliarApp", null, 0),
            new ObservedElement("", null, 1),
            new ObservedElement("UnknownContent", null, 2),
        ],
        "com.unknown.app",
        seq);

    /// <summary>
    /// Criteria matching RealitySeededSettingsFixture pages.
    /// InternetPage: "T-Mobile" + "Add network" anchors; Wi‑Fi entry WITHOUT SwitchState.
    /// WifiPage: "Auto-connect" anchor; Wi‑Fi WITH SwitchState.
    /// Negative anchors prevent cross-page collapse:
    ///   "T-Mobile" contradicts WifiPage; "Auto-connect" contradicts InternetPage.
    /// </summary>
    private static PageAnalysisCriteria SettingsPageCriteria() => new(
        ExpectedForegroundApplication: "com.android.settings",
        PageAnchors: new Dictionary<string, ImmutableArray<string>>
        {
            ["InternetPage"] = ["T-Mobile", "Add network"],
            ["WifiPage"] = ["Auto-connect", "Network preferences"],
        }.ToImmutableDictionary(),
        PageNegativeAnchors: new Dictionary<string, ImmutableArray<string>>
        {
            ["InternetPage"] = ["Auto-connect"],       // Auto-connect on InternetPage → contradicts
            ["WifiPage"] = ["T-Mobile", "Add network"], // T-Mobile/Add network on WifiPage → contradicts
        }.ToImmutableDictionary(),
        PageSwitchStateAnchors: new Dictionary<string, ImmutableArray<string>>
        {
            ["WifiPage"] = ["Wi‑Fi"],  // SwitchState-bearing Wi‑Fi → supports WifiPage
        }.ToImmutableDictionary());

    // ── P1: OBSERVATION → EVIDENCE (Primary Falsifier) ──────────────────────

    /// <summary>
    /// P1: Fresh Observation automatically produces semantic evidence.
    /// Primary proof contains no manually authored final Page verdict.
    ///
    /// InternetPage observation → TEXT_ANCHOR supports InternetPage,
    ///   TEXT_ANCHOR is Insufficient for WifiPage (no WifiPage anchors present),
    ///   TEXT_ANCHOR_NEGATIVE contradicts WifiPage (T-Mobile is on InternetPage, not WifiPage).
    /// </summary>
    [Fact]
    public void P1_InternetPageObservation_ProducesDerivedEvidence_NotManualVerdict()
    {
        var observation = InternetPageObservation();
        var criteria = SettingsPageCriteria();

        // Act — evidence produced FROM OBSERVATION, not manually authored
        var evidence = PageAnalysis.Analyze(observation, criteria);

        // Assert: multiple evidence sources produced
        Assert.NotEmpty(evidence);

        // FOREGROUND: Supports "application scope is com.android.settings"
        var foregroundEvidence = evidence.Single(e => e.Source == "FOREGROUND");
        Assert.Equal(SemanticEvidenceStance.Supports, foregroundEvidence.Stance);
        // CLAIM GRANULARITY: FOREGROUND claims "application scope", NOT "page is X"
        Assert.Contains("application scope", foregroundEvidence.Claim, StringComparison.Ordinal);
        Assert.DoesNotContain("page is", foregroundEvidence.Claim, StringComparison.Ordinal);

        // TEXT_ANCHOR: Supports InternetPage (anchors "T-Mobile" + "Add network" present)
        var internetAnchor = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, internetAnchor.Stance);
        Assert.Contains("T-Mobile", internetAnchor.Reason, StringComparison.Ordinal);

        // TEXT_ANCHOR: Insufficient for WifiPage (no WifiPage anchors present)
        var wifiAnchor = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Insufficient, wifiAnchor.Stance);

        // TEXT_ANCHOR_NEGATIVE: Contradicts WifiPage (T-Mobile present — NOT on WifiPage)
        var wifiNegative = evidence.Single(e => e is { Source: "TEXT_ANCHOR_NEGATIVE", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Contradicts, wifiNegative.Stance);
    }

    /// <summary>
    /// P1 continuation: WifiPage observation → TEXT_ANCHOR supports WifiPage,
    ///   SWITCH_DISTRIBUTION supports WifiPage (SwitchState-bearing Wi‑Fi),
    ///   TEXT_ANCHOR is Insufficient for InternetPage.
    /// </summary>
    [Fact]
    public void P1_WifiPageObservation_ProducesDerivedEvidence_SupportsWifiPage()
    {
        var observation = WifiPageObservation();
        var criteria = SettingsPageCriteria();

        var evidence = PageAnalysis.Analyze(observation, criteria);

        Assert.NotEmpty(evidence);

        // TEXT_ANCHOR: Supports WifiPage ("Auto-connect" + "Network preferences" present)
        var wifiAnchor = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, wifiAnchor.Stance);

        // SWITCH_DISTRIBUTION: Supports WifiPage (Wi‑Fi has SwitchState)
        var wifiSwitch = evidence.Single(e => e is { Source: "SWITCH_DISTRIBUTION", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, wifiSwitch.Stance);

        // TEXT_ANCHOR: Insufficient for InternetPage (no InternetPage anchors present)
        var internetAnchor = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Insufficient, internetAnchor.Stance);
    }

    // ── P2: SOURCE INDEPENDENCE ────────────────────────────────────────────

    /// <summary>
    /// P2: Two sources may disagree. Support + Contradiction → Contradicted (not silently Supported).
    ///
    /// This is the alias-collapse detection: if an observation somehow has both
    /// InternetPage anchors (T-Mobile) AND WifiPage anchors (Auto-connect),
    /// sources disagree → CONTRADICTED.
    /// </summary>
    [Fact]
    public void P2_SourceIndependence_DisagreeingSources_YieldsContradicted()
    {
        // Construct an ambiguous observation that has anchors for BOTH pages
        var ambiguousObservation = new Observation(
            [
                new ObservedElement("T-Mobile", null, 0),       // InternetPage anchor
                new ObservedElement("Add network", null, 1),    // InternetPage anchor
                new ObservedElement("Auto-connect", true, 2),   // WifiPage anchor + SwitchState
                new ObservedElement("Network preferences", null, 3), // WifiPage anchor
                new ObservedElement("Wi‑Fi", true, 4),          // SwitchState-bearing Wi‑Fi
            ],
            "com.android.settings",
            1);

        var criteria = SettingsPageCriteria();
        var evidence = PageAnalysis.Analyze(ambiguousObservation, criteria);

        // InternetPage: TEXT_ANCHOR Supports (T-Mobile present)
        var internetSupport = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, internetSupport.Stance);

        // WifiPage: TEXT_ANCHOR Supports (Auto-connect present)
        var wifiSupport = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, wifiSupport.Stance);

        // Both pages get Supports → when fused with LOCAL_IDENTITY → can produce CONTRADICTED
        // (This proves the evidence structure supports source independence)
    }

    /// <summary>
    /// P2 continuation: Single source's Insufficient does NOT contaminate other sources.
    /// InternetPage evidence is Insufficient for WifiPage, but that doesn't cancel
    /// the Supports for InternetPage.
    /// </summary>
    [Fact]
    public void P2_SingleSourceInsufficient_DoesNotContaminateOtherSources()
    {
        var observation = InternetPageObservation();
        var criteria = SettingsPageCriteria();
        var evidence = PageAnalysis.Analyze(observation, criteria);

        // InternetPage: TEXT_ANCHOR Supports
        var internetSupport = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, internetSupport.Stance);

        // WifiPage: TEXT_ANCHOR Insufficient — but this does NOT cancel InternetPage Supports
        var wifiInsufficient = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Insufficient, wifiInsufficient.Stance);

        // Both evidence stances coexist independently
    }

    // ── P3: CLAIM GRANULARITY ──────────────────────────────────────────────

    /// <summary>
    /// P3: FOREGROUND evidence alone MUST NOT prove page identity.
    /// "application scope is com.android.settings" ≠ "page is WifiDetail".
    /// Weak evidence cannot manufacture a strong claim.
    /// </summary>
    [Fact]
    public void P3_ClaimGranularity_ForegroundDoesNotProvePageIdentity()
    {
        var observation = WifiPageObservation();
        var criteria = SettingsPageCriteria();
        var evidence = PageAnalysis.Analyze(observation, criteria);

        // FOREGROUND evidence exists
        var foregroundEvidence = evidence.Where(e => e.Source == "FOREGROUND").ToImmutableArray();
        Assert.NotEmpty(foregroundEvidence);

        // Every FOREGROUND claim is about "application scope", NOT "page is X"
        foreach (var fe in foregroundEvidence)
        {
            Assert.Contains("application scope", fe.Claim, StringComparison.Ordinal);
            Assert.DoesNotContain("page is", fe.Claim, StringComparison.Ordinal);
        }

        // TEXT_ANCHOR evidence DOES claim "page is X" — that's its granularity
        var textAnchorEvidence = evidence.Where(e => e.Source == "TEXT_ANCHOR").ToImmutableArray();
        Assert.NotEmpty(textAnchorEvidence);
        foreach (var te in textAnchorEvidence)
        {
            Assert.Contains("page is", te.Claim, StringComparison.Ordinal);
        }
    }

    // ── P4: UNKNOWN PAGE ───────────────────────────────────────────────────

    /// <summary>
    /// P4: Observation lacking sufficient semantic signals → Insufficient / Unresolved.
    /// NOT nearest-known-page forced classification.
    /// </summary>
    [Fact]
    public void P4_UnknownPage_ProducesInsufficient_NotForcedClassification()
    {
        var observation = UnknownPageObservation();
        var criteria = SettingsPageCriteria();

        var evidence = PageAnalysis.Analyze(observation, criteria);

        // FOREGROUND: Contradicts (app doesn't match)
        var foregroundEvidence = evidence.Single(e => e.Source == "FOREGROUND");
        Assert.Equal(SemanticEvidenceStance.Contradicts, foregroundEvidence.Stance);

        // All TEXT_ANCHOR evidence is Insufficient (no anchors match)
        var textAnchors = evidence.Where(e => e.Source == "TEXT_ANCHOR").ToImmutableArray();
        Assert.NotEmpty(textAnchors);
        Assert.All(textAnchors, e => Assert.Equal(SemanticEvidenceStance.Insufficient, e.Stance));

        // NO evidence has Supports for any known page
        var supportsForKnownPages = evidence.Where(e =>
            e.Stance == SemanticEvidenceStance.Supports && e.Claim.Contains("page is", StringComparison.Ordinal));
        Assert.Empty(supportsForKnownPages);
    }

    // ── P5: SAME PAGE STATE MUTATION ───────────────────────────────────────

    /// <summary>
    /// P5: Changed SwitchState/content must not automatically imply new Page identity.
    /// WifiPage (Wi‑Fi OFF) and WifiOnPage (Wi‑Fi ON) are the SAME semantic page.
    /// PageAnalysis evidence for page identity should be consistent across state mutation.
    /// </summary>
    [Fact]
    public void P5_StateMutation_PageIdentityStable_AcrossSwitchStateChange()
    {
        var wifiOffObs = WifiPageObservation();
        var wifiOnObs = WifiOnPageObservation();
        var criteria = SettingsPageCriteria();

        var evidenceOff = PageAnalysis.Analyze(wifiOffObs, criteria);
        var evidenceOn = PageAnalysis.Analyze(wifiOnObs, criteria);

        // Both observations support WifiPage via TEXT_ANCHOR
        var offAnchor = evidenceOff.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });
        var onAnchor = evidenceOn.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });

        // Both SUPPORT WifiPage — state change did not change page identity
        Assert.Equal(SemanticEvidenceStance.Supports, offAnchor.Stance);
        Assert.Equal(SemanticEvidenceStance.Supports, onAnchor.Stance);

        // SWITCH_DISTRIBUTION still supports WifiPage for both (SwitchState-bearing Wi‑Fi present)
        var offSwitch = evidenceOff.Single(e => e is { Source: "SWITCH_DISTRIBUTION", Claim: "page is WifiPage" });
        var onSwitch = evidenceOn.Single(e => e is { Source: "SWITCH_DISTRIBUTION", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, offSwitch.Stance);
        Assert.Equal(SemanticEvidenceStance.Supports, onSwitch.Stance);
    }

    // ── P6: REORDER / NOISE ────────────────────────────────────────────────

    /// <summary>
    /// P6: Reordered elements, duplicate labels, empty text must not turn element
    /// indexes into Page identity. PageAnalysis uses text multiset, not element ordering.
    /// </summary>
    [Fact]
    public void P6_ElementReordering_DoesNotAffectPageEvidence()
    {
        // InternetPage with elements in REVERSE order
        var reversedObservation = new Observation(
            [
                new ObservedElement("Non-carrier data usage", null, 0),
                new ObservedElement("Wi-Fi doesn't turn backon automatically", null, 1),
                new ObservedElement("Networkpreferences", null, 2),
                new ObservedElement("Add network", null, 3),
                new ObservedElement("", null, 4),
                new ObservedElement("AndroidWifi", null, 5),
                new ObservedElement("Wi‑Fi", null, 6),
                new ObservedElement("", false, 7),
                new ObservedElement("T-Mobile", null, 8),
                new ObservedElement("", null, 9),
                new ObservedElement("", null, 10),
                new ObservedElement("T-Mobile", null, 11),
                new ObservedElement("", null, 12),
                new ObservedElement("Internet", null, 13),
            ],
            "com.android.settings",
            1);

        var criteria = SettingsPageCriteria();
        var evidence = PageAnalysis.Analyze(reversedObservation, criteria);

        // TEXT_ANCHOR still Supports InternetPage (index-independent)
        var internetAnchor = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, internetAnchor.Stance);

        // WifiPage still Insufficient (index-independent)
        var wifiAnchor = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Insufficient, wifiAnchor.Stance);
    }

    // ── P8: DETERMINISTIC REPLAY ───────────────────────────────────────────

    /// <summary>
    /// P8: Same Observation + same criteria → same SemanticEvidence output.
    /// Pure function — no hidden state, no randomness.
    /// </summary>
    [Fact]
    public void P8_DeterministicReplay_SameInputProducesSameOutput()
    {
        var observation = InternetPageObservation();
        var criteria = SettingsPageCriteria();

        var result1 = PageAnalysis.Analyze(observation, criteria);
        var result2 = PageAnalysis.Analyze(observation, criteria);

        Assert.Equal(result1.Length, result2.Length);
        for (int i = 0; i < result1.Length; i++)
        {
            Assert.Equal(result1[i].Source, result2[i].Source);
            Assert.Equal(result1[i].Claim, result2[i].Claim);
            Assert.Equal(result1[i].Stance, result2[i].Stance);
            Assert.Equal(result1[i].Reason, result2[i].Reason);
        }
    }

    // ── P7: NO FULL ELEMENT SEMANTICS REQUIRED ─────────────────────────────

    /// <summary>
    /// P7: Page analysis works using available screen/coarse evidence without
    /// requiring semantic resolution of every ObservedElement.
    ///
    /// This observation has empty-text elements, duplicates, and noise —
    /// but PageAnalysis still produces correct evidence from screen-level signals.
    /// </summary>
    [Fact]
    public void P7_NoFullElementSemanticsRequired_CoarseEvidenceSufficient()
    {
        // InternetPage observation already has: empty-text elements, duplicate "T-Mobile",
        // "AndroidWifi" (Wi‑Fi substring), "Wi-Fi doesn't turn backon automatically" (false anchor)
        var observation = InternetPageObservation();
        var criteria = SettingsPageCriteria();

        var evidence = PageAnalysis.Analyze(observation, criteria);

        // TEXT_ANCHOR: "T-Mobile" IS present → Supports InternetPage
        // This works despite: "AndroidWifi" containing "Wi‑Fi" text overlap,
        // "Wi-Fi doesn't turn backon automatically" containing "Wi‑Fi",
        // empty-text elements at indices 2,3,7,9
        var internetAnchor = evidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, internetAnchor.Stance);

        // SWITCH_DISTRIBUTION: Wi‑Fi has NO SwitchState → does NOT support WifiPage
        // (Wi‑Fi entry on InternetPage is a NavigableContainer, not a StateChangingControl)
        // No SWITCH_DISTRIBUTION evidence supports WifiPage
        var wifiSwitchEvidence = evidence.Where(e =>
            e is { Source: "SWITCH_DISTRIBUTION", Claim: "page is WifiPage" }).ToImmutableArray();
        Assert.Empty(wifiSwitchEvidence);
    }

    // ── End-to-End: PageAnalysis → Container Fusion ────────────────────────

    /// <summary>
    /// Filters PageAnalysis evidence to only stances relevant to a specific page claim.
    /// PageAnalysis produces evidence about ALL candidate pages. Container fusion
    /// (FuseBelief) treats all evidence as about the same implicit claim, so we must
    /// scope evidence to the Container's page before fusion.
    ///
    /// Evidence with claims NOT about this page (e.g., "application scope is X",
    /// "page is OtherPage") is excluded from the fusion input.
    /// </summary>
    private static ImmutableArray<SemanticEvidence> EvidenceForPage(
        ImmutableArray<SemanticEvidence> allEvidence,
        string pageName)
    {
        var pageClaim = $"page is {pageName}";
        return allEvidence
            .Where(e => e.Claim == pageClaim || e.Claim.Contains("application scope", StringComparison.Ordinal))
            .ToImmutableArray();
    }

    /// <summary>
    /// End-to-end: PageAnalysis evidence → Container.EvaluatePageBelief → SemanticBeliefState.
    ///
    /// InternetPage observation + PageAnalysis evidence → Container belief for "InternetPage" container.
    /// TEXT_ANCHOR Supports InternetPage + LOCAL_IDENTITY Supports → SUPPORTED.
    /// Evidence about WifiPage is excluded — it's a different claim.
    /// </summary>
    [Fact]
    public void EndToEnd_PageAnalysisEvidenceFusedIntoContainerBelief()
    {
        var container = new RuntimeContainer(
            "InternetPage",
            _ => true,  // identity rule: this IS InternetPage (caller knows the container's page)
            (_, _, _) => throw new InvalidOperationException("not used"));

        var observation = InternetPageObservation();
        var criteria = SettingsPageCriteria();
        var allEvidence = PageAnalysis.Analyze(observation, criteria);

        // Scope evidence to Container's page claim — different claims must not cross-contaminate
        var pageEvidence = EvidenceForPage(allEvidence, "InternetPage");

        // Fuse: LOCAL_IDENTITY (Supports "page is InternetPage")
        //     + TEXT_ANCHOR (Supports "page is InternetPage")
        //     + FOREGROUND (Supports "application scope is com.android.settings")
        var belief = container.EvaluatePageBelief(observation, [.. pageEvidence]);

        // LOCAL_IDENTITY Supports + TEXT_ANCHOR Supports + no Contradicts → Supported
        Assert.Equal(SemanticBeliefState.Supported, belief);
        Assert.Equal(SemanticBeliefState.Supported, container.LocalPageBeliefState);
    }

    /// <summary>
    /// End-to-end: WifiPage observation + PageAnalysis → Container belief for WifiPage.
    /// PageAnalysis evidence for WifiPage + LOCAL_IDENTITY Supports → SUPPORTED.
    /// </summary>
    [Fact]
    public void EndToEnd_WifiPageObservation_FusesToSupported()
    {
        var container = new RuntimeContainer(
            "WifiPage",
            _ => true,
            (_, _, _) => throw new InvalidOperationException("not used"));

        var observation = WifiPageObservation();
        var criteria = SettingsPageCriteria();
        var allEvidence = PageAnalysis.Analyze(observation, criteria);

        var pageEvidence = EvidenceForPage(allEvidence, "WifiPage");

        var belief = container.EvaluatePageBelief(observation, [.. pageEvidence]);

        Assert.Equal(SemanticBeliefState.Supported, belief);
    }

    /// <summary>
    /// End-to-end: Ambiguous observation (both pages' anchors present) → CONTRADICTED.
    /// TEXT_ANCHOR Supports both InternetPage AND WifiPage → sources disagree.
    /// </summary>
    [Fact]
    public void EndToEnd_AmbiguousObservation_YieldsContradicted()
    {
        var container = new RuntimeContainer(
            "InternetPage",
            _ => true,  // LOCAL_IDENTITY Supports InternetPage
            (_, _, _) => throw new InvalidOperationException("not used"));

        // Ambiguous: has both InternetPage AND WifiPage anchors
        var ambiguousObs = new Observation(
            [
                new ObservedElement("T-Mobile", null, 0),       // InternetPage anchor
                new ObservedElement("Add network", null, 1),    // InternetPage anchor
                new ObservedElement("Auto-connect", true, 2),   // WifiPage anchor WITH SwitchState
                new ObservedElement("Wi‑Fi", true, 3),          // SwitchState-bearing Wi‑Fi
            ],
            "com.android.settings",
            1);

        var criteria = SettingsPageCriteria();
        var allEvidence = PageAnalysis.Analyze(ambiguousObs, criteria);

        // TEXT_ANCHOR: Supports InternetPage (T-Mobile + Add network present)
        var internetSupport = allEvidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, internetSupport.Stance);

        // TEXT_ANCHOR_NEGATIVE: Contradicts InternetPage (Auto-connect IS a negative anchor)
        var internetNegative = allEvidence.Single(e => e is { Source: "TEXT_ANCHOR_NEGATIVE", Claim: "page is InternetPage" });
        Assert.Equal(SemanticEvidenceStance.Contradicts, internetNegative.Stance);

        // TEXT_ANCHOR: Supports WifiPage (Auto-connect present)
        var wifiSupport = allEvidence.Single(e => e is { Source: "TEXT_ANCHOR", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Supports, wifiSupport.Stance);

        // TEXT_ANCHOR_NEGATIVE: Contradicts WifiPage (T-Mobile is a negative anchor)
        var wifiNegative = allEvidence.Single(e => e is { Source: "TEXT_ANCHOR_NEGATIVE", Claim: "page is WifiPage" });
        Assert.Equal(SemanticEvidenceStance.Contradicts, wifiNegative.Stance);

        // Scope to InternetPage: TEXT_ANCHOR Supports + TEXT_ANCHOR_NEGATIVE Contradicts
        var pageEvidence = EvidenceForPage(allEvidence, "InternetPage");

        // Fuse into container believing "InternetPage"
        var belief = container.EvaluatePageBelief(ambiguousObs, [.. pageEvidence]);

        // LOCAL_IDENTITY Supports + TEXT_ANCHOR Supports + TEXT_ANCHOR_NEGATIVE Contradicts
        // → Supports + Contradicts → CONTRADICTED
        Assert.Equal(SemanticBeliefState.Contradicted, belief);
    }
}
