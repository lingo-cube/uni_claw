using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Host.Tests.PopupReplay;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RecordedPopupRuntimeReplayTests
{
    private const string Route = "android.settings|route:Replay";
    private static readonly DateTimeOffset ReplayTime = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    private sealed class Feed(RecordedPopupEpisode episode, string mode) : IEffectDriver
    {
        private int _cycles;
        private readonly PopupReplayInjection _fault = new(mode == "held" ? "hold-after-delivery" : "none");
        internal int Deliveries { get; private set; }
        private int _successfulDeliveries;
        internal string? CurrentCaptureId { get; private set; }
        internal string? LastPopup { get; private set; }
        internal List<object> DeliveredFrames { get; } = [];
        internal IReadOnlyList<PopupInjectionReceipt> Faults => _fault.Receipts;
        public DispatchResult Deliver(DispatchRequest request)
        {
            Assert.Equal("tap", request.EffectClass); Assert.NotNull(request.Target.Spatial);
            Deliveries++;
            if (mode != "failed") _successfulDeliveries++;
            return new(mode == "failed" ? DispatchOutcome.DeliveryFailed : DispatchOutcome.DeliveryCompleted, "controlled replay delivery; not original CANCEL receipt", ReplayTime.AddSeconds(_cycles));
        }
        internal RunDriverInput? Next(ObservationDirective directive)
        {
            if (++_cycles > 12) return new RunDriverInput.Cancel("replay-fixture-bound", ReplayTime.AddSeconds(_cycles));
            // The source has no recorded CANCEL or post-clearance frame. For the
            // successful branch only, splice the recorded pre-popup layout under
            // a new, explicitly controlled post-action envelope.
            var afterDelivery = _successfulDeliveries > 0;
            var normal = episode.Frames[afterDelivery ? 0 : 1];
            var source = _fault.SelectFrame(normal, episode.Frames[1], afterDelivery);
            var cleared = afterDelivery && ReferenceEquals(source, episode.Frames[0]);
            var xml = _fault.Deliver(source);
            var capture = CurrentCaptureId = "popup-replay-" + _cycles;
            var time = ReplayTime.AddSeconds(_cycles); var cycle = "popup-cycle-" + _cycles;
            var context = directive.Context;
            var proposals = new List<ObservationProposal>();
            var tags = new[] { "recorded-xml-layout", "controlled-route-identity", "synthetic-replay-clock", "source-capture:" + source.CaptureId };
            ObservationProposal Proposal(string subject, string value) => new(new(subject, value), IngressKind.Observation,
                context, new("host.replay.settings", time, "scope:" + subject + ":" + cycle, tags));
            var route = cleared ? Route : Route + ":nested";
            proposals.Add(Proposal(ProductAssociationStrategy.ScreenIdentitySubject, "controlled-popup-replay-page:" + route));
            proposals.Add(Proposal(ProductAssociationStrategy.ScreenRouteSubject, route));
            var parsed = UiAutomatorDump.ParseHierarchyObservation(xml, new(capture, time, "offline-popup", "sim016", 35,
                ObservationCycleId: cycle, Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities,
                Space: CoordinateSpace.DeviceViewport(source.Width, source.Height, captureId: capture)));
            if (parsed.Observation is { } observation)
                proposals.AddRange(TypedHierarchyProposalProjector.Project(observation, context));
            LastPopup = RecordedPopupEpisodes.Classify(xml);
            if (LastPopup is { } popup) proposals.Add(Proposal(ProductAssociationStrategy.PopupSubject, popup));
            DeliveredFrames.Add(new { capture, cycle, context = context.ToString(), source.CaptureId, source.SourceCycleId, source.SourceContext, source.XmlSha256,
                popup = LastPopup, stage = cleared ? "controlled post-CANCEL splice; not original clearance" : "recorded popup layout" });
            return new RunDriverInput.Observation(proposals);
        }
    }

    [Fact, Trait("Scenario", "SCN-REPLAY-011")]
    public void RecordedPopupClears_ThenControlledTaskResumes() => Execute("clears", "SCN-REPLAY-011");

    [Fact, Trait("Scenario", "SCN-REPLAY-012")]
    public void InjectedPopupPersists_RealRunnerStopsWithinObstacleBudget() => Execute("held", "SCN-REPLAY-012");

    [Fact, Trait("Scenario", "SCN-REPLAY-013")]
    public void InjectedDeliveryFailure_PreservesUnconfirmedBarrierWithoutRedispatch() => Execute("failed", "SCN-REPLAY-013");

    private static void Execute(string mode, string scenarioId)
    {
        var guard = RecordedPopupEpisodes.Guard();
        var episode = guard.Create(RecordedPopupEpisodes.Root, () => RecordedPopupEpisodes.Extract("sim-dialog"));
        var feed = new Feed(episode, mode);
        var config = new SettingsCoverageConfig("1", new("popup replay", "offline", false, false),
            new(8, 16, 0, 3, 1), new(true, "configured", 0, 0, false, 0), [],
            new(true, true, true, true), Route, "unused-scroll", null,
            Popup: new(MaxObstacleRetries: 2));
        var runRoot = Path.Combine(Path.GetTempPath(), "sim016-popup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = SettingsCoverageRunner.Run(runRoot, new("offline-popup",
                new("offline-popup", "unused", "/nonexistent", "/nonexistent/python", "/nonexistent/cache"), config,
                context => new AgentDecision.Act(new AgentActionProposal(context.DecisionId,
                    [new AgentActionStep("ui.element", "CANCEL", "tap", null)], Justification: "controlled-popup-clearance")),
                ViewportWidth: episode.Frames[1].Width, ViewportHeight: episode.Frames[1].Height,
                FeedNext: feed.Next, CurrentCaptureId: () => feed.CurrentCaptureId, EffectDriver: feed));
            RecordedPopupEpisodes.Export("runtime-" + mode, new { scope = "real Host runner/kernel; recorded popup layout; synthetic Agent/delivery/post-action branch; controlled route/identity/replay clock",
                episode.RecordedClearance, episode.Missing, feed.Deliveries, feed.LastPopup, feed.DeliveredFrames,
                fault = feed.Faults, result.Status, result.Reason, result.Consults, result.Steps, result.Report,
                guard.ManifestSha256, guard.Pins });
            Assert.NotEmpty(result.Consults.Where(c => c.DirectiveKind == "obstacle"));
            Assert.All(result.Steps, step => Assert.Equal("CANCEL", step.TargetDescriptor));
            Assert.All(result.Steps, step => Assert.Equal(mode == "failed" ? "DeliveryFailed" : "DeliveryCompleted", step.ReceiptOutcome));
            Assert.False(episode.RecordedClearance); Assert.Contains("recorded post-CANCEL frame", episode.Missing);
            using var oracle = JsonDocument.Parse(File.ReadAllText(RecordedPopupEpisodes.Fixture("oracle.json")));
            var expected = oracle.RootElement.GetProperty("runtime").GetProperty(mode);
            Assert.Equal(expected.GetProperty("status").GetString(), result.Status.ToString());
            Assert.Equal(expected.GetProperty("consultations").GetInt32(), result.Consults.Count);
            using var scene = JsonDocument.Parse(File.ReadAllText(Path.Combine(RecordedPopupEpisodes.Root, "scenarios", scenarioId + ".json")));
            var annotation = scene.RootElement.GetProperty("expectations");
            Assert.Equal(result.Status.ToString(), annotation.GetProperty("status").GetString());
            Assert.Equal(feed.Deliveries, annotation.GetProperty("effects").GetInt32());
            Assert.Equal(result.Consults.Count, annotation.GetProperty("agentConsultations").GetInt32());
            if (mode == "clears")
            {
                Assert.Equal(expected.GetProperty("deliveries").GetInt32(), feed.Deliveries);
                Assert.Equal(expected.GetProperty("popupAfter").GetString(), feed.LastPopup);
                Assert.Empty(feed.Faults);
                Assert.Equal("CoverageComplete", result.Report.Status);
                Assert.Empty(result.Report.UncoveredItems);
                Assert.Contains(result.Consults, c => c.Justification == "coverage-complete");
                Assert.DoesNotContain(result.Consults, c => c.Justification == "bounded-stop:popup-not-cleared");
            }
            else if (mode == "held")
            {
                Assert.NotEqual("CoverageComplete", result.Report.Status);
                Assert.NotEmpty(result.Report.UncoveredItems);
                Assert.InRange(feed.Deliveries, 1, expected.GetProperty("maxDeliveries").GetInt32());
                Assert.Equal("present", feed.LastPopup); Assert.NotEmpty(feed.Faults);
                Assert.Contains(result.Consults, c => c.Justification == expected.GetProperty("stopReason").GetString());
                Assert.DoesNotContain(result.Consults, c => c.DirectiveKind is "enter" or "re-enter" or "scroll" or "back");
            }
            else
            {
                Assert.Equal(expected.GetProperty("deliveries").GetInt32(), feed.Deliveries);
                Assert.Equal("present", feed.LastPopup);
                Assert.NotEqual("CoverageComplete", result.Report.Status);
                Assert.NotEmpty(result.Report.UncoveredItems);
            }
        }
        finally { if (Directory.Exists(runRoot)) Directory.Delete(runRoot, recursive: true); }
    }
}
