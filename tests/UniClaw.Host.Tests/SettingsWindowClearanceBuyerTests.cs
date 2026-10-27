using System.Text.Json;
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

namespace UniClaw.Host.Tests;

// The source/Agent/delivery are synthetic. Runner, acquisition classifier,
// Evidence, World, Control, grounding, Effect Boundary and Assurance are real.
public sealed partial class SettingsWindowClearanceBuyerTests
{
    [Theory]
    [InlineData("same-route", true)]
    [InlineData("route-change", true)]
    [InlineData("persistent", false)]
    [InlineData("delivery-failed", false)]
    [InlineData("missing-overlay", false)]
    [InlineData("late-overlay", false)]
    [InlineData("repeated-frame", false)]
    [InlineData("conflict", false)]
    [InlineData("partial-windows", false)]
    [InlineData("unknown-windows", false)]
    [InlineData("pre-partial-windows", false)]
    [InlineData("pre-unknown-windows", false)]
    [InlineData("wrong-base", false)]
    [InlineData("other-popup", false)]
    [InlineData("unrelated-target", false)]
    [InlineData("no-window-evidence", false)]
    public void QualifiedWindowObservationControlsClearanceVerification(string mode, bool verified)
    {
        var source = new WindowSource(mode);
        var root = Path.Combine(Path.GetTempPath(), "sim025-clearance-" + Guid.NewGuid().ToString("N"));
        var responses = new Queue<string>(["SIM name & color", "CANCEL", "Other entry"]);
        try
        {
            var result = SettingsCoverageRunner.Run(root, new("synthetic-window-device",
                new("synthetic-window-device", "unused", "/unused", "/unused/python"),
                new("1", new("synthetic window clearance", "offline", false, false),
                    new(4, 6, 4, 3, 1), new(true, "configured", 0, 0, false, 0), ["SIM name & color", "Other entry"],
                    new(true, true, true, true), WindowSource.Route, "unused", null), context =>
                {
                    if (context.Phase == AgentDecisionPhase.VerificationFailed || !responses.TryDequeue(out var target))
                        return new AgentDecision.Blocked(context.DecisionId, "synthetic-window-task-bound");
                    return new AgentDecision.Act(new(context.DecisionId, [new("ui.element", target, "tap", null)], "synthetic window task"));
                }, ViewportWidth: 100, ViewportHeight: 100, FeedNext: source.Next,
                CurrentCaptureId: () => source.CaptureId, EffectDriver: source));
            Export(mode, source, result);
            Assert.Contains(result.Consults, c => c.DirectiveKind == "arrival-detected");
            Assert.Contains("SIM name & color", result.Report.ReachedTargets ?? []);
            var cancel = Assert.Single(result.Steps, s => s.TargetDescriptor == "CANCEL");
            Assert.Equal(verified, cancel.Verified);
            Assert.Equal(verified ? 3 : 2, source.Deliveries);
            Assert.All(result.Steps, s => Assert.Contains(s.TargetDescriptor, new[] { "SIM name & color", "CANCEL", "Other entry" }));
            if (verified)
            {
                Assert.Contains(cancel.Checks, c => c.Key == "post-action-overlay-dismissed" && c.Value);
                Assert.Equal(new[] { "SIM name & color", "CANCEL", "Other entry" }, result.Steps.Select(s => s.TargetDescriptor));
                Assert.All(result.Steps, s => Assert.True(s.Verified));
                Assert.DoesNotContain(result.Consults.SkipWhile(c => c.DirectiveKind != "arrival-detected").Skip(1),
                    c => c.DirectiveKind == "enter" && c.Directive?.Contains("<SIM name & color>", StringComparison.Ordinal) == true);
            }
            else Assert.DoesNotContain(result.Steps, s => s.TargetDescriptor == "Other entry");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class WindowSource(string mode) : IEffectDriver
    {
        public const string Route = "synthetic:window-settings";
        private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        private PageCaptureObservation? _capture;
        private PageCaptureObservation? _popupCapture;
        private int _captures;
        public string? CaptureId => _capture?.CaptureId;
        public int Deliveries { get; private set; }
        public List<object> Observations { get; } = [];
        public DispatchResult Deliver(DispatchRequest request)
        {
            Assert.Equal("tap", request.EffectClass);
            Deliveries++;
            return new(Deliveries == 2 && mode == "delivery-failed" ? DispatchOutcome.DeliveryFailed : DispatchOutcome.DeliveryCompleted,
                "synthetic qualified-window delivery", _capture!.CaptureTime.AddMilliseconds(100));
        }
        public RunDriverInput? Next(ObservationDirective directive)
        {
            if (directive.Need is { } need)
            {
                if (need.Kind == OverlayObservationCodec.NeedKind) return null;
                return new RunDriverInput.Observation([Proposal(PageObservationCodec.IdentitySubject,
                    PageObservationCodec.Encode(new PageIdentityObservation(need.Capture.CaptureId,
                        need.Capture.ObservationCycleId, need.Capture.SessionCorrelation, need.Capture.CaptureTime,
                        "synthetic-window-settings", "Settings")), directive.Context) with
                {
                    DerivedFromEvidenceIds = [need.CaptureEvidenceId],
                    TransitiveEvidenceBasis = new HashSet<string> { need.CaptureEvidenceId },
                    FusionRule = "synthetic-window-identity", FusionVersion = "1",
                }]);
            }
            if (++_captures > 8) return new RunDriverInput.Cancel("synthetic-window-bound", Start.AddMinutes(1));
            var afterCancel = Deliveries >= 2;
            var popup = Deliveries == 1 || afterCancel && mode is "persistent" or "other-popup";
            var xml = Xml(popup);
            var id = "synthetic-window-" + _captures;
            var time = Start.AddSeconds(_captures * 2);
            _capture = PageIdentityRecovery.Capture(id, "cycle:" + id, "synthetic-window-session",
                time, 100, 100, xml, null) with
                { DeviceId = "synthetic-window-device", RequiresOverlayEvidence = true };
            Assert.Equal(CoverageCompleteness.Partial, _capture.Coverage!.Completeness);
            if (Deliveries == 1) _popupCapture = _capture;
            if (afterCancel && mode == "repeated-frame")
            { _capture = _popupCapture!; xml = Xml(true); popup = true; }
            var baseId = afterCancel && mode == "wrong-base" ? "base-replaced" : "base-settings";
            var overlayId = afterCancel && mode == "other-popup" ? "overlay-unrelated" : "overlay-sim";
            var windows = Windows(popup, baseId, overlayId);
            var classified = SettingsPopupClassifier.ClassifyWithWindows(xml, windows, PopupClearanceConfig.DefaultPopupResourceIds);
            if (afterCancel && mode is "partial-windows" or "unknown-windows"
                || Deliveries == 1 && mode is "pre-partial-windows" or "pre-unknown-windows")
                classified = classified with { WindowEvidence = classified.WindowEvidence! with
                    { VisibleWindowCoverage = mode is "partial-windows" or "pre-partial-windows"
                        ? new(CoverageCompleteness.Partial, "synthetic missing visible-window region")
                        : new(CoverageCompleteness.Unknown) } };
            Assert.Equal(popup ? "present" : "absent", classified.State);
            var typed = UiAutomatorDump.ParseHierarchyObservation(xml, new(_capture.CaptureId, _capture.CaptureTime,
                "synthetic-window-device", _capture.SessionCorrelation, 35,
                ObservationCycleId: _capture.ObservationCycleId, Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities,
                Space: _capture.Space));
            var route = popup && mode == "route-change" ? Route + ":dialog" : Route;
            var proposals = new List<ObservationProposal>
            {
                Proposal(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(_capture), directive.Context),
                Proposal(ProductAssociationStrategy.ScreenRouteSubject, route, directive.Context),
            };
            proposals.AddRange(TypedHierarchyProposalProjector.Project(typed.Observation!, directive.Context, "synthetic-window-hierarchy"));
            if (!(afterCancel && mode == "missing-overlay"))
            {
                var c = afterCancel && mode == "late-overlay" ? _popupCapture! : _capture;
                var observation = new OverlayObservation(c.CaptureId, c.ObservationCycleId, c.SessionCorrelation,
                    c.CaptureTime, classified.State, "synthetic: " + classified.Reason)
                    { WindowEvidence = mode == "no-window-evidence" ? null : classified.WindowEvidence };
                proposals.Add(Proposal(OverlayObservationCodec.Subject, OverlayObservationCodec.Encode(observation), directive.Context));
                if (afterCancel && mode == "conflict") proposals.Add(Proposal(OverlayObservationCodec.Subject,
                    OverlayObservationCodec.Encode(observation with { State = "present", WindowEvidence = null }), directive.Context));
            }
            Observations.Add(new { _capture.CaptureId, _capture.CaptureTime, route, classified, xml,
                synthetic = true, context = directive.Context.ToString() });
            return new RunDriverInput.Observation(proposals);
        }
        private ObservationProposal Proposal(string subject, string value, ObservationContext context) => new(new(subject, value),
            IngressKind.Observation, context, new("synthetic-window-source", _capture!.CaptureTime,
                _capture.CaptureId + ":" + subject, ["synthetic", "offline", "API35 window protocol"]));
        private string Xml(bool popup) => "<hierarchy>" +
            "<node text='Settings' resource-id='synthetic:id/title' class='android.widget.TextView' package='com.android.settings' bounds='[0,0][100,20]'/>" +
            "<node text='SIM name &amp; color' resource-id='synthetic:id/sim' class='android.widget.TextView' package='com.android.settings' clickable='true' enabled='true' bounds='[0,20][100,40]'/>" +
            "<node text='Other entry' resource-id='synthetic:id/other' class='android.widget.TextView' package='com.android.settings' clickable='true' enabled='true' bounds='[0,40][100,60]'/>" +
            (popup ? "<node resource-id='android:id/parentPanel' class='android.widget.LinearLayout' package='com.android.settings' bounds='[10,10][90,95]'>" +
                "<node text='SIM name &amp; color' resource-id='android:id/alertTitle' class='android.widget.TextView' package='com.android.settings' bounds='[10,10][90,30]'/>" +
                (mode == "unrelated-target" ? "</node>" : "") +
                "<node text='CANCEL' resource-id='android:id/button2' class='android.widget.Button' package='com.android.settings' clickable='true' enabled='true' bounds='[10,75][90,95]'/>" +
                (mode == "unrelated-target" ? "" : "</node>") : "") + "</hierarchy>";
        private static SettingsWindowEvidence Windows(bool popup, string baseId, string popupId)
        {
            static string Block(int index, string id, string type, string bounds) => $"  Window #{index} Window{{{id}}}:\n    package=com.android.settings ty={type} mHasSurface=true mToken=ActivityRecord{{synthetic-settings}}\n    isVisible=true\n    isOnScreen=true\n    Frames: frame={bounds}\n\n";
            var text = "WINDOW MANAGER WINDOWS\n" + Block(0, baseId, "BASE_APPLICATION", "[0,0][100,100]")
                + (popup ? Block(1, popupId, "APPLICATION", "[10,10][90,95]") : "")
                + "  Animation settings:\n  mSystemBooted=true mDisplayEnabled=true\n";
            var display = "WINDOW MANAGER DISPLAY CONTENTS\n  Display: mDisplayId=0\n  cur=100x100\n  mCurrentFocus=Window{" + (popup ? popupId : baseId) + "}\n";
            return new(text, display, text, display, 35);
        }
    }
    private static void Export(string mode, WindowSource source, SettingsCoverageRunner.RunResult result)
    {
        var root = Environment.GetEnvironmentVariable("SIM025_BUYER_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(root)) return;
        var dir = Path.Combine(root, mode); Directory.CreateDirectory(dir);
        foreach (var file in Directory.GetFiles(result.RunDir)) File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), true);
        File.WriteAllText(Path.Combine(dir, "synthetic-observations.json"), JsonSerializer.Serialize(source.Observations,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
