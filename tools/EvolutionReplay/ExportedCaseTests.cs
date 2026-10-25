using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.EvolutionReplay;

/// <summary>Explicit tool-only carrier. It never executes recorded commands.</summary>
public sealed class ExportedCaseTests
{
    [Fact]
    public void IndependentPopupLabel()
    {
        var manifestPath = Environment.GetEnvironmentVariable("UNICLAW_EVOLUTION_CASE")
            ?? throw new InvalidOperationException("Use evolution-case.py replay with a sealed case");
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var bytes = File.ReadAllBytes(manifestPath);
        Assert.Equal(File.ReadAllText(Path.Combine(directory, "case.sha256")).Trim(), Hash(bytes));
        using var document = JsonDocument.Parse(bytes);
        var manifest = document.RootElement;
        Assert.Equal("settings-popup.v1", manifest.GetProperty("adapter").GetString());
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var input in manifest.GetProperty("inputs").EnumerateArray())
        {
            var path = Path.GetFullPath(Path.Combine(directory, input.GetProperty("path").GetString()!));
            Assert.StartsWith(directory + Path.DirectorySeparatorChar, path);
            var content = File.ReadAllBytes(path);
            Assert.Equal(input.GetProperty("sha256").GetString(), Hash(content));
            inputs.Add(input.GetProperty("role").GetString()!, content);
        }
        using var oracle = JsonDocument.Parse(inputs["oracle"]);
        var expected = oracle.RootElement.GetProperty("cases").EnumerateArray().Single(c =>
            c.GetProperty("id").GetString() == manifest.GetProperty("expected").GetProperty("oracleCase").GetString());
        Assert.Equal(expected.GetProperty("popupAtTrigger").GetBoolean(), manifest.GetProperty("expected").GetProperty("popupPresent").GetBoolean());
        var actual = SettingsPopupClassifier.Classify(System.Text.Encoding.UTF8.GetString(inputs["capture-xml"]),
            manifest.GetProperty("context").GetProperty("hostPackage").GetString()!, PopupClearanceConfig.DefaultPopupResourceIds);
        Assert.NotNull(actual);
        // The curated label is a proposed independent test expectation, not window-stack truth.
        Assert.True(expected.GetProperty("popupAtTrigger").GetBoolean() == (actual == "present"),
            $"independent-popup-label-mismatch: expected={expected.GetProperty("popupAtTrigger").GetBoolean()}; observed={actual}; capture={manifest.GetProperty("source").GetProperty("captureId").GetString()}");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
