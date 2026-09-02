using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Versioned evidence-sufficiency profile (profile-bound configuration).
///
/// V1 (EVIDENCE_SUFFICIENCY_PROFILE_V1): the hardened evidence model from
/// SEMANTIC_SAFETY_HARDENING_APPLY. Generic tokens and exclusive per-identity
/// anchors encode CONTAINER IDENTITY semantic knowledge — derived from the
/// tuning corpora and captured real-trace Settings-app vocabulary
/// (docs/experiments/semantic-perception-safety-analysis.md §4), describing
/// identities, not failure samples. No case-id special case exists here.
/// </summary>
public static class EvidenceSufficiencyProfiles
{
    /// <summary>Tuning + real-trace Settings-app identity anchors (exclusive per identity).</summary>
    public static readonly IReadOnlyDictionary<string, ImmutableHashSet<string>> V1Anchors =
        new Dictionary<string, ImmutableHashSet<string>>(StringComparer.Ordinal)
        {
            ["DeveloperOptions"] = ImmutableHashSet.Create(StringComparer.Ordinal,
                "developer options", "enable demo mode", "show demo mode", "automatic system updates",
                "don't keep activities", "background process limit", "bug report shortcut",
                "system ui demo mode"),
            ["WifiSettings"] = ImmutableHashSet.Create(StringComparer.Ordinal,
                "wi-fi", "androidwifi", "connected", "use wi-fi", "saved networks",
                "add network", "wi-fi preferences", "signal strength"),
            ["NetworkAndInternet"] = ImmutableHashSet.Create(StringComparer.Ordinal,
                "cellular", "sim cards", "airplane mode", "mobile data", "data usage",
                "hotspot & tethering", "emergency alerts", "vpn"),
            ["SettingsRoot"] = ImmutableHashSet.Create(StringComparer.Ordinal,
                "settings", "search settings", "connected devices", "bluetooth, pairing", "apps",
                "recent apps, default apps", "notifications", "notification history, conversations",
                "battery", "storage", "sound & vibration", "volume, vibration, do not disturb",
                "display", "dark theme, font size, brightness", "wallpaper", "home, lock screen",
                "accessibility", "security & privacy", "privacy", "about phone", "reset options",
                "100%"),
        };

    /// <summary>Generic UI-similarity tokens (settings-family words shared across containers).</summary>
    public static readonly ImmutableHashSet<string> V1GenericTokens = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "text", "toggle", "button", "option", "options", "menu", "settings", "system",
        "network", "status", "general", "advanced", "device", "about", "search");

    /// <summary>
    /// The hardened V1 evidence-sufficiency profile
    /// (identity: EVIDENCE_SUFFICIENCY_PROFILE_V1).
    /// </summary>
    public static EvidenceSufficiencyOptions V1 { get; } = new()
    {
        Enabled = true,
        MinEvidenceScore = 2,
        MinNonGenericText = 1,
        MinDiscriminativeSignal = 1,
        RequireTextEvidence = true,
        GenericTokens = V1GenericTokens,
        PerIdentityAnchors = V1Anchors,
    };
}