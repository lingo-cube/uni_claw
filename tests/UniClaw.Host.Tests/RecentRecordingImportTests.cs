using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RecentRecordingImportTests
{
    [Theory, InlineData("sims"), InlineData("all-apps"), InlineData("default-apps")]
    public void RecentOriginalXml_PublicHostImportMatchesFrozenReplayInput(string id)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UniClaw.Kernel.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json")));
        var corpus = doc.RootElement;
        var c = corpus.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        string Read(JsonElement asset)
        {
            var bytes = File.ReadAllBytes(Path.Combine(root.FullName, asset.GetProperty("path").GetString()!));
            Assert.Equal(asset.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        using var recording = JsonDocument.Parse(Read(c.GetProperty("recording")));
        var runTime = recording.RootElement.GetProperty("frozenAtUtc").GetDateTimeOffset();
        Assert.Equal(c.GetProperty("recordedAt").GetDateTimeOffset(), runTime);
        var window = corpus.GetProperty("window");
        Assert.InRange(runTime, window.GetProperty("from").GetDateTimeOffset(),
            window.GetProperty("untilExclusive").GetDateTimeOffset().AddTicks(-1));
        using var sequence = JsonDocument.Parse(Read(c.GetProperty("sequence")));
        using var trace = JsonDocument.Parse(Read(c.GetProperty("settingsTrace")));
        using var facts = JsonDocument.Parse(Read(c.GetProperty("facts")));
        var steps = sequence.RootElement.EnumerateArray().ToArray();
        var captures = trace.RootElement.EnumerateArray().ToArray();
        Assert.Equal(c.GetProperty("action").GetProperty("descriptor").GetString(), steps[^1].GetProperty("targetDescriptor").GetString());
        Assert.True(steps[^1].GetProperty("verified").GetBoolean());
        Assert.False(facts.RootElement.GetProperty("terminal").GetBoolean());
        foreach (var stage in new[] { "before", "after" })
        {
            var source = c.GetProperty(stage); var capture = source.GetProperty("captureId").GetString()!;
            var offset = stage == "before" ? 2 : 1;
            Assert.Equal(capture, steps[^offset].GetProperty("postCaptureId").GetString());
            Assert.Equal(capture, captures[^offset].GetProperty("captureId").GetString());
            Assert.Equal("PostActionEffectFlow", captures[^offset].GetProperty("context").GetString());
            var parsed = UiAutomatorDump.ParseHierarchyObservation(Read(source),
                new(capture, source.GetProperty("replayTime").GetDateTimeOffset(), "offline-replay-context", "sim012",
                    corpus.GetProperty("replayAndroidApiLevel").GetInt32(), ObservationCycleId: capture,
                    Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities,
                    Space: CoordinateSpace.DeviceViewport(corpus.GetProperty("width").GetInt32(),
                        corpus.GetProperty("height").GetInt32(), captureId: capture)));
            Assert.NotNull(parsed.Observation); Assert.True(parsed.Observation.IsValid);
            var ledger = new EvidenceLedger(); var strategy = new UiHierarchyOccurrenceStrategy();
            IReadOnlyList<ProposedOccurrence> candidates = Array.Empty<ProposedOccurrence>();
            foreach (var proposal in TypedHierarchyProposalProjector.Project(parsed.Observation, ObservationContext.External))
            {
                var (admission, record) = ledger.Admit(proposal);
                Assert.Equal(AdmissionDecision.Accepted, admission.Decision); Assert.NotNull(record);
                candidates = strategy.Derive(record, null);
            }
            var frozen = Read(c.GetProperty("imported").GetProperty(stage));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(frozen), JsonSerializer.SerializeToNode(
                new { observation = parsed.Observation, candidates }, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
            var title = c.GetProperty("identityInput").GetProperty(stage).GetProperty("title").GetString();
            Assert.Contains(parsed.Observation.Nodes, n => n.Text.State == FieldState.Observed && n.Text.Value == title
                || n.ContentDescription.State == FieldState.Observed && n.ContentDescription.Value == title);
            Assert.All(candidates, o => Assert.Null(o.OwningContainerId));
            Assert.All(candidates, o => Assert.True(o.SupportingEvidenceIds is null or { Count: 0 }));
        }
    }
}
