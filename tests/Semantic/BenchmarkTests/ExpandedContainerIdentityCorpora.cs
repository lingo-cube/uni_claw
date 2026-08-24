using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// Expanded Container Identity corpora. Each identity contains:
/// A Normal, B Scroll, C Partial, D Ambiguous, E Failure Regression.
/// Categories support golden / regression / adversarial filtering.
/// </summary>
public static class ExpandedContainerIdentityCorpora
{
    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    private static SemanticCase Case(
        string caseId,
        long seq,
        string expectedCandidate,
        string? expectedIdentity,
        SemanticCaseSource source,
        SemanticCaseDifficulty difficulty,
        SemanticViewportState viewport,
        SemanticVisibleAnchorState anchor,
        int noise,
        int ambiguity,
        double scroll,
        string? previousVerifiedIdentity,
        params ObservedElement[] elements) =>
        new(
            caseId,
            Obs(seq, elements),
            expectedCandidate,
            expectedIdentity,
            source,
            difficulty)
        {
            PreviousVerifiedIdentity = previousVerifiedIdentity,
            ViewportState = viewport,
            VisibleAnchorState = anchor,
            NoiseLevel = noise,
            AmbiguityLevel = ambiguity,
            ScrollPosition = scroll,
        };

    private static SemanticCase Dev(string id, long seq, string candidate, string? identity,
        SemanticCaseSource source, SemanticCaseDifficulty difficulty,
        SemanticViewportState viewport, SemanticVisibleAnchorState anchor,
        int noise, int ambiguity, double scroll, string? previous, params ObservedElement[] elements) =>
        Case(id, seq, candidate, identity, source, difficulty, viewport, anchor, noise, ambiguity, scroll, previous, elements);

    private static SemanticCase Wifi(string id, long seq, string candidate, string? identity,
        SemanticCaseSource source, SemanticCaseDifficulty difficulty,
        SemanticViewportState viewport, SemanticVisibleAnchorState anchor,
        int noise, int ambiguity, double scroll, string? previous, params ObservedElement[] elements) =>
        Case(id, seq, candidate, identity, source, difficulty, viewport, anchor, noise, ambiguity, scroll, previous, elements);

    private static SemanticCase Network(string id, long seq, string candidate, string? identity,
        SemanticCaseSource source, SemanticCaseDifficulty difficulty,
        SemanticViewportState viewport, SemanticVisibleAnchorState anchor,
        int noise, int ambiguity, double scroll, string? previous, params ObservedElement[] elements) =>
        Case(id, seq, candidate, identity, source, difficulty, viewport, anchor, noise, ambiguity, scroll, previous, elements);

    private static SemanticCase Root(string id, long seq, string candidate, string? identity,
        SemanticCaseSource source, SemanticCaseDifficulty difficulty,
        SemanticViewportState viewport, SemanticVisibleAnchorState anchor,
        int noise, int ambiguity, double scroll, string? previous, params ObservedElement[] elements) =>
        Case(id, seq, candidate, identity, source, difficulty, viewport, anchor, noise, ambiguity, scroll, previous, elements);

    private static SemanticCorpus BuildGolden(string corpusId, ImmutableArray<SemanticCase> cases) =>
        new(corpusId, cases) { Category = SemanticCorpusCategory.Golden };

    /// <summary>DeveloperOptions golden corpus with A–E cases.</summary>
    public static SemanticCorpus DeveloperOptionsGolden() =>
        BuildGolden(
            "DeveloperOptions-golden-v1",
            ImmutableArray.Create(
                Dev("dev-golden-A", 1, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, SemanticVisibleAnchorState.AnchorVisible, 0, 0, 0, "DeveloperOptions",
                    new ObservedElement("Developer options", null, 0, null, "text"),
                    new ObservedElement("Enable demo mode", null, 1, null, "menu_item")),
                Dev("dev-golden-B", 2, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 0, 0, 0.8, "DeveloperOptions",
                    new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                    new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                    new ObservedElement("Automatic system updates", true, 2, null, "switch")),
                Dev("dev-golden-C", 3, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, SemanticVisibleAnchorState.AnchorMissing, 1, 0, 0.5, "DeveloperOptions",
                    new ObservedElement("Enable demo mode", null, 0, null, "menu_item")),
                Dev("dev-golden-D", 4, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, SemanticVisibleAnchorState.AnchorMissing, 1, 2, 0, "DeveloperOptions",
                    new ObservedElement("Data usage", null, 0, null, "menu_item"),
                    new ObservedElement("Mobile data", null, 1, null, "menu_item")),
                Dev("dev-golden-E", 5, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Regression, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 1, 1, 0.9, "DeveloperOptions",
                    new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                    new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                    new ObservedElement("Automatic system updates", true, 2, null, "switch"))));

    /// <summary>WifiSettings golden corpus with A–E cases.</summary>
    public static SemanticCorpus WifiSettingsGolden() =>
        BuildGolden(
            "WifiSettings-golden-v1",
            ImmutableArray.Create(
                Wifi("wifi-golden-A", 1, "WifiSettings", "WifiSettings", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, SemanticVisibleAnchorState.AnchorVisible, 0, 0, 0, "WifiSettings",
                    new ObservedElement("Wi-Fi", null, 0, null, "menu_item"),
                    new ObservedElement("AndroidWifi", null, 1, null, "text_block")),
                Wifi("wifi-golden-B", 2, "WifiSettings", "WifiSettings", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 0, 0, 0.8, "WifiSettings",
                    new ObservedElement("Connected", null, 0, null, "text_block"),
                    new ObservedElement("AndroidWifi", null, 1, null, "text_block")),
                Wifi("wifi-golden-C", 3, "WifiSettings", "WifiSettings", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, SemanticVisibleAnchorState.AnchorMissing, 1, 0, 0.5, "WifiSettings",
                    new ObservedElement("AndroidWifi", null, 0, null, "text_block")),
                Wifi("wifi-golden-D", 4, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, SemanticVisibleAnchorState.AnchorMissing, 1, 2, 0, "WifiSettings",
                    new ObservedElement("Data usage", null, 0, null, "menu_item")),
                Wifi("wifi-golden-E", 5, "WifiSettings", "WifiSettings", SemanticCaseSource.Regression, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 1, 1, 0.9, "WifiSettings",
                    new ObservedElement("Connected", null, 0, null, "text_block"),
                    new ObservedElement("AndroidWifi", null, 1, null, "text_block"))));

    /// <summary>NetworkAndInternet golden corpus with A–E cases.</summary>
    public static SemanticCorpus NetworkAndInternetGolden() =>
        BuildGolden(
            "NetworkAndInternet-golden-v1",
            ImmutableArray.Create(
                Network("net-golden-A", 1, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, SemanticVisibleAnchorState.AnchorVisible, 0, 0, 0, "NetworkAndInternet",
                    new ObservedElement("Network & internet", null, 0, null, "menu_item"),
                    new ObservedElement("Cellular", null, 1, null, "menu_item")),
                Network("net-golden-B", 2, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 0, 0, 0.8, "NetworkAndInternet",
                    new ObservedElement("Cellular", null, 0, null, "menu_item"),
                    new ObservedElement("SIM cards", null, 1, null, "menu_item")),
                Network("net-golden-C", 3, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, SemanticVisibleAnchorState.AnchorMissing, 1, 0, 0.5, "NetworkAndInternet",
                    new ObservedElement("Cellular", null, 0, null, "menu_item")),
                Network("net-golden-D", 4, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, SemanticVisibleAnchorState.AnchorMissing, 1, 2, 0, "NetworkAndInternet",
                    new ObservedElement("Security", null, 0, null, "menu_item")),
                Network("net-golden-E", 5, "NetworkAndInternet", "NetworkAndInternet", SemanticCaseSource.Regression, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 1, 1, 0.9, "NetworkAndInternet",
                    new ObservedElement("Cellular", null, 0, null, "menu_item"),
                    new ObservedElement("SIM cards", null, 1, null, "menu_item"))));

    /// <summary>SettingsRoot golden corpus with A–E cases.</summary>
    public static SemanticCorpus SettingsRootGolden() =>
        BuildGolden(
            "SettingsRoot-golden-v1",
            ImmutableArray.Create(
                Root("root-golden-A", 1, "SettingsRoot", "SettingsRoot", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Easy, SemanticViewportState.TitleVisible, SemanticVisibleAnchorState.AnchorVisible, 0, 0, 0, "SettingsRoot",
                    new ObservedElement("Settings", null, 0, null, "text_block"),
                    new ObservedElement("Network & internet", null, 1, null, "menu_item")),
                Root("root-golden-B", 2, "SettingsRoot", "SettingsRoot", SemanticCaseSource.RealTrace, SemanticCaseDifficulty.Medium, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 0, 0, 0.8, "SettingsRoot",
                    new ObservedElement("Network & internet", null, 0, null, "menu_item"),
                    new ObservedElement("Security", null, 1, null, "menu_item")),
                Root("root-golden-C", 3, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Manual, SemanticCaseDifficulty.Medium, SemanticViewportState.Partial, SemanticVisibleAnchorState.AnchorMissing, 1, 0, 0.5, "SettingsRoot",
                    new ObservedElement("Security", null, 0, null, "menu_item")),
                Root("root-golden-D", 4, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, SemanticVisibleAnchorState.AnchorMissing, 1, 2, 0, "SettingsRoot",
                    new ObservedElement("Developer options", null, 0, null, "text_block")),
                Root("root-golden-E", 5, "SettingsRoot", "SettingsRoot", SemanticCaseSource.Regression, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 1, 1, 0.9, "SettingsRoot",
                    new ObservedElement("Network & internet", null, 0, null, "menu_item"),
                    new ObservedElement("Security", null, 1, null, "menu_item"))));

    /// <summary>All golden corpora.</summary>
    public static ImmutableArray<SemanticCorpus> AllGolden() =>
        ImmutableArray.Create(
            DeveloperOptionsGolden(),
            WifiSettingsGolden(),
            NetworkAndInternetGolden(),
            SettingsRootGolden());

    /// <summary>Regression corpus containing historical failure scenarios.</summary>
    public static SemanticCorpus RegressionCorpus() =>
        new(
            "container-identity-regression-v1",
            ImmutableArray.Create(
                Case("reg-scrolled-drift", 1, "DeveloperOptions", "DeveloperOptions", SemanticCaseSource.Regression, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 0, 1, 0.9, "DeveloperOptions",
                    new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                    new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                    new ObservedElement("Automatic system updates", true, 2, null, "switch")),
                Case("reg-text-resolver-failure", 2, "WifiSettings", "WifiSettings", SemanticCaseSource.Regression, SemanticCaseDifficulty.Hard, SemanticViewportState.TitleOffscreen, SemanticVisibleAnchorState.AnchorMissing, 1, 1, 0.8, "WifiSettings",
                    new ObservedElement("Connected", null, 0, null, "text_block"),
                    new ObservedElement("AndroidWifi", null, 1, null, "text_block")),
                Case("reg-wrong-container-rejection", 3, "None", null, SemanticCaseSource.Regression, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, SemanticVisibleAnchorState.AnchorMissing, 1, 2, 0, "DeveloperOptions",
                    new ObservedElement("Data usage", null, 0, null, "menu_item"))))
        {
            Category = SemanticCorpusCategory.Regression,
        };

    /// <summary>Adversarial corpus containing false-recovery-prone samples.</summary>
    public static SemanticCorpus AdversarialCorpus() =>
        new(
            "container-identity-adversarial-v1",
            ImmutableArray.Create(
                Case("adv-similar-page", 1, "None", null, SemanticCaseSource.Synthetic, SemanticCaseDifficulty.Hard, SemanticViewportState.WrongPage, SemanticVisibleAnchorState.AnchorMissing, 2, 3, 0, "DeveloperOptions",
                    new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                    new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                    new ObservedElement("Security", null, 2, null, "menu_item"))))
        {
            Category = SemanticCorpusCategory.Adversarial,
        };
}