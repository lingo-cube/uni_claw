using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-002 §23 — Migration Adapter 产物验证（不是 product 测试：验证 committed
/// manifest 与 legacy 源样本的对应关系；adapter 工具本身
/// tools/legacy-perception-import/import.cs 是 one-time migration 工具，
/// dies with migration——删除 adapter 时本测试类一并删除，corpus survives）。
/// </summary>
public sealed class LegacyImportAdapterTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();
    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");

    private static readonly string[] AdaptedIds =
    {
        "popup04-page", "scroll01-v1", "scroll01-v2", "popup09-before",
        "popup01-dialog", "nav03-parent", "nav03-childa",
    };

    private static XDocument LoadXml(string scenarioId) =>
        XDocument.Load(Path.Combine(CorpusRoot, "artifacts", $"{scenarioId}.xml"));

    [Fact]
    public void TextSemanticsPreserved_EveryXmlTextAppearsInObservations()
    {
        foreach (var id in AdaptedIds)
        {
            var xmlTexts = LoadXml(id).Descendants("node")
                .Select(n => (string?)n.Attribute("text") ?? "")
                .Where(t => t.Length > 0).ToHashSet();
            var observedValues = Corpus.Scenario(id).Observations.Select(o => o.Value).ToHashSet();
            Assert.Subset(observedValues, xmlTexts); // 所有 XML 文本都被保留为 observation value
        }
    }

    [Fact]
    public void CoordinateMeaningPreserved_PixelBoundsInArtifactFrame()
    {
        // 抽样核对：scenario_title / alertTitle 的 bounds 与 XML pixel bounds 一致
        var cases = new[] { "popup04-page", "scroll01-v1" };
        foreach (var id in cases)
        {
            var titleNode = LoadXml(id).Descendants("node")
                .First(n => ((string?)n.Attribute("resource-id") ?? "").EndsWith("scenario_title"));
            var bounds = ((string?)titleNode.Attribute("bounds"))!.Trim('[', ']').Split("][");
            var expected = $"{bounds[0]},{bounds[1]}";
            var observed = Corpus.Scenario(id).Observations
                .Single(o => o.Subject == "spatial.artifact.bounds.scenario_title").Value;
            Assert.StartsWith(expected, observed); // "l,t,r,b"（pixel，非 normalized）
        }

        // 全量：artifact frame 内的 pixel 坐标（1080×1920）
        foreach (var id in AdaptedIds)
        {
            foreach (var o in Corpus.Scenario(id).Observations.Where(o =>
                         o.Subject.StartsWith("spatial.artifact.bounds.", StringComparison.Ordinal)))
            {
                var parts = o.Value.Split(',').Select(int.Parse).ToArray();
                Assert.Equal(4, parts.Length);
                Assert.InRange(parts[0], 0, 1080); // x1
                Assert.InRange(parts[2], 0, 1080); // x2（artifact frame：1080 宽）
                Assert.InRange(parts[1], 0, 1920); // y1
                Assert.InRange(parts[3], 0, 1920); // y2
                Assert.True(parts[2] >= parts[0] && parts[3] >= parts[1], $"{id}:{o.Subject} bounds 退化");
            }
        }
    }

    [Fact]
    public void CaptureMetadataPreserved()
    {
        // frameDefaults 与真实 device profile（emulator-5554 / 1080×1920）一致
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CorpusRoot, "scenarios.json")));
        var defaults = doc.RootElement.GetProperty("frameDefaults");
        Assert.Equal(1080, defaults.GetProperty("width").GetInt32());
        Assert.Equal(1920, defaults.GetProperty("height").GetInt32());
        Assert.Equal("artifact", defaults.GetProperty("frame").GetString());

        foreach (var scenario in Corpus.Scenarios)
        {
            Assert.False(scenario.CaptureTime == default);          // capture time 显式
            Assert.True(File.Exists(Path.Combine(CorpusRoot, scenario.Artifact))); // artifact 在库
        }
    }

    [Fact]
    public void NoLegacyWorldTruthPromoted()
    {
        var forbidden = new[] { "currentpage", "containerid", "expectedidentity",
            "goalcompleted", "worldsnapshot", "runstate" };
        foreach (var scenario in Corpus.Scenarios)
        {
            foreach (var o in scenario.Observations)
            {
                Assert.DoesNotContain(forbidden,
                    f => o.Subject.Contains(f, StringComparison.OrdinalIgnoreCase)
                      || o.Value.Contains(f, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void DirectGoldenProviderOutputPreservedWithPixelBounds()
    {
        // DIRECT：真实 YOLO+OCR provider JSON 的语义与像素坐标进入 corpus
        var golden = Corpus.Scenario("golden-case-a-before");
        Assert.Equal("legacy-direct", golden.Provenance);
        Assert.Contains(golden.Observations, o => o.Value == "Wi-Fi");
        Assert.Contains(golden.Observations, o => o.Value == "AndroidWifi");
        Assert.Contains(golden.Observations, o => o.Subject.StartsWith("ui.detect.", StringComparison.Ordinal));

        // 与源 JSON 抽样核对（真实 provider 输出未在转换中丢失/变形）
        using var source = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(CorpusRoot, "legacy-direct", "golden-run-v1", "case-a-before.json")));
        var firstOcr = source.RootElement.GetProperty("ocr")[0];
        var px = firstOcr.GetProperty("boundsPx").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        Assert.Equal($"{px[0]},{px[1]},{px[2]},{px[3]}",
            golden.Observations.Single(o => o.Subject == "spatial.artifact.bounds.ocr0").Value);
        Assert.Equal(firstOcr.GetProperty("text").GetString(),
            golden.Observations.Single(o => o.Subject == "ui.text.ocr0").Value);
    }

    [Fact]
    public void SyntheticVariantsAreExplicitlyMarked()
    {
        // 诚实性：partial/degraded 是 synthetic-on-legacy-artifact，不是 legacy-real
        Assert.Equal("synthetic-on-legacy-artifact", Corpus.Scenario("scroll01-v1-partial").Provenance);
        Assert.Equal("synthetic-on-legacy-artifact", Corpus.Scenario("popup04-degraded").Provenance);
        Assert.NotNull(Corpus.Scenario("popup04-degraded").KnownFailure);
        Assert.NotNull(Corpus.Scenario("nav03-parent").KnownAmbiguity); // duplicate-title 歧义如实记录
    }
}
