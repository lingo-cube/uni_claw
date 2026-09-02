using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// ContainerIdentity-heldout-v4 — TRUE held-out qualification corpus for
/// SEMANTIC_CONTAINER_IDENTITY_PROFILE_V4.
///
/// SCOPE (the V4 exam): does the semantic-concept-oriented representation
/// generalize to surface forms that never participated in development?
///   - LEXICALLY_NOVEL_POSITIVE (-LN): real Settings-app rows whose *surfaces*
///     appear in NO prototype, NO terminology profile, NO development corpus
///     (e.g. "Window animation scale", "Wi‑Fi charging", "Preferred network
///     type", "Quick tap") — but whose SEMANTIC family (developer-debugging /
///     wireless-network / mobile-network / device-category) is known. This
///     direkt tests concept generalization vs dictionary memorization.
///   - CONCEPT_COLLISION_NEGATIVE (-CC): pages whose rows map to concepts that
///     legitimately span several identities (wireless/mobile/device/system);
///     the pipeline must still rely on full evidence + ranking + margin +
///     policy, never single-concept → identity.
/// 96 cases = 4 identities × 14 positives (56) + 40 negatives.
/// No case is copied from tuning / former-heldout-v1/v2/v3 (isolation Q1).
/// Sources honest: RealTrace = verbatim contiguous subsets of the captured
/// root-scrolled frame (truth.json); Manual = fresh compositions; Synthetic =
/// independent adversarial. Limited real-trace availability is recorded, not
/// faked.
/// </summary>
public static class HeldOutContainerIdentityCorpusV4
{
    public const string CorpusId = "ContainerIdentity-heldout-v4";

    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    private static ObservedElement El(string text, int index, string? type = null, bool? switchState = null) =>
        new(text, switchState, index, null, type);

    private static SemanticCase Case(
        string caseId, long seq, string expectedCandidate, string? expectedIdentity,
        SemanticCaseSource source, SemanticCaseDifficulty difficulty,
        SemanticViewportState viewport, int ambiguity, double scroll,
        string? previousVerifiedIdentity, params ObservedElement[] elements) =>
        new(caseId, Obs(seq, elements), expectedCandidate, expectedIdentity, source, difficulty)
        {
            PreviousVerifiedIdentity = previousVerifiedIdentity,
            ViewportState = viewport,
            VisibleAnchorState = viewport == SemanticViewportState.TitleVisible
                ? SemanticVisibleAnchorState.AnchorVisible
                : SemanticVisibleAnchorState.AnchorMissing,
            NoiseLevel = difficulty == SemanticCaseDifficulty.Hard ? 1 : 0,
            AmbiguityLevel = ambiguity,
            ScrollPosition = scroll,
        };

    /// <summary>Lexically-novel positive case ids and concept-collision negative case ids (manifest).</summary>
    public static readonly ImmutableHashSet<string> LexicallyNovelPositiveIds = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ho4-dev-P10", "ho4-dev-P11", "ho4-wifi-P10", "ho4-wifi-P11",
        "ho4-net-P10", "ho4-net-P11", "ho4-root-P10", "ho4-root-P11");

    public static readonly ImmutableHashSet<string> ConceptCollisionNegativeIds = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ho4-dev-N6", "ho4-dev-N7", "ho4-wifi-N6", "ho4-wifi-N7",
        "ho4-net-N6", "ho4-net-N7", "ho4-root-N6", "ho4-root-N7");

    public static SemanticCorpus Create() => Build();

    private static SemanticCorpus Build()
    {
        var cases = ImmutableArray.Create(
            // ── DeveloperOptions ×14 positives ────────────────────────────────
            Case("ho4-dev-P1", 1, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "DeveloperOptions",
                El("Developer options", 0, "text"), El("Window animation scale", 1, "menu_item"), El("Transition animation scale", 2, "menu_item")),
            Case("ho4-dev-P2", 2, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "DeveloperOptions",
                El("Developer options", 0, "text"), El("Debug GPU overdraw", 1, "menu_item"), El("Show surface updates", 2, "switch", false)),
            Case("ho4-dev-P3", 3, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.5, "DeveloperOptions",
                El("Animator duration scale", 0, "menu_item"), El("Transition animation scale", 1, "menu_item"), El("Wireless debugging", 2, "switch", true)),
            Case("ho4-dev-P4", 4, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.9, "DeveloperOptions",
                El("Show surface updates", 0, "switch", true), El("Debug GPU overdraw", 1, "menu_item"), El("Memory usage", 2, "menu_item")),
            Case("ho4-dev-P5", 5, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 0, 0.4, "DeveloperOptions",
                El("Developer options", 0, "text"), El("Animator duration scale", 1, "menu_item")),
            Case("ho4-dev-P6", 6, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 0, 0.3, "DeveloperOptions",
                El("Show surface updates", 0, "switch", false)),
            Case("ho4-dev-P7", 7, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "DeveloperOptions",
                El("Developer options", 0, "text"),
                El("USB debugging", 1, "switch", true)),
            Case("ho4-dev-P8", 8, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "DeveloperOptions",
                El("Debugging panel", 0, "text"), El("Animation scales", 1, "menu_item")),
            Case("ho4-dev-P9", 9, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 0, 0, "DeveloperOptions",
                El("Developer options", 0, "text"), El("Window animation scale", 1, "menu_item"), El("USB debugging", 2, "menu_item"), El("Show surface updates", 3, "switch", true)),
            Case("ho4-dev-P10", 10, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "DeveloperOptions",
                El("Developer options", 0, "text"), El("Force activities to be resizable", 1, "menu_item")),
            Case("ho4-dev-P11", 11, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 1, 0.6, "DeveloperOptions",
                El("Window animation scale", 0, "menu_item"), El("Animator duration scale", 1, "menu_item"), El("Show taps", 2, "switch", false)),
            Case("ho4-dev-P12", 12, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 0, 0, "DeveloperOptions",
                El("Developer options", 0, "text"), El("Force activities to be resizable", 1, "switch", true), El("OEM unlocking", 2, "menu_item"), El("Memory usage", 3, "menu_item")),
            Case("ho4-dev-P13", 13, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.8, "DeveloperOptions",
                El("Transition animation scale", 0, "menu_item"), El("Wireless debugging", 1, "switch", true), El("USB debugging", 2, "menu_item")),
            Case("ho4-dev-P14", 14, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "DeveloperOptions",
                El("Developer options", 0, "text"), El("Window animation scale", 1, "menu_item"), El("Animator duration scale", 2, "menu_item"), El("Background process limit", 3, "menu_item")),
            // ── WifiSettings ×14 positives ───────────────────────────────────
            Case("ho4-wifi-P1", 15, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "WifiSettings",
                El("Wi‑Fi", 0, "menu_item"), El("GardenApartment5G", 1, "menu_item"), El("Connected", 2, "text_block"), El("AndroidWifi", 3, "text_block")),
            Case("ho4-wifi-P2", 16, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "WifiSettings",
                El("Wi‑Fi", 0, "text"), El("StudioWLAN", 1, "menu_item"), El("Use Wi‑Fi", 2, "switch", true), El("Connected", 3, "text_block")),
            Case("ho4-wifi-P3", 17, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.55, "WifiSettings",
                El("AndroidWifi", 0, "text_block"), El("GardenApartment5G", 1, "menu_item"), El("Connected", 2, "text_block")),
            Case("ho4-wifi-P4", 18, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.95, "WifiSettings",
                El("Wi‑Fi Direct", 0, "switch", true), El("MAC address", 1, "text_block"), El("Advanced settings", 2, "menu_item")),
            Case("ho4-wifi-P5", 19, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 0, 0.35, "WifiSettings",
                El("Connected", 0, "text_block"), El("Signal strength", 1, "text_block")),
            Case("ho4-wifi-P6", 20, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 1, 0.3, "WifiSettings",
                El("AndroidWifi", 0, "text_block"), El("Wi‑Fi Direct", 1, "switch", true)),
            Case("ho4-wifi-P7", 21, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "WifiSettings",
                El("WLAN", 0, "text"), El("Network list", 1, "text_block"), El("Connected", 2, "text_block")),
            Case("ho4-wifi-P8", 22, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 0, 0, "WifiSettings",
                El("Wi‑Fi", 0, "menu_item"), El("StudioWLAN", 1, "menu_item"), El("Signal strength", 2, "text_block"), El("Use Wi‑Fi", 3, "switch", true)),
            Case("ho4-wifi-P9", 23, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.7, "WifiSettings",
                El("Wi‑Fi Direct", 0, "switch", true), El("Install certificates", 1, "menu_item"), El("Signal strength", 2, "text_block")),
            Case("ho4-wifi-P10", 24, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 1, 0.5, "WifiSettings",
                El("Scanning always available", 0, "switch", false), El("AndroidWifi", 1, "text_block")),
            Case("ho4-wifi-P11", 25, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "WifiSettings",
                El("Wi‑Fi charging", 0, "switch", true), El("MAC randomization", 1, "menu_item"), El("Connected", 2, "text_block")),
            Case("ho4-wifi-P12", 26, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "WifiSettings",
                El("Wi‑Fi", 0, "menu_item"), El("GardenApartment5G", 1, "menu_item"), El("Use Wi‑Fi", 2, "switch", true), El("AndroidWifi", 3, "text_block")),
            Case("ho4-wifi-P13", 27, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.85, "WifiSettings",
                El("Saved networks", 0, "menu_item"), El("StudioWLAN", 1, "menu_item"), El("MAC address", 2, "text_block")),
            Case("ho4-wifi-P14", 28, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "WifiSettings",
                El("Wi‑Fi", 0, "text"), El("Connected", 1, "text_block"), El("AndroidWifi", 2, "text_block"), El("Wi‑Fi charging", 3, "switch", false)),
            // ── NetworkAndInternet ×14 positives ──────────────────────────────
            Case("ho4-net-P1", 29, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "NetworkAndInternet",
                El("Network & internet", 0, "text"), El("Preferred network type", 1, "menu_item"), El("Cellular", 2, "menu_item")),
            Case("ho4-net-P2", 30, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "NetworkAndInternet",
                El("Network & internet", 0, "text"), El("SIM card lock", 1, "menu_item"), El("Mobile data", 2, "switch", true)),
            Case("ho4-net-P3", 31, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.5, "NetworkAndInternet",
                El("SIM cards", 0, "menu_item"), El("Carrier", 1, "text_block"), El("Mobile data", 2, "menu_item")),
            Case("ho4-net-P4", 32, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.95, "NetworkAndInternet",
                El("Preferred network type", 0, "menu_item"), El("Network unlock", 1, "menu_item"), El("Roaming", 2, "menu_item")),
            Case("ho4-net-P5", 33, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 0, 0.4, "NetworkAndInternet",
                El("SIM cards", 0, "menu_item"), El("Carrier", 1, "text_block")),
            Case("ho4-net-P6", 34, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 1, 0.5, "NetworkAndInternet",
                El("SIM card lock", 0, "menu_item")),
            Case("ho4-net-P7", 35, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "NetworkAndInternet",
                El("Internet", 0, "text"), El("Network unlock", 1, "menu_item"), El("Access Point Names", 2, "menu_item")),
            Case("ho4-net-P8", 36, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 0, 0, "NetworkAndInternet",
                El("Network & internet", 0, "text"), El("VPN", 1, "menu_item"), El("Preferred network type", 2, "menu_item"), El("Airplane mode", 3, "switch", true)),
            Case("ho4-net-P9", 37, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.7, "NetworkAndInternet",
                El("Carrier", 0, "text_block"), El("SIM status", 1, "text_block"), El("Calling", 2, "menu_item")),
            Case("ho4-net-P10", 38, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "NetworkAndInternet",
                El("Network & internet", 0, "text"), El("Data cap", 1, "menu_item")),
            Case("ho4-net-P11", 39, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 1, 0.8, "NetworkAndInternet",
                El("Preferred network type", 0, "menu_item"), El("SIM cards", 1, "menu_item"), El("Billing cycle", 2, "text_block")),
            Case("ho4-net-P12", 40, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "NetworkAndInternet",
                El("Network & internet", 0, "text"), El("Cellular", 1, "menu_item"), El("SIM card lock", 2, "menu_item"), El("Mobile data", 3, "switch", true)),
            Case("ho4-net-P13", 41, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.6, "NetworkAndInternet",
                El("Access Point Names", 0, "menu_item"), El("Proxy", 1, "menu_item"), El("Network unlock", 2, "menu_item")),
            Case("ho4-net-P14", 42, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "NetworkAndInternet",
                El("Internet", 0, "text"), El("SIM status", 1, "text_block"), El("Carrier", 2, "text_block"), El("Airplane mode", 3, "switch", false)),
            // ── SettingsRoot ×14 positives ───────────────────────────────────
            Case("ho4-root-P1", 43, "SettingsRoot", "SettingsRoot", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "SettingsRoot",
                El("Settings", 0, "text"), El("Search settings", 1, "text_block"), El("Accessibility", 2, "menu_item")),
            Case("ho4-root-P2", 44, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "SettingsRoot",
                El("Settings", 0, "text"), El("Quick tap", 1, "menu_item"), El("Now Playing", 2, "menu_item")),
            Case("ho4-root-P3", 45, "SettingsRoot", "SettingsRoot", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.65, "SettingsRoot",
                El("Search settings", 0, "text_block"), El("Accessibility", 1, "menu_item"), El("Security & privacy", 2, "menu_item"), El("System", 3, "menu_item")),
            Case("ho4-root-P4", 46, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.95, "SettingsRoot",
                El("System update", 0, "menu_item"), El("Screen timeout", 1, "menu_item"), El("Adaptive charging", 2, "switch", true)),
            Case("ho4-root-P5", 47, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 0, 0.4, "SettingsRoot",
                El("Settings", 0, "text"), El("Quick tap", 1, "menu_item")),
            Case("ho4-root-P6", 48, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, 1, 0.5, "SettingsRoot",
                El("Search settings", 0, "text_block"), El("Now Playing", 1, "menu_item")),
            Case("ho4-root-P7", 49, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "SettingsRoot",
                El("Main settings", 0, "text"), El("Screen timeout", 1, "menu_item")),
            Case("ho4-root-P8", 50, "SettingsRoot", "SettingsRoot", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.75, "SettingsRoot",
                El("Accessibility", 0, "menu_item"), El("Security & privacy", 1, "menu_item"), El("System", 2, "menu_item"), El("About phone", 3, "menu_item")),
            Case("ho4-root-P9", 51, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 0, 0, "SettingsRoot",
                El("Settings", 0, "text"), El("Quick tap", 1, "menu_item"), El("Now Playing", 2, "menu_item"), El("System navigation", 3, "menu_item")),
            Case("ho4-root-P10", 52, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleVisible, 1, 0, "SettingsRoot",
                El("Settings", 0, "text"), El("Adaptive charging", 1, "switch", true)),
            Case("ho4-root-P11", 53, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 1, 0.5, "SettingsRoot",
                El("Quick tap", 0, "menu_item"), El("Now Playing", 1, "menu_item"), El("Search settings", 2, "text_block")),
            Case("ho4-root-P12", 54, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "SettingsRoot",
                El("Settings", 0, "text"), El("Quick tap", 1, "menu_item"), El("System update", 2, "menu_item"), El("Storage", 3, "menu_item")),
            Case("ho4-root-P13", 55, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, 0, 0.85, "SettingsRoot",
                El("Screen timeout", 0, "menu_item"), El("Adaptive charging", 1, "switch", true), El("System navigation", 2, "menu_item")),
            Case("ho4-root-P14", 56, "SettingsRoot", "SettingsRoot", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, 0, 0, "SettingsRoot",
                El("Settings", 0, "text"), El("Search settings", 1, "text_block"), El("Security & privacy", 2, "menu_item")),
            // ── negatives / hard negatives (40 = 10 per identity) ─────────────
            Case("ho4-dev-N1", 57, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "DeveloperOptions",
                El("Lock screen", 0, "menu_item"), El("Face unlock", 1, "menu_item"), El("System navigation", 2, "menu_item")),
            Case("ho4-dev-N2", 58, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 3, 0, "DeveloperOptions",
                El("Developer", 0, "text"), El("Setup", 1, "text"), El("Menu", 2, "menu_item"), El("Options", 3, "menu_item")),
            Case("ho4-dev-N3", 59, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.Unknown, 3, 0, "DeveloperOptions",
                El("", 0, "toggle"), El("", 1, "text_block")),
            Case("ho4-dev-N4", 60, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "DeveloperOptions",
                El("Quick tap", 0, "menu_item"), El("Now Playing", 1, "menu_item"), El("Screen timeout", 2, "menu_item")),
            Case("ho4-dev-N5", 61, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "DeveloperOptions",
                El("Window scale", 0, "text_block"), El("Transition preview", 1, "text_block"), El("Status", 2, "text_block")),
            Case("ho4-dev-N6", 62, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "DeveloperOptions",
                El("Wi‑Fi", 0, "menu_item"), El("Preferred network type", 1, "menu_item"), El("SIM card lock", 2, "menu_item")),
            Case("ho4-dev-N7", 63, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "DeveloperOptions",
                El("Ethernet", 0, "menu_item"), El("Data cap", 1, "menu_item"), El("Billing cycle", 2, "text_block")),
            Case("ho4-dev-N8", 64, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "DeveloperOptions",
                El("Settings", 0, "text"), El("Menu", 1, "menu_item"), El("List", 2, "text_block"), El("Options", 3, "menu_item")),
            Case("ho4-dev-N9", 65, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleVisible, 1, 0, "WifiSettings",
                El("Developer options", 0, "text"), El("Window animation scale", 1, "menu_item")),
            Case("ho4-dev-N10", 66, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "DeveloperOptions",
                El("Sound & vibration", 0, "menu_item"), El("Do Not Disturb", 1, "menu_item"), El("Volume, vibration", 2, "text_block")),
            Case("ho4-wifi-N1", 67, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "WifiSettings",
                El("SIM cards", 0, "menu_item"), El("Carrier", 1, "text_block"), El("Preferred network type", 2, "menu_item")),
            Case("ho4-wifi-N2", 68, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 3, 0, "WifiSettings",
                El("Wireless settings", 0, "text"), El("Setup wizard", 1, "menu_item"), El("Options", 2, "menu_item")),
            Case("ho4-wifi-N3", 69, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.Unknown, 3, 0, "WifiSettings",
                El("", 0, "toggle"), El("", 1, "text_block")),
            Case("ho4-wifi-N4", 70, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "WifiSettings",
                El("Quick tap", 0, "menu_item"), El("Adaptive charging", 1, "switch", true), El("Screen timeout", 2, "menu_item")),
            Case("ho4-wifi-N5", 71, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "WifiSettings",
                El("Signal", 0, "text_block"), El("Bandwidth", 1, "text_block"), El("Status", 2, "text_block")),
            Case("ho4-wifi-N6", 72, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "WifiSettings",
                El("Reading", 0, "menu_item"), El("Deep sleep", 1, "menu_item"), El("Background data", 2, "menu_item")),
            Case("ho4-wifi-N7", 73, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "WifiSettings",
                El("Ethernet", 0, "menu_item"), El("Preferred network type", 1, "menu_item"), El("IP settings", 2, "menu_item")),
            Case("ho4-wifi-N8", 74, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "WifiSettings",
                El("Menu", 0, "text"), El("Settings", 1, "text"), El("Advanced", 2, "menu_item"), El("Options", 3, "menu_item")),
            Case("ho4-wifi-N9", 75, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleVisible, 1, 0, "DeveloperOptions",
                El("Wi‑Fi", 0, "text"), El("Connected", 1, "text_block"), El("AndroidWifi", 2, "text_block")),
            Case("ho4-wifi-N10", 76, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "WifiSettings",
                El("Bluetooth", 0, "menu_item"), El("Pairing status", 1, "text_block"), El("Connected devices", 2, "menu_item")),
            Case("ho4-net-N1", 77, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "NetworkAndInternet",
                El("Wallpaper", 0, "menu_item"), El("Quick tap", 1, "menu_item"), El("Now Playing", 2, "menu_item")),
            Case("ho4-net-N2", 78, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 3, 0, "NetworkAndInternet",
                El("Network", 0, "text"), El("Connectivity", 1, "text_block"), El("Settings", 2, "text")),
            Case("ho4-net-N3", 79, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.Unknown, 3, 0, "NetworkAndInternet",
                El("", 0, "text_block"), El("", 1, "text_block")),
            Case("ho4-net-N4", 80, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "NetworkAndInternet",
                El("Saved networks", 0, "menu_item"), El("StudioWLAN", 1, "menu_item"), El("Connected", 2, "text_block")),
            Case("ho4-net-N5", 81, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "NetworkAndInternet",
                El("Mobile", 0, "text"), El("Mobile hotspot", 1, "menu_item"), El("Tethering", 2, "text_block")),
            Case("ho4-net-N6", 82, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "NetworkAndInternet",
                El("Smart home", 0, "menu_item"), El("Speaker", 1, "menu_item"), El("Chromecast", 2, "menu_item")),
            Case("ho4-net-N7", 83, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "NetworkAndInternet",
                El("Developer options", 0, "text"), El("Window animation scale", 1, "menu_item"), El("Animator duration scale", 2, "menu_item")),
            Case("ho4-net-N8", 84, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "NetworkAndInternet",
                El("Settings", 0, "text"), El("Menu", 1, "menu_item"), El("Overview", 2, "text_block")),
            Case("ho4-net-N9", 85, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleVisible, 1, 0, "SettingsRoot",
                El("SIM cards", 0, "menu_item"), El("Preferred network type", 1, "menu_item"), El("Carrier", 2, "text_block")),
            Case("ho4-net-N10", 86, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "NetworkAndInternet",
                El("Battery", 0, "menu_item"), El("Adaptive charging", 1, "switch", true), El("Screen timeout", 2, "menu_item")),
            Case("ho4-root-N1", 87, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "SettingsRoot",
                El("SIM cards", 0, "menu_item"), El("SIM card lock", 1, "menu_item"), El("Carrier", 2, "text_block")),
            Case("ho4-root-N2", 88, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 3, 0, "SettingsRoot",
                El("Settings", 0, "text"), El("Overview", 1, "menu_item"), El("Everything", 2, "text_block")),
            Case("ho4-root-N3", 89, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.Unknown, 3, 0, "SettingsRoot",
                El("", 0, "menu_item"), El("", 1, "toggle")),
            Case("ho4-root-N4", 90, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "SettingsRoot",
                El("Window animation scale", 0, "menu_item"), El("Show surface updates", 1, "switch", true), El("Debug GPU overdraw", 2, "menu_item")),
            Case("ho4-root-N5", 91, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "SettingsRoot",
                El("Overview", 0, "text_block"), El("Categories", 1, "menu_item"), El("Feature list", 2, "text_block")),
            Case("ho4-root-N6", 92, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "SettingsRoot",
                El("Wi‑Fi", 0, "menu_item"), El("Connected", 1, "text_block"), El("Preferred network type", 2, "menu_item")),
            Case("ho4-root-N7", 93, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "SettingsRoot",
                El("SIM card lock", 0, "menu_item"), El("Network unlock", 1, "menu_item"), El("Data cap", 2, "menu_item")),
            Case("ho4-root-N8", 94, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "SettingsRoot",
                El("Item", 0, "text"), El("Element", 1, "text_block"), El("Menu item", 2, "menu_item")),
            Case("ho4-root-N9", 95, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleVisible, 1, 0, "NetworkAndInternet",
                El("Settings", 0, "text"), El("Quick tap", 1, "menu_item"), El("System update", 2, "menu_item")),
            Case("ho4-root-N10", 96, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, 2, 0, "SettingsRoot",
                El("Carrier", 0, "text_block"), El("Mobile data", 1, "menu_item"), El("Airplane mode", 2, "switch", true)));

        return new SemanticCorpus(CorpusId, cases)
        {
            Category = SemanticCorpusCategory.Experimental,
        };
    }
}