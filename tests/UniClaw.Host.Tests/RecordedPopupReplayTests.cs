using System.Text.Json;
using UniClaw.Host.Tests.PopupReplay;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RecordedPopupReplayTests
{
    public static IEnumerable<object[]> Cases() => RecordedPopupEpisodes.Cases();

    [Theory, MemberData(nameof(Cases)), Trait("Scenario", "SCN-REPLAY-010")]
    public void OriginalFramesAreExtractedInOrder_AndKnownMismatchRemainsVisible(string id)
    {
        var guard = RecordedPopupEpisodes.Guard();
        var episode = guard.Create(RecordedPopupEpisodes.Root, () => RecordedPopupEpisodes.Extract(id));
        using var oracle = JsonDocument.Parse(File.ReadAllText(RecordedPopupEpisodes.Fixture("oracle.json")));
        var expected = oracle.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        Assert.Equal(expected.GetProperty("frameCount").GetInt32(), episode.Frames.Length);
        Assert.Equal(expected.GetProperty("triggerTarget").GetString(), episode.Trigger.GetProperty("targetDescriptor").GetString());
        Assert.Equal("DeliveryCompleted", episode.Trigger.GetProperty("receiptOutcome").GetString());
        var states = episode.Frames.Select(f => RecordedPopupEpisodes.Classify(f.Xml)).ToArray();
        Assert.Equal(episode.Frames.Select(f => f.RecordedPopup), states); // fidelity to original observations, not semantic correctness
        var desired = expected.GetProperty("popupAtTrigger").GetBoolean();
        var mismatch = desired != (states[1] == "present");
        Assert.Equal(expected.GetProperty("knownMismatch").GetBoolean(), mismatch);
        Assert.False(episode.RecordedClearance); Assert.Contains("recorded CANCEL action/receipt", episode.Missing);
        Assert.Throws<InvalidDataException>(() => RecordedPopupEpisodes.Extract(id, requireRecordedClearance: true));
        RecordedPopupEpisodes.Export("extracted-" + id, new { scope = "recorded prefix; original CANCEL/clearance not captured", episode,
            states, desiredPopupAtTrigger = desired, firstDivergence = mismatch ? "host.popup.classification" : null,
            semanticStatus = mismatch ? "KNOWN_FAILURE_UNRESOLVED_SIM017" : "fixture-matches", guard.ManifestSha256, guard.Pins });
    }

    [Fact, Trait("Scenario", "SCN-REPLAY-010")]
    public void MissingXmlFaultMakesNoPopupClaim_ThenSameRecordedInputRecovers()
    {
        var guard = RecordedPopupEpisodes.Guard(); var episode = guard.Create(RecordedPopupEpisodes.Root, () => RecordedPopupEpisodes.Extract("sim-dialog"));
        var frame = episode.Frames[1]; var original = frame.Xml; var fault = new PopupReplayInjection("drop-once");
        Assert.Null(RecordedPopupEpisodes.Classify(fault.Deliver(frame)));
        Assert.Equal("present", RecordedPopupEpisodes.Classify(fault.Deliver(frame)));
        Assert.Equal(original, frame.Xml); Assert.Single(fault.Receipts);
        RecordedPopupEpisodes.Export("missing-xml", new { scope = "injected missing input; not a recorded acquisition failure", fault = fault.Receipts, restored = "present" });
    }
}
