using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

/// <summary>
/// First benchmark corpus for Fast Semantic Container Identity retrieval.
/// Covers DeveloperOptions: title visible, title offscreen, partial elements,
/// wrong page, and similar-page interference.
/// </summary>
public static class DeveloperOptionsBenchmarkCorpus
{
    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    /// <summary>Creates the DeveloperOptions-v1 benchmark corpus.</summary>
    public static SemanticCorpus Create() =>
        new(
            "DeveloperOptions-v1",
            ImmutableArray.Create(
                // A: title visible
                new SemanticCase(
                    "dev-A-title-visible",
                    Obs(1,
                        new ObservedElement("Developer options", null, 0, null, "text"),
                        new ObservedElement("Enable demo mode", null, 1, null, "menu_item")),
                    "DeveloperOptions",
                    "DeveloperOptions",
                    SemanticCaseSource.RealWorld,
                    SemanticCaseDifficulty.Easy)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                },

                // B: title leaves viewport
                new SemanticCase(
                    "dev-B-title-offscreen",
                    Obs(2,
                        new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                        new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                        new ObservedElement("Automatic system updates", true, 2, null, "switch")),
                    "DeveloperOptions",
                    "DeveloperOptions",
                    SemanticCaseSource.RealWorld,
                    SemanticCaseDifficulty.Medium)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                },

                // C: partial elements missing
                new SemanticCase(
                    "dev-C-partial-elements",
                    Obs(3,
                        new ObservedElement("Enable demo mode", null, 0, null, "menu_item")),
                    "DeveloperOptions",
                    "DeveloperOptions",
                    SemanticCaseSource.Regression,
                    SemanticCaseDifficulty.Medium)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                },

                // D: wrong page
                new SemanticCase(
                    "dev-D-wrong-page",
                    Obs(4,
                        new ObservedElement("Data usage", null, 0, null, "menu_item"),
                        new ObservedElement("Mobile data", null, 1, null, "menu_item")),
                    "None",
                    null,
                    SemanticCaseSource.Synthetic,
                    SemanticCaseDifficulty.Hard)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                },

                // E: similar page interference (low-confidence false positive, not false recovery)
                new SemanticCase(
                    "dev-E-similar-page",
                    Obs(5,
                        new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                        new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                        new ObservedElement("Security", null, 2, null, "menu_item")),
                    "None",
                    null,
                    SemanticCaseSource.Synthetic,
                    SemanticCaseDifficulty.Hard)
                {
                    PreviousVerifiedIdentity = "Security",
                }));
}