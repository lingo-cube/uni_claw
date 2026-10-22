using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>The Host parser validates immutable replay inputs without entering
/// the Simulation composition root. Regeneration is a separate explicit import.</summary>
public sealed class RecordedHierarchyImportTests
{
    [Theory, InlineData("network"), InlineData("stay-awake")]
    public void OriginalXml_ParsesToExactlyTheReviewedImmutableInputs(string id)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UniClaw.Kernel.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        string Read(string relative, string expectedHash)
        {
            var bytes = File.ReadAllBytes(Path.Combine(root.FullName, relative));
            Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "tests/UniClaw.Simulation.Tests/Fixtures/Replay/recorded-operations.json")));
        var corpus = manifest.RootElement;
        var input = corpus.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        foreach (var stage in new[] { "initial", "post", "unchanged" })
        {
            var source = input.GetProperty(stage == "post" ? "after" : "before");
            var xml = Read(source.GetProperty("path").GetString()!, source.GetProperty("sha256").GetString()!);
            var second = stage == "initial" ? 0 : 2; var capture = id + ":" + second;
            var actual = UiAutomatorDump.ParseHierarchyObservation(xml,
                new(capture, new DateTimeOffset(2026, 9, 12, 10, 12, 44, TimeSpan.Zero).AddSeconds(second),
                    "recorded-api35", "sim010", 35, ObservationCycleId: capture,
                    Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities,
                    Space: CoordinateSpace.DeviceViewport(corpus.GetProperty("width").GetInt32(),
                        corpus.GetProperty("height").GetInt32(), captureId: capture)));
            Assert.NotNull(actual.Observation);
            Assert.True(actual.Observation.IsValid);
            var imported = input.GetProperty("imported").GetProperty(stage);
            var frozen = Read(imported.GetProperty("path").GetString()!, imported.GetProperty("sha256").GetString()!);
            var ledger = new EvidenceLedger(); var strategy = new UiHierarchyOccurrenceStrategy();
            IReadOnlyList<ProposedOccurrence> candidates = Array.Empty<ProposedOccurrence>();
            var proposals = TypedHierarchyProposalProjector.Project(actual.Observation, ObservationContext.External);
            foreach (var proposal in proposals)
            {
                var (admission, record) = ledger.Admit(proposal);
                Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
                Assert.NotNull(record);
                candidates = strategy.Derive(record, null);
            }
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(frozen), JsonSerializer.SerializeToNode(
                new { observation = actual.Observation, candidates, triggerSubject = proposals[^1].Claim.Subject },
                new JsonSerializerOptions(JsonSerializerDefaults.Web))), id + ":" + stage + " differs from the reviewed import");
        }
    }
}
