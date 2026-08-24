using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// Container Identity corpora for Vector Backend Evaluation. Contains
/// DeveloperOptions, WifiSettings, NetworkAndInternet, and SettingsRoot.
/// Scope remains Container Identity only.
/// </summary>
public static class ContainerIdentityCorpora
{
    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    /// <summary>DeveloperOptions corpus.</summary>
    public static SemanticCorpus DeveloperOptions() => DeveloperOptionsBenchmarkCorpus.Create();

    /// <summary>WifiSettings container identity corpus.</summary>
    public static SemanticCorpus WifiSettings() =>
        new(
            "WifiSettings-v1",
            ImmutableArray.Create(
                new SemanticCase(
                    "wifi-001",
                    Obs(1,
                        new ObservedElement("Wi-Fi", null, 0, null, "menu_item"),
                        new ObservedElement("Connected", null, 1, null, "text_block"),
                        new ObservedElement("AndroidWifi", null, 2, null, "text_block")),
                    "WifiSettings",
                    "WifiSettings",
                    SemanticCaseSource.RealWorld,
                    SemanticCaseDifficulty.Medium)
                {
                    PreviousVerifiedIdentity = "WifiSettings",
                },
                new SemanticCase(
                    "wifi-negative-001",
                    Obs(2, new ObservedElement("Data usage", null, 0, null, "menu_item")),
                    "None",
                    null,
                    SemanticCaseSource.Synthetic,
                    SemanticCaseDifficulty.Hard)
                {
                    PreviousVerifiedIdentity = "WifiSettings",
                }));

    /// <summary>NetworkAndInternet container identity corpus.</summary>
    public static SemanticCorpus NetworkAndInternet() =>
        new(
            "NetworkAndInternet-v1",
            ImmutableArray.Create(
                new SemanticCase(
                    "net-001",
                    Obs(1,
                        new ObservedElement("Network & internet", null, 0, null, "menu_item"),
                        new ObservedElement("Cellular", null, 1, null, "menu_item")),
                    "NetworkAndInternet",
                    "NetworkAndInternet",
                    SemanticCaseSource.RealWorld,
                    SemanticCaseDifficulty.Medium)
                {
                    PreviousVerifiedIdentity = "NetworkAndInternet",
                },
                new SemanticCase(
                    "net-negative-001",
                    Obs(2, new ObservedElement("Security", null, 0, null, "menu_item")),
                    "None",
                    null,
                    SemanticCaseSource.Synthetic,
                    SemanticCaseDifficulty.Hard)
                {
                    PreviousVerifiedIdentity = "NetworkAndInternet",
                }));

    /// <summary>SettingsRoot container identity corpus.</summary>
    public static SemanticCorpus SettingsRoot() =>
        new(
            "SettingsRoot-v1",
            ImmutableArray.Create(
                new SemanticCase(
                    "root-001",
                    Obs(1,
                        new ObservedElement("Settings", null, 0, null, "text_block"),
                        new ObservedElement("Network & internet", null, 1, null, "menu_item"),
                        new ObservedElement("Security", null, 2, null, "menu_item")),
                    "SettingsRoot",
                    "SettingsRoot",
                    SemanticCaseSource.RealWorld,
                    SemanticCaseDifficulty.Easy)
                {
                    PreviousVerifiedIdentity = "SettingsRoot",
                },
                new SemanticCase(
                    "root-negative-001",
                    Obs(2, new ObservedElement("Developer options", null, 0, null, "text_block")),
                    "None",
                    null,
                    SemanticCaseSource.Synthetic,
                    SemanticCaseDifficulty.Hard)
                {
                    PreviousVerifiedIdentity = "SettingsRoot",
                }));

    /// <summary>All Container Identity corpora.</summary>
    public static ImmutableArray<SemanticCorpus> All() =>
        ImmutableArray.Create(DeveloperOptions(), WifiSettings(), NetworkAndInternet(), SettingsRoot());
}