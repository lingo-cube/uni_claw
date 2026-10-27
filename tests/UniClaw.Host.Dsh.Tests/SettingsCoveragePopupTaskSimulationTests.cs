using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

public sealed class SettingsCoveragePopupTaskSimulationTests
{
    [Fact]
    public void SameActivityPopupArrivalIsConsumedAfterCancelInOneRunner()
    {
        var source = new Source(withWindowEvidence: true);
        var root = Path.Combine(Path.GetTempPath(), "sim025-" + Guid.NewGuid().ToString("N"));
        var responses = new Queue<string>(["SIM name & color", "CANCEL", "Other entry"]);
        try
        {
            var result = SettingsCoverageRunner.Run(root, new("synthetic-popup-device",
                new("synthetic-popup-device", "unused", "/unused", "/unused/python"),
                new("1", new("synthetic popup task", "offline", false, false),
                    new(4, 6, 4, 3, 1), new(true, "configured", 0, 0, false, 0), ["SIM name & color", "Other entry"],
                    new(true, true, true, true), Source.Route, "unused", null), context =>
                {
                    if (responses.Count == 0) return new AgentDecision.Blocked(context.DecisionId, "synthetic-task-bound");
                    return new AgentDecision.Act(new(context.DecisionId,
                        [new("ui.element", responses.Dequeue(), "tap", null)], "synthetic task response"));
                }, ViewportWidth: 100, ViewportHeight: 100, FeedNext: source.Next,
                CurrentCaptureId: () => source.CaptureId, EffectDriver: source));
            var output = Environment.GetEnvironmentVariable("SIM025_EVIDENCE_DIR");
            if (output is not null)
            {
                Directory.CreateDirectory(output);
                foreach (var path in Directory.GetFiles(result.RunDir)) File.Copy(path, Path.Combine(output, Path.GetFileName(path)), true);
            }
            Assert.Contains(result.Consults, c => c.DirectiveKind == "arrival-detected");
            Assert.Equal(new[] { "SIM name & color", "CANCEL", "Other entry" }, result.Steps.Select(s => s.TargetDescriptor));
            Assert.All(result.Steps, s => Assert.True(s.Verified));
            var cancel = Assert.Single(result.Steps, s => s.TargetDescriptor == "CANCEL");
            Assert.Contains(cancel.Checks, c => c.Key == "post-action-overlay-dismissed" && c.Value);
            Assert.Equal(3, source.Deliveries);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("match", true)]
    [InlineData("mismatch", false)]
    [InlineData("missing", false)]
    [InlineData("duplicate", false)]
    public void OnlyUniqueCurrentAcceptedMatchingTitleMarksArrival(string titleMode, bool expectedArrival)
    {
        var source = new Source(titleMode);
        var root = Path.Combine(Path.GetTempPath(), "sim025-title-" + Guid.NewGuid().ToString("N"));
        var responses = new Queue<string>(["SIM name & color", "CANCEL"]);
        try
        {
            var result = SettingsCoverageRunner.Run(root, new("synthetic-popup-device",
                new("synthetic-popup-device", "unused", "/unused", "/unused/python"),
                new("1", new("synthetic title projection", "offline", false, false),
                    new(4, 6, 4, 3, 1), new(true, "configured", 0, 0, false, 0), ["SIM name & color", "Other entry"],
                    new(true, true, true, true), Source.Route, "unused", null), context =>
                    responses.TryDequeue(out var target)
                        ? new AgentDecision.Act(new(context.DecisionId, [new("ui.element", target, "tap", null)], "synthetic title test"))
                        : new AgentDecision.Blocked(context.DecisionId, "synthetic title bound"),
                ViewportWidth: 100, ViewportHeight: 100, FeedNext: source.Next,
                CurrentCaptureId: () => source.CaptureId, EffectDriver: source));
            Assert.Equal(expectedArrival, result.Consults.Any(c => c.DirectiveKind == "arrival-detected"));
            Assert.Equal(expectedArrival, result.Report.ReachedTargets?.Contains("SIM name & color") == true);
            Assert.Equal(2, source.Deliveries);
            // This test asserts arrival projection only. Same-route CANCEL remains unverified.
            Assert.Contains(result.Steps, step => step.TargetDescriptor == "CANCEL" && !step.Verified);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class Source(string titleMode = "match", bool withWindowEvidence = false) : IEffectDriver
    {
        public const string Route = "synthetic:settings-same-activity";
        private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        private int _cycle;
        public int Deliveries { get; private set; }
        public string? CaptureId { get; private set; }
        public DispatchResult Deliver(DispatchRequest request)
        {
            Assert.Equal("tap", request.EffectClass);
            Deliveries++;
            return new(DispatchOutcome.DeliveryCompleted, "synthetic delivery", Time.AddSeconds(_cycle));
        }
        public RunDriverInput? Next(ObservationDirective directive)
        {
            if (withWindowEvidence && directive.Need is { } need)
            {
                if (need.Kind == OverlayObservationCodec.NeedKind) return null;
                var identity = new PageIdentityObservation(need.Capture.CaptureId, need.Capture.ObservationCycleId,
                    need.Capture.SessionCorrelation, need.Capture.CaptureTime, "synthetic-same-activity", "Settings");
                return new RunDriverInput.Observation([new ObservationProposal(
                    new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(identity)), IngressKind.Observation,
                    directive.Context, new("synthetic-popup-source", need.Capture.CaptureTime,
                        need.Capture.CaptureId + ":identity", ["synthetic", "offline"]))
                {
                    DerivedFromEvidenceIds = [need.CaptureEvidenceId],
                    TransitiveEvidenceBasis = new HashSet<string> { need.CaptureEvidenceId },
                    FusionRule = "synthetic-popup-identity", FusionVersion = "1",
                }]);
            }
            if (++_cycle > 12) return new RunDriverInput.Cancel("synthetic bound", Time.AddSeconds(_cycle));
            var popup = Deliveries == 1;
            var id = CaptureId = "synthetic-popup-" + _cycle;
            var time = Time.AddSeconds(_cycle);
            var title = titleMode == "mismatch" ? "Battery optimization" : "SIM name &amp; color";
            var titleNode = titleMode == "missing" ? "" : $"<node index='3' text='{title}' resource-id='android:id/alertTitle' class='android.widget.TextView' package='com.android.settings' bounds='[10,60][90,75]'/>";
            if (titleMode == "duplicate") titleNode += $"<node index='5' text='{title}' resource-id='synthetic:id/alertTitle' class='android.widget.TextView' package='com.android.settings' bounds='[10,60][90,75]'/>";
            var xml = "<hierarchy>" +
                "<node index='0' text='Settings' resource-id='synthetic:id/title' class='android.widget.TextView' package='com.android.settings' bounds='[0,0][100,20]'/>" +
                "<node index='1' text='SIM name &amp; color' resource-id='synthetic:id/sim' class='android.widget.TextView' package='com.android.settings' clickable='true' enabled='true' bounds='[0,20][100,40]'/>" +
                "<node index='2' text='Other entry' resource-id='synthetic:id/other' class='android.widget.TextView' package='com.android.settings' clickable='true' enabled='true' bounds='[0,40][100,60]'/>" +
                (popup ? (withWindowEvidence ? "<node resource-id='android:id/parentPanel' class='android.widget.LinearLayout' package='com.android.settings' bounds='[10,10][90,95]'>" : "") +
                    titleNode + "<node index='4' text='CANCEL' resource-id='android:id/button2' class='android.widget.Button' package='com.android.settings' clickable='true' enabled='true' bounds='[10,75][90,95]'/>" +
                    (withWindowEvidence ? "</node>" : "") : "") + "</hierarchy>";
            ObservationProposal Proposal(string subject, string value) => new(new(subject, value), IngressKind.Observation,
                directive.Context, new("synthetic-popup-source", time, id + ":" + subject, ["synthetic", "offline"]));
            var parsed = UiAutomatorDump.ParseHierarchyObservation(xml, new(id, time, "synthetic-popup-device", "synthetic-popup-session", 35,
                ObservationCycleId: id, Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities,
                Space: CoordinateSpace.DeviceViewport(100, 100, captureId: id)));
            var proposals = new List<ObservationProposal>
            {
                Proposal(ProductAssociationStrategy.ScreenIdentitySubject, "synthetic-same-activity"),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, Route),
                Proposal(ProductAssociationStrategy.PopupSubject, popup ? "present" : "absent"),
            };
            if (withWindowEvidence)
            {
                // Synthetic acquisition input; production classification and Assurance decide the outcome.
                var items = new List<SemanticLayoutItem>
                {
                    new("title", "text", "Settings", new(0, 0, 100, 20)),
                    new("sim", "ui.element", "SIM name & color", new(0, 20, 100, 40)),
                    new("other", "ui.element", "Other entry", new(0, 40, 100, 60)),
                };
                if (popup)
                {
                    items.Add(new("popup-title", "text", "SIM name & color", new(10, 60, 90, 75)));
                    items.Add(new("cancel", "ui.element", "CANCEL", new(10, 75, 90, 95)));
                }
                var capture = new PageCaptureObservation(id, id, "synthetic-popup-session", time,
                    CoordinateSpace.DeviceViewport(100, 100, captureId: id), items)
                {
                    DeviceId = "synthetic-popup-device", RequiresOverlayEvidence = true,
                    Coverage = new(CoverageCompleteness.Partial, "synthetic viewport; offscreen content unproven"),
                };
                var classified = SettingsPopupClassifier.ClassifyWithWindows(xml, Windows(popup), PopupClearanceConfig.DefaultPopupResourceIds);
                proposals = [Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
                    Proposal(ProductAssociationStrategy.ScreenRouteSubject, Route),
                    Proposal(OverlayObservationCodec.Subject, OverlayObservationCodec.Encode(new(id, id,
                        capture.SessionCorrelation, time, classified.State, "synthetic: " + classified.Reason)
                        { WindowEvidence = classified.WindowEvidence }))];
            }
            proposals.AddRange(TypedHierarchyProposalProjector.Project(parsed.Observation!, directive.Context, "synthetic-popup-source"));
            return new RunDriverInput.Observation(proposals);
        }

        private static SettingsWindowEvidence Windows(bool popup)
        {
            static string Block(int index, string id, string type, string bounds) => $"  Window #{index} Window{{{id}}}:\n    package=com.android.settings ty={type} mHasSurface=true mToken=ActivityRecord{{synthetic-settings}}\n    isVisible=true\n    isOnScreen=true\n    Frames: frame={bounds}\n\n";
            var windows = "WINDOW MANAGER WINDOWS\n" + Block(0, "base-settings", "BASE_APPLICATION", "[0,0][100,100]")
                + (popup ? Block(1, "overlay-sim", "APPLICATION", "[10,10][90,95]") : "")
                + "  Animation settings:\n  mSystemBooted=true mDisplayEnabled=true\n";
            var displays = "WINDOW MANAGER DISPLAY CONTENTS\n  Display: mDisplayId=0\n  cur=100x100\n  mCurrentFocus=Window{" + (popup ? "overlay-sim" : "base-settings") + "}\n";
            return new(windows, displays, windows, displays, 35);
        }
    }
}
