using System.Text.Json;
using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RecordedPopupClassificationTests
{
    private static string Repo()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repo not found");
    }

    [Fact]
    public void KnownGap_RecordedNormalPageFalsePositiveIsReproduced_NotCertifiedAsCorrect()
    {
        using var corpus = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo(),
            "tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json")));
        var source = corpus.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "default-apps");
        var xml = File.ReadAllText(Path.Combine(Repo(), source.GetProperty("after").GetProperty("path").GetString()!));
        var actual = SettingsPopupClassifier.Classify(xml, "com.android.settings", PopupClearanceConfig.DefaultPopupResourceIds);
        // Owner explicitly deferred classifier semantics (SIM-017). This test
        // proves reproducibility of the known mismatch, never semantic correctness.
        Assert.Equal("present", actual);
        var expectedPopupPresent = false; // independent curated case label, not an original window-stack fact
        Assert.NotEqual(expectedPopupPresent, actual == "present");
    }

    [Fact]
    public void EveryHostScenarioTraitResolvesToAnExistingScenario()
    {
        var ids = Directory.EnumerateFiles(Path.Combine(Repo(), "scenarios"), "SCN-*.json")
            .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        var traits = typeof(RecordedPopupClassificationTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods()).SelectMany(m => m.GetCustomAttributesData())
            .Where(a => a.AttributeType == typeof(TraitAttribute) && a.ConstructorArguments.Count == 2
                && a.ConstructorArguments[0].Value as string == "Scenario")
            .Select(a => a.ConstructorArguments[1].Value as string).ToArray();
        Assert.NotEmpty(traits);
        Assert.All(traits, id => Assert.Contains(id, ids));
    }
}
